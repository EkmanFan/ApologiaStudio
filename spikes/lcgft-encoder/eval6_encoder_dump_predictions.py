#!/usr/bin/env python3
"""
EVAL-6 - materialise the frozen encoder cascade's per-record predictions.

The Spike Encoder V2.1 published aggregate metrics but no per-record output, and
EVAL-6 needs per-record predictions to compute per-label confusion counts and to
compare the two candidates document by document.

This re-runs the FROZEN configuration at the FROZEN thresholds. It changes
nothing: no training, no threshold selection, no label change. Because the
models are deterministic, it must reproduce the published aggregates, and the
script checks that itself.

Frozen configuration (Spike Encoder V2.1):
    primary   FacebookAI/xlm-roberta-large   threshold 0.47
    fallback  microsoft/mdeberta-v3-base     threshold 0.43
    fallback fires only when the primary predicts no label
    max_length 512, CPU

Device defaults to CPU, which is the retained runtime.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer

MACHINE_LABELS = [
    "textbook", "handbook_manual", "dictionary", "encyclopedia",
    "academic_degree_work", "conference_proceedings", "anthology",
    "collected_works", "edited_volume", "biography", "autobiography",
    "personal_narrative", "essays", "commentary", "apologetic_writing",
    "catechism", "creed", "devotional_literature", "prayer", "sacred_work",
    "sermon", "scholarly_article", "correspondence", "diary",
]

PRIMARY_THRESHOLD = 0.47
FALLBACK_THRESHOLD = 0.43


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def load_records(path: Path) -> list[dict]:
    with path.open(encoding="utf-8") as handle:
        return [json.loads(line) for line in handle if line.strip()]


def label_order(model) -> list[str]:
    """Trust the checkpoint's own id2label when it carries real names, so the
    dump can never silently misalign columns with label names."""
    id2label = getattr(model.config, "id2label", None) or {}
    names = [id2label.get(i) or id2label.get(str(i)) for i in range(len(MACHINE_LABELS))]

    if all(isinstance(n, str) and n in MACHINE_LABELS for n in names):
        return names

    if model.config.num_labels != len(MACHINE_LABELS):
        raise SystemExit(
            f"checkpoint exposes {model.config.num_labels} labels, "
            f"expected {len(MACHINE_LABELS)}")

    return MACHINE_LABELS


def score(records, model_dir: Path, device: str, max_length: int, batch_size: int):
    tokenizer = AutoTokenizer.from_pretrained(str(model_dir))
    model = AutoModelForSequenceClassification.from_pretrained(str(model_dir)).to(device).eval()
    order = label_order(model)

    probabilities = np.zeros((len(records), len(order)), dtype=np.float64)

    with torch.no_grad():
        for start in range(0, len(records), batch_size):
            batch = records[start:start + batch_size]
            encoded = tokenizer(
                [r["content"]["serialized_input"] for r in batch],
                truncation=True,
                max_length=max_length,
                padding=True,
                return_tensors="pt",
            ).to(device)

            logits = model(**encoded).logits
            probabilities[start:start + len(batch)] = torch.sigmoid(logits).cpu().numpy()

    return probabilities, order


def predicted_set(row, order, threshold) -> list[str]:
    return [order[i] for i, p in enumerate(row) if p >= threshold]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--test-split", type=Path, required=True)
    parser.add_argument("--primary-model-dir", type=Path, required=True)
    parser.add_argument("--fallback-model-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--max-length", type=int, default=512)
    parser.add_argument("--batch-size", type=int, default=8)
    parser.add_argument("--device", default="cpu")
    args = parser.parse_args()

    records = load_records(args.test_split)

    primary, primary_order = score(
        records, args.primary_model_dir, args.device, args.max_length, args.batch_size)
    fallback, fallback_order = score(
        records, args.fallback_model_dir, args.device, args.max_length, args.batch_size)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    rescued = 0
    fallback_fired = 0

    with args.output.open("w", encoding="utf-8") as handle:
        for index, record in enumerate(records):
            primary_labels = predicted_set(primary[index], primary_order, PRIMARY_THRESHOLD)
            fallback_labels = predicted_set(fallback[index], fallback_order, FALLBACK_THRESHOLD)

            if primary_labels:
                final, stage = primary_labels, "primary"
            else:
                final, stage = fallback_labels, "fallback"
                fallback_fired += 1
                if fallback_labels:
                    rescued += 1

            handle.write(json.dumps({
                "record_id": record["record_id"],
                "work_key": record["work_key"],
                "predicted": sorted(final),
                "stage": stage,
                "primary_predicted": sorted(primary_labels),
                "fallback_predicted": sorted(fallback_labels),
                "scores_primary": {
                    primary_order[i]: round(float(primary[index][i]), 6)
                    for i in range(len(primary_order))
                },
            }, ensure_ascii=False) + "\n")

    manifest = {
        "experiment": "eval6-encoder-cascade-dump",
        "test_split": str(args.test_split),
        "test_split_sha256": sha256(args.test_split),
        "records": len(records),
        "primary": {"model_dir": str(args.primary_model_dir), "threshold": PRIMARY_THRESHOLD},
        "fallback": {"model_dir": str(args.fallback_model_dir), "threshold": FALLBACK_THRESHOLD},
        "max_length": args.max_length,
        "device": args.device,
        "fallback_fired": fallback_fired,
        "fallback_rescued_nonempty": rescued,
    }
    args.output.with_suffix(".manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

    print(json.dumps(manifest, ensure_ascii=False, indent=2))
    print(f"\nwrote {args.output}")
    print("Cross-check the aggregates against evaluation/cascade-v1/cascade-results.json "
          "before using this dump: a deterministic re-run must reproduce them.")


if __name__ == "__main__":
    main()
