#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"

cd "${ROOT_DIRECTORY}"

ENV_FILE=".env.apologia.local"
COMPOSE_FILE="compose.knowledge.yaml"
SERVICE="knowledge-postgres"
POSTGRES_USER="apologia_knowledge"
PRIMARY_DATABASE="apologia_knowledge"
TEST_DATABASE="apologia_knowledge_test"

fail() {
  echo "ERROR: $1" >&2
  exit 1
}

[[ -f "${ENV_FILE}" ]]   || fail "${ENV_FILE} was not found."

grep -q '^APOLOGIA_KNOWLEDGE_DB_PASSWORD=.' "${ENV_FILE}"   || fail "APOLOGIA_KNOWLEDGE_DB_PASSWORD is not configured in ${ENV_FILE}."

compose() {
  docker compose     --env-file "${ENV_FILE}"     -f "${COMPOSE_FILE}"     "$@"
}

compose config --quiet
compose up -d "${SERVICE}"

echo "Waiting for Knowledge PostgreSQL..."

ready=0
for attempt in $(seq 1 40); do
  if compose exec -T "${SERVICE}"     pg_isready       -U "${POSTGRES_USER}"       -d "${PRIMARY_DATABASE}"       >/dev/null 2>&1
  then
    ready=1
    break
  fi

  sleep 1
done

[[ "${ready}" -eq 1 ]]   || fail "Knowledge PostgreSQL did not become ready."

if ! compose exec -T "${SERVICE}"   psql     -U "${POSTGRES_USER}"     -d postgres     -tAc "SELECT 1 FROM pg_database WHERE datname = '${TEST_DATABASE}'"     | grep -qx '1'
then
  compose exec -T "${SERVICE}"     createdb       -U "${POSTGRES_USER}"       -O "${POSTGRES_USER}"       "${TEST_DATABASE}"
fi

for database in "${PRIMARY_DATABASE}" "${TEST_DATABASE}"; do
  compose exec -T "${SERVICE}"     psql       -v ON_ERROR_STOP=1       -U "${POSTGRES_USER}"       -d "${database}"       -c "CREATE EXTENSION IF NOT EXISTS vector;"       >/dev/null
done

vector_version="$(
  compose exec -T "${SERVICE}"     psql       -U "${POSTGRES_USER}"       -d "${PRIMARY_DATABASE}"       -tAc "SELECT extversion FROM pg_extension WHERE extname = 'vector'"
)"

[[ -n "${vector_version}" ]]   || fail "The vector extension is not enabled."

echo "Knowledge PostgreSQL is ready (pgvector ${vector_version})."
echo "Primary database: ${PRIMARY_DATABASE}"
echo "Test database: ${TEST_DATABASE}"
