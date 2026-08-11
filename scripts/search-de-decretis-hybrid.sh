#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"
cd "${ROOT_DIRECTORY}"

QUERY="${1:-}"
TOP_K="${2:-5}"
MODE="${3:-exact}"
CANDIDATE_K="${4:-20}"
ENV_FILE=".env.apologia.local"
PROJECT="tools/ApologiaStudio.KnowledgeImporter/ApologiaStudio.KnowledgeImporter.csproj"
KNOWLEDGE_USER="apologia_knowledge"
KNOWLEDGE_DATABASE="apologia_knowledge"
EMBEDDING_MODEL="qwen3-embedding:4b"

fail() {
  printf 'ERROR: %s\n' "$1" >&2
  exit 1
}

[[ -n "${QUERY}" ]] \
  || fail "Usage: bash scripts/search-de-decretis-hybrid.sh \"query\" [top-k] [exact|hnsw] [candidate-k]"
[[ "${TOP_K}" =~ ^[0-9]+$ ]] \
  || fail "top-k must be an integer."
[[ "${CANDIDATE_K}" =~ ^[0-9]+$ ]] \
  || fail "candidate-k must be an integer."
case "${MODE}" in
  exact|hnsw) ;;
  *) fail "Mode must be exact or hnsw." ;;
esac

bash scripts/knowledge-db-up.sh

if [[ -z "${APOLOGIA_KNOWLEDGE_DB_PASSWORD:-}" ]]; then
  [[ -f "${ENV_FILE}" ]] \
    || fail "${ENV_FILE} was not found."

  APOLOGIA_KNOWLEDGE_DB_PASSWORD="$(
    sed -n 's/^APOLOGIA_KNOWLEDGE_DB_PASSWORD=//p' "${ENV_FILE}" \
      | tail -n 1
  )"
fi

[[ -n "${APOLOGIA_KNOWLEDGE_DB_PASSWORD:-}" ]] \
  || fail "APOLOGIA_KNOWLEDGE_DB_PASSWORD is not configured."

export APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION="Host=127.0.0.1;Port=54330;Database=${KNOWLEDGE_DATABASE};Username=${KNOWLEDGE_USER};Password=${APOLOGIA_KNOWLEDGE_DB_PASSWORD};Pooling=false"

command -v ollama >/dev/null 2>&1 \
  || fail "Ollama is not installed or is not available on PATH."
ollama list \
  | awk 'NR > 1 { print $1 }' \
  | grep -Fxq "${EMBEDDING_MODEL}" \
  || fail "Required embedding model ${EMBEDDING_MODEL} is unavailable."

dotnet run \
  --project "${PROJECT}" \
  -- \
  search-hybrid \
  --query "${QUERY}" \
  --top-k "${TOP_K}" \
  --mode "${MODE}" \
  --candidate-k "${CANDIDATE_K}"
