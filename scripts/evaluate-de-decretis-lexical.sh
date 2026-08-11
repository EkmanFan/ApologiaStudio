#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"
cd "${ROOT_DIRECTORY}"

DATASET="${1:-evaluations/knowledge/de-decretis-lexical-retrieval-v1.json}"
RECALL_K="${2:-5}"
CANDIDATE_K="${3:-20}"
ENV_FILE=".env.apologia.local"
PROJECT="tools/ApologiaStudio.KnowledgeImporter/ApologiaStudio.KnowledgeImporter.csproj"
KNOWLEDGE_USER="apologia_knowledge"
KNOWLEDGE_DATABASE="apologia_knowledge"

fail() {
  printf 'ERROR: %s\n' "$1" >&2
  exit 1
}

[[ "${RECALL_K}" =~ ^[0-9]+$ ]] \
  || fail "recall-k must be an integer."
[[ "${CANDIDATE_K}" =~ ^[0-9]+$ ]] \
  || fail "candidate-k must be an integer."
[[ -f "${DATASET}" ]] \
  || fail "Dataset not found: ${DATASET}"

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

dotnet run \
  --project "${PROJECT}" \
  -- \
  evaluate-lexical \
  --dataset "${DATASET}" \
  --recall-k "${RECALL_K}" \
  --candidate-k "${CANDIDATE_K}"
