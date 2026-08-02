#!/usr/bin/env bash

set -Eeuo pipefail

repository_root="$(
    cd "$(dirname "${BASH_SOURCE[0]}")/.."
    pwd
)"

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 /absolute/path/to/source-archives"
    exit 2
fi

if [[ ! -d "$1" ]]; then
    echo "ERROR: Source archive directory does not exist: $1"
    exit 2
fi

artifact_directory="$(realpath "$1")"

if [[ -z "${APOLOGIASTUDIO_DB_CONNECTION:-}" ]]; then
    if [[ ! -f "${repository_root}/.env.apologia.local" ]]; then
        echo "ERROR: APOLOGIASTUDIO_DB_CONNECTION is not defined and .env.apologia.local was not found."
        exit 2
    fi

    set -a
    source "${repository_root}/.env.apologia.local"
    set +a
    export APOLOGIASTUDIO_DB_CONNECTION="Host=localhost;Port=54329;Database=apologia_studio;Username=apologia;Password=${APOLOGIA_DB_PASSWORD};Include Error Detail=false"
fi

cd "${repository_root}"

dotnet run \
    --project tools/ApologiaStudio.BibleCorpusImporter \
    -- \
    --manifest corpora/manifests/fraLSG-2026-08-02.json \
    --artifacts "${artifact_directory}" \
    --confirm-manifest fraLSG-2026-08-02

dotnet run \
    --project tools/ApologiaStudio.BibleCorpusImporter \
    -- \
    --manifest corpora/manifests/eng-web-2026-08-02.json \
    --artifacts "${artifact_directory}" \
    --confirm-manifest eng-web-2026-08-02
