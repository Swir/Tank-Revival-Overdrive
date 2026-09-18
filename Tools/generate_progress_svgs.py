#!/usr/bin/env python3
"""Generate and verify SWIR Progress SVG Pro assets from canonical ROADMAP.md data.

ROADMAP.md is the sole progress authority. The generator validates the protected
SWIR Roadmap Standard v1 dashboard, removes legacy text meters, writes the README
card + roadmap mini from the same verified snapshot, and keeps public release
readiness separate from roadmap completion.
"""
from __future__ import annotations

import argparse
import html
import re
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
V133_CANDIDATE = "0e3496ce095d27b2a686efbb5539cc7e475e3213"
V133_RUN = "35304207429"
V133_WORKFLOW = "battlefield-cohesion-v133-windows.yml"
LEGACY_METER_LINE = re.compile(
    r"(?m)^[ \t]*[█▓▒░▉▊▋▌▍▎▏■□]{8,}(?:[ \t]+[0-9]+(?:\.[0-9]+)?%)?[ \t]*$"
)
LEGACY_METER_FENCE = re.compile(
    r"(?ms)\n```(?:text)?\s*\n[ \t]*[█▓▒░▉▊▋▌▍▎▏■□]{8,}(?:[ \t]+[0-9]+(?:\.[0-9]+)?%)?[ \t]*\n```\s*\n?"
)


@dataclass(frozen=True)
class Progress:
    completed: int
    remaining: int
    total: int
    percent: float
    status: str

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


def fail(message: str) -> None:
    raise RuntimeError(message)


def branch_for_status(status: str) -> str:
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
    table_values = (
        int(table.group(1)), int(table.group(2)), int(table.group(3)), float(table.group(4))
    )
    expected = (completed, remaining, total, percent)
    if table_values != expected:
        fail(f"ROADMAP table disagrees with checklist: table={table_values} checklist={expected}")

    done_badge = re.search(r"DONE-(\d+)%2F(\d+)-", text)
    road_badge = re.search(r"ROADMAP-([0-9.]+)%25-", text)
    status_badge = re.search(
        r"STATUS-([^\"?]+?)-(?:yellow|brightgreen|red|orange|blue|1f6feb)\?", text
    )
    if not (done_badge and road_badge and status_badge):
        fail("ROADMAP badges are incomplete")
    if (int(done_badge.group(1)), int(done_badge.group(2))) != (completed, total):
        fail("DONE badge disagrees with checklist")
    if float(road_badge.group(1)) != percent:
        fail("ROADMAP percentage badge disagrees with checklist")
    status = urllib.parse.unquote(status_badge.group(1)).strip()
    return Progress(completed, remaining, total, percent, status)


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
  <rect data-role="progress-fill" x="32" y="94" width="{fill:.2f}" height="18" rx="9" fill="url(#swirProgress)" filter="url(#softGlow)"/>
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
  <defs>
    <linearGradient id="swirMini" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#0088FF"/><stop offset="1" stop-color="#62E5FF"/></linearGradient>
    <filter id="miniGlow" x="-10%" y="-60%" width="120%" height="220%"><feGaussianBlur stdDeviation="2" result="blur"/><feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
  </defs>
  <rect x="1" y="1" width="698" height="90" rx="14" fill="#02050A" stroke="#16394C" stroke-width="2"/>
  <rect x="12" y="12" width="676" height="68" rx="10" fill="#07111C" stroke="#0B2637"/>
  <text x="24" y="32" fill="#62E5FF" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="14" font-weight="700">{esc(SCOPE)}</text>
  <text x="676" y="32" text-anchor="end" fill="#EAF8FF" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="14" font-weight="700">{p.percent:.1f}% · {esc(p.counter)}</text>
  <rect x="24" y="43" width="360" height="12" rx="6" fill="#02050A" stroke="#143449"/>
  <rect data-role="progress-fill" x="24" y="43" width="{fill:.2f}" height="12" rx="6" fill="url(#swirMini)" filter="url(#miniGlow)"/>
  <text x="24" y="72" fill="#9FB7C8" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="11">{esc(p.status)} · release readiness tracked separately</text>
</svg>\n'''


def svg_template() -> str:
    return '''<svg xmlns="http://www.w3.org/2000/svg" width="760" height="220" viewBox="0 0 760 220" role="img" aria-labelledby="title desc" data-template="SWIR-PROGRESS-SVG-PRO:v1">
  <title id="title">SWIR Progress SVG Pro — TEMPLATE</title>
  <desc id="desc">TEMPLATE only. Progress is N/A until generated from verified authoritative data.</desc>
  <defs>
    <linearGradient id="swirProgress" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#0088FF"/><stop offset="1" stop-color="#62E5FF"/></linearGradient>
    <filter id="softGlow" x="-10%" y="-30%" width="120%" height="160%"><feGaussianBlur stdDeviation="3"/></filter>
  </defs>
  <rect x="1" y="1" width="758" height="218" rx="18" fill="#02050A" stroke="#16394C" stroke-width="2"/>
  <rect x="18" y="18" width="724" height="184" rx="14" fill="#07111C" stroke="#0B2637"/>
  <text x="32" y="48" fill="#62E5FF" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="20" font-weight="700">{{PROJECT}} — TEMPLATE</text>
  <text x="32" y="73" fill="#9FB7C8" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="13">Measured scope: {{SCOPE}}</text>
  <text x="728" y="51" text-anchor="end" fill="#EAF8FF" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="24" font-weight="700">N/A</text>
  <rect x="32" y="94" width="656" height="18" rx="9" fill="#02050A" stroke="#143449"/>
  <rect data-role="progress-fill" x="32" y="94" width="0" height="18" rx="9" fill="url(#swirProgress)" opacity="0"/>
  <text x="32" y="139" fill="#D6EAF5" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="14">Status: {{STATUS}}</text>
  <text x="728" y="139" text-anchor="end" fill="#D6EAF5" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="14">{{COUNTER}}</text>
  <text x="32" y="169" fill="#7FA2B5" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="12">Release readiness: tracked separately</text>
  <text x="32" y="190" fill="#4D748A" font-family="Segoe UI, Inter, Arial, sans-serif" font-size="11">TEMPLATE — populate only from verified authoritative data</text>
</svg>\n'''


def strip_legacy_meters(text: str) -> str:
    text = LEGACY_METER_FENCE.sub("\n", text)
    text = LEGACY_METER_LINE.sub("", text)
    return text


def canonical_progress_block(p: Progress, mini: bool) -> str:
    asset = "progress-mini.svg" if mini else "progress-card.svg"
    width_px = "700" if mini else "760"
    alt = "SWIR roadmap progress mini" if mini else "SWIR project roadmap progress"
    return (
        f"{MARKER}\n"
        f'<p align="center"><img src="assets/readme/{asset}" alt="{alt}" width="{width_px}"></p>\n'
        f'<p align="center"><sub>{p.fallback}</sub></p>\n'
    )


def replace_progress_embed(text: str, p: Progress, *, mini: bool) -> str:
    block = canonical_progress_block(p, mini)
    asset = "progress-mini.svg" if mini else "progress-card.svg"
    asset_pattern = re.escape(asset)
    pattern = re.compile(
        rf"{re.escape(MARKER)}\s*\n<p align=\"center\"><img src=\"assets/readme/{asset_pattern}\".*?</p>\s*\n<p align=\"center\"><sub>.*?</sub></p>",
        re.S,
    )
    if pattern.search(text):
        return pattern.sub(block.rstrip(), text, count=1)
    if mini:
        anchor = "\n## 📊 Overall progress"
        if anchor not in text:
            fail("ROADMAP Overall progress anchor missing")
        return text.replace(anchor, "\n\n" + block + anchor, 1)
    anchor = "\n---\n\n## 📌 Project Status"
    if anchor not in text:
        fail("README Project Status anchor missing")
    return text.replace(anchor, "\n\n" + block + anchor, 1)


def sync_v133_readme(text: str, p: Progress) -> str:
    if p.status.upper() != "V13.3 QUALIFIED":
        return text
    branch = branch_for_status(p.status)
    gate_badge = (
        f"[![v13.3 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/{V133_WORKFLOW}/badge.svg?branch={branch})]"
        f"(https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/{V133_WORKFLOW})"
    )
    text = re.sub(r"(?m)^\[!\[v\d+\.\d+ Windows Gate\].*$", gate_badge, text, count=1)
    text = text.replace("**Battlefield cohesion v13.3 — in development**", "**Battlefield cohesion v13.3 — qualified**")
    text = re.sub(
        r"\| 🧪 \*\*Exact-candidate qualification\*\* \|.*?\|",
        "| 🧪 **Exact-candidate qualification** | v13.3 passed one packaged Windows EXE through authored-audio preflight, v13.3 smoke, v13.2/v13.1/v13.0/v12.9/v12.8 regressions and late-round 80/90/100 soak. |",
        text,
        count=1,
    )
    text = text.replace("### Battlefield Cohesion v13.3 — in development", "### Battlefield Cohesion v13.3 — qualified")
    text = re.sub(
        r"The current v13\.3 core adds a fixed-capacity squad registry.*?until Windows qualification is green\.",
        "The qualified v13.3 core adds a fixed-capacity squad registry for at most **24 eligible enemy actors / six four-vehicle squads**. Slots deterministically map to Leader, Wingman, Breacher and Support roles. Cohesion can transition through Forming, Cohesive, Shocked and Regrouping states; losing a leader creates a finite shock before deterministic promotion and regrouping. The director publishes bounded movement, reload, spread, target and cardinal formation intent only — it does not spawn, move, fire, damage or heal actors itself. Exact candidate `"
        + V133_CANDIDATE + "` passed Windows qualification run `" + V133_RUN + "`.",
        text,
        count=1,
        flags=re.S,
    )
    roadmap_section = (
        "## 🗺️ Roadmap & Quality Gates\n\n"
        f"The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **{p.completed} / {p.total} ({p.percent:.1f}%) — {p.status}**. Release readiness is tracked separately by exact-candidate Windows qualification gates.\n\n"
        "Recent qualified milestone layers include v13.0 encounter/boss warfare, v13.1 objective warfare, v13.2 adaptive enemy command and v13.3 battlefield cohesion. The qualified v13.3 exact-candidate gate required source/authority contracts, a clean Unity Windows x64 build, deterministic packaging and one exact packaged EXE running:\n\n"
        "- authored production-audio preflight with both HeavyCannon and BossAlarm;\n"
        "- v13.3 battlefield-cohesion smoke;\n"
        "- v13.2 adaptive-command regression;\n"
        "- v13.1 objective regression;\n"
        "- v13.0 encounter/cross-stack/boss regression;\n"
        "- v12.9 component/repair/casualty/presentation regression;\n"
        "- v12.8 full-stack integration regression;\n"
        "- late-round soak covering rounds 80 / 90 / 100.\n\n"
        f"The qualified v13.3 candidate is `{V133_CANDIDATE}`; Windows qualification run `{V133_RUN}` completed successfully. Roadmap qualification and public-release readiness remain separate.\n\n"
    )
    text = re.sub(
        r"## 🗺️ Roadmap & Quality Gates\n\n.*?(?=## 📦 Releases)",
        roadmap_section,
        text,
        count=1,
        flags=re.S,
    )
    text = re.sub(
        r"- \*\*Development milestone:\*\*.*",
        f"- **Development milestone:** v13.3 is qualified on `{branch}`; it has not been published as a new public v13.3 release.",
        text,
        count=1,
    )
    return text


def embed_docs(p: Progress) -> bool:
    changed = False
    roadmap_original = ROADMAP.read_text(encoding="utf-8")
    roadmap = strip_legacy_meters(roadmap_original)
    roadmap = replace_progress_embed(roadmap, p, mini=True)
    roadmap = roadmap.replace(
        "Update the checklist first, then badges, numbers, percentage and the 20-segment bar.",
        "Update the checklist first, then badges, numbers, percentage and the generated progress SVG.",
    )
    if roadmap != roadmap_original:
        ROADMAP.write_text(roadmap, encoding="utf-8")
        changed = True

    readme_original = README.read_text(encoding="utf-8")
    readme = strip_legacy_meters(readme_original)
    readme = replace_progress_embed(readme, p, mini=False)
    readme = sync_v133_readme(readme, p)
    if readme != readme_original:
        README.write_text(readme, encoding="utf-8")
        changed = True
    return changed


def validate_svg(name: str, content: str, p: Progress | None = None, track: float | None = None) -> None:
    try:
        root = ET.fromstring(content)
    except ET.ParseError as exc:
        fail(f"{name} is not valid XML: {exc}")
    if not root.tag.endswith("svg"):
        fail(f"{name} root element is not SVG")
    for color in ("#02050A", "#07111C", "#0088FF", "#62E5FF"):
        if color not in content:
            fail(f"{name} does not contain required SWIR color {color}")
    if name.endswith("template.svg"):
        if "TEMPLATE" not in content or "N/A" not in content or 'data-template="SWIR-PROGRESS-SVG-PRO:v1"' not in content:
            fail("progress-template.svg must be visibly and structurally marked TEMPLATE with N/A")
        return
    if p is None or track is None:
        fail(f"{name} validation requires verified progress data")
    if f"{p.percent:.1f}%" not in content or p.counter not in content or p.status not in content:
        fail(f"{name} visible/accessibility data disagrees with roadmap")
    expected_fill = width(track, p)
    match = re.search(r'data-role="progress-fill"[^>]* width="([0-9.]+)"', content)
    if not match or abs(float(match.group(1)) - expected_fill) > 0.01:
        fail(f"{name} fill width disagrees with verified progress")
    if float(match.group(1)) < 0.0 or float(match.group(1)) > track:
        fail(f"{name} fill width is outside bounded geometry")


def check_docs(p: Progress) -> None:
    roadmap = ROADMAP.read_text(encoding="utf-8")
    readme = README.read_text(encoding="utf-8")
    if LEGACY_METER_LINE.search(roadmap) or LEGACY_METER_FENCE.search(roadmap):
        fail("ROADMAP contains a legacy text progress meter")
    if LEGACY_METER_LINE.search(readme) or LEGACY_METER_FENCE.search(readme):
        fail("README contains a legacy text progress meter")
    if roadmap.count("assets/readme/progress-mini.svg") != 1:
        fail("ROADMAP must embed exactly one progress-mini.svg")
    if "assets/readme/progress-card.svg" in roadmap or "progress-template.svg" in roadmap:
        fail("ROADMAP must not embed progress-card.svg or progress-template.svg")
    if readme.count("assets/readme/progress-card.svg") != 1:
        fail("README must embed exactly one progress-card.svg")
    if "assets/readme/progress-mini.svg" in readme or "progress-template.svg" in readme:
        fail("README must not embed progress-mini.svg or progress-template.svg")
    if README_MARKER not in readme:
        fail("README PRO v2 marker missing")
    if "## 🔎 Search Keywords" not in readme:
        fail("README Search Keywords section missing")
    if p.fallback not in roadmap or p.fallback not in readme:
        fail("textual progress fallback is missing or stale")
    if "20-segment bar" in roadmap:
        fail("active roadmap rules still reference the legacy text meter")


def write_if_changed(path: Path, content: str) -> bool:
    old = path.read_text(encoding="utf-8") if path.exists() else None
    if old == content:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")
    return True


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--embed", action="store_true", help="migrate/embed SVG presentation and write generated assets")
    parser.add_argument("--check", action="store_true", help="verify docs/assets are deterministic and SVG-only")
    args = parser.parse_args()

    p = parse_progress(ROADMAP.read_text(encoding="utf-8"))
    changed = False
    if args.embed:
        changed |= embed_docs(p)
        p = parse_progress(ROADMAP.read_text(encoding="utf-8"))

    expected = {
        CARD: svg_card(p),
        MINI: svg_mini(p),
        TEMPLATE: svg_template(),
    }
    validate_svg(str(CARD), expected[CARD], p, TRACK_CARD)
    validate_svg(str(MINI), expected[MINI], p, TRACK_MINI)
    validate_svg(str(TEMPLATE), expected[TEMPLATE])

    if args.embed:
        for path, content in expected.items():
            changed |= write_if_changed(path, content)

    if args.check:
        check_docs(p)
        for path, content in expected.items():
            if not path.exists():
                fail(f"missing generated asset: {path}")
            actual = path.read_text(encoding="utf-8")
            if actual != content:
                fail(f"generated asset drift: {path}; run generator with --embed")
            validate_svg(str(path), actual, None if path == TEMPLATE else p, None if path == TEMPLATE else (TRACK_CARD if path == CARD else TRACK_MINI))

    print(
        f"SWIR Progress SVG Pro: {p.counter} ({p.percent:.1f}%) — {p.status}; "
        f"branch={branch_for_status(p.status)}; changed={'yes' if changed else 'no'}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
