# Generic Knowledge Import Package v1

## Purpose

Increment 2C removes source-specific persistence and retrieval-projection
contracts from the Knowledge importer. The reusable import boundary is a
`KnowledgeImportPackage` owned by the Application layer.

The package is data. It describes the reviewed documentary graph and prepared
artifact payloads that infrastructure persists. It does not parse PDFs and it
does not infer bibliographic, historical, theological, methodological, or
epistemic metadata.

## Reusable boundary

```text
source-specific preparation / curated profile
                    |
                    v
          KnowledgeImportPackage
                    |
        +-----------+------------+
        |                        |
        v                        v
Application                Infrastructure
chunk construction         artifact + PostgreSQL
                            persistence/projection
```

The reusable code lives under:

```text
src/ApologiaStudio.Application/Knowledge/Ingestion
src/ApologiaStudio.Infrastructure/Knowledge/Ingestion
```

The CLI under `tools/ApologiaStudio.KnowledgeImporter` is now a consumer and a
source-specific composition adapter. A future Blazor ingestion workflow can use
the same Application and Infrastructure contracts without referencing the CLI
executable project.

## Package content

The package carries:

- profile identity and stable-ID namespace;
- primary Work and normalized Artifact identifiers;
- Works, Expressions, Expression relations, Manifestations, and identifiers;
- Contributors and Contributions;
- raw and derived Artifacts with exact payload provenance;
- ProcessingActivities;
- stable DocumentSegments;
- controlled-vocabulary terms and reviewed classification assertions;
- reviewed metadata assertions.

`KnowledgeImportPackageValidator` rejects structurally inconsistent packages
before filesystem or database writes. Among other checks it enforces unique
resource identifiers, valid graph references, deterministic artifact derivation
order, exactly one artifact payload source, valid SHA-256 syntax and byte-payload identity, database-backed enum values,
segment-parent ordering, and valid classification references.

## De Decretis compatibility adapter

`DeDecretisImportPackageFactory` remains in the CLI project because it is
intentionally source-specific. It adapts the existing `PreparedDeDecretis`
result into the reusable package.

The factory preserves the existing stable-ID namespace, artifact hashes,
documentary graph, assertions, segment ordinals, and retrieval profile. Existing
De Decretis database identifiers and retrieval identifiers therefore remain
stable while source-specific knowledge moves out of reusable persistence code.

The existing `DeDecretisDocument` parser itself remains source-specific in
Increment 2C. Replacing that preparation path with the generic PDF extraction,
normalization, and segmentation pipeline is deferred to real multi-document
validation rather than assuming the new heuristics are equivalent.

## Managed artifacts

`ManagedKnowledgeArtifactStore` materializes every artifact declared by the
package. Managed paths are derived only from validated artifact type, SHA-256,
and file extension. The store verifies byte length and SHA-256 and writes
through a temporary file before move where possible.

The store has no fixed De Decretis artifact names or page rules.

## PostgreSQL persistence

`PostgreSqlKnowledgeImportStore` persists package data through the existing
Knowledge Store schema. No database migration is required by Increment 2C.

Import remains transactional and protected by a profile-specific advisory lock.
Re-import validates package-owned resources instead of blindly duplicating them.
A partially present package is rejected.

Removal deletes only assertions and graph objects declared by the package.
Shared contributors and controlled-vocabulary terms are deleted only when they
are no longer referenced. Managed artifact files are deletable only for hashes
that are no longer referenced by the Knowledge Store.

## Retrieval projection

`KnowledgeRetrievalChunkBuilder` belongs to Application because chunk formation
is deterministic knowledge-processing logic, independent of PostgreSQL.
`PostgreSqlKnowledgeRetrievalProjectionStore` persists the resulting chunks and
embeddings.

Retrieval chunks remain rebuildable projections. They never replace
`DocumentSegment` as the stable citation/evidence unit.

Ordinary v1 retrieval includes only:

- `MainText`;
- `Sidebar`;
- `Caption`.

`Unknown`, `PedagogicalPrompt`, `Bibliography`, `Glossary`, and `Index` are not
silently treated as ordinary evidence.

## Deliberate limits

Increment 2C does not add:

- a generic user-facing upload command;
- a new database schema;
- OCR;
- LLM metadata generation;
- automatic source approval;
- a second real-document import;
- changes to retrieval scoring or evaluation algorithms.

The next validation increment must prove these reusable boundaries on the
existing De Decretis source and on a structurally different born-digital PDF
without introducing another persistence implementation.
