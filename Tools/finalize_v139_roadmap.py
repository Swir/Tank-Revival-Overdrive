#!/usr/bin/env python3
"""Finalize the exact-qualified v13.9 Fire-Support milestone without conflating release readiness."""
from __future__ import annotations

import argparse
import re
from pathlib import Path

ROADMAP = Path("ROADMAP.md")
README = Path("README.md")
EVIDENCE = Path("docs/qualification/V13_9_WINDOWS_QUALIFICATION.md")

CANDIDATE_SHA = "30511fb9452dfb47e481ecb57f87ccd51aded1fa"
QUAL_RUN_ID = "35462501123"
QUAL_WORKFLOW = "Fire Support v13.9 Windows Qualification Gate"
ARTIFACT_ID = "10590695610"
ARTIFACT_NAME = "TankRevivalOverdrive-v13.9-fire-support-Windows-x64"
ARTIFACT_BYTES = 60268470
ARTIFACT_DIGEST = "sha256:7af94e0ef1032ccd7309740da061cd3e1351dde3e4fe06a139ab05a733db872d"
SOURCE_RUN_ID = "35462501080"
TYPE_GUARD_RUN_ID = "35462501077"


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


def finalize_roadmap(text: str) -> str:
    done = len(re.findall(r"^- \[x\] ", text, re.M))
    open_ = len(re.findall(r"^- \[ \] ", text, re.M))
    if (done, open_, done + open_) != (455, 8, 463):
        raise SystemExit(f"unexpected pre-finalization roadmap state: {done}/{done + open_} with {open_} open")

    text = replace_once(text, "ROADMAP-98.3%25-blue", "ROADMAP-100.0%25-brightgreen", "roadmap badge")
    text = replace_once(text, "DONE-455%2F463-1f6feb", "DONE-463%2F463-1f6feb", "done badge")
    text = replace_once(
        text,
        "STATUS-V13.9%20IN%20DEVELOPMENT-blue",
        "STATUS-V13.9%20QUALIFIED-brightgreen",
        "status badge",
    )
    text = replace_once(
        text,
        "| **455** | **8** | **463** | **98.3%** |",
        "| **463** | **0** | **463** | **100.0%** |",
        "roadmap table",
    )

    heading = "## v13.9 — Suppression Counterplay & Tactical Fire-Support Command — IN DEVELOPMENT"
    qualified = "## v13.9 — Suppression Counterplay & Tactical Fire-Support Command — QUALIFIED"
    text = replace_once(text, heading, qualified, "v13.9 heading")
    start = text.index(qualified)
    next_heading = text.find("\n## ", start + len(qualified))
    end = len(text) if next_heading < 0 else next_heading
    section = text[start:end]
    if section.count("- [ ]") != 8 or section.count("- [x]") != 0:
        raise SystemExit("v13.9 section must contain exactly eight open and zero completed items")
    text = text[:start] + section.replace("- [ ]", "- [x]") + text[end:]

    done2 = len(re.findall(r"^- \[x\] ", text, re.M))
    open2 = len(re.findall(r"^- \[ \] ", text, re.M))
    if (done2, open2, done2 + open2) != (463, 0, 463):
        raise SystemExit(f"unexpected finalized checklist state: {done2}/{done2 + open2} with {open2} open")
    return text


def finalize_readme(text: str) -> str:
    text = replace_once(
        text,
        "Roadmap-98.3%25%20V13.9%20In%20Development",
        "Roadmap-100.0%25%20V13.9%20Qualified",
        "README roadmap badge",
    )
    text = replace_once(
        text,
        "[![v13.8 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-suppression-v138-windows.yml/badge.svg?branch=dev-v13-8)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-suppression-v138-windows.yml)",
        "[![v13.9 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-fire-support-v139-windows.yml/badge.svg?branch=dev-v13-9)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-fire-support-v139-windows.yml)",
        "README Windows gate badge",
    )
    text = replace_once(
        text,
        "| Development milestone | **V13.9 IN DEVELOPMENT** on `dev-v13-9` |",
        "| Development milestone | **V13.9 QUALIFIED** on `dev-v13-9` |",
        "README milestone row",
    )
    text = replace_once(
        text,
        "| Roadmap | **455 / 463 completed (98.3%)** — authoritative `ROADMAP.md` scope |",
        "| Roadmap | **463 / 463 completed (100.0%)** — authoritative `ROADMAP.md` scope |",
        "README roadmap row",
    )
    text = replace_once(
        text,
        "| Latest qualified milestone | **v13.8** — exact Windows candidate `0805a27a7a69a0d620e96f64e6cac66185be6547` qualified in run `35413636894` |",
        f"| Latest qualified milestone | **v13.9** — exact Windows candidate `{CANDIDATE_SHA}` qualified in run `{QUAL_RUN_ID}` |",
        "README latest qualified row",
    )
    text = replace_once(
        text,
        "| 🎯 **Fire-support counterplay v13.9 — in development** | Planned bounded command windows will turn qualified suppression, cover, cohesion and contact confidence into readable tactical opportunities and class-aware reactions without creating a second damage/projectile authority. |",
        "| 🎯 **Fire-support counterplay v13.9 — qualified** | Earned F-key command windows combine qualified suppression, cover, cohesion and verified contacts into bounded fire-support intent with class-aware reactions, anti-spam cooldowns and canonical TankGame projectile execution. |",
        "README v13.9 highlight",
    )
    text = replace_once(
        text,
        "| 🧪 **Exact-candidate qualification** | v13.8 passed one packaged Windows EXE through production-audio preflight, v13.8 suppression/morale smoke, v13.7/v13.6/v13.5/v13.4/v13.3 regressions and late-round 80/90/100 soak. |",
        "| 🧪 **Exact-candidate qualification** | v13.9 passed one packaged Windows EXE through production-audio preflight, v13.9 fire-support smoke, v13.8/v13.7/v13.6 regressions and late-round 80/90/100 soak. |",
        "README qualification highlight",
    )
    text = replace_once(
        text,
        "| **C** | Active sensor sweep when v13.6 sensor fusion is available and off cooldown |",
        "| **C** | Active sensor sweep when v13.6 sensor fusion is available and off cooldown |\n| **F** | Activate earned v13.9 tactical fire-support when READY and a verified contact opportunity exists |",
        "README fire-support control",
    )
    text = replace_once(
        text,
        "### Suppression Counterplay & Tactical Fire-Support Command v13.9 — in development",
        "### Suppression Counterplay & Tactical Fire-Support Command v13.9 — qualified",
        "README v13.9 heading",
    )
    old_body = (
        "v13.9 is being developed as a bounded command-and-counterplay layer above the qualified v13.8 suppression system. The planned director may publish finite support-window and enemy-reaction intent derived from suppression, terrain, cohesion and sensor confidence, but existing `TankGame`, `EnemyTank`, `Rigidbody2D`, `Projectile` and `Health` systems remain the canonical gameplay authorities. The milestone is not qualified until one exact packaged Windows candidate passes its dedicated smoke, historical regressions and rounds 80/90/100 soak."
    )
    new_body = (
        f"v13.9 adds a bounded command-and-counterplay layer above the qualified v13.8 suppression system. The intent-only `BattlefieldFireSupportDirector` earns finite READY / ACTIVE / COOLDOWN windows from suppression pressure, v13.4 terrain exposure, v13.3 cohesion and v13.6 verified contacts; the player activates support with **F** while a separate execution bridge forwards strike intent only through canonical `TankGame.SpawnProjectile`. Class-aware Hold / Brace / Disperse / Evade reactions remain bounded and `EnemyTank`, `Rigidbody2D`, `Projectile` and `Health` retain their existing authorities. Exact candidate `{CANDIDATE_SHA}` passed Windows qualification run `{QUAL_RUN_ID}`, including production authored-audio preflight, v13.9 fire-support smoke, v13.8/v13.7/v13.6 regressions and rounds 80/90/100 soak on the same packaged executable."
    )
    text = replace_once(text, old_body, new_body, "README v13.9 body")
    text = replace_once(
        text,
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **455 / 455 (100.0%) — V13.8 QUALIFIED**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **463 / 463 (100.0%) — V13.9 QUALIFIED**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "README roadmap summary",
    )
    v138_evidence = (
        "The qualified v13.8 candidate is `0805a27a7a69a0d620e96f64e6cac66185be6547`; Windows qualification run `35413636894` completed successfully. The same packaged EXE passed production authored-audio preflight, v13.8 suppression/morale smoke, v13.7/v13.6/v13.5/v13.4/v13.3 regressions and rounds 80/90/100 soak. The retained Windows artifact `TankRevivalOverdrive-v13.8-suppression-morale-Windows-x64` has GitHub artifact digest `sha256:4f6e98ee70dfb6d888bff7aba70075fd26977006b5833967da82dbc99b9ce6fa`."
    )
    v139_evidence = (
        v138_evidence
        + f"\n\nThe qualified v13.9 candidate is `{CANDIDATE_SHA}`; Windows qualification run `{QUAL_RUN_ID}` completed successfully. The same packaged EXE passed production authored-audio preflight, v13.9 fire-support smoke, v13.8/v13.7/v13.6 regressions and rounds 80/90/100 soak. The retained Windows artifact `{ARTIFACT_NAME}` has GitHub artifact digest `{ARTIFACT_DIGEST}`."
    )
    text = replace_once(text, v138_evidence, v139_evidence, "README qualification evidence")
    text = replace_once(
        text,
        "- **Development milestone:** v13.8 is qualified on `dev-v13-8`; this milestone qualification does not automatically create or replace a public release.",
        "- **Development milestone:** v13.9 is qualified on `dev-v13-9`; this milestone qualification does not automatically create or replace a public release.",
        "README release status",
    )
    return text


def write_evidence(finalizer_run_id: str) -> None:
    EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
    EVIDENCE.write_text(
        f"""# v13.9 Windows Qualification Evidence

Status: **QUALIFIED**

This document records the exact-candidate evidence used to close the v13.9 Suppression Counterplay & Tactical Fire-Support Command roadmap scope. Milestone qualification is not the same as public GitHub Release readiness.

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

The exact packaged candidate passed the production authored-audio presentation preflight, v13.9 fire-support runtime smoke, v13.8 suppression/morale regression, v13.7 late-round-performance regression, v13.6 sensor-fusion regression and the rounds 80/90/100 soak regression on the same executable.

v13.9 remains an intent-and-counterplay layer. The director does not own projectile instantiation or Health damage; the execution bridge forwards qualified strike intent through canonical `TankGame.SpawnProjectile`, while enemy movement/fire and survivability remain under their existing authorities.
""",
        encoding="utf-8",
    )


def already_finalized(roadmap: str, readme: str) -> bool:
    return (
        "STATUS-V13.9%20QUALIFIED-brightgreen" in roadmap
        and "| **463** | **0** | **463** | **100.0%** |" in roadmap
        and "## v13.9 — Suppression Counterplay & Tactical Fire-Support Command — QUALIFIED" in roadmap
        and roadmap.count("- [ ]") == 0
        and "**V13.9 QUALIFIED** on `dev-v13-9`" in readme
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
        print("v13.9 roadmap already finalized and exact evidence matches")
        return 0

    ROADMAP.write_text(finalize_roadmap(roadmap), encoding="utf-8")
    README.write_text(finalize_readme(readme), encoding="utf-8")
    write_evidence(args.finalizer_run)
    print("finalized v13.9 roadmap scope: 463/463 (100.0%) V13.9 QUALIFIED")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
