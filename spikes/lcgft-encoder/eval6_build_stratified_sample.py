#!/usr/bin/env python3
"""
EVAL-6 - build the frozen stratified decision sample.

Replaces the exhaustive 886 x 24 grid with a stratified sample of
document x label decisions, chosen BEFORE any inference and never changed
afterwards.

Design rules, all enforced here rather than left to the runner:

- The primary stratum is the LABEL: every one of the 24 gets its own positives
  and negatives.
- Selection is deterministic. No RNG: candidates are ordered by
  sha256(seed | label | stratum | record_id), so the same inputs always yield
  the same sample on any machine, and a resumed run cannot drift.
- Selection is blind to both candidates. Nothing here reads an encoder
  prediction or an LLM decision.
- FR and EN alternate inside each stratum, so a tier is balanced by
  construction and degrades gracefully when one language runs out.
- Negatives mix out-of-taxonomy and in-taxonomy records, 3 to 7 per tier of
  ten: OOT is represented without being the only source of negatives.
- All three tiers are emitted in one file. Tier 2 therefore cannot replay
  tier 1, and the whole sample is frozen before the first call.

Output: one JSON line per decision, plus a manifest carrying the file hash.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path

MACHINE_LABELS = [
    "textbook", "handbook_manual", "dictionary", "encyclopedia",
    "academic_degree_work", "conference_proceedings", "anthology",
    "collected_works", "edited_volume", "biography", "autobiography",
    "personal_narrative", "essays", "commentary", "apologetic_writing",
    "catechism", "creed", "devotional_literature", "prayer", "sacred_work",
    "sermon", "scholarly_article", "correspondence", "diary",
]

TIERS = 3
PER_TIER_POSITIVE = 10
PER_TIER_NEGATIVE = 10
OOT_PER_TIER = 3  # of the ten negatives; the rest are in-taxonomy negatives


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def order_key(seed: str, label: str, stratum: str, record_id: str) -> str:
    return hashlib.sha256(f"{seed}|{label}|{stratum}|{record_id}".encode()).hexdigest()


def deterministic(records, seed, label, stratum):
    return sorted(records, key=lambda r: order_key(seed, label, stratum, r["record_id"]))


def interleave_languages(records, seed, label, stratum):
    """Alternates fr and en so any prefix is as balanced as the pools allow."""
    french = [r for r in records if r["language"] == "fr"]
    english = [r for r in records if r["language"] != "fr"]

    french = deterministic(french, seed, label, stratum + ":fr")
    english = deterministic(english, seed, label, stratum + ":en")

    merged = []
    for index in range(max(len(french), len(english))):
        if index < len(french):
            merged.append(french[index])
        if index < len(english):
            merged.append(english[index])

    return merged


def compose_negatives(oot, in_taxonomy):
    """Three OOT then seven in-taxonomy, repeated. Falls back to whichever pool
    still has records so a tier is never short because one side ran dry."""
    sequence = []
    oot_index = in_index = 0

    while oot_index < len(oot) or in_index < len(in_taxonomy):
        for _ in range(OOT_PER_TIER):
            if oot_index < len(oot):
                sequence.append(oot[oot_index]); oot_index += 1
            elif in_index < len(in_taxonomy):
                sequence.append(in_taxonomy[in_index]); in_index += 1

        for _ in range(PER_TIER_NEGATIVE - OOT_PER_TIER):
            if in_index < len(in_taxonomy):
                sequence.append(in_taxonomy[in_index]); in_index += 1
            elif oot_index < len(oot):
                sequence.append(oot[oot_index]); oot_index += 1

    return sequence


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--test-split", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--seed", default="eval6-stratified-v1")
    args = parser.parse_args()

    with args.test_split.open(encoding="utf-8") as handle:
        records = [json.loads(line) for line in handle if line.strip()]

    rows = []
    shortfalls = []

    for label in MACHINE_LABELS:
        positives = [r for r in records if label in r["encoder_labels"]]
        negatives = [r for r in records if label not in r["encoder_labels"]]

        ordered_positive = interleave_languages(positives, args.seed, label, "positive")

        oot = interleave_languages(
            [r for r in negatives if r["is_encoder_out_of_taxonomy"]],
            args.seed, label, "negative_oot")
        in_taxonomy = interleave_languages(
            [r for r in negatives if not r["is_encoder_out_of_taxonomy"]],
            args.seed, label, "negative_in_taxonomy")

        ordered_negative = compose_negatives(oot, in_taxonomy)

        wanted_positive = TIERS * PER_TIER_POSITIVE
        if len(ordered_positive) < wanted_positive:
            shortfalls.append({
                "label": label,
                "positives_available": len(ordered_positive),
                "positives_requested": wanted_positive,
                "missing": wanted_positive - len(ordered_positive),
            })

        for tier in range(1, TIERS + 1):
            start_positive = (tier - 1) * PER_TIER_POSITIVE
            start_negative = (tier - 1) * PER_TIER_NEGATIVE

            selected = [
                (r, "positive")
                for r in ordered_positive[start_positive:start_positive + PER_TIER_POSITIVE]
            ] + [
                (r, "negative_oot" if r["is_encoder_out_of_taxonomy"]
                 else "negative_in_taxonomy")
                for r in ordered_negative[start_negative:start_negative + PER_TIER_NEGATIVE]
            ]

            for record, stratum in selected:
                rows.append({
                    "record_id": record["record_id"],
                    "work_key": record["work_key"],
                    "label": label,
                    "ground_truth": label in record["encoder_labels"],
                    "language": record["language"],
                    "stratum": stratum,
                    "tier": tier,
                    "selection_rule": "sha256(seed|label|stratum|record_id) ascending, "
                                      "fr/en interleaved, negatives 3 OOT : 7 in-taxonomy",
                    "selection_seed": args.seed,
                })

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8") as handle:
        for row in rows:
            handle.write(json.dumps(row, ensure_ascii=False) + "\n")

    # A decision identity must never appear twice: it would be scored twice.
    identities = [(r["record_id"], r["label"]) for r in rows]
    duplicates = [k for k, v in Counter(identities).items() if v > 1]
    if duplicates:
        raise SystemExit(f"{len(duplicates)} duplicated decision identities; sample rejected")

    per_tier = Counter(r["tier"] for r in rows)
    manifest = {
        "artifact": "eval6-stratified-sample-v1",
        "test_split": str(args.test_split),
        "test_split_sha256": sha256_file(args.test_split),
        "sample_sha256": sha256_file(args.output),
        "selection_seed": args.seed,
        "labels": len(MACHINE_LABELS),
        "tiers": TIERS,
        "per_tier_per_label": {"positive": PER_TIER_POSITIVE, "negative": PER_TIER_NEGATIVE,
                               "of_which_oot": OOT_PER_TIER},
        "decisions_total": len(rows),
        "decisions_per_tier": {str(k): v for k, v in sorted(per_tier.items())},
        "cumulative_by_tier": {
            str(t): sum(v for k, v in per_tier.items() if k <= t) for t in range(1, TIERS + 1)
        },
        "positive_shortfalls": shortfalls,
        "language_balance": {
            str(t): dict(Counter(r["language"] for r in rows if r["tier"] == t))
            for t in range(1, TIERS + 1)
        },
        "stratum_balance": {
            str(t): dict(Counter(r["stratum"] for r in rows if r["tier"] == t))
            for t in range(1, TIERS + 1)
        },
        "independence": "Selection reads only ground truth and language. "
                        "No encoder prediction and no LLM decision was consulted.",
    }
    args.output.with_suffix(".manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

    print(json.dumps(manifest, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
