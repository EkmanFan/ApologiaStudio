#!/usr/bin/env bash
set -euo pipefail

if (( $# != 4 )); then
  echo "Usage: $0 <lsg-usfm-path> <lsg-vpl-path> <web-usfm-path> <web-vpl-path>" >&2
  exit 2
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/.." && pwd)"
report_directory="${APOLOGIA_CORPUS_REPORT_DIR:-$repository_root/artifacts/bible-corpus-validation}"

mkdir -p "$report_directory"

dotnet run \
  --project "$repository_root/tools/ApologiaStudio.BibleCorpusBench" \
  -- \
  --name LSG1910 \
  --usfm "$1" \
  --vpl "$2" \
  --expected-books 66 \
  --require-strong \
  --report "$report_directory/lsg1910.json"

dotnet run \
  --project "$repository_root/tools/ApologiaStudio.BibleCorpusBench" \
  -- \
  --name WEB \
  --usfm "$3" \
  --vpl "$4" \
  --expected-books 66 \
  --exclude-usfm FRT,GLO \
  --report "$report_directory/web.json"
