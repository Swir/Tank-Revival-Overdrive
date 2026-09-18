#!/usr/bin/env python3
"""Generate and verify SWIR Progress SVG Pro assets from canonical ROADMAP.md data.

ROADMAP.md is the sole progress authority. The generator validates the protected
SWIR Roadmap Standard v1 dashboard before writing visual assets, keeps release
readiness separate from milestone completion, and idempotently embeds textual
fallbacks in README/ROADMAP. Milestone branch labels are derived from the
verified roadmap status instead of being hard-coded to a previous milestone.
"""
from __future__ import annotations

import argparse
import html
import re
import sys
import urllib.parse
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path

ROADMAP = Path("ROADMAP.md")
README = Path("README.md")
ASSET_DIR = Path("assets/readme")
CARD = ASSET_DIR / "progress-card.svg"
MINI = ASSET_DIR / "progress-mini.svg"
TEMPLATE = ASSET_DIR / "progress-template.svg"
MARKER = "<!-- SWIR-PROGRESS-SVG-PRO:v1 -->"
ROADMAP_MARKER = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
README_MARKER = "<!-- SWIR-README-STANDARD:v2 -->"
PROJECT = "Tank Revival: Orzeł Overdrive"
SCOPE = "ROADMAP deliverables"
TRACK_CARD = 656.0
TRACK_MINI = 360.0


@dataclass(frozen=True)
class Progress:
    completed: int
    remaining: int
    total: int
    percent: float
    status: str
    segments: int

    @property
    def counter(self) -> str:
        return f"{self.completed} / {self.total} completed"

    @property
    def release_readiness(self) -> str:
        return "Release readiness: tracked separately by Windows qualification gates"

    @property
    def fallback(self) -> str:
        return (
            f"Roadmap progress: {self.completed} / {self.total} completed "
            f"({self.percent:.1f}%) — {self.status}. "
            "Release readiness is tracked separately by Windows qualification gates."
        )


def fail(message: str):
    raise RuntimeError(message)


def branch_for_status(status: str) -> str:
    """Map a verified status such as V13.3 IN DEVELOPMENT to dev-v13-3."""
    match = re.search(r"\bV(\d+)\.(\d+)\b", status, re.I)
    if not match:
        fail(f"cannot derive development branch from roadmap status: {status!r}")
    return f"dev-v{int(match.group(1))}-{int(match.group(2))}"


def parse_progress(text: str) -> Progress:
    if len(re.findall(r"^<!-- SWIR-ROADMAP-STANDARD:v1 -->$", text, re.M)) != 1:
        fail("ROADMAP must contain exactly one SWIR Roadmap Standard v1 marker")
    for token in (
        "<!-- ROADMAP-PROGRESS:START -->",
        "<!-- ROADMAP-PROGRESS:END -->",
        "## 📊 Overall progress",
        "| ✅ Completed | ⏳ Remaining | 📦 Total | 🎯 Progress |",
    ):
        if token not in text:
            fail(f"ROADMAP protected dashboard token missing: {token}")

    completed = len(re.findall(r"^- \[x\] ", text, re.M))
    remaining = len(re.findall(r"^- \[ \] ", text, re.M))
    total = completed + remaining
    if total <= 0:
        fail("ROADMAP has no verifiable checklist scope; progress is N/A")
    percent = round(completed * 100.0 / total, 1)

    table = re.search(
        r"\| \*\*(\d+)\*\* \| \*\*(\d+)\*\* \| \*\*(\d+)\*\* \| \*\*([0-9.]+)%\*\* \|",
        text,
    )
    if not table:
        fail("ROADMAP progress table row is missing")
    table_values = (int(table.group(1)), int(table.group(2)), int(table.group(3)), float(table.group(4)))
    expected = (completed, remaining, total, percent)
    if table_values != expected:
        fail(f"ROADMAP table disagrees with checklist: table={table_values} checklist={expected}")

    done_badge = re.search(r"DONE-(\d+)%2F(\d+)-", text)
    road_badge = re.search(r"ROADMAP-([0-9.]+)%25-", text)
    status_badge = re.search(r"STATUS-([^\"?]+?)-(?:yellow|brightgreen|red|orange|blue|1f6feb)\?", text)
    if not (done_badge and road_badge and status_badge):
        fail("ROADMAP badges are incomplete")
    if (int(done_badge.group(1)), int(done_badge.group(2))) != (completed, total):
        fail("DONE badge disagrees with checklist")
    if float(road_badge.group(1)) != percent:
        fail("ROADMAP percentage badge disagrees with checklist")
    status = urllib.parse.unquote(status_badge.group(1)).strip()

    bar = re.search(r"^([█░]{20}) ([0-9.]+)%$", text, re.M)
    if not bar:
        fail("20-segment ROADMAP bar missing")
    expected_segments = min(20, max(0, int(percent // 5)))
    if percent >= 100.0:
        expected_segments = 20
    if bar.group(1).count("█") != expected_segments or float(bar.group(2)) != percent:
        fail("ROADMAP text bar disagrees with documented checklist percentage")
    return Progress(completed, remaining, total, percent, status, expected_segments)


def esc(value: str) -> str:
    return html.escape(value, quote=True)


def width(track: float, p: Progress) -> float:
    return round(min(track, max(0.0, track * p.completed / p.total)), 2)


def svg_card(p: Progress) -> str:
    fill = width(TRACK_CARD, p)
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="760" height="220" viewBox="0 0 760 220" role="img" aria-labelledby="title desc">
  <title id="title">{esc(PROJECT)} progress — {p.percent:.1f}%</title>
  <desc id="desc">{esc(p.fallback)}</desc>
  <defs>
    <linearGradient id="swirProgress" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#0088FF"/><stop offset="1" stop-color="#62E5FF"/></linearGradient>
    <filter id="softGlow" x="-10%" y="-30%" width="120%" height="160%"><feGaussianBlur stdDeviation="3" result="blur"/><feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
  </defs>
  <rect x="1" y="1" width="758" height="218" rx="18" fill="#02050A" stroke="#16394C" stroke-width="2"/>
  <rect x="18" y="18" width="724" height="184" rx="14" fill="#07111C" stroke="#0B2637"/>
  <text x="32" y="48" fill="#62E5FF" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="20" font-weight="700">{esc(PROJECT)}</text>
  <text x="32" y="73" fill="#9FB7C8" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="13">Measured scope: {esc(SCOPE)}</text>
  <text x="728" y="51" text-anchor="end" fill="#EAF8FF" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="24" font-weight="700">{p.percent:.1f}%</text>
  <rect x="32" y="94" width="656" height="18" rx="9" fill="#02050A" stroke="#143449"/>
  <rect x="32" y="94" width="{fill:.2f}" height="18" rx="9" fill="url(#swirProgress)" filter="url(#softGlow)"/>
  <text x="32" y="139" fill="#D6EAF5" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="14" font-weight="600">Status: {esc(p.status)}</text>
  <text x="728" y="139" text-anchor="end" fill="#D6EAF5" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="14">{esc(p.counter)}</text>
  <text x="32" y="169" fill="#7FA2B5" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="12">{esc(p.release_readiness)}</text>
  <text x="32" y="190" fill="#4D748A" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="11">Generated deterministically from ROADMAP.md — SWIR Progress SVG Pro v1</text>
</svg>\n'''


def svg_mini(p: Progress) -> str:
    fill = width(TRACK_MINI, p)
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="700" height="92" viewBox="0 0 700 92" role="img" aria-labelledby="title desc">
  <title id="title">{esc(PROJECT)} roadmap — {p.percent:.1f}%</title>
  <desc id="desc">{esc(p.fallback)}</desc>
  <defs><linearGradient id="swirMini" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#0088FF"/><stop offset="1" stop-color="#62E5FF"/></linearGradient></defs>
  <rect x="1" y="1" width="698" height="90" rx="14" fill="#02050A" stroke="#16394C" stroke-width="2"/>
  <text x="18" y="28" fill="#62E5FF" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="14" font-weight="700">{esc(SCOPE)}</text>
  <text x="682" y="28" text-anchor="end" fill="#EAF8FF" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="15" font-weight="700">{p.percent:.1f}% · {esc(p.counter)}</text>
  <rect x="18" y="42" width="360" height="12" rx="6" fill="#07111C" stroke="#143449"/>
  <rect x="18" y="42" width="{fill:.2f}" height="12" rx="6" fill="url(#swirMini)"/>
  <text x="18" y="76" fill="#9FB7C8" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="12">{esc(p.status)} · release readiness tracked separately</text>
</svg>\n'''


def svg_template() -> str:
    return '''<svg xmlns="http://www.w3.org/2000/svg" width="760" height="220" viewBox="0 0 760 220" role="img" aria-labelledby="title desc">
  <title id="title">SWIR Progress SVG Pro reusable template</title>
  <desc id="desc">Template only. Progress values are N/A until generated from verified authoritative data.</desc>
  <defs>
    <linearGradient id="swirProgress" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#0088FF"/><stop offset="1" stop-color="#62E5FF"/></linearGradient>
    <filter id="softGlow" x="-10%" y="-30%" width="120%" height="160%"><feGaussianBlur stdDeviation="3"/></filter>
  </defs>
  <rect x="1" y="1" width="758" height="218" rx="18" fill="#02050A" stroke="#16394C" stroke-width="2"/>
  <rect x="18" y="18" width="724" height="184" rx="14" fill="#07111C" stroke="#0B2637"/>
  <text x="32" y="48" fill="#62E5FF" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="20" font-weight="700">{{PROJECT}}</text>
  <text x="32" y="73" fill="#9FB7C8" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="13">Measured scope: {{SCOPE}}</text>
  <text x="728" y="51" text-anchor="end" fill="#EAF8FF" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="24" font-weight="700">N/A</text>
  <rect x="32" y="94" width="656" height="18" rx="9" fill="#02050A" stroke="#143449"/>
  <rect data-role="progress-fill" x="32" y="94" width="1" height="18" rx="9" fill="url(#swirProgress)" opacity="0"/>
  <text x="32" y="139" fill="#D6EAF5" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="14" font-weight="600">Status: {{STATUS}}</text>
  <text x="728" y="139" text-anchor="end" fill="#D6EAF5" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="14">{{COUNTER}}</text>
  <text x="32" y="169" fill="#7FA2B5" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="12">Release readiness: tracked separately</text>
  <text x="32" y="190" fill="#4D748A" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="11">Template — replace only from verified authoritative data</text>
</svg>\n'''


def validate_svg(name: str, content: str, max_width: float | None = None) -> None:
    try:
        ET.fromstring(content)
    except ET.ParseError as exc:
        fail(f"{name} is not valid XML: {exc}")
    for color in ("#02050A", "#07111C", "#0088FF", "#62E5FF"):
        if color not in content:
            fail(f"{name} does not contain required SWIR color {color}")
    if max_width is not None:
        widths = [float(x) for x in re.findall(r'<rect x="(?:18|32)" y="(?:42|94)" width="([0-9.]+)"', content)]
        if not widths or max(widths) > max_width + 1e-9:
            fail(f"{name} progress geometry exceeds its track")


def embed_docs(p: Progress) -> bool:
    changed = False
    roadmap = ROADMAP.read_text(encoding="utf-8")
    if ROADMAP_MARKER not in roadmap:
        fail("cannot embed progress mini without protected ROADMAP marker")
    mini_block = (
        f"{MARKER}\n"
        '<p align="center"><img src="assets/readme/progress-mini.svg" alt="SWIR roadmap progress mini" width="700"></p>\n'
        f'<p align="center"><sub>{p.fallback}</sub></p>\n'
    )
    roadmap = re.sub(
        r"\n*<!-- SWIR-PROGRESS-SVG-PRO:v1 -->\n<p align=\"center\"><img src=\"assets/readme/progress-mini\.svg\".*?</p>\n<p align=\"center\"><sub>.*?</sub></p>\n*",
        "\n\n", roadmap, flags=re.S,
    )
    anchor = "\n\n## 📊 Overall progress"
    if anchor not in roadmap:
        fail("ROADMAP Overall progress anchor missing")
    roadmap = roadmap.replace(anchor, "\n\n" + mini_block + "\n## 📊 Overall progress", 1)
    original = ROADMAP.read_text(encoding="utf-8")
    if roadmap != original:
        ROADMAP.write_text(roadmap, encoding="utf-8")
        changed = True

    readme = README.read_text(encoding="utf-8")
    if README_MARKER not in readme:
        fail("README must remain on SWIR README Standard v2")
    card_block = (
        f"{MARKER}\n"
        '<p align="center"><img src="assets/readme/progress-card.svg" alt="SWIR project roadmap progress" width="760"></p>\n'
        f'<p align="center"><sub>{p.fallback}</sub></p>\n'
    )
    readme = re.sub(
        r"\n*<!-- SWIR-PROGRESS-SVG-PRO:v1 -->\n<p align=\"center\"><img src=\"assets/readme/progress-card\.svg\".*?</p>\n<p align=\"center\"><sub>.*?</sub></p>\n*",
        "\n\n", readme, flags=re.S,
    )
    marker = "\n---\n\n## 📌 Project Status"
    if marker not in readme:
        fail("README Project Status anchor missing")
    readme = readme.replace(marker, "\n\n" + card_block + "\n---\n\n## 📌 Project Status", 1)
    badge_status = urllib.parse.quote(p.status.title(), safe="")
    roadmap_badge = f"[![Roadmap](https://img.shields.io/badge/Roadmap-{p.percent:.1f}%25%20{badge_status}-02050A?style=for-the-badge&logo=github&logoColor=62E5FF)](ROADMAP.md)"
    readme = re.sub(r"(?m)^\[!\[Roadmap\]\([^\n]+\]\(ROADMAP\.md\)$", roadmap_badge, readme, count=1)
    branch = branch_for_status(p.status)
    readme = re.sub(
        r"\| Development milestone \| \*\*.*?\*\* on `dev-v[^`]+` \|",
        f"| Development milestone | **{p.status}** on `{branch}` |",
        readme,
        count=1,
    )
    readme = re.sub(
        r"\| Roadmap \| \*\*\d+ / \d+[^\n]*\|",
        f"| Roadmap | **{p.completed} / {p.total} completed ({p.percent:.1f}%)** — authoritative `ROADMAP.md` scope |",
        readme,
        count=1,
    )
    readme = re.sub(
        r"(?m)^The authoritative roadmap is \[`ROADMAP\.md`\]\(ROADMAP\.md\)\..*$",
        f"The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **{p.completed} / {p.total} ({p.percent:.1f}%) — {p.status}**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        readme,
        count=1,
    )
    if "## 🔎 Search Keywords" not in readme:
        fail("README PRO v2 Search Keywords section is missing")
    before = README.read_text(encoding="utf-8")
    if readme != before:
        README.write_text(readme, encoding="utf-8")
        changed = True
    return changed


def expected_assets(p: Progress) -> dict[Path, str]:
    return {CARD: svg_card(p), MINI: svg_mini(p), TEMPLATE: svg_template()}


def write_assets(p: Progress) -> bool:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    changed = False
    for path, content in expected_assets(p).items():
        validate_svg(path.name, content, TRACK_CARD if path == CARD else TRACK_MINI if path == MINI else None)
        old = path.read_text(encoding="utf-8") if path.exists() else None
        if old != content:
            path.write_text(content, encoding="utf-8")
            changed = True
    return changed


def check_all() -> None:
    p = parse_progress(ROADMAP.read_text(encoding="utf-8"))
    for path, expected in expected_assets(p).items():
        if not path.is_file():
            fail(f"missing generated asset: {path}")
        actual = path.read_text(encoding="utf-8")
        if actual != expected:
            fail(f"generated asset is stale/non-deterministic: {path}")
        validate_svg(path.name, actual, TRACK_CARD if path == CARD else TRACK_MINI if path == MINI else None)
    roadmap = ROADMAP.read_text(encoding="utf-8")
    readme = README.read_text(encoding="utf-8")
    if roadmap.count(MARKER) != 1 or readme.count(MARKER) != 1:
        fail("progress marker must occur exactly once in both ROADMAP and README")
    if 'assets/readme/progress-mini.svg' not in roadmap or p.fallback not in roadmap:
        fail("ROADMAP mini embed or textual fallback is stale")
    if 'assets/readme/progress-card.svg' not in readme or p.fallback not in readme:
        fail("README card embed or textual fallback is stale")
    if README_MARKER not in readme or "## 🔎 Search Keywords" not in readme:
        fail("README PRO v2 marker/Search Keywords must be preserved")
    expected_branch = branch_for_status(p.status)
    if f"**{p.status}** on `{expected_branch}`" not in readme:
        fail("README milestone branch disagrees with roadmap status")
    print(f"SWIR progress SVG check: PASS — {p.completed}/{p.total} ({p.percent:.1f}%) {p.status} on {expected_branch}")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    parser.add_argument("--embed", action="store_true")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    if not (args.write or args.embed or args.check):
        parser.error("choose at least one of --write, --embed, --check")
    try:
        p = parse_progress(ROADMAP.read_text(encoding="utf-8"))
        changed = False
        if args.embed:
            changed |= embed_docs(p)
            p = parse_progress(ROADMAP.read_text(encoding="utf-8"))
        if args.write:
            changed |= write_assets(p)
        if args.check:
            check_all()
        print("SWIR progress SVG rollout:", "updated" if changed else "no-op")
        return 0
    except (OSError, RuntimeError, ValueError) as exc:
        print(f"SWIR progress SVG FAILED: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
