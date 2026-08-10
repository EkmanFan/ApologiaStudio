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
