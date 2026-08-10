#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/.."
  pwd
)"

cd "${ROOT_DIRECTORY}"

docker compose   --env-file .env.apologia.local   -f compose.knowledge.yaml   down
