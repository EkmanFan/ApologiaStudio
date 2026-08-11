# De Decretis vector search v1

## Scope

This increment adds the first query-time retrieval path over the approved `De Decretis` retrieval projection.

It deliberately implements only dense vector retrieval. It does not add lexical search, hybrid fusion, reranking, query decomposition, LLM answer generation, or retrieval-quality claims. Those remain separate increments so retrieval can be evaluated independently.

## Query path

```text
user query
  -> Qwen3 query instruction
  -> qwen3-embedding:4b
  -> 2,560-dimensional query embedding
  -> PostgreSQL + pgvector
  -> top-k RetrievalChunk
  -> DocumentSegment evidence
```

`RetrievalChunk` remains a technical search unit. `DocumentSegment` remains the citable evidence unit.

## Query instruction

Qwen3-Embedding recommends a one-sentence task instruction on retrieval queries while documents need no retrieval instruction. The v1 query task is:

> Given a user question about approved historical and theological sources, retrieve passages that provide evidence relevant to answering the question.

The instruction is intentionally written in English even when the user query is in another supported language, following Qwen's recommendation for multilingual retrieval instructions.

## Search modes

### Exact

`exact` is the reference implementation for 6F. It ranks the stored full-precision `vector(2560)` embeddings by cosine distance.

This is an exact nearest-neighbor scan and therefore provides the retrieval baseline against which approximate search can later be evaluated.

### HNSW

`hnsw` uses the same full-precision stored embeddings but indexes an expression cast to `halfvec(2560)` with cosine distance:

```sql
(embedding::halfvec(2560)) halfvec_cosine_ops
```

The HNSW index is partial and applies only to the pinned retrieval profile and dimension. This is important because the shared embedding table may legally contain other embedding profiles and dimensions.

The diagnostic CLI forces PostgreSQL to use an index-capable plan for `hnsw` mode and verifies that the expected HNSW index is present in the query plan. `hnsw.ef_search` is set to 100 for this diagnostic path.

With only 65 chunks, HNSW is not needed for performance. It is included now to prove the scalable query path without pretending that approximate search improves quality.

## Approval and provenance filters

Search results are accepted only when the persisted chain is editorially approved at these citable/source levels:

- Work;
- Expression;
- Manifestation;
- Artifact;
- DocumentSegment.

The query also pins:

- retrieval profile;
- embedding provider;
- embedding model;
- exact Ollama model digest;
- embedding dimensions;
- chunking strategy and version.

A query-time Ollama model digest change is rejected.

## Evidence integrity

Every returned chunk is revalidated against its persisted `DocumentSegment` offsets. If the chunk text no longer equals the exact segment substring represented by those offsets, retrieval fails rather than returning unverifiable evidence.

## CLI

```bash
bash scripts/search-de-decretis-retrieval.sh exact \
  "Why did the Council use the phrase one in essence?" 5

bash scripts/search-de-decretis-retrieval.sh hnsw \
  "Why did the Council use the phrase one in essence?" 5
```

The command prints distance/similarity, work, manifestation citation label, segment locator, and chunk text. This output is diagnostic; 6H will later resolve application citations from `DocumentSegment`.

## What 6F does not establish

A successful query proves that the vector retrieval path works. It does **not** prove that the retrieved passages are the best passages for real user questions.

That requires a labelled retrieval evaluation set and metrics such as Recall@k and MRR in 6G.

## Primary references

- Qwen3 Embedding: https://github.com/QwenLM/Qwen3-Embedding
- pgvector: https://github.com/pgvector/pgvector
- pgvector-dotnet: https://github.com/pgvector/pgvector-dotnet
- EF Core custom migration operations: https://learn.microsoft.com/ef/core/managing-schemas/migrations/operations
