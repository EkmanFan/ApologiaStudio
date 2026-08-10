#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"

cd "${ROOT_DIRECTORY}"

SOURCE_PDF="${1:-}"
ARTIFACT_ROOT="${2:-${XDG_DATA_HOME:-$HOME/.local/share}/ApologiaStudio/knowledge/artifacts}"
ENV_FILE=".env.apologia.local"
PROJECT="tools/ApologiaStudio.KnowledgeImporter/ApologiaStudio.KnowledgeImporter.csproj"

fail() {
  printf 'ERROR: %s\n' "$1" >&2
  exit 1
}

[[ -n "${SOURCE_PDF}" ]] \
  || fail "Usage: bash scripts/import-de-decretis-npnf204.sh /absolute/path/to/npnf204.pdf [artifact-root]"

[[ -f "${SOURCE_PDF}" ]] \
  || fail "Source PDF was not found: ${SOURCE_PDF}"

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

export APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION="Host=127.0.0.1;Port=54330;Database=apologia_knowledge;Username=apologia_knowledge;Password=${APOLOGIA_KNOWLEDGE_DB_PASSWORD};Pooling=false"

bash scripts/knowledge-db-up.sh

dotnet run \
  --project "${PROJECT}" \
  -- \
  import \
  --source "${SOURCE_PDF}" \
  --artifact-root "${ARTIFACT_ROOT}"
