#!/usr/bin/env python3
"""Guard SWIR Visual Report v3 presentation contracts for active docs/SVG assets."""
from __future__ import annotations

import argparse
import re
import xml.etree.ElementTree as ET
from pathlib import Path

README = Path("README.md")
ROADMAP = Path("ROADMAP.md")
CARD = Path("assets/readme/progress-card.svg")
MINI = Path("assets/readme/progress-mini.svg")
TEMPLATE = Path("assets/readme/progress-template.svg")

LEGACY_PATTERNS = (
    re.compile(r"^[\s>*-]*[█▓▒░]{6,}(?:\s+[0-9]+(?:\.[0-9]+)?%)?\s*$"),
    re.compile(r"^[\s>*-]*\[[=#█▓▒░-]{8,}\](?:\s+[0-9]+(?:\.[0-9]+)?%)?\s*$"),
)


def fail(message: str) -> None:
    raise SystemExit("SWIR visual report guard FAIL: " + message)


def strip_legacy_meter_lines(text: str) -> tuple[str, int]:
    output: list[str] = []
    removed = 0
    in_fence = False
    for line in text.splitlines(keepends=True):
        if line.lstrip().startswith("```"):
            in_fence = not in_fence
            output.append(line)
            continue
        probe = line.rstrip("\r\n")
        if not in_fence and any(p.fullmatch(probe) for p in LEGACY_PATTERNS):
            removed += 1
            continue
        output.append(line)
    return "".join(output), removed


def verify_doc_contracts(readme: str, roadmap: str) -> None:
    if len(re.findall(r"^<!-- SWIR-README-STANDARD:v2 -->$", readme, re.M)) != 1:
        fail("README must contain exactly one README Standard v2 marker")
    if len(re.findall(r"^<!-- SWIR-ROADMAP-STANDARD:v1 -->$", roadmap, re.M)) != 1:
        fail("ROADMAP must contain exactly one Roadmap Standard v1 marker")
    if "## 🔎 Search Keywords" not in readme:
        fail("README Search Keywords section missing")
    if readme.count("assets/readme/progress-card.svg") != 1:
        fail("README must embed exactly one progress-card.svg")
    if "assets/readme/progress-mini.svg" in readme:
        fail("README must not duplicate the roadmap mini")
    if roadmap.count("assets/readme/progress-mini.svg") != 1:
        fail("ROADMAP must embed exactly one progress-mini.svg")
    if "assets/readme/progress-card.svg" in roadmap:
        fail("ROADMAP must not duplicate the README card")
    if "progress-template.svg" in readme or "progress-template.svg" in roadmap:
        fail("progress-template.svg is a TEMPLATE and must not be embedded as project data")
    for token in (
        "<!-- ROADMAP-PROGRESS:START -->",
        "<!-- ROADMAP-PROGRESS:END -->",
        "## 📊 Overall progress",
        "| ✅ Completed | ⏳ Remaining | 📦 Total | 🎯 Progress |",
    ):
        if token not in roadmap:
            fail("ROADMAP protected structure missing: " + token)
    for name, text in (("README", readme), ("ROADMAP", roadmap)):
        _, found = strip_legacy_meter_lines(text)
        if found:
            fail(f"{name} contains {found} legacy character/ASCII progress meter line(s)")


def verify_svg(path: Path, *, template: bool = False) -> None:
    if not path.is_file():
        fail(f"missing {path}")
    text = path.read_text(encoding="utf-8")
    try:
        root = ET.fromstring(text)
    except ET.ParseError as exc:
        fail(f"{path} invalid XML: {exc}")
    if not root.tag.endswith("svg"):
        fail(f"{path} root is not svg")
    for color in ("#02050A", "#07111C", "#0088FF", "#62E5FF"):
        if color not in text:
            fail(f"{path} missing SWIR color {color}")
    if "<title" not in text or "<desc" not in text:
        fail(f"{path} must provide accessible title and description")
    if template:
        if "Template" not in text or "N/A" not in text:
            fail("progress-template.svg must be visibly TEMPLATE/N/A")
    else:
        if "Measured scope:" not in text and "ROADMAP deliverables" not in text:
            fail(f"{path} measured scope label missing")
        if "Release readiness" not in text and "release readiness" not in text:
            fail(f"{path} must keep release readiness separate")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--fix", action="store_true", help="remove legacy meter lines outside fenced code")
    ap.add_argument("--check", action="store_true", help="verify final presentation contracts")
    args = ap.parse_args()
    if not (args.fix or args.check):
        args.check = True

    readme = README.read_text(encoding="utf-8")
    roadmap = ROADMAP.read_text(encoding="utf-8")
    if args.fix:
        readme2, r1 = strip_legacy_meter_lines(readme)
        roadmap2, r2 = strip_legacy_meter_lines(roadmap)
        if readme2 != readme:
            README.write_text(readme2, encoding="utf-8")
        if roadmap2 != roadmap:
            ROADMAP.write_text(roadmap2, encoding="utf-8")
        print(f"SWIR visual legacy-meter cleanup: removed {r1 + r2}")
        readme, roadmap = readme2, roadmap2

    if args.check:
        verify_doc_contracts(readme, roadmap)
        verify_svg(CARD)
        verify_svg(MINI)
        verify_svg(TEMPLATE, template=True)
        print("SWIR Visual Report v3 SVG-only presentation contract: PASS")


if __name__ == "__main__":
    main()
