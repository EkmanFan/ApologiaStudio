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
yields zero words rather than an error.

[Docling spike v1](../knowledge-ingestion/docling-spike-v1.md) tested whether
Docling — a Python document-understanding library with an OCR pipeline —
provides enough additional value to justify adopting it here. The spike was
executed and reported its measurements, but deliberately stopped short of a
decision: *"This report is intentionally empirical. It does not decide that
Docling should replace the current pipeline."* This ADR takes that decision.

The decisive context is not in this repository. Per the accepted
[Document Manager to Knowledge workflow](../knowledge-ingestion/document-manager-to-knowledge-workflow-v1.md),
Document Manager produces format-neutral documentary evidence and Apologia
Studio preserves, reviews and publishes it. Character recognition is document
processing, and DocumentProcessingEngine **already implements it**:

- `IDocumentPreflightAnalyzer` classifies a source as `HealthyBornDigital`,
  `Hybrid`, `RasterOrScanned` or `Problematic`;
- `NativeTextStatus` distinguishes native text that is `Missing`, `Healthy`,
  or deterministically corrupt, and drives whether OCR verification is needed;
- OCR runs through a PaddleOCR adapter and serving client
  (`DocumentProcessing.Ocr.Adapters/PaddleOCR`);
- `TargetedHybridTextExecutor`, `MissingNativeHybridPageExecutor` and
  `NativePresentHybridPageExecutor` apply it per page, wired into
  `DocumentProcessingEngine`, with a dual-run harness for comparison.

So targeted OCR is not future work. It exists, is evidence-driven rather than
unconditional, and is already the upstream owner of this concern.

What is missing is on the Apologia Studio side of the contract. The Knowledge
Store model reserves an `ocr` value in both `artifact_type`
(`'raw', 'ocr', 'parsed', 'normalized'`) and `activity_type`
(`'download', 'ocr', 'parse', 'normalize', 'correct'`), and
`KnowledgeImportPackageValidator` accepts them, but **nothing produces an
artifact of that type**. The provenance slot exists and is empty.

## Decision drivers

- Respecting the responsibility boundary already accepted between the two systems
- Not duplicating a capability that already exists upstream
- Keeping the Apologia Studio runtime free of a Python dependency
- Citation-grade provenance: ADR 0002 requires explaining exactly which source
  supports a generated answer
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
   The gain is attributable to OCR, not to Docling's document model.
3. **OCR is expensive.** 4.4 s without OCR against 41.0 s with it on the same
   7 pages, roughly 5.9 s per page on CPU — about an hour for a 617-page book.
   This forbids running OCR unconditionally, and matches the evidence-driven
   targeting DocumentProcessingEngine already implements.

### Provenance actually crossing the boundary

`DocumentProcessingManifest` (namespace `Provenance`) is part of the published
result and carries document-level recognition provenance:
`NativeExtraction`, `Rasterization`, `LayoutAnalysis`, `Ocr` (a list of
component identities), `Reconciliation`, plus assembly, normalization and
segmentation profile identifiers.

`DocumentElement` — the unit that actually carries text — carries
`ElementId`, `Ordinal`, `Kind`, `Location`, `SegmentId`, `Text` and
`TextSha256`. **It has no origin marker.**

The consequence is precise: a consumer can establish that OCR participated in
producing a document, but cannot attribute any individual passage to a text
layer rather than to character recognition. For a system whose grounding
contract is per-citation, document-level attribution is not sufficient.

On the Apologia Studio side, `DocumentManagerResultClaim` carries identity,
scope, integrity (`SchemaVersion`, `MediaType`, `ByteLength`, `Sha256`) and an
opaque `Payload`. The manifest travels inside that payload; nothing at the
claim level exposes it, and the inbox stores the payload without interpreting it.

## Decision

**Do not adopt Docling in Apologia Studio.** The measured advantage on the
corpus is attributable to OCR, which does not require Docling, and character
recognition already exists upstream with a different engine. Adoption would add
a Python runtime, a model-licensing review, and a second document model
competing with both the accepted extraction boundary here and the accepted
recognition pipeline there.

**Character recognition stays a DocumentProcessingEngine responsibility.**
Apologia Studio does not grow an OCR capability. `PdfPigDocumentExtractor`
keeps its born-digital scope unchanged.

**Populate the reserved `ocr` artifact type from the published manifest.**
`DocumentProcessingManifest.Ocr` and `Reconciliation` are sufficient to record,
at document level, that recognition participated and which components ran. That
closes the empty-producer gap for the `ocr` artifact and `ocr` activity types.

**Treat recognized text as untrusted and separately attributed.** OCR output
carries recognition errors. It must never be merged into a `parsed` artifact as
if it had been read from a text layer.

**Element-level recognition attribution is required for citation grounding,
and is a contract gap to close upstream.** Until `DocumentElement` — or an
adjacent per-element provenance structure — states whether its text came from
native extraction or from recognition, Apologia Studio cannot honour ADR 0002's
citation contract for hybrid documents. Apologia Studio must not infer it
heuristically from page ranges or confidence scores.

## Rejected alternatives

**Adopt Docling as the extraction pipeline.** Rejected: parity on born-digital
pages, and the raster advantage comes from OCR rather than from Docling itself.
Adoption would introduce a Python runtime into a .NET solution, a second
document model, and an unreviewed model-licensing question — substantial
operational complexity for a benefit already obtained upstream by other means.

**Add OCR inside Apologia Studio, next to PdfPig.** Rejected: it contradicts
the accepted responsibility boundary and would duplicate a working capability
in a second repository, with a second engine and a second quality baseline.

**Run OCR unconditionally on every ingested page.** Rejected on measured cost,
and contrary to the evidence-driven targeting already implemented upstream.

**Infer element origin from the preflight classification or page range.**
Rejected: a `Hybrid` document mixes both origins within the same page range, so
any inference would silently mislabel provenance in exactly the case that
motivates the distinction.

**Do nothing and accept document-level attribution only.** Rejected: it
degrades ADR 0002's citation contract from "which passage supports this answer"
to "this book involved OCR somewhere", which is not auditable.

## Consequences

Positive:

- The Apologia Studio runtime stays .NET-only, with no Python dependency and no
  model-licensing exposure.
- The responsibility boundary between the two systems stays intact, and no
  capability is duplicated.
- The `ocr` artifact and activity types finally get a producer, so recognition
  participation becomes auditable rather than invisible.
- Recognition errors stay attributable, because OCR-derived artifacts are never
  confused with text-layer artifacts.

Negative and accepted:

- Provenance is only document-level until the element-level contract gap is
  closed. Citations from hybrid documents cannot yet state their origin, and
  this ADR creates a cross-repository dependency on that change.
- Consuming the manifest requires interpreting the payload the inbox currently
  stores opaquely, which extends the consumer beyond pure preservation.
- Documents already ingested are not retroactively re-attributed; they will
  need re-submission once element-level provenance exists.
- Docling remains unadopted but not disproven for other purposes. Its
  structural heading recovery was measurably better on one sample, which may
  matter later for segmentation and is not addressed here.

## Open questions

The element-level provenance representation belongs to
DocumentProcessingEngine and should get its own record there: whether origin
lives on `DocumentElement` or in an adjacent structure, whether confidence is
reported per element, and how a partially recognized element is represented.

Whether the published payload schema should expose recognition provenance at
the claim level, rather than only inside the payload, is a joint contract
question between the two systems.

## Implementation sequence

1. Interpret `DocumentProcessingManifest` from the stored payload during
   submission assembly, without altering the preservation guarantee of the inbox.
2. Emit an `ocr` artifact and `ocr` activity when the manifest reports
   recognition components, distinct from `parsed` artifacts.
3. Surface recognition participation in the editorial review draft, so a human
   approving a submission knows the source involved OCR.
4. Agree the element-level provenance contract with DocumentProcessingEngine.
5. Carry element origin through to retrieval projection and citation rendering
   once that contract exists.
