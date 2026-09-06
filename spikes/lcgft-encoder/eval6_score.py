#!/usr/bin/env python3
"""
EVAL-6 offline scorer.

Reads the frozen ground truth, the encoder cascade's per-record predictions and
the LLM-PER-LABEL decision log, and scores BOTH candidates with exactly the same
code. A metric difference between the two is therefore a difference in
behaviour, never in how it was computed.

No model is loaded and no inference is run: every metric is recomputable from
the artifacts alone, as often as needed.

Discipline carried over from EVAL-1..5: an unresolved LLM decision (invalid JSON
or transport failure) is a contract failure. It is never silently read as
"false". It is excluded and reported on its own, and any record holding one is
excluded from set-level metrics, which are then reported over a stated
denominator.
"""

from __future__ import annotations

import argparse
import json
import re
import statistics
from collections import Counter, defaultdict
from pathlib import Path

MACHINE_LABELS = [
    "textbook", "handbook_manual", "dictionary", "encyclopedia",
    "academic_degree_work", "conference_proceedings", "anthology",
    "collected_works", "edited_volume", "biography", "autobiography",
    "personal_narrative", "essays", "commentary", "apologetic_writing",
    "catechism", "creed", "devotional_literature", "prayer", "sacred_work",
    "sermon", "scholarly_article", "correspondence", "diary",
]

# Lexical ABOUT markers. These flag SUSPECTS for human adjudication. They are
# never a verdict, and the slice they produce is reported as indicative only.
ABOUT_PATTERN = re.compile(
    r"\b(a |the )?(study|studies|history|commentary|commentaries|introduction|"
    r"analysis|critique|interpretation|reception|role|views?|survey|companion|"
    r"reflections on|talks on|reading|rereading|understanding)\b[^.]{0,40}\b(of|on|to|upon)\b"
    r"|\b(étude|histoire|commentaire|introduction|analyse|critique|réception|lecture)s?\b"
    r"[^.]{0,30}\b(de|des|du|sur|d')\b",
    re.IGNORECASE)


def load_jsonl(path: Path) -> list[dict]:
    with path.open(encoding="utf-8") as handle:
        return [json.loads(line) for line in handle if line.strip()]


def prf(tp: int, fp: int, fn: int) -> tuple[float, float, float]:
    precision = tp / (tp + fp) if tp + fp else 0.0
    recall = tp / (tp + fn) if tp + fn else 0.0
    f1 = 2 * precision * recall / (precision + recall) if precision + recall else 0.0
    return precision, recall, f1


def score_candidate(name, truth, predicted, scorable_records):
    """truth / predicted: record_id -> set(labels). scorable_records: the ids
    whose predicted set is complete enough for set-level metrics."""
    per_label = []

    for label in MACHINE_LABELS:
        tp = fp = fn = tn = 0
        for record_id, gold in truth.items():
            if record_id not in predicted:
                continue
            has_gold = label in gold
            has_pred = label in predicted[record_id]
            if has_gold and has_pred:
                tp += 1
            elif has_pred:
                fp += 1
            elif has_gold:
                fn += 1
            else:
                tn += 1

        precision, recall, f1 = prf(tp, fp, fn)
        per_label.append({
            "label": label,
            "support": tp + fn,
            "tp": tp, "fp": fp, "fn": fn, "tn": tn,
            "precision": precision, "recall": recall, "f1": f1,
            "predicted_positive": tp + fp,
        })

    micro_tp = sum(x["tp"] for x in per_label)
    micro_fp = sum(x["fp"] for x in per_label)
    micro_fn = sum(x["fn"] for x in per_label)
    micro_p, micro_r, micro_f1 = prf(micro_tp, micro_fp, micro_fn)

    scored = [r for r in scorable_records if r in truth and r in predicted]
    gold_positive = [r for r in scored if truth[r]]
    gold_oot = [r for r in scored if not truth[r]]

    exact = sum(1 for r in scored if truth[r] == predicted[r])
    positive_any_recall = (
        sum(1 for r in gold_positive if predicted[r]) / len(gold_positive)
        if gold_positive else 0.0)
    oot_accuracy = (
        sum(1 for r in gold_oot if not predicted[r]) / len(gold_oot)
        if gold_oot else 0.0)

    under = sum(1 for r in scored if len(predicted[r]) < len(truth[r]))
    over = sum(1 for r in scored if len(predicted[r]) > len(truth[r]))

    by_cardinality = defaultdict(lambda: [0, 0])
    for r in scored:
        bucket = by_cardinality[len(truth[r])]
        bucket[1] += 1
        if truth[r] == predicted[r]:
            bucket[0] += 1

    return {
        "candidate": name,
        "records_with_predictions": len(predicted),
        "records_scored_at_set_level": len(scored),
        "micro_precision": micro_p, "micro_recall": micro_r, "micro_f1": micro_f1,
        "macro_precision": statistics.fmean(x["precision"] for x in per_label),
        "macro_recall": statistics.fmean(x["recall"] for x in per_label),
        "macro_f1": statistics.fmean(x["f1"] for x in per_label),
        "exact_match": exact / len(scored) if scored else 0.0,
        "positive_any_label_recall": positive_any_recall,
        "oot_accuracy": oot_accuracy,
        "oot_records": len(gold_oot),
        "oot_leaked_to_false_positive": sum(1 for r in gold_oot if predicted[r]),
        "positive_records": len(gold_positive),
        "positive_left_without_label": sum(1 for r in gold_positive if not predicted[r]),
        "mean_expected_labels": statistics.fmean(len(truth[r]) for r in scored) if scored else 0.0,
        "mean_predicted_labels": statistics.fmean(len(predicted[r]) for r in scored) if scored else 0.0,
        "under_classified": under,
        "over_classified": over,
        "exact_by_true_cardinality": {
            str(k): {"exact": v[0], "records": v[1]} for k, v in sorted(by_cardinality.items())
        },
        "most_over_predicted": sorted(
            ({"label": x["label"], "fp": x["fp"]} for x in per_label),
            key=lambda x: -x["fp"])[:5],
        "most_under_predicted": sorted(
            ({"label": x["label"], "fn": x["fn"]} for x in per_label),
            key=lambda x: -x["fn"])[:5],
        "per_label": per_label,
    }


def llm_sets(decisions, expected_labels):
    """Builds each record's predicted set from resolved decisions only, and
    reports which records are incomplete."""
    resolved = defaultdict(set)
    seen = defaultdict(set)
    unresolved = defaultdict(list)
    latencies, prompt_tokens, output_tokens = [], [], []
    status_counts = Counter()
    per_record_time = defaultdict(float)

    for decision in decisions:
        record_id, label = decision["RecordId"], decision["Label"]
        status_counts[decision["Status"]] += 1
        latencies.append(decision["LatencyMilliseconds"])
        per_record_time[record_id] += decision["LatencyMilliseconds"]

        if decision.get("PromptTokenCount"):
            prompt_tokens.append(decision["PromptTokenCount"])
        if decision.get("OutputTokenCount"):
            output_tokens.append(decision["OutputTokenCount"])

        if decision["Status"] == "ok":
            seen[record_id].add(label)
            if decision["Applicable"]:
                resolved[record_id].add(label)
        else:
            unresolved[record_id].append(label)

    complete = {r for r, labels in seen.items()
                if len(labels) == expected_labels and not unresolved.get(r)}

    ordered = sorted(latencies)

    def pct(p):
        return ordered[min(int(p * len(ordered)), len(ordered) - 1)] if ordered else 0.0

    cost = {
        "decisions": len(decisions),
        "status": dict(status_counts),
        "invalid_rate": status_counts["invalid_json"] / len(decisions) if decisions else 0.0,
        "failed_rate": status_counts["failed"] / len(decisions) if decisions else 0.0,
        "latency_ms_p50": pct(0.50), "latency_ms_p95": pct(0.95), "latency_ms_p99": pct(0.99),
        "latency_ms_mean": statistics.fmean(latencies) if latencies else 0.0,
        "seconds_per_document_all_labels": (
            statistics.fmean(per_record_time.values()) / 1000 if per_record_time else 0.0),
        "sustained_throughput_decisions_per_second": (
            len(latencies) / (sum(latencies) / 1000) if latencies else 0.0),
        "prompt_tokens_median": statistics.median(prompt_tokens) if prompt_tokens else None,
        "output_tokens_median": statistics.median(output_tokens) if output_tokens else None,
        "records_incomplete": len(set(seen) | set(unresolved)) - len(complete),
    }

    # A record with no affirmative decision still has an empty predicted set.
    sets = {r: resolved.get(r, set()) for r in seen}
    return sets, complete, cost


def about_slice(records, truth, candidates):
    """Indicative IS-versus-ABOUT slice over records the lexical markers flag.
    Reported separately and never merged into the headline metrics."""
    suspects = [r for r in records
                if ABOUT_PATTERN.search(r["content"]["serialized_input"][:400])
                and truth.get(r["record_id"])]

    rows = []
    for record in suspects:
        record_id = record["record_id"]
        rows.append({
            "record_id": record_id,
            "gold": sorted(truth[record_id]),
            "text": record["content"]["serialized_input"][:180].replace("\n", " "),
            **{name: sorted(predicted.get(record_id, set()))
               for name, predicted in candidates.items()},
        })

    return {
        "suspect_count": len(suspects),
        "note": ("Lexical suspects only. The ground truth itself was never adjudicated "
                 "for the IS/ABOUT boundary, so agreement here measures agreement with "
                 "an unadjudicated label, not correctness. Do not derive a metric from "
                 "this slice without human adjudication."),
        "rows": rows,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--test-split", type=Path, required=True)
    parser.add_argument("--encoder-predictions", type=Path, required=True)
    parser.add_argument("--llm-decisions", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    records = load_jsonl(args.test_split)
    truth = {r["record_id"]: set(r["encoder_labels"]) for r in records}
    all_ids = set(truth)

    encoder = {r["record_id"]: set(r["predicted"]) for r in load_jsonl(args.encoder_predictions)}
    decisions = load_jsonl(args.llm_decisions)
    llm, llm_complete, llm_cost = llm_sets(decisions, len(MACHINE_LABELS))

    report = {
        "benchmark": "EVAL-6",
        "test_split": str(args.test_split),
        "records": len(records),
        "labels": len(MACHINE_LABELS),
        "encoder": score_candidate("ENCODER cascade", truth, encoder, all_ids),
        "llm_per_label": score_candidate("LLM-PER-LABEL", truth, llm, llm_complete),
        "llm_cost": llm_cost,
        "is_about_slice": about_slice(
            records, truth, {"encoder": encoder, "llm": llm}),
    }

    # Per-label head-to-head, the view the decision criteria are stated against.
    encoder_f1 = {x["label"]: x["f1"] for x in report["encoder"]["per_label"]}
    llm_f1 = {x["label"]: x["f1"] for x in report["llm_per_label"]["per_label"]}
    report["per_label_delta"] = sorted(
        ({"label": l, "encoder_f1": encoder_f1[l], "llm_f1": llm_f1[l],
          "delta_llm_minus_encoder": llm_f1[l] - encoder_f1[l]} for l in MACHINE_LABELS),
        key=lambda x: -x["delta_llm_minus_encoder"])

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    for key in ("macro_f1", "micro_f1", "exact_match", "positive_any_label_recall", "oot_accuracy"):
        print(f"{key:28} encoder {report['encoder'][key]:.4f}   "
              f"llm {report['llm_per_label'][key]:.4f}")
    print(f"\nllm unresolved decisions: {llm_cost['status']}")
    print(f"records excluded from set-level LLM metrics: {llm_cost['records_incomplete']}")
    print(f"\nwrote {args.output}")


if __name__ == "__main__":
    main()
