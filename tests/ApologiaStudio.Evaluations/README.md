# ApologiaStudio AI Evaluations

The evaluation project separates deterministic engineering checks from local-model quality checks.

## Routing

`RoutingEvaluationTests` keeps the built-in routing regression baseline.

`DynamicRoutingEvaluationTests` adds:

- custom-agent routing;
- ambiguous boundaries between history, apologetics and argument analysis;
- evidence that `RoutingDescription` is supplied from a fresh routing snapshot on every turn;
- deterministic fallback when semantic routing fails;
- an optional local Ollama accuracy run.

The local run is enabled with:

```bash
OLLAMA_EVALUATIONS_ENABLED=true dotnet test tests/ApologiaStudio.Evaluations
```

Optional model overrides:

```bash
OLLAMA_ROUTING_MODEL=qwen3:8b
OLLAMA_RESPONSE_MODEL=qwen3:8b
OLLAMA_BASE_URL=http://127.0.0.1:11434
```

## Role fidelity

`RoleFidelityEvaluationTests` currently implements a hard canary rather than an LLM-as-judge:

- an evaluation-only custom agent must obey a system-prompt prefix contract;
- history produced by a different agent must not leak into its answer;
- TTFT, prompt/output tokens and Ollama durations are emitted in the test output.

This deliberately verifies the runtime contract before adding subjective semantic judging. A later increment can add representative role-quality datasets and human/LLM-assisted scoring without replacing these deterministic canaries.

## Model comparison

`ModelQualityEvaluationTests` adds a deterministic rubric over representative historical and Protestant-apologetic questions derived from real ApologiaStudio usage. It records:

- required-concept coverage;
- explicitly forbidden factual/anachronistic claims;
- raw responses for human review;
- TTFT, output tokens and total generation duration.

The rubric is intentionally not an LLM-as-judge. Coverage and forbidden-pattern scores are only coarse automated signals; factual nuance and depth still require human review of the raw answers.

Tests tagged `Benchmark=ModelComparison` run the same routing, role-fidelity and response-quality protocol against any model selected through `OLLAMA_ROUTING_MODEL` and `OLLAMA_RESPONSE_MODEL`.

## Model switching benchmark

`scripts/benchmark-ollama-model-switching.sh` measures Ollama residency and model-switching cost directly at the local runtime boundary. It deliberately does not change application routing or generation code.

The controlled scenarios compare:

- a hot `qwen3.6:27b` generation;
- switching from resident `qwen3:8b` to `qwen3.6:27b`;
- the hybrid `27B -> 8B routing -> 27B generation` path;
- a single-resident `27B routing -> 27B generation` path;
- an all-`8B` control path.

Each request records wall time plus Ollama `load_duration`, `prompt_eval_duration`, `eval_duration`, token counts and the models reported by `/api/ps`. The benchmark unloads running Ollama models between controlled scenarios so the measured load/swap cost is explicit rather than inferred.

Python is used only in this benchmark utility because the standard library provides robust JSON parsing and precise timing without adding a project/runtime dependency.
