#!/usr/bin/env python3
"""Guard SWIR SVG-only progress presentation against retired text meters.

This is intentionally presentation-only: it does not change roadmap checklist state,
percentages, release gates, or generated SVG values. It normalizes legacy meter
visuals in maintained README/ROADMAP documents and verifies the approved SVG
embedding contract introduced by SWIR-VISUAL-REPORT:v3.
"""
from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

README = Path("README.md")
ROADMAP = Path("ROADMAP.md")
CARD = Path("assets/readme/progress-card.svg")
MINI = Path("assets/readme/progress-mini.svg")
TEMPLATE = Path("assets/readme/progress-template.svg")

README_MARKER = "<!-- SWIR-README-STANDARD:v2 -->"
ROADMAP_MARKER = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
PROGRESS_MARKER = "<!-- SWIR-PROGRESS-SVG-PRO:v1 -->"

# Retired character-art meters. The bracket form is deliberately restricted to
# meter-only characters so Markdown links/checklists are not false positives.
UNICODE_METER_RE = re.compile(r"[█▓▒░▉▊▋▌▍▎▏■□]{8,}")
BRACKET_METER_RE = re.compile(r"\[(?=[#=_\.\- ]{8,}\])(?=[#=_\.\- ]*[#=\-])[#=_\.\- ]{8,}\]")
EMPTY_TEXT_FENCE_RE = re.compile(r"(?ms)\n```(?:text)?[ \t]*\n[ \t]*\n```[ \t]*(?=\n|$)")

REQUIRED_COLORS = ("#02050A", "#07111C", "#0088FF", "#62E5FF")


def fail(message: str) -> None:
    raise RuntimeError(message)


def meter_hits(text: str) -> list[tuple[int, str]]:
    hits: list[tuple[int, str]] = []
    for lineno, line in enumerate(text.splitlines(), start=1):
        if UNICODE_METER_RE.search(line) or BRACKET_METER_RE.search(line):
            hits.append((lineno, line.strip()[:180]))
    return hits


def strip_legacy_meters(text: str) -> str:
    """Remove retired meter tokens outside fenced code, preserving nearby text.

    Fenced code is not rewritten automatically because it may be an unrelated
    command/source example. The checker still reports a meter there so a human can
    decide whether it is historical evidence or an active presentation artifact.
    """
    out: list[str] = []
    in_fence = False
    for line in text.splitlines(keepends=True):
        stripped = line.lstrip()
        if stripped.startswith("```"):
            in_fence = not in_fence
            out.append(line)
            continue
        if in_fence:
            out.append(line)
            continue
        line = UNICODE_METER_RE.sub("", line)
        line = BRACKET_METER_RE.sub("", line)
        out.append(line)
    cleaned = "".join(out)
    cleaned = EMPTY_TEXT_FENCE_RE.sub("\n", cleaned)
    return cleaned


def validate_svg(path: Path, *, template: bool = False) -> None:
    if not path.is_file():
        fail(f"missing SVG asset: {path}")
    text = path.read_text(encoding="utf-8")
    try:
        root = ET.fromstring(text)
    except ET.ParseError as exc:
        fail(f"invalid SVG/XML {path}: {exc}")
    if not root.tag.endswith("svg"):
        fail(f"invalid SVG root: {path}")
    for color in REQUIRED_COLORS:
        if color not in text:
            fail(f"{path} missing required SWIR color {color}")
    if root.find("{http://www.w3.org/2000/svg}title") is None:
        fail(f"{path} missing <title>")
    if root.find("{http://www.w3.org/2000/svg}desc") is None:
        fail(f"{path} missing <desc>")
    if template:
        if "TEMPLATE" not in text or "N/A" not in text:
            fail("progress-template.svg must be clearly marked TEMPLATE and N/A")
        if 'data-template="SWIR-PROGRESS-SVG-PRO:v1"' not in text:
            fail("progress-template.svg missing template contract marker")


def check_docs() -> None:
    readme = README.read_text(encoding="utf-8")
    roadmap = ROADMAP.read_text(encoding="utf-8")

    if README_MARKER not in readme:
        fail("README PRO v2 marker missing")
    if ROADMAP_MARKER not in roadmap:
        fail("ROADMAP structure marker missing")
    for token in (
        "<!-- ROADMAP-PROGRESS:START -->",
        "<!-- ROADMAP-PROGRESS:END -->",
        "## 📊 Overall progress",
        "| ✅ Completed | ⏳ Remaining | 📦 Total | 🎯 Progress |",
    ):
        if token not in roadmap:
            fail(f"ROADMAP protected dashboard token missing: {token}")
    if "## 🔎 Search Keywords" not in readme:
        fail("README Search Keywords section missing")

    for name, text in (("README.md", readme), ("ROADMAP.md", roadmap)):
        hits = meter_hits(text)
        if hits:
            detail = "; ".join(f"L{line}: {snippet}" for line, snippet in hits[:4])
            fail(f"{name} contains retired character progress meter(s): {detail}")

    if readme.count("assets/readme/progress-card.svg") != 1:
        fail("README must embed exactly one progress-card.svg")
    if "assets/readme/progress-mini.svg" in readme or "progress-template.svg" in readme:
        fail("README must not duplicate mini/template progress visuals")
    if roadmap.count("assets/readme/progress-mini.svg") != 1:
        fail("ROADMAP must embed exactly one progress-mini.svg")
    if "assets/readme/progress-card.svg" in roadmap or "progress-template.svg" in roadmap:
        fail("ROADMAP must not duplicate card/template progress visuals")
    if PROGRESS_MARKER not in readme or PROGRESS_MARKER not in roadmap:
        fail("SVG progress contract marker missing from README or ROADMAP")

    validate_svg(CARD)
    validate_svg(MINI)
    validate_svg(TEMPLATE, template=True)


def fix_docs() -> bool:
    changed = False
    for path in (README, ROADMAP):
        original = path.read_text(encoding="utf-8")
        cleaned = strip_legacy_meters(original)
        if cleaned != original:
            path.write_text(cleaned, encoding="utf-8")
            changed = True
    return changed


def self_test() -> None:
    bad = (
        "████████░░░░ 66.7%",
        "Progress [####----] 50%",
        "[========        ]",
        "status [----####]",
    )
    for sample in bad:
        if not meter_hits(sample):
            fail(f"legacy meter regression was not detected: {sample!r}")
    good = (
        "- [x] verified deliverable",
        "- [ ] pending deliverable",
        "[README](README.md)",
        "Progress: 415 / 415 (100.0%)",
        "array[index] = value",
    )
    for sample in good:
        if meter_hits(sample):
            fail(f"false-positive legacy meter detection: {sample!r}")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fix", action="store_true", help="remove legacy meter tokens from maintained docs")
    parser.add_argument("--check", action="store_true", help="verify SVG-only presentation contract")
    parser.add_argument("--self-test", action="store_true", help="run detector regression cases")
    args = parser.parse_args(argv)
    if not (args.fix or args.check or args.self_test):
        parser.error("choose --fix, --check and/or --self-test")

    if args.self_test:
        self_test()
    changed = fix_docs() if args.fix else False
    if args.check:
        check_docs()
    print(f"SWIR visual report guard: changed={'yes' if changed else 'no'}; check={'ok' if args.check else 'skipped'}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as exc:
        print(f"SWIR visual report guard FAILED: {exc}", file=sys.stderr)
        raise SystemExit(2)
