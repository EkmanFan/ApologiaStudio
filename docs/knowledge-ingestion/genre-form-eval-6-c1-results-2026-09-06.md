# EVAL-6 C1 — stratified head-to-head — 2026-09-06

Status: tier 1 complete. **Verdict A — ENCODER clearly ahead. STOP.**
C2 was not launched.

Both candidates scored on **exactly the same 480 decisions**, by the same code,
against the same ground truth. The encoder was re-scored on this sample; its
published 886-record figure of 0.7743 appears nowhere in this comparison, and
the LLM's macro-F1 must never be set beside it.

## Run

| | |
|---|---|
| Sample | `stratified-sample-v1.jsonl` tier 1, sha256 `20473a80…` |
| Decisions | 480 — 24 labels x (10 positive + 10 negative) |
| Strata | 240 positive, 72 negative OOT, 168 negative in-taxonomy |
| Languages | fr 264, en 216 |
| Model | `qwen3.8:27b`, temperature 0.2, num_predict 64, timeout 120 s, 3 attempts |
| Contract | unchanged from Step B, before and after observation |
| Wall clock | 8 min 34 s |
| GPU state at launch | no model resident, card at 0.03 GiB, port 5090 free |

## Aggregates on the common sample

| | macro P | macro R | macro F1 | micro P | micro R | micro F1 |
|---|---:|---:|---:|---:|---:|---:|
| ENCODER cascade | 0.9809 | 0.8000 | **0.8705** | 0.9796 | 0.8000 | 0.8807 |
| LLM-PER-LABEL | 0.9348 | 0.5417 | **0.6582** | 0.9630 | 0.5417 | 0.6933 |
| delta (LLM − ENCODER) | −0.0461 | **−0.2583** | **−0.2123** | −0.0166 | −0.2583 | −0.1874 |

**The gap is recall, not precision.** Macro precision differs by 0.046; macro
recall by 0.258. The LLM is nearly as accurate as the encoder when it does fire
— it simply does not fire. Across 240 positive decisions it answered true 130
times against the encoder's 192.

That is the same conservative behaviour EVAL-1 through EVAL-5 measured on this
model with this rule set: told that false is a valid and expected answer, it
prefers false. Here that disposition costs it the benchmark.

## Per label

n+/n− is 10/10 for every label. Counts are TP FP FN TN.

| Label | n+/n− | ENC counts | ENC P | ENC R | ENC F1 | LLM counts | LLM P | LLM R | LLM F1 | dF1 |
|---|---|---|---:|---:|---:|---|---:|---:|---:|---:|
| `textbook` | 10/10 | 6 1 4 9 | 0.86 | 0.60 | 0.71 | 4 0 6 10 | 1.00 | 0.40 | 0.57 | **-0.134** |
| `handbook_manual` | 10/10 | 8 0 2 10 | 1.00 | 0.80 | 0.89 | 3 0 7 10 | 1.00 | 0.30 | 0.46 | **-0.427** |
| `dictionary` | 10/10 | 8 0 2 10 | 1.00 | 0.80 | 0.89 | 4 0 6 10 | 1.00 | 0.40 | 0.57 | **-0.317** |
| `encyclopedia` | 10/10 | 8 0 2 10 | 1.00 | 0.80 | 0.89 | 7 0 3 10 | 1.00 | 0.70 | 0.82 | **-0.065** |
| `academic_degree_work` | 10/10 | 8 0 2 10 | 1.00 | 0.80 | 0.89 | 1 0 9 10 | 1.00 | 0.10 | 0.18 | **-0.707** |
| `conference_proceedings` | 10/10 | 8 0 2 10 | 1.00 | 0.80 | 0.89 | 8 0 2 10 | 1.00 | 0.80 | 0.89 | **+0.000** |
| `anthology` | 10/10 | 7 1 3 9 | 0.88 | 0.70 | 0.78 | 7 1 3 9 | 0.88 | 0.70 | 0.78 | **+0.000** |
| `collected_works` | 10/10 | 10 0 0 10 | 1.00 | 1.00 | 1.00 | 10 1 0 9 | 0.91 | 1.00 | 0.95 | **-0.048** |
| `edited_volume` | 10/10 | 10 0 0 10 | 1.00 | 1.00 | 1.00 | 9 2 1 8 | 0.82 | 0.90 | 0.86 | **-0.143** |
| `biography` | 10/10 | 8 0 2 10 | 1.00 | 0.80 | 0.89 | 7 0 3 10 | 1.00 | 0.70 | 0.82 | **-0.065** |
| `autobiography` | 10/10 | 5 0 5 10 | 1.00 | 0.50 | 0.67 | 5 0 5 10 | 1.00 | 0.50 | 0.67 | **+0.000** |
| `personal_narrative` | 10/10 | 9 0 1 10 | 1.00 | 0.90 | 0.95 | 6 0 4 10 | 1.00 | 0.60 | 0.75 | **-0.197** |
| `essays` | 10/10 | 4 0 6 10 | 1.00 | 0.40 | 0.57 | 4 0 6 10 | 1.00 | 0.40 | 0.57 | **+0.000** |
| `commentary` | 10/10 | 6 0 4 10 | 1.00 | 0.60 | 0.75 | 4 0 6 10 | 1.00 | 0.40 | 0.57 | **-0.179** |
| `apologetic_writing` | 10/10 | 8 0 2 10 | 1.00 | 0.80 | 0.89 | 3 0 7 10 | 1.00 | 0.30 | 0.46 | **-0.427** |
| `catechism` | 10/10 | 10 1 0 9 | 0.91 | 1.00 | 0.95 | 5 1 5 9 | 0.83 | 0.50 | 0.62 | **-0.327** |
| `creed` | 10/10 | 9 1 1 9 | 0.90 | 0.90 | 0.90 | 4 0 6 10 | 1.00 | 0.40 | 0.57 | **-0.329** |
| `devotional_literature` | 10/10 | 9 0 1 10 | 1.00 | 0.90 | 0.95 | 8 0 2 10 | 1.00 | 0.80 | 0.89 | **-0.058** |
| `prayer` | 10/10 | 8 0 2 10 | 1.00 | 0.80 | 0.89 | 5 0 5 10 | 1.00 | 0.50 | 0.67 | **-0.222** |
| `sacred_work` | 10/10 | 10 0 0 10 | 1.00 | 1.00 | 1.00 | 6 0 4 10 | 1.00 | 0.60 | 0.75 | **-0.250** |
| `sermon` | 10/10 | 9 0 1 10 | 1.00 | 0.90 | 0.95 | 5 0 5 10 | 1.00 | 0.50 | 0.67 | **-0.281** |
| `scholarly_article` | 10/10 | 5 0 5 10 | 1.00 | 0.50 | 0.67 | 0 0 10 10 | 0.00 | 0.00 | 0.00 | **-0.667** |
| `correspondence` | 10/10 | 9 0 1 10 | 1.00 | 0.90 | 0.95 | 9 0 1 10 | 1.00 | 0.90 | 0.95 | **+0.000** |
| `diary` | 10/10 | 10 0 0 10 | 1.00 | 1.00 | 1.00 | 6 0 4 10 | 1.00 | 0.60 | 0.75 | **-0.250** |

## Labels with |delta F1| >= 0.15

Thirteen of twenty-four. **All thirteen favour the encoder; the LLM leads on
none, at any margin.**

| Label | delta F1 | Ahead |
|---|---:|---|
| `academic_degree_work` | -0.707 | ENCODER |
| `scholarly_article` | -0.667 | ENCODER |
| `handbook_manual` | -0.427 | ENCODER |
| `apologetic_writing` | -0.427 | ENCODER |
| `creed` | -0.329 | ENCODER |
| `catechism` | -0.327 | ENCODER |
| `dictionary` | -0.317 | ENCODER |
| `sermon` | -0.281 | ENCODER |
| `sacred_work` | -0.250 | ENCODER |
| `diary` | -0.250 | ENCODER |
| `prayer` | -0.222 | ENCODER |
| `personal_narrative` | -0.197 | ENCODER |
| `commentary` | -0.179 | ENCODER |

Five labels tie exactly — `conference_proceedings`, `anthology`,
`autobiography`, `essays`, `correspondence` — and the remaining six differ by
less than 0.15, always in the encoder's favour.

Two collapses stand out. `scholarly_article`: the LLM answered true **zero times
in ten positives**, F1 0.00 against the encoder's 0.67. `academic_degree_work`:
one true in ten, F1 0.18 against 0.89. On this evidence the definition alone
does not let the model recognise those two forms from a title.

## Apologia-critical labels

Reported separately, as specified; they did not influence sampling.

| Label | ENCODER F1 | LLM F1 | delta |
|---|---:|---:|---:|
| `essays` | 0.571 | 0.571 | +0.000 |
| `devotional_literature` | 0.947 | 0.889 | -0.058 |
| `commentary` | 0.750 | 0.571 | -0.179 |
| `prayer` | 0.889 | 0.667 | -0.222 |
| `sacred_work` | 1.000 | 0.750 | -0.250 |
| `sermon` | 0.947 | 0.667 | -0.281 |
| `catechism` | 0.952 | 0.625 | -0.327 |
| `creed` | 0.900 | 0.571 | -0.329 |
| `apologetic_writing` | 0.889 | 0.462 | -0.427 |

`apologetic_writing`, the label this product cares most about, is one of the
worst: 0.889 against 0.462, a −0.427 gap, with the LLM recognising 3 of 10
positives. `creed` and `catechism` follow at −0.33. Only `essays` ties, and it
ties low at 0.571 for both — the same weak concept boundary EVAL-4 and EVAL-5
found on the LCGFT profile.

## LLM operational behaviour

| | Value |
|---|---:|
| Invalid JSON | **0 / 480 (0.00%)** |
| Failures, timeouts | **0 / 480 (0.00%)** |
| First-attempt success | **100.00%** |
| Resolved only after retry | 0 |
| Latency p50 / p95 / p99 | 1 044 / 1 266 / 1 392 ms |
| Latency mean | 1 072 ms |
| Sustained throughput | 0.93 decisions/s |
| Projected per document, 24 labels | **25.7 s** |
| Input tokens, median | 407.5 |
| Output tokens, median | 12 |
| Resident VRAM | 20.21 – 20.66 GiB of 23.98 |
| Host RSS, `ollama serve` | 0.07 GiB |

The contract is operationally flawless: not one malformed or failed decision in
480, and the retry policy never engaged. Whatever the quality verdict, this is
not a reliability problem.

The cost, against the encoder cascade's 78.71 ms P50 and zero VRAM: **25.7
seconds per document versus 79 milliseconds**, roughly 330x, plus 20.66 GiB of
GPU held throughout.

## Family B — not computable

No document in the stratified sample carries all 24 decisions, so exact match,
OOT accuracy and labels-per-document are omitted rather than approximated, as
the protocol requires.

## Verdict

**A — ENCODER clearly ahead. STOP.**

The rule is a macro-F1 delta of at least 0.05 in the encoder's favour with no
credible prospect of reversal. The measured delta is **0.2123**, over four times
the threshold, with the encoder ahead or level on all 24 labels and the LLM
leading on none. No plausible movement from a larger sample reverses that.

C2 is not launched.

## What this does and does not establish

It establishes that on this corpus, this input and this ground truth, an
independent binary LLM decision per label agrees with the frozen labels far less
often than the trained cascade — driven by recall, at roughly 330x the latency
and 20.66 GiB of VRAM.

It does not establish that the LLM understands the taxonomy less well. The
encoder was trained on this corpus's annotation conventions; the LLM reads a
definition once. The benchmark measures agreement with the frozen ground truth,
which is silver and unreviewed. The evidence is also thin — 477 of 886 records
are title-only — which penalises the reader of a definition more than a model
fitted to the corpus.

Those caveats bound the interpretation. They do not soften the architectural
conclusion: a 0.21 macro-F1 deficit with zero labels won, at 330x the cost, is
not a close call.

Nothing here decides the final architecture. It closes the question of whether
LLM-PER-LABEL is a viable *replacement* for the encoder cascade on this task.
