# ApologiaStudio

## Bible corpus validation

ApologiaStudio uses USFM as its canonical Bible source, `SIL.Machine` for parsing, and VPL as the validation oracle. See [Bible corpus validation](docs/bible-corpus-validation.md) for the executable benchmark.

The accepted persistence and versioning design is recorded in
[ADR 0001: Canonical Bible corpus model](docs/adr/0001-canonical-bible-corpus-model.md).

The approved source snapshots and their archive hashes are recorded in the
[Bible corpus provenance manifests](docs/bible-corpus-provenance.md).

Approved snapshots are imported through the manifest-driven
`ApologiaStudio.BibleCorpusImporter` command documented on that page.

## Bible corpus query API

The web application exposes deterministic, read-only access to active and
approved Bible corpus versions:

```text
GET /api/bible/editions
GET /api/bible/editions/{editionCode}/books
GET /api/bible/editions/{editionCode}/books/{bookCode}/chapters/{chapterNumber}
GET /api/bible/editions/{editionCode}/books/{bookCode}/chapters/{chapterNumber}/verses/{verseLabel}
```

Book identifiers use canonical USFM codes such as `GEN`, `PSA`, and `JHN`.
Chapter responses and exact-verse responses include imported word annotations,
including Strong attributes when present.
