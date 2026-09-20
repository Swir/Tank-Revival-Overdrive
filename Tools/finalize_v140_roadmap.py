#!/usr/bin/env python3
"""Finalize v14.0 only from the exact verified Windows qualification evidence."""
from __future__ import annotations
import re
from pathlib import Path

CANDIDATE_SHA = "77213238077e999ed9b880c6e53bad0b0b5b1dfa"
WINDOWS_RUN = 35482246072
SOURCE_RUN = 35482246038
TYPE_RUN = 35482246036
ARTIFACT_ID = 10596580538
ARTIFACT_NAME = "TankRevivalOverdrive-v14.0-counter-battery-Windows-x64"
ARTIFACT_BYTES = 60278167
ARTIFACT_DIGEST = "sha256:2be0f4b745ed85f78be2869cdb56b63ecab05f3213f15754d0675abdbfe31c5d"
UNITY_VERSION = "6000.3.17f1"
ROADMAP = Path("ROADMAP.md")
README = Path("README.md")
EVIDENCE = Path("docs/qualification/V14_0_WINDOWS_QUALIFICATION.md")

FINAL_ITEMS = (
    "**Deterministic runtime, authority and performance contracts** — verify exposure decay, lock/break thresholds, observer caps, shell budgets, no hot-path scene scans and unchanged Health/Projectile/TankGame ownership in the packaged executable.",
    "**Exact-SHA Windows qualification** — package one Windows x64 candidate, pass the v14.0 counter-battery smoke, rerun v13.9/v13.8/v13.7/v13.6 regressions plus rounds 80/90/100 soak on that same executable, and only then finalize checklist/progress SVGs.",
)


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


def checklist_counts(text: str) -> tuple[int, int, int]:
    done = len(re.findall(r"^- \[x\] ", text, re.M))
    open_ = len(re.findall(r"^- \[ \] ", text, re.M))
    return done, open_, done + open_


def finalize_roadmap(text: str) -> str:
    if checklist_counts(text) != (469, 2, 471):
        raise RuntimeError(f"ROADMAP pre-state must be 469/2/471, found {checklist_counts(text)}")
    for item in FINAL_ITEMS:
        text = replace_once(text, "- [ ] " + item, "- [x] " + item, "v14.0 final checkbox")
    text = replace_once(text, "ROADMAP-99.6%25-blue", "ROADMAP-100.0%25-brightgreen", "roadmap badge")
    text = replace_once(text, "DONE-469%2F471-1f6feb", "DONE-471%2F471-brightgreen", "done badge")
    text = replace_once(text, "STATUS-V14.0%20IN%20DEVELOPMENT-blue", "STATUS-V14.0%20QUALIFIED-brightgreen", "status badge")
    text = replace_once(text, "| **469** | **2** | **471** | **99.6%** |", "| **471** | **0** | **471** | **100.0%** |", "progress table")
    text = replace_once(text, "## v14.0 — Counter-Battery & Mobile Fire-Control Warfare — IN DEVELOPMENT", "## v14.0 — Counter-Battery & Mobile Fire-Control Warfare — QUALIFIED", "milestone heading")
    if checklist_counts(text) != (471, 0, 471):
        raise RuntimeError(f"ROADMAP post-state must be 471/0/471, found {checklist_counts(text)}")
    return text


def finalize_readme(text: str) -> str:
    text = replace_once(
        text,
        "[![Roadmap](https://img.shields.io/badge/Roadmap-99.6%25%20V14.0%20In%20Development-02050A?style=for-the-badge&logo=github&logoColor=62E5FF)](ROADMAP.md)",
        "[![Roadmap](https://img.shields.io/badge/Roadmap-100.0%25%20V14.0%20Qualified-02050A?style=for-the-badge&logo=github&logoColor=62E5FF)](ROADMAP.md)",
        "README roadmap badge",
    )
    text = replace_once(
        text,
        "[![v13.9 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-fire-support-v139-windows.yml/badge.svg?branch=dev-v13-9)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-fire-support-v139-windows.yml)",
        "[![v14.0 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/counter-battery-v140-windows.yml/badge.svg?branch=dev-v14-0)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/counter-battery-v140-windows.yml)",
        "README Windows badge",
    )
    text = replace_once(text, "| Development milestone | **V14.0 IN DEVELOPMENT** on `dev-v14-0` |", "| Development milestone | **V14.0 QUALIFIED** on `dev-v14-0` |", "README milestone status")
    text = replace_once(text, "| Roadmap | **469 / 471 completed (99.6%)** — authoritative `ROADMAP.md` scope |", "| Roadmap | **471 / 471 completed (100.0%)** — authoritative `ROADMAP.md` scope |", "README roadmap counter")
    text = replace_once(
        text,
        "| Latest qualified milestone | **v13.9** — exact Windows candidate `30511fb9452dfb47e481ecb57f87ccd51aded1fa` qualified in run `35462501123` |",
        f"| Latest qualified milestone | **v14.0** — exact Windows candidate `{CANDIDATE_SHA}` qualified in run `{WINDOWS_RUN}` |",
        "README latest qualified",
    )
    text = replace_once(
        text,
        "| 📍 **Counter-battery warfare v14.0 — in development** | Source-verified terrain/sensor relocation, bounded observer/command weighting and readable SEARCHING/LOCKED/INCOMING/RELOCATE presentation now extend the counter-battery barrage authority; packaged runtime contracts and exact Windows qualification remain open. |",
        "| 📍 **Counter-battery warfare v14.0 — qualified** | Player fire-support signatures now drive bounded SEARCHING/LOCKED/BARRAGE pressure with terrain/sensor relocation counterplay, capped observer acquisition and canonical enemy projectile execution; one exact Windows candidate passed runtime, historical regression and late-round soak gates. |",
        "README v14 highlight",
    )
    text = replace_once(
        text,
        "| 🧪 **Exact-candidate qualification** | v13.9 passed one packaged Windows EXE through production-audio preflight, v13.9 fire-support smoke, v13.8/v13.7/v13.6 regressions and late-round 80/90/100 soak. |",
        "| 🧪 **Exact-candidate qualification** | v14.0 passed one packaged Windows EXE through production-audio preflight, v14.0 counter-battery smoke, v13.9/v13.8/v13.7/v13.6 regressions and late-round 80/90/100 soak. |",
        "README exact qualification highlight",
    )
    text = replace_once(text, "### Counter-Battery & Mobile Fire-Control Warfare v14.0 — in development", "### Counter-Battery & Mobile Fire-Control Warfare v14.0 — qualified", "README v14 heading")
    text = replace_once(
        text,
        "v14.0 source counterplay is now verified: v13.9 support-strike intent creates bounded exposure, terrain and sensor state reduce lock pressure through explicit relocation counterplay, and at most six eligible observers contribute cohesion- and command-weighted acquisition. SEARCHING / WARNING / LOCKED / INCOMING / RELOCATE presentation is bounded and readable while the separate execution bridge remains the only v14.0 path into canonical `TankGame.SpawnProjectile`. Packaged runtime contracts and exact Windows qualification remain open; `Health`, `Projectile`, `EnemyTank` and `Rigidbody2D` retain canonical gameplay authority.",
        f"v14.0 is qualified: v13.9 support-strike intent creates bounded exposure, terrain and sensor state reduce lock pressure through explicit relocation counterplay, and at most six eligible observers contribute cohesion- and command-weighted acquisition. SEARCHING / WARNING / LOCKED / INCOMING / RELOCATE presentation remains bounded while the separate execution bridge is the only v14.0 path into canonical `TankGame.SpawnProjectile`. Exact candidate `{CANDIDATE_SHA}` passed Windows qualification run `{WINDOWS_RUN}` with v14.0 runtime smoke, v13.9/v13.8/v13.7/v13.6 regressions and rounds 80/90/100 soak; `Health`, `Projectile`, `EnemyTank` and `Rigidbody2D` retain canonical gameplay authority.",
        "README v14 body",
    )
    text = replace_once(
        text,
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **469 / 471 (99.6%) — V14.0 IN DEVELOPMENT**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **471 / 471 (100.0%) — V14.0 QUALIFIED**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "README roadmap gate state",
    )
    anchor = "The qualified v13.9 candidate is `30511fb9452dfb47e481ecb57f87ccd51aded1fa`; Windows qualification run `35462501123` completed successfully. The same packaged EXE passed production authored-audio preflight, v13.9 fire-support smoke, v13.8/v13.7/v13.6 regressions and rounds 80/90/100 soak. The retained Windows artifact `TankRevivalOverdrive-v13.9-fire-support-Windows-x64` has GitHub artifact digest `sha256:7af94e0ef1032ccd7309740da061cd3e1351dde3e4fe06a139ab05a733db872d`."
    v14 = (
        f"\n\nThe qualified v14.0 candidate is `{CANDIDATE_SHA}`; Windows qualification run `{WINDOWS_RUN}` completed successfully. "
        "The same packaged EXE passed production authored-audio preflight, v14.0 counter-battery smoke, v13.9/v13.8/v13.7/v13.6 regressions and rounds 80/90/100 soak. "
        f"The retained Windows artifact `{ARTIFACT_NAME}` (artifact `{ARTIFACT_ID}`, {ARTIFACT_BYTES} bytes) has GitHub artifact digest `{ARTIFACT_DIGEST}`."
    )
    text = replace_once(text, anchor, anchor + v14, "README v14 qualification evidence")
    text = replace_once(
        text,
        "- **Development milestone:** v14.0 is in development on `dev-v14-0`; v13.9 remains the latest qualified milestone and this development scope does not automatically create or replace a public release.",
        "- **Development milestone:** v14.0 is qualified on `dev-v14-0`; this qualified development scope does not automatically create or replace a public release.",
        "README release status",
    )
    return text


def evidence() -> str:
    return f"""# v14.0 Windows Qualification Evidence\n\n- Milestone: **v14.0 — Counter-Battery & Mobile Fire-Control Warfare**\n- Candidate SHA: `{CANDIDATE_SHA}`\n- Source qualification run: `{SOURCE_RUN}` — SUCCESS\n- C# type-uniqueness run: `{TYPE_RUN}` — SUCCESS\n- Windows qualification run: `{WINDOWS_RUN}` — SUCCESS\n- Unity: `{UNITY_VERSION}`\n- Target: Windows x64\n- Artifact: `{ARTIFACT_NAME}`\n- Artifact ID: `{ARTIFACT_ID}`\n- Artifact bytes: `{ARTIFACT_BYTES}`\n- Artifact digest: `{ARTIFACT_DIGEST}`\n\n## Same-executable qualification matrix\n\nThe exact packaged candidate passed production authored-audio presentation preflight, the v14.0 counter-battery runtime/authority smoke, v13.9 fire-support regression, v13.8 suppression/morale regression, v13.7 late-round-performance regression, v13.6 sensor-fusion regression, and the rounds 80/90/100 soak on the same executable.\n\n## Authority and release boundary\n\nThe v14.0 layer keeps enemy barrage execution behind the separate execution bridge into canonical `TankGame.SpawnProjectile`; it does not add direct `Health` damage, parallel movement authority, scene-scan hot paths or a second spawn authority. Roadmap qualification does not itself publish a public Release or merge this development branch to `main`.\n"""


def main() -> None:
    roadmap = ROADMAP.read_text(encoding="utf-8")
    readme = README.read_text(encoding="utf-8")
    if "<!-- SWIR-ROADMAP-STANDARD:v1 -->" not in roadmap:
        raise RuntimeError("ROADMAP standard marker missing")
    if "<!-- SWIR-README-STANDARD:v2 -->" not in readme:
        raise RuntimeError("README standard marker missing")
    if "## 🔎 Search Keywords" not in readme:
        raise RuntimeError("README Search Keywords missing")
    ROADMAP.write_text(finalize_roadmap(roadmap), encoding="utf-8")
    README.write_text(finalize_readme(readme), encoding="utf-8")
    EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
    EVIDENCE.write_text(evidence(), encoding="utf-8")
    print(f"v14.0 finalization prepared from exact candidate {CANDIDATE_SHA}")


if __name__ == "__main__":
    main()
