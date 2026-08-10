#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"

cd "${ROOT_DIRECTORY}"

ENV_FILE=".env.apologia.local"

fail() {
  echo "ERROR: $1" >&2
  exit 1
}

[[ -f "${ENV_FILE}" ]]   || fail "${ENV_FILE} was not found."

knowledge_password="$(
  sed -n 's/^APOLOGIA_KNOWLEDGE_DB_PASSWORD=//p' "${ENV_FILE}"     | tail -n 1
)"

[[ -n "${knowledge_password}" ]]   || fail "APOLOGIA_KNOWLEDGE_DB_PASSWORD is not configured in ${ENV_FILE}."

export APOLOGIASTUDIO_KNOWLEDGE_TEST_DB_CONNECTION="Host=127.0.0.1;Port=54330;Database=apologia_knowledge_test;Username=apologia_knowledge;Password=${knowledge_password};Pooling=false"

dotnet test   tests/ApologiaStudio.IntegrationTests/ApologiaStudio.IntegrationTests.csproj   --filter "FullyQualifiedName~PostgreSqlKnowledgeStoreInfrastructureTests"   "$@"
