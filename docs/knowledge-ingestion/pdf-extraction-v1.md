# Generic PDF Extraction v1

## Purpose

Increment 2A establishes the reusable PDF extraction boundary for the
Apologia Studio knowledge-ingestion pipeline.

The extractor is intentionally independent from any specific work, author,
publisher, page range, theological perspective, or editorial classification.

It is infrastructure for later normalization, structural segmentation,
`SegmentKind` classification, persistence, and retrieval projection.

## V1 scope

Supported:

- born-digital PDF files with an extractable text layer;
- byte-level source identity through SHA-256 and byte length;
- page count and page dimensions;
- word text, source/render sequence, font name, point size, orientation,
  and PDF-space bounding boxes;
- provisional text blocks with geometry and reading order;
- cancellation between document pages;
- detection of a source file that changes during extraction.

Not supported in this increment:

- OCR;
- scanned-image recovery;
- normalization;
- header/footer removal;
- semantic section detection;
- `SegmentKind` assignment;
- source-specific page ranges or rules;
- persistence;
- retrieval chunk construction;
- LLM-assisted extraction.

## Layering

The reusable contract lives in:

```text
ApologiaStudio.Application/Knowledge/Ingestion
```

The PdfPig implementation lives in:

```text
ApologiaStudio.Infrastructure/Knowledge/Ingestion
```

The KnowledgeImporter CLI is deliberately not migrated to this contract in
Increment 2A. That migration belongs to Increment 2C, after normalization and
segmentation have their own reusable contracts.

## Extraction profile

The current implementation records:

```text
pdfpig-0.1.15-nearest-neighbour-docstrum-unsupervised-v1
```

This identifies the technical extraction behavior:

1. PdfPig 0.1.15;
2. `NearestNeighbourWordExtractor`;
3. `DocstrumBoundingBoxes` page segmentation;
4. `UnsupervisedReadingOrderDetector`.

The profile is versioned because layout-analysis behavior is part of processing
provenance and may evolve after regression testing on structurally different
documents.

The downstream contract does not expose PdfPig types.

## Design constraint

No source-specific parser or source identity is introduced in the generic
extractor.

A document-specific profile may later provide optional metadata or deterministic
hints, but the core extractor must remain usable with only a PDF path.
