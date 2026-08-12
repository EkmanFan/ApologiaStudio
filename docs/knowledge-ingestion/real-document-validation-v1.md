# Real-document PDF validation v1

## Purpose

Increment 2D validates the generic born-digital PDF pipeline against real
documents before treating its extraction and segmentation heuristics as
production-ready.

The first 2D stage was observational. It added bounded diagnostics without
changing extraction, normalization, segmentation, persistence, or retrieval.

The second stage applies only corrections justified by the Stage 1 evidence and
turns the observed failure modes into regression checks.

## Stage 1 evidence

The existing curated De Decretis path remained valid:

```text
sections = 32
result = VALID
```

The generic 2A/2B path over the same 50 PDF pages extracted text successfully
but produced one 159,288-character `ParagraphGroup`.

That demonstrated a generic segmentation defect: when no heading candidate was
found, unheaded body blocks were accumulated across the complete selected
document.

The Ehrman/Mendez validation PDF produced useful text and headings, but 286 of
617 pages had no extractable words in the Stage 1 report. Visual inspection of
a textless sample page showed visible page text rendered as a raster page image.
The supplied PDF is therefore not a uniformly born-digital source with a
complete text layer.

Consequently, successful parsing of the available text must not be interpreted
as complete ingestion of that file.

Stage 1 also showed that the original multi-column warning was too broad.
Column switching is common in tables, pedagogical boxes, key-term layouts, and
other deliberately interleaved page designs. A column switch is therefore a
layout-complexity signal, not by itself proof of incorrect reading order.

## Stage 2 corrections

### Page-bounded fallback segmentation

When no heading has established a structural segment, `ParagraphGroup`
fallbacks are now bounded by PDF page.

This is deliberately a fallback, not a replacement for semantic section
detection. A PDF page is source-defined and stable, so it is safer than one
document-wide segment while avoiding arbitrary technical character limits at
the `DocumentSegment` level.

### Heading quality gate

Automatic font-based headings must contain enough textual signal to be useful.
Short decorative glyph fragments and extraction noise are rejected before they
can create chapter/section boundaries.

Explicit editorial heading hints continue to override the automatic quality
gate.

### Decorated heading hints

Heading hints remain exact in semantic content, but the matcher tolerates:

- leading/trailing non-alphanumeric ornamentation;
- a very short uppercase prefix such as a layout marker.

This handles source-layout artifacts such as decorated bibliography headings
without introducing title-, author-, publisher-, or confession-specific parser
code.

### Text-layer diagnostics

Generic extraction now records per page:

- raster image count;
- largest raster-image area ratio.

The real-document report combines that information with word extraction to
report:

- pages with and without extractable words;
- text-layer coverage percentage;
- textless pages containing a dominant raster image;
- bounded page-number samples.

These metrics are diagnostics. A dominant raster page can be a legitimate
illustration, so the metric is not an OCR decision by itself.

However, a document with many visible-text pages lacking an extractable text
layer cannot be claimed as completely ingested by the current born-digital
pipeline. OCR remains outside the V1 ingestion scope.

### Reading-order diagnostic semantics

The report now separates:

```text
multiColumnCandidatePages
interleavedColumnPages
verticalReversalPages
```

`interleavedColumnPages` means that narrow blocks switch between left and right
columns more than once. This describes layout complexity.

`verticalReversalPages` is the stronger reading-order signal: within a column,
the extracted sequence materially moves upward after moving down.

Neither metric alone is a correctness proof. Both identify pages for targeted
inspection.

## Diagnostic command

The KnowledgeImporter exposes:

```text
analyze-pdf
```

The command runs:

```text
PDF
 |
 v
PdfPigDocumentExtractor
 |
 v
PdfDocumentNormalizer
 |
 v
HeuristicDocumentSegmenter
 |
 v
bounded JSON diagnostics
```

It does not:

- import the source into PostgreSQL;
- create retrieval chunks;
- generate embeddings;
- modify the source PDF;
- copy source content into the repository;
- approve a source for production retrieval.

The report is an evaluation artifact, not a normalized corpus artifact.

Probe diagnostics report both normalized-block matches and a page-level word
stream match count. This helps distinguish missing text from a phrase split or
reconstructed differently at the text-block layer.

## Source-specific validation data

Document-specific values such as expected hashes, De Decretis page ranges,
known probes, and Ehrman/Mendez editorial headings remain in the external
validation runner.

They do not belong in the generic production extraction, normalization, or
segmentation code.

## Acceptance meaning

The real-document validation establishes different things for the two current
fixtures.

For De Decretis it verifies that:

- the curated importer still passes its historical 32-section regression;
- generic extraction retains text across the selected pages;
- generic fallback segmentation no longer creates one document-wide segment.

For the supplied Ehrman/Mendez PDF it verifies that:

- the generic pipeline can analyze the pages that contain extractable text;
- editorial heading hints work through layout decoration;
- the report explicitly exposes incomplete text-layer coverage.

It does **not** establish that the supplied Ehrman/Mendez PDF can be completely
ingested without OCR or a better source artifact.

Editorial approval, copyright/licensing review, and production retrieval
approval remain separate concerns.

## Stage 2 evidence refinement

The first Stage 2 assertion treated every exact probe match for
`SUGGESTIONS FOR FURTHER READING` as if it were a section heading. Inspection
of the pinned Ehrman/Mendez artifact showed that assumption was incorrect.

Of the 21 pages with the phrase in the extracted text:

- two are prose references to the further-reading section rather than headings;
- one real heading is extracted as `SUGG ESTIONS FOR FU RTH ER READING` and is
  merged with the following source line in the same layout block;
- the remaining occurrences are ordinary heading instances.

The generic hint matcher therefore uses the first retained source line as an
additional heading candidate and permits exact compact alphanumeric equality
to recover split-letter extraction artifacts. It does not use substring
matching, so prose references remain ordinary body text.

This is a general extraction-quality correction derived from real-document
evidence. It does not add title-specific production rules.
