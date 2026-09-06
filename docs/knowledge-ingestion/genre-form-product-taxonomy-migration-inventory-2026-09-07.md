# Genre/Form product taxonomy — GF-TAX-1 inventory and migration proof — 2026-09-07

Inventory only. No schema was mutated, no migration created, no seed run, no
production code changed, no Ollama workload. Every count below was read from the
development knowledge database, not inferred from tests or code.

## 1. Executive summary

**The migration is far cheaper than the SDD assumed, and the reason is a
measurement rather than an argument: every table that references a Genre/Form
term is empty.**

| Table | Rows |
|---|---:|
| `knowledge_work_genre_forms` | **0** |
| `document_manager_editorial_draft_genre_forms` | **0** |
| `metadata_review_suggestions` | **0** |
| `metadata_review_analyses` | **0** |
| `genre_form_profile_entries` | 22 (14 selectable + 8 structural) |
| `genre_form_authority_terms` | 2 681 (the imported LCGFT catalogue) |
| `knowledge_works` | 2 |

Consequently the two cases the SDD singled out as needing a human decision —
`Pastoral letters and charges` and `Hagiographies` — have **zero assignments,
zero suggestions and zero review history**. There is nothing to widen, nothing
to lose, and no product decision to take before migrating.

What remains is a pure model change: the canonical identity of a Genre/Form
moves from an LCGFT authority term to an Apologia product term, and three
foreign keys move with it.

## 2. Current-state model

Genre/Form identity today **is** LCGFT authority identity. There is no product
term type anywhere in the codebase.

```text
GenreFormProfile.SelectableLabels        14 English LCGFT preferred labels
        │  resolved at seed time by label
        ▼
genre_form_authority_terms               2 681 imported LCGFT concepts
        │  id (uuid)
        ├── genre_form_profile_entries   term_id + usage_status + display_order
        ├── knowledge_work_genre_forms   work_id + term_id      ← authoritative
        ├── document_manager_editorial_draft_genre_forms
        │                                draft_id + term_id     ← pre-publication
        └── metadata_review_suggestions  analysis_id + term_id  ← advisory
```

Three properties of this model matter for the migration:

**Identity is a label.** `PostgreSqlGenreFormProfileSeeder` resolves each of the
14 approved terms by matching `PreferredLabel` against the imported catalogue,
failing closed on absence or ambiguity. The product's vocabulary is therefore
defined by English LCGFT display strings, which is exactly what the SDD
forbids going forward.

**The policy snapshot carries authority shape.** `GenreFormPolicyTerm` exposes
`AuthorityUri`, `AuthorityIdentifier`, `PreferredLabel`, `Usage` and `Ancestors`.
`GenreFormSelectionRules.Resolve` accepts a URI or an identifier — never a
label — and `FindRedundantHierarchy` uses the LCGFT broader-relation closure.

**Three foreign keys point at the authority table.** All three are
`uuid NOT NULL` referencing `genre_form_authority_terms(id)`.

## 3. Dependency inventory

Classified as requested: **A** product identity coupling, **B** external
authority catalogue usage, **C** review/history reference, **D** migration-only
concern, **E** safe to keep unchanged.

### A — product identity coupling (must change)

| Type / file | Coupling |
|---|---|
| `GenreFormProfile` | The product vocabulary itself: 14 English LCGFT labels + `Version = "apologia-genre-form-profile-v1"` |
| `GenreFormPolicy.cs` — `GenreFormPolicySnapshot`, `GenreFormPolicyTerm`, `GenreFormSelectionRules` | Product selection rules expressed over authority URIs and the LCGFT hierarchy |
| `PostgreSqlGenreFormProfileSeeder` | Resolves the product vocabulary by LCGFT label; computes the transitive ancestor closure |
| `KnowledgeStoreGenreFormPolicyProvider` | Builds the active policy from authority tables |
| `PostgreSqlGenreFormAssignmentStore` | Authoritative Work classification keyed on authority term id |
| `knowledge_work_genre_forms` | FK → `genre_form_authority_terms` |
| `document_manager_editorial_draft_genre_forms` | FK → `genre_form_authority_terms` |
| `DocumentManagerEditorialDraft.GenreForms`, `…ReviewCommand.GenreFormAuthorityUris` | Reviewer's confirmed selection carried as authority URIs |
| `EditorialReviewPanel.razor` / `.razor.cs` | UI selection list and `EditorialForm.GenreFormAuthorityUris` |

### B — external authority catalogue usage (keep, re-purpose)

| Type / file | Note |
|---|---|
| `GenreFormAuthorityContracts`, `PostgreSqlGenreFormAuthorityStore` | Import, snapshots, deprecation, integrity |
| `SkosJsonLdGenreFormDatasetReader` | LCGFT SKOS/JSON-LD reader |
| `genre_form_authority_terms`, `…_notes`, `…_variants`, `…_broader_relations`, `…_related_relations`, `…_snapshots` | The catalogue proper |
| `tools/ApologiaStudio.GenreFormImporter` | Import CLI |

None of this should be deleted. It becomes an alignment source rather than the
product identity.

### C — review/history reference (must change, with care)

| Type / file | Coupling |
|---|---|
| `metadata_review_suggestions.term_id` | FK → `genre_form_authority_terms`, unique `(analysis_id, term_id)` |
| `MetadataReviewSuggestionRecord` | Carries `AuthorityUri`, `AuthorityIdentifier`, `PreferredLabel` |
| `PostgreSqlMetadataReviewAnalysisStore` | Resolves and persists suggestions against authority terms |
| `MetadataReviewOutcomeCalculator` | Compares suggested vs confirmed **sets of authority URIs** |
| `GenreFormClassificationValidator` | Validates model output against the 14-term selectable profile |
| `GenreFormClassificationContracts` — `GenreFormSuggestion`, `GenreFormRejection` | Authority-shaped |

Existing analyses are historical evidence and must stay readable. With zero rows
today, that is currently a design constraint rather than a data problem.

### D — migration-only concern

`20260903205406_AddGenreFormAuthority`, `20260903231126_AddEditorialDraftGenreForms`,
`20260903233434_AddMetadataReviewHistory`, and `KnowledgeDbContextModelSnapshot`.
Past migrations are never rewritten; they are listed because the new migration
must be additive on top of them.

### E — safe to keep unchanged

The MRA runtime seam and its fail-closed discipline: `StructuredGenreFormClassifier`
(prompt/purpose/versioning), `IStructuredGenerationRuntime`, the advisory-history
scoping, and the authoritative save path. They are shaped by *how* a suggestion
is produced and reviewed, not by *what* a term is.

The EVAL-1..6 evaluation corpus is also unchanged: it is historical evidence
about the old profile and must not be retro-fitted.

## 4. Real database counts

Knowledge database, port 54330, read 2026-09-07. Profile version
`apologia-genre-form-profile-v1`.

| LCGFT term | Authority id | Work assignments | Draft selections | MRA suggestions |
|---|---|---:|---:|---:|
| Academic theses | `gf2014026039` | 0 | 0 | 0 |
| Apologetic writings | `gf2015026027` | 0 | 0 | 0 |
| Biographies | `gf2014026049` | 0 | 0 | 0 |
| Catechisms | `gf2015026029` | 0 | 0 | 0 |
| Commentaries | `gf2025026014` | 0 | 0 | 0 |
| Creeds | `gf2015026031` | 0 | 0 | 0 |
| Devotional literature | `gf2015026057` | 0 | 0 | 0 |
| Essays | `gf2014026094` | 0 | 0 | 0 |
| **Hagiographies** | `gf2015026032` | **0** | **0** | **0** |
| **Pastoral letters and charges** | `gf2015026047` | **0** | **0** | **0** |
| Prayers | `gf2015026049` | 0 | 0 | 0 |
| Sacred works | `gf2015026045` | 0 | 0 | 0 |
| Sermons | `gf2015026051` | 0 | 0 | 0 |
| Textbooks | `gf2014026191` | 0 | 0 | 0 |

`metadata_review_analyses` is empty, so no review event references any term.

The eight structural-only profile entries — Business correspondence,
Correspondence, Creative nonfiction, Discursive works, Informational works,
Instructional and educational works, Records (Documents), Religious materials —
exist to support the hierarchy rule and were never selectable.

**Read against the SDD's §7.2:** usage is zero for both non-equivalent terms, so
by its own rule *no semantic migration is required* and no Work needs routing
for explicit review.

## 5. Migration matrix — 14 historical → V1 product

| Historical LCGFT term | Product term | Migration kind | Safe automatic | Notes |
|---|---|---|---|---|
| Apologetic writings | `apologetic_writing` | exact | YES | 0 rows; mapping is data-model only |
| Textbooks | `textbook` | exact | YES | 0 rows |
| Sacred works | `sacred_work` | exact | YES | 0 rows |
| Sermons | `sermon` | exact | YES | 0 rows |
| Catechisms | `catechism` | exact | YES | 0 rows |
| Creeds | `creed` | **close, not exact** | YES (0 rows) | The product definition is **broader**: religious *or* non-religious profession of beliefs. LCGFT `Creeds` is religious. Mapping kind must be `Broader`, not `Exact` |
| Devotional literature | `devotional_literature` | exact | YES | 0 rows |
| Prayers | `prayer` | exact | YES | 0 rows |
| Biographies | `biography` | exact | YES | 0 rows |
| Academic theses | `academic_degree_work` | **close** | YES (0 rows) | The product term covers degree works generally; a thesis is one kind. `Narrower` from the product term's point of view |
| Essays | `essays` | exact | YES | 0 rows |
| Commentaries | `commentary` | exact | YES | 0 rows |
| **Pastoral letters and charges** | *none* | **no product equivalent** | **N/A — no data** | `correspondence` is **not** equivalent: a pastoral charge is a directive act, not correspondence in general. Not mapped |
| **Hagiographies** | *none* | **no product equivalent** | **N/A — no data** | V1 has no term for lives of saints. `biography` is a different concept. Not mapped |

### Non-equivalent cases

Both resolve to nothing to do, but for different reasons that should not be
conflated:

- **Data**: zero assignments, so nothing migrates and no Work is affected.
- **Model**: neither has a V1 product equivalent, so **no authority mapping row
  should be created for them either.** Creating one would assert a crosswalk the
  product does not have.

The correct V1 outcome is that these two LCGFT concepts simply have no Apologia
product term. That is a legitimate state of an alignment table.

### Two mappings that must not be labelled `Exact`

`Creeds → creed` and `Academic theses → academic_degree_work` are the only
label-lookalikes whose semantics genuinely differ. The SDD's §6.2 warns against
`Exact` "merely because labels look similar", and these are precisely that case.
Recording them as `Broader`/`Narrower` costs nothing now and prevents a false
crosswalk being published later.

## 6. External authority catalogue reuse

The existing LCGFT subsystem needs **no structural change** to serve as an
external catalogue. It already stores concepts with stable ids, canonical URIs,
notes, variants, broader and related relations, and snapshot provenance —
scoped by authority since the import fix of `a559dc2`.

What must be added is a mapping table that is not LCGFT-shaped:

```text
genre_form_authority_mappings
  product_term_id      → apologia_genre_form_terms(id)
  authority            'LCGFT' | 'BNF' | …
  external_concept_id  text        -- the authority's own identifier
  external_concept_uri text NULL   -- absent for authorities without URIs
  mapping_kind         'exact' | 'close' | 'broader' | 'narrower' | 'related'
  unique (product_term_id, authority, external_concept_id)
```

Two deliberate choices keep it authority-neutral. `external_concept_id` is text
rather than a foreign key to `genre_form_authority_terms`, so a mapping to BnF
can exist **before** any BnF vocabulary is imported — the SDD's §6.1 requires
exactly that. And `external_concept_uri` is nullable, because not every
authority is URI-addressable.

No BnF import is proposed here.

## 7. Proposed product model

Confirmed against the real code; no adjustment to the SDD's shape was needed.

```text
apologia_genre_form_terms
  id                uuid          -- stable product identity
  code              varchar(64)   -- 'apologetic_writing'; UNIQUE
  preferred_label   varchar(200)
  definition        text
  prediction_mode   varchar(32)   -- 'encoder_predictable' | 'manual_only'
  status            varchar(16)   -- 'active' | 'retired'
  taxonomy_version  varchar(64)   -- 'apologia-genre-form-v1'
  display_order     integer
```

`code` is the identity used by application logic and, later, by the encoder
adapter. Display labels, LCGFT URIs, BnF identifiers and model head indices are
all excluded from identity, per §4.1.

Deterministic ids are recommended over random ones: deriving the uuid from
`taxonomy_version + code` makes the seed idempotent across environments without
a lookup, the same technique `DocumentManagerEditorialDraftFactory.CreateStableId`
already uses in this codebase.

Integrity constraints, all enforceable in the schema: unique `code`; a CHECK on
`prediction_mode` and `status`; and — verified by the seed rather than by a
constraint — exactly 27 active V1 terms, 24 encoder-predictable and 3
manual-only.

## 8. Work ↔ Genre/Form

`knowledge_work_genre_forms(id, work_id, term_id)` keeps its shape; only the
target of `term_id` changes to `apologia_genre_form_terms(id)`.

Every current invariant survives unchanged: zero terms is valid, multiple terms
are valid, the `(work_id, term_id)` pair stays unique, and the reviewer's save
remains authoritative.

One rule needs a decision at GF-TAX-2 rather than a silent carry-over. The
current `FindRedundantHierarchy` forbids persisting both a term and its LCGFT
ancestor. **The product taxonomy is flat** — the 27 terms have no declared
parent-child relations — so that rule has nothing to operate on. Dropping it
without saying so would quietly relax a validation the reviewer currently
relies on; the honest options are to declare the product taxonomy flat and
retire the rule explicitly, or to introduce product-level broader relations. I
recommend the former for V1, stated in the SDD rather than assumed.

Because the table is empty, the migration can simply re-point the foreign key.
No data copy, no dual-write window, no backfill.

## 9. Metadata Review Assistant

`metadata_review_suggestions.term_id` re-points to `apologia_genre_form_terms`.
Per §9, it must not remain a column that sometimes means one and sometimes the
other; with zero rows, a clean re-point is available and a rename to
`product_term_id` is the unambiguous choice.

Consequent changes:

- **`GenreFormClassificationValidator`** validates against the active product
  taxonomy instead of the 14-term profile. Its eleven failure kinds stay
  meaningful; `TermNotSelectable` becomes "not encoder-predictable", and
  `RedundantHierarchy` follows the §8 decision.
- **`GenreFormPolicySnapshot` / `GenreFormPolicyTerm`** carry `Code` and
  `PredictionMode` instead of `AuthorityUri` / `AuthorityIdentifier` / `Usage`.
  `GenreFormSelectionRules.Resolve` resolves a code.
- **`MetadataReviewSuggestionRecord`** carries the product code and label.
- **`MetadataReviewOutcomeCalculator`** compares sets of product codes instead
  of authority URIs. Its Accepted/Modified/Rejected logic is unaffected.
- **`MetadataReviewAnalysis.PolicyVersion`** records the taxonomy version, which
  is what §5 requires for provenance.

Fail-closed is preserved throughout: an unknown product code is rejected, and a
single violation still discards the whole classification.

**One naming debt worth recording now.** `PromptVersion` and
`ModelProvider = "ollama"` on the analysis record presume an LLM. They are free
text so nothing breaks, but an encoder has no prompt. This was already noted in
the P1-01 review; P3-02 is where it should be settled.

## 10. Seed V1 — deferred to GF-TAX-2

Seeding is **not** performed in GF-TAX-1, and the reason is structural rather
than cautious: the 27 product terms cannot be inserted before
`apologia_genre_form_terms` exists, and creating that table is the schema
mutation GF-TAX-1 is explicitly forbidden from making.

The seed's contract is nonetheless fully determined by this inventory: 27 active
terms at `apologia-genre-form-v1`, 24 encoder-predictable, 3 manual-only, unique
codes, deterministic ids, the broad `creed` definition, idempotent re-runs, and
fail-closed on any divergence in count, mode or version.

## 11. Migration sequencing

Ordered so that each step is independently reversible and none is destructive:

1. **GF-TAX-2** — create `apologia_genre_form_terms`; seed the 27 terms. Nothing
   consumes them yet.
2. **GF-TAX-3** — create `genre_form_authority_mappings`; seed the 12 approved
   LCGFT crosswalks, with `Creeds` as `broader` and `Academic theses` as
   `narrower`. `Pastoral letters and charges` and `Hagiographies` get no row.
3. **GF-TAX-4** — re-point the three foreign keys. Empty tables, so no backfill.
4. **GF-TAX-5** — move the policy snapshot, validator, selection rules and MRA
   contracts onto product codes.
5. **GF-TAX-6** — move the editorial draft model and the review UI onto product
   terms.
6. **GF-TAX-7** — retire `GenreFormProfile.SelectableLabels` and the
   label-resolving seeder, once nothing reads them. The LCGFT catalogue itself
   stays.

## 12. Risks

**The empty database is a development fact, not a guarantee.** These counts come
from this machine. Any environment that has been used for real editorial review
would need the same query before its foreign keys are re-pointed. The migration
should therefore verify emptiness rather than assume it, and refuse to run
otherwise.

**`Creeds` is not `creed`.** Reusing the label hides a real broadening. Recording
the mapping kind as `broader` is what keeps that visible.

**The hierarchy rule disappears quietly if nobody decides.** See §8.

**The evaluation corpus is about the old profile.** EVAL-1 through EVAL-6
measured 14 LCGFT terms and, later, the 24 encoder labels. Neither set is the
27-term product taxonomy. Those results stay valid as history and must not be
presented as measurements of the product taxonomy.

## 13. Blockers

**None for GF-TAX-2.**

The two cases the SDD flagged for human decision are resolved by measurement:
`Pastoral letters and charges` and `Hagiographies` have no data, so no product
decision is needed before migrating.

Two decisions are needed *during* GF-TAX-2 to GF-TAX-4, and they are design
choices rather than blockers: whether the product taxonomy is declared flat
(§8), and whether `term_id` is renamed to `product_term_id` (§9). I recommend
yes to both.

## 14. Recommendation for GF-TAX-2

The smallest coherent next slice:

**Create `apologia_genre_form_terms` and seed the 27 V1 terms, consumed by
nothing.**

It is additive, reversible, touches no existing foreign key, changes no
behaviour, and can be verified entirely by counting: 27 active, 24 predictable,
3 manual-only, unique codes, correct `creed` definition, idempotent on re-run.

Deliberately excluded from GF-TAX-2: authority mappings, foreign-key re-pointing,
validator changes, and any UI work. Each is its own slice above.
