#!/usr/bin/env python3
"""Idempotently register/verify the v13.3 roadmap scope without legacy meters."""
from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ROADMAP = ROOT / "ROADMAP.md"
README = ROOT / "README.md"
GENERATOR = ROOT / "Tools/generate_progress_svgs.py"
MARKER = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
HEADING = "## v13.3 — Battlefield Cohesion & Squad Command Warfare"
LEGACY_METER = re.compile(r"(?m)^[ \t]*[█▓▒░▉▊▋▌▍▎▏■□]{8,}(?:[ \t]+[0-9]+(?:\.[0-9]+)?%)?[ \t]*$")

PENDING_ITEMS = [
    "**Deterministic bounded squad roster** — assign eligible enemy tanks into fixed four-vehicle squads at spawn registration time with at most 24 tracked actors / six squads, no scene scans, no unbounded collections and no second spawn authority.",
    "**Leader, wingman, breacher and support roles** — maintain one effective leader per live squad, deterministic member roles and bounded leader promotion after casualties while existing EnemyTank / platoon systems retain movement, targeting and firing authority.",
    "**Cohesion state machine and regroup warfare** — expose Forming / Cohesive / Shocked / Regrouping squad states with hard timers and deterministic recovery so formations react to losses without frame-by-frame command thrashing.",
    "**Leader-loss counterplay** — destroying a squad leader creates a finite, readable cohesion shock that temporarily softens movement/fire-control intent and forces regroup before a promoted leader restores coordination; no hidden HP changes, enemy deletion or scripted stun authority.",
    "**Formation intent integration** — feed bounded spacing/regroup direction plus movement/reload/spread multipliers through existing EnemyTank and AdaptivePlatoonManeuver paths only; Rigidbody2D, Projectile, Health, ArmorSystem and TankGame remain canonical authorities.",
    "**Squad readability and tactical telemetry** — provide bounded world-space leader markers plus compact squad/cohesion/leader-loss telemetry with fixed refresh and actor budgets so the player can deliberately break enemy command structure.",
    "**Packaged-EXE v13.3 runtime smoke** — validate roster capacity, deterministic assignments, leader promotion, shock/regroup timing, bounded posture/formation intent and authority contracts, then rerun v13.2/v13.1/v13.0/v12.9/v12.8 regressions plus rounds 80/90/100 soak on the same executable.",
    "**Exact-SHA Windows qualification** — deterministically package one Windows x64 candidate with provenance, require the complete v13.3 gate to pass and only then finalize the SWIR roadmap dashboard/checklist and progress SVGs.",
]


def fail(message: str) -> None:
    raise SystemExit("v13.3 scope FAIL: " + message)


def counts(text: str) -> tuple[int, int, int, float]:
    checked = len(re.findall(r"^- \[x\] ", text, re.M))
    pending = len(re.findall(r"^- \[ \] ", text, re.M))
    total = checked + pending
    if total <= 0:
        fail("roadmap checklist scope is empty")
    return checked, pending, total, round(checked * 100.0 / total, 1)


def sync_open_readme(readme: str) -> str:
    replacements = {
        "Roadmap-100.0%25%20V13.2%20Qualified-02050A": "Roadmap-98.1%25%20V13.3%20In%20Development-02050A",
        "| Development milestone | **V13.2 QUALIFIED** on `dev-v13-2` |": "| Development milestone | **V13.3 IN DEVELOPMENT** on `dev-v13-3` |",
        "| Roadmap | **407 / 407 completed (100.0%)** — authoritative `ROADMAP.md` scope |": "| Roadmap | **407 / 415 completed (98.1%)** — authoritative `ROADMAP.md` scope |",
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **407 / 407 (100.0%) — V13.2 QUALIFIED**. Release readiness is tracked separately by exact-candidate Windows qualification gates.": "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **407 / 415 (98.1%) — V13.3 IN DEVELOPMENT**. The last qualified gameplay milestone is v13.2; release readiness remains separate from roadmap scope and is tracked by exact-candidate Windows qualification gates.",
        "- **Development milestone:** v13.2 is qualified on `dev-v13-2` but has not been published as a new public v13.2 release.": "- **Development milestone:** v13.3 is in development on `dev-v13-3`; v13.2 remains the last qualified milestone and has not been published as a new public v13.2 release.",
    }
    for old, new in replacements.items():
        if old in readme:
            readme = readme.replace(old, new, 1)
    return readme


def verify_current(text: str) -> None:
    if LEGACY_METER.search(text) or "20-segment bar" in text:
        fail("active ROADMAP contains a legacy text progress meter")
    checked, pending, total, pct = counts(text)
    if "— QUALIFIED" in text[text.index(HEADING):]:
        expected = (415, 0, 415, 100.0)
        required = (
            "ROADMAP-100.0%25-brightgreen", "DONE-415%2F415",
            "STATUS-V13.3%20QUALIFIED-brightgreen", "| **415** | **0** | **415** | **100.0%** |",
        )
        section = text.split(HEADING + " — QUALIFIED", 1)[1]
        if section.count("- [x]") != 8 or section.count("- [ ]") != 0:
            fail("qualified v13.3 section must contain eight checked items")
    else:
        expected = (407, 8, 415, 98.1)
        required = (
            "ROADMAP-98.1%25-yellow", "DONE-407%2F415",
            "STATUS-V13.3%20IN%20DEVELOPMENT-yellow", "| **407** | **8** | **415** | **98.1%** |",
        )
        section = text.split(HEADING + " — IN DEVELOPMENT", 1)[1]
        if section.count("- [ ]") != 8 or section.count("- [x]") != 0:
            fail("open v13.3 section must contain eight pending items")
    if (checked, pending, total, pct) != expected:
        fail(f"roadmap checklist mismatch: {(checked, pending, total, pct)} != {expected}")
    for token in required:
        if token not in text:
            fail(f"roadmap invariant missing: {token}")
    if text.count("assets/readme/progress-mini.svg") != 1:
        fail("ROADMAP must embed exactly one progress-mini.svg")


def main() -> None:
    text = ROADMAP.read_text(encoding="utf-8")
    if len(re.findall(r"^<!-- SWIR-ROADMAP-STANDARD:v1 -->$", text, re.M)) != 1:
        fail("expected exactly one standalone SWIR roadmap marker")

    if HEADING not in text:
        checked, pending, total, pct = counts(text)
        if (checked, pending, total, pct) != (407, 0, 407, 100.0):
            fail(f"expected qualified v13.2 baseline 407/407, got {checked}/{total}")
        for token in (
            "badge.svg?branch=dev-v13-2", "ROADMAP-100.0%25-brightgreen",
            "DONE-407%2F407", "STATUS-V13.2%20QUALIFIED-brightgreen",
            "| **407** | **0** | **407** | **100.0%** |",
        ):
            if token not in text:
                fail(f"qualified v13.2 dashboard token missing: {token}")
        text = text.replace("badge.svg?branch=dev-v13-2", "badge.svg?branch=dev-v13-3", 1)
        text = re.sub(r"ROADMAP-100\.0%25-brightgreen", "ROADMAP-98.1%25-yellow", text, count=1)
        text = re.sub(r"DONE-407%2F407-[A-Za-z0-9]+", "DONE-407%2F415-1f6feb", text, count=1)
        text = re.sub(r"STATUS-V13\.2%20QUALIFIED-brightgreen", "STATUS-V13.3%20IN%20DEVELOPMENT-yellow", text, count=1)
        text = text.replace("| **407** | **0** | **407** | **100.0%** |", "| **407** | **8** | **415** | **98.1%** |", 1)
        text = text.rstrip() + "\n\n\n" + HEADING + " — IN DEVELOPMENT\n" + "\n".join(f"- [ ] {item}" for item in PENDING_ITEMS) + "\n"
        ROADMAP.write_text(text, encoding="utf-8")
        readme = README.read_text(encoding="utf-8")
        if "<!-- SWIR-README-STANDARD:v2 -->" not in readme or "## 🔎 Search Keywords" not in readme:
            fail("README PRO v2/Search Keywords contract missing")
        README.write_text(sync_open_readme(readme), encoding="utf-8")

    subprocess.run([sys.executable, str(GENERATOR), "--embed"], cwd=ROOT, check=True)
    subprocess.run([sys.executable, str(GENERATOR), "--check"], cwd=ROOT, check=True)
    final_text = ROADMAP.read_text(encoding="utf-8")
    verify_current(final_text)
    checked, pending, total, pct = counts(final_text)
    state = "QUALIFIED" if pending == 0 else "IN DEVELOPMENT"
    print(f"v13.3 scope consistent: {checked}/{total} = {pct:.1f}%, {state}; SVG-only presentation verified")


if __name__ == "__main__":
    main()
