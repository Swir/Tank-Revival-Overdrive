#!/usr/bin/env python3
"""Finalize v14.1 only from the exact verified Windows qualification evidence."""
from __future__ import annotations
import re
from pathlib import Path

CANDIDATE_SHA = "897e42ba0b02b90bc6b7de678b1b78761bd899fc"
WINDOWS_RUN = 35497720665
TYPE_RUN = 35497722640
ARTIFACT_ID = 10601617216
ARTIFACT_NAME = "TankRevivalOverdrive-v14.1-counter-observation-Windows-x64"
ARTIFACT_BYTES = 60289129
ARTIFACT_DIGEST = "sha256:807bfc2bfd1b4d5b8b557d93ea085e0e33d89d5d24b4480adf87e8764491346c"
UNITY_VERSION = "6000.3.17f1"
ROADMAP = Path("ROADMAP.md")
README = Path("README.md")
EVIDENCE = Path("docs/qualification/V14_1_WINDOWS_QUALIFICATION.md")

FINAL_ITEMS = (
    "**Deterministic runtime, authority and performance contracts** — verify fixed hostile/observer caps, sensor thresholds, lifecycle cleanup, acquisition floors, no hot-path scene scans and unchanged Health/Projectile/TankGame/EnemyTank ownership.",
    "**Exact-SHA Windows qualification** — package one Windows x64 candidate, pass v14.1 observer-hunt smoke, rerun v14.0/v13.9/v13.8/v13.7 regressions plus rounds 80/90/100 soak on the same executable, then finalize checklist/progress SVGs.",
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
    if checklist_counts(text) != (477, 2, 479):
        raise RuntimeError(f"ROADMAP pre-state must be 477/2/479, found {checklist_counts(text)}")
    for item in FINAL_ITEMS:
        text = replace_once(text, "- [ ] " + item, "- [x] " + item, "v14.1 final checkbox")
    text = replace_once(text, "ROADMAP-99.6%25-blue", "ROADMAP-100.0%25-brightgreen", "roadmap badge")
    text = replace_once(text, "DONE-477%2F479-1f6feb", "DONE-479%2F479-brightgreen", "done badge")
    text = replace_once(text, "STATUS-V14.1%20IN%20DEVELOPMENT-blue", "STATUS-V14.1%20QUALIFIED-brightgreen", "status badge")
    text = replace_once(text, "| **477** | **2** | **479** | **99.6%** |", "| **479** | **0** | **479** | **100.0%** |", "progress table")
    text = replace_once(text, "## v14.1 — Counter-Observation & Hunter-Killer Warfare — IN DEVELOPMENT", "## v14.1 — Counter-Observation & Hunter-Killer Warfare — QUALIFIED", "milestone heading")
    if checklist_counts(text) != (479, 0, 479):
        raise RuntimeError(f"ROADMAP post-state must be 479/0/479, found {checklist_counts(text)}")
    return text


def finalize_readme(text: str) -> str:
    text = replace_once(
        text,
        "[![Roadmap](https://img.shields.io/badge/Roadmap-99.6%25%20V14.1%20In%20Development-02050A?style=for-the-badge&logo=github&logoColor=62E5FF)](ROADMAP.md)",
        "[![Roadmap](https://img.shields.io/badge/Roadmap-100.0%25%20V14.1%20Qualified-02050A?style=for-the-badge&logo=github&logoColor=62E5FF)](ROADMAP.md)",
        "README roadmap badge",
    )
    text = replace_once(
        text,
        "[![v14.0 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/counter-battery-v140-windows.yml/badge.svg?branch=dev-v14-0)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/counter-battery-v140-windows.yml)",
        "[![v14.1 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/counter-observation-v141-windows.yml/badge.svg?branch=dev-v14-1)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/counter-observation-v141-windows.yml)",
        "README Windows badge",
    )
    text = replace_once(text, "| Development milestone | **V14.1 IN DEVELOPMENT** on `dev-v14-1` |", "| Development milestone | **V14.1 QUALIFIED** on `dev-v14-1` |", "README milestone status")
    text = replace_once(text, "| Roadmap | **477 / 479 completed (99.6%)** — authoritative `ROADMAP.md` scope |", "| Roadmap | **479 / 479 completed (100.0%)** — authoritative `ROADMAP.md` scope |", "README roadmap counter")
    text = replace_once(
        text,
        "| Latest qualified milestone | **v14.0** — exact Windows candidate `77213238077e999ed9b880c6e53bad0b0b5b1dfa` qualified in run `35482246072` |",
        f"| Latest qualified milestone | **v14.1** — exact Windows candidate `{CANDIDATE_SHA}` qualified in run `{WINDOWS_RUN}` |",
        "README latest qualified",
    )
    text = replace_once(
        text,
        "| 🔭 **Counter-observation warfare v14.1 — in development** | Source-verified observer hunts publish one bounded hunter-killer target, confirm success only through canonical Health/runtime-registry lifecycle, scale designation/disruption through terrain/cohesion/command resilience, and add fixed-cap world-space target emphasis through the existing late-round FX budget. |",
        "| 🔭 **Counter-observation warfare v14.1 — qualified** | Verified sensor contact can expose one bounded observer-class hunter-killer target; canonical kill confirmation creates only a short counter-battery disruption window, while terrain/cohesion/command resilience and fixed-cap presentation preserve combat authority and late-round budgets. |",
        "README v14.1 highlight",
    )
    text = replace_once(
        text,
        "| 🧪 **Exact-candidate qualification** | v14.0 passed one packaged Windows EXE through production-audio preflight, v14.0 counter-battery smoke, v13.9/v13.8/v13.7/v13.6 regressions and late-round 80/90/100 soak. |",
        "| 🧪 **Exact-candidate qualification** | v14.1 passed one packaged Windows EXE through production-audio preflight, v14.1 observer-hunt smoke, v14.0/v13.9/v13.8/v13.7 regressions and late-round 80/90/100 soak. |",
        "README exact qualification highlight",
    )
    text = replace_once(
        text,
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **477 / 479 (99.6%) — V14.1 IN DEVELOPMENT**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **479 / 479 (100.0%) — V14.1 QUALIFIED**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "README roadmap gate state",
    )
    anchor = "The qualified v14.0 candidate is `77213238077e999ed9b880c6e53bad0b0b5b1dfa`; Windows qualification run `35482246072` completed successfully. The same packaged EXE passed production authored-audio preflight, v14.0 counter-battery smoke, v13.9/v13.8/v13.7/v13.6 regressions and rounds 80/90/100 soak. The retained Windows artifact `TankRevivalOverdrive-v14.0-counter-battery-Windows-x64` (artifact `10596580538`, 60278167 bytes) has GitHub artifact digest `sha256:2be0f4b745ed85f78be2869cdb56b63ecab05f3213f15754d0675abdbfe31c5d`."
    v141 = (
        f"\n\nThe qualified v14.1 candidate is `{CANDIDATE_SHA}`; Windows qualification run `{WINDOWS_RUN}` completed successfully. "
        "The same packaged EXE passed production authored-audio preflight, v14.1 counter-observation smoke, v14.0/v13.9/v13.8/v13.7 regressions and rounds 80/90/100 soak. "
        f"The retained Windows artifact `{ARTIFACT_NAME}` (artifact `{ARTIFACT_ID}`, {ARTIFACT_BYTES} bytes) has GitHub artifact digest `{ARTIFACT_DIGEST}`."
    )
    text = replace_once(text, anchor, anchor + v141, "README v14.1 qualification evidence")
    text = replace_once(
        text,
        "- **Development milestone:** v14.1 is in development on `dev-v14-1`; v14.0 remains the latest qualified milestone and this development scope does not automatically create or replace a public release.",
        "- **Development milestone:** v14.1 is qualified on `dev-v14-1`; this qualified development scope does not automatically create or replace a public release.",
        "README release status",
    )
    text = replace_once(text, "### Counter-Observation & Hunter-Killer Warfare v14.1 — in development", "### Counter-Observation & Hunter-Killer Warfare v14.1 — qualified", "README v14.1 heading")
    text = replace_once(
        text,
        "v14.1 builds active hunter-killer counterplay above the qualified v14.0 counter-battery system. The player must use existing sensor information to identify real observer-class threats, destroy them through canonical combat, and earn only a short bounded disruption of enemy acquisition. The new layer may publish designation/disruption intent but never deals damage, deletes projectiles, teleports enemies or replaces `Health`, `EnemyTank`, `Projectile`, `TankGame` or `CounterBatteryDirectorV140` authority.",
        f"v14.1 is qualified above the v14.0 counter-battery system. Verified sensor information can designate one real observer-class threat, canonical `Health` / runtime-registry lifecycle confirms the kill, and success grants only a short bounded disruption of enemy acquisition. Terrain, cohesion and command resilience remain bounded and observer-hunt presentation stays fixed-cap. Exact candidate `{CANDIDATE_SHA}` passed Windows run `{WINDOWS_RUN}` with v14.1 smoke, v14.0/v13.9/v13.8/v13.7 regressions and rounds 80/90/100 soak; the layer never deals direct damage, deletes projectiles, teleports enemies or replaces `Health`, `EnemyTank`, `Projectile`, `TankGame` or `CounterBatteryDirectorV140` authority.",
        "README v14.1 body",
    )
    return text


def evidence() -> str:
    return f"""# v14.1 Windows Qualification Evidence\n\n- Milestone: **v14.1 — Counter-Observation & Hunter-Killer Warfare**\n- Candidate SHA: `{CANDIDATE_SHA}`\n- C# type-uniqueness run: `{TYPE_RUN}` — SUCCESS\n- Windows qualification run: `{WINDOWS_RUN}` — SUCCESS\n- Unity: `{UNITY_VERSION}`\n- Target: Windows x64\n- Artifact: `{ARTIFACT_NAME}`\n- Artifact ID: `{ARTIFACT_ID}`\n- Artifact bytes: `{ARTIFACT_BYTES}`\n- Artifact digest: `{ARTIFACT_DIGEST}`\n\n## Same-executable qualification matrix\n\nThe exact packaged candidate passed production authored-audio presentation preflight, the v14.1 counter-observation runtime/authority smoke, v14.0 counter-battery regression, v13.9 fire-support regression, v13.8 suppression/morale regression, v13.7 late-round-performance regression, and the rounds 80/90/100 soak on the same executable.\n\n## Authority and release boundary\n\nThe v14.1 layer designates and observes bounded targets only. Canonical `Health` / runtime-registry lifecycle confirms observer kills, while `CounterBatteryDirectorV140` remains acquisition/barrage authority and `Projectile`, `TankGame`, `EnemyTank` and Rigidbody paths remain unchanged. Roadmap qualification does not itself publish a public Release or merge this development branch to `main`.\n"""


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
    print(f"v14.1 finalization prepared from exact candidate {CANDIDATE_SHA}")


if __name__ == "__main__":
    main()
