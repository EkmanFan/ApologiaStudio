#!/usr/bin/env python3

from __future__ import annotations

import argparse
import collections
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any


WHITESPACE = re.compile(r"\s+")
NON_ALNUM = re.compile(r"[^0-9A-Za-z]+")


@dataclass(frozen=True)
class DoclingMetrics:
    run_id: str
    page_range: str
    ocr_mode: str
    elapsed_seconds: float | None
    schema_name: str | None
    schema_version: str | None
    text_items: int
    text_characters: int
    pages_with_text: int
    heading_items: int
    label_counts: dict[str, int]
    probe_matches: dict[str, int]
    compact_probe_matches: dict[str, int]
    markdown_characters: int

    def as_dict(self) -> dict[str, Any]:
        return {
            "runId": self.run_id,
            "pageRange": self.page_range,
            "ocrMode": self.ocr_mode,
            "elapsedSeconds": self.elapsed_seconds,
            "schemaName": self.schema_name,
            "schemaVersion": self.schema_version,
            "textItems": self.text_items,
            "textCharacters": self.text_characters,
            "pagesWithText": self.pages_with_text,
            "headingItems": self.heading_items,
            "labelCounts": self.label_counts,
            "probeMatches": self.probe_matches,
            "compactProbeMatches": self.compact_probe_matches,
            "markdownCharacters": self.markdown_characters,
        }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Summarize Docling spike outputs and compare them with "
                    "ApologiaStudio's current PDF diagnostics."
    )
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output-json", required=True, type=Path)
    parser.add_argument("--output-md", required=True, type=Path)
    return parser.parse_args()


def normalize(text: str) -> str:
    return WHITESPACE.sub(" ", text).strip().upper()


def compact(text: str) -> str:
    return NON_ALNUM.sub("", text).upper()


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def first_json(directory: Path) -> Path:
    canonical = directory / "document.json"
    if canonical.exists():
        return canonical

    ignored = {
        "timing.json",
        "run-metadata.json",
    }
    candidates = sorted(
        path
        for path in directory.glob("*.json")
        if path.name not in ignored
        and not path.name.endswith(".profile.json")
    )

    if not candidates:
        raise RuntimeError(f"No Docling document JSON found in {directory}")

    if len(candidates) != 1:
        raise RuntimeError(
            f"Expected exactly one Docling document JSON in {directory}, "
            f"found: {[path.name for path in candidates]}"
        )

    return candidates[0]


def first_markdown(directory: Path) -> Path | None:
    candidates = sorted(directory.glob("*.md"))
    return candidates[0] if candidates else None


def read_elapsed_seconds(directory: Path) -> float | None:
    timing = directory / "timing.json"
    if not timing.exists():
        return None

    data = load_json(timing)
    value = data.get("elapsedSeconds")
    return float(value) if value is not None else None


def collect_text_items(document: dict[str, Any]) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    for key in ("texts", "tables", "key_value_items", "form_items"):
        value = document.get(key, [])
        if isinstance(value, list):
            items.extend(item for item in value if isinstance(item, dict))
    return items


def extract_item_text(item: dict[str, Any]) -> str:
    for key in ("text", "orig"):
        value = item.get(key)
        if isinstance(value, str) and value.strip():
            return value
    return ""


def extract_pages(item: dict[str, Any]) -> set[int]:
    pages: set[int] = set()
    provenance = item.get("prov", [])
    if not isinstance(provenance, list):
        return pages

    for prov in provenance:
        if not isinstance(prov, dict):
            continue
        page_no = prov.get("page_no")
        if isinstance(page_no, int):
            pages.add(page_no)
    return pages


def analyze_docling_run(run: dict[str, Any], probes: list[str]) -> DoclingMetrics:
    directory = Path(run["outputDirectory"])
    document = load_json(first_json(directory))
    markdown_path = first_markdown(directory)
    markdown = (
        markdown_path.read_text(encoding="utf-8")
        if markdown_path is not None
        else ""
    )

    items = collect_text_items(document)
    texts = [extract_item_text(item) for item in items]
    texts = [text for text in texts if text]

    pages_with_text: set[int] = set()
    for item in items:
        if extract_item_text(item):
            pages_with_text.update(extract_pages(item))

    labels = collections.Counter(
        str(item.get("label", "unknown"))
        for item in items
    )

    heading_items = sum(
        count
        for label, count in labels.items()
        if label in {"title", "section_header"}
    )

    normalized_corpus = normalize("\n".join(texts))
    compact_corpus = compact("\n".join(texts))

    probe_matches = {
        probe: normalized_corpus.count(normalize(probe))
        for probe in probes
    }
    compact_probe_matches = {
        probe: compact_corpus.count(compact(probe))
        for probe in probes
    }

    return DoclingMetrics(
        run_id=str(run["id"]),
        page_range=str(run["pageRange"]),
        ocr_mode=str(run["ocrMode"]),
        elapsed_seconds=read_elapsed_seconds(directory),
        schema_name=document.get("schema_name"),
        schema_version=document.get("version"),
        text_items=len(texts),
        text_characters=sum(len(text) for text in texts),
        pages_with_text=len(pages_with_text),
        heading_items=heading_items,
        label_counts=dict(sorted(labels.items())),
        probe_matches=probe_matches,
        compact_probe_matches=compact_probe_matches,
        markdown_characters=len(markdown),
    )


def load_apologia_report(path: str | None) -> dict[str, Any] | None:
    if not path:
        return None

    report_path = Path(path)
    if not report_path.exists():
        return None

    return load_json(report_path)


def parse_curated_sections(path: str | None) -> int | None:
    if not path:
        return None
    output = Path(path)
    if not output.exists():
        return None

    match = re.search(
        r"^Sections:\s*(\d+)\s*$",
        output.read_text(encoding="utf-8"),
        flags=re.MULTILINE,
    )
    return int(match.group(1)) if match else None


def format_float(value: float | None) -> str:
    return f"{value:.1f}" if value is not None else "n/a"


def render_markdown(summary: dict[str, Any]) -> str:
    lines = [
        "# Docling spike v1 results",
        "",
        f"Docling version: `{summary['doclingVersion']}`",
        f"OCR engine: `{summary['ocrEngine']}`",
        f"Device: `{summary['device']}`",
        "",
        "## Docling runs",
        "",
        "| Run | Pages | OCR | Text items | Text chars | Pages with text | Headings | Seconds |",
        "|---|---:|---|---:|---:|---:|---:|---:|",
    ]

    for run in summary["doclingRuns"]:
        lines.append(
            "| {runId} | {pageRange} | {ocrMode} | {textItems} | "
            "{textCharacters} | {pagesWithText} | {headingItems} | {seconds} |".format(
                seconds=format_float(run["elapsedSeconds"]),
                **run,
            )
        )

    lines.extend(
        [
            "",
            "## Current ApologiaStudio baseline",
            "",
        ]
    )

    baselines = summary.get("apologiaBaselines", {})
    for name, report in baselines.items():
        if report is None:
            lines.append(f"- **{name}:** not available.")
            continue

        extraction = report.get("extraction", {})
        segmentation = report.get("segmentation", {})
        lines.append(
            f"- **{name}:** "
            f"{report.get('pageSelection', {}).get('pageCount', '?')} pages, "
            f"{extraction.get('wordCount', '?')} words, "
            f"{extraction.get('textLayerCoveragePercent', '?')}% text-layer coverage, "
            f"{segmentation.get('segmentCount', '?')} segments."
        )

    curated_sections = summary.get("deDecretisCuratedSections")
    if curated_sections is not None:
        lines.append(
            f"- **De Decretis curated validator:** {curated_sections} sections."
        )

    lines.extend(
        [
            "",
            "## OCR recovery signal",
            "",
        ]
    )

    recovery = summary.get("ocrRecovery", {})
    if recovery:
        lines.extend(
            [
                f"- Raster sample without OCR: "
                f"{recovery.get('withoutOcrPagesWithText', '?')} pages with text, "
                f"{recovery.get('withoutOcrTextCharacters', '?')} characters.",
                f"- Raster sample with full-page OCR: "
                f"{recovery.get('withOcrPagesWithText', '?')} pages with text, "
                f"{recovery.get('withOcrTextCharacters', '?')} characters.",
            ]
        )

    lines.extend(
        [
            "",
            "## Probe results",
            "",
        ]
    )

    for run in summary["doclingRuns"]:
        probes = run.get("probeMatches", {})
        compact_probes = run.get("compactProbeMatches", {})
        if not probes:
            continue
        lines.append(f"### {run['runId']}")
        lines.append("")
        for probe, count in probes.items():
            lines.append(
                f"- `{probe}`: exact-normalized={count}, "
                f"compact={compact_probes.get(probe, 0)}"
            )
        lines.append("")

    lines.extend(
        [
            "## Interpretation",
            "",
            "This report is intentionally empirical. It does not decide that Docling "
            "should replace the current pipeline. The useful questions are whether "
            "Docling materially improves OCR recovery, structural headings and "
            "document ordering on the pinned real-document samples, and whether "
            "those benefits justify a Python/service dependency.",
            "",
        ]
    )

    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    manifest = load_json(args.manifest)

    probes = [
        str(probe)
        for probe in manifest.get("probes", [])
    ]

    runs = [
        analyze_docling_run(run, probes).as_dict()
        for run in manifest["runs"]
    ]

    baselines = {
        name: load_apologia_report(path)
        for name, path in manifest.get("apologiaReports", {}).items()
    }

    runs_by_id = {
        run["runId"]: run
        for run in runs
    }

    raster_no_ocr = runs_by_id.get("ehrman-raster-no-ocr")
    raster_ocr = runs_by_id.get("ehrman-raster-full-ocr")

    ocr_recovery = {}
    if raster_no_ocr and raster_ocr:
        ocr_recovery = {
            "withoutOcrPagesWithText":
                raster_no_ocr["pagesWithText"],
            "withoutOcrTextCharacters":
                raster_no_ocr["textCharacters"],
            "withOcrPagesWithText":
                raster_ocr["pagesWithText"],
            "withOcrTextCharacters":
                raster_ocr["textCharacters"],
        }

    summary = {
        "schemaVersion": "apologia-docling-spike-v1",
        "doclingVersion": manifest["doclingVersion"],
        "ocrEngine": manifest["ocrEngine"],
        "device": manifest["device"],
        "runs": manifest["runs"],
        "doclingRuns": runs,
        "apologiaBaselines": baselines,
        "deDecretisCuratedSections":
            parse_curated_sections(
                manifest.get("deDecretisCuratedOutput")
            ),
        "ocrRecovery": ocr_recovery,
        "probes": probes,
    }

    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(
        json.dumps(summary, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    args.output_md.write_text(
        render_markdown(summary),
        encoding="utf-8",
    )

    print(f"Summary JSON: {args.output_json}")
    print(f"Summary Markdown: {args.output_md}")

    if ocr_recovery:
        print(
            "Raster OCR comparison: "
            f"{ocr_recovery['withoutOcrPagesWithText']} -> "
            f"{ocr_recovery['withOcrPagesWithText']} pages with text; "
            f"{ocr_recovery['withoutOcrTextCharacters']} -> "
            f"{ocr_recovery['withOcrTextCharacters']} text characters."
        )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
