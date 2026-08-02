#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
    cd "$(dirname "${BASH_SOURCE[0]}")/.."
    pwd
)"

cd "${ROOT_DIRECTORY}"

if [[ ! -f .env.apologia.local ]]; then
    echo "ERROR: .env.apologia.local was not found."
    exit 1
fi

docker compose \
    --env-file .env.apologia.local \
    -f compose.postgres.yaml \
    up -d postgres

echo "Waiting for PostgreSQL..."

for attempt in $(seq 1 40); do
    if docker compose \
        --env-file .env.apologia.local \
        -f compose.postgres.yaml \
        exec -T postgres \
        pg_isready \
        -U apologia \
        -d apologia_studio \
        >/dev/null 2>&1
    then
        echo "PostgreSQL is ready."
        exit 0
    fi

    sleep 1
done

echo "ERROR: PostgreSQL did not become ready."
exit 1
