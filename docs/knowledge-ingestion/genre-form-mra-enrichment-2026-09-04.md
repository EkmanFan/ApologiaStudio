# Genre/Form MRA evidence enrichment — EVAL-3 — 2026-09-04

Status: evaluation only. Only the evidence payload changed. Model, prompt,
policy, generation options, validator and the ten-repetition method are
identical to [EVAL-2](genre-form-mra-stability-2026-09-04.md), so any delta is
attributable to the payload.

Ground truth is unchanged: `De Decretis → Apologetic writings` and
`papacy essay → Apologetic writings + Essays` remain normative.

Reproduce with:

```bash
OLLAMA_EVALUATIONS_ENABLED=true \
OLLAMA_GENRE_FORM_MODEL=qwen3.8:27b \
GENRE_FORM_ENRICHMENT_REPORT=/tmp/enrichment.md \
dotnet test tests/ApologiaStudio.Evaluations --filter Genre_form_evidence_enrichment
```

## Result

- Run: 2026-09-04 06:40:49Z
- Model: `qwen3.8:27b`
- Prompt: `genre-form-classification/1`
- Policy: `apologia-genre-form-profile-v1`
- Repetitions per case: 10
- Nothing was changed from the accepted baseline: same model, prompt, evidence, policy and inference options.

## Per case

| Case | Expected | Exact | Insufficient | Failures | Latency ms (min/med/max) |
|---|---|---:|---:|---:|---|
| de-decretis | Apologetic writings | 3/10 | 0 | 0 | 1898 / 3808 / 13086 |
| de-decretis-enriched | Apologetic writings | 2/10 | 0 | 0 | 1628 / 5235 / 17006 |
| papacy-essay | Apologetic writings, Essays | 0/10 | 0 | 0 | 1691 / 1702 / 2305 |
| papacy-essay-enriched | Apologetic writings, Essays | 0/10 | 0 | 0 | 1787 / 1985 / 2571 |
| contra-gentes | Apologetic writings | 10/10 | 0 | 0 | 1625 / 1638 / 2237 |
| bauckham-eyewitnesses | ∅ | 10/10 | 0 | 0 | 721 / 4720 / 5555 |

## Suggested-term frequency

### de-decretis

- Apologetic writings: 3/10 (expected)

### de-decretis-enriched

- Apologetic writings: 2/10 (expected)

### papacy-essay

- Essays: 10/10 (expected)

### papacy-essay-enriched

- Essays: 10/10 (expected)

### contra-gentes

- Apologetic writings: 10/10 (expected)

### bauckham-eyewitnesses

No term was ever proposed.

## Tokens

- Output tokens: min 19, median 90, max 714


## Payload sizes

```text
de-decretis              199 characters   (EVAL-2 payload, unchanged)
de-decretis-enriched   1 196 characters   source-supported, 6x larger
papacy-essay             210 characters   (EVAL-2 payload, unchanged)
papacy-essay-enriched    862 characters   curated, 4x larger
```

A test asserts that no enriched payload contains the words apologetic,
apologétique, apologie, essay, essai, academic thesis, textbook or sacred work.
The evidence describes the work; it never names the answer.

## Enrichment did not help

| Case | EVAL-2 | EVAL-3 original | EVAL-3 enriched |
|---|---:|---:|---:|
| de-decretis | 0/10 | 3/10 | 2/10 |
| papacy-essay | 0/10 | 0/10 | 0/10 |
| contra-gentes (control) | 10/10 | 10/10 | — |
| bauckham (control) | 9/10 | 10/10 | — |

Six times more evidence, drawn from the work's own text, moved `de-decretis`
from 3/10 to 2/10 — a difference well inside the variance shown below. Four
times more evidence left the papacy essay unchanged at 0/10.

**No false positives were introduced.** Bauckham proposed nothing in ten runs,
and no unexpected term appeared on any enriched case. The enrichment did not
buy recall at the cost of precision; it simply bought nothing.

## A correction to EVAL-2

EVAL-2 concluded that `de-decretis` fails **systematically**, on the strength
of 0 successes in 10.

EVAL-3 re-ran the **identical payload** under identical conditions and observed
3 successes in 10. That conclusion was therefore wrong, or rather it
over-read a small sample: at a true success rate near 15%, observing 0/10 has
roughly one chance in five.

The honest statement is that `de-decretis` is a **low-success, high-variance
case**, not a case the model never gets. Ten repetitions are enough to
distinguish 10/10 from 0/10, and not enough to characterise anything in
between. Any future claim about this case needs a larger sample.

`papacy-essay` survives the same scrutiny: `Apologetic writings` was missed in
0 of 20 runs across two payloads and two sessions, while `Essays` was proposed
20 times out of 20. That failure is systematic.

## A hypothesis that proved wrong

The EVAL-2 payload for the papacy essay opened with the French word *essai* —
effectively the target label. The obvious hypothesis was that `Essays` was
being read off the payload rather than judged.

The enriched payload removes that word and describes the form structurally
instead. `Essays` is still proposed 10 times out of 10. The detection does not
depend on the word, and the hypothesis is refuted.

What remains unexplained is why the same enriched payload — which states that
the work refutes a claim and answers anticipated objections — never yields
`Apologetic writings`. The model appears to settle on a single dominant form
and stop, rather than to miss the apologetic function.

## Latency

`de-decretis` remains the slowest case in both variants, with maxima of 13.1 s
and 17.0 s against roughly 1.7 s for `contra-gentes`. As instructed, generation
duration is recorded as an exploratory observation only and is not used as a
confidence signal.

## Architectural requirement

Recorded, not implemented: Metadata Review requires its **own configurable
application-AI model selection**, independent of conversational Agent
profiles. EVAL-1 established that `qwen3:8b` is unusable for this task while
`qwen3.8:27b` is serviceable; a deployment cannot be forced to use one model
for both purposes.
