#!/usr/bin/env python3
"""Finalize v13.5 docs only after the pinned exact Windows candidate has qualified."""
from __future__ import annotations

import argparse
import re
from pathlib import Path

README = Path("README.md")
ROADMAP = Path("ROADMAP.md")
EVIDENCE = Path("docs/qualification/V13_5_WINDOWS_QUALIFICATION.md")


def fail(message: str) -> None:
    raise SystemExit("v13.5 finalizer FAIL: " + message)


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        fail(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


def finalize_roadmap(text: str) -> str:
    text = replace_once(text, "ROADMAP-98.1%25-yellow", "ROADMAP-100.0%25-brightgreen", "roadmap badge")
    text = replace_once(text, "DONE-423%2F431-1f6feb", "DONE-431%2F431-1f6feb", "done badge")
    text = replace_once(text, "STATUS-V13.5%20IN%20DEVELOPMENT-yellow", "STATUS-V13.5%20QUALIFIED-brightgreen", "status badge")
    text = replace_once(text,
        "Roadmap progress: 423 / 431 completed (98.1%) — V13.5 IN DEVELOPMENT. Release readiness is tracked separately by Windows qualification gates.",
        "Roadmap progress: 431 / 431 completed (100.0%) — V13.5 QUALIFIED. Release readiness is tracked separately by Windows qualification gates.",
        "roadmap numeric summary")
    text = replace_once(text, "| **423** | **8** | **431** | **98.1%** |", "| **431** | **0** | **431** | **100.0%** |", "roadmap table")
    text = replace_once(text,
        "## v13.5 — Battlefield Weather & Visibility Warfare — IN DEVELOPMENT",
        "## v13.5 — Battlefield Weather & Visibility Warfare — QUALIFIED",
        "v13.5 heading")
    section_start = text.index("## v13.5 — Battlefield Weather & Visibility Warfare — QUALIFIED")
    section = text[section_start:]
    if section.count("- [ ] ") != 8 or section.count("- [x] ") != 0:
        fail("v13.5 checklist is not the expected eight-item open scope")
    section = section.replace("- [ ] ", "- [x] ")
    return text[:section_start] + section


def finalize_readme(text: str, candidate_sha: str, run_id: str) -> str:
    text = replace_once(text,
        "[![Roadmap](https://img.shields.io/badge/Roadmap-98.1%25%20V13.5%20In%20Development-02050A?style=for-the-badge&logo=github&logoColor=62E5FF)](ROADMAP.md)",
        "[![Roadmap](https://img.shields.io/badge/Roadmap-100.0%25%20V13.5%20Qualified-02050A?style=for-the-badge&logo=github&logoColor=62E5FF)](ROADMAP.md)",
        "README roadmap badge")
    text = replace_once(text,
        "[![v13.4 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/tactical-terrain-v134-windows.yml/badge.svg?branch=dev-v13-4)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/tactical-terrain-v134-windows.yml)",
        "[![v13.5 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-weather-v135-windows.yml/badge.svg?branch=dev-v13-5)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-weather-v135-windows.yml)",
        "README Windows gate badge")
    text = replace_once(text,
        "Roadmap progress: 423 / 431 completed (98.1%) — V13.5 IN DEVELOPMENT. Release readiness is tracked separately by Windows qualification gates.",
        "Roadmap progress: 431 / 431 completed (100.0%) — V13.5 QUALIFIED. Release readiness is tracked separately by Windows qualification gates.",
        "README numeric summary")
    text = replace_once(text, "| Development milestone | **V13.5 IN DEVELOPMENT** on `dev-v13-5` |", "| Development milestone | **V13.5 QUALIFIED** on `dev-v13-5` |", "README milestone")
    text = replace_once(text, "| Roadmap | **423 / 431 completed (98.1%)** — authoritative `ROADMAP.md` scope |", "| Roadmap | **431 / 431 completed (100.0%)** — authoritative `ROADMAP.md` scope |", "README roadmap row")
    text = replace_once(text, "| Latest qualified milestone | **v13.4** — exact Windows candidate qualified |", "| Latest qualified milestone | **v13.5** — exact Windows candidate qualified |", "README qualified row")

    terrain_highlight = "| 🧱 **Tactical terrain v13.4 — qualified** | Deterministic cover overlays add bounded brick, steel and water layouts, safe-route preservation and breach-aware squad movement while canonical `Obstacle` damage authority remains unchanged. |"
    text = replace_once(text, terrain_highlight, terrain_highlight + "\n| 🌦️ **Battlefield weather v13.5 — qualified** | Deterministic Clear, Mist, Rain, Storm and Snow fronts change bounded traction, visibility, spread and reload pressure while AP/Plasma retain measured precision counterplay and existing movement/ballistics authorities remain canonical. |", "README weather highlight")
    text = replace_once(text,
        "| 🧪 **Exact-candidate qualification** | v13.4 passed one packaged Windows EXE through production-audio preflight, v13.4 smoke, v13.3/v13.2/v13.1/v13.0/v12.9/v12.8 regressions and late-round 80/90/100 soak. |",
        "| 🧪 **Exact-candidate qualification** | v13.5 passed one packaged Windows EXE through production-audio preflight, v13.5 weather smoke, v13.4/v13.3 regressions and late-round 80/90/100 soak. |",
        "README exact gate highlight")

    terrain_section = "### Tactical Terrain & Cover Warfare v13.4 — qualified\n\nThe v13.4 branch adds a deterministic tactical overlay built from canonical `Obstacle` components. Per-round planning selects among seven terrain doctrines, caps the overlay at 12 cover nodes chosen from 24 bounded candidate slots, preserves the Orzełek/player corridor and enemy spawn exits, and mixes brick, steel and water within explicit budgets. `EnemyTank` receives only a bounded cardinal breach-aware direction hint; structural damage remains owned by `Obstacle`, spawning by `TankGame`, and movement/fire by existing enemy logic. The exact Windows candidate passed the full production-audio, v13.4 terrain, historical regression and rounds 80/90/100 soak matrix while preserving the existing gameplay authority boundaries.\n"
    weather_section = terrain_section + "\n### Battlefield Weather & Visibility Warfare v13.5 — qualified\n\nThe v13.5 layer deterministically assigns Clear, Mist, Rain, Storm or Snow across the 100-round campaign. Weather supplies bounded traction, visibility, spread and enemy-reload multipliers through the existing `PlayerTank`, `EnemyTank`, `Rigidbody2D` and fire-control paths; it does not create a second movement, projectile, damage or round authority. Rain, Snow and Storm couple conservatively to canonical `TacticalTerrainMap` surfaces, AP/Plasma reduce only the player spread penalty, and presentation is capped at 24 deterministic weather streaks. The exact Windows candidate passed the authored-audio preflight, v13.5 weather smoke, v13.4/v13.3 regressions and rounds 80/90/100 soak on the same executable.\n"
    text = replace_once(text, terrain_section, weather_section, "README v13.5 gameplay section")

    text = replace_once(text,
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **423 / 431 (98.1%) — V13.5 IN DEVELOPMENT**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **431 / 431 (100.0%) — V13.5 QUALIFIED**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "README roadmap summary")
    v134_evidence = "The qualified v13.4 candidate is `ef891049f2c40bf797b4b6e7f38b12c1c25ec407`; Windows qualification run `35336689226` completed successfully. The same packaged EXE passed production authored-audio preflight, v13.4 tactical-terrain smoke, v13.3/v13.2/v13.1/v13.0/v12.9/v12.8 regressions and rounds 80/90/100 soak."
    text = replace_once(text, v134_evidence, v134_evidence + f"\n\nThe qualified v13.5 candidate is `{candidate_sha}`; Windows qualification run `{run_id}` completed successfully. The same packaged EXE passed production authored-audio preflight, v13.5 Battlefield Weather smoke, v13.4 tactical-terrain regression, v13.3 battlefield-cohesion regression and rounds 80/90/100 soak.", "README qualification evidence")
    text = replace_once(text,
        "- **Development milestone:** v13.4 is qualified on `dev-v13-4`; it remains a development milestone and has not been published as a new public v13.4 release.",
        "- **Development milestone:** v13.5 is qualified on `dev-v13-5`; it remains a development milestone and has not been published as a new public v13.5 release.",
        "README releases status")
    text = replace_once(text, "`tactical terrain game` • `boss tank battles`", "`tactical terrain game` • `battlefield weather game` • `boss tank battles`", "README search keywords")
    return text


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--candidate-sha", required=True)
    ap.add_argument("--run-id", required=True)
    ap.add_argument("--candidate-zip-sha256", required=True)
    ap.add_argument("--artifact-sha256", required=True)
    args = ap.parse_args()
    if not re.fullmatch(r"[0-9a-f]{40}", args.candidate_sha):
        fail("candidate SHA must be a full lowercase Git SHA")
    for value, name in ((args.candidate_zip_sha256, "candidate ZIP"), (args.artifact_sha256.removeprefix("sha256:"), "artifact")):
        if not re.fullmatch(r"[0-9a-f]{64}", value):
            fail(f"{name} SHA-256 invalid")

    ROADMAP.write_text(finalize_roadmap(ROADMAP.read_text(encoding="utf-8")), encoding="utf-8")
    README.write_text(finalize_readme(README.read_text(encoding="utf-8"), args.candidate_sha, args.run_id), encoding="utf-8")
    EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
    EVIDENCE.write_text(
        "# v13.5 Windows Qualification Evidence\n\n"
        "This file records the exact candidate that justified closing the v13.5 roadmap scope. It does not by itself publish a public release.\n\n"
        f"- Candidate commit: `{args.candidate_sha}`\n"
        f"- Qualification run: `{args.run_id}` — `success`\n"
        "- Workflow: `Battlefield Weather v13.5 Windows Qualification Gate`\n"
        "- Unity / platform: `6000.3.17f1` / `Windows x64`\n"
        f"- Candidate ZIP SHA-256: `{args.candidate_zip_sha256}`\n"
        f"- GitHub artifact digest: `{args.artifact_sha256}`\n"
        "- Runtime matrix: production authored-audio preflight; v13.5 Battlefield Weather; v13.4 Tactical Terrain; v13.3 Battlefield Cohesion; rounds 80/90/100 soak — all PASS.\n"
        "- Public release status: unchanged; roadmap qualification and release readiness/publication remain separate.\n",
        encoding="utf-8")
    print("v13.5 roadmap/README qualification transition prepared: 431/431")


if __name__ == "__main__":
    main()
