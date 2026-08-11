#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"

cd "${ROOT_DIRECTORY}"

MODE="${1:-exact}"
QUERY="${2:-}"
TOP_K="${3:-5}"
ENV_FILE=".env.apologia.local"
PROJECT="tools/ApologiaStudio.KnowledgeImporter/ApologiaStudio.KnowledgeImporter.csproj"
EMBEDDING_MODEL="qwen3-embedding:4b"

fail() {
  printf 'ERROR: %s\n' "$1" >&2
  exit 1
}

[[ "${MODE}" == "exact" || "${MODE}" == "hnsw" ]] \
  || fail "Mode must be exact or hnsw."
[[ -n "${QUERY}" ]] \
  || fail "Usage: bash scripts/search-de-decretis-retrieval.sh exact|hnsw \"query\" [top-k]"
[[ "${TOP_K}" =~ ^[0-9]+$ ]] \
  || fail "top-k must be an integer."

command -v ollama >/dev/null 2>&1 \
  || fail "Ollama is not installed or is not available on PATH."

if [[ -z "${APOLOGIA_KNOWLEDGE_DB_PASSWORD:-}" ]]; then
  [[ -f "${ENV_FILE}" ]] \
    || fail "${ENV_FILE} was not found."
  APOLOGIA_KNOWLEDGE_DB_PASSWORD="$(
    sed -n 's/^APOLOGIA_KNOWLEDGE_DB_PASSWORD=//p' "${ENV_FILE}" \
      | tail -n 1
  )"
  [[ -n "${APOLOGIA_KNOWLEDGE_DB_PASSWORD}" ]] \
    || fail "APOLOGIA_KNOWLEDGE_DB_PASSWORD is not configured."
fi

ollama_models="$(ollama list)" \
  || fail "Ollama is not reachable. Ensure the local Ollama service is running."
if ! printf '%s\n' "${ollama_models}" \
  | awk 'NR > 1 { print $1 }' \
  | grep -Fxq "${EMBEDDING_MODEL}"
then
  fail "Required embedding model is not installed: ${EMBEDDING_MODEL}"
fi

bash scripts/knowledge-db-up.sh

export APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION="Host=127.0.0.1;Port=54330;Database=apologia_knowledge;Username=apologia_knowledge;Password=${APOLOGIA_KNOWLEDGE_DB_PASSWORD};Pooling=false"

dotnet run \
  --project "${PROJECT}" \
  -- \
  search-retrieval \
  --query "${QUERY}" \
  --top-k "${TOP_K}" \
  --mode "${MODE}"
