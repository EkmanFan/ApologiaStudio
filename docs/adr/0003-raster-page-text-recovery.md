# ADR 0003: Raster page text recovery

- Status: Accepted
- Date: 2026-09-03
- Decision owners: ApologiaStudio

## Context

The generic PDF extraction boundary established in Increment 2A
([PDF extraction v1](../knowledge-ingestion/pdf-extraction-v1.md)) is
deliberately scoped to born-digital PDF files with an extractable text layer.
`PdfPigDocumentExtractor` reads the text layer; it does not render pages and
does not attempt character recognition. A scanned or image-only page therefore
yields zero words.

[Docling spike v1](../knowledge-ingestion/docling-spike-v1.md) tested whether
Docling — a Python document-understanding library with an OCR pipeline —
provides enough additional value to justify adopting it here. The spike was
executed and reported its measurements, but deliberately stopped short of a
decision: *"This report is intentionally empirical. It does not decide that
Docling should replace the current pipeline."* This ADR takes that decision.

### Character recognition already exists upstream

Per the accepted
[Document Manager to Knowledge workflow](../knowledge-ingestion/document-manager-to-knowledge-workflow-v1.md),
DocumentProcessingEngine produces format-neutral documentary evidence and
Apologia Studio preserves, reviews and publishes it. Recognition is document
processing, and DPEngine implements it:

- `IDocumentPreflightAnalyzer` classifies a source as `HealthyBornDigital`,
  `Hybrid`, `RasterOrScanned` or `Problematic`;
- `NativeTextStatus` (`Missing`, `Healthy`, `Suspicious`, `Unverified`) drives
  whether recognition is needed for a page;
- three closed page routes exist — `NativeOnly`,
  `LayoutWithTargetedOcrRecovery` and `LayoutWithTargetedOcrReconciliation`;
- recognition runs through a PaddleOCR adapter and serving client, planned by
  the Engine rather than applied unconditionally.

Targeted OCR is therefore not future work in this system. It exists, is
evidence-driven, and is already owned upstream.

### What is missing is on the Apologia Studio side

The Knowledge Store model reserves an `ocr` value in both `artifact_type`
(`'raw', 'ocr', 'parsed', 'normalized'`) and `activity_type`
(`'download', 'ocr', 'parse', 'normalize', 'correct'`), and
`KnowledgeImportPackageValidator` accepts them. **Nothing produces an artifact
or activity of that type.** The provenance slot exists and is empty, because
the import path that would populate it — `AS-DM-06`, atomic
`KnowledgeImportPackage` persistence — is the next unstarted increment.

## Decision drivers

- Respecting the responsibility boundary already accepted between the two systems
- Not duplicating a capability that already exists and works upstream
- Keeping the Apologia Studio runtime free of a Python dependency
- ADR 0002's provenance model: artifacts are immutable and every material
  transformation is an auditable `ProcessingActivity`
- Evidence before popularity: adopt the richer tool only on demonstrated need

## Evidence

### Docling spike

Measured on 2026-08-13 with `docling[easyocr]==2.115.0`, CPU, against the
pinned real documents used by Increment 2D. Full results in
`docling-spike-summary.md` alongside the spike.

| Run | Pages | OCR | Text chars | Pages with text | Headings | Seconds |
|---|---|---|---:|---:|---:|---:|
| de-decretis-no-ocr | 512-561 | off | 156 946 | 50 | 2 | 25.5 |
| ehrman-text-no-ocr | 398-405 | off | 29 489 | 7 | 16 | 5.0 |
| ehrman-raster-no-ocr | 14-20 | off | 0 | 0 | 0 | 4.4 |
| ehrman-raster-full-ocr | 14-20 | full page | 12 393 | 7 | 4 | 41.0 |
| ehrman-hybrid-default-ocr | 1-10 | default | 11 147 | 9 | 17 | 50.4 |

Current Apologia Studio baseline on the same samples:

| Sample | Pages | Words | Text-layer coverage | Segments |
|---|---:|---:|---:|---:|
| deDecretis | 50 | 29 044 | 100 % | 50 |
| ehrmanTextSample | 8 | 4 728 | 87.5 % | 13 |
| ehrmanRasterSample | 7 | 0 | 0 % | 0 |
| ehrmanHybridSample | 10 | 384 | 20 % | 1 |

Three readings matter:

1. **On born-digital pages, Docling adds nothing decisive.** The existing
   pipeline already achieves full text-layer coverage on `deDecretis`.
   Docling's advantage there is structural, not textual.
2. **On raster pages, OCR is the whole difference.** Docling without OCR
   recovers exactly zero characters — the same result as the current pipeline.
   The gain is attributable to recognition, not to Docling's document model.
3. **Recognition is expensive.** 4.4 s without OCR against 41.0 s with it on
   the same 7 pages, roughly 5.9 s per page on CPU — about an hour for a
   617-page book. This matches the evidence-driven targeting DPEngine already
   implements, and rules out unconditional recognition.

### What crosses the publication boundary

The Manager publishes `DocumentProcessingResult`, schema
`document-processing-result-v4`. Its `ProcessingManifest`
(`DocumentProcessingManifest`, namespace `Provenance`) carries document-level
recognition provenance: `NativeExtraction`, `Rasterization`, `LayoutAnalysis`,
`Ocr` (a list of component identities), `Reconciliation`, plus assembly,
normalization and segmentation profile identifiers.

Per-element origin also exists upstream. `DocumentElementProvenance` carries
`TextOrigin` (`TextSelectionOrigin`: `None`, `Native`, `Ocr`), `OcrBackendId`,
`OcrProfileId`, `ReconciliationDecision` and `HasReconciliationDivergence`, and
it is serialized — but in `paged-document-processing-model-v1`, which is not
the published contract.

The published `DocumentElement` carries `ElementId`, `Ordinal`, `Kind`,
`Location`, `SegmentId`, `Text` and `TextSha256`, and states the omission
explicitly:

> *Processing-specific custody evidence is intentionally not copied into this
> first structural contract. It remains in the proven V1 provenance model until
> the later evidence migration.*

This is a sequenced migration owned by DPEngine, not an oversight.

## Decision

**Do not adopt Docling in Apologia Studio.** The measured advantage on the
corpus is attributable to OCR, which does not require Docling, and recognition
already exists upstream with a different engine. Adoption would add a Python
runtime, a model-licensing review, and a second document model competing with
both the accepted extraction boundary here and the accepted recognition
pipeline there.

**Character recognition stays a DocumentProcessingEngine responsibility.**
Apologia Studio does not grow an OCR capability. `PdfPigDocumentExtractor`
keeps its born-digital scope unchanged.

**Populate the reserved `ocr` artifact and activity from the published
manifest, as part of `AS-DM-06`.** `DocumentProcessingManifest.Ocr` and
`Reconciliation` identify the recognition components that participated and
their profiles. That is sufficient to record an immutable derived artifact and
its `ProcessingActivity` under ADR 0002's provenance model, whose citation
chain resolves `DocumentSegment → Artifact → Manifestation → Expression →
Work`. Artifact-level recognition provenance therefore satisfies the accepted
citation contract; it is not a stopgap.

**Treat recognized text as untrusted and never merge it into a `parsed`
artifact.** Recognition carries errors. A derived artifact produced with OCR
participation is recorded as `ocr`, so a reader can always tell that a source
involved recognition rather than pure text-layer extraction.

**Adopt element-level origin when DPEngine's evidence migration delivers it,
and not before.** Per-element `TextOrigin` would let a reader know whether a
specific quoted passage was recognized rather than read — a genuine
improvement in auditability, beyond what ADR 0002 requires today. Apologia
Studio must not anticipate it by inferring origin locally.

## Rejected alternatives

**Adopt Docling as the extraction pipeline.** Rejected: parity on born-digital
pages, and the raster advantage comes from recognition rather than from Docling
itself. Adoption would introduce a Python runtime into a .NET solution, a
second document model, and an unreviewed model-licensing question — substantial
operational complexity for a benefit already obtained upstream by other means.

**Add OCR inside Apologia Studio, next to PdfPig.** Rejected: it contradicts
the accepted responsibility boundary and would duplicate a working capability
in a second repository, with a second engine and a second quality baseline.

**Run recognition unconditionally on every ingested page.** Rejected on
measured cost, and contrary to the evidence-driven targeting already
implemented upstream.

**Infer element origin locally from the preflight classification, page range
or confidence.** Rejected: a `Hybrid` document mixes both origins within the
same page range, so any local inference would mislabel provenance in exactly
the case that motivates the distinction. Waiting for the upstream migration is
correct.

**Request an immediate contract change to carry element origin in v4.**
Rejected as premature: the omission is deliberate and sequenced upstream, and
artifact-level provenance already satisfies the citation model. Reopening the
published contract now would trade a working boundary for an improvement that
is already planned.

## Consequences

Positive:

- The Apologia Studio runtime stays .NET-only, with no Python dependency and no
  model-licensing exposure.
- The responsibility boundary between the two systems stays intact, and no
  capability is duplicated.
- The `ocr` artifact and activity types get a producer, so recognition
  participation becomes auditable instead of invisible.
- Recognition errors stay attributable, because OCR-derived artifacts are never
  confused with text-layer artifacts.

Negative and accepted:

- Provenance stays artifact-level until DPEngine's evidence migration lands.
  A citation can state that its source involved recognition, but not that a
  particular sentence was recognized.
- Populating the `ocr` artifact requires interpreting the manifest inside the
  payload that the inbox currently stores opaquely, which extends the import
  adapter beyond pure preservation.
- Sources imported before this decision takes effect carry no recognition
  provenance and will need re-import to gain it.
- Docling remains unadopted but not disproven for other purposes. Its
  structural heading recovery was measurably better on one sample, which may
  matter later for segmentation and is not addressed here.

## Implementation sequence

This decision carries no increment of its own. It constrains `AS-DM-06` and
`AS-DM-07` in
[Document Manager to Knowledge workflow](../knowledge-ingestion/document-manager-to-knowledge-workflow-v1.md).

1. In `AS-DM-06`, read `ProcessingManifest` from the stored payload while
   building the `KnowledgeImportPackage`, without weakening the inbox
   preservation guarantee.
2. Emit a derived `ocr` artifact and an `ocr` `ProcessingActivity` when the
   manifest reports recognition components, recording backend and profile
   identity, distinct from `parsed` artifacts.
3. Surface recognition participation in the editorial review draft, so a
   reviewer approving a source knows it involved OCR.
4. In `AS-DM-07`, carry the distinction into retrieval projections so a
   recognized source can be identified at citation rendering.
5. Revisit element-level origin only when DPEngine's evidence migration
   publishes it; at that point this ADR is superseded rather than amended.
