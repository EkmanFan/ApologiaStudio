# Document Manager consumer v1

Status: implemented transport, custody inbox, submission assembly,
provisional editorial record and human review UI; knowledge-package adaptation
remains pending.

## Purpose

Apologia Studio consumes completed DPEngine results through the durable Document
Manager consumer API. It never reads the Manager PostgreSQL database or custody
filesystem directly and never references DPEngine implementation assemblies.

The v1 flow is deterministic:

```text
claim oldest result
        |
        v
download result JSON + advertised visuals
        |
        v
verify byte lengths + SHA-256 + advertised JSON schema identity
        |
        v
validate and persist the finalized submission manifest
        |
        v
persist one atomic Apologia inbox transaction
        |
        v
acknowledge the Manager claim
        |
        v
report the assembly as awaiting parts, ready or blocked
        |
        v
create one pending-review provisional record when ready
```

The ACK is deliberately last. If it fails after Apologia commits, the Manager
redelivers the result. The inbox then verifies that the same `ResultReference`
has exactly the same custody data before the consumer retries the ACK. A reused
reference with different bytes, metadata, or visual manifest is an integrity
failure.

## Components

The Application layer owns the orchestration ports and integrity policy under:

```text
src/ApologiaStudio.Application/Knowledge/DocumentProcessing
```

Infrastructure owns the HTTP adapter and PostgreSQL inbox under:

```text
src/ApologiaStudio.Infrastructure/Knowledge/DocumentProcessing
```

The executable composition adapter is:

```text
tools/ApologiaStudio.DocumentManagerConsumer
```

It supports `consume-once` for an explicit delivery and `run` for continuous
polling. Both modes use the same handler and idempotence boundary.

## Persistence

The Knowledge Store migration adds:

```text
document_manager_result_inbox
document_manager_visual_asset_inbox
document_manager_submission_manifest_inbox
document_manager_expected_unit_inbox
document_manager_editorial_drafts
document_manager_editorial_draft_parts
document_manager_editorial_review_events
```

`result_reference` is the inbox identity. The raw vendor JSON is retained as
`bytea`, along with its claimed schema, media type, length, SHA-256, submission,
processing unit, scope, and timestamps. Visual bytes and their individual
custody data are children of the same result.

Claim tokens and service credentials are intentionally never persisted.

Submission manifests are immutable by `(submission_id, revision)`. Each
manifest records the ordered processing-unit identities and neutral scopes that
define the complete work. Results are grouped only by `SubmissionId`, then
placed in manifest order; filenames and arrival order never determine grouping.

After each delivery, the consumer reports how many expected parts are present.
An incomplete work remains stored but cannot advance to editorial preparation.
An incoherent plan or conflicting result is reported as blocked.

A complete assembly creates one provisional editorial record per manifest
revision. Its stable identity and ordered source parts are deterministic. A
replay returns the existing record and preserves all editorial changes. The
title derived from the original filename is explicitly marked as proposed;
unknown bibliographic values remain empty.

The Apologia Studio route `/editorial-review` lists these records and lets a
human editor complete their bibliographic metadata. Every save, approval and
rejection increments the record version and appends an audit event. A stale
browser session is rejected through optimistic concurrency instead of silently
overwriting newer editorial work. Approval requires a title, language and
primary contributor; rejection requires a reason and both decisions require an
explicit confirmation.

## Local execution

Start the Document Manager first, then run one delivery from Apologia Studio:

```bash
./scripts/run-document-manager-consumer.sh consume-once
```

For continuous consumption:

```bash
./scripts/run-document-manager-consumer.sh run
```

The script starts the local Knowledge PostgreSQL container, applies pending
Knowledge Store migrations through the consumer startup, and uses the local
Document Manager development credential only for the default loopback URL.

Production configuration must provide:

```text
APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION
APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_KEY
APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_ID
APOLOGIASTUDIO_DOCUMENT_MANAGER_URL
```

Remote transport requires HTTPS. Plain HTTP is accepted only for a loopback
endpoint. Result and visual downloads are bounded before allocation and every
advertised digest is independently recomputed by Apologia.

## Deliberate boundary

These increments establish reliable delivery, preserve the complete raw input
for later reprocessing, determine when all parts of one work are present, create
the pending editorial record and support its human approval. They do not yet
construct a `KnowledgeImportPackage`, create the corresponding Knowledge Store
source, build retrieval chunks, or calculate embeddings.

That next adapter must map the portable DPEngine structure into a pending
editorial package. It must not infer author, language, edition, theological
classification, or review approval from a filename. The inbox allows that
mapping to evolve without asking the Manager to redeliver or rerun a document.

The mandatory downstream stages, the split-submission completeness requirement
and the non-negotiable architecture invariants are defined in
[Document Manager to Knowledge workflow v1](document-manager-to-knowledge-workflow-v1.md).
