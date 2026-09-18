#!/usr/bin/env python3
"""Register the honest v13.4 tactical-terrain scope and migrate progress to SVG-only presentation."""
from __future__ import annotations

from pathlib import Path
import re

ROADMAP = Path("ROADMAP.md")
GENERATOR = Path("Tools/generate_progress_svgs.py")

HEADING = "## v13.4 — Tactical Terrain & Cover Warfare — IN DEVELOPMENT"
ITEMS = [
    "**Deterministic 100-round tactical-terrain planner** — derive bounded terrain doctrine, cover budget, safe-lane width and cover mix for rounds 1–100 with anti-repeat signatures while TankGame remains round/arena authority.",
    "**Bounded tactical-cover runtime overlay** — add at most twelve planner-owned Brick/Steel/Water nodes through the canonical Obstacle component, fixed storage and collision-safe placement; no second structural-damage authority.",
    "**Permanent Orzełek and spawn egress safety lanes** — reserve a deterministic player/base corridor plus enemy spawn exits so terrain pressure never creates an unavoidable lockout or seals every route.",
    "**Cover-aware squad maneuver intent** — expose bounded cardinal cover/breach direction hints that existing EnemyTank/BattlefieldCohesion may consume without replacing Rigidbody2D, targeting, firing or path authority.",
    "**Canonical breach reaction and counterplay** — consume ReactiveCoverBreachDirector snapshots and Obstacle integrity only; AP/HE/Plasma remain deliberate breach tools and no duplicate cover HP/damage path is introduced.",
    "**Tactical terrain readability and telemetry** — publish doctrine, active cover, recent breaches, safe-lane width and deterministic signature with fixed HUD/presentation budgets.",
    "**Packaged-EXE v13.4 runtime smoke** — validate all 100 terrain plans, doctrine coverage, safe-lane invariants, fixed cover budgets, deterministic cover slots, breach/authority contracts, then rerun v13.3 regressions plus rounds 80/90/100 soak on the same executable.",
    "**Exact-SHA Windows qualification** — deterministically package one Windows x64 candidate with provenance, require the complete v13.4 gate to pass and only then finalize roadmap/checklist/progress SVGs.",
]


def die(message: str) -> None:
    raise RuntimeError(message)


def checklist_counts(text: str) -> tuple[int, int, int]:
    done = len(re.findall(r"^- \[x\] ", text, re.M))
    remaining = len(re.findall(r"^- \[ \] ", text, re.M))
    return done, remaining, done + remaining


def patch_generator() -> bool:
    text = GENERATOR.read_text(encoding="utf-8")
    original = text
    text = text.replace("    segments: int\n", "")
    legacy = '''    bar = re.search(r"^([█░]{20}) ([0-9.]+)%$", text, re.M)\n    if not bar:\n        fail("20-segment ROADMAP bar missing")\n    expected_segments = min(20, max(0, int(percent // 5)))\n    if percent >= 100.0:\n        expected_segments = 20\n    if bar.group(1).count("█") != expected_segments or float(bar.group(2)) != percent:\n        fail("ROADMAP text bar disagrees with documented checklist percentage")\n    return Progress(completed, remaining, total, percent, status, expected_segments)\n'''
    replacement = '''    # SWIR Progress SVG Pro v1 (2026-09-18 correction) retires legacy\n    # character meters. Numeric table + checklist remain the progress authority.\n    if re.search(r"(?m)^[█░]{8,}\\s+[0-9.]+%$", text):\n        fail("legacy character progress meter must not appear in active ROADMAP dashboard")\n    return Progress(completed, remaining, total, percent, status)\n'''
    if legacy in text:
        text = text.replace(legacy, replacement)
    elif "20-segment ROADMAP bar missing" in text:
        die("generator legacy-bar block changed unexpectedly")

    needle = '''    if README_MARKER not in readme or "## 🔎 Search Keywords" not in readme:\n        fail("README PRO v2 marker/Search Keywords must be preserved")\n'''
    strengthened = needle + '''    if re.search(r"(?m)^[█░]{8,}\\s+[0-9.]+%$", roadmap):\n        fail("legacy character progress meter returned to ROADMAP")\n'''
    if needle in text and "legacy character progress meter returned to ROADMAP" not in text:
        text = text.replace(needle, strengthened)

    if "20-segment ROADMAP bar missing" in text or "expected_segments" in text or "segments: int" in text:
        die("legacy bar logic remains in generator")
    if text != original:
        GENERATOR.write_text(text, encoding="utf-8")
        return True
    return False


def patch_roadmap() -> bool:
    text = ROADMAP.read_text(encoding="utf-8")
    original = text
    if len(re.findall(r"^<!-- SWIR-ROADMAP-STANDARD:v1 -->$", text, re.M)) != 1:
        die("SWIR Roadmap Standard v1 marker missing/duplicated as a protected standalone marker")

    done, remaining, total = checklist_counts(text)
    if HEADING not in text:
        if (done, remaining, total) != (415, 0, 415):
            die(f"unexpected pre-v13.4 checklist state: {(done, remaining,total)}")
        section = "\n\n" + HEADING + "\n" + "\n".join(f"- [ ] {item}" for item in ITEMS) + "\n"
        text = text.rstrip() + section

    done, remaining, total = checklist_counts(text)
    if (done, remaining, total) != (415, 8, 423):
        die(f"v13.4 checklist scope mismatch: {(done, remaining,total)}")
    pct = round(done * 100.0 / total, 1)
    if pct != 98.1:
        die(f"unexpected v13.4 progress: {pct}")

    text = re.sub(r"badge\.svg\?branch=dev-v13-\d+", "badge.svg?branch=dev-v13-4", text, count=1)
    text = re.sub(r"ROADMAP-[0-9.]+%25-[A-Za-z0-9]+", f"ROADMAP-{pct:.1f}%25-yellow", text, count=1)
    text = re.sub(r"DONE-[0-9]+%2F[0-9]+", f"DONE-{done}%2F{total}", text, count=1)
    text = re.sub(r"STATUS-V13\.3%20QUALIFIED-brightgreen", "STATUS-V13.4%20IN%20DEVELOPMENT-yellow", text, count=1)
    text = re.sub(
        r"\| \*\*[0-9]+\*\* \| \*\*[0-9]+\*\* \| \*\*[0-9]+\*\* \| \*\*[0-9.]+%\*\* \|",
        f"| **{done}** | **{remaining}** | **{total}** | **{pct:.1f}%** |",
        text,
        count=1,
    )

    text, removed = re.subn(r"\n```text\n[█░]{20} [0-9.]+%\n```\n", "\n", text, count=1)
    if removed == 0 and re.search(r"(?m)^[█░]{8,}\s+[0-9.]+%$", text):
        die("legacy meter exists but expected dashboard wrapper was not found")
    text = text.replace(
        "Update the checklist first, then badges, numbers, percentage and the 20-segment bar.",
        "Update the checklist first, then badges, numeric table, percentage and generated Progress SVG.",
    )
    if re.search(r"(?m)^[█░]{8,}\s+[0-9.]+%$", text):
        die("legacy character meter remains after migration")

    if text != original:
        ROADMAP.write_text(text, encoding="utf-8")
        return True
    return False


def verify() -> None:
    r = ROADMAP.read_text(encoding="utf-8")
    g = GENERATOR.read_text(encoding="utf-8")
    done, remaining, total = checklist_counts(r)
    assert (done, remaining, total) == (415, 8, 423)
    assert HEADING in r and r[r.index(HEADING):].count("- [ ]") == 8
    assert "DONE-415%2F423" in r and "ROADMAP-98.1%25-yellow" in r
    assert "STATUS-V13.4%20IN%20DEVELOPMENT-yellow" in r
    assert "| **415** | **8** | **423** | **98.1%** |" in r
    assert "badge.svg?branch=dev-v13-4" in r
    assert not re.search(r"(?m)^[█░]{8,}\s+[0-9.]+%$", r)
    assert "20-segment ROADMAP bar missing" not in g and "expected_segments" not in g
    assert "legacy character progress meter must not appear" in g


def main() -> int:
    changed = patch_generator() | patch_roadmap()
    verify()
    print("v13.4 scope + SVG-only progress migration:", "updated" if changed else "no-op")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
