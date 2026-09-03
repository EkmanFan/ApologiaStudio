#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import time
from pathlib import Path

from docling.datamodel.accelerator_options import (
    AcceleratorDevice,
    AcceleratorOptions,
)
from docling.datamodel.base_models import InputFormat
from docling.datamodel.pipeline_options import (
    EasyOcrOptions,
    PdfPipelineOptions,
)
from docling.document_converter import DocumentConverter, PdfFormatOption
from docling_core.types.doc import ImageRefMode


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Run one pinned Docling standard-PDF experiment through the "
            "Python API. This avoids relying on CLI option parity."
        )
    )
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument(
        "--pages",
        required=True,
        help="One-based inclusive page range, e.g. 14-20.",
    )
    parser.add_argument(
        "--ocr-mode",
        required=True,
        choices=("off", "default", "full_page"),
    )
    return parser.parse_args()


def parse_page_range(value: str) -> tuple[int, int]:
    parts = value.split("-", maxsplit=1)
    if len(parts) != 2:
        raise ValueError(
            f"Invalid page range {value!r}; expected START-END."
        )

    start = int(parts[0])
    end = int(parts[1])

    if start < 1 or end < start:
        raise ValueError(
            f"Invalid page range {value!r}; expected 1 <= START <= END."
        )

    return start, end


def build_pipeline_options(ocr_mode: str) -> PdfPipelineOptions:
    options = PdfPipelineOptions()
    options.do_table_structure = False
    options.accelerator_options = AcceleratorOptions(
        num_threads=4,
        device=AcceleratorDevice.CPU,
    )

    if ocr_mode == "off":
        options.do_ocr = False
        return options

    options.do_ocr = True

    if ocr_mode == "full_page":
        options.ocr_options = EasyOcrOptions(
            lang=["en"],
            use_gpu=False,
            force_full_page_ocr=True,
        )
    else:
        options.ocr_options = EasyOcrOptions(
            lang=["en"],
            use_gpu=False,
        )

    return options


def main() -> int:
    args = parse_args()

    if not args.source.is_file():
        raise FileNotFoundError(args.source)

    page_range = parse_page_range(args.pages)
    args.output.mkdir(parents=True, exist_ok=True)

    pipeline_options = build_pipeline_options(args.ocr_mode)
    converter = DocumentConverter(
        allowed_formats=[InputFormat.PDF],
        format_options={
            InputFormat.PDF: PdfFormatOption(
                pipeline_options=pipeline_options,
            )
        },
    )

    started = time.monotonic()
    result = converter.convert(
        args.source,
        page_range=page_range,
    )
    elapsed = time.monotonic() - started

    result.document.save_as_json(
        args.output / "document.json",
        image_mode=ImageRefMode.PLACEHOLDER,
    )
    result.document.save_as_markdown(
        args.output / "document.md",
        image_mode=ImageRefMode.PLACEHOLDER,
        page_break_placeholder="\n\n<!-- page-break -->\n\n",
    )

    metadata = {
        "source": str(args.source),
        "pageRange": {
            "start": page_range[0],
            "end": page_range[1],
        },
        "ocrMode": args.ocr_mode,
        "elapsedSeconds": elapsed,
        "conversionStatus": str(result.status),
        "documentPages": result.document.num_pages(),
        "errors": [
            str(error)
            for error in result.errors
        ],
    }
    (args.output / "timing.json").write_text(
        json.dumps(metadata, indent=2) + "\n",
        encoding="utf-8",
    )

    print(f"RESULT: {result.status}")
    print(f"Pages requested: {page_range[0]}-{page_range[1]}")
    print(f"Document pages represented: {result.document.num_pages()}")
    print(f"OCR mode: {args.ocr_mode}")
    print(f"Elapsed seconds: {elapsed:.1f}")
    print(f"Errors: {len(result.errors)}")
    print(f"JSON: {args.output / 'document.json'}")
    print(f"Markdown: {args.output / 'document.md'}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
