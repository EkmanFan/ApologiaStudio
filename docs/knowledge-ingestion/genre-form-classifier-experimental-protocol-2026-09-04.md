# Genre/Form classifier — experimental protocol — 2026-09-04

Status: protocol. Nothing here has been executed. It is written to be run once
the encoder checkpoint is frozen, and it fixes the decision criteria *before*
any measurement, so that no result can be reinterpreted after the fact.

No Ollama inference, no encoder inference, no training and no GPU workload was
run to produce this document. It rests on committed EVAL-1..5 reports, on the
ApologiaStudio code, and on read-only inspection of the spike's artifacts.

## 0. Nomenclature

`A`, `B` and `C` are retired: they name the three EVAL-5 framings and must keep
that meaning. The two candidates under decision are:

| Name | Definition |
|---|---|
| **LLM-PER-LABEL** | genuinely separate binary LLM calls, one per work × label, no candidate list in any context |
| **ENCODER** | specialized encoder classifier with independent per-label sigmoid heads |

`ENCODER` is currently `FacebookAI/xlm-roberta-base`, not DeBERTa. The name is
kept generic because the family may change; the protocol does not depend on it.

## 1. State correction — the encoder is already threshold-calibrated

The architecture review of 2026-09-04 stated that the encoder's thresholds were
uncalibrated and that six of ten labels never fired. Two artifacts produced
after that review supersede it:

- `runs/xlm-roberta-base-title-notes/score-diagnostics-v1.json`
- `runs/xlm-roberta-base-title-notes/threshold-tuning-v1.json`

The tuning declares `selection_split: validation` and
`test_used_for_threshold_selection: false`, which is the correct discipline.

Effect of calibration on the **current, non-frozen** checkpoint:

| Split | Thresholds | micro P | micro R | micro F1 | macro F1 | exact-set |
|---|---|---:|---:|---:|---:|---:|
| validation | 0.5 fixed | 0.773 | 0.282 | 0.413 | 0.265 | 0.205 |
| validation | selected | 0.621 | 0.669 | 0.644 | **0.641** | 0.305 |
| test | 0.5 fixed | 0.815 | 0.243 | 0.374 | 0.207 | 0.151 |
| test | selected | 0.585 | 0.552 | 0.568 | **0.540** | 0.237 |

Per label on test, at the selected thresholds, with the threshold-independent
diagnostics beside them:

| Label | Thr | Support | P | R | F1 | Pred+ | ROC-AUC | PR-AUC |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Apologetic writings | 0.281 | 15 | 0.40 | 0.13 | 0.20 | 5 | 0.781 | 0.272 |
| Textbooks | 0.295 | 15 | 0.86 | 0.40 | 0.55 | 7 | 0.872 | 0.672 |
| Sacred works | 0.491 | 15 | 1.00 | 0.20 | 0.33 | 3 | 0.834 | 0.686 |
| Sermons | 0.190 | 16 | 0.30 | 0.75 | 0.43 | 40 | 0.844 | 0.336 |
| Devotional literature | 0.181 | 28 | 0.61 | 0.82 | 0.70 | 38 | 0.931 | 0.841 |
| Prayers | 0.729 | 27 | 1.00 | 0.52 | 0.68 | 14 | 0.934 | 0.847 |
| Biographies | 0.199 | 19 | 0.73 | 0.58 | 0.65 | 15 | 0.867 | 0.681 |
| Academic theses | 0.320 | 15 | 0.82 | 0.60 | 0.69 | 11 | 0.973 | 0.862 |
| Essays | 0.282 | 15 | 0.62 | 0.67 | 0.65 | 16 | 0.909 | 0.755 |
| Commentaries | 0.207 | 16 | 0.45 | 0.62 | 0.53 | 22 | 0.889 | 0.569 |

Four consequences for this protocol.

**Zero labels never fire.** The six silent labels were a fixed-threshold
artifact, exactly as suspected. That claim in the review is withdrawn.

**A ranking signal exists for every label.** ROC-AUC runs 0.781 to 0.973. Even
the weakest label separates positives from negatives well above chance. This is
the property `LLM-PER-LABEL` has no mechanism to exhibit.

**`Apologetic writings` is the weakest label of the ten** — PR-AUC 0.272,
P 0.40 / R 0.13 — and it is the label that matters most to this product. Any
conclusion drawn from macro averages must be checked against this label
separately.

**F1-optimal threshold selection is the wrong objective for MRA.** `Sermons`
was tuned to 0.190, producing 40 predicted positives for 16 true ones:
P 0.30 / R 0.75. F1 accepted that trade; a reviewer-facing assistant whose
suggestions must be trustworthy would not. E1 therefore fixes the selection
objective explicitly rather than inheriting it.

These numbers are **provisional**: the checkpoint is still being tuned on
epochs in another thread. E1 must be re-run in full on the frozen checkpoint.

## 2. E1 — Encoder calibration and single test evaluation

Runs only once the checkpoint is frozen. Preconditions, all verifiable before
any GPU work:

- the frozen checkpoint directory is named and its `model.safetensors` hash
  recorded;
- `dataset-v2` is unchanged since the checkpoint was trained — same
  `train/validation/test.jsonl`, verified by hash;
- the run is identified by the epoch count and seed that produced the frozen
  checkpoint.

Procedure, in this order and no other:

1. Load the frozen checkpoint. Never a mid-training checkpoint.
2. Score the **validation** split. Select one threshold per label from the
   validation scores alone.
3. **Freeze the thresholds** into a file that is never edited afterwards.
4. Score the **test** split once, at the frozen thresholds. No re-selection, no
   second pass, no "let us try 0.35 instead".

Threshold selection objective — fixed here so it is not chosen after seeing the
result. Report **three** selections per label from the same validation scores:

| Objective | Rule |
|---|---|
| `f1` | maximises validation F1 — comparability with the existing tuning |
| `precision_floor` | lowest threshold whose validation precision ≥ 0.80, maximising recall subject to that |
| `precision_floor_strict` | same at ≥ 0.90 |

If a label cannot reach a floor at any threshold, it is reported as
**unreachable at that floor**, not silently relaxed. A label that cannot reach
0.80 precision on validation is a candidate for staying on a different mechanism
altogether, and that is a finding, not a failure to be tuned away.

Required report, per label: support, selected threshold (for each of the three
objectives), TP, FP, FN, precision, recall, F1, predicted positives, and the
threshold-independent ROC-AUC and PR-AUC.

Required report, overall: micro precision / recall / F1, macro F1, exact-set
accuracy, the count of labels never predicted, and the **distribution of the
number of labels predicted per work** — 0, 1, 2, 3+ — against the same
distribution in the ground truth. That distribution is the encoder-side answer
to the question EVAL-5 asked of the LLM: does the mechanism produce several
applicable labels when several apply?

Also report the validation-to-test gap on macro F1. The current provisional gap
is 0.641 → 0.540; thresholds fitted on 151 records with 15-28 positives per
label are expected to overfit, and the size of that gap is itself a decision
input.

## 3. E2 — Common benchmark, ENCODER versus LLM-PER-LABEL

Not to be executed until explicitly requested.

Held identical between the two candidates, without exception:

| | Value |
|---|---|
| Items | the `dataset-v2` **test** split, 152 work groups |
| Input text | the `text_title_notes` field, byte-for-byte |
| Labels | the 10 `dataset-v2` labels, in `labels.json` order |
| Ground truth | the `labels` field of each record |
| Metrics | those of section 2 |

`LLM-PER-LABEL` protocol:

- one independent inference per work × label — 152 × 10 = 1 520;
- no context contains a list of candidate terms, so no ordering exists;
- the binary framing already established as EVAL-5's condition C, unchanged in
  its rules, so this measurement is continuous with EVAL-4A and EVAL-5D;
- repetitions: 3 per work × label, reporting the majority decision and the
  disagreement rate, because this candidate is stochastic and a single pass
  would misrepresent it as deterministic. 4 560 inferences; at the 1 574 ms
  median measured in EVAL-5C, roughly two hours.

Scoring rules that must be stated before the run:

- **The MRA policy guards are bypassed on both sides.** `MaximumSuggestions`,
  the hierarchy rule and `insufficientEvidence` are downstream policy applied
  identically to whatever wins. Applying them to one candidate and not the other
  would score the policy, not the mechanism.
- **`LLM-PER-LABEL` is not an oracle and not ground truth.** EVAL-5D established
  that it over-affirms when asked the full vocabulary. Its output is a
  measurement like any other.
- **The ground truth is LoC cataloguing practice, not Apologia policy.** Both
  candidates are measured against the same silver labels, so the comparison is
  fair, but no conclusion may be phrased as "correct under GF-RULE".

## 4. E3 — IS-versus-ABOUT challenge set

Separate from E2 by design. E2 measures general classification quality on the
catalogue distribution; E3 measures one semantic boundary. Mixing them would let
a good aggregate hide the failure that matters.

This is the criterion on which **neither candidate has any evidence**.
`adversarial-study-of-sermons` collapsed 5/5 → 1/5 between EVAL-5's framings A
and B, but it was absent from the EVAL-5D oracle set and from the EVAL-4A probe
plan, so `LLM-PER-LABEL` has never been asked it. `ENCODER`, trained on title
plus notes, is a priori at risk of learning keyword presence, which is precisely
this failure mode.

### Construction rules

Per the instruction, the rules come before the cases. No case is written until
these are accepted.

**Unit.** A minimal pair: two records sharing a genre/form term in their
surface text, where the term applies to one and not the other. One pair
contributes two items.

**Pair validity.** A pair is admissible only if all of the following hold.

1. Both items come from the LoC catalogue and carry the catalogue's own
   Genre/Form assignment. No case is invented, and no label is assigned by us.
2. The IS item carries the target term in its Genre/Form field. The ABOUT item
   does **not** carry it.
3. Both items' `text_title_notes` contain the term's surface form or an obvious
   morphological variant — "sermon", "sermons", "preaching". A pair where the
   ABOUT item never names the form tests nothing.
4. The two items are not the same work or edition, verified by the same
   `work_group_key` normalisation `dataset-v2` uses.
5. Neither item appears in `dataset-v2`'s train or validation split. Overlap
   with the test split is permitted and must be recorded, since test items were
   never trained on.

**Coverage.** 30 to 50 pairs, at least 3 pairs for each target term that reaches
it. Target terms are the 10 `dataset-v2` labels; the 4 sparse labels are out of
scope for E3, as they are for E2. Report the achieved per-term count; a term
with fewer than 3 admissible pairs is reported as under-covered rather than
padded.

**Annotation.** Two fields per item, both mechanical, neither a judgement call
of ours: `expected_positive` for the target term, taken from the catalogue's
Genre/Form field; and `surface_term_present`, taken from a regex over
`text_title_notes`. A third field, `pair_id`, links the two. No free-text
rationale is stored, so no annotator opinion can leak into the ground truth.

**Adversarial floor.** At least a quarter of the ABOUT items must be *studies of
the form itself* — a history of apologetics, a study of sermons — rather than
merely unrelated works that happen to contain the word. This is the hard case;
without a floor the set drifts toward easy negatives.

### Metrics

Reported separately from E2, per target term and overall:

- false-positive rate on ABOUT items — the primary number;
- true-positive rate on IS items, to prove the set is not simply hard for
  everyone;
- **pair accuracy**: both items of the pair correct. This is the only metric
  that cannot be gamed by a mechanism that always answers false;
- for `ENCODER`, the score margin between the IS and ABOUT item of each pair,
  which says whether the boundary is represented at all or merely thresholded.

## 5. Correction on LLM scores

The architecture review left open that Ollama logprobs, if available, might give
`LLM-PER-LABEL` a per-label threshold mechanism. That was an overstatement and
is corrected here.

A token logprob is the model's confidence in emitting a token. It is **not** a
calibrated probability that a genre/form term applies. Treating one as the other
would repeat the EVAL-5 error in a new form: assuming a property holds because
something resembling it is available.

Before any logprob could serve as a per-label threshold mechanism, three things
would have to be demonstrated, in this order, and each can fail independently:

1. **Discrimination** — the score separates applicable from non-applicable
   cases, measured as per-label ROC-AUC on a labelled set. Anything near 0.5 is
   noise regardless of how confident it looks.
2. **Stability** — the score is reproducible across repetitions at
   `temperature = 0.2`. A score that moves between runs cannot anchor a
   threshold.
3. **Calibratability** — a monotone mapping from score to observed frequency
   exists and holds on held-out data.

Only after all three would a per-label threshold be meaningful. None has been
measured, and the current runtime returns no score at all:
`StructuredGenerationResult` carries `Model`, `Json`, `DoneReason`, token counts
and a duration, and `OllamaStructuredGenerationRuntime` never requests one.

For `ENCODER` the situation is categorically different. Independent sigmoid
outputs **are** a per-label score by construction, and the diagnostics above
already show per-label ROC-AUC between 0.781 and 0.973 on real data. The
mechanism exists and has been measured; only its quality is in question.

This asymmetry is not a tie-breaker to be applied now, but it must not be
flattened by the phrase "both could have scores".

## 6. Decision criteria

Fixed before execution. Each cell states not only the value but its
epistemic status: **measured**, **by construction**, or **unknown**.

| Criterion | ENCODER | LLM-PER-LABEL |
|---|---|---|
| Structural independence between labels | **By construction** — independent sigmoid heads, no candidate list, no ordering | **By construction** — one label per context, no list to order |
| Per-label precision | **Measured, provisional**: 0.30-1.00 on test at F1-tuned thresholds; `Apologetic writings` 0.40 | **Unknown** on any comparable set; EVAL-5D showed over-affirmation on 2 of 7 cases |
| Per-label recall | **Measured, provisional**: 0.13-0.82; `Apologetic writings` 0.13 | **Unknown**; 20/20 on De Decretis is one label on one case |
| IS-versus-ABOUT robustness | **Unknown** — a priori at risk from title+notes keyword learning | **Unknown** — never asked the question |
| Exact-set | **Measured, provisional**: 0.237 test at selected thresholds | **Unknown** on the common benchmark |
| Stability | **By construction** — deterministic | **Measured as absent**: `temperature = 0.2`, variance in every EVAL from 2 on |
| Calibratability / thresholds | **By construction and measured** — ROC-AUC 0.781-0.973 per label | **Unknown, and no mechanism exists today**; see section 5 |
| Inference cost | **Measured**: 1 forward pass per work | **Measured**: 10 inferences per work on this benchmark, 14 in production |
| Latency | **Measured**: 3.99 ms median, 21.20 ms p95 | **Measured**: 1 574 ms median per call; 27 898 ms per work in EVAL-5D |
| Operational surface | **Measured as new**: ROCm/Docker runtime, training reproducibility, model and threshold versioning, drift | **Measured as nil**: reuses the existing runtime, validator, policy and UI |
| Reviewer-visible justification | **By construction absent** — breaks the current suggestion contract, which fails validation on a missing justification | **By construction present** — one justification per decision |
| MRA integration complexity | **Unknown, presumed high**: new component, new contract for justification, 4 labels uncovered | **Low**: the EVAL-5C framing already exists in the evaluation project |

Two criteria are decided by construction rather than by any experiment, in
opposite directions: `ENCODER` alone can carry a calibrated per-label score,
`LLM-PER-LABEL` alone can produce the justification the contract requires. E2
and E3 cannot change either. They decide whether the quality difference is large
enough to pay for one of those two structural costs.

Non-negotiable in every scenario: the deterministic policy validator, the
fail-closed behaviour, and the reviewer's authority over every suggestion.

## 7. Sequencing and stopping rule

1. **E1** on the frozen checkpoint. Stop for review.
2. **E3 construction** — rules accepted, cases built, set frozen and committed
   before either candidate sees it. Stop for review.
3. **E2** and **E3 execution**, on explicit request only.
4. Decide.

Evaluate `ENCODER` alone and `LLM-PER-LABEL` alone. Adopt the simpler mechanism
if it suffices. A hybrid cascade is **not** a candidate at this stage and must
not be pre-selected: it is only to be studied if the evidence shows neither
mechanism satisfies the criteria on its own.

## 8. Artifacts required once the checkpoint is frozen

| Artifact | Purpose |
|---|---|
| frozen checkpoint directory + `model.safetensors` hash | reproducibility of every downstream number |
| epoch count, seed, learning rate, batch size, max length | identifies the run |
| `dataset-v2` file hashes | proves the splits did not move under the model |
| `thresholds-frozen.json` — per label, per objective | the object E2 consumes; never edited after E1 |
| `e1-report.json` | section 2's metrics |
| `is-about-challenge-set.jsonl` + its construction log | E3 input, frozen before any candidate sees it |

## 9. Conflicts with current code and reports

**The architecture review of 2026-09-04 is partly stale.** Its claims that the
encoder is uncalibrated and that six labels never fire were true of
`baseline-report.json` and are false as of `threshold-tuning-v1.json`. Corrected
in place, per the project's rule that these documents are authoritative rather
than historical.

**`GenreFormApplicabilityProbe` cannot consume a LoC record as-is.** It builds
its user prompt from a `GenreFormEvaluationCase` — title, contributors,
language, edition, description, sections — whereas `text_title_notes` is a
single pre-rendered blob. Feeding it through the `Title` field would render
`title: Title: ...\nNote: ...`, which is not the same input the encoder sees,
and E2's central requirement is byte-identical input.

Required change, additive only: a second entry point on the probe taking the
evidence text verbatim, leaving the existing method untouched so EVAL-4A and
EVAL-5D stay reproducible. It is specified here and deliberately **not
implemented yet**: it must be written against the frozen protocol, and adding
code to the evaluation project that cannot be executed or validated would be
unverified work.

**The 4 sparse labels are outside every experiment here.** Pastoral letters and
charges, Creeds, Catechisms and Hagiographies have 6, 9, 29 and 50 records in
the whole LoC catalogue. Whatever wins E2 and E3, those labels need a separate
mechanism, and no result from this protocol says anything about them.

**Neither experiment measures transfer to MRA evidence.** `text_title_notes` is
a common denominator chosen because both candidates can consume it, not because
it is the production input, which includes body excerpts. Transfer stays a
separate measurement after the decision.
