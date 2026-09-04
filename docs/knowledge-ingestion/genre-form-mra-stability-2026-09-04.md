# Genre/Form MRA stability study — EVAL-2 — 2026-09-04

Status: evaluation only. Nothing was optimized; the accepted baseline
[`da8a450`](genre-form-mra-baseline-2026-09-04.md) is unchanged and remains the
reference for every later comparison.

Purpose: tell a systematic semantic failure apart from stochastic instability
by repeating identical inferences ten times per case.

Reproduce with:

```bash
OLLAMA_EVALUATIONS_ENABLED=true \
OLLAMA_GENRE_FORM_MODEL=qwen3.8:27b \
GENRE_FORM_STABILITY_REPORT=/tmp/stability.md \
dotnet test tests/ApologiaStudio.Evaluations --filter Genre_form_stability
```

## Result

- Run: 2026-09-04 06:07:51Z
- Model: `qwen3.8:27b`
- Prompt: `genre-form-classification/1`
- Policy: `apologia-genre-form-profile-v1`
- Repetitions per case: 10
- Nothing was changed from the accepted baseline: same model, prompt, evidence, policy and inference options.

## Per case

| Case | Expected | Exact | Insufficient | Failures | Latency ms (min/med/max) |
|---|---|---:|---:|---:|---|
| de-decretis | Apologetic writings | 0/10 | 0 | 1 | 3503 / 7633 / 16854 |
| contra-gentes | Apologetic writings | 10/10 | 0 | 0 | 1615 / 1682 / 2142 |
| papacy-essay | Apologetic writings, Essays | 0/10 | 0 | 0 | 1611 / 1685 / 2279 |
| adversarial-translated-sacred-work | Sacred works | 9/10 | 0 | 0 | 1287 / 1620 / 5616 |
| habermas-resurrection | Apologetic writings | 10/10 | 0 | 0 | 1686 / 1751 / 2421 |
| septuagint | Sacred works | 10/10 | 0 | 0 | 1460 / 1676 / 2214 |
| bauckham-eyewitnesses | ∅ | 9/10 | 0 | 0 | 2032 / 4955 / 6420 |
| adversarial-prompt-injection | ∅ | 10/10 | 6 | 0 | 2914 / 3743 / 4351 |

## Suggested-term frequency

### de-decretis

No term was ever proposed.

### contra-gentes

- Apologetic writings: 10/10 (expected)

### papacy-essay

- Essays: 10/10 (expected)

### adversarial-translated-sacred-work

- Sacred works: 9/10 (expected)

### habermas-resurrection

- Apologetic writings: 10/10 (expected)

### septuagint

- Sacred works: 10/10 (expected)

### bauckham-eyewitnesses

- Academic theses: 1/10 (unexpected)

### adversarial-prompt-injection

No term was ever proposed.

## Tokens

- Output tokens: min 52, median 83, max 517


## What the repetition settles

**Two of the three baseline misses are systematic, not bad luck.**

`de-decretis` proposed nothing in **10 runs out of 10**. Its sibling
`contra-gentes` succeeded **10 out of 10**. This is not sampling noise: the
model reads the two records differently and consistently.

`papacy-essay` is a systematic *partial* failure. It proposed `Essays` in
10 runs out of 10 and `Apologetic writings` in none. The multiple-independent-
genres case fails on one specific term, reproducibly.

**One baseline miss was stochastic.** `adversarial-translated-sacred-work`
succeeded 9 times out of 10; the baseline run happened to catch the failing
one.

## A correction to the baseline headline

The baseline reported precision 100% and FP=0. Over 80 repeated inferences,
one false positive appeared: `Academic theses` proposed once out of ten on
`bauckham-eyewitnesses`, where nothing was expected.

The fail-closed behaviour therefore holds strongly but not absolutely. The
baseline's FP=0 is a single-sample result, not a property. Any later
experiment must be compared against this qualified reading, and false
positives watched at least as closely as recall.

## Latency correlates with hesitation

`de-decretis` is roughly four times slower than `contra-gentes` — median 7.6 s
against 1.7 s, up to 16.9 s — and never succeeds. The model spends
substantially more computation on the case it then declines. Output tokens
range from 52 to 517 across the study.

One inference failure occurred, on `de-decretis`, out of 80 runs.

## Evidence comparison: de-decretis and contra-gentes

The two payloads are structurally identical — same contributor, same language,
no edition statement, descriptions of comparable length (129 and 143
characters). A deterministic test pins this.

They differ in **what is being defended**:

```text
contra-gentes  a defence of the Christian faith against pagan objections
de-decretis    a defence of the decisions of the Council of Nicaea,
               justifying the terms adopted by the council
```

`contra-gentes` is a defence of the faith against outsiders, which is the
prototypical apologetic frame. `de-decretis` is a defence of conciliar
decisions inside a doctrinal controversy.

So a material input difference does explain the divergence, and the model's
reading is defensible: it appears to treat an internal doctrinal defence as
something other than apologetics. Whether Apologia agrees is a **policy
question about what `Apologetic writings` covers**, not a model defect to be
tuned away. GF-RULE-11 warns against forcing anachronistic categories onto
historical works, and this is exactly that boundary.

No chain-of-thought was requested, inspected or stored.

## Architectural finding — model selection

`qwen3:8b` and `qwen3.8:27b` are not interchangeable for this task. The
smaller model declares insufficient evidence on 16 of 19 cases and proposes
nothing at all; the larger one reaches 16/19 exact. The gap is a capability
gap, not a prompt defect.

Metadata Review will therefore eventually need its **own model selection**,
configurable independently from conversational agent profiles: a model that
serves conversation acceptably may be unusable for closed-vocabulary
classification. No UI is added for this now; it is recorded as a product and
architecture finding.
