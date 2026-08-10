#!/usr/bin/env bash

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BASELINE_MODEL="${BASELINE_MODEL:-qwen3:8b}"
CANDIDATE_MODEL="${CANDIDATE_MODEL:-qwen3.6:27b}"
OLLAMA_URL="${OLLAMA_BASE_URL:-http://127.0.0.1:11434}"
REPETITIONS="${SWAP_BENCHMARK_REPETITIONS:-2}"
OUTPUT_DIR="${SWAP_BENCHMARK_OUTPUT_DIR:-$HOME/Downloads/apologia-model-switching-$(date +%Y%m%d-%H%M%S)}"

if ! command -v python3 >/dev/null 2>&1; then
    echo "ERROR: python3 is required for streaming-safe JSON parsing and precise timing."
    exit 1
fi

if ! command -v ollama >/dev/null 2>&1; then
    echo "ERROR: ollama CLI was not found."
    exit 1
fi

python3 "$SCRIPT_DIR/benchmark-ollama-model-switching.py" \
    --base-url "$OLLAMA_URL" \
    --baseline-model "$BASELINE_MODEL" \
    --candidate-model "$CANDIDATE_MODEL" \
    --repetitions "$REPETITIONS" \
    --output-dir "$OUTPUT_DIR"
