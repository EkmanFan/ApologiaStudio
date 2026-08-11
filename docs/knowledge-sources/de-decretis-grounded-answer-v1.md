# De Decretis grounded answer v1

## Scope

This increment adds the first end-to-end grounded-generation vertical slice over the
approved `De Decretis` knowledge source.

The pipeline is deliberately deterministic around the LLM step:

```text
user question
  -> qwen3-embedding:4b query embedding
  -> approved vector retrieval
  -> five citable DocumentSegment evidence records
  -> qwen3.6:27b structured generation
  -> application validation
  -> application-resolved citations
```

This is a bounded RAG workflow, not an autonomous agent.

## Profiles

- source: `de-decretis-npnf2-04-v1`;
- retrieval: `de-decretis-retrieval-qwen3-embedding-4b-v1`;
- search: `de-decretis-vector-search-v1`;
- grounded answer: `de-decretis-grounded-answer-v1`;
- generation model: `qwen3.6:27b`.

The embedding and generation model digests are resolved immediately before use and
checked again after the corresponding model call. A model update under the same tag is
therefore detected rather than silently accepted during a request.

## Context construction

The search step requests twenty chunk candidates, then collapses them by stable
`DocumentSegment` identity and supplies at most five unique segments to generation.

The LLM receives the persisted segment text and application-assigned evidence IDs
(`E1`, `E2`, ...). The evidence IDs are ephemeral request-local handles. They are not
citations and they are not persisted knowledge identifiers.

`DocumentSegment` remains the citable evidence unit.

## Structured generation contract

Ollama `/api/chat` is called non-streaming with `think=false`, temperature zero, and a
JSON schema. The model must return:

```json
{
  "status": "answered",
  "claims": [
    {
      "text": "A concise supported claim.",
      "evidenceIds": ["E1"]
    }
  ]
}
```

or, when the supplied context is insufficient:

```json
{
  "status": "insufficient_evidence",
  "claims": []
}
```

Schema conformance is not treated as proof of semantic correctness. Application code
validates the returned status, claim count, claim length, evidence-id count, uniqueness,
and membership in the exact evidence set supplied to the model.

Unknown or duplicate evidence IDs are rejected.

## Application-resolved citations

The model never writes bibliographic citation strings.

For an accepted claim, application code resolves each validated evidence ID back to the
retrieved `DocumentSegment`, then renders the stored citation label and segment locator.
For example:

```text
[1] NPNF2-04, De Decretis — §20; NPNF pp. 510–512
```

This prevents a model-generated page number, work title, or section identifier from
becoming an authoritative citation merely because it looks plausible.

## Attribution

`De Decretis` is a pro-Nicene primary source written by Athanasius, not a neutral modern
historical synthesis. The generation instructions therefore require attribution when a
claim is Athanasius's argument, interpretation, or report.

This is a generation rule, not a proof that every generated sentence is historically
correct. Generation faithfulness and citation support require their own evaluation.

## Retrieved content is untrusted

Retrieved source text is treated as untrusted data. The generation prompt explicitly
instructs the model not to follow instruction-like text found inside evidence, and the
model receives no tools in this workflow.

That does not make prompt instructions a security boundary. Indirect prompt-injection
resistance must be tested separately as part of the AI-security work.

## What this increment proves

A successful smoke run demonstrates that:

- a real question can flow through retrieval into local generation;
- generation uses `qwen3.6:27b`;
- the response is machine-validated structured output;
- every emitted citation reference is resolved by software against retrieved evidence;
- the final displayed citation targets a `DocumentSegment`, not a `RetrievalChunk`.

It does not prove that every claim is faithful, historically correct, complete, or
optimally cited. Those are evaluation concerns, not properties guaranteed by RAG.

## Manual run

```bash
bash scripts/answer-de-decretis-grounded.sh
```

Optional arguments:

```bash
bash scripts/answer-de-decretis-grounded.sh \
  "Why did the Council use the expression one in essence?" \
  hnsw
```
