#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"

cd "${ROOT_DIRECTORY}"

command="${1:-consume-once}"
env_file=".env.apologia.local"

fail() {
  echo "ERROR: $1" >&2
  exit 1
}

[[ "${command}" == "consume-once" || "${command}" == "run" ]] \
  || fail "Expected consume-once or run."

if [[ -z "${APOLOGIA_KNOWLEDGE_DB_PASSWORD:-}" ]]; then
  [[ -f "${env_file}" ]] \
    || fail "${env_file} was not found."

  APOLOGIA_KNOWLEDGE_DB_PASSWORD="$(
    sed -n 's/^APOLOGIA_KNOWLEDGE_DB_PASSWORD=//p' "${env_file}" \
      | tail -n 1
  )"
fi

[[ -n "${APOLOGIA_KNOWLEDGE_DB_PASSWORD}" ]] \
  || fail "APOLOGIA_KNOWLEDGE_DB_PASSWORD is not configured."

export APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION="${APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION:-Host=127.0.0.1;Port=54330;Database=apologia_knowledge;Username=apologia_knowledge;Password=${APOLOGIA_KNOWLEDGE_DB_PASSWORD};Pooling=false}"
export APOLOGIASTUDIO_DOCUMENT_MANAGER_URL="${APOLOGIASTUDIO_DOCUMENT_MANAGER_URL:-http://127.0.0.1:5080/}"
export APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_ID="${APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_ID:-apologia-studio}"

if [[ -z "${APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_KEY:-}" ]]; then
  if [[ "${APOLOGIASTUDIO_DOCUMENT_MANAGER_URL}" == "http://127.0.0.1:5080/" ||
        "${APOLOGIASTUDIO_DOCUMENT_MANAGER_URL}" == "http://localhost:5080/" ]]
  then
    export APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_KEY="${DPE_MANAGER_CONSUMER_API_KEY:-dpengine-consumer-local-development-key-2026}"
  else
    fail "APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_KEY is required outside the default local development endpoint."
  fi
fi

./scripts/knowledge-db-up.sh

dotnet run \
  --project tools/ApologiaStudio.DocumentManagerConsumer/ApologiaStudio.DocumentManagerConsumer.csproj \
  -- "${command}"
