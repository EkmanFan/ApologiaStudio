# LCGFT authority and Genre/Form profile v1

Status: current infrastructure contract. Implements the Genre/Form half of
[ADR 0004](../adr/0004-bibliographic-standards-alignment.md).

This increment delivers infrastructure only. It deliberately does **not**
decide which terms Apologia may assign; that is the separate
`Apologia Genre/Form Profile V1` specification.

## Two separate concepts

```text
authority status      "is this term current according to the authority?"
profile usage status  "how may Apologia use this term?"
```

They never change together. An authority refresh rewrites authority facts and
leaves every Apologia decision untouched.

## Ingestion source

```text
authority       Library of Congress Genre/Form Terms (LCGFT)
linked data     https://id.loc.gov/authorities/genreForms/
bulk dataset    https://id.loc.gov/download/authorities/genreForms.skosrdf.jsonld.gz
representation  lcgft-skosrdf-jsonld-v1
```

LCGFT is the normative authority; SKOS/RDF JSON-LD is only the v1 ingestion
representation. `SkosJsonLdGenreFormDatasetReader` is the sole component aware
of SKOS property names, so a MADS/RDF adapter can be added later without
touching the domain model. The printable annual PDF is documentation and is
never the ingestion format.

The dataset is JSON Lines: one record per line, each carrying an `@graph`.

### Canonical identity

Every top-level record `@id` in the published dump is **relative**
(`/authorities/genreForms/gf…`); only the concept node inside `@graph` carries
the absolute URI. Canonical identity is therefore read from the concept node.
Local term identity is `KnowledgeStableIds.ForAuthority(authorityUri)`, which
makes re-import idempotent without a natural-key lookup.

### Notes

The SKOS dump supplies no `skos:scopeNote`. Notes keep their real semantics
and none is fabricated:

```text
General  ← skos:note
History  ← skos:historyNote
Example  ← skos:example
```

### Hierarchy and association

Only `skos:broader` is persisted. `narrower` is derived by inverting it, so
hierarchy has a single source of truth. Polyhierarchy is expected: in the
snapshot measured on 2026-09-03, 693 of 2 681 terms had more than one broader
term, which is why no single `ParentId` exists.

`skos:related` is preserved separately as a non-hierarchical association,
stored canonically with the lower identifier first so a symmetric pair is
never duplicated in reverse.

### Deprecation

The bulk source carries no status flag. A term withdrawn upstream keeps only
its `cs:ChangeSet` records and loses its `skos:Concept` node, so it simply
stops being published. `AuthorityStatus` is therefore a **derived local
interpretation**, not a source field.

Consequently a fresh import reports zero deprecated terms: the 193 withdrawn
records in the 2026-09-03 snapshot carry no concept to import. Deprecation
becomes visible on a *refresh*, when a previously imported term disappears
from the new snapshot. Such terms are reported for explicit review whenever
they are referenced by a profile entry or a Work assignment, and are never
deleted or silently remapped.

### Snapshot identity

The Library of Congress download exposes no usable version identifier, so none
is invented:

```text
ContentSha256    deterministic identity of the imported payload
SourceUri        official source location
RetrievedAt      acquisition timestamp
ImporterVersion  implementation provenance
```

Two retrievals of identical content share one semantic snapshot even when
`RetrievedAt` differs.

## Running the importer

Synchronization is an explicit maintenance operation. Normal Apologia runtime
reads only the local snapshot and never depends on `id.loc.gov`.

```bash
eval "$(direnv export bash)"
export APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION="…"

# fetch the official dump
dotnet run --project tools/ApologiaStudio.GenreFormImporter

# or replay a pinned local copy
dotnet run --project tools/ApologiaStudio.GenreFormImporter -- --file genreForms.skosrdf.jsonld.gz
```

Re-importing identical content reports `snapshot already imported` and applies
no semantic change.

### Verified run, 2026-09-03

```text
content sha256    4f3eaf5625af355adad2c801b8e34f30114600bbe78431ba5e57353b8f489744
terms             2681
variants          6121
notes             1680
broader relations 3401
related relations 111 canonical pairs (212 directed references)
profile entries   0
```

Zero profile entries is the intended state: no term is selectable before the
approved profile list exists.

## Failure behaviour

The importer fails closed rather than guessing. A malformed record, a concept
without a preferred label, an ambiguous identity, a non-absolute URI or a
broader reference absent from the snapshot aborts the import inside its
transaction, leaving no partial snapshot.

A dangling `skos:related` reference is skipped rather than fatal, because an
association carries no hierarchy or assignment semantics.

## Apologia Genre/Form Profile V1

`GenreFormProfile` declares the thirteen approved terms **by preferred label**.
Authority identifiers are never hard-coded: the seeder resolves each label
against the imported snapshot and fails closed when a label is absent or
ambiguous, so the profile cannot drift from the authority it claims to follow.

The required structural ancestors are **derived** from the imported hierarchy
rather than declared. Applying the profile over the 2026-09-03 snapshot yields:

```text
selectable       13
structural only   6   Business correspondence, Creative nonfiction,
                      Discursive works, Informational works,
                      Instructional and educational works, Religious materials
```

Those six are exactly the direct ancestors of an approved term that are not
themselves approved. `Biographies` is both approved and an ancestor of
`Hagiographies`; it stays selectable, and the redundancy is prevented at
assignment time rather than by demoting it.

Apply the profile as an explicit maintenance operation:

```bash
dotnet run --project tools/ApologiaStudio.GenreFormImporter -- --apply-profile
```

Re-applying reports `profile already current` and changes nothing.

### Assignment rules enforced in code

```text
only selectable terms may be assigned
a duplicate Work/term pair is refused
a term and one of its ancestors never coexist on the same Work
broader terms are never persisted implicitly
zero genre/form assignments is valid
independent genres may coexist
```

Variants are never assignable: `Homilies` resolves to `Sermons` and
`Confessions of faith` to `Creeds`, both verified against the real authority.

### Multi-authority scoping

An import rebuilds relations, variants and notes **only for the authority
being imported**, and reviews only that authority's profile entries. Importing
one authority never disturbs another's facts or editorial decisions.

## What this increment does not do

```text
no production Genre/Form picker
no term marked selectable by default
no migration of knowledge_source_kinds or primary_source
no automatic classification of existing Works
no change to Perspective, EvidenceRole or the framework vocabularies
```

`knowledge_work_genre_forms` exists so the approved profile can be wired later
without another structural migration. It is empty and receives nothing
automatically.

## Schema

```text
genre_form_authority_snapshots   acquired dataset identity
genre_form_authority_terms       one row per authority concept
genre_form_authority_variants    alternate labels, not independently assignable
genre_form_authority_notes       general / history / example
genre_form_broader_relations     canonical hierarchy, polyhierarchical
genre_form_related_relations     canonical symmetric association
genre_form_profile_entries       Apologia usage decisions, product-owned
knowledge_work_genre_forms       Work ↔ term assignment, unique per pair
```

Migration `AddGenreFormAuthority` is purely additive: eight tables and thirteen
indexes, no alteration of any existing table.
