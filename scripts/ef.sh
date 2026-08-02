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

set -a
source .env.apologia.local
set +a

export APOLOGIASTUDIO_DB_CONNECTION="Host=localhost;Port=54329;Database=apologia_studio;Username=apologia;Password=${APOLOGIA_DB_PASSWORD};Include Error Detail=false"

dotnet tool restore

dotnet tool run dotnet-ef \
    "$@" \
    --project src/ApologiaStudio.Infrastructure \
    --startup-project src/ApologiaStudio.Web
