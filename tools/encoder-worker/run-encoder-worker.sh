#!/usr/bin/env bash
# Starts the resident CPU encoder worker inside the qualified Spike image.
#
# Apologia never runs this: it only talks to the resulting HTTP endpoint. The
# hosting mechanism can be replaced without touching the .NET contract.
set -euo pipefail

IMAGE="${ENCODER_WORKER_IMAGE:-lcgft-ml:rocm7.2.4-mdeberta-v1}"
ARTIFACT_ROOT="${SPIKE_ARTIFACT_ROOT:-/mnt/SharedDrive/Spike Encoder/Spike Encoder V2}"
PRIMARY="${ENCODER_PRIMARY_MODEL_PATH:-/artifacts/models/xlm-roberta-large/gate-e-v1/best-model}"
FALLBACK="${ENCODER_FALLBACK_MODEL_PATH:-/artifacts/models/mdeberta-v3-base/gate-e-v1/best-model}"
PORT="${ENCODER_WORKER_PORT:-5099}"
THREADS="${ENCODER_CPU_THREADS:-8}"
NAME="${ENCODER_WORKER_NAME:-apologia-encoder-worker}"

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

command -v docker >/dev/null 2>&1 || { echo "ERROR: docker unavailable" >&2; exit 3; }
docker image inspect "$IMAGE" >/dev/null 2>&1 || { echo "ERROR: missing image $IMAGE" >&2; exit 4; }
[[ -d "$ARTIFACT_ROOT" ]] || { echo "ERROR: missing artifacts at $ARTIFACT_ROOT" >&2; exit 5; }

docker rm -f "$NAME" >/dev/null 2>&1 || true

# The worker source is piped in rather than bind-mounted: mounting the repo
# into the container trips SELinux labelling on the NVMe volume, and relabelling
# a source tree to run a test is a worse trade than sending it on stdin.
exec docker run --rm -i --name "$NAME" \
  -p "127.0.0.1:${PORT}:${PORT}" \
  -e CUDA_VISIBLE_DEVICES="" \
  -e HIP_VISIBLE_DEVICES="" \
  -e ROCR_VISIBLE_DEVICES="" \
  -e OMP_NUM_THREADS="$THREADS" \
  -e MKL_NUM_THREADS="$THREADS" \
  -e OPENBLAS_NUM_THREADS="$THREADS" \
  -e TOKENIZERS_PARALLELISM=false \
  -e PYTHONUNBUFFERED=1 \
  -e ENCODER_PRIMARY_MODEL_PATH="$PRIMARY" \
  -e ENCODER_FALLBACK_MODEL_PATH="$FALLBACK" \
  -e ENCODER_WORKER_PORT="$PORT" \
  -e ENCODER_CPU_THREADS="$THREADS" \
  -v "$ARTIFACT_ROOT:/artifacts:ro" \
  "$IMAGE" \
  python3 - < "$HERE/encoder_worker.py"
