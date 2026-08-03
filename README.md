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

## Language preferences

The `/settings` page stores two preferences per user:

- interface language: French or English;
- theological language: French, English, or unset.

When theological language is unset, it inherits the interface language. The
effective theological language controls the default language of theological
answers and deterministic Bible passage retrieval. An edition or output
language explicitly requested in the current message takes precedence.

Current default Bible editions are `lsg1910` for French and `web-classic` for
English. The routing model may normalize a reference and extract an explicitly
requested language, but it never selects the application default.

## Application shell

The conversation workspace uses a viewport-height application shell:

- the sidebar header and local-account footer remain visible;
- Library and Chats scroll independently from the conversation;
- Library lists active, approved Bible editions from PostgreSQL;
- the message thread scrolls independently while the composer remains visible;
- automatic scrolling follows new content only while the reader is already near
  the latest message;
- below 900 pixels, the sidebar becomes a dismissible navigation drawer.

Pinned conversations and Projects are intentionally deferred until their
persistent data model is implemented. The Library entries are read-only until a
dedicated Bible reader is available.
