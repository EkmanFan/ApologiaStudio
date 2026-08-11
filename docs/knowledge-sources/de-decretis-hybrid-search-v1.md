# De Decretis hybrid retrieval v1

## Purpose

This increment measures whether PostgreSQL lexical retrieval adds useful evidence to the existing vector retrieval path for the curated `De Decretis` source.

It does not replace the vector path and it does not change grounded generation yet. The implementation is deliberately measurement-first: hybrid retrieval becomes a production candidate only if the labelled evaluation shows a material benefit without degrading recall.

## Frozen inputs

- Source profile: `de-decretis-npnf2-04-v1`
- Retrieval projection: `de-decretis-retrieval-qwen3-embedding-4b-v1`
- Vector search: `de-decretis-vector-search-v1`
- Lexical search: `de-decretis-lexical-search-v1`
- Labelled dataset: `evaluations/knowledge/de-decretis-retrieval-v1.json`
- Embedding model: `qwen3-embedding:4b`
- Embedding dimensions: 2560

The hybrid evaluation intentionally reuses the frozen 6G dataset rather than creating another copy of the relevance labels.

## Fusion design

Profile: `de-decretis-hybrid-search-v1`

Fusion strategy: `segment-level-rrf`

Reciprocal Rank Fusion is calculated as:

```text
score(segment) = Σ 1 / (60 + rank_in_branch)
```

The branches are:

1. vector retrieval;
2. PostgreSQL lexical retrieval.

The RRF constant is 60 and is not learned from the evaluation dataset.

### Why fusion occurs at DocumentSegment level

Both retrieval branches rank technical `RetrievalChunk` objects. A single citable `DocumentSegment` can own multiple chunks. Before RRF is applied, each branch is therefore deduplicated by segment and the best-ranked chunk establishes that branch's segment rank.

This prevents a long segment from receiving an artificial advantage merely because it produced more chunks.

The final ranking is consequently a ranking of citable evidence segments, while retaining one representative chunk for diagnostics.

## Determinism and trust boundaries

- vector and lexical retrieval remain independent;
- RRF is deterministic application code;
- no LLM performs fusion or reranking;
- no learned weights are introduced;
- no database schema or index is added;
- only already-approved Knowledge resources can enter either branch because both existing retrievers enforce the editorial filters;
- citations continue to target `DocumentSegment`, never `RetrievalChunk`.

## Candidate depth

Each branch retrieves at most 20 chunks. After per-branch segment deduplication, the two ranked lists are fused. The evaluation can therefore inspect up to 40 distinct candidate segments.

The user-facing smoke search defaults to the top 5 fused segments.

## Evaluation

Run:

```bash
bash scripts/evaluate-de-decretis-hybrid.sh exact
```

The evaluation reports:

- Recall@5;
- MRR;
- HitRate@5;
- per-language metrics.

The application script for 6J also re-runs the frozen vector and lexical baselines before measuring hybrid retrieval, so the comparison is made on the same source state, model digest and labelled cases.

## Decision rule

6J is a measurement increment, not an automatic production switch.

The hybrid path should not replace the vector path merely because it exists. The measured result must be compared with the frozen vector baseline. A degradation in Recall@5 is a strong reason not to put hybrid retrieval in the grounded-answer hot path. A gain in MRR without recall loss is evidence in favour of the hybrid path, but should still be confirmed on a larger corpus before production claims are made.

No reranker is introduced in this increment.
