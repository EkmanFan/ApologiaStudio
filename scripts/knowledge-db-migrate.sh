#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"

cd "${ROOT_DIRECTORY}"

ENV_FILE=".env.apologia.local"
PROJECT="src/ApologiaStudio.Infrastructure/ApologiaStudio.Infrastructure.csproj"
CONTEXT="KnowledgeDbContext"

fail() {
  echo "ERROR: $1" >&2
  exit 1
}

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

dotnet tool restore

migrate_database() {
  local database="$1"

  export APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION="Host=127.0.0.1;Port=54330;Database=${database};Username=apologia_knowledge;Password=${APOLOGIA_KNOWLEDGE_DB_PASSWORD};Pooling=false"

  dotnet tool run dotnet-ef database update \
    --project "${PROJECT}" \
    --context "${CONTEXT}" \
    --no-build
}

migrate_database "apologia_knowledge"
migrate_database "apologia_knowledge_test"
