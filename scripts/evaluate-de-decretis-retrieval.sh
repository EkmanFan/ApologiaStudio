#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"
cd "${ROOT_DIRECTORY}"

MODE="${1:-exact}"
DATASET="${2:-evaluations/knowledge/de-decretis-retrieval-v1.json}"
RECALL_K="${3:-5}"
CANDIDATE_K="${4:-20}"
ENV_FILE=".env.apologia.local"
PROJECT="tools/ApologiaStudio.KnowledgeImporter/ApologiaStudio.KnowledgeImporter.csproj"
KNOWLEDGE_USER="apologia_knowledge"
KNOWLEDGE_DATABASE="apologia_knowledge"

fail() {
  printf 'ERROR: %s\n' "$1" >&2
  exit 1
}

case "${MODE}" in
  exact|hnsw) ;;
  *) fail "Mode must be exact or hnsw." ;;
esac

[[ -f "${DATASET}" ]] \
  || fail "Evaluation dataset not found: ${DATASET}"

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

ollama list >/dev/null \
  || fail "Ollama is not reachable."

dotnet run \
  --project "${PROJECT}" \
  -- \
  evaluate-retrieval \
  --dataset "${DATASET}" \
  --mode "${MODE}" \
  --recall-k "${RECALL_K}" \
  --candidate-k "${CANDIDATE_K}"
