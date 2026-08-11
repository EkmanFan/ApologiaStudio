# De Decretis retrieval projection v1

## Scope

This profile adds the first rebuildable retrieval projection for the approved `De Decretis` source imported by profile `de-decretis-npnf2-04-v1`.

It deliberately stops at deterministic retrieval chunks plus dense embeddings. It does not add vector search, HNSW, hybrid retrieval, reranking, query decomposition, or LLM grounding. Those belong to later RAG increments.

## Profile

| Setting | Value |
| --- | --- |
| Retrieval profile | `de-decretis-retrieval-qwen3-embedding-4b-v1` |
| Source artifact | approved normalized `De Decretis` artifact |
| Chunking strategy | `segment-character-window` |
| Chunking version | `v1` |
| Maximum chunk size | 1,800 characters |
| Nominal overlap | 300 characters |
| Embedding provider | Ollama |
| Embedding model | `qwen3-embedding:4b` |
| Stored dimensions | 2,560 (native 4B dimension) |

## Chunking contract

`DocumentSegment` remains the stable citable evidence unit. `RetrievalChunk` is a rebuildable search projection.

For v1:

- a chunk never crosses a `DocumentSegment` boundary;
- each chunk maps to exactly one source segment with exact start/end character offsets;
- chunk text must equal the exact segment substring represented by those offsets;
- consecutive chunks may overlap but must not leave gaps;
- boundaries prefer nearby sentence punctuation, then whitespace, before falling back to the hard character limit;
- stable chunk identifiers are derived from the retrieval profile, segment identifier, and offsets.

Citations must continue to resolve to `DocumentSegment`, never to `RetrievalChunk`.

## Embedding contract

Qwen3-Embedding-4B has a native embedding dimension of 2,560 and supports Matryoshka Representation Learning (custom output dimensions). This profile deliberately keeps the native 2,560 dimensions and requests them explicitly through Ollama's `/api/embed` endpoint with `truncate=false`.

pgvector can store full-precision `vector` values up to 16,000 dimensions, while a direct HNSW `vector` index is limited to 2,000 dimensions. We do not reduce the stored representation merely to satisfy a future index constraint. In the retrieval increment, an HNSW half-precision expression index can cover 2,560 dimensions while the full-precision vectors remain available for exact scoring or reranking. No HNSW index is created in this increment.

Documents are embedded without a retrieval instruction. Query-side instruction handling is intentionally deferred to the query/retrieval increment; Qwen recommends task-specific instruction on the query side for retrieval.

Each embedding stores:

- provider;
- model tag;
- exact local Ollama model digest;
- embedding profile;
- dimensions;
- vector;
- creation timestamp.

A model digest mismatch is rejected under the same profile version; changing the model build requires an explicit new retrieval profile version.

## Reproducibility and safety

The projection command:

1. reparses and revalidates the pinned source PDF;
2. verifies the approved normalized artifact and all persisted source segments;
3. deterministically rebuilds and validates chunk boundaries;
4. resolves the exact installed Ollama model digest;
5. refuses partial or conflicting existing projections;
6. requests all document embeddings with truncation disabled;
7. validates embedding count, dimensions, and finite values;
8. writes chunks, mappings, and embeddings in one PostgreSQL transaction;
9. validates the persisted projection before commit.

The projection is idempotent. Re-running it with the same source, profile, and model digest returns `ALREADY PROJECTED`.

## Primary references

- Qwen3 Embedding repository: https://github.com/QwenLM/Qwen3-Embedding
- Ollama embedding API: https://docs.ollama.com/api/embed
- Ollama model tags API: https://docs.ollama.com/api/tags
- Qwen3 Embedding in Ollama: https://ollama.com/library/qwen3-embedding
- pgvector: https://github.com/pgvector/pgvector
- pgvector-dotnet: https://github.com/pgvector/pgvector-dotnet
