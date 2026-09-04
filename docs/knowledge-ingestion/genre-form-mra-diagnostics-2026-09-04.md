# Genre/Form MRA diagnostics — EVAL-4 — 2026-09-04

Status: diagnosis only. No production prompt, policy, model, options, evidence
selection or UI was changed. The experimental framings live in the evaluation
project.

Ground truth unchanged: `De Decretis → Apologetic writings`, `papacy essay →
Apologetic writings + Essays`.

## Methodological note

A ten-run suite is **regression and smoke evidence**. It separates "always"
from "never" and nothing finer. A proportion between those extremes requires a
targeted larger sample and a stated interval. No number below should be read
as a production-quality estimate, and none is derived from a single run.

Evidence provenance is kept distinct throughout: the De Decretis enriched
payload is drawn from the imported text of the work, the papacy-essay payload
is curated because the work is not in the corpus.

---

# De Decretis variance — EVAL-4

- Model: `qwen3.8:27b`
- Repetitions per payload: 50
- Targeted larger sample. A ten-run suite is smoke evidence and cannot characterise a rate between the extremes.

| Payload | Provenance | Selected | Rate | 95% interval | Latency med ms |
|---|---|---:|---:|---|---:|
| de-decretis | curated | 10/50 | 20% | [11%, 33%] | 5545 |
| de-decretis-enriched | source-supported | 5/50 | 10% | [4%, 21%] | 7599 |

Fifty repetitions per payload replace the earlier ten-run readings. The
selection rate is **20%** for the original payload and **10%** for the enriched
one, with overlapping intervals: the enrichment did not measurably change the
rate, and certainly did not improve it.

This retires the EVAL-2 description of the case as systematic. It is a
low-rate, high-variance selection under the joint framing.

---

# Independent-label framing — EVAL-4A

- Model: `qwen3.8:27b`
- Repetitions per question: 10
- Same policy rules, asked one term at a time instead of as a set.

| Case | Provenance | Term | Applies | Failures |
|---|---|---|---:|---:|
| papacy-essay-enriched | curated | Apologetic writings | 3/10 | 0 |
| papacy-essay-enriched | curated | Essays | 8/10 | 0 |
| papacy-essay-enriched | curated | Textbooks | 0/10 | 0 |
| papacy-essay-enriched | curated | Academic theses | 0/10 | 0 |
| de-decretis | curated | Apologetic writings | 10/10 | 0 |
| de-decretis-enriched | source-supported | Apologetic writings | 9/10 | 0 |
| contra-gentes | curated | Apologetic writings | 10/10 | 0 |
| contra-gentes | curated | Textbooks | 0/10 | 0 |
| bauckham-eyewitnesses | curated | Apologetic writings | 0/10 | 0 |
| bauckham-eyewitnesses | curated | Academic theses | 0/10 | 0 |

This is the decisive result.

Asked whether `Apologetic writings` applies to **De Decretis**, the model
answers yes **10 times out of 10** on the original payload and 9 out of 10 on
the enriched one. Under the joint framing the same model, same evidence and
same rules select that term only 20% of the time.

The controls rule out a lenient framing. `bauckham-eyewitnesses` answers no to
both candidates, 0 out of 10 each; `contra-gentes` answers yes to
`Apologetic writings` and no to `Textbooks`, cleanly. The binary question is
not simply agreeable.

For the papacy essay the picture is weaker but points the same way:
`Apologetic writings` rises from 0/10 under joint framing to 3/10 asked alone,
while `Textbooks` and `Academic theses` stay at 0/10.

---

# Candidate-order bias — EVAL-4B

- Model: `qwen3.8:27b`
- Repetitions per ordering: 10
- The production prompt lists candidates in profile order, so reordering the policy reorders the prompt with no code change.

| Case | Ordering | Exact | Apologetic writings | Essays |
|---|---|---:|---:|---:|
| papacy-essay-enriched | profile order | 0/10 | 0 | 10 |
| papacy-essay-enriched | reversed | 0/10 | 10 | 0 |
| papacy-essay-enriched | shuffled seed 1 | 0/10 | 0 | 10 |
| papacy-essay-enriched | shuffled seed 2 | 0/10 | 10 | 0 |
| contra-gentes | profile order | 10/10 | 10 | 0 |
| contra-gentes | reversed | 10/10 | 10 | 0 |
| contra-gentes | shuffled seed 1 | 10/10 | 10 | 0 |
| contra-gentes | shuffled seed 2 | 10/10 | 10 | 0 |

The papacy essay result is entirely determined by candidate order, and always
returns **exactly one** term. Never both, never none. `contra-gentes`, where
only one term competes, is unaffected: 10/10 under every ordering.

The positions rule out a simple recency law:

```text
ordering          Apologetic  Essays   selected
profile order         4          2     Essays
reversed              9         11     Apologetic writings
shuffled seed 1       1          5     Essays
shuffled seed 2       2          7     Apologetic writings
```

Three orderings out of four select the **earlier-listed** candidate. It is a
strong positional tendency, not a deterministic rule.

---

## Conclusions

**(a) Dominant-single-form / task-framing bias — strongly supported.**

The same model, evidence, rules and options judge `Apologetic writings`
applicable to De Decretis 10 times out of 10 when asked about that term alone,
and select it 10 times out of 50 when asked for a set. On the papacy essay the
joint framing returns exactly one of two applicable terms in every one of 40
runs. The classifier behaves as if choosing a single dominant form rather than
evaluating each label independently.

**(b) Candidate-order bias — supported.**

Reordering the candidate list flips which term is returned, 10/10 one way and
0/10 the other, without touching evidence or rules. The effect appears only
where two terms compete. It is a consequence of (a): once the model commits to
one label, position decides which.

**(c) Model/policy disagreement on De Decretis — not supported; refuted.**

The hypothesis was that the model reads an internal doctrinal defence as
something other than apologetics, and therefore disagrees with the policy.
Asked directly, it agrees 10 times out of 10. The joint framing prevents it
from expressing an agreement it holds. Nothing here argues for revisiting the
Genre/Form policy.

**(d)** Does not apply: (a) and (b) are both supported.

## What this does not establish

The independent framing was probed on four terms and six case-term pairs, not
on the full fourteen-label matrix, and at ten repetitions. It is enough to
show that framing changes behaviour materially; it is not enough to estimate
what a per-label production classifier would score.

The papacy essay evidence is curated. Its 3/10 under independent framing is a
weaker signal than the De Decretis 10/10, which rests on the work's own text.
