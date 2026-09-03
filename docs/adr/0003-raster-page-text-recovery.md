# ADR 0003: Raster page text recovery

- Status: Accepted
- Date: 2026-09-03
- Decision owners: ApologiaStudio

## Context

The generic PDF extraction boundary established in Increment 2A
([PDF extraction v1](../knowledge-ingestion/pdf-extraction-v1.md)) is
deliberately scoped to born-digital PDF files with an extractable text layer.
`PdfPigDocumentExtractor` reads the text layer; it does not render pages and
does not attempt character recognition.

That scope is correct for the documents it was designed against, but it leaves
a silent failure mode. A scanned or image-only page yields zero words rather
than an error. The page is ingested, segmented into nothing, and disappears
from retrieval without any signal that content was lost. Several real sources
in the corpus — notably scanned front matter and plates in academic works —
fall into this case.

The Knowledge Store model already anticipates the missing capability. Both
`artifact_type` and `activity_type` reserve an `ocr` value
(`artifact_type IN ('raw', 'ocr', 'parsed', 'normalized')`,
`activity_type IN ('download', 'ocr', 'parse', 'normalize', 'correct')`) and
`KnowledgeImportPackageValidator` accepts them. Nothing in the solution
currently produces an artifact of that type. The provenance slot exists and is
empty.

[Docling spike v1](../knowledge-ingestion/docling-spike-v1.md) tested whether
Docling — a Python document-understanding library with an OCR pipeline —
provides enough additional value to justify adopting it. The spike was
executed and reported its measurements, but it deliberately stopped short of a
decision: *"This report is intentionally empirical. It does not decide that
Docling should replace the current pipeline."* This ADR takes that decision.

## Decision drivers

- Closing a silent data-loss failure mode, not adding a feature
- Respecting the responsibility boundary already accepted between
  Document Manager and Apologia Studio
- Keeping the Apologia Studio runtime free of a Python dependency
- Reproducible, auditable provenance for every recovered character
- Proportional operational cost: OCR is expensive and must be targeted
- Evidence before popularity: adopt the richer tool only on demonstrated need

## Evidence

Measured on 2026-08-13 with `docling[easyocr]==2.115.0`, CPU, against the
pinned real documents already used by Increment 2D. Full results in
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
   Docling's advantage there is structural (16 headings recovered on the
   Ehrman text sample against 2 on De Decretis), not textual.
2. **On raster pages, OCR is the whole difference.** Docling without OCR
   recovers exactly zero characters — the same result as the current
   pipeline. With full-page OCR it recovers 12 393 characters across the same
   7 pages. The gain is attributable to OCR, not to Docling's document model.
3. **OCR is expensive.** The raster sample cost 4.4 s without OCR and 41.0 s
   with it, roughly 5.9 s per page on CPU. Extrapolated to a 617-page book
   that is around one hour of CPU time, which forbids running OCR
   unconditionally over whole corpora.

## Decision

**Do not adopt Docling in Apologia Studio.** The measured advantage on the
corpus is attributable to OCR, which does not require Docling, while adoption
would add a Python runtime, a model-licensing review, and a second document
model competing with the accepted extraction boundary.

**Close the raster gap through targeted OCR, produced upstream.** Per the
accepted [Document Manager to Knowledge workflow](../knowledge-ingestion/document-manager-to-knowledge-workflow-v1.md),
Document Manager produces format-neutral documentary evidence and Apologia
Studio preserves, reviews and publishes it. Character recognition is document
processing, therefore a Document Manager responsibility. Apologia Studio
consumes its output through the existing `ocr` artifact type rather than
growing an extraction capability of its own.

**Keep `PdfPigDocumentExtractor` unchanged and born-digital only.** Its scope
stays correct; what changes is that a page with no text layer must stop being
silent.

**Make text-layer coverage an explicit, observable signal.** Apologia Studio
must be able to state, per page, that no text layer was found, so that a
missing OCR artifact becomes visible during editorial review instead of
degrading retrieval invisibly.

OCR output is untrusted input. Recovered text carries recognition errors and
must be persisted as its own `ocr` artifact with its own provenance, never
merged into a `parsed` artifact as if it had been read from a text layer.

## Rejected alternatives

**Adopt Docling as the extraction pipeline.** Rejected: the evidence shows
parity on born-digital pages, and the raster advantage comes from OCR rather
than from Docling itself. Adoption would introduce a Python runtime or service
into a .NET solution, a second document model alongside the accepted
extraction boundary, and an unreviewed model-licensing question — substantial
operational complexity bought for a benefit obtainable otherwise.

**Add OCR inside Apologia Studio, next to PdfPig.** Rejected: it contradicts
the accepted responsibility boundary. Apologia Studio would then both produce
and preserve documentary evidence, which is precisely the coupling the
Document Manager contract was designed to avoid, and would duplicate a
capability in two repositories.

**Run OCR unconditionally on every ingested page.** Rejected on measured cost:
roughly 5.9 s per page on CPU, about an hour for a single 617-page book, to
re-recognise pages whose text layer is already complete and more accurate than
any OCR result.

**Do nothing and accept the gap.** Rejected: the failure is silent. A page
that yields no words is indistinguishable from a page that legitimately
contains none, so the corpus loses content without anyone being told.

## Consequences

Positive:

- The Apologia Studio runtime stays .NET-only, with no Python dependency and
  no model-licensing exposure.
- The responsibility boundary between the two systems stays intact.
- The `ocr` artifact and activity types finally have a producer, so recovered
  text is auditable and separable from text-layer extraction.
- Recognition errors remain attributable, because OCR text is never confused
  with text-layer text.

Negative and accepted:

- The raster gap is not closed by this ADR alone. It stays open in Apologia
  Studio until Document Manager delivers OCR artifacts, and this decision
  creates a cross-repository dependency on that work.
- Making coverage observable requires touching the extraction diagnostics and
  the editorial review surface.
- Documents already ingested with unreported raster pages are not
  retroactively repaired; they will need re-submission once OCR exists.
- Docling remains unadopted but not disproven for other purposes. Its
  structural heading recovery was measurably better on one sample, which may
  matter later for segmentation and is not addressed here.

## Open questions

This ADR decides the Apologia Studio side only. It does not decide how
Document Manager implements recognition — engine, language models, quality
thresholds, or whether Docling is in fact the right choice *there*, where a
Python pipeline carries none of the cost it would carry here. That decision
belongs to the DocumentProcessingEngine repository and should get its own
record.

The contract for an `ocr` artifact — required metadata, confidence reporting,
and how a partially recognised page is represented — is not yet specified and
must be agreed between the two systems before implementation.

## Implementation sequence

1. Report per-page text-layer coverage from the extraction boundary, so that
   a zero-word page is an explicit outcome rather than an empty result.
2. Surface that signal in the editorial review draft, so a human approving a
   submission sees which pages carry no recoverable text.
3. Specify the `ocr` artifact contract jointly with Document Manager:
   required provenance, confidence, and partial-recognition representation.
4. Consume `ocr` artifacts in `KnowledgeImportPackage` persistence, keeping
   them distinct from `parsed` artifacts through to retrieval projection.
5. Re-submit affected documents once recognition is available upstream.
