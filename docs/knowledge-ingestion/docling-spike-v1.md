# Docling spike v1

## Decision to test

Apologia Studio already has a deterministic generic PDF pipeline based on
PdfPig, plus real-document regression diagnostics.

This spike does **not** replace that pipeline. It tests whether Docling provides
enough additional value in document understanding and OCR to justify a future
adapter or independent document-ingestion component.

The production code under `src/` is deliberately untouched.

## Pinned dependency

The spike pins:

```text
docling[easyocr] == 2.115.0
```

The pin is intentional. A document-ingestion benchmark must be reproducible;
Docling and its document models evolve quickly.

## Experiment matrix

The spike uses the same pinned real documents already used by Increment 2D.

| Run | Document | Pages | OCR | Purpose |
|---|---|---:|---|---|
| de-decretis-no-ocr | De Decretis | 512-561 | off | Compare structure on a fully extractable text PDF |
| ehrman-text-no-ocr | Ehrman/Méndez | 398-405 | off | Inspect born-digital text and heading reconstruction |
| ehrman-raster-no-ocr | Ehrman/Méndez | 14-20 | off | Establish the no-OCR baseline on raster pages |
| ehrman-raster-full-ocr | Ehrman/Méndez | 14-20 | full-page | Measure OCR recovery |
| ehrman-hybrid-default-ocr | Ehrman/Méndez | 1-10 | default | Test mixed text/raster handling |

The raster range is intentionally small. Running OCR over all 617 pages before
we know whether the output is useful would spend time and model resources
without improving the architectural decision.

## Scope

The spike measures:

- successful local conversion;
- text-item and text-character recovery;
- pages represented by text provenance;
- detected title/section-header items;
- label distribution;
- selected phrase recovery;
- wall-clock conversion time;
- OCR recovery on a known raster-only sample.

The spike does not evaluate:

- embeddings;
- vector databases;
- retrieval quality;
- reranking;
- production deployment;
- remote Docling services;
- VLM pipelines;
- full-book OCR.

Those become relevant only if the standard Docling pipeline materially improves
the document-understanding boundary.

## Isolation

The execution script creates an isolated Python virtual environment outside the
repository and writes all conversion artifacts under the user's Downloads
directory.

No Docling Python package is added to the Apologia Studio runtime.

The experiment invokes Docling through its Python `DocumentConverter` API,
not through CLI subcommands. The API exposes `page_range` directly and is the
stable comparison boundary we need for reproducible page-slice experiments.

The spike explicitly runs Docling on CPU for reproducibility. Hardware
acceleration can be evaluated separately if Docling proves useful.

## Decision criteria

After the run, do not choose Docling because it is more feature-rich.

Prefer an adapter or independent ingestion component only if the evidence shows
material improvement in at least one difficult capability that matters to
Apologia Studio, such as:

1. recovering text from raster pages without unacceptable corruption;
2. preserving or improving heading and reading-order structure;
3. providing useful provenance suitable for stable citation;
4. reducing custom parsing code enough to justify Python/service operational
   complexity.

If the result is merely equivalent to PdfPig on born-digital pages while adding
substantial runtime complexity, retain the current pipeline and use a narrower
OCR integration instead.

## Productization caveat

Docling's code is open source, but model licenses must be reviewed separately
before any production or redistributable product decision.
