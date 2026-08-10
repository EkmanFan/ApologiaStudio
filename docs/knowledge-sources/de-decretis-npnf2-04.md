# Curated source profile: De Decretis from NPNF2-04

- Profile: `de-decretis-npnf2-04-v1`
- Status: approved for the initial local Knowledge Store ingestion
- Scope: one real source used to validate provenance and citable segmentation

## Source artifact

The importer accepts exactly one reviewed source artifact:

- CCEL identifier: `npnf204`
- Title represented by the complete artifact: *NPNF2-04: Athanasius — Select Works and Letters*
- Media type: `application/pdf`
- Expected size: `11,963,985` bytes
- Expected PDF pages: `1,479`
- SHA-256: `de5e95573b7910292b4b07c02b5cfd834fe63dd5daf4056e9a947c96cb81bc75`
- Origin: `https://ccel.org/ccel/schaff/npnf204.html`

The original PDF is never modified. The importer copies it into content-addressed
managed storage and records its hash as the documentary integrity boundary.

The CCEL PDF states that the PDF file may be copied for non-commercial purposes
when unmodified and that written permission is required for commercial use.
ApologiaStudio therefore treats this profile as suitable for local development and
evaluation. Redistribution of the CCEL PDF or derived artifacts requires a separate
rights review for the intended use.

## Bibliographic representation

The complete PDF is represented as the artifact of an editorial compilation:

```text
Work: NPNF2-04: Athanasius — Select Works and Letters
  -> English editorial expression
  -> NPNF Second Series, Volume IV manifestation
  -> complete CCEL PDF artifact
```

`De Decretis` is represented independently:

```text
Work: De Decretis (Defence of the Nicene Definition)
  -> original Greek expression (metadata only in 6D)
  -> English NPNF translation/revision
  -> De Decretis-in-NPNF2-04 manifestation
  -> parsed artifact derived from the complete PDF
  -> normalized artifact
  -> 32 citable DocumentSegment rows (§1–§32)
```

The English expression is related to the Greek expression with `translation_of`.
The volume preface identifies Archibald Robertson as editor and explains that the
earlier translations and notes prepared by John Henry Newman were revised for the
volume, explicitly stating that this treatment also applies to `De Decretis`.

The publication year is deliberately left unset in v1 because the curated PDF itself
does not provide a sufficiently explicit publication-year statement for this
manifestation. This can be added later as a reviewed bibliographic assertion from a
separate authority source.

## Extraction boundary

The curated extraction profile uses:

- PDF pages `512` through `561`, inclusive;
- corresponding printed NPNF pages `482` through `531`;
- the next work, *De Sententia Dionysii*, begins on the following PDF page;
- exactly 32 numbered sections are required.

The parser uses the positioned PDF glyph layer. For this specific pinned artifact it
keeps the main text region and excludes smaller editorial footnotes, running headers,
and page numbers. The thresholds are versioned as part of the profile.

The normalized artifact additionally excludes editorial chapter headings so that each
`DocumentSegment` contains Athanasius's translated primary-source text for one numbered
section. Line-break hyphens are preserved conservatively rather than guessed away.
Unicode is normalized to NFC.

This is a curated source-specific parser, not a general PDF ingestion engine.

## Editorial classification

The `De Decretis` Work receives reviewed assertions for:

- `SourceKind = primary_source`;
- `Perspective = pro_nicene` with analytical classification;
- `EvidenceRole = historical_witness`;
- `EvidenceRole = theological_argument`.

These classifications describe how the source may be used. They are not credibility
scores and do not make Athanasius's claims automatically true.

## Managed artifacts

By default the portable import script stores content-addressed artifacts below:

```text
${XDG_DATA_HOME:-$HOME/.local/share}/ApologiaStudio/knowledge/artifacts/
  raw/<sha256>.pdf
  parsed/<sha256>.txt
  normalized/<sha256>.txt
```

The source PDF and derived text are intentionally not committed to Git.

## Validation contract

An import is accepted only when all of the following hold:

- source byte length, SHA-256, and PDF page count match the pinned artifact;
- sections `§1` through `§32` are found exactly once and in order;
- known beginning and ending sentinels are present;
- text from the following work is absent;
- a known editorial-footnote sentinel is absent from normalized text;
- the persisted chain resolves the normalized segments back through the parsed
  artifact to the exact raw artifact;
- a repeated import validates the existing chain rather than creating duplicates.

No retrieval chunks, embeddings, vector indexes, or retrieval behavior are created by
this profile. Those remain the next implementation increment.
