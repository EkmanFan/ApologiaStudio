# Genre/Form — local LLM versus encoder: what is measured, what is not — 2026-09-06

Purpose: give a single, honest basis for deciding between the local LLM path and
the encoder cascade, on classification quality **and** on hardware occupancy.

Read the top of section 3 before quoting any quality number from this document.

Nothing was executed to produce it: no Ollama inference, no encoder inference,
no training, no GPU workload. It consolidates the committed EVAL-1..5 reports,
the ApologiaStudio code, and the Spike Encoder V2.1 artifacts at
`~/RiderProjects/SpikeEncoder`.

## 1. Short answer

There is no head-to-head benchmark, and there never was one. The two sides were
measured on different data, different label spaces, different inputs and
different metrics. What follows separates what can be compared today from what
cannot.

- **Hardware and speed: comparable now.** Both sides have real latency numbers,
  and the encoder side has complete memory and throughput figures. One LLM
  figure is missing and is cheap to obtain.
- **Classification quality: not comparable, and the gap widened.** Since the
  architecture review of 2026-09-04 the encoder moved to a different taxonomy,
  a different dataset and a different input contract. Any table putting
  `macro-F1 0.76` beside `exact 64%` today would be misleading.

## 2. What each side actually measured

| | Local LLM (`qwen3.8:27b`) | Encoder cascade V2.1 |
|---|---|---|
| Evaluated on | 12-21 curated Apologia cases | 886-record frozen test split |
| Label space | 14 LCGFT profile terms | 24 predicted of a 27-label product taxonomy |
| Input | MRA evidence: title, contributors, description, body excerpts | `content.serialized_input`: `[TITLE][SUBTITLE][DESCRIPTION][TOC][STRUCTURE]`, 512 tokens |
| Ground truth | Apologia curation | LoC-derived corpus, 4 744 positive + 1 162 out-of-taxonomy, FR 2 965 / EN 2 941 |
| Out-of-taxonomy class | none; nearest analogue is "zero labels" | explicit OOT class, measured separately |
| Latency | measured | measured, CPU and GPU |
| Memory / VRAM / throughput | **not measured** | measured |
| Repetition variance | measured (stochastic) | not applicable (deterministic) |

The label spaces overlap but do not coincide. Twelve of the fourteen LCGFT
profile terms have an encoder counterpart — `apologetic_writing`, `textbook`,
`sacred_work`, `sermon`, `catechism`, `creed`, `devotional_literature`,
`prayer`, `biography`, `academic_degree_work`, `essays`, `commentary`.
`Hagiographies` has none, and `Pastoral letters and charges` maps only loosely
onto the broader `correspondence`. The encoder additionally predicts eleven
concepts the MRA profile does not contain at all.

**Consequence: the E2 protocol written on 2026-09-04 is obsolete.** It specified
`dataset-v2`, the `text_title_notes` field and 10 LCGFT labels. That dataset
belongs to the superseded V1 spike. Any common benchmark must now be
respecified against `gate-d-split-v1`.

## 3. Classification quality — presented, not compared

> These two blocks are **not** on the same scale. The exact-match figures look
> superficially similar, and that similarity is an artefact of unrelated test
> sets: 886 records against 12 cases, 24 labels against 14, catalogue text
> against MRA evidence. Do not read one against the other.

### Encoder, 886-record test split, 24 labels

| Configuration | Macro-F1 | Micro-F1 | Exact match | Positive recall | OOT accuracy |
|---|---:|---:|---:|---:|---:|
| XLM-R-large (primary) | 0.7636 | 0.7664 | 0.7709 | 0.9185 | 0.8793 |
| mDeBERTa-v3-base (fallback) | 0.7521 | 0.7555 | 0.7404 | 0.8722 | **0.9253** |
| **Cascade (retained)** | **0.7743** | 0.7735 | **0.7878** | **0.9677** | 0.8563 |

Cascade rule: run XLM-R-large; if it returns no label, run mDeBERTa and use its
result. The fallback rescued 36 genuinely positive documents at the cost of 4
extra out-of-taxonomy false positives.

Per-label F1 for XLM-R-large on the twelve labels that overlap the MRA profile:

| Label | F1 | | Label | F1 |
|---|---:|---|---|---:|
| `sacred_work` | 0.877 | | `devotional_literature` | 0.800 |
| `catechism` | 0.852 | | `sermon` | 0.759 |
| `creed` | 0.912 | | `biography` | 0.692 |
| `prayer` | 0.842 | | `textbook` | 0.630 |
| `apologetic_writing` | **0.821** | | `commentary` | 0.627 |
| `academic_degree_work` | 0.807 | | `essays` | **0.500** |

Two of these deserve attention in any comparison discussion.

`apologetic_writing` reaches 0.821. In the superseded V1 spike the same label
scored F1 0.20 with PR-AUC 0.272, and it was the label this product cares about
most. That was a corpus and taxonomy problem, not a ceiling.

`essays` is now the **worst** of the twenty-four labels at 0.500 — and `Essays`
is precisely the label that dominated the papacy-essay failures throughout
EVAL-4 and EVAL-5. Both mechanisms are weak on the same concept, which suggests
the difficulty is in the concept's boundary rather than in either architecture.

### Local LLM, 12 curated Apologia cases, 14 LCGFT labels, 5 repetitions

| Framing | Exact | Micro precision | Micro recall | Mean labels | Invalid |
|---|---:|---:|---:|---:|---:|
| joint subset selection (production) | 64% | 0.91 | 0.62 | 0.57 | 2/60 |
| decision matrix @800 output tokens | 69% | 0.85 | 0.79 | 0.82 | 11/60 |
| decision matrix @2400 output tokens | 68% | 0.82 | 0.80 | 0.82 | 0/60 |
| per-label calls (7 cases, 2 reps) | 57% | 0.69 | 0.75 | 0.93 | 0/14 |

Verdict already recorded: EVAL-5 = NO-GO for the matrix framing. Order
sensitivity persists, the about-versus-is control degrades from 5/5 to 1/5, and
independence obtained by instruction is not structural independence.

## 4. Hardware and speed — this part is comparable

### Encoder cascade, CPU, 16 threads, GPU not exposed

| | Value |
|---|---:|
| Both models ready | 2.137 s |
| Resident RSS, both loaded | 1 557 MiB |
| P50 / P95 / P99 per document | 78.71 / 130.10 / 195.48 ms |
| Fallback rate | 28.3% |
| Peak RSS under load | 3 402 MiB |
| **VRAM** | **0** |
| Throughput, batch 8 | 20.6 docs/s |
| Throughput, single document | 11.3 docs/s |

### XLM-R-large alone, on the RX 7900 XTX

| | Value |
|---|---:|
| Ready | 1.80 s |
| P50 / P95 / P99 | 10.18 / 11.28 / 13.61 ms |
| Peak VRAM allocated / reserved | 2 808 / 2 852 MiB |
| Throughput batch 1 / 8 / 16 / 32 | 100.6 / 457.2 / 432.1 / 332.2 docs/s |

### Local LLM, `qwen3.8:27b`

| | Value | Status |
|---|---:|---|
| Weights on disk, Q4_K_M, 27.3 B params | 17.7 GB | measured |
| Joint framing, one call for 14 labels, P50 | 3 134 ms | measured |
| Decision matrix, one call, P50 | 12 554 ms | measured |
| One binary per-label call, P50 | 1 574 ms | measured |
| Per-label over 14 labels, one document | 27 898 ms | measured |
| Output tokens, joint / matrix | 90 / 785 | measured |
| VRAM footprint in service | — | **not measured** |
| Host RSS | — | **not measured** |
| Sustained throughput | — | **not measured** |

Only the disk size is known for memory. On a 24 GiB card, 17.7 GB of weights
before any KV cache leaves little headroom — but that is arithmetic, not a
measurement, and it is not stated here as one.

### Ratios, per document, full classification

Read these as orders of magnitude, not as precise figures: the LLM classifies 14
labels and the encoder 24, on different inputs.

| Comparison | Ratio |
|---|---:|
| Encoder CPU cascade vs LLM joint single call | ~40× faster |
| Encoder CPU cascade vs LLM per-label ×14 | ~354× faster |
| Encoder GPU vs LLM per-label ×14 | ~2 740× faster |
| Throughput, encoder CPU batch 8 vs LLM per-label | ~575× |

### The decisive architectural point

The two are **not competing for the same resource**. The retained encoder
configuration is CPU-resident and consumes zero VRAM, which leaves the RX 7900
XTX entirely to `qwen3.8:27b`:

```text
RX 7900 XTX  ──  qwen3.8:27b
CPU / RAM    ──  XLM-R-large + mDeBERTa-v3-base   (~3.4 GiB, 0 VRAM)
```

A framing of "encoder *or* LLM" that assumes contention for the GPU is
therefore wrong for this machine. The real trade-off is 79 ms of CPU against
3 to 28 seconds of GPU, and what each mechanism can and cannot produce.

## 5. What is missing to make the comparison complete

**On the LLM side, three cheap measurements.** VRAM occupancy while the model is
resident, host RSS, and sustained throughput. All three are minutes of work now
that the GPU is free, and none requires a new experiment design. Their absence
is currently the only gap in the hardware comparison.

**On the quality side, a common benchmark that no longer exists in specified
form.** Making the two quality columns comparable requires deciding, first, which
of these it should be:

1. *Encoder's ground.* Run the LLM per-label over `gate-d-split-v1`'s 886-record
   test split on the 12 overlapping labels, same `serialized_input`. Cost at the
   measured 1 574 ms per call: 886 × 12 ≈ 10 632 inferences, roughly 4.6 hours
   for one pass. Measures the LLM on catalogue text, which is not MRA evidence.
2. *MRA's ground.* Score the encoder on the Apologia evaluation cases. Cheap in
   compute, but 12-21 cases cannot support per-label precision for 12 labels,
   and the encoder would be scored on an input distribution it never saw.
3. *A new shared set.* MRA-shaped evidence with enough per-label positives.
   Correct, and the most expensive.

None is obviously right, and the choice determines what the resulting numbers
mean. That decision precedes any run.

**Two things neither option resolves.** The about-versus-is boundary still has no
measurement on either side — the one MRA case that isolates it collapsed 5/5 to
1/5 between LLM framings, and it was never put to the per-label framing or to the
encoder. And the encoder produces no reviewer-facing justification, which the
current MRA suggestion contract requires and treats a missing one as a validation
failure.

## 6. Provisional reading

On hardware there is no contest: 79 ms on CPU with zero VRAM against 3 to 28
seconds on the GPU, two to three orders of magnitude apart, and the encoder
leaves the card free for the LLM to do something else.

On quality nothing is settled, and the honest statement is that we do not know.
The encoder's 0.774 macro-F1 over 886 records and 24 labels is a far more solid
number than anything the LLM side has — 12 cases cannot yield a macro-F1 worth
the name — but it is a number about a different problem. That asymmetry favours
the encoder in the evidence available, not necessarily in the task at hand.

What the encoder cannot do is write the justification a reviewer reads. What the
LLM cannot do is produce a calibrated per-label score. Those two facts are
structural, no benchmark will change them, and they are likely to matter more to
the final architecture than any macro-F1 difference.
