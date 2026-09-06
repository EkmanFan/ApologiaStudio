# Field-suggestion capability — integration audit — 2026-09-06

Audit only. No code was modified, no migration created, no ONNX export, no
inference, nothing touched in the Spike Encoder. Every type and path below was
read from the repository.

## 1. Executive summary

The seam is better than expected. MRA-1 to MRA-4 already separate *inference*,
*validation*, *advisory persistence* and *authoritative save* into distinct
components, and the reviewer workflow never treats a suggestion as metadata. A
second provider can be introduced without a second workflow.

Three findings shape the SDD.

**The classifier abstraction is the wrong seam; the panel is the right one.**
`IGenreFormClassifier` returns `GenreFormClassificationValidation` — a
Genre/Form-shaped, already-validated result. `IFieldSuggestionProvider` cannot
sit behind it without either leaking Genre/Form into the generic port or
re-validating twice. The real seam is the two call sites in
`EditorialReviewPanel.razor.cs`: `RunGenreFormAnalysisAsync` (line 601) and
`BuildEvidence` (line 803).

**The encoder input contract cannot be satisfied today.** Of the five canonical
sections, only `[TITLE]` and `[DESCRIPTION]` have a source, and `[TITLE]` is
currently derived from the **original file name**, not from document content.
`[SUBTITLE]`, `[TOC]` and `[STRUCTURE]` have no field anywhere in the Apologia
model. The raw DPEngine result is retained and addressable, so the data may be
recoverable — but by deterministic derivation, not by reading an existing field.
This is the largest gap and the first thing the SDD must decide.

**MRA-4 persistence is Genre/Form-shaped at the database level.**
`metadata_review_suggestions.term_id` is a non-nullable FK to
`genre_form_authority_terms`. A suggestion for any other field cannot be stored
in that table as it stands. Everything else — analysis identity, status,
supersession, reviewer outcome, latency — is already field-agnostic and carries
a `field` discriminator.

Verdict: **READY FOR SDD**, with the input-source question as the one decision
that must be made inside the SDD rather than deferred.

## 2. Current MRA flow

```text
reviewer clicks "Suggest genre/form"
  EditorialReviewPanel.RunGenreFormAnalysisAsync()          Web
    scope: IGenreFormClassifier                              Application port
      StructuredGenreFormClassifier.ClassifyAsync()          Application
        IGenreFormPolicyProvider.GetActivePolicyAsync()      Infrastructure
        IStructuredGenerationRuntime.GenerateAsync()         AgentRuntime
        GenreFormClassificationValidator.Validate()          Application  (MRA-1)
    -> GenreFormClassificationValidation
       valid   -> _suggestions, RecordAnalysisAsync()        own DI scope
       invalid -> _analysisError, RecordFailureAsync()       own DI scope

reviewer clicks Accept / Reject                             purely in-memory
  AcceptSuggestions()  -> _form.GenreFormAuthorityUris = suggested
  RejectSuggestions()  -> clears the panel

reviewer clicks Save
  ExecuteAsync(Save) -> DocumentManagerEditorialDraftReviewCommand
    ... authoritative save commits ...
  then RecordReviewerOutcomeAsync()                          after the save
    MetadataReviewOutcomeCalculator.Determine(suggested, confirmed)
    IMetadataReviewAnalysisStore.RecordReviewerOutcomeAsync()
```

Two properties already hold and must be preserved. Advisory history is written
in **its own DI scope and transaction**, and a failure there is swallowed so the
reviewer's editorial work is never affected. And the reviewer outcome is
recorded **after** the authoritative save commits, never before.

## 3. Exact integration seam

| Concern | Type | File |
|---|---|---|
| Triggers the analysis | `EditorialReviewPanel.RunGenreFormAnalysisAsync()` | `src/ApologiaStudio.Web/Components/DocumentManager/EditorialReviewPanel.razor.cs:601` |
| Builds the evidence | `EditorialReviewPanel.BuildEvidence()` | same file, line 803 |
| Inference port | `IGenreFormClassifier` | `src/ApologiaStudio.Application/Knowledge/MetadataReview/StructuredGenreFormClassifier.cs:8` |
| Current implementation | `StructuredGenreFormClassifier` | same file, line 22 |
| Generic model transport | `IStructuredGenerationRuntime` | `src/ApologiaStudio.Application/Abstractions/AiRuntime/IStructuredGenerationRuntime.cs:33` |
| Machine output validated | `GenreFormClassificationValidator.Validate()` | `src/ApologiaStudio.Application/Knowledge/MetadataReview/GenreFormClassificationValidator.cs:20` |
| Vocabulary and hierarchy rules | `GenreFormSelectionRules` | `src/ApologiaStudio.Application/Knowledge/GenreForms/GenreFormPolicy.cs` |
| Advisory persistence port | `IMetadataReviewAnalysisStore` | `src/ApologiaStudio.Application/Knowledge/MetadataReview/MetadataReviewHistory.cs:88` |
| Advisory persistence impl | `PostgreSqlMetadataReviewAnalysisStore` | `src/ApologiaStudio.Infrastructure/Knowledge/MetadataReview/PostgreSqlMetadataReviewAnalysisStore.cs` |
| Accept / Reject | `AcceptSuggestions()` / `RejectSuggestions()` | panel, lines 781 and 793 |
| Reviewer outcome | `MetadataReviewOutcomeCalculator.Determine()` | `MetadataReviewHistory.cs:135` |
| Authoritative save | `DocumentManagerEditorialDraftReviewCommand` | `src/ApologiaStudio.Application/Knowledge/DocumentProcessing/DocumentManagerEditorialReview.cs` |
| Reviewer's confirmed selection | `EditorialForm.GenreFormAuthorityUris` | panel, inner class |

### Reusable as-is

`GenreFormClassificationValidator`, `GenreFormSelectionRules`,
`IMetadataReviewAnalysisStore` and its Postgres implementation,
`MetadataReviewOutcomeCalculator`, the whole authoritative save path, and the
assistant panel markup including Accept / Reject.

### Not reusable as the generic port

`IGenreFormClassifier` returns `GenreFormClassificationValidation`, which
carries `GenreFormSuggestion` — authority URI, identifier, preferred label,
justification, evidence. That is a Genre/Form contract, and it is
*post-validation*. `IFieldSuggestionProvider` must return raw scored
suggestions and let the field's own rules validate them, so it belongs beside
`IGenreFormClassifier`, not behind it.

The natural shape is: the panel calls `IFieldSuggestionProvider`, an adapter
turns a `FieldSuggestionResult` for `genre_form` into a
`RawGenreFormClassification`, and the **existing** validator decides. Nothing in
MRA-1's fail-closed discipline is bypassed, and `MetadataReviewOptions`,
hierarchy and cardinality guards keep applying.

## 4. Input-source mapping

The encoder contract is `[TITLE] [SUBTITLE] [DESCRIPTION] [TOC] [STRUCTURE]`.
What Apologia can supply at review time:

| Section | Real source today | Type / property | Exists? | Assessment |
|---|---|---|---|---|
| `[TITLE]` | `DocumentManagerEditorialDraft.Title`, editable by the reviewer | `string Title` | Yes | **Present but semantically wrong at creation.** `DocumentManagerEditorialDraftFactory.ProposeTitle()` derives it from `assembly.OriginalFileName` — filename minus extension, truncated to 1000 chars. Until a reviewer edits it, it is a filename, not a title. |
| `[SUBTITLE]` | — | none | **No** | No property on the draft, the entity, or `MetadataReviewEvidence`. Would have to be derived from the result payload or left empty. |
| `[DESCRIPTION]` | `DocumentManagerEditorialDraft.Description` | `string? Description` | Yes | Set to `null` at creation (`DocumentManagerEditorialDraftFactory`, `Description: null`). Populated only if a reviewer types it. |
| `[TOC]` | — | none | **No** | Not on the draft. Candidate source below. |
| `[STRUCTURE]` | — | none | **No** | Not on the draft. Candidate source below. |

### What the draft does carry

`DocumentManagerEditorialDraftPart(ProcessingUnitId, Ordinal, ResultReference,
DocumentManagerResultScope)`. `DocumentManagerResultScope` carries `Kind`,
page range, `ScopeTitle`, and content-unit indices — so a coarse outline of
**processing units** exists, with per-unit titles. That is closer to a
segmentation map than to a table of contents, but it is real structural data
already in Apologia's own tables.

### The recoverable source

The full DPEngine result is retained:
`DocumentManagerResultInboxEntity.Payload` (`byte[]`), keyed by
`ResultReference`, with `SchemaVersion`, `MediaType` and `Sha256`, in the
knowledge database. `ConsumeDocumentManagerResultHandler` already parses it once
with `JsonDocument.Parse` to verify the advertised schema version, so the shape
is readable from Apologia.

Nothing currently reads that payload for content. Extracting headings for
`[TOC]` and `[STRUCTURE]` would be a **new deterministic derivation** over a
retained artifact — not a new field, not a change to DPEngine, and not a
migration.

### Consequence for the SDD

Three options exist and the SDD must choose one; they are not equivalent.

1. **Title plus description only.** Feed `[TITLE]` and `[DESCRIPTION]`, leave
   the other three empty. Matches the training corpus more closely than it
   looks: in `gate-d-split-v1`, 886 records carry `[TITLE]`, 409 carry
   `[DESCRIPTION]`, and **none** carry `[SUBTITLE]`, `[TOC]` or `[STRUCTURE]`.
   The encoder has never seen those three sections populated.
2. **Add a deterministic derivation** from the result payload for `[TOC]` and
   `[STRUCTURE]`. More faithful to the contract, but the encoder was trained
   without them, so it would be evidence the model has never encountered.
3. **Use `ScopeTitle` from the draft parts** as a cheap outline. Available with
   no payload parsing, but it describes processing units rather than the work.

Option 1 is the only one that matches how the model was actually trained. The
filename-derived title is the real risk: the encoder was trained on catalogue
titles, and a filename such as `the-new-testament-...-9780197754023_compress`
is not one.

## 5. Persistence and provenance assessment

Against the fifteen elements requested:

| Element | Status | Evidence |
|---|---|---|
| Apologia field concerned | **Already supported** | `MetadataReviewAnalysisEntity.Field`, `MetadataReviewAnalysis.Field`, constant `GenreFormField = "genre_form"`; the store API already takes `field` |
| Classifier / cascade version | **Representable** | `PolicyVersion` is a free string; a cascade version fits, though the name would then be a misnomer |
| Primary model identity / version | **Already supported** | `ModelProvider`, `ModelName` |
| Primary threshold | **Requires schema extension** | no column |
| Primary scores | **Requires schema extension** | `MetadataReviewSuggestionEntity` has `TermId`, `Disposition`, `Justification` — no score |
| Fallback invoked | **Requires schema extension** | no column |
| Fallback model identity / version | **Requires schema extension** | `ModelProvider`/`ModelName` are single-valued |
| Fallback scores | **Requires schema extension** | as above |
| Fallback threshold | **Requires schema extension** | no column |
| Input-contract version | **Representable** | `PromptVersion` is free text and semantically close; reusing it for an encoder input contract is a naming compromise |
| Label-set version | **Representable** | via `PolicyVersion`, same compromise |
| Latency | **Already supported** | `DurationMilliseconds` |
| Timestamp | **Already supported** | `RequestedAtUtc`, `CompletedAtUtc`, `ReviewedAtUtc` |
| Final status | **Already supported** | `Status` (`Valid` / `Failed`), plus `FailureReason` with the `CHECK ((status='failed') = (failure_reason IS NOT NULL))` invariant |
| Final suggestions | **Partially** | stored, but see below |

### The one hard constraint

`metadata_review_suggestions.term_id` is `uuid NOT NULL` with a foreign key to
`genre_form_authority_terms(id)`, and a unique index on
`(analysis_id, term_id)`. Suggestions are therefore **structurally Genre/Form**.
A suggestion for a future field cannot be stored without either a nullable
generic value column beside `term_id`, or a per-field suggestion table.

For the first slice this does not block anything: Genre/Form is the field being
integrated, and its suggestions fit the existing table exactly. It becomes a
blocker the moment a second field arrives, which the SDD should acknowledge
without solving it prematurely.

### Provenance gaps that do matter now

A cascade has two models, two thresholds and a fallback decision. The current
schema records one model and no threshold. Without an extension, the history
could not answer "which stage produced this suggestion, at what score, against
what threshold" — which is exactly the question a reviewer audit would ask, and
exactly what EVAL-6 measured. Minimum viable extension: `fallback_invoked bool`,
`primary_threshold`/`fallback_threshold`, a second model identity, and a `score`
on the suggestion row.

## 6. Optional-capability composition

Registrations live in exactly two places:

- `src/ApologiaStudio.Infrastructure/DependencyInjection.cs` — line 90
  `IGenreFormPolicyProvider`, line 93 `IMetadataReviewAnalysisStore`, line 96
  `IGenreFormClassificationValidator`.
- `src/ApologiaStudio.Web/DependencyInjection.cs` — line 301
  `IStructuredGenerationRuntime`, line 304 `IGenreFormClassifier`, alongside the
  Ollama runtime and telemetry.

`IGenreFormClassifier` is registered `AddScoped` in the Web composition root.
That is the exact place a suggestion provider belongs.

### Startup tolerance — already correct

Startup does **not** block on any AI dependency. `OllamaHttpClientFactory` and
`OllamaStructuredGenerationRuntime` are registered as plain scoped services;
settings are read at call time through `IAiRuntimeSettingsStore`, and
`OllamaStructuredGenerationRuntime` throws only when `GenerateAsync` is invoked
with uninitialised settings. There is no `ValidateOnStart`, no health gate, and
no hosted service that pings a model. Apologia already boots with Ollama down.

### Where a Null Object fits naturally

`RunGenreFormAnalysisAsync` resolves its dependency **inside a scope, at click
time**, and already wraps the whole call in `try/catch` mapping any exception to
"The assistant is unavailable." A provider that reports itself unavailable fits
that shape with no `if (enabled)` anywhere in the panel:

- register `IFieldSuggestionProvider` **always**, never conditionally;
- when the capability is configured, register the encoder-backed implementation;
- when it is not, register one returning `Status = Unavailable` per requested
  field.

The panel then branches on `Status`, which it must do regardless — `Unavailable`
is a first-class status in the requested contract, not an absence. This is why
`NoSuggestion != Unavailable != Failed` matters at the seam and not only in the
type: the panel already distinguishes "the model proposed nothing" from "the
assistant is unavailable" in its user-facing text, and today it can only tell
them apart by catching an exception.

One consequence worth stating: the Suggest button is currently rendered
unconditionally (`EditorialReviewPanel.razor:198`, disabled only while analysing
or read-only). With an explicit `Unavailable` status the button can be disabled
honestly instead of failing on click.

## 7. Runtime-abstraction placement

Existing abstractions:

```text
src/ApologiaStudio.Application/Abstractions/AiRuntime/
    IAiRuntimeSettingsStore.cs
    IStructuredGenerationRuntime.cs
src/ApologiaStudio.Application/Abstractions/Agents/
    IAgentRuntime.cs
    IAgentSettingsStore.cs
```

### Do not reuse `IStructuredGenerationRuntime`

Its contract is `StructuredGenerationRequest(Purpose, SystemPrompt, UserPrompt,
ResponseSchema, ModelOverride, MaximumOutputTokens)` returning
`StructuredGenerationResult(Model, Json, DoneReason, PromptTokenCount,
OutputTokenCount, DurationMilliseconds)`.

Every field is generation-shaped. An encoder has no system prompt, no user
prompt, no JSON schema, no token counts and no `DoneReason`; it has text in and
per-label scores out. Forcing it in would mean five meaningless parameters and a
JSON round-trip invented purely to satisfy the interface. The instruction not to
force the fit is correct on the evidence.

### Recommended placement

**A new abstraction, in `Application/Abstractions/` beside the existing two, and
its implementation in `Infrastructure`.**

- The **port** goes in Application because Application already owns
  `IStructuredGenerationRuntime` and the layer tests forbid Application
  depending outward. A sibling folder — `Abstractions/Encoders/` — keeps the two
  runtimes visibly distinct rather than implying a common family they do not
  share.
- The **implementation** goes in `Infrastructure`, not `AgentRuntime`.
  `AgentRuntime` is defined by the architecture tests as LLM orchestration and
  must not depend on `Infrastructure`; an ONNX or embedded encoder session is
  neither an agent nor an orchestration, and placing it there would stretch that
  project's stated purpose. `Infrastructure` already hosts every other
  technical adapter, including the Genre/Form authority store.

The generic port must know nothing of Genre/Form: text in, label scores out,
with a model identity. Field-specific inference rules — thresholds, the cascade
decision, mapping scores to authority URIs — belong in a Genre/Form-specific
component in `Application/Knowledge/GenreForms`, exactly where
`GenreFormSelectionRules` already lives. That keeps the multi-head and
multi-model future open without a plugin framework, a service locator or a
registry: a second field is a second field-specific component over the same
runtime port.

## 8. Conflicts and stale assumptions

**No conflict with an acquired decision.** The four items below are genuine
inconsistencies to record, not decisions to reopen.

1. **`MetadataReviewAnalysis.PromptVersion` and `ModelProvider` presume an LLM.**
   An encoder has no prompt, and `"ollama"` as provider is wrong for a
   CPU-resident ONNX cascade. The columns are free text so nothing breaks, but
   the names will mislead a future reader. Naming debt, worth a note in the SDD.

2. **`StructuredGenreFormClassifier.PromptVersion = "genre-form-classification/1"`
   and `Purpose = "genre-form-classification"`** are hardcoded and recorded in
   history. If the encoder replaces the LLM on this field, past analyses stay
   attributed to a prompt version that no longer describes anything running.
   Append-only history handles this correctly — old rows describe what actually
   ran — provided the new path writes a distinguishable identity.

3. **The 27-versus-24 label taxonomy has no representation in Apologia.**
   `GenreFormProfile.SelectableLabels` holds **14** LCGFT labels
   (`Apologetic writings`, `Textbooks`, …), and the Genre/Form authority tables
   are keyed on LCGFT authority URIs. The Spike Encoder's 24 machine labels
   (`apologetic_writing`, `handbook_manual`, `scholarly_article`, …) are a
   different vocabulary in a different naming convention, with eleven concepts
   absent from the current profile and `Hagiographies` absent from the encoder
   scope. **There is no mapping table anywhere in the repository.** Producing one
   is a prerequisite for turning encoder output into `AuthorityUri` values that
   `GenreFormSelectionRules.Resolve` will accept — otherwise every suggestion
   fails validation as `UnknownAuthorityTerm`. This is the second real gap after
   the input contract, and the SDD must own it.

4. **`docs/knowledge-ingestion/genre-form-classifier-architecture-review-2026-09-04.md`
   is superseded on its central question.** It presents ENCODER versus
   LLM-PER-LABEL as open and its E2 protocol targets the retired `dataset-v2`.
   EVAL-6 C1 settled it. The document already carries in-place corrections and
   should get one more pointing at the C1 results, per the project's rule that
   these documents are authoritative rather than historical.

No conflict was found with ADR 0002 or ADR 0004, and none with the frozen
decisions listed in the request.

## 9. Smallest coherent integration slice

The smallest slice that is genuinely finished, in dependency order:

1. **The label mapping.** A deterministic, reviewable mapping from the 24
   machine labels to Genre/Form authority identities, failing closed on anything
   unmapped. Without it nothing downstream can pass validation. Product data, so
   it belongs beside `GenreFormProfile`.
2. **The port.** `IFieldSuggestionProvider` and its result types in Application,
   plus a `Unavailable`-returning implementation registered by default. Ships on
   its own: the panel gains an honest disabled state and the capability becomes
   optional before any model exists.
3. **The encoder runtime port and one adapter.** Text in, label scores out, in
   Infrastructure, with the cascade rule and thresholds in a Genre/Form-specific
   component.
4. **The panel switch.** `RunGenreFormAnalysisAsync` calls
   `IFieldSuggestionProvider` instead of `IGenreFormClassifier`, maps the result
   into `RawGenreFormClassification`, and passes it through the **existing**
   validator. Accept / Reject, the outcome calculator and the authoritative save
   are untouched.
5. **The provenance extension.** Cascade columns and a suggestion score.

Steps 1 and 2 are independently shippable and carry no model dependency. Step 5
can follow step 4 if the first integration records a degraded provenance
knowingly, but not silently.

## 10. Open questions

Only two are real; everything else is answered above.

**Which input the encoder actually receives.** The training corpus contains no
`[SUBTITLE]`, `[TOC]` or `[STRUCTURE]` at all, and the draft title is a filename
until a reviewer edits it. Sending a filename to a model trained on catalogue
titles is a measurable risk, not a theoretical one. Options: suggest only after
the reviewer has corrected the title; derive a better title from the result
payload; or accept the degradation and measure it. This is a product decision.

**Whether Genre/Form suggestions remain in the existing suggestion table.**
Keeping `term_id` works perfectly for this field and blocks the next one. The
choice is to extend now for a second field that does not exist yet, or to extend
when it arrives. On the project's simplicity-first principle the second is
defensible, provided the SDD says so explicitly rather than leaving it
undiscovered.
