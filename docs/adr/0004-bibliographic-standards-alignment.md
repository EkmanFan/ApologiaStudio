# ADR 0004: Bibliographic alignment on IFLA LRM and RDA

- Status: Accepted
- Date: 2026-09-03
- Decision owners: ApologiaStudio
- Supersedes in part: [ADR 0002](0002-knowledge-store-and-rag-architecture.md) §3, §4 and §8

## Context

Apologia Studio enriches the portable results received from
DocumentProcessingEngine before relational persistence, knowledge-unit
transformation and vector indexing. Part of that enrichment is bibliographic,
and must not be reinvented as an Apologia-specific ontology.

ADR 0002 established a "pragmatic WEMI-inspired model" and a `SourceKind`
classification dimension. Both were designed before the relevant standards were
examined. Comparative research since then shows that:

- **IFLA LRM** is the reference conceptual model for the bibliographic domain,
  and the conceptual successor to FRBR. It defines `Work`, `Expression`,
  `Manifestation`, `Item`, `Agent` (`Person`, `Collective Agent`), `Nomen`,
  `Place` and `Time-span`.
- LRM defines `Work.Category` (LRM-E2-A1) as *"a type to which the work
  belongs"*, explicitly able to span several categorisation axes — termination
  intention, creative domain, form/genre.
- LRM deliberately **excludes** genre/form authority data from the model
  itself. It answers *where does this information belong*, not *which values
  should we use*.
- The IFLA genre/form working group frames those terms as describing what a
  resource **is**, rather than what it **is about**.

`SourceKind` as defined in ADR 0002 mixed several axes: documentary nature,
genre/form, primary/secondary relation to a question, milieu of production and
argumentative purpose. That ambiguity would produce unstable classifications
and assertions that are hard to govern.

### What the current model already does correctly

Verified against the Knowledge Store schema on 2026-09-03, the persistence
model is already closer to LRM than ADR 0002's text suggests:

- `knowledge_contributions` targets a WEMI level explicitly (`work_id`,
  `expression_id`, `manifestation_id`) and carries `role`, `ordinal` and
  `attribution_status` (`explicit`, `established`, `traditional`, `probable`,
  `possible`, `disputed`). Roles are constrained to 13 values.
- `knowledge_expressions.language_code` places language at the Expression
  level, as LRM requires.
- `knowledge_contributors.contributor_type` distinguishes person from
  collective agent, matching LRM `Agent`.

The gap is therefore not primarily in the Knowledge model. It is in the
editorial record, which flattens this into a single
`PrimaryContributorName` / `PrimaryContributorRole` pair with four role values
and no WEMI target, and in the controlled-vocabulary tables, which carry no
authority, external code, URI, deprecation state or version.

## Decision

Apologia adopts a bibliographic application profile aligned on existing
standards.

1. **IFLA LRM is the reference conceptual model** for Apologia's bibliographic
   domain.
2. **RDA is the principal descriptive and operational reference** for
   normalized elements and vocabularies.
3. The internal model keeps the **Work / Expression / Manifestation / Item**
   distinction where a business need justifies it. `Item` may remain
   unmaterialized until a copy-specific need appears.
4. **`SourceKind` is replaced or decomposed.** It must no longer aggregate
   documentary genre, evidential role, perspective, methodology or
   argumentative purpose.
5. **Genre/form relies on Library of Congress Genre/Form Terms (LCGFT)**,
   through a closed subset approved by Apologia.
6. **Contributor roles rely on MARC Relator terms/codes** (or their appropriate
   RDA alignment) rather than a proprietary enum.
7. Standard vocabularies are exposed as **closed lists**: no ad-hoc creation or
   modification from the interface.
8. **Authority codes and URIs are retained alongside stable internal
   identifiers**, to permit evolution, deprecation, localization and
   interoperability.
9. **BIBFRAME is not the internal canonical model.** It may later serve as a
   Linked Data interoperability target.
10. Dimensions genuinely specific to Apologia — `Perspective`,
    `MethodologicalFramework`, `EpistemicFramework`, `EvidenceRole`,
    `AttributionStatus` and passage-level assertions — stay separate from the
    standard bibliographic model and are decided in their own records.

### Modelling rules

| Dimension | Rule |
|---|---|
| Genre/Form | Describes what the work **is**, not what it is **about**. Use LCGFT where an adequate term exists. |
| Primary / Secondary | Not an intrinsic `SourceKind`. It depends on the question, claim or analytical context, and is handled outside bibliographic genre. |
| Language | Belongs primarily at the Expression level, per LRM. |
| Translation | Not modelled as an intrinsic genre of the Work; it is a realization or derivation between Work and Expression as the case requires. |
| Content / Media / Carrier | Use RDA vocabularies when Apologia needs these dimensions. |
| Contributor role | Use a normalized role (for example a MARC Relator code) bound to the appropriate W/E/M/I target. |
| Unclassified | Absence of classification is permitted. It must not be replaced artificially by an `Unknown` or `Other` business value without an explicit decision. |

### Controlled application profile

Apologia does not load external vocabularies wholesale into user-facing lists.
Each standard vocabulary is reduced to an explicitly approved subset.

A controlled-vocabulary entry must be able to retain at least:

- a stable internal identifier;
- the source authority or scheme;
- the external code or notation where one exists;
- the authority URI/IRI where one exists;
- the preferred label and its useful translations;
- an active/deprecated status;
- a display order specific to the Apologia profile.

Values are versioned with the product or its reference dataset. Users have no
CRUD allowing them to extend vocabularies from the editorial record.

### Separation from the Apologia domain

Bibliographic alignment must not absorb the product's intellectual
classifications. The target model keeps a distinct layer of Apologia
assertions, applicable to the Work, the Expression or later the Segment:

- `Perspective`
- `MethodologicalFramework`
- `EpistemicFramework`
- `EvidenceRole`
- `AttributionStatus`
- justification, provenance, assertion author/reviewer and review status

An analytical assertion must never be confused with a standard bibliographic
property, nor silently rewritten when a vocabulary evolves.

## Rejected alternatives

**A fully proprietary vocabulary.** Rejected: reinvents mature bibliographic
concepts, increases semantic debt and reduces interoperability.

**A closed C# enum per vocabulary.** Rejected as the canonical mechanism:
insufficient to retain authority URIs, deprecation, localization and evolution
without destructive migrations.

**Importing LCGFT or RDA wholesale.** Rejected: far too large a surface for
Apologia and poor UX. The product uses a closed, targeted profile.

**BIBFRAME as the internal model.** Not retained: BIBFRAME 2.0 structures
mainly Work / Instance / Item, whereas Apologia benefits from the explicit
Work / Expression / Manifestation distinction of LRM and RDA, notably for
languages, translations and editions.

**Keeping `SourceKind` as a universal field.** Rejected: it mixed several
classification axes and risked conflating genre, analytical function and
perspective.

## Consequences

Positive:

- less proprietary ontology;
- better bibliographic and WEMI consistency;
- easier future interoperability;
- closed but evolvable vocabularies, with no free-text entry;
- clearer separation between bibliographic facts and Apologia interpretations;
- reduced risk of anachronism, or of an analytical classification being
  presented as factual.

Costs and constraints:

- an Apologia application profile must be maintained explicitly;
- existing bibliographic fields may need mapping and migration;
- term-by-term validation of the useful LCGFT subset and relator codes;
- Apologia-specific vocabularies still have to be designed and governed
  separately.

Concretely, on the current schema:

- `knowledge_source_kinds` (`id`, `code`, `label`, `description`) lacks
  authority, external code, URI, deprecation state, display order and version.
- `knowledge_contributions.role` is a 13-value proprietary list to be mapped
  onto relator codes.
- the editorial record's `PrimaryContributorName` / `PrimaryContributorRole`
  pair carries no WEMI target and no attribution status, while the Knowledge
  model already supports both.

## Risks

- Over-modelling LRM and RDA beyond Apologia's real needs.
- Confusing conceptual conformance with an obligation to implement a full
  cataloguing standard.
- Selecting a genre/form subset too early, either too narrow or too broad.
- Using a standard term outside its real scope because its label seems to fit.
- Evolving a closed list without preserving the stable identity and provenance
  of former values.

## Revision conditions

This decision must be reassessed if:

- a real need shows that the chosen WEMI distinction blocks an essential use
  case;
- RDA or LCGFT does not provide a bibliographic concept Apologia requires;
- an institutional integration imposes another standard or a stronger mapping;
- the operational cost of the application profile becomes disproportionate;
- a major change in the reference standards makes the retained mappings
  obsolete.

## Next decisions

1. Build `Apologia Bibliographic Application Profile V1`.
2. Select and validate the LCGFT subset for the biblical, patristic, conciliar,
   Reformed, theological, apologetic, historical and academic corpora.
3. Select the MARC Relator subset needed for contributors and state its
   W/E/M/I level.
4. Define `AttributionStatus` separately.
5. Then handle `Perspective`, `EvidenceRole`, `MethodologicalFramework` and
   `EpistemicFramework` through their own records or governed vocabularies.

## Normative and research references

- IFLA Library Reference Model (LRM): https://repository.ifla.org/handle/20.500.14598/40
- RDA Registry — Guide for developers: https://www.rdaregistry.info/rgGuide/
- RDA Registry — Value vocabularies: https://www.rdaregistry.info/termList/
- Library of Congress — Controlled Vocabularies / LCGFT: https://www.loc.gov/librarians/controlled-vocabularies/
- Library of Congress — MARC Code List for Relators: https://www.loc.gov/marc/relators/
- Library of Congress — BIBFRAME 2.0 Model: https://www.loc.gov/bibframe/docs/bibframe2-model.html

---

Synthetic decision: standardize everything bibliographic before inventing;
limit the standards to an application profile useful to Apologia; reserve
proprietary taxonomies for genuinely Apologia-specific needs.
