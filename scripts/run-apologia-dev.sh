#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"

cd "${ROOT_DIRECTORY}"

env_file=".env.apologia.local"

fail() {
  echo "ERROR: $1" >&2
  exit 1
}

read_local_value() {
  local name="$1"

  sed -n "s/^${name}=//p" "${env_file}" |
    tail -n 1
}

if [[ -z "${APOLOGIA_DB_PASSWORD:-}" ||
      -z "${APOLOGIA_KNOWLEDGE_DB_PASSWORD:-}" ]]
then
  [[ -f "${env_file}" ]] || fail "${env_file} was not found."

  APOLOGIA_DB_PASSWORD="${APOLOGIA_DB_PASSWORD:-$(read_local_value APOLOGIA_DB_PASSWORD)}"
  APOLOGIA_KNOWLEDGE_DB_PASSWORD="${APOLOGIA_KNOWLEDGE_DB_PASSWORD:-$(read_local_value APOLOGIA_KNOWLEDGE_DB_PASSWORD)}"
fi

[[ -n "${APOLOGIA_DB_PASSWORD}" ]] ||
  fail "APOLOGIA_DB_PASSWORD is not configured."
[[ -n "${APOLOGIA_KNOWLEDGE_DB_PASSWORD}" ]] ||
  fail "APOLOGIA_KNOWLEDGE_DB_PASSWORD is not configured."

export ConnectionStrings__ApologiaStudio="${ConnectionStrings__ApologiaStudio:-Host=127.0.0.1;Port=54329;Database=apologia_studio;Username=apologia;Password=${APOLOGIA_DB_PASSWORD}}"
export APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION="${APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION:-Host=127.0.0.1;Port=54330;Database=apologia_knowledge;Username=apologia_knowledge;Password=${APOLOGIA_KNOWLEDGE_DB_PASSWORD};Pooling=false}"

./scripts/db-up.sh
./scripts/knowledge-db-up.sh

dotnet ef database update \
  --project src/ApologiaStudio.Infrastructure/ApologiaStudio.Infrastructure.csproj \
  --context KnowledgeDbContext

dotnet run \
  --project src/ApologiaStudio.Web/ApologiaStudio.Web.csproj \
  --launch-profile http
