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
export DocumentManagerAdministration__Enabled="${DocumentManagerAdministration__Enabled:-true}"
export DocumentManagerConsumer__Enabled="${DocumentManagerConsumer__Enabled:-true}"
export DocumentManagerConsumer__ManagerUrl="${APOLOGIASTUDIO_DOCUMENT_MANAGER_URL:-http://127.0.0.1:5080/}"
export DocumentManagerConsumer__ConsumerId="${APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_ID:-apologia-studio}"
export DocumentManagerConsumer__ConsumerKey="${APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_KEY:-dpengine-consumer-local-development-key-2026}"
export DocumentManagerConsumer__NotificationSecret="${DPE_MANAGER_NOTIFICATION_SHARED_SECRET:-dpengine-notification-local-development-key-2026}"
export DocumentManagerConsumer__DeliveryReplayApiKey="${DPE_MANAGER_DELIVERY_REPLAY_API_KEY:-dpengine-delivery-replay-local-development-key-2026}"
export DocumentManagerConsumer__ReconciliationSeconds="${DocumentManagerConsumer__ReconciliationSeconds:-300}"
export DocumentManagerConsumer__RetrySeconds="${DocumentManagerConsumer__RetrySeconds:-10}"
export IdentityBootstrap__Enabled="${IdentityBootstrap__Enabled:-true}"
export IdentityBootstrap__Email="${APOLOGIA_BOOTSTRAP_ADMIN_EMAIL:-admin@apologia.local}"
export IdentityBootstrap__Password="${APOLOGIA_BOOTSTRAP_ADMIN_PASSWORD:-Apologia-Local-Admin-2026!}"
export IdentityBootstrap__DisplayName="${APOLOGIA_BOOTSTRAP_ADMIN_NAME:-Apologia Administrator}"

printf 'Local administrator: %s\n' "${IdentityBootstrap__Email}"
printf 'Local password: %s\n' "${IdentityBootstrap__Password}"
printf 'These development defaults are never used unless the identity store is empty.\n\n'

./scripts/db-up.sh
./scripts/knowledge-db-up.sh

export APOLOGIASTUDIO_DB_CONNECTION="${ConnectionStrings__ApologiaStudio}"

dotnet ef database update \
  --project src/ApologiaStudio.Infrastructure/ApologiaStudio.Infrastructure.csproj \
  --context ApologiaStudioDbContext

dotnet ef database update \
  --project src/ApologiaStudio.Infrastructure/ApologiaStudio.Infrastructure.csproj \
  --context KnowledgeDbContext

dotnet run \
  --project src/ApologiaStudio.Web/ApologiaStudio.Web.csproj \
  --launch-profile http
