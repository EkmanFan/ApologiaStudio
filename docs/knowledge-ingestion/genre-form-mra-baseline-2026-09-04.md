# Genre/Form MRA evaluation baseline — 2026-09-04

Status: baseline measurement. Nothing was tuned to produce it: the model,
prompt, evidence selection and policy are exactly what MRA-2 and MRA-3
shipped.

Reproduce with:

```bash
OLLAMA_EVALUATIONS_ENABLED=true \
OLLAMA_GENRE_FORM_MODEL=qwen3.8:27b \
GENRE_FORM_EVALUATION_REPORT=/tmp/report.md \
dotnet test tests/ApologiaStudio.Evaluations --filter Genre_form_baseline
```

## Result

- Run: 2026-09-03 23:51:45Z
- Model: `qwen3.8:27b`
- Prompt: `genre-form-classification/1`
- Policy: `apologia-genre-form-profile-v1` (14 selectable terms)
- Evidence: curated metadata-level fixtures. No end-to-end real-document run: the editorial workflow supplies metadata only.

## Contract vs semantics

Deterministic policy validation is reported separately from model quality. A response rejected by the validator is a contract failure and is excluded from accuracy.

- Cases: 19
- Scored for semantics: 19
- Contract failures: 0
- Inference failures: 0

## Accuracy

- Exact-set correctness: 16/19 (84%)
- Correct empty classifications: 10/10
- Term-level: TP=7 FP=0 FN=3
- Declared insufficient evidence: 1/19
- Precision: 100%
- Recall: 70%

## Cost

- Latency: median 3846 ms, max 18427 ms
- Prompt tokens: median 535
- Output tokens: median 134

## Cases

| Case | Kind | Expected | Suggested | Insufficient | Result | ms |
|---|---|---|---|---|---|---:|
| habermas-resurrection | reference | Apologetic writings | Apologetic writings |  | exact | 6228 |
| ehrman-new-testament | reference | Textbooks | Textbooks |  | exact | 2416 |
| brenner-logic | reference | Textbooks | Textbooks |  | exact | 2211 |
| bauckham-eyewitnesses | reference | ∅ | ∅ |  | exact | 4814 |
| calvin-institutes | reference | ∅ | ∅ |  | exact | 4594 |
| septuagint | reference | Sacred works | Sacred works |  | exact | 2214 |
| npnf-aggregate | reference | ∅ | ∅ |  | exact | 4774 |
| contra-gentes | reference | Apologetic writings | Apologetic writings |  | exact | 2222 |
| de-decretis | reference | Apologetic writings | ∅ |  | mismatch | 18427 |
| papacy-essay | reference | Apologetic writings, Essays | Essays |  | mismatch | 2444 |
| arcane-thesis | reference | Academic theses | Academic theses |  | exact | 2106 |
| adversarial-history-of-apologetics | adversarial | ∅ | ∅ |  | exact | 2873 |
| adversarial-study-of-sermons | adversarial | ∅ | ∅ |  | exact | 3895 |
| adversarial-psychology-of-prayer | adversarial | ∅ | ∅ |  | exact | 3434 |
| adversarial-required-reading | adversarial | ∅ | ∅ |  | exact | 4235 |
| adversarial-bishop-letter | adversarial | ∅ | ∅ |  | exact | 2732 |
| adversarial-translated-sacred-work | adversarial | Sacred works | ∅ |  | mismatch | 11566 |
| adversarial-no-applicable-term | adversarial | ∅ | ∅ |  | exact | 3846 |
| adversarial-prompt-injection | adversarial | ∅ | ∅ | yes | exact | 5854 |

## Second model, same harness

The same 19 cases on `qwen3:8b`, the smaller model:

```text
exact-set correctness      10/19 (53%)
term-level                 TP=0 FP=0 FN=10
declared insufficient      16/19
latency median             405 ms
```

That model proposes nothing at all. It does not omit terms silently: it
explicitly declares insufficient evidence on 16 of 19 cases, including records
carrying a full description. The contract holds — zero contract failures — but
the classification is useless.

The comparison matters because it separates two explanations. The prompt is not
malformed and the closed vocabulary reaches the model correctly, since the
larger model uses it well. What the smaller model lacks is the judgement to
apply the "substantially characterizes" rule, so it falls back on the escape
hatch the prompt offers.

## Failure patterns

**No false positives at either size.** The closed vocabulary and the fail-closed
validator hold: across 38 runs, not one term outside the profile, not one
hierarchy redundancy, not one contract failure. The prompt-injection case was
correctly refused.

**All errors are omissions.** Every miss is a false negative — the assistant
under-proposes rather than inventing. For an advisory tool reviewed by a human,
that is the safer failure direction, and it is the direction the prompt asks
for.

Three misses on `qwen3.8:27b`:

- `de-decretis` — proposed nothing where `Apologetic writings` was expected,
  after 18.4 s, the slowest run of the set. The sibling case `contra-gentes`,
  with a near-identical description, succeeded in 2.2 s.
- `papacy-essay` — proposed `Essays` but not `Apologetic writings`. The
  multiple-independent-genres case is the one that partially fails.
- `adversarial-translated-sacred-work` — proposed nothing where `Sacred works`
  was expected. The bilingual edition statement appears to have suppressed the
  judgement rather than being ignored as WEMI noise.

**Latency is bimodal.** Median 3.8 s, but the two hardest cases took 18.4 s and
11.6 s. Both are misses. Long generation correlates with hesitation here.

## Qualified by the stability study

The repeated-inference study in
[EVAL-2](genre-form-mra-stability-2026-09-04.md) refines two figures here.
`de-decretis` and `papacy-essay` fail systematically rather than by chance,
while the `adversarial-translated-sacred-work` miss was the unlucky run of
nine successes in ten. And the FP=0 above is a single-sample result: one false
positive appears over eighty repeated inferences.

## Known limitations of this baseline

Evidence is **curated metadata-level fixtures**, not end-to-end real documents.
The editorial workflow currently supplies title, contributor, language, edition
and description only; no decoded DPEngine content reaches the assistant, so no
stable page or element references exist. The fixtures were written to resemble
what a completed editorial record would carry, and are marked `"source":
"curated"` in the case set.

No production `KnowledgeWork` was created for the evaluation.

Recall is computed over 10 expected terms across 19 cases. It is a baseline
signal, not a statistically meaningful measurement.
