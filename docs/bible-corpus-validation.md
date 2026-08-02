# Bible corpus validation

The benchmark validates the canonical ingestion source (USFM) against a simplified VPL oracle before any PostgreSQL persistence is designed.

## Validation boundary

- `SIL.Machine 3.9.1` tokenizes and parses USFM.
- ApologiaStudio rejects unknown markers, unmatched end markers, and unclosed character, note, or sidebar markers.
- Only visible scripture text is compared. Introductions, editorial section titles, notes, cross-references, figures, and sidebars are excluded.
- USFM descriptive titles (`\\d`, commonly Hebrew Psalm superscriptions) and speaker labels (`\\sp`) are preserved separately with their positions. For comparison only, descriptive titles and inline speaker labels are flattened back into verse text; standalone speaker headings remain excluded, matching eBible's VPL export.
- USFM word attributes are retained in the normalized in-memory representation and Strong attributes are counted.
- Comparison normalization is deliberately narrow: Unicode Form C plus collapsed whitespace. Punctuation and letter casing are not changed.
- No data is written to PostgreSQL.

## Run both approved corpora

```bash
cd ~/RiderProjects/ApologiaStudio

./scripts/validate-bible-corpora.sh \
  /absolute/path/to/lsg1910/usfm \
  /absolute/path/to/lsg1910.vpl \
  /absolute/path/to/web/usfm \
  /absolute/path/to/web.vpl
```

JSON reports are written under `artifacts/bible-corpus-validation/` and are ignored by Git.

The process exits with:

- `0` when references, normalized text, expected book count, and requested Strong checks match;
- `1` when the inputs parse but validation differences exist;
- `2` for invalid arguments, malformed input, unknown USFM, or an execution failure.

## Run one corpus

```bash
dotnet run --project tools/ApologiaStudio.BibleCorpusBench -- \
  --name LSG1910 \
  --usfm /absolute/path/to/lsg1910/usfm \
  --vpl /absolute/path/to/lsg1910.vpl \
  --expected-books 66 \
  --require-strong \
  --report artifacts/bible-corpus-validation/lsg1910.json
```

Both `--usfm` and `--vpl` accept either one file or a directory. Directories are scanned recursively. VPL lines must use this form:

```text
GEN 1:1 In the beginning...
```
