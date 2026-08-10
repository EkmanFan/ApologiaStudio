# ADR 0002: Knowledge Store and RAG architecture

- Status: Accepted
- Date: 2026-08-11
- Decision owners: ApologiaStudio

## Context

ApologiaStudio needs retrieval-augmented generation for historical, theological,
academic, apologetic, and other documentary sources. The system must improve
factual grounding while preserving enough provenance to explain exactly which
source, edition, artifact, and citable passage supports a generated answer.

The canonical Bible corpus already has deterministic lookup, immutable corpus
versions, provenance, approval, and stable references. RAG must complement that
boundary rather than turn Scripture into an approximate nearest-neighbour lookup.

The first RAG implementation must also remain simple enough to evaluate and
operate locally. Retrieval quality, grounding quality, and citation correctness
must be measurable independently.

## Decision drivers

- Reproducible and auditable citations
- Clear separation between bibliographic identity and technical retrieval data
- Support for original texts, translations, editions, and digital artifacts
- Explicit handling of disputed or interpretive metadata
- Multi-perspective historical and theological research
- Semantic retrieval across differently worded but conceptually related passages
- Isolation of vector-search workload from the transactional application database
- Rebuildable chunks and embeddings
- Independent evaluation of retrieval and generation
- Minimal operational complexity compatible with the current local-first system

## Decision

### 1. Keep canonical Bible retrieval deterministic

The approved Bible corpus remains canonical reference data in the application
PostgreSQL database and is queried by exact normalized references.

RAG may later project Bible content into search indexes when a use case requires
semantic discovery, but such projections are derived and rebuildable. They never
become the canonical Bible source and never replace exact passage lookup.

This decision is consistent with ADR 0001, which separates canonical Bible
storage from future search and RAG projections.

### 2. Use a dedicated PostgreSQL Knowledge Store with pgvector

RAG knowledge data is stored in a PostgreSQL instance dedicated to the Knowledge
Store, separate from the transactional application PostgreSQL instance.

The Knowledge Store uses pgvector for dense embeddings and vector indexes.
PostgreSQL full-text search remains available in the same Knowledge Store so
lexical and vector retrieval can later be combined without duplicating canonical
knowledge metadata across two products.

The initial vector index strategy is HNSW unless evaluation demonstrates a
better option for the actual corpus and workload.

Qdrant and other dedicated vector databases are not part of v1. They remain
replaceable infrastructure options behind retrieval abstractions if measured
scale or performance later justifies them.

### 3. Model bibliographic identity as Work, Expression, Manifestation, Artifact

Documentary identity follows a pragmatic WEMI-inspired model:

```text
KnowledgeWork
    |
    v
KnowledgeExpression
    |
    v
KnowledgeManifestation
    |
    v
SourceArtifact
```

A `KnowledgeWork` identifies the intellectual work.

A `KnowledgeExpression` identifies a particular textual or linguistic realization,
such as the original Greek text, an English translation, or a revised
translation.

A `KnowledgeManifestation` identifies a citable publication or edition that
materializes an expression, including publisher, publication details, edition
statement, pagination scheme, and external publication identifiers when known.

A `SourceArtifact` identifies the exact digital object acquired by ApologiaStudio,
such as a PDF, HTML snapshot, XML document, EPUB, or text file.

Expression-to-expression relationships such as `TranslationOf`, `RevisionOf`,
and `DerivedFrom` are explicit rather than inferred from edition metadata.

### 4. Model bibliographic responsibility with contributors and contributions

People and collective bodies are represented as `KnowledgeContributor`
identities. Roles belong to contribution relationships, not to contributors
themselves.

Examples include:

```text
Author       -> KnowledgeWork
Translator   -> KnowledgeExpression
Reviser      -> KnowledgeExpression
Publisher    -> KnowledgeManifestation
IssuingBody  -> KnowledgeWork or KnowledgeManifestation as appropriate
```

A contribution records at least its role, ordering where meaningful, and an
attribution status capable of representing established, traditional, probable,
possible, or disputed responsibility.

Technical tools and operators used for OCR, parsing, or normalization are not
bibliographic contributors. They belong to processing provenance.

### 5. Make artifact processing provenance explicit and immutable

Artifacts are immutable. A download, OCR pass, parse, normalization, correction,
or other material transformation produces a new artifact rather than silently
modifying an existing one.

Processing provenance follows this minimal shape:

```text
SourceArtifact
    |
    v
ProcessingActivity
    |
    v
DerivedArtifact
```

Every artifact records integrity information including SHA-256 and media type.

Every material processing activity records enough information to reproduce or
audit the transformation, including:

- activity type;
- input and output artifact identifiers;
- tool name and version;
- relevant configuration or policy version;
- start and completion timestamps;
- executing human, service, or pipeline identity where applicable;
- processing status.

This is intentionally inspired by W3C PROV concepts without adopting the full
PROV model.

### 6. Separate citable segments from retrieval chunks

`DocumentSegment` is the stable intellectual unit used for citation.

Examples include a book, chapter, section, numbered paragraph, canon, article,
or other structurally meaningful passage. A segment records its artifact,
hierarchy, order, text, and stable locator when available.

`RetrievalChunk` is a technical and rebuildable search projection. Chunk size,
overlap, grouping, and embedding strategy may change without changing citable
segments or historical citations.

A chunk may map to one or more document segments. Mapping metadata preserves
the relevant offsets where needed.

The governing rule is:

> The chunk serves retrieval. The segment serves evidence and citation.

### 7. Treat interpretive metadata as assertions

Technical facts such as SHA-256, byte size, media type, and acquisition timestamp
are direct properties.

Metadata that can be interpretive, disputed, historically uncertain, or
editorially classified is represented as a traceable `MetadataAssertion`.

An assertion records at least:

- target resource and property;
- asserted value;
- assertion origin;
- assertion timestamp;
- review status;
- reviewer and review timestamp when applicable;
- justification when applicable;
- supporting segment when applicable;
- the assertion it supersedes when applicable.

Validated assertions are not silently overwritten. Competing assertions may
coexist when the underlying scholarship or attribution is genuinely disputed.

### 8. Keep source kind, perspective, and evidence role independent

These dimensions answer different questions:

```text
SourceKind    = what the source is
Perspective   = the standpoint from which it speaks
EvidenceRole  = how it can function in an analysis
```

Examples of `SourceKind` include primary source, academic secondary source,
confessional document, commentary, reference work, and apologetic work.

Perspective uses a controlled, hierarchical taxonomy. It supports both declared
perspective and analytical classification. Historical categories must be
period-appropriate; modern confessional labels are not projected backwards
without justification.

Examples of `EvidenceRole` include doctrinal definition, historical witness,
modern scholarship, textual commentary, confessional position, apologetic
argument, counter-position, and reference material.

Perspective and evidence role are not credibility scores. Retrieval must not
encode a rule that one confession or viewpoint is intrinsically more truthful
than another.

### 9. Separate technical, editorial, and metadata status

Three status dimensions remain independent:

```text
ArtifactLifecycleStatus
EditorialReviewStatus
MetadataAssertionStatus
```

`ArtifactLifecycleStatus` represents whether an artifact is active, superseded,
retired, corrupted, or deleted.

`EditorialReviewStatus` represents whether a resource is pending, in review,
approved, or rejected for production use.

`MetadataAssertionStatus` represents whether a metadata assertion is proposed,
verified, rejected, disputed, or superseded.

Only resources approved for production retrieval may contribute evidence to
normal user answers.

### 10. Treat embeddings as derived projections

Embeddings, vector indexes, full-text indexes, retrieval chunks, ranking
features, and prompt-ready evidence are derived, rebuildable projections.

They are never the documentary system of record.

Every retrieval chunk must retain enough linkage to resolve its contributing
`DocumentSegment` records and therefore its exact documentary provenance.

Embedding generation records at least the embedding model identity/version,
vector dimensions, and chunking strategy/version needed to reproduce the
projection.

### 11. Target hybrid retrieval, but evaluate each retrieval mode separately

The intended v1 retrieval architecture supports:

```text
lexical retrieval
        +
dense vector retrieval
        |
        v
deterministic rank fusion
```

Initial hybrid fusion should remain deterministic, such as Reciprocal Rank
Fusion, unless evaluation justifies a learned or model-based reranker.

Lexical, vector, and hybrid retrieval must be independently measurable so the
system can demonstrate whether additional retrieval complexity improves actual
evidence recall.

### 12. Build citations from evidence identifiers, not model-generated references

The LLM receives bounded evidence identifiers such as `[E1]`, `[E2]`, and `[E3]`
alongside retrieved passages.

The model may select those identifiers when supporting a claim. It must not be
trusted to invent bibliographic references, locators, URLs, authors, or edition
metadata.

The application resolves an evidence identifier through:

```text
Evidence
  -> DocumentSegment
  -> Artifact
  -> Manifestation
  -> Expression
  -> Work
```

and renders the citation from stored, reviewed metadata.

A retrieval chunk may explain how evidence was found, but it is not itself the
public citation target.

### 13. Treat retrieved content as untrusted data

Retrieved documents may contain erroneous claims, adversarial text, or prompt
injection content.

Retrieved content is data, not an instruction channel. Prompt text from a source
must not gain system or tool authority merely because the source was retrieved.

Authentication, authorization, corpus approval, metadata validation, and
consequential controls remain software responsibilities outside the LLM.

### 14. Keep MCP outside the initial retrieval hot path

The initial Knowledge Store is accessed through an application-facing retrieval
contract implemented in-process.

`ApologiaStudio.Mcp.KnowledgeServer` is not inserted between the application and
the Knowledge Store for v1. MCP may later expose the same retrieval capability
to external clients if a real interoperability requirement appears.

This avoids adding a protocol and service boundary before there is a consumer
that requires it.

### 15. Evaluate retrieval separately from grounded generation

The first retrieval evaluation set is built from representative historical and
theological questions with expected evidence segments.

Initial retrieval measures include:

- Recall@5;
- Mean Reciprocal Rank (MRR).

The system compares lexical, vector, and hybrid retrieval independently.

Only after retrieval quality is measurable is retrieval connected to the main
LLM runtime. Grounded-generation evaluation then compares model-only answers
with RAG-grounded answers, including factual correctness and citation support.

## Out of scope for RAG v1

The following are explicitly deferred until measured evidence or a concrete
product requirement justifies them:

- Qdrant or another separate vector database;
- GraphRAG or a knowledge graph;
- neural or LLM reranking;
- agentic or multi-hop query decomposition;
- generalized OCR pipelines for arbitrary scanned corpora;
- massive bulk ingestion;
- advanced claim-to-evidence graph modeling;
- MCP exposure of the Knowledge Store;
- fully automated acceptance of AI-generated metadata.

## Consequences

### Positive

- Citations remain tied to exact, auditable documentary evidence.
- Translations and publications are no longer conflated with intellectual works.
- Retrieval chunks and embeddings can evolve without invalidating citations.
- Vector workload is operationally isolated from the transactional application
  database.
- PostgreSQL keeps metadata, provenance, lexical search, and vectors together
  inside the Knowledge Store.
- Conflicting historical or theological classifications can coexist explicitly.
- The LLM cannot create a valid application citation merely by inventing a
  plausible bibliography.
- Retrieval quality can be measured before generation quality obscures failures.

### Costs

- The Knowledge domain contains more entities than a minimal chunk-and-vector
  implementation.
- Ingestion must preserve provenance and lifecycle information rather than merely
  inserting text and embeddings.
- A second PostgreSQL instance must be operated and backed up.
- Editorial review is required for important metadata and corpus activation.
- Chunk and embedding rebuilds require versioned projection configuration.

These costs are accepted because provenance, factual grounding, and citation
quality are core product requirements rather than optional enhancements.

## Implementation sequence

1. Add the dedicated PostgreSQL + pgvector Knowledge Store and independent test
   database.
2. Implement the accepted Knowledge persistence model and integrity constraints.
3. Ingest one real approved document through artifact provenance to stable
   document segments.
4. Generate versioned retrieval chunks and embeddings.
5. Add vector retrieval, then lexical/vector hybrid retrieval.
6. Add a retrieval evaluation dataset and compare lexical, vector, and hybrid
   Recall@5 and MRR.
7. Connect retrieved evidence to `qwen3.6:27b` with application-resolved citation
   identifiers.
8. Compare model-only and RAG-grounded answers before expanding corpus scale or
   retrieval complexity.
