#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import statistics
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

NS_TO_MS = 1_000_000.0
WARMUP_PROMPT = 'Réponds uniquement OK.'
ROUTE_PROMPT = (
    'Tu es un routeur. Choisis uniquement "historian" ou '
    '"protestant-apologist" pour cette demande : '
    '"Que s’est-il passé au concile de Nicée en 325 ?"'
)
ANSWER_PROMPT = (
    'En cinq phrases maximum, explique pourquoi la formule '
    '« une ousia, trois hypostases » ne doit pas être rétroprojetée '
    'telle quelle sur le concile de Nicée I de 325.'
)

SCENARIOS = [
    {
        'name': 'HOT_27B_GENERATION',
        'warm': 'candidate',
        'steps': [('answer', 'candidate', ANSWER_PROMPT, 96)],
    },
    {
        'name': 'SWITCH_8B_TO_27B',
        'warm': 'baseline',
        'steps': [('answer', 'candidate', ANSWER_PROMPT, 96)],
    },
    {
        'name': 'HYBRID_27B_8B_27B',
        'warm': 'candidate',
        'steps': [
            ('route', 'baseline', ROUTE_PROMPT, 24),
            ('answer', 'candidate', ANSWER_PROMPT, 96),
        ],
    },
    {
        'name': 'QUALITY_27B_27B',
        'warm': 'candidate',
        'steps': [
            ('route', 'candidate', ROUTE_PROMPT, 24),
            ('answer', 'candidate', ANSWER_PROMPT, 96),
        ],
    },
    {
        'name': 'FAST_8B_8B',
        'warm': 'baseline',
        'steps': [
            ('route', 'baseline', ROUTE_PROMPT, 24),
            ('answer', 'baseline', ANSWER_PROMPT, 96),
        ],
    },
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description='Measure controlled Ollama model-switching cost.'
    )
    parser.add_argument('--base-url', default='http://127.0.0.1:11434')
    parser.add_argument('--baseline-model', default='qwen3:8b')
    parser.add_argument('--candidate-model', default='qwen3.6:27b')
    parser.add_argument('--repetitions', type=int, default=2)
    parser.add_argument('--keep-alive', default='10m')
    parser.add_argument('--timeout-seconds', type=int, default=600)
    parser.add_argument('--output-dir', required=True)
    return parser.parse_args()


class OllamaClient:
    def __init__(self, base_url: str, timeout_seconds: int) -> None:
        self.base_url = base_url.rstrip('/')
        self.timeout_seconds = timeout_seconds

    def get_json(self, path: str) -> dict[str, Any]:
        request = urllib.request.Request(
            f'{self.base_url}{path}', method='GET'
        )
        try:
            with urllib.request.urlopen(
                request, timeout=self.timeout_seconds
            ) as response:
                return json.loads(response.read().decode('utf-8'))
        except urllib.error.URLError as exc:
            raise RuntimeError(f'GET {path} failed: {exc}') from exc

    def chat(
        self,
        model: str,
        prompt: str,
        num_predict: int,
        keep_alive: str,
    ) -> tuple[dict[str, Any], float]:
        payload = {
            'model': model,
            'messages': [{'role': 'user', 'content': prompt}],
            'stream': False,
            'think': False,
            'keep_alive': keep_alive,
            'options': {'temperature': 0, 'num_predict': num_predict},
        }
        request = urllib.request.Request(
            f'{self.base_url}/api/chat',
            data=json.dumps(payload).encode('utf-8'),
            headers={'Content-Type': 'application/json'},
            method='POST',
        )
        started = time.perf_counter_ns()
        try:
            with urllib.request.urlopen(
                request, timeout=self.timeout_seconds
            ) as response:
                parsed = json.loads(response.read().decode('utf-8'))
        except urllib.error.URLError as exc:
            raise RuntimeError(
                f'Ollama request failed for {model}: {exc}'
            ) from exc
        wall_ms = (time.perf_counter_ns() - started) / NS_TO_MS
        if not parsed.get('done'):
            raise RuntimeError(f'Ollama did not complete request for {model}.')
        return parsed, wall_ms

    def ps(self) -> list[dict[str, Any]]:
        return list(self.get_json('/api/ps').get('models', []))


def ns_to_ms(value: Any) -> float:
    return 0.0 if value is None else float(value) / NS_TO_MS


def resolve_model(
    alias: str,
    baseline_model: str,
    candidate_model: str,
) -> str:
    if alias == 'baseline':
        return baseline_model
    if alias == 'candidate':
        return candidate_model
    raise ValueError(f'Unknown model alias: {alias}')


def unload_all(client: OllamaClient) -> None:
    for item in client.ps():
        name = str(item.get('name') or item.get('model') or '').strip()
        if name:
            subprocess.run(
                ['ollama', 'stop', name],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                check=False,
            )

    deadline = time.monotonic() + 20.0
    while time.monotonic() < deadline:
        if not client.ps():
            return
        time.sleep(0.25)
    raise RuntimeError('Ollama still reports loaded models after cleanup.')


def state_text(client: OllamaClient) -> str:
    models = client.ps()
    if not models:
        return 'none'
    values: list[str] = []
    for item in models:
        name = str(item.get('name') or item.get('model') or '?')
        vram_mib = int(item.get('size_vram') or 0) / 1024 / 1024
        values.append(f'{name}:vramMiB={vram_mib:.0f}')
    return ','.join(values)


def emit_state(
    client: OllamaClient,
    scenario: str,
    repetition: int,
    step: str,
) -> None:
    print(
        f'MODEL_STATE|scenario={scenario}|rep={repetition}|'
        f'step={step}|models={state_text(client)}',
        flush=True,
    )


def request_metrics(
    client: OllamaClient,
    *,
    scenario: str,
    repetition: int,
    step: str,
    model: str,
    prompt: str,
    num_predict: int,
    keep_alive: str,
) -> dict[str, Any]:
    response, wall_ms = client.chat(
        model, prompt, num_predict, keep_alive
    )
    result = {
        'step': step,
        'model': model,
        'wallMs': wall_ms,
        'totalMs': ns_to_ms(response.get('total_duration')),
        'loadMs': ns_to_ms(response.get('load_duration')),
        'promptEvalMs': ns_to_ms(response.get('prompt_eval_duration')),
        'evalMs': ns_to_ms(response.get('eval_duration')),
        'promptTokens': int(response.get('prompt_eval_count') or 0),
        'outputTokens': int(response.get('eval_count') or 0),
        'doneReason': str(response.get('done_reason') or 'unknown'),
    }
    print(
        'SWAP_REQUEST|'
        f'scenario={scenario}|rep={repetition}|step={step}|model={model}|'
        f'wallMs={result["wallMs"]:.1f}|'
        f'ollamaTotalMs={result["totalMs"]:.1f}|'
        f'loadMs={result["loadMs"]:.1f}|'
        f'promptEvalMs={result["promptEvalMs"]:.1f}|'
        f'evalMs={result["evalMs"]:.1f}|'
        f'promptTokens={result["promptTokens"]}|'
        f'outputTokens={result["outputTokens"]}|'
        f'doneReason={result["doneReason"]}',
        flush=True,
    )
    return result


def run_scenario(
    client: OllamaClient,
    *,
    definition: dict[str, Any],
    repetition: int,
    baseline_model: str,
    candidate_model: str,
    keep_alive: str,
) -> dict[str, Any]:
    scenario = str(definition['name'])
    unload_all(client)
    emit_state(client, scenario, repetition, 'after-unload')

    warm_model = resolve_model(
        str(definition['warm']), baseline_model, candidate_model
    )
    warmup = request_metrics(
        client,
        scenario=scenario,
        repetition=repetition,
        step='warmup',
        model=warm_model,
        prompt=WARMUP_PROMPT,
        num_predict=1,
        keep_alive=keep_alive,
    )
    emit_state(client, scenario, repetition, 'after-warmup')

    steps: list[dict[str, Any]] = []
    for step_name, model_alias, prompt, num_predict in definition['steps']:
        model = resolve_model(model_alias, baseline_model, candidate_model)
        step_result = request_metrics(
            client,
            scenario=scenario,
            repetition=repetition,
            step=step_name,
            model=model,
            prompt=prompt,
            num_predict=num_predict,
            keep_alive=keep_alive,
        )
        steps.append(step_result)
        emit_state(client, scenario, repetition, f'after-{step_name}')

    route = next((step for step in steps if step['step'] == 'route'), None)
    answer = next(step for step in steps if step['step'] == 'answer')
    route_wall_ms = float(route['wallMs']) if route else 0.0
    route_load_ms = float(route['loadMs']) if route else 0.0
    end_to_end_ms = route_wall_ms + float(answer['wallMs'])

    result = {
        'scenario': scenario,
        'rep': repetition,
        'warmup': warmup,
        'steps': steps,
        'routeWallMs': route_wall_ms,
        'routeLoadMs': route_load_ms,
        'answerWallMs': float(answer['wallMs']),
        'answerLoadMs': float(answer['loadMs']),
        'endToEndMs': end_to_end_ms,
    }
    print(
        'SWAP_SCENARIO|'
        f'scenario={scenario}|rep={repetition}|'
        f'routeWallMs={route_wall_ms:.1f}|'
        f'routeLoadMs={route_load_ms:.1f}|'
        f'answerWallMs={result["answerWallMs"]:.1f}|'
        f'answerLoadMs={result["answerLoadMs"]:.1f}|'
        f'endToEndMs={end_to_end_ms:.1f}',
        flush=True,
    )
    return result


def average(results: list[dict[str, Any]], key: str) -> float:
    return statistics.fmean(float(result[key]) for result in results)


def build_summary(
    all_results: list[dict[str, Any]], repetitions: int
) -> list[str]:
    grouped = {
        scenario['name']: [
            result
            for result in all_results
            if result['scenario'] == scenario['name']
        ]
        for scenario in SCENARIOS
    }
    lines: list[str] = []

    for scenario in SCENARIOS:
        name = str(scenario['name'])
        values = grouped[name]
        lines.append(
            'SWAP_SUMMARY|'
            f'scenario={name}|reps={len(values)}|'
            f'avgRouteWallMs={average(values, "routeWallMs"):.1f}|'
            f'avgRouteLoadMs={average(values, "routeLoadMs"):.1f}|'
            f'avgAnswerWallMs={average(values, "answerWallMs"):.1f}|'
            f'avgAnswerLoadMs={average(values, "answerLoadMs"):.1f}|'
            f'avgEndToEndMs={average(values, "endToEndMs"):.1f}'
        )

    hot_load = average(grouped['HOT_27B_GENERATION'], 'answerLoadMs')
    switched_load = average(grouped['SWITCH_8B_TO_27B'], 'answerLoadMs')
    hybrid_e2e = average(grouped['HYBRID_27B_8B_27B'], 'endToEndMs')
    quality_e2e = average(grouped['QUALITY_27B_27B'], 'endToEndMs')
    ratio = hybrid_e2e / quality_e2e if quality_e2e > 0 else 0.0

    lines.append(
        'SWAP_LOAD_COMPARISON|'
        f'hot27AnswerLoadMs={hot_load:.1f}|'
        f'from8To27AnswerLoadMs={switched_load:.1f}|'
        f'switchLoadPenaltyMs={switched_load - hot_load:.1f}'
    )
    lines.append(
        'SWAP_ARCHITECTURE_COMPARISON|'
        f'hybridAvgEndToEndMs={hybrid_e2e:.1f}|'
        f'quality27AvgEndToEndMs={quality_e2e:.1f}|'
        f'hybridPenaltyMs={hybrid_e2e - quality_e2e:.1f}|'
        f'hybridPenaltyRatio={ratio:.3f}|'
        f'repetitions={repetitions}'
    )
    return lines


def main() -> int:
    args = parse_args()
    if args.repetitions < 1 or args.repetitions > 10:
        raise ValueError('repetitions must be between 1 and 10.')

    output_dir = Path(args.output_dir).expanduser().resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    client = OllamaClient(args.base_url, args.timeout_seconds)

    try:
        client.get_json('/api/tags')
    except Exception as exc:
        print(f'ERROR: Ollama is not reachable: {exc}', file=sys.stderr)
        return 2

    print('ApologiaStudio Ollama model-switching benchmark')
    print(f'Baseline model:  {args.baseline_model}')
    print(f'Candidate model: {args.candidate_model}')
    print(f'Repetitions:     {args.repetitions}')
    print(f'Ollama:          {args.base_url}')
    print(f'Results:         {output_dir}')
    print()
    print(
        'NOTE: this benchmark unloads all currently running Ollama models '
        'between controlled scenarios.',
        flush=True,
    )

    all_results: list[dict[str, Any]] = []
    try:
        for definition in SCENARIOS:
            for repetition in range(1, args.repetitions + 1):
                print()
                print(
                    f'=== {definition["name"]} / repetition '
                    f'{repetition}/{args.repetitions} ===',
                    flush=True,
                )
                all_results.append(
                    run_scenario(
                        client,
                        definition=definition,
                        repetition=repetition,
                        baseline_model=args.baseline_model,
                        candidate_model=args.candidate_model,
                        keep_alive=args.keep_alive,
                    )
                )

        summary_lines = build_summary(all_results, args.repetitions)
        results_file = output_dir / 'model-switching-results.json'
        summary_file = output_dir / 'summary.txt'
        results_file.write_text(
            json.dumps(all_results, ensure_ascii=False, indent=2),
            encoding='utf-8',
        )
        summary_file.write_text(
            '\n'.join(summary_lines) + '\n', encoding='utf-8'
        )

        print()
        print('=== Summary ===')
        for line in summary_lines:
            print(line)
        print()
        print(f'Raw results: {results_file}')
        print(f'Summary:     {summary_file}')
        return 0
    finally:
        try:
            unload_all(client)
        except Exception as exc:
            print(f'WARNING: final cleanup failed: {exc}', file=sys.stderr)


if __name__ == '__main__':
    raise SystemExit(main())
