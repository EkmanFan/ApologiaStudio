# De Decretis vector reranker evaluation v1

## Purpose

This increment measures whether a second-stage reranker improves the already strong vector retrieval baseline without reducing Recall@5.

It is an evaluation increment only. The grounded-answer hot path remains unchanged until the measured benefit justifies the additional inference cost.

## Frozen inputs

- Source profile: `de-decretis-npnf2-04-v1`
- Retrieval projection: `de-decretis-retrieval-qwen3-embedding-4b-v1`
- Vector search: `de-decretis-vector-search-v1`
- Labelled dataset: `evaluations/knowledge/de-decretis-retrieval-v1.json`
- Embedding model: `qwen3-embedding:4b`
- Vector baseline: Recall@5 `0.966667`, MRR `0.816667`, HitRate@5 `1.000000`

## Reranker design

Profile: `de-decretis-vector-reranker-v1`

The first experiment uses the already-approved local `qwen3.6:27b` model as a deterministic **listwise LLM reranker** rather than introducing another resident model.

This is deliberate:

1. the earlier local model-switching benchmark showed that swapping models on the 24 GB GPU is expensive;
2. `qwen3.6:27b` is already the generation model used by the grounded-answer path;
3. using the same resident model lets the experiment measure quality before adding another runtime dependency;
4. this is not a claim that `qwen3.6:27b` is a specialized cross-encoder reranker.

A dedicated reranker remains a separate future experiment if the listwise approach is insufficient or its cost is unjustified.

## Candidate pipeline

```text
query
  -> qwen3-embedding:4b
  -> exact vector retrieval, top 20 chunks
  -> deduplicate to top 10 DocumentSegments
  -> qwen3.6:27b listwise ordering
  -> top 5 DocumentSegments
```

The reranker receives only the representative best-ranked retrieval chunk for each candidate segment. It receives opaque candidate IDs and untrusted source text. It must return every candidate ID exactly once in ranked order.

The application validates the structured ordering before accepting it.

## Evaluation

The evaluation reuses the frozen 6G labels. It reports:

- CandidateRecall@10: upper-bound recall available to the reranker;
- Recall@5;
- MRR;
- HitRate@5;
- per-language metrics;
- total and average reranker inference time.

Run:

```bash
bash scripts/evaluate-de-decretis-reranker.sh exact
```

## Decision rule

The reranker must not enter the grounded-answer hot path merely because it can improve one or two examples.

The primary acceptance condition is **no Recall@5 regression**. An MRR improvement without recall loss is evidence in favour of the reranker, but must be weighed against the measured latency and confirmed on a larger corpus before any production claim.

If Recall@5 regresses, the reranker is rejected for the hot path in this profile. If metrics tie, the simpler vector-only path remains preferred.
