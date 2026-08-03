# Bible reader contract

## Scope

UX-04 turns each active Library edition into a deterministic Bible reader. It
uses the approved PostgreSQL corpus through application query handlers. Reading,
navigating, and selecting Bible text never invokes an LLM.

## Routes and navigation

- `/library/{editionCode}` opens the first available book and chapter.
- `/library/{editionCode}/{bookCode}/{chapterNumber}` opens an exact chapter.
- Edition and book codes are matched without regard to case, then rendered with
  their canonical stored values.
- Previous and next navigation crosses book boundaries in canonical order.
- Deep links can be reloaded and shared.

## Reading and selection

- A chapter displays the imported verse label and normalized text exactly as
  stored in the active corpus.
- The first verse click selects one verse. A second click extends the selection
  from the anchor to a continuous range in either direction.
- Selecting the same single verse again clears the selection. A separate clear
  action is always available after selection.
- The normalized reference includes the stored book display name, chapter,
  exact verse labels, and edition display name.
- Clipboard access uses the browser API with a local fallback and reports
  failure without losing the selection.

## Conversation handoff

- `Use in a chat` creates a new empty conversation.
- The browser transmits only edition code, book code, chapter number, and start
  and end verse labels.
- The server resolves those identifiers again against the active approved
  corpus before preparing the question.
- Missing or altered identifiers are rejected; browser-supplied Bible text or
  display names are never trusted.
- The resulting question remains editable and is never sent automatically.

## Failure behavior

The reader distinguishes these states without silently substituting content:

- no active approved corpus;
- unknown or inactive edition;
- book absent from the selected edition;
- chapter absent from the selected book.

Unexpected data-access failures use the corpus-unavailable state. Existing
editions are offered as explicit links when an unknown edition was requested.

## Accessibility and responsive behavior

- Editions, books, chapters, previous and next chapters use native links or
  selects.
- Verse selections use buttons with `aria-pressed`.
- Focus remains visible for keyboard navigation.
- Below 900 pixels, the existing dismissible sidebar and a compact reader layout
  are used.

## Out of scope

- parallel translation comparison;
- full-text search;
- Strong, morphology, and interlinear presentation;
- annotations, highlights, bookmarks, and synchronized reading position;
- commentaries, RAG, and LLM-generated navigation.
