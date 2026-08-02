# ADR 0001: Canonical Bible corpus model

- Status: Accepted
- Date: 2026-08-02
- Decision owners: ApologiaStudio

## Context

ApologiaStudio needs a trustworthy, reproducible Bible corpus for exact passage
lookup, citations, later lexical search, and later RAG projections.

The following decisions and validations already exist:

- USFM is the canonical ingestion source.
- `SIL.Machine` is the approved USFM parser.
- VPL is an independent format oracle for the eBible distributions; it is not a
  second canonical source.
- Louis Segond 1910 (`fraLSG`) and World English Bible Classic (`eng-web`) are
  approved for import with the 66-book Protestant canon.
- USFM/VPL parity has been demonstrated for both approved corpora.
- Visible verse text is normalized only with Unicode NFC and collapsed
  whitespace. Punctuation and letter casing are preserved.
- Descriptive titles, speaker labels, and USFM word attributes must remain
  structurally distinct from visible verse text.
- Strong attributes are annotations supplied by the electronic corpus; they are
  not part of the printed Bible text.

The persistence model must support source updates without silently changing the
text behind an existing citation. It must also keep source provenance and import
validation auditable.

## Decision drivers

- Exact and stable Bible references
- Reproducible citations
- Lossless retention of the approved USFM information boundary
- Idempotent imports
- Explicit corpus provenance and validation evidence
- Efficient PostgreSQL passage lookup
- A clean boundary between canonical data and future search/RAG projections
- Operational simplicity for the current two-edition, 66-book scope

## Decision

### 1. Treat an edition and an imported corpus version as different concepts

A `BibleEdition` is the stable identity of a translation or revision, such as
`lsg1910` or `web-classic`.

A `BibleCorpusVersion` is an immutable imported snapshot of one edition. A new
eBible release or a material importer/normalizer change creates a new corpus
version; it never overwrites an approved version in place.

Only one corpus version per edition may be active. Historical versions remain
available so that stored citations and generated answers can be reproduced.

### 2. Use a normalized relational write model

The canonical PostgreSQL model consists of these tables:

| Table | Purpose | Principal constraints |
|---|---|---|
| `bible_editions` | Stable translation/revision identity | Unique edition code; BCP 47 language tag; canon code |
| `bible_corpus_versions` | Immutable imported snapshot | Unique import fingerprint; one active version per edition |
| `bible_source_artifacts` | Provenance for USFM, VPL, and validation reports | SHA-256, source URI, role, size, and download timestamp |
| `bible_books` | Stable book catalog | USFM code as stable key; unique OSIS code and canonical order |
| `bible_corpus_books` | Books and localized names present in one snapshot | Unique version/book and version/order pairs |
| `bible_verses` | Canonical normalized visible verse text | Unique version/book/chapter/verse label |
| `bible_word_annotations` | USFM word-level attributes, including Strong data | Ordered and anchored to a verse-text span |
| `bible_supplemental_texts` | Descriptive titles and speaker labels | Ordered and positionally anchored without merging into verse text |

The initial edition codes are `lsg1910` and `web-classic`. The initial canon code
is `protestant-66`.

### 3. Preserve source references without assuming that every verse is an integer

Each verse stores:

- a surrogate database identifier for efficient foreign keys;
- corpus version;
- USFM book code;
- positive chapter number;
- the normalized USFM verse label as text;
- a positive source ordinal within the chapter;
- normalized visible text;
- source-relative file name and source line for diagnostics.

The verse label remains textual because USFM permits labels such as `3a`, `4-5`,
or comma-separated forms. The importer must not split or renumber verse bridges.
The source ordinal provides deterministic ordering without trying to infer
semantics from a complex label.

The canonical lookup key is:

```text
(corpus_version_id, usfm_book_code, chapter_number, verse_label)
```

Verse text is non-null but may be empty when the source explicitly declares a
reference without visible text.

### 4. Keep annotations separate from canonical verse text

`bible_word_annotations` stores one parsed USFM attribute per row with:

- verse identifier;
- source ordinal;
- USFM marker;
- attribute name;
- unmodified attribute value;
- zero-based start offset and length in the normalized verse text.

Strong values remain source data. They are not silently corrected, expanded,
or treated as inspired text. Query-specific parsing or lexicon resolution is a
later read-model concern.

`bible_supplemental_texts` stores each supported `\\d` or `\\sp` item with its
marker, normalized text, source ordinal, anchor verse, placement (`before`,
`within`, or `after`), and character offset when it occurs within a verse.
Applications may render these elements, but they must not mutate the canonical
verse text to do so.

Introductions, editorial headings, notes, cross-references, figures, and
sidebars remain outside the first persistence scope. The strict parser still
validates their structure; their omission is explicit rather than accidental.

### 5. Make provenance part of the model

Every corpus version records:

- edition identifier;
- upstream source revision or publication date when supplied;
- deterministic source-tree SHA-256;
- import fingerprint;
- parser name and version;
- normalization policy identifier;
- import and approval timestamps;
- validation status;
- active/inactive status.

Every source artifact records its role (`canonical-usfm`, `validation-vpl`, or
`validation-report`), original URI, original file name, SHA-256, byte length, and
download timestamp.

Original corpus files are not stored as PostgreSQL blobs. They remain in
controlled corpus storage; PostgreSQL stores their identity and integrity data.
VPL content is not imported into `bible_verses`.

### 6. Define deterministic, idempotent import semantics

The source-tree hash is computed from a stable ordering of each source-relative
path and file SHA-256. It must not depend on filesystem traversal order or archive
metadata.

Version 1 hashes the concatenation of one record per selected USFM book, ordered
by its ordinal source-relative path. Each record is encoded as UTF-8 path,
`0x00`, lowercase ASCII file SHA-256, then `0x0a`. Paths always use `/` as their
separator. Excluded USFM documents do not participate in the source-tree hash.

The import fingerprint is derived from at least:

```text
edition code
source-tree SHA-256
parser name and version
normalization policy identifier
canonical schema version
```

The database enforces uniqueness of the import fingerprint. Re-running an import
with the same fingerprint returns the existing corpus version and writes no new
verses.

Fingerprint version 1 is the SHA-256 of a newline-delimited UTF-8 payload
containing the edition code, source-tree SHA-256, parser name, parser version,
normalization policy identifier, and canonical schema version. The payload begins
with `apologia-bible-import-fingerprint-v1`; changing its framing requires a new
fingerprint version.

A new import is parsed and validated before activation. Persistence and
activation occur transactionally:

1. acquire an edition-scoped import lock;
2. return the existing version when the fingerprint already exists;
3. insert the new corpus version, books, verses, annotations, and provenance;
4. verify expected counts, constraints, and validation status;
5. mark the new version approved and active while deactivating the previous one;
6. commit.

A failed import never becomes active. Approved corpus content is immutable.

### 7. Keep the domain boundary small

The Bible corpus is immutable reference data, not one aggregate containing tens
of thousands of child entities. The application must not load a complete corpus
as an EF Core aggregate graph.

The parser is encapsulated behind an application-facing USFM reader contract.
`SIL.Machine` types do not cross that boundary. Infrastructure implements parsing,
hashing, bulk persistence, locking, and PostgreSQL configuration.

The validation CLI remains an executable quality gate and shares normalization
and parsing behavior with the production importer rather than developing a
second interpretation of USFM.

### 8. Separate canonical storage from retrieval projections

Lexical indexes, chunks, embeddings, vector indexes, reranking metadata, and
prompt-ready documents are derived, rebuildable projections. They do not belong
in the canonical Bible schema introduced by this decision.

Every future chunk or citation must retain at least:

- corpus version identifier;
- start canonical reference;
- end canonical reference;
- derivation policy version.

This prevents a RAG result from becoming detached from the exact source text that
produced it.

## Required PostgreSQL integrity rules

- Unique `bible_editions.code`.
- Unique `bible_books.osis_code` and `bible_books.canonical_order` for the current
  `protestant-66` catalog.
- Unique `(corpus_version_id, usfm_book_code)` and
  `(corpus_version_id, book_ordinal)` in `bible_corpus_books`.
- Unique `(corpus_version_id, usfm_book_code, chapter_number, verse_label)` in
  `bible_verses`.
- Unique `(corpus_version_id, usfm_book_code, chapter_number, verse_ordinal)` in
  `bible_verses`.
- Positive chapter, book ordinal, verse ordinal, annotation ordinal, and lengths;
  non-negative character offsets.
- A partial unique index allowing only one active corpus version per edition.
- Foreign keys from versions to editions, corpus books to the book catalog, and
  annotations/supplemental texts to verses.
- B-tree indexes supporting edition/version activation and ordered passage lookup.

The first approved import must assert exactly 66 distinct books for both
`lsg1910` and `web-classic`. The count is an import policy for the approved canon,
not a universal database limitation.

## Rejected alternatives

### Store only raw USFM

Rejected because every lookup would repeat parsing and normalization, query
semantics would be harder to constrain, and database integrity could not protect
canonical references.

### Store VPL as the canonical text

Rejected because VPL deliberately flattens or omits USFM structure and word
attributes. It remains a validation oracle.

### Overwrite verses when an upstream corpus changes

Rejected because previous citations and generated answers would no longer be
reproducible.

### Store core scripture data in JSONB

Rejected because books, references, verses, and provenance have stable relational
semantics and benefit from explicit constraints and indexes.

### Add vector storage to the canonical schema now

Rejected because retrieval design and evaluation have not yet established a
chunking or embedding strategy. Those artifacts are derived and rebuildable.

## Consequences

### Positive

- Exact passage lookup is simple and strongly constrained.
- Corpus updates are auditable and reversible by activation.
- Existing citations remain reproducible.
- Strong and supplemental USFM data remain available without contaminating
  canonical verse text.
- Future lexical and vector retrieval can be rebuilt from one authoritative
  relational source.

### Costs

- Versioning duplicates approximately 31,000 verse rows per edition update.
- The importer must compute deterministic hashes and manage transactional
  activation.
- Word spans require the production reader to capture both offset and length,
  extending the current validation model, which records only an offset.
- Edition-specific book names require import or deterministic catalog metadata in
  addition to the current verse validation path.

These costs are acceptable at the current corpus size and materially improve
traceability.

## Implementation sequence

1. Add versioned provenance manifests for `fraLSG` and `eng-web`.
2. Define domain/reference value types and application ingestion contracts without
   exposing `SIL.Machine`.
3. Extract or reuse the validated USFM normalization behavior for the production
   reader and capture complete annotation spans.
4. Add EF Core configurations, PostgreSQL constraints, indexes, and a migration.
5. Implement a transactionally idempotent importer.
6. Add unit tests for references, fingerprints, normalization, and annotation
   anchoring.
7. Add PostgreSQL integration tests for re-import no-op, failed-import rollback,
   one-active-version enforcement, corpus counts, and passage ordering.
8. Import LSG1910 and WEB Classic and record the resulting corpus version IDs and
   validation evidence.

Search projections and RAG ingestion begin only after this canonical import path
is green and reproducible.
