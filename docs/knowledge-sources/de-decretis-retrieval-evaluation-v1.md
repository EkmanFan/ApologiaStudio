# De Decretis retrieval evaluation v1

## Scope

This profile adds the first labelled retrieval-quality evaluation for the approved
`De Decretis` corpus.

It evaluates retrieval only. It does not evaluate answer generation, citation wording,
historical truth, theological correctness, or LLM reasoning.

The purpose of the first run is to establish an honest baseline for the dense retrieval
implemented in 6F. No quality threshold is selected after seeing the results.

## Dataset

Dataset:

```text
evaluations/knowledge/de-decretis-retrieval-v1.json
```

Profile:

```text
de-decretis-retrieval-evaluation-v1
```

The v1 gold set contains ten manually curated questions:

- English and French paraphrases are both represented;
- relevance is labelled at `DocumentSegment` level, because segments are the stable
  citable evidence unit;
- the questions cover council conduct, the Son as Word/Wisdom/Power, the Nicene
  expressions, the objection that those expressions are not verbatim Scripture,
  the material/composite objection, earlier authorities, and the term
  `unoriginate`;
- the source's own editorial structure and the primary text are used to assign the
  relevant numbered sections.

This is deliberately a small development benchmark. It is not representative enough
to justify production-level retrieval claims.

## Ranking unit

The search engine ranks `RetrievalChunk` rows, but several chunks may belong to the
same citable section. Evaluating raw chunk ranks would reward a section simply because
it was split into multiple overlapping chunks.

The evaluator therefore:

1. retrieves up to 20 chunk candidates;
2. preserves their search order;
3. collapses duplicate `DocumentSegment` ordinals by first occurrence;
4. computes retrieval metrics over the resulting citable-segment ranking.

The retrieval implementation itself is not changed by 6G.

## Metrics

### Recall@5

For each query:

```text
relevant gold segments found in the first 5 unique retrieved segments
----------------------------------------------------------------------
                     number of gold segments
```

The reported `Recall@5` is the macro-average over evaluation cases.

A case may have more than one relevant segment. This is intentional for questions
whose evidence spans several adjacent numbered sections.

### Mean Reciprocal Rank (MRR)

For each query, reciprocal rank is:

```text
1 / rank of the first relevant unique segment
```

or zero when no relevant segment appears in the candidate ranking.

MRR is the mean across all cases. It measures how early the first useful citable
segment appears.

### HitRate@5

`HitRate@5` is reported as a supplemental diagnostic: the fraction of questions for
which at least one relevant segment appears in the first five unique segments.

It does not replace Recall@5.

## Exact versus HNSW

Both 6F modes are evaluated against the identical dataset:

- `exact` remains the full-precision reference retrieval path;
- `hnsw` validates the approximate `halfvec(2560)` path against the same gold labels.

The application script records both metric sets but does not require them to be equal.
Approximate retrieval is allowed to differ; the difference is evidence to inspect,
not something to hide with a test threshold.

## Baseline policy

6G deliberately has no arbitrary pass/fail target such as `Recall@5 >= 0.90`.

The first labelled run establishes the baseline. After the dataset has been reviewed
and enlarged, explicit regression thresholds can be selected prospectively and
versioned. This avoids tuning the benchmark or thresholds merely to make the current
implementation pass.

A failed command, invalid dataset, model-digest mismatch, invalid evidence mapping,
missing HNSW path, or invalid metric is still an engineering failure and causes the
validation script to fail.

## Reproducibility

The evaluation pins the existing 6F/6E contracts:

- source profile: `de-decretis-npnf2-04-v1`;
- search profile: `de-decretis-vector-search-v1`;
- retrieval profile: `de-decretis-retrieval-qwen3-embedding-4b-v1`;
- embedding model: `qwen3-embedding:4b`;
- model digest is checked before and after batch query embedding;
- retrieval continues to enforce approved-source and evidence-integrity filters.

No Knowledge database schema or persisted content is modified by this increment.

## Running manually

```bash
bash scripts/evaluate-de-decretis-retrieval.sh exact
bash scripts/evaluate-de-decretis-retrieval.sh hnsw
```

The output includes per-case rankings plus machine-readable aggregate lines:

```text
METRIC Recall@5=...
METRIC MRR=...
METRIC HitRate@5=...
```

These values are measurements, not generation-quality claims.
