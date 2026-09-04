# Genre/Form classifier — architecture review post-EVAL-5 — 2026-09-04

Status: review. No decision is taken here and no experiment was run for it. It
defines the criteria a later experiment must answer, and records what is
demonstrated against what remains hypothetical.

Nothing in this review executed an Ollama inference, an encoder training, an
evaluation or any GPU workload. It rests on the committed EVAL-1..5 reports, on
the ApologiaStudio code, and on read-only inspection of the encoder spike's
artifacts.

## 1. What EVAL-5 establishes architecturally

Beyond the NO-GO itself, EVAL-5 settles four things that constrain every
candidate that follows.

**Independence obtained by instruction is not independence.** Condition B was
told explicitly, at length, that there is no best, principal, dominant or most
representative term and that candidates must never be compared. It still let
candidate order decide whether a second applicable label appeared: both terms in
2 of 4 orderings, 5/5 within each, 0/5 in the others. A property that must hold
structurally cannot be obtained from a prompt.

**Order sensitivity is a property of joint framings, not of the prompt.** Both A
and B are fully deterministic inside one ordering and flip on reordering alone.
`contra-gentes`, where a single term competes, is 5/5 under every ordering in
both. The effect requires competition between candidates in one context to
exist. Any framing that presents the vocabulary as a list in a single context
inherits it.

**The multi-label deficit is not a limit of the model's judgement.** De Decretis
went from 1/20 under the joint framing to 20/20 under the matrix, matching the
per-label oracle exactly. `qwen3.8:27b` holds the right judgement; the joint
framing suppresses its expression.

**Per-label independence has its own precision cost.** EVAL-5D asked the oracle
all fourteen candidates instead of the six probed in EVAL-4, and it
over-affirmed: `Sacred works` for De Decretis, `Devotional literature` for the
Septuagint. Condition C scored 8/14 exact against B's 27/35 on the same cases.
Independence alone does not buy precision; it must be bought separately, per
label.

Consequence for the vocabulary of this review: **C is no longer a semantic
ground truth.** It is one more candidate, with a measured false-positive
problem.

## 2. The two candidates

### Candidate A — separate binary LLM calls, one per label

Fourteen independent inferences, each asking whether one term applies, each in
its own context.

Demonstrated strengths:

- Structural independence is real, not instructed: no candidate list exists in
  any context, so no ordering exists to be sensitive to. Order invariance is
  guaranteed by construction rather than measured.
- Recovers the labels the joint framing suppresses: 20/20 on De Decretis in
  EVAL-5C, 10/10 in EVAL-4A.
- Reuses the existing runtime, validator, policy and review UI unchanged. No new
  subsystem, no new operational surface. This is its decisive advantage against
  the simplicity-first principle.
- Produces a reviewer-facing justification per decision, which the current
  suggestion contract requires (`MissingJustification` is a validation failure).

Demonstrated weaknesses:

- False positives when the full vocabulary is asked: EVAL-5D, two of seven cases.
- Cost: 27 898 ms median per work in EVAL-5D against 3 134 ms for the joint
  framing, and fourteen inferences instead of one.
- Non-deterministic: the runtime sends `temperature = 0.2`, and every EVAL from
  2 onward measured run-to-run variance.

Structural weakness, verified in code rather than measured:

- **No score.** `StructuredGenerationResult` carries `Model`, `Json`,
  `DoneReason`, token counts and a duration — no probability, no logprob — and
  `OllamaStructuredGenerationRuntime` never requests one. A binary yes/no gives
  nothing to threshold. Criterion 3, controllable per-label precision, has no
  mechanism on the current runtime: the only lever is prompt wording, which
  EVAL-5 just demonstrated to be an unreliable way to obtain a structural
  property.

### Candidate B — specialized encoder classifier

Real state of the spike, read from its artifacts, not from its plan.

The trained model is **`FacebookAI/xlm-roberta-base`**, not DeBERTa. The choice
is deliberate and documented: the LoC sample spans Arabic, Urdu, Russian,
Chinese, Tibetan, Ge'ez and more, so an English-only encoder was rejected. The
"DeBERTa" name survives from the spike's title only.

One baseline run exists, `runs/xlm-roberta-base-title-notes/baseline-report.json`,
matching the handoff's Section 31 parameters exactly. The handoff's claim that
no training has run is stale relative to that file.

Dataset `dataset-v2`: 1 094 catalogue rows, 1 067 unique instances, 998 usable
work groups, split 695 / 151 / 152 at work-group level, zero exact cross-split
collisions and zero fuzzy candidates at 0.88. Ten of the fourteen profile labels
are targets; four are excluded as too sparse in the whole catalogue — Pastoral
letters and charges 6 records, Creeds 9, Catechisms 29, Hagiographies 50.

Features are **title plus typed notes only**. Subjects are excluded entirely,
because the spike measured target leakage in non-Genre subject fields at 98-100%
for most classes. That exclusion is methodologically sound and structural, and
it is the single best decision in the spike.

First baseline, threshold 0.5, zero tuning — no class weights, no threshold
calibration, no augmentation, no hyperparameter search:

| Metric | Value |
|---|---:|
| Micro precision | 81.5% |
| Micro recall | 24.3% |
| Micro F1 | 37.4% |
| Macro F1 | 20.7% |
| Exact-set accuracy | 15.1% |
| Latency median / p95 | 3.99 ms / 21.20 ms |

Per label, the shape matters far more than the aggregate:

| Label | Support | Predicted positive | Precision | Recall |
|---|---:|---:|---:|---:|
| Devotional literature | 28 | 25 | 80.0% | 71.4% |
| Prayers | 27 | 24 | 79.2% | 70.4% |
| Sacred works | 15 | 2 | 100% | 13.3% |
| Academic theses | 15 | 3 | 100% | 20.0% |
| Apologetic writings | 15 | **0** | — | 0% |
| Textbooks | 15 | **0** | — | 0% |
| Sermons | 16 | **0** | — | 0% |
| Biographies | 19 | **0** | — | 0% |
| Essays | 15 | **0** | — | 0% |
| Commentaries | 16 | **0** | — | 0% |

Six of ten labels never fire. Of 54 positive predictions, 49 fall on the two
highest-support classes. This is the signature of an under-trained imbalanced
multi-label head read at a fixed 0.5 threshold, on 695 training examples — not
evidence that the approach fails. It is also not evidence that it works.

Demonstrated strengths:

- Structural independence by construction: independent sigmoid heads, no
  candidate list, no ordering, no competition. The EVAL-5 failure mode cannot
  occur.
- Native per-label thresholds and calibratable scores. Criterion 3 is
  structurally available, which is exactly what candidate A lacks.
- Deterministic: identical input gives identical output. Every stability
  question EVAL-2 through EVAL-5 had to measure disappears.
- Latency 4 ms median against 3 to 28 seconds. Three orders of magnitude.
- Leakage is prevented structurally in dataset construction rather than by
  instruction, which is the same lesson EVAL-5 teaches about independence.

Demonstrated weaknesses:

- Current quality is poor and covers 10 of 14 labels. `Apologetic writings`, the
  label that matters most to this product, currently predicts nothing.
- The four sparse labels have no encoder path at all and would need a different
  mechanism whatever happens.
- Produces no reviewer-facing justification. The existing suggestion contract
  requires one, so an encoder-only path breaks it and would need either a
  contract change or a second component to write the justification.
- Introduces a genuinely new operational surface: ROCm/Docker runtime, training
  reproducibility, model and threshold versioning, drift. Against the
  simplicity-first principle this is the real cost, and it is not paid once.

## 3. What still blocks the choice

**There is no shared benchmark, and this is the blocker.** The two candidates
have never been measured on the same thing:

| | Qwen path (EVAL-1..5) | Encoder spike |
|---|---|---|
| Items | 12-21 curated Apologia cases | 152 LoC catalogue work groups |
| Evidence | title, contributors, description, body sections | title + typed notes |
| Labels | 14 | 10 |
| Positives per label | roughly 0-5 | 15-28 |

No number from one side can be compared with a number from the other. The
EVAL-5 case set cannot support per-label precision claims at all: with 21 cases
and 14 labels, most labels have a handful of positives or none. The LoC test set
has ten times the per-label support but the wrong feature distribution.

**Whether candidate A can produce a score is unverified.** The current runtime
returns none and requests none. Whether the installed Ollama build can return
token logprobs at all is a one-call question that this review deliberately did
not ask. If the answer is no, criterion 3 is structurally unavailable to A and
the comparison is largely settled on that alone.

**The about-versus-is discrimination has never been measured for either
candidate.** `adversarial-study-of-sermons` is the one case that isolates it. It
collapsed from 5/5 to 1/5 between framings A and B in EVAL-5A — but it was not
in the EVAL-5D oracle case set, and it was not in the EVAL-4A probe plan either.
Condition C has therefore never been asked the question, and the encoder,
trained on title plus notes, is a priori at risk of learning keyword presence,
which is precisely the failure mode. **The criterion that most distinguishes a
form classifier from a topic classifier is the one with no measurement on
either side.**

**The encoder's thresholds are uncalibrated.** Six zero-prediction labels at a
fixed 0.5 threshold is very likely an artifact. Until per-label thresholds are
fitted on the validation split, the baseline understates the approach by an
unknown margin, and comparing against it would be unfair.

**Transfer is unmeasured.** A model trained on LoC title plus notes has never
been shown to work on MRA evidence, which is a different distribution with body
excerpts. Nothing in the spike addresses this.

## 4. Decision criteria, fixed before any further experiment

Both candidates are to be judged on these, per label, on identical inputs. Exact-set
accuracy is reported but is explicitly not a gate: it hides per-label behaviour,
which is what this decision turns on.

1. **Structural independence.** No candidate ordering may change any per-label
   decision. Verified by permutation where an ordering exists, and by
   construction where none does.
2. **Form versus topic discrimination.** False-positive rate on a dedicated
   about-versus-is subset — works *about* a form against works *of* that form.
   This subset does not yet exist and must be built.
3. **Controllable per-label precision.** For each label, can a threshold be set
   that reaches a stated precision floor while keeping a stated recall? A
   candidate with no score cannot satisfy this and fails the criterion outright,
   whatever its raw accuracy.
4. **Stability.** Variation across repeated runs on identical input. Zero is
   expected from the encoder; the LLM path must report an interval, not a point.
5. **Cost.** Median and p95 latency, inference count per work, output tokens.
6. **Operability.** Reviewer-facing justification available; coverage of all 14
   profile labels including the 4 sparse ones; versioning of model, thresholds,
   policy snapshot and dataset; failure mode when the component is unavailable.

Fail-closed behaviour and the deterministic policy validator are preserved in
every scenario and are not negotiable trade-offs.

## 5. Minimal experiment that would actually decide

The comparison is currently impossible, so the minimal experiment is the one
that makes it possible. It requires **no new training and no new dataset**, and
it is deliberately unglamorous.

**Run both candidates over `dataset-v2`'s 152-record test split, on the same
`text_title_notes` input, over the same 10 labels.**

- *A-side*: the per-label binary framing already implemented as EVAL condition C,
  pointed at the LoC records instead of the Apologia cases. 152 × 10 = 1 520
  inferences; at the 1 574 ms median measured in EVAL-5C, roughly 40 minutes.
  No GPU beyond what Ollama already uses, and it must not run while the spike
  holds the card.
- *B-side*: fit per-label thresholds on the existing 151-record validation split
  using the **already trained** checkpoint, then re-score the test split. No
  retraining. Inference over 303 records is CPU-feasible.

Report per label: precision, recall, F1, predicted positives, and for the
encoder the threshold chosen and its precision/recall curve. This is the first
number-for-number comparison the project would possess.

Two additions make it decisive rather than merely comparable:

- **The score question, first and separately.** One call establishing whether
  the installed Ollama returns logprobs. It costs minutes and can eliminate
  criterion 3 for candidate A before anything else is spent.
- **The about-versus-is subset.** Construct roughly 30-50 pairs — a work *of* a
  form against a work *about* it — from LoC records, labelled from the
  catalogue's own Genre/Form assignment, and run both candidates on it. This is
  the only criterion where neither candidate has any evidence, and it is the one
  that separates a genre/form classifier from a topic classifier.

What this experiment cannot settle, and should not be asked to: transfer to real
MRA evidence with body excerpts. `text_title_notes` is a common denominator
chosen because both candidates can consume it, not because it is the production
input. Whichever candidate wins, transfer remains a separate measurement.

## 6. Preliminary reading, offered as a position and not a decision

Candidate B is the only one that can satisfy criterion 3 at all, and it
satisfies criteria 1 and 4 by construction rather than by measurement — which is
precisely the lesson EVAL-5 taught. Candidate A is far cheaper to adopt, reuses
everything, and already produces the justification the contract needs.

The likely shape of the answer is neither exclusively: an encoder for confident
per-label decisions with calibrated thresholds, the LLM retained for the four
sparse labels, for ambiguous cases near the threshold, and for writing the
reviewer-facing justification. The spike's own Section 3 anticipated this
cascade. But that is a hypothesis, and adopting it before the shared benchmark
exists would repeat the mistake EVAL-5 was run to avoid: choosing an
architecture from a plausible story rather than from a measurement.

No production change is proposed.
