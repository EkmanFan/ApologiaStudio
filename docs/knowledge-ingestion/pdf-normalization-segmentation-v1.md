# PDF Normalization and Segmentation v1

## Purpose

Increment 2B adds deterministic normalization and structural segmentation on top
of the generic PDF extraction boundary introduced in Increment 2A.

The implementation remains source-agnostic. It does not contain work-, author-,
publisher-, or confession-specific parsing logic.

## Pipeline boundary

```text
ExtractedPdfDocument
        |
        v
IPdfDocumentNormalizer
        |
        v
NormalizedPdfDocument
        |
        v
IDocumentSegmenter
        |
        v
DocumentSegmentationResult
```

Persistence, retrieval chunks, embeddings, and source-specific import profiles
remain outside this increment.

## Normalization profile

The v1 normalizer records:

```text
unicode-nfc-whitespace-dehyphenation-recurring-margins-v1
```

It performs deterministic transformations only:

- Unicode NFC normalization;
- line-ending normalization;
- conservative dehyphenation when a letter followed by `-` is broken across a
  line and the next character is lowercase;
- whitespace collapse;
- recurring header/footer detection in page-margin zones.

The original block text is retained as `SourceText`. Recurring headers and
footers are not deleted. They are marked with `IsExcluded` and a typed exclusion
reason so the transformation remains auditable.

Recurring margin detection uses repeated normalized text and position. Digit
runs are canonicalized for recurrence detection so changing page numbers can be
recognized without hard-coded coordinates or document-specific page-number
rules.

## Segmentation profile

The v1 segmenter records:

```text
font-hierarchy-exact-heading-hints-v1
```

It uses:

- the word-count-weighted median font size as the body-font baseline;
- bounded heading length and word count;
- font-size ratios for chapter/section/subsection candidates;
- exact normalized heading hints for optional `SegmentKind` overrides;
- `ParagraphGroup` + `MainText` for ordinary unheaded body text.

Excluded normalized blocks are not segmented.

## Segment kinds

The Application model mirrors the v1 documentary categories already persisted
by the Knowledge Store:

```text
Unknown
MainText
PedagogicalPrompt
Sidebar
Bibliography
Caption
Glossary
Index
```

Ordinary body-flow text is classified as `MainText`. Explicit kind hints may
classify a heading-led segment differently.

An exact heading hint is configuration data, not parser code. The generic
segmenter has no knowledge of any particular title, publisher, author, or
exercise label.

## Deliberate limitations

Increment 2B does not attempt:

- OCR;
- semantic classification with an LLM;
- automatic bibliography understanding;
- automatic sidebar/caption recognition beyond future explicit hints;
- source-specific page ranges;
- persistence to the Knowledge Store;
- retrieval chunk generation;
- migration of the existing De Decretis importer.

The heuristics are intentionally modest. Real-document regression testing in
Increment 2D will determine whether the reading-order and segmentation profiles
need revision before they are considered stable for broader ingestion.
