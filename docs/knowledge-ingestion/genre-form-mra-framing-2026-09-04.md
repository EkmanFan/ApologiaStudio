# Genre/Form MRA — EVAL-5 — Independent Decision Matrix Framing — 2026-09-04

Status: evaluation only. No production prompt, service, policy, options, model,
persistence or UI was changed. Conditions B and C exist in
`tests/ApologiaStudio.Evaluations` and are never registered in the container.

## Question

EVAL-4 established that the production framing turns a multi-label
classification into a competitive selection of one dominant form: the same
model judged `Apologetic writings` applicable to De Decretis 10 times out of 10
asked alone, and selected it 10 times out of 50 asked for a set.

EVAL-5 tests whether a framing that demands an independent binary decision per
candidate, **while staying inside a single inference**, removes that bias
without multiplying cost by fourteen.

## Conditions

| | Framing | Inferences per work |
|---|---|---:|
| A | joint subset selection — production behaviour | 1 |
| B | independent decision matrix, `applicable=true\|false` per candidate | 1 |
| C | independent per-label calls — EVAL-4 framing, behavioural oracle | 14 |

Held constant across A and B: the case set, the evidence, the candidate list and
its order, the model, and the deterministic policy validation. The user prompt
of B is byte-identical to production. B's system prompt restates the
single-decision rules verbatim and forbids every notion of a best, principal,
dominant or most representative term, and every comparison between candidates.

Two deliberate design choices, stated because they bound what the result proves:

- B's `false` decisions are **not** mapped to `consideredButRejected`. Forcing
  fourteen rejection reasons would change the response burden as well as the
  framing, and the comparison would no longer isolate one variable. Matrix
  coverage is measured separately instead.
- The production rule "never answer true for both a term and a broader term of
  it" is inherently relational and sits in tension with "never compare
  candidates". It was kept, because the production validator enforces it and
  removing it would flatter B on the contract.

C is a behavioural oracle, not a proposed architecture. Its fourteen binary
answers are not subject to the joint-response contract: the hierarchy and
cardinality guards that constrain A and B do not constrain C.

## Sizing and what it can support

Reduced sizing, chosen to settle an architectural gate rather than to produce a
definitive statistical characterisation: 5 repetitions for the framing and order
experiments, 20 for the De Decretis rate, 2 for the fourteen-term oracle.

Following the EVAL-4 methodological distinction: a five- or ten-run suite
separates "always" from "never" and nothing finer. Only the De Decretis rates
below carry a Wilson interval and come from a targeted sample. The oracle table,
at two repetitions, is directional evidence only.

Evidence provenance is preserved throughout: `de-decretis-enriched` is
source-supported, drawn from the imported text of the work; every other payload
is curated.

---

# Joint framing versus decision matrix — EVAL-5A

- Model: `qwen3.8:27b`
- Repetitions per case and condition: 5
- Cases: 12
- Output budget: 800 tokens for A and B, 2400 tokens for B-wide.
- Every condition passes through the same deterministic policy validation, so an invalid response is a contract failure in each and is excluded from accuracy.

## Aggregate

| Condition | Exact | Exact % | Precision | Recall | F1 | Mean labels | Clean negatives | Invalid | Latency med ms | Out tokens med |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| A joint | 37/58 | 64% | 91% | 62% | 74% | 0.57 | 17/20 | 2 | 3134 | 90 |
| B matrix | 34/49 | 69% | 85% | 79% | 82% | 0.82 | 13/15 | 11 | 11997 | 768 |
| B-wide matrix | 41/60 | 68% | 82% | 80% | 81% | 0.82 | 15/20 | 0 | 12554 | 785 |

## Invalid responses by cause

| Condition | Cause | Count |
|---|---|---:|
| A joint | StructuredGenerationException | 2 |
| B matrix | StructuredGenerationException | 11 |
| B-wide matrix | none | 0 |

## Per case

| Case | Provenance | Reference | A exact | A labels | B exact | B labels | B-wide exact | B-wide labels | A invalid | B invalid | B-wide invalid |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| habermas-resurrection | curated | Apologetic writings | 5/5 | 1.00 | 1/5 | 1.67 | 4/5 | 1.20 | 0 | 2 | 0 |
| septuagint | curated | Sacred works | 5/5 | 1.00 | 5/5 | 1.00 | 5/5 | 1.00 | 0 | 0 | 0 |
| contra-gentes | curated | Apologetic writings | 5/5 | 1.00 | 5/5 | 1.00 | 5/5 | 1.00 | 0 | 0 | 0 |
| de-decretis | curated | Apologetic writings | 2/5 | 0.40 | 5/5 | 1.00 | 3/5 | 1.40 | 0 | 0 | 0 |
| de-decretis-enriched | source-supported | Apologetic writings | 0/5 | 0.00 | 4/5 | 1.00 | 4/5 | 1.20 | 2 | 1 | 0 |
| papacy-essay | curated | Apologetic writings, Essays | 0/5 | 1.00 | 0/5 | 1.00 | 0/5 | 1.00 | 0 | 0 | 0 |
| papacy-essay-enriched | curated | Apologetic writings, Essays | 0/5 | 1.00 | 0/5 | 1.00 | 0/5 | 1.00 | 0 | 1 | 0 |
| bauckham-eyewitnesses | curated | ∅ | 4/5 | 0.20 | 4/5 | 0.00 | 5/5 | 0.00 | 0 | 1 | 0 |
| calvin-institutes | curated | ∅ | 3/5 | 0.40 | 3/5 | 0.00 | 4/5 | 0.20 | 0 | 2 | 0 |
| adversarial-study-of-sermons | curated | ∅ | 5/5 | 0.00 | 1/5 | 0.67 | 1/5 | 0.80 | 0 | 2 | 0 |
| adversarial-translated-sacred-work | curated | Sacred works | 3/5 | 0.60 | 1/5 | 1.67 | 5/5 | 1.00 | 0 | 2 | 0 |
| adversarial-prompt-injection | curated | ∅ | 5/5 | 0.00 | 5/5 | 0.00 | 5/5 | 0.00 | 0 | 0 | 0 |

## Per label

| Label | Condition | TP | FP | FN | Precision | Recall |
|---|---|---:|---:|---:|---:|---:|
| Academic theses | A joint | 0 | 1 | 0 | 0% | — |
| Apologetic writings | A joint | 12 | 2 | 16 | 86% | 43% |
| Essays | A joint | 10 | 0 | 0 | 100% | 100% |
| Sacred works | A joint | 8 | 0 | 2 | 100% | 80% |
| Apologetic writings | B matrix | 17 | 0 | 9 | 100% | 65% |
| Essays | B matrix | 9 | 4 | 0 | 69% | 100% |
| Prayers | B matrix | 0 | 2 | 0 | 0% | — |
| Sacred works | B matrix | 8 | 0 | 0 | 100% | 100% |
| Academic theses | B-wide matrix | 0 | 3 | 0 | 0% | — |
| Apologetic writings | B-wide matrix | 20 | 0 | 10 | 100% | 67% |
| Essays | B-wide matrix | 10 | 4 | 0 | 71% | 100% |
| Pastoral letters and charges | B-wide matrix | 0 | 1 | 0 | 0% | — |
| Sacred works | B-wide matrix | 10 | 1 | 0 | 91% | 100% |

## Condition B matrix coverage

Coverage is a property of this framing rather than of the vocabulary: a model that silently omits candidates is not answering the question that was asked.

- Responses: 60
- Covering every candidate exactly once: 60/60
- Median decisions returned: 14
- Responses with an unknown identifier: 0
- Responses with a duplicated candidate: 0
- Mean true decisions: 0.82


---

# Candidate-order sensitivity by framing — EVAL-5B

- Model: `qwen3.8:27b`
- Repetitions per ordering and condition: 5
- Same permutations as EVAL-4B. Reordering the policy reorders the candidate list of every condition identically.
- The matrix condition runs at the widened 2400-token budget, so an ordering effect is never confused with truncation.

| Case | Condition | Ordering | Exact | Apologetic writings | Essays | Both | Invalid |
|---|---|---|---:|---:|---:|---:|---:|
| papacy-essay-enriched | A joint | profile order | 0/5 | 0 | 5 | 0 | 0 |
| papacy-essay-enriched | A joint | reversed | 0/5 | 5 | 0 | 0 | 0 |
| papacy-essay-enriched | A joint | shuffled seed 1 | 0/5 | 0 | 5 | 0 | 0 |
| papacy-essay-enriched | A joint | shuffled seed 2 | 0/5 | 5 | 0 | 0 | 0 |
| papacy-essay-enriched | B-wide matrix | profile order | 0/5 | 0 | 5 | 0 | 0 |
| papacy-essay-enriched | B-wide matrix | reversed | 5/5 | 5 | 5 | 5 | 0 |
| papacy-essay-enriched | B-wide matrix | shuffled seed 1 | 0/5 | 0 | 5 | 0 | 0 |
| papacy-essay-enriched | B-wide matrix | shuffled seed 2 | 5/5 | 5 | 5 | 5 | 0 |
| contra-gentes | A joint | profile order | 5/5 | 5 | 0 | 0 | 0 |
| contra-gentes | A joint | reversed | 5/5 | 5 | 0 | 0 | 0 |
| contra-gentes | A joint | shuffled seed 1 | 5/5 | 5 | 0 | 0 | 0 |
| contra-gentes | A joint | shuffled seed 2 | 5/5 | 5 | 0 | 0 | 0 |
| contra-gentes | B-wide matrix | profile order | 5/5 | 5 | 0 | 0 | 0 |
| contra-gentes | B-wide matrix | reversed | 5/5 | 5 | 0 | 0 | 0 |
| contra-gentes | B-wide matrix | shuffled seed 1 | 5/5 | 5 | 0 | 0 | 0 |
| contra-gentes | B-wide matrix | shuffled seed 2 | 5/5 | 5 | 0 | 0 | 0 |

---

# De Decretis selection rate by framing — EVAL-5C

- Model: `qwen3.8:27b`
- Repetitions per payload and condition: 20
- Targeted larger sample with Wilson 95% intervals. Rates whose intervals overlap are not distinguished by this experiment.
- The matrix condition runs at the widened 2400-token budget.

| Payload | Provenance | Condition | Apologetic writings | Rate | 95% interval | Invalid | Latency med ms |
|---|---|---|---:|---:|---|---:|---:|
| de-decretis | curated | A joint | 1/20 | 5% | [1%, 24%] | 0 | 5345 |
| de-decretis | curated | B-wide matrix | 20/20 | 100% | [84%, 100%] | 0 | 11818 |
| de-decretis | curated | C per-label | 20/20 | 100% | [84%, 100%] | 0 | 1574 |
| de-decretis-enriched | source-supported | A joint | 1/20 | 5% | [1%, 24%] | 1 | 6546 |
| de-decretis-enriched | source-supported | B-wide matrix | 20/20 | 100% | [84%, 100%] | 0 | 12914 |
| de-decretis-enriched | source-supported | C per-label | 20/20 | 100% | [84%, 100%] | 0 | 1854 |

---

# Independent per-label oracle — EVAL-5D

- Model: `qwen3.8:27b`
- Repetitions per case: 2
- Candidates asked per repetition: 14
- Inferences: 196
- C is a behavioural oracle, not a proposed architecture. Its responses are fourteen separate binary answers and are therefore not subject to the joint-response contract; the hierarchy and cardinality guards that constrain A and B do not constrain C.

## Aggregate on the oracle case set

| Condition | Exact | Exact % | Precision | Recall | F1 | Mean labels | Clean negatives | Invalid | Latency med ms | Out tokens med |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| C per-label | 8/14 | 57% | 69% | 75% | 72% | 0.93 | 4/4 | 0 | 27898 | 692 |

Compare against conditions A and B restricted to the same cases in the EVAL-5A report.

## Terms answered true at least once

| Case | Provenance | Term | True | Reference |
|---|---|---|---:|---|
| contra-gentes | curated | Apologetic writings | 2/2 | expected |
| de-decretis | curated | Apologetic writings | 2/2 | expected |
| de-decretis | curated | Sacred works | 2/2 | not expected |
| de-decretis-enriched | source-supported | Apologetic writings | 2/2 | expected |
| papacy-essay-enriched | curated | Essays | 1/2 | expected |
| septuagint | curated | Sacred works | 2/2 | expected |
| septuagint | curated | Devotional literature | 2/2 | not expected |

---

## Findings

### 1. B converges on C where the joint framing was failing

De Decretis is the case EVAL-4 diagnosed, and the convergence is complete.

| Payload | A | B | C |
|---|---:|---:|---:|
| de-decretis | 1/20 (5%) | **20/20 (100%)** | 20/20 (100%) |
| de-decretis-enriched | 1/20 (5%) | **20/20 (100%)** | 20/20 (100%) |

B is indistinguishable from the per-label oracle here, at one inference instead
of fourteen. A's 5% is consistent with the 20% measured over fifty runs in
EVAL-4: the intervals [1%, 24%] and [11%, 33%] overlap.

### 2. Multiple applicable labels: improved, not solved

The papacy essay is the case where two terms genuinely apply.

Under A, across all four orderings and twenty runs, both terms were returned
**zero** times. Under B, both terms were returned in **two of four orderings**,
5/5 within each — ten runs out of twenty.

So B can hold two labels true simultaneously, which A never did. But it does so
in half the candidate orderings, and the aggregate case result in EVAL-5A stayed
at 0/5 because that experiment runs in profile order, one of the two orderings
where B fails.

### 3. Order sensitivity is transformed, not removed

| Ordering | A returns | B returns |
|---|---|---|
| profile order | Essays only | Essays only |
| reversed | Apologetic writings only | **both** |
| shuffled seed 1 | Essays only | Essays only |
| shuffled seed 2 | Apologetic writings only | **both** |

Both framings are fully deterministic within an ordering — 5/5 or 0/5, never
in between — and both flip on reordering alone. A's order effect decides *which*
single label; B's decides *whether the second one appears at all*. When B fails,
it fails toward `Essays` regardless of which term is listed first, so no simple
positional law explains it at four orderings.

`contra-gentes`, where only one term competes, is 5/5 under every ordering and
both framings. The effect requires competition to appear.

**This is the gate criterion B does not meet.**

### 4. Negative controls degrade

| Case (empty reference) | A clean | B-wide clean |
|---|---:|---:|
| bauckham-eyewitnesses | 4/5 | 5/5 |
| calvin-institutes | 3/5 | 4/5 |
| adversarial-study-of-sermons | **5/5** | **1/5** |
| adversarial-prompt-injection | 5/5 | 5/5 |
| total | 17/20 | 15/20 |

Prompt injection is unaffected: 5/5 in every condition. The loss is concentrated
on one trap — a study *about* sermons — where A is perfect and B nearly always
answers true. Asking about each candidate in isolation appears to weaken the
"what the work IS, not what it is ABOUT" rule, exactly where that rule matters.

B also raises false positives on labels A never touched: `Prayers`,
`Pastoral letters and charges`, `Academic theses`, and `Essays` precision falls
from 100% to 71%.

### 5. B does not fit the production output budget

`MaximumOutputTokens` is 800 in the product. Fourteen justified decisions do
not fit.

| Condition | Invalid | Median output tokens |
|---|---:|---:|
| A joint | 2/60 (3%) | 90 |
| B @ 800 | **11/60 (18%)** | 768 |
| B @ 2400 | 0/60 (0%) | 785 |

Truncated JSON fails closed as a contract failure, so nothing incorrect reaches
a reviewer — but nearly one response in five produces no suggestion at all.
Adopting B would require raising the output budget, which raises it for every
other structured-generation caller sharing the settings.

Matrix coverage itself is flawless at the widened budget: 60/60 responses cover
all fourteen candidates exactly once, with no invented identifier and no
duplicate. B answers the question it is asked; it simply does not fit.

### 6. Cost

| Condition | Median latency | Median output tokens | Inferences |
|---|---:|---:|---:|
| A joint | 3 134 ms | 90 | 1 |
| B @ 2400 | 12 554 ms | 785 | 1 |
| C per-label | 27 898 ms | 692 | 14 |

B costs about 4× A's latency and 8.7× its output tokens, and is roughly 2.2×
cheaper than C in wall time. The hypothesis that a single-inference matrix
avoids multiplying cost by fourteen is confirmed.

### 7. The oracle is weaker than assumed

EVAL-4 probed C on six case-term pairs and found it clean. Asked the full
fourteen-term matrix, C over-affirms: `Sacred works` true for De Decretis and
`Devotional literature` true for the Septuagint, neither expected. On the seven
oracle cases, exact-set correctness is C 8/14 (57%) against A 21/35 (60%) and
B-wide 27/35 (77%).

At two repetitions this is directional, not conclusive. But it materially
weakens the assumption that per-label independence is the ceiling B should be
measured against: on this case set B beat its own oracle.

## Gate verdict — NO-GO

The gate required B to (i) approach C significantly on applicable labels,
(ii) remove or strongly reduce order sensitivity, (iii) preserve the negative
controls and fail-closed behaviour, and (iv) remain a single inference.

| Criterion | Result |
|---|---|
| Converges on C for applicable labels | **Met** — completely on De Decretis, partially on the papacy essay |
| Removes or strongly reduces order sensitivity | **Not met** — transformed, not reduced; still a deterministic flip |
| Preserves negative controls / fail-closed | **Not met** — 17/20 → 15/20, and the about-versus-is trap collapses 5/5 → 1/5 |
| Single inference | **Met** |
| Invalid rate | 18% at the production budget, 0% at 2400 |
| Cost versus A | 4× latency, 8.7× output tokens |

Two of four criteria fail. **B is not a candidate for the production framing.**

Per the standing instruction, no further prompt tuning was attempted and none
should be: the second and third failures are not phrasing defects. B was told
explicitly and at length not to compare candidates, and it still lets candidate
order decide whether a second applicable label appears. Instruction is not
achieving independence.

## What this establishes for the next decision

The positive result is real and should not be lost: a single-inference matrix
closed a 5%-to-100% gap on De Decretis and produced two simultaneous labels
where the production framing never did once in twenty runs. The multi-label
deficit is a framing artefact, not a limit of the model's judgement.

But independence obtained by instruction remains partial and order-dependent.
That is a substantially stronger argument than EVAL-4 alone for a classifier
whose independence is **structural** rather than instructed — genuinely separate
calls, or the dedicated encoder/DeBERTa classifier under study in the spike.

Finding 7 sharpens the choice rather than settling it: naive per-label calls are
not automatically better, since C over-affirmed on two of seven cases and scored
below B on exact-set. Whatever comes next needs its own per-label precision
discipline, not just independence.

Stopping for review. No production change is proposed.
