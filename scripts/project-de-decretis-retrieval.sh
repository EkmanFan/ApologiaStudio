#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"

cd "${ROOT_DIRECTORY}"

SOURCE_PDF="${1:-}"
ENV_FILE=".env.apologia.local"
PROJECT="tools/ApologiaStudio.KnowledgeImporter/ApologiaStudio.KnowledgeImporter.csproj"
EMBEDDING_MODEL="qwen3-embedding:4b"

fail() {
  printf 'ERROR: %s\n' "$1" >&2
  exit 1
}

[[ -n "${SOURCE_PDF}" ]] \
  || fail "Usage: bash scripts/project-de-decretis-retrieval.sh /absolute/path/to/npnf204.pdf"

[[ -f "${SOURCE_PDF}" ]] \
  || fail "Source PDF was not found: ${SOURCE_PDF}"

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
  echo "Pulling required embedding model: ${EMBEDDING_MODEL}"
  ollama pull "${EMBEDDING_MODEL}"
fi

bash scripts/knowledge-db-up.sh

export APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION="Host=127.0.0.1;Port=54330;Database=apologia_knowledge;Username=apologia_knowledge;Password=${APOLOGIA_KNOWLEDGE_DB_PASSWORD};Pooling=false"

dotnet run \
  --project "${PROJECT}" \
  -- \
  project-retrieval \
  --source "${SOURCE_PDF}"
