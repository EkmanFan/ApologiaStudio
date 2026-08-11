#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"
cd "${ROOT_DIRECTORY}"

QUERY="${1:-}"
TOP_K="${2:-5}"
ENV_FILE=".env.apologia.local"
PROJECT="tools/ApologiaStudio.KnowledgeImporter/ApologiaStudio.KnowledgeImporter.csproj"
KNOWLEDGE_USER="apologia_knowledge"
KNOWLEDGE_DATABASE="apologia_knowledge"

fail() {
  printf 'ERROR: %s\n' "$1" >&2
  exit 1
}

[[ -n "${QUERY}" ]] \
  || fail "Usage: bash scripts/search-de-decretis-lexical.sh \"query\" [top-k]"
[[ "${TOP_K}" =~ ^[0-9]+$ ]] \
  || fail "top-k must be an integer."

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
  search-lexical \
  --query "${QUERY}" \
  --top-k "${TOP_K}"
