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

The primary executable composition adapter is the Apologia web host. It owns a
signed notification endpoint and an internal background consumer using the
same Application handler and infrastructure ports as the original command-line
adapter.

The diagnostic command-line adapter remains available at:

```text
tools/ApologiaStudio.DocumentManagerConsumer
```

It supports `consume-once` for an explicit diagnostic delivery. Its legacy
`run` mode remains available for isolated troubleshooting, but it is not part
of the normal application runtime.

## Hybrid notification and reconciliation

The normal local and deployed workflow does not continuously ask the Manager
for work. Once a processing result is durably registered, the Manager sends a
small signed callback to:

```text
POST /internal/document-manager/result-available
```

The callback contains only a notification identity, the intended consumer ID
and a timestamp. It contains no book, result reference or custody data. Its
HMAC-SHA256 signature covers the exact request bytes; Apologia rejects an
altered body, the wrong consumer, an old timestamp or an invalid signature.

An accepted callback wakes one in-process consumer. That consumer repeatedly
uses the existing durable API until no result remains:

```text
signed wake-up
    -> claim
    -> download and verify
    -> persist the immutable inbox transaction
    -> prepare/update the editorial draft
    -> acknowledge
    -> claim the next result
    -> stop when the Manager returns no result
```

The callback is only an optimization. Apologia also performs the same drain on
startup and every five minutes by default. The Manager retries failed callbacks
every ten seconds. This hybrid design gives prompt processing without a hot
polling loop and still recovers from restarts, temporary outages and lost
signals. Duplicate callbacks remain harmless because claims and inbox writes
are idempotent.

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

### Administrative review controls

Two exceptional controls are kept separate from the ordinary editorial flow:

- reopening a rejected record returns it to `pending_review`, clears the active
  rejection decision and appends a `reopen` review event; the earlier rejection
  event and its reason remain immutable history;
- permanently deleting a submission removes every Apologia inbox and editorial
  row for that Manager `SubmissionId` in one database transaction, including
  drafts, draft parts, review events, raw result payloads, visual payloads,
  manifests and expected-unit rows;
- deleting and reimporting first performs that complete Apologia purge, then
  asks the Manager to reopen all acknowledged deliveries for the submission.
  The Manager sends its normal signed notification and Apologia rebuilds the
  inbox and provisional record from the unchanged results.

Permanent deletion does not call the Manager and does not erase its independent
custody copy. It is disabled by default through
`DocumentManagerAdministration:Enabled`. The local launcher enables it solely
for development tests. This feature flag is not a production authorization
mechanism; production use remains forbidden until authenticated `Admin` role
authorization replaces the configured authorizer.

Delete-and-reimport also leaves Manager processing and custody untouched: it is
a redelivery, not a DPEngine rerun. Because the Apologia purge and remote
Manager request cannot share a database transaction, a failed replay request is
reported explicitly after the local purge. The operator can then use “Send to
Apologia again” on any completed part in the Manager UI; that action reopens the
whole submission, not only the selected part. Manager replay requests are
audited independently.

The current purge boundary covers only the pre-publication workflow implemented
through AS-DM-05. AS-DM-06 must either refuse deletion after Knowledge Store
publication or extend the same transaction boundary to all published resources
and derived projections.

## Local execution

Start the Document Manager, then start Apologia Studio:

```bash
./scripts/run-apologia-dev.sh
```

No separate continuously polling process is needed. To force one diagnostic
delivery manually, use:

```bash
./scripts/run-document-manager-consumer.sh consume-once
```

The application launcher starts the local databases, applies pending Knowledge
Store migrations, starts the web application and enables the hybrid consumer
with local-only development credentials.

The web-hosted consumer uses standard .NET configuration. Production must
provide:

```text
APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION
DocumentManagerConsumer__Enabled=true
DocumentManagerConsumer__ConsumerKey=<service credential>
DocumentManagerConsumer__ConsumerId=<stable consumer identity>
DocumentManagerConsumer__ManagerUrl=<HTTPS Manager URL>
DocumentManagerConsumer__NotificationSecret=<distinct shared HMAC secret>
DocumentManagerConsumer__DeliveryReplayApiKey=<distinct administration credential>
```

The `APOLOGIASTUDIO_DOCUMENT_MANAGER_*` and
`DPE_MANAGER_NOTIFICATION_SHARED_SECRET` and
`DPE_MANAGER_DELIVERY_REPLAY_API_KEY` names are local-launcher conveniences that
are mapped to these settings by `run-apologia-dev.sh`.

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
