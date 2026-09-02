# ApologiaStudio

## Bible corpus validation

ApologiaStudio uses USFM as its canonical Bible source, `SIL.Machine` for parsing, and VPL as the validation oracle. See [Bible corpus validation](docs/bible-corpus-validation.md) for the executable benchmark.

The accepted persistence and versioning design is recorded in
[ADR 0001: Canonical Bible corpus model](docs/adr/0001-canonical-bible-corpus-model.md).

The approved source snapshots and their archive hashes are recorded in the
[Bible corpus provenance manifests](docs/bible-corpus-provenance.md).

Approved snapshots are imported through the manifest-driven
`ApologiaStudio.BibleCorpusImporter` command documented on that page.

## Knowledge grounding and RAG architecture

The accepted v1 architecture for documentary provenance, the dedicated
PostgreSQL + pgvector Knowledge Store, retrieval projections, and citation
grounding is recorded in
[ADR 0002: Knowledge Store and RAG architecture](docs/adr/0002-knowledge-store-and-rag-architecture.md).

Completed DPEngine results enter Apologia Studio through the durable,
hash-verified Document Manager inbox described in
[Document Manager consumer v1](docs/knowledge-ingestion/document-manager-consumer-v1.md).
The inbox preserves raw results and visuals before acknowledging delivery; the
subsequent editorial `KnowledgeImportPackage` adaptation remains a separate
step. Its mandatory sequence and invariants are fixed by the
[Document Manager to Knowledge workflow v1](docs/knowledge-ingestion/document-manager-to-knowledge-workflow-v1.md).

Operators can open the standalone Manager UI inside the Apologia application
shell at `/document-manager`. Its address is configured with
`DocumentManager:UiUrl`; local development expects the Manager launcher on
`http://localhost:5092`.

Completed provisional records are reviewed at `/editorial-review`. The editor
can correct bibliographic metadata, inspect the immutable source parts, save a
work in progress, or explicitly approve or reject it. The web application needs
the same Knowledge Store connection as the consumer through
`ConnectionStrings:Knowledge` or
`APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION`.

For local development, the launcher starts both PostgreSQL containers, applies
pending Knowledge Store migrations and starts the web application:

```bash
./scripts/run-apologia-dev.sh
```

The canonical Bible corpus remains deterministic reference data. RAG retrieval
is a separate, derived knowledge path and must not replace exact Bible passage
lookup.

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

The sidebar data rules are defined in the
[sidebar organization contract](docs/ux-sidebar-organization.md) and the
[sidebar management contract](docs/ux-sidebar-management.md).

The conversation workspace uses a viewport-height application shell:

- the sidebar header and local-account footer remain visible;
- Library, Pinned, Projects, and Chats scroll independently from the
  conversation;
- Library lists active, approved Bible editions from PostgreSQL;
- conversations may belong to one project or remain in Chats;
- pinned projects and conversations are shortcuts and do not change their
  underlying location;
- projects, conversations, and pinned shortcuts have a persistent manual sort
  order scoped to the current user;
- projects and conversations expose rename, pin, move, reorder, and confirmed
  deletion controls as applicable;
- conversation deletion is recoverable from Trash and never deletes messages;
- deleting a project returns its conversations to Chats;
- the message thread scrolls independently while the composer remains visible;
- automatic scrolling follows new content only while the reader is already near
  the latest message;
- below 900 pixels, the sidebar becomes a dismissible navigation drawer.

Pinned is hidden when empty. Projects remains visible because it contains the
project-creation control. Drag-and-drop and equivalent keyboard actions persist
manual order. Library entries open the deterministic Bible reader described in
the [Bible reader contract](docs/ux-bible-reader.md).

## Bible reader

Active, approved editions are available at stable routes:

```text
/library/{editionCode}
/library/{editionCode}/{bookCode}/{chapterNumber}
```

The reader loads complete chapters from PostgreSQL, preserves imported verse
labels, navigates across book boundaries, and lets the user select one verse or
a continuous range. A selection can prepare a new conversation, but it is never
sent automatically. The server revalidates every edition, book, chapter, and
verse label before it creates the editable draft.
