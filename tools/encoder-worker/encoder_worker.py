#!/usr/bin/env python3
"""Resident CPU encoder worker for the Apologia field-suggestion capability.

Deliberately dumb. It loads the two frozen Spike Encoder V2.1 artifacts, runs
one of them over a serialized input and returns the post-sigmoid scores in the
model's own label order.

It knows nothing about Genre/Form, thresholds, the cascade rule, the Apologia
taxonomy or LCGFT: those decisions belong to the application, as the V2.1
handoff requires. Replacing this worker with another hosting mechanism must not
change the .NET contract.
"""

from __future__ import annotations

import hashlib
import json
import os
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer

MAX_LENGTH = 512

# Logical identities. The application addresses models by these, never by path,
# so the artifacts can move without an application change.
PRIMARY_ID = "xlm-roberta-large"
FALLBACK_ID = "mdeberta-v3-base"


def _fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr, flush=True)
    raise SystemExit(2)


def _artifact_version(model_dir: Path) -> str:
    """Content identity of the weights, so a suggestion is attributable to the
    exact artifact that produced it."""
    digest = hashlib.sha256()
    with (model_dir / "model.safetensors").open("rb") as handle:
        for block in iter(lambda: handle.read(8 * 1024 * 1024), b""):
            digest.update(block)
    return "sha256:" + digest.hexdigest()


class LoadedModel:
    def __init__(self, model_id: str, model_dir: Path) -> None:
        if not model_dir.is_dir():
            _fail(f"model directory '{model_dir}' does not exist")

        print(f"loading {model_id} from {model_dir}", flush=True)
        started = time.perf_counter()

        self.model_id = model_id
        self.model_dir = model_dir
        self.tokenizer = AutoTokenizer.from_pretrained(
            model_dir, trust_remote_code=False
        )
        self.model = AutoModelForSequenceClassification.from_pretrained(
            model_dir, trust_remote_code=False
        )
        self.model.float()
        self.model.eval()
        self.model.to("cpu")

        config = self.model.config
        self.labels = [
            config.id2label[i] for i in range(len(config.id2label))
        ]
        self.version = _artifact_version(model_dir)
        self.lock = threading.Lock()

        elapsed = time.perf_counter() - started
        print(
            f"  {model_id} ready: {len(self.labels)} labels, "
            f"{self.version}, {elapsed:.1f}s",
            flush=True,
        )

    def infer(self, text: str) -> tuple[list[float], float]:
        started = time.perf_counter()

        # One model, one request at a time: the models are resident and shared,
        # and interactive batch size is 1 by decision.
        with self.lock:
            encoded = self.tokenizer(
                [text],
                truncation=True,
                max_length=MAX_LENGTH,
                padding=True,
                return_tensors="pt",
            )
            with torch.inference_mode():
                logits = self.model(**encoded).logits
                scores = torch.sigmoid(logits)[0].tolist()

        return scores, (time.perf_counter() - started) * 1000.0


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, fmt, *args):
        # Never log request bodies: they carry document content.
        pass

    def _respond(self, status: int, payload: dict) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:
        if self.path.rstrip("/") != "/health":
            self._respond(404, {"error": "not found"})
            return

        self._respond(
            200,
            {
                "status": "ready",
                "runtime": RUNTIME,
                "models": [
                    {
                        "modelId": model.model_id,
                        "modelVersion": model.version,
                        "outputCount": len(model.labels),
                        "labels": model.labels,
                    }
                    for model in MODELS.values()
                ],
            },
        )

    def do_POST(self) -> None:
        if self.path.rstrip("/") != "/infer":
            self._respond(404, {"error": "not found"})
            return

        try:
            length = int(self.headers.get("Content-Length") or 0)
            request = json.loads(self.rfile.read(length) or b"{}")
        except (ValueError, json.JSONDecodeError):
            self._respond(400, {"error": "malformed request"})
            return

        model_id = request.get("modelId")
        text = request.get("input")

        if model_id not in MODELS:
            self._respond(400, {"error": f"unknown model '{model_id}'"})
            return

        if not isinstance(text, str) or not text.strip():
            self._respond(400, {"error": "empty input"})
            return

        model = MODELS[model_id]

        try:
            scores, duration = model.infer(text)
        except Exception as exception:  # surfaced as a failure, never as zero
            self._respond(500, {"error": type(exception).__name__})
            return

        self._respond(
            200,
            {
                "modelId": model.model_id,
                "modelVersion": model.version,
                "scores": scores,
                "durationMilliseconds": duration,
            },
        )


def _runtime_identity() -> dict:
    import tokenizers
    import transformers

    return {
        "worker": "apologia-encoder-worker/1",
        "python": sys.version.split()[0],
        "torch": torch.__version__,
        "transformers": transformers.__version__,
        "tokenizers": tokenizers.__version__,
        "device": "cpu",
    }


def main() -> None:
    global MODELS, RUNTIME

    primary = os.environ.get("ENCODER_PRIMARY_MODEL_PATH")
    fallback = os.environ.get("ENCODER_FALLBACK_MODEL_PATH")

    if not primary or not fallback:
        _fail(
            "ENCODER_PRIMARY_MODEL_PATH and ENCODER_FALLBACK_MODEL_PATH "
            "must both be set"
        )

    threads = int(os.environ.get("ENCODER_CPU_THREADS", "8"))
    port = int(os.environ.get("ENCODER_WORKER_PORT", "5099"))

    torch.set_num_threads(threads)
    try:
        torch.set_num_interop_threads(1)
    except RuntimeError:
        pass

    # CPU only, by decision: the VRAM belongs to the LLM.
    if torch.cuda.is_available():
        _fail("a GPU is visible; the encoder worker must run on CPU only")

    RUNTIME = _runtime_identity()
    print(json.dumps(RUNTIME), flush=True)

    MODELS = {
        PRIMARY_ID: LoadedModel(PRIMARY_ID, Path(primary)),
        FALLBACK_ID: LoadedModel(FALLBACK_ID, Path(fallback)),
    }

    server = ThreadingHTTPServer(("0.0.0.0", port), Handler)
    print(f"listening on 0.0.0.0:{port}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
