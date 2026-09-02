# Document Manager to Knowledge workflow v1

Status: accepted workflow on 2026-09-02. The delivery inbox, Manager UI
integration, submission assembly, provisional editorial record and human
review are implemented; Knowledge Store publication remains planned in the
ordered increments below.

## Decision

Document Manager processing and Apologia Studio knowledge publication are two
separate workflows joined by a durable consumer contract.

The Manager produces format-neutral documentary evidence. Apologia preserves
that evidence, assembles all results belonging to one submission, prepares an
editorial draft, requires human approval, and only then makes the source
available to retrieval and agents.

```text
Document Manager processing
            |
            v
verified Apologia inbox                         IMPLEMENTED
            |
            v
submission assembly                             IMPLEMENTED
            |
            v
editorial import draft                          IMPLEMENTED
            |
            v
human review and approval                       IMPLEMENTED
            |
            v
atomic KnowledgeImportPackage persistence       PLANNED
            |
            v
retrieval chunks                                PLANNED
            |
            v
embeddings and search indexes                   PLANNED
            |
            v
approved source available to agents             PLANNED
```

The order is mandatory. A later stage must not be used to compensate for a
missing guarantee in an earlier stage.

## Operator workspace integration

Apologia Studio exposes a dedicated `Document Manager` workspace in its main
navigation. It embeds the standalone Manager UI rather than copying its screens
or referencing DPEngine implementation assemblies.

This integration changes where the operator works, not who owns the workflow:

- the Manager remains responsible for uploads, splitting, queue operations,
  processing status, result retention and custody-affecting actions;
- Apologia Studio provides the surrounding application shell and a full-screen
  escape hatch;
- the Manager API key remains in the Manager server process and is never placed
  in the Apologia browser;
- the Manager UI address is configured through `DocumentManager:UiUrl`;
- non-local deployments require HTTPS;
- the Manager explicitly allowlists the Apologia parent origin through
  `ManagerEmbedding:AllowedParentOrigins`; embedding is disabled when this list
  is empty.

The local development configuration uses the same `localhost` site for both
applications so the server-side Blazor antiforgery cookie continues to work in
the embedded UI. Future login and role integration may replace this boundary
with an authenticated reverse proxy or shared identity, but must not broaden
the Manager's frame allowlist to arbitrary origins.

## Stage 1 — Verified delivery inbox

Apologia claims one completed processing result, downloads its canonical JSON
and every advertised visual, independently verifies byte lengths and SHA-256
digests, then commits all bytes and custody metadata in one PostgreSQL
transaction. It acknowledges the Manager claim only after that transaction.

This stage is defined by
[Document Manager consumer v1](document-manager-consumer-v1.md) and is already
implemented and proven with a real Manager result.

The inbox is immutable source evidence. Later mapping corrections must be
replayable from these stored bytes without rerunning DPEngine.

## Stage 2 — Assemble one submission

One original document can be processed as a whole or divided into multiple
processing units. Every result carrying the same Manager `SubmissionId` belongs
to one Apologia ingestion draft. A processing unit is not a book.

Ordering uses the neutral Manager scope:

- a whole-document result stands alone;
- page ranges are ordered by their first physical page;
- content-unit ranges are ordered by their first content-unit index;
- incompatible, overlapping, duplicate or discontinuous scopes block assembly
  and require review.

Apologia must never group results by filename, displayed title, arrival time or
content similarity. Those values are not stable identities.

### Finalized Manager contract

Every delivery claim now contains an immutable, versioned submission manifest
with:

- `SubmissionId`;
- the complete ordered set of expected processing-unit IDs and scopes;
- a manifest revision;
- the source SHA-256, original filename and finalization timestamp.

The Manager creates revision 1 for the original processing plan and appends a
new revision when a split replaces that plan. Apologia stores each revision as
immutable evidence and assembles against the latest revision received.

The assembly has three explicit states:

- `AwaitingParts`: the plan is coherent, but at least one expected result is
  still missing;
- `Ready`: every expected result is present and ordered according to the
  manifest;
- `Blocked`: an unexpected or duplicate unit, a scope mismatch, or a
  discontinuous/incompatible plan prevents assembly.

Apologia does not use the administrative API key, read the Manager database, or
infer completeness from a flag such as `isLast` on an arriving result.

## Stage 3 — Prepare an editorial draft

When every expected unit is present and verified, a deterministic Apologia
adapter creates one editable ingestion draft.

This stage is implemented as a provisional editorial record, separate from the
published Knowledge Store. One record exists for each immutable
`(SubmissionId, manifest revision)` pair. Creation is idempotent: a replay
validates the same source evidence but never overwrites fields already edited
by a reviewer.

The draft contains:

- the source identity and complete custody chain;
- ordered processing units and their scopes;
- candidate title, language, edition and contributor fields;
- proposed chapters, sections and other document segments;
- notes and visual references;
- source locators needed to return to the original page or content unit;
- DPEngine quality observations and assembly conflicts;
- the exact versions of the mapping rules that created the draft.

DPEngine structural segments become candidate Apologia `DocumentSegment`
records. They are not retrieval chunks. Stable draft identifiers are derived
from Manager identities and DPEngine element or segment identities, never from
mutable display text.

A filename may provide a proposed title for convenience. It must not be treated
as approved bibliographic truth. Author, language, edition, historical period,
theological perspective and other classifications remain unknown unless they
come from explicit trusted metadata or a reviewer.

An LLM is not part of the deterministic adapter. A future AI metadata assistant
may propose values, but every such value must remain visibly AI-proposed and
must pass through the same human approval gate.

The initial deterministic proposal removes the file extension to suggest a
title and records `original_filename` as its origin. Language, edition,
publication year and place, description and contributors remain empty until
trusted metadata or a reviewer supplies them. The record starts in
`pending_review`, is not inserted into the Work/Expression/Manifestation graph,
and is not searchable.

Every ordered draft part retains its processing-unit identity, result reference
and neutral scope. The source SHA-256 and original filename are copied as
immutable provenance. A database foreign key keeps the draft attached to its
stored manifest and raw inbox results.

## Stage 4 — Human review

The Apologia UI presents the assembled draft as “To review”. The reviewer can:

- confirm or correct the title, language and edition;
- add or correct contributors and their roles;
- verify the order and completeness of processed parts;
- inspect quality warnings;
- correct the proposed documentary structure;
- decide whether the source document represents one Work or a compilation of
  multiple Works;
- reject the draft without destroying the immutable inbox evidence.

Approval is allowed only when:

- the finalized Manager manifest is complete;
- all expected processing results and required visuals are present;
- all custody checks pass;
- unit order and scopes are consistent;
- the minimum required bibliographic fields are explicitly confirmed;
- no blocking quality or assembly conflict remains;
- the reviewer intentionally approves publication.

The word `pending` means “waiting for editorial review”. It does not mean
“waiting for DPEngine processing”.

## Stage 5 — Publish into the Knowledge Store

Approval materializes a validated `KnowledgeImportPackage` and persists it in
one Knowledge Store transaction.

The package contains the reviewed Work/Expression/Manifestation graph, source
and derived artifacts, processing activities, stable documentary segments and
approved or explicitly proposed assertions. The raw DPEngine result remains in
the inbox and is referenced as provenance; it is not replaced by the relational
projection.

No partially imported book may become visible. A retry of the same approved
draft must be idempotent. A new Manager result must create a new import version
or an explicit supersession proposal; it must never silently overwrite an
approved source.

## Stage 6 — Build retrieval projections

Only after Knowledge Store publication does Apologia build retrieval chunks
from eligible approved documentary segments.

Chunks are a derived search projection. They may be rebuilt when chunking rules
change and must retain links to their source segments and locators. They never
replace the reviewed document structure or the raw Manager evidence.

## Stage 7 — Embeddings and agent availability

Embeddings and lexical/vector indexes are calculated from the retrieval
projection. A source becomes available to ordinary search, RAG and agents only
after the approved Knowledge Store version and its required indexes are ready.

Indexing failure does not corrupt or roll back the approved documentary source.
It leaves that source in a visible “indexing failed” or “indexing pending” state
that can be retried deterministically.

## Ownership boundaries

| Responsibility | Owner |
| --- | --- |
| Source acquisition, extraction, OCR, reconciliation and structural evidence | Document Manager / DPEngine |
| Durable result publication and claim/ack lifecycle | Document Manager |
| Independent integrity verification and immutable downstream inbox | Apologia Studio |
| Multi-unit submission assembly | Apologia Studio |
| Bibliographic interpretation and editorial workflow | Apologia Studio |
| Knowledge graph persistence | Apologia Studio |
| Chunking, embeddings and retrieval indexes | Apologia Studio |
| Approval of source availability to agents | Human reviewer through Apologia Studio |

## Non-negotiable invariants

Future implementation must preserve all of the following:

1. Apologia never reads the Manager PostgreSQL database or custody filesystem.
2. Apologia never requires a reference to DPEngine implementation assemblies.
3. The raw result and required visuals are durably stored before ACK.
4. A Manager processing unit never automatically becomes a separate book.
5. Parts are grouped by `SubmissionId`, not by human-readable text.
6. An assembled submission is not considered complete without a finalized
   Manager manifest.
7. No unreviewed draft is searchable by ordinary users or available to agents.
8. No author, language, edition, classification or approval is invented from a
   filename or model output.
9. Documentary segments remain the stable evidence units; chunks and embeddings
   remain rebuildable projections.
10. Reprocessing never silently overwrites an approved Knowledge Store version.
11. Rejecting or deleting a draft does not silently erase immutable custody
    evidence; retention and permanent deletion require a separate explicit
    policy.

Any change to an invariant requires an explicit architecture decision and an
update to this document before implementation.

## Ordered delivery increments

```text
AS-DM-01  verified result + visual inbox                    DONE
AS-DM-UI-01 Manager UI workspace in Apologia Studio         DONE
AS-DM-02  finalized Manager submission manifest contract   DONE
AS-DM-03  multi-unit assembly and completeness checks       DONE
AS-DM-04  editable editorial draft persistence              DONE
AS-DM-05  review and approval UI                             DONE
AS-DM-06  KnowledgeImportPackage adapter + atomic import    NEXT
AS-DM-07  retrieval projection + embeddings
AS-DM-08  real split-book end-to-end acceptance
```

`AS-DM-05` edits the provisional record through optimistic concurrency,
surfaces its immutable source evidence, and keeps approval unavailable until
the mandatory title, language and primary-contributor fields have been
provided. Saving, approval and rejection are recorded as immutable review
events with the acting user, time, record version and metadata snapshot.
Approval and rejection require an explicit confirmation in the interface;
rejection additionally requires a reason. In this first version, an approved or
rejected decision is terminal. The review queue is available at
`/editorial-review` in the Apologia Studio shell.

## End-to-end acceptance scenarios

The workflow is complete only when automated tests and a human acceptance run
prove at least:

1. a whole document produces one reviewed Knowledge Store source;
2. a document split into three batches produces one draft in the correct order,
   never three books;
3. a missing or failed batch keeps the draft incomplete and unsearchable;
4. duplicate delivery changes no stored content and still permits ACK retry;
5. altered bytes or hashes block ACK and publication;
6. reviewer rejection leaves nothing searchable;
7. approval followed by indexing makes citations traceable back to the exact
   Manager result, DPEngine segment and source locator;
8. reprocessing proposes a new version without modifying the approved version.
