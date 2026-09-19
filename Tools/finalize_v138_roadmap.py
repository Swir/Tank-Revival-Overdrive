#!/usr/bin/env python3
"""Close the verified v13.8 roadmap scope without conflating release readiness."""
from __future__ import annotations

import argparse
import re
from pathlib import Path

ROADMAP = Path("ROADMAP.md")
README = Path("README.md")
EVIDENCE = Path("docs/qualification/V13_8_WINDOWS_QUALIFICATION.md")

CANDIDATE_SHA = "0805a27a7a69a0d620e96f64e6cac66185be6547"
QUAL_RUN_ID = "35413636894"
QUAL_WORKFLOW = "Battlefield Suppression v13.8 Windows Qualification Gate"
ARTIFACT_ID = "10575925003"
ARTIFACT_NAME = "TankRevivalOverdrive-v13.8-suppression-morale-Windows-x64"
ARTIFACT_BYTES = 60261926
ARTIFACT_DIGEST = "sha256:4f6e98ee70dfb6d888bff7aba70075fd26977006b5833967da82dbc99b9ce6fa"
SOURCE_RUN_ID = "35394513424"
TYPE_GUARD_RUN_ID = "35394513425"


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


def finalize_roadmap(text: str) -> str:
    done = len(re.findall(r"^- \[x\] ", text, re.M))
    open_ = len(re.findall(r"^- \[ \] ", text, re.M))
    if (done, open_, done + open_) != (447, 8, 455):
        raise SystemExit(
            f"unexpected pre-finalization roadmap state: {done}/{done + open_} with {open_} open"
        )

    text = replace_once(text, "ROADMAP-98.2%25-blue", "ROADMAP-100.0%25-brightgreen", "roadmap badge")
    text = replace_once(text, "DONE-447%2F455-1f6feb", "DONE-455%2F455-1f6feb", "done badge")
    text = replace_once(
        text,
        "STATUS-V13.8%20IN%20DEVELOPMENT-blue",
        "STATUS-V13.8%20QUALIFIED-brightgreen",
        "status badge",
    )
    text = replace_once(
        text,
        "| **447** | **8** | **455** | **98.2%** |",
        "| **455** | **0** | **455** | **100.0%** |",
        "roadmap table",
    )

    heading = "## v13.8 — Battlefield Suppression & Morale Warfare — IN DEVELOPMENT"
    qualified = "## v13.8 — Battlefield Suppression & Morale Warfare — QUALIFIED"
    text = replace_once(text, heading, qualified, "v13.8 heading")
    start = text.index(qualified)
    section = text[start:]
    if section.count("- [ ]") != 8 or section.count("- [x]") != 0:
        raise SystemExit("v13.8 section must contain exactly eight open and zero completed items")
    text = text[:start] + section.replace("- [ ]", "- [x]")

    done2 = len(re.findall(r"^- \[x\] ", text, re.M))
    open2 = len(re.findall(r"^- \[ \] ", text, re.M))
    if (done2, open2, done2 + open2) != (455, 0, 455):
        raise SystemExit(f"unexpected finalized checklist state: {done2}/{done2 + open2} with {open2} open")
    return text


def finalize_readme(text: str) -> str:
    text = replace_once(
        text,
        "Roadmap-98.2%25%20V13.8%20In%20Development",
        "Roadmap-100.0%25%20V13.8%20Qualified",
        "README roadmap badge",
    )
    text = replace_once(
        text,
        "[![v13.7 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/late-round-performance-v137-windows.yml/badge.svg?branch=dev-v13-7)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/late-round-performance-v137-windows.yml)",
        "[![v13.8 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-suppression-v138-windows.yml/badge.svg?branch=dev-v13-8)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-suppression-v138-windows.yml)",
        "README Windows gate badge",
    )
    text = replace_once(
        text,
        "| Development milestone | **V13.8 IN DEVELOPMENT** on `dev-v13-8` |",
        "| Development milestone | **V13.8 QUALIFIED** on `dev-v13-8` |",
        "README milestone row",
    )
    text = replace_once(
        text,
        "| Roadmap | **447 / 455 completed (98.2%)** — authoritative `ROADMAP.md` scope |",
        "| Roadmap | **455 / 455 completed (100.0%)** — authoritative `ROADMAP.md` scope |",
        "README roadmap row",
    )
    text = replace_once(
        text,
        "| Latest qualified milestone | **v13.7** — exact Windows candidate `dc646fc60be572e625f5ea310058aab10d2f26da` qualified in run `35393272731` |",
        f"| Latest qualified milestone | **v13.8** — exact Windows candidate `{CANDIDATE_SHA}` qualified in run `{QUAL_RUN_ID}` |",
        "README latest qualified row",
    )
    text = replace_once(
        text,
        "| 💢 **Battlefield suppression v13.8 — in development** | Player impacts and near misses build bounded enemy suppression that affects only existing movement/reload/spread paths, with ammo-aware pressure, cohesion recovery and no bonus damage or permanent stun. |",
        "| 💢 **Battlefield suppression v13.8 — qualified** | Player impacts and near misses build bounded Steady/Suppressed/Pinned pressure that affects only existing movement/reload/spread paths, with ammo-aware pressure, cohesion-aware recovery, boss resistance and no bonus damage or permanent stun. |",
        "README v13.8 highlight",
    )
    text = replace_once(
        text,
        "| 🧪 **Exact-candidate qualification** | v13.7 passed one packaged Windows EXE through production-audio preflight, v13.7 performance smoke, v13.6/v13.5/v13.4/v13.3 regressions and late-round 80/90/100 soak. |",
        "| 🧪 **Exact-candidate qualification** | v13.8 passed one packaged Windows EXE through production-audio preflight, v13.8 suppression/morale smoke, v13.7/v13.6/v13.5/v13.4/v13.3 regressions and late-round 80/90/100 soak. |",
        "README qualification highlight",
    )
    text = replace_once(
        text,
        "### Battlefield Suppression & Morale Warfare v13.8 — in development",
        "### Battlefield Suppression & Morale Warfare v13.8 — qualified",
        "README v13.8 heading",
    )
    old_body = (
        "v13.8 adds a bounded information-and-behavior layer for enemy suppression without creating a second damage, movement or firing authority. Existing player-owned Projectile impacts and near misses build finite pressure with ammo-aware profiles; EnemyTank consumes only clamped movement/reload/spread multipliers, while v13.3 cohesion and class resistance govern recovery. The implementation is required to stay fixed-cap, avoid hot-path scene scans, preserve enemy counts and keep bosses resistant rather than immune. The exact Windows gate must prove the same packaged EXE still passes v13.7+ regressions and rounds 80/90/100 soak before any v13.8 checkbox can close."
    )
    new_body = (
        f"v13.8 adds a bounded information-and-behavior layer for enemy suppression without creating a second damage, movement or firing authority. Existing player-owned Projectile impacts and near misses build finite Steady / Suppressed / Pinned pressure with ammo-aware profiles; EnemyTank consumes only clamped movement/reload/spread multipliers, while v13.3 cohesion and class resistance govern recovery. The runtime remains fixed-cap, avoids hot-path scene scans, preserves enemy counts and keeps bosses resistant rather than immune. Exact candidate `{CANDIDATE_SHA}` passed Windows qualification run `{QUAL_RUN_ID}`, including production authored-audio preflight, v13.8 suppression/morale smoke, v13.7/v13.6/v13.5/v13.4/v13.3 regressions and rounds 80/90/100 soak on the same packaged executable."
    )
    text = replace_once(text, old_body, new_body, "README v13.8 body")
    text = replace_once(
        text,
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **447 / 455 (98.2%) — V13.8 IN DEVELOPMENT**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **455 / 455 (100.0%) — V13.8 QUALIFIED**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "README roadmap summary",
    )
    v137_evidence = (
        "The qualified v13.7 candidate is `dc646fc60be572e625f5ea310058aab10d2f26da`; Windows qualification run `35393272731` completed successfully. The same packaged EXE passed production authored-audio preflight, v13.7 late-round-performance smoke, v13.6/v13.5/v13.4/v13.3 regressions and rounds 80/90/100 soak. Its deterministic candidate ZIP SHA-256 is `0328a6613dcc64dffb1f675282e27a798b0f3abcb561e13423b7b6de3d75c25f`."
    )
    v138_evidence = (
        v137_evidence
        + f"\n\nThe qualified v13.8 candidate is `{CANDIDATE_SHA}`; Windows qualification run `{QUAL_RUN_ID}` completed successfully. The same packaged EXE passed production authored-audio preflight, v13.8 suppression/morale smoke, v13.7/v13.6/v13.5/v13.4/v13.3 regressions and rounds 80/90/100 soak. The retained Windows artifact `{ARTIFACT_NAME}` has GitHub artifact digest `{ARTIFACT_DIGEST}`."
    )
    text = replace_once(text, v137_evidence, v138_evidence, "README qualification evidence")
    text = replace_once(
        text,
        "- **Development milestone:** v13.8 is in development on `dev-v13-8`; v13.7 remains the latest qualified development milestone and neither state automatically creates or replaces a public release.",
        "- **Development milestone:** v13.8 is qualified on `dev-v13-8`; this milestone qualification does not automatically create or replace a public release.",
        "README release status",
    )
    return text


def write_evidence(finalizer_run_id: str) -> None:
    EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
    EVIDENCE.write_text(
        f"""# v13.8 Windows Qualification Evidence

Status: **QUALIFIED**

This document records the exact-candidate evidence used to close the v13.8 Battlefield Suppression & Morale Warfare roadmap scope. Milestone qualification is not the same as public GitHub Release readiness.

| Field | Verified value |
|---|---|
| Candidate commit | `{CANDIDATE_SHA}` |
| Qualification workflow | `{QUAL_WORKFLOW}` |
| Qualification run | `{QUAL_RUN_ID}` — SUCCESS |
| Source qualification run | `{SOURCE_RUN_ID}` — SUCCESS |
| C# type uniqueness run | `{TYPE_GUARD_RUN_ID}` — SUCCESS |
| Unity | `6000.3.17f1` |
| Platform | Windows x64 |
| Candidate artifact | `{ARTIFACT_NAME}` |
| Candidate artifact ID | `{ARTIFACT_ID}` |
| Candidate artifact bytes | `{ARTIFACT_BYTES}` |
| GitHub artifact digest | `{ARTIFACT_DIGEST}` |
| Finalizer run | `{finalizer_run_id}` |

## Same-executable qualification matrix

The exact packaged candidate passed the production authored-audio presentation preflight, v13.8 suppression/morale smoke, v13.7 late-round-performance regression, v13.6 sensor-fusion regression, v13.5 battlefield-weather regression, v13.4 tactical-terrain regression, v13.3 battlefield-cohesion regression and the rounds 80/90/100 soak regression.

The v13.8 layer changes bounded suppression/morale intent only. Qualification does not authorize bonus damage, hidden Health mutation, alternate projectile/spawn/movement authority, permanent crowd control, enemy deletion, reduced enemy counts or suppression of gameplay events.
""",
        encoding="utf-8",
    )


def already_finalized(roadmap: str, readme: str) -> bool:
    return (
        "STATUS-V13.8%20QUALIFIED-brightgreen" in roadmap
        and "| **455** | **0** | **455** | **100.0%** |" in roadmap
        and "## v13.8 — Battlefield Suppression & Morale Warfare — QUALIFIED" in roadmap
        and roadmap.count("- [ ]") == 0
        and "**V13.8 QUALIFIED** on `dev-v13-8`" in readme
        and f"exact Windows candidate `{CANDIDATE_SHA}` qualified in run `{QUAL_RUN_ID}`" in readme
    )


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--qualified-sha", required=True)
    ap.add_argument("--qualified-run", required=True)
    ap.add_argument("--finalizer-run", default="local")
    args = ap.parse_args()
    if args.qualified_sha != CANDIDATE_SHA or args.qualified_run != QUAL_RUN_ID:
        raise SystemExit("refusing to finalize from unapproved qualification evidence")

    roadmap = ROADMAP.read_text(encoding="utf-8")
    readme = README.read_text(encoding="utf-8")
    if already_finalized(roadmap, readme):
        if not EVIDENCE.is_file():
            write_evidence(args.finalizer_run)
        print("v13.8 roadmap already finalized and exact evidence matches")
        return 0

    ROADMAP.write_text(finalize_roadmap(roadmap), encoding="utf-8")
    README.write_text(finalize_readme(readme), encoding="utf-8")
    write_evidence(args.finalizer_run)
    print("finalized v13.8 roadmap scope: 455/455 (100.0%) V13.8 QUALIFIED")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
