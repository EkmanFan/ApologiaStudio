#!/usr/bin/env python3
"""
E1 - Encoder calibration and single test evaluation.

Runs ONCE, on a frozen checkpoint. Thresholds are selected from the validation
split alone; the test split is scored exactly once, at frozen thresholds.

Protocol: docs/knowledge-ingestion/genre-form-classifier-experimental-protocol-2026-09-04.md

The device defaults to CPU. Passing --device cuda is deliberate, never implicit:
this script must never contend for a GPU that a training run is using.

Outputs, written side by side and never edited afterwards:
  thresholds-frozen.json   per label, per selection objective
  e1-report.json           every metric the protocol requires
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import torch
from sklearn.metrics import average_precision_score, roc_auc_score
from transformers import AutoModelForSequenceClassification, AutoTokenizer

# Selection objectives, fixed by the protocol before any measurement.
PRECISION_FLOORS = {"precision_floor": 0.80, "precision_floor_strict": 0.90}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def load_jsonl(path: Path) -> list[dict]:
    with path.open(encoding="utf-8") as handle:
        return [json.loads(line) for line in handle if line.strip()]


def score_split(rows, labels, tokenizer, model, text_field, max_length, device, batch_size):
    """One forward pass per split. Every threshold objective is then computed
    from these stored scores, so no objective costs an extra pass."""
    probabilities = np.zeros((len(rows), len(labels)), dtype=np.float64)

    with torch.no_grad():
        for start in range(0, len(rows), batch_size):
            batch = rows[start:start + batch_size]
            encoded = tokenizer(
                [row[text_field] for row in batch],
                truncation=True,
                max_length=max_length,
                padding=True,
                return_tensors="pt",
            ).to(device)

            logits = model(**encoded).logits
            probabilities[start:start + len(batch)] = torch.sigmoid(logits).cpu().numpy()

    truth = np.array(
        [[1 if label in row["labels"] else 0 for label in labels] for row in rows],
        dtype=np.int8,
    )

    return probabilities, truth


def counts(truth_column, predicted_column):
    true_positive = int(((truth_column == 1) & (predicted_column == 1)).sum())
    false_positive = int(((truth_column == 0) & (predicted_column == 1)).sum())
    false_negative = int(((truth_column == 1) & (predicted_column == 0)).sum())
    return true_positive, false_positive, false_negative


def prf(true_positive, false_positive, false_negative):
    precision = true_positive / (true_positive + false_positive) if true_positive + false_positive else 0.0
    recall = true_positive / (true_positive + false_negative) if true_positive + false_negative else 0.0
    f1 = 2 * precision * recall / (precision + recall) if precision + recall else 0.0
    return precision, recall, f1


def candidate_thresholds(scores_column):
    """Midpoints between observed scores, plus the boundaries. Selecting only
    from values the model actually produced keeps the choice on the data."""
    unique = np.unique(scores_column)
    midpoints = (unique[:-1] + unique[1:]) / 2 if unique.size > 1 else np.array([])
    return np.concatenate(([0.0], midpoints, unique, [1.0]))


def select_threshold(scores_column, truth_column, objective):
    """Returns (threshold, reachable). A precision floor that no threshold
    reaches is reported as unreachable, never silently relaxed."""
    best = None

    for threshold in candidate_thresholds(scores_column):
        predicted = (scores_column >= threshold).astype(np.int8)
        precision, recall, f1 = prf(*counts(truth_column, predicted))

        if objective == "f1":
            key = (f1, recall, -threshold)
        else:
            if precision < PRECISION_FLOORS[objective]:
                continue
            key = (recall, f1, -threshold)

        if best is None or key > best[0]:
            best = (key, float(threshold))

    if best is None:
        return 1.0, False

    return best[1], True


def evaluate(probabilities, truth, labels, thresholds):
    predicted = np.zeros_like(truth)
    for index, label in enumerate(labels):
        predicted[:, index] = (probabilities[:, index] >= thresholds[label]).astype(np.int8)

    per_label = []
    for index, label in enumerate(labels):
        true_positive, false_positive, false_negative = counts(truth[:, index], predicted[:, index])
        precision, recall, f1 = prf(true_positive, false_positive, false_negative)

        column_truth = truth[:, index]
        both_classes = 0 < column_truth.sum() < len(column_truth)

        per_label.append({
            "label": label,
            "support": int(column_truth.sum()),
            "threshold": thresholds[label],
            "tp": true_positive,
            "fp": false_positive,
            "fn": false_negative,
            "precision": precision,
            "recall": recall,
            "f1": f1,
            "predicted_positive": int(predicted[:, index].sum()),
            "roc_auc": float(roc_auc_score(column_truth, probabilities[:, index])) if both_classes else None,
            "pr_auc": float(average_precision_score(column_truth, probabilities[:, index])) if both_classes else None,
        })

    micro_tp = sum(x["tp"] for x in per_label)
    micro_fp = sum(x["fp"] for x in per_label)
    micro_fn = sum(x["fn"] for x in per_label)
    micro_precision, micro_recall, micro_f1 = prf(micro_tp, micro_fp, micro_fn)

    # How many labels does the mechanism assign per work, against the truth?
    # This is the encoder-side answer to the question EVAL-5 asked of the LLM.
    def distribution(matrix):
        per_work = matrix.sum(axis=1)
        return {str(k): int((per_work == k).sum()) for k in range(0, int(per_work.max()) + 1)}

    return {
        "micro_precision": micro_precision,
        "micro_recall": micro_recall,
        "micro_f1": micro_f1,
        "macro_f1": float(np.mean([x["f1"] for x in per_label])),
        "exact_set_accuracy": float((predicted == truth).all(axis=1).mean()),
        "labels_never_predicted": [x["label"] for x in per_label if x["predicted_positive"] == 0],
        "labels_per_work_predicted": distribution(predicted),
        "labels_per_work_truth": distribution(truth),
        "per_label": per_label,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset-dir", type=Path, required=True)
    parser.add_argument("--model-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--text-field", default="text_title_notes")
    parser.add_argument("--max-length", type=int, default=384)
    parser.add_argument("--batch-size", type=int, default=16)
    parser.add_argument("--device", default="cpu")
    parser.add_argument("--checkpoint-note", default="", help="epochs, seed and anything identifying the frozen run")
    args = parser.parse_args()

    labels = json.loads((args.dataset_dir / "labels.json").read_text(encoding="utf-8"))

    tokenizer = AutoTokenizer.from_pretrained(str(args.model_dir))
    model = AutoModelForSequenceClassification.from_pretrained(str(args.model_dir)).to(args.device).eval()

    validation = load_jsonl(args.dataset_dir / "validation.jsonl")
    test = load_jsonl(args.dataset_dir / "test.jsonl")

    validation_scores, validation_truth = score_split(
        validation, labels, tokenizer, model, args.text_field, args.max_length, args.device, args.batch_size)

    # Step 2 and 3: select from validation alone, then freeze.
    selection = {}
    for objective in ("f1", "precision_floor", "precision_floor_strict"):
        chosen = {}
        for index, label in enumerate(labels):
            threshold, reachable = select_threshold(
                validation_scores[:, index], validation_truth[:, index], objective)
            chosen[label] = {"threshold": threshold, "reachable": reachable}
        selection[objective] = chosen

    args.output_dir.mkdir(parents=True, exist_ok=True)
    frozen = {
        "selection_split": "validation",
        "test_used_for_threshold_selection": False,
        "model_dir": str(args.model_dir),
        "model_sha256": sha256(args.model_dir / "model.safetensors"),
        "checkpoint_note": args.checkpoint_note,
        "text_field": args.text_field,
        "max_length": args.max_length,
        "precision_floors": PRECISION_FLOORS,
        "dataset_sha256": {
            name: sha256(args.dataset_dir / f"{name}.jsonl")
            for name in ("train", "validation", "test")
        },
        "thresholds": selection,
    }
    (args.output_dir / "thresholds-frozen.json").write_text(
        json.dumps(frozen, ensure_ascii=False, indent=2), encoding="utf-8")

    # Step 4: the test split is scored once, at the frozen thresholds.
    test_scores, test_truth = score_split(
        test, labels, tokenizer, model, args.text_field, args.max_length, args.device, args.batch_size)

    report = {
        "protocol": "E1",
        "frozen_thresholds_file": "thresholds-frozen.json",
        "validation_size": len(validation),
        "test_size": len(test),
        "results": {},
    }

    for objective, chosen in selection.items():
        thresholds = {label: chosen[label]["threshold"] for label in labels}
        validation_metrics = evaluate(validation_scores, validation_truth, labels, thresholds)
        test_metrics = evaluate(test_scores, test_truth, labels, thresholds)

        report["results"][objective] = {
            "unreachable_labels": [l for l in labels if not chosen[l]["reachable"]],
            "validation": validation_metrics,
            "test": test_metrics,
            "macro_f1_generalization_gap": validation_metrics["macro_f1"] - test_metrics["macro_f1"],
        }

    # Reference point: the untuned threshold, for continuity with the baseline.
    reference = {label: 0.5 for label in labels}
    report["results"]["reference_0_5"] = {
        "unreachable_labels": [],
        "validation": evaluate(validation_scores, validation_truth, labels, reference),
        "test": evaluate(test_scores, test_truth, labels, reference),
    }

    (args.output_dir / "e1-report.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    print(json.dumps(report["results"]["precision_floor"]["test"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
