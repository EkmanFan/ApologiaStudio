# Bible corpus provenance

The versioned manifests under `corpora/manifests` identify the exact source
archives approved for the first ApologiaStudio Bible imports.

## Approved snapshots

| Manifest | Edition | Canonical source | Validation oracle | Status |
|---|---|---|---|---|
| `fraLSG-2026-08-02.json` | Louis Segond 1910 | eBible `fraLSG_usfm.zip` | eBible `fraLSG_vpl.zip` | Approved |
| `eng-web-2026-08-02.json` | World English Bible Classic | eBible `engwebp_usfm.zip` | eBible `engwebp_vpl.zip` | Approved |

The manifest identity uses the approved ApologiaStudio corpus codes `fraLSG`
and `eng-web`. The WEB archive names use eBible's upstream distribution ID
`engwebp`; these are two names for the same approved WEB Classic source, not two
different editions.

The capture timestamp is the time at which the downloaded archives were
inspected and hashed. It is not presented as an upstream publication date.

## Integrity boundary

Each manifest records:

- the stable ApologiaStudio edition and canon codes;
- the upstream distribution ID and source page;
- the exact USFM and VPL archive URLs, byte lengths, and SHA-256 hashes;
- the parser and normalization policy used for validation;
- expected book, verse, and Strong-attribute counts;
- the successful USFM/VPL parity result.

The archive SHA-256 values identify the downloaded files. The deterministic
source-tree SHA-256 and import fingerprint defined by ADR 0001 are separate,
derived values. The production importer will compute them after extracting and
filtering the canonical USFM files.

For WEB, the upstream USFM archive contains the non-canonical `FRT` (front
matter) and `GLO` (glossary) documents in addition to the 66 canonical books.
They are explicitly excluded from the first import. The LSG archive already
contains exactly the selected 66 books.

## Validation status

Both snapshots are approved for development and import:

- source archive integrity recorded;
- 66-book Protestant canon selected;
- USFM/VPL reference parity validated;
- normalized visible-text parity validated;
- Strong attributes retained and counted;
- independent editorial collation deferred and non-blocking.

The JSON schema describes the manifest contract. Any later upstream archive or
material importer/normalizer change requires a new manifest; an approved
manifest is never edited to point at different source bytes.
