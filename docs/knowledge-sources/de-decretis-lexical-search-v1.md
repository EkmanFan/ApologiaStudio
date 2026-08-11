# De Decretis lexical search v1

## Scope

This increment adds the first PostgreSQL full-text lexical retrieval path over the approved `De Decretis` retrieval chunks.

It is deliberately separate from dense-vector retrieval. No hybrid fusion, reranking, query rewriting, or LLM generation is introduced here.

The goal is to measure what lexical retrieval can and cannot recover on the same labelled questions already used for the vector baseline.

## Query path

```text
user query
  -> PostgreSQL english text-search normalization
  -> normalized query lexemes
  -> OR-composed tsquery
  -> ts_rank_cd over approved RetrievalChunk text
  -> top-k RetrievalChunk
  -> DocumentSegment evidence
```

`RetrievalChunk` remains a technical search unit. `DocumentSegment` remains the citable evidence unit.

## Text-search configuration

The curated NPNF manifestation used in this vertical slice is English, so v1 uses PostgreSQL's `english` text-search configuration for both query normalization and chunk text.

This is a source-language decision, not a claim of multilingual lexical retrieval. French questions remain in the evaluation set specifically so their behavior is measured rather than assumed.

## Query strategy

Natural-language questions often contain several informative terms. Requiring every surviving lexeme to occur in a single chunk can over-constrain lexical candidate retrieval.

For v1, PostgreSQL first normalizes the query with `to_tsvector('english', ...)`. The resulting lexemes are then combined with OR semantics into a `tsquery`.

Example, conceptually:

```text
"Why did the Council use one in essence?"
  -> council, essenc, use
  -> 'council' | 'essenc' | 'use'
```

The user query is always passed as a parameter. Application code does not splice raw user text into SQL.

## Ranking

Matching chunks are ranked with:

```sql
ts_rank_cd(to_tsvector('english', chunk_text), query, 32)
```

`ts_rank_cd` incorporates term proximity. Normalization `32` maps the raw rank through `rank / (rank + 1)`; this changes the displayed scale, not the ordering.

The score is a lexical relevance signal only. It must not be interpreted as probability, factual reliability, theological authority, or citation quality.

## Approval and provenance filters

The lexical path preserves the same citable/source approval boundary used by vector retrieval:

- Work approved;
- Expression approved;
- Manifestation approved;
- Artifact approved;
- DocumentSegment approved.

The current vertical slice is pinned to the reviewed normalized `De Decretis` artifact SHA-256 and the existing chunking strategy/version. This prevents future unrelated chunks from silently changing this baseline.

Every returned chunk is revalidated against its persisted `DocumentSegment` offsets before it is accepted as evidence.

## No GIN index yet

This v1 path intentionally performs the text-search expression over the current 65-chunk corpus without adding a new schema object or GIN index.

That is a deliberate measurement-first choice: at this corpus size, performance does not justify another migration solely to prove lexical quality. PostgreSQL recommends GIN as the preferred full-text index when text search is performed regularly at larger scale. Indexing can therefore be added later when corpus size or latency measurements justify it.

## Evaluation

The lexical benchmark copies the human relevance labels from the frozen 6G dataset unchanged, but identifies the lexical search profile explicitly.

Run:

```bash
bash scripts/evaluate-de-decretis-lexical.sh
```

The evaluator reports:

- Recall@5;
- MRR;
- HitRate@5;
- the same metrics split by `en` and `fr`.

No quality threshold is imposed in this first lexical run. The result is a baseline to compare against vector retrieval and, later, hybrid fusion.

## Manual search

```bash
bash scripts/search-de-decretis-lexical.sh \
  "Why did the Council use the expressions from the essence and one in essence?" 5
```

The diagnostic output includes the normalized `tsquery`, lexical score, work, citation label, segment locator, chunk ordinal, and chunk text.

## What this increment does not establish

A successful run proves that lexical retrieval is operational and measurable. It does not prove that lexical search is superior to vector search or that either should be used alone.

Hybrid retrieval remains the next separate decision and should be justified by measured complementarity rather than by convention.

## Primary references

- PostgreSQL full-text search controls: https://www.postgresql.org/docs/current/textsearch-controls.html
- PostgreSQL preferred text-search indexes: https://www.postgresql.org/docs/current/textsearch-indexes.html
