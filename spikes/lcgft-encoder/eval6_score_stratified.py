#!/usr/bin/env python3
"""
EVAL-6 stratified scorer and sequential stop report.

Scores ENCODER and LLM-PER-LABEL on *exactly the same sampled decisions*, with
the same code, against the same ground truth. Published encoder metrics over the
886 records are never compared against LLM metrics over a sample: the encoder is
re-scored on the sample itself.

Two families of metric are kept strictly apart:

  A. valid on decision sampling      - per-label TP/FP/FN/TN, precision, recall,
                                       F1, micro and macro aggregates
  B. requires full 24-label coverage - exact match, OOT accuracy, labels per
                                       document

Family B is emitted only for documents whose 24 decisions are all present and
resolved, and the count is stated. No pseudo exact-match is ever fabricated from
partial coverage.

The stop rules are engineering decision rules, not statistical significance.
Wilson intervals are reported as context on how much the numbers could move,
never as a test.
"""

from __future__ import annotations

import argparse
import json
import math
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

# Highlighted in the report only. They never influence sampling or the rules.
APOLOGIA_CRITICAL = [
    "apologetic_writing", "creed", "catechism", "sermon", "prayer",
    "sacred_work", "devotional_literature", "commentary", "essays",
]

CLEAR_LEAD = 0.05
EQUIVALENT = 0.02
LABEL_LEAD = 0.10
LABEL_STRONG = 0.15


def load_jsonl(path: Path) -> list[dict]:
    with path.open(encoding="utf-8") as handle:
        return [json.loads(line) for line in handle if line.strip()]


def wilson95(successes: int, trials: int) -> tuple[float, float]:
    if trials == 0:
        return (0.0, 0.0)
    z = 1.96
    p = successes / trials
    denominator = 1 + z * z / trials
    centre = p + z * z / (2 * trials)
    margin = z * math.sqrt(p * (1 - p) / trials + z * z / (4 * trials * trials))
    return (max(0.0, (centre - margin) / denominator),
            min(1.0, (centre + margin) / denominator))


def prf(tp: int, fp: int, fn: int) -> tuple[float, float, float]:
    precision = tp / (tp + fp) if tp + fp else 0.0
    recall = tp / (tp + fn) if tp + fn else 0.0
    f1 = 2 * precision * recall / (precision + recall) if precision + recall else 0.0
    return precision, recall, f1


def score_on_sample(name: str, decisions: dict[tuple[str, str], bool],
                    truth: dict[tuple[str, str], bool]) -> dict:
    """decisions / truth keyed by (record_id, label). Only identities present in
    `decisions` are scored, so both candidates are judged on the same set."""
    per_label = []

    for label in MACHINE_LABELS:
        keys = [k for k in decisions if k[1] == label]
        tp = sum(1 for k in keys if truth[k] and decisions[k])
        fp = sum(1 for k in keys if not truth[k] and decisions[k])
        fn = sum(1 for k in keys if truth[k] and not decisions[k])
        tn = sum(1 for k in keys if not truth[k] and not decisions[k])

        precision, recall, f1 = prf(tp, fp, fn)
        precision_low, precision_high = wilson95(tp, tp + fp)
        recall_low, recall_high = wilson95(tp, tp + fn)

        per_label.append({
            "label": label,
            "n_positive": tp + fn,
            "n_negative": fp + tn,
            "tp": tp, "fp": fp, "fn": fn, "tn": tn,
            "precision": precision, "recall": recall, "f1": f1,
            "predicted_positive": tp + fp,
            "precision_ci95": [precision_low, precision_high],
            "recall_ci95": [recall_low, recall_high],
        })

    micro_tp = sum(x["tp"] for x in per_label)
    micro_fp = sum(x["fp"] for x in per_label)
    micro_fn = sum(x["fn"] for x in per_label)
    micro_p, micro_r, micro_f1 = prf(micro_tp, micro_fp, micro_fn)

    return {
        "candidate": name,
        "decisions_scored": len(decisions),
        "micro_precision": micro_p,
        "micro_recall": micro_r,
        "micro_f1": micro_f1,
        "macro_precision": statistics.fmean(x["precision"] for x in per_label),
        "macro_recall": statistics.fmean(x["recall"] for x in per_label),
        "macro_f1": statistics.fmean(x["f1"] for x in per_label),
        "per_label": per_label,
    }


def llm_decisions(rows: list[dict]) -> tuple[dict[tuple[str, str], bool], dict]:
    resolved: dict[tuple[str, str], bool] = {}
    latencies, prompt_tokens, output_tokens = [], [], []
    status = Counter()
    first_attempt_ok = 0

    for row in rows:
        key = (row["RecordId"], row["Label"])
        status[row["Status"]] += 1
        latencies.append(row["LatencyMilliseconds"])

        if row.get("PromptTokenCount"):
            prompt_tokens.append(row["PromptTokenCount"])
        if row.get("OutputTokenCount"):
            output_tokens.append(row["OutputTokenCount"])

        if row["Status"] == "ok":
            resolved[key] = bool(row["Applicable"])
            if row["Attempts"] == 1:
                first_attempt_ok += 1

    ordered = sorted(latencies)

    def pct(p: float) -> float:
        return ordered[min(int(p * len(ordered)), len(ordered) - 1)] if ordered else 0.0

    total_seconds = sum(latencies) / 1000 if latencies else 0.0

    cost = {
        "decisions_attempted": len(rows),
        "status": dict(status),
        "invalid_rate": status["invalid_json"] / len(rows) if rows else 0.0,
        "failure_rate": status["failed"] / len(rows) if rows else 0.0,
        "first_attempt_success_rate": first_attempt_ok / len(rows) if rows else 0.0,
        "resolved_after_retry": status["ok"] - first_attempt_ok,
        "latency_ms_p50": pct(0.50), "latency_ms_p95": pct(0.95), "latency_ms_p99": pct(0.99),
        "latency_ms_mean": statistics.fmean(latencies) if latencies else 0.0,
        "wall_clock_seconds": total_seconds,
        "sustained_decisions_per_second": len(latencies) / total_seconds if total_seconds else 0.0,
        "projected_seconds_per_document_24_labels": (
            statistics.fmean(latencies) * 24 / 1000 if latencies else 0.0),
        "prompt_tokens_median": statistics.median(prompt_tokens) if prompt_tokens else None,
        "output_tokens_median": statistics.median(output_tokens) if output_tokens else None,
    }
    return resolved, cost


def full_coverage_metrics(decisions, truth, resolved_keys) -> dict:
    """Family B. Emitted only for documents whose 24 decisions are all present."""
    by_document = defaultdict(set)
    for record_id, label in resolved_keys:
        by_document[record_id].add(label)

    complete = [r for r, labels in by_document.items() if len(labels) == len(MACHINE_LABELS)]

    if not complete:
        return {
            "documents_with_full_24_label_coverage": 0,
            "note": ("No document has all 24 decisions in this sample, so exact match, "
                     "OOT accuracy and labels-per-document are not computable and are "
                     "deliberately omitted rather than approximated."),
        }

    exact = 0
    for record_id in complete:
        predicted = {l for l in MACHINE_LABELS if decisions.get((record_id, l))}
        gold = {l for l in MACHINE_LABELS if truth[(record_id, l)]}
        if predicted == gold:
            exact += 1

    return {
        "documents_with_full_24_label_coverage": len(complete),
        "exact_match": exact / len(complete),
        "note": "Computed only over fully covered documents; not comparable to the 886-record run.",
    }


def stop_report(encoder: dict, llm: dict, tier: int) -> dict:
    delta = llm["macro_f1"] - encoder["macro_f1"]

    encoder_f1 = {x["label"]: x["f1"] for x in encoder["per_label"]}
    llm_f1 = {x["label"]: x["f1"] for x in llm["per_label"]}

    deltas = sorted(
        ({"label": l,
          "encoder_f1": encoder_f1[l],
          "llm_f1": llm_f1[l],
          "delta_llm_minus_encoder": llm_f1[l] - encoder_f1[l]}
         for l in MACHINE_LABELS),
        key=lambda x: -x["delta_llm_minus_encoder"])

    llm_wins = [d for d in deltas if d["delta_llm_minus_encoder"] >= LABEL_LEAD]
    encoder_wins = [d for d in deltas if d["delta_llm_minus_encoder"] <= -LABEL_LEAD]
    strong = [d for d in deltas if abs(d["delta_llm_minus_encoder"]) >= LABEL_STRONG]

    if delta <= -CLEAR_LEAD:
        verdict, action = "A - ENCODER clearly ahead", "STOP"
    elif delta >= CLEAR_LEAD:
        verdict, action = "B - LLM clearly ahead", "STOP"
    elif abs(delta) < EQUIVALENT:
        verdict, action = "C - equivalent for architecture", "STOP"
    else:
        verdict, action = "D - mixed or uncertain", "CONTINUE to the next tier"

    return {
        "tier": tier,
        "macro_f1_encoder": encoder["macro_f1"],
        "macro_f1_llm": llm["macro_f1"],
        "delta_llm_minus_encoder": delta,
        "micro_f1_encoder": encoder["micro_f1"],
        "micro_f1_llm": llm["micro_f1"],
        "macro_precision_delta": llm["macro_precision"] - encoder["macro_precision"],
        "macro_recall_delta": llm["macro_recall"] - encoder["macro_recall"],
        "labels_llm_ahead_by_0_10": [d["label"] for d in llm_wins],
        "labels_encoder_ahead_by_0_10": [d["label"] for d in encoder_wins],
        "labels_with_delta_at_least_0_15": [d["label"] for d in strong],
        "verdict": verdict,
        "action": action,
        "thresholds": {"clear_lead": CLEAR_LEAD, "equivalent": EQUIVALENT,
                       "label_lead": LABEL_LEAD, "label_strong": LABEL_STRONG},
        "caveat": ("Engineering decision rules, not statistical significance. "
                   "Under verdict C the architectural recommendation leans to ENCODER "
                   "on operational cost, which is a cost decision and not a quality win."),
        "per_label_delta": deltas,
        "apologia_critical_labels": [
            d for d in deltas if d["label"] in APOLOGIA_CRITICAL
        ],
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--sample", type=Path, required=True)
    parser.add_argument("--encoder-predictions", type=Path, required=True)
    parser.add_argument("--llm-decisions", type=Path, required=True)
    parser.add_argument("--tier", type=int, required=True,
                        help="score tiers 1..N cumulatively")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    sample = [r for r in load_jsonl(args.sample) if r["tier"] <= args.tier]
    truth = {(r["record_id"], r["label"]): r["ground_truth"] for r in sample}
    sampled = set(truth)

    encoder_sets = {r["record_id"]: set(r["predicted"])
                    for r in load_jsonl(args.encoder_predictions)}
    encoder = {k: (k[1] in encoder_sets.get(k[0], set())) for k in sampled}

    llm_rows = [r for r in load_jsonl(args.llm_decisions)
                if (r["RecordId"], r["Label"]) in sampled]
    llm, cost = llm_decisions(llm_rows)

    # Both candidates are scored on the identities the LLM actually resolved,
    # so neither is credited or penalised for a decision the other never made.
    common = sampled & set(llm)
    encoder_common = {k: encoder[k] for k in common}
    truth_common = {k: truth[k] for k in common}

    encoder_metrics = score_on_sample("ENCODER cascade", encoder_common, truth_common)
    llm_metrics = score_on_sample("LLM-PER-LABEL", {k: llm[k] for k in common}, truth_common)

    report = {
        "benchmark": "EVAL-6 stratified",
        "tier_scored": args.tier,
        "sample": str(args.sample),
        "decisions_in_sample_up_to_tier": len(sampled),
        "decisions_resolved_by_llm": len(common),
        "decisions_unresolved": len(sampled) - len(common),
        "strata": dict(Counter(r["stratum"] for r in sample)),
        "languages": dict(Counter(r["language"] for r in sample)),
        "metrics_family_a_decision_sampling": {
            "encoder": encoder_metrics,
            "llm_per_label": llm_metrics,
        },
        "metrics_family_b_requires_full_coverage": {
            "encoder": full_coverage_metrics(encoder_common, truth_common, common),
            "llm_per_label": full_coverage_metrics({k: llm[k] for k in common},
                                                   truth_common, common),
        },
        "llm_operational": cost,
        "sequential_stop_report": stop_report(encoder_metrics, llm_metrics, args.tier),
    }

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    stop = report["sequential_stop_report"]
    print(f"tier {args.tier}  decisions scored {len(common)} / {len(sampled)}")
    print(f"macro-F1  encoder {stop['macro_f1_encoder']:.4f}   "
          f"llm {stop['macro_f1_llm']:.4f}   delta {stop['delta_llm_minus_encoder']:+.4f}")
    print(f"verdict   {stop['verdict']}")
    print(f"action    {stop['action']}")
    print(f"\nwrote {args.output}")


if __name__ == "__main__":
    main()
