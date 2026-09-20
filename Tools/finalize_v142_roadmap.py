#!/usr/bin/env python3
"""Finalize v14.2 only from the exact verified Windows qualification evidence."""
from __future__ import annotations
import re
from pathlib import Path

CANDIDATE_SHA = "1c846c02f7867170e52593cf9d7ddc9a0b842b08"
WINDOWS_RUN = 35517709576
TYPE_RUN = 35517711106
SOURCE_RUN = 35517711102
PRESENTATION_RUN = 35517711093
ARTIFACT_ID = 10607850989
ARTIFACT_NAME = "TankRevivalOverdrive-v14.2-deception-Windows-x64"
ARTIFACT_BYTES = 60295014
ARTIFACT_DIGEST = "sha256:a4947d769645c8213098066a09cb823009b4cb422776f0422262f23c6718c5c6"
UNITY_VERSION = "6000.3.17f1"

ROADMAP = Path("ROADMAP.md")
README = Path("README.md")
EVIDENCE = Path("docs/qualification/V14_2_WINDOWS_QUALIFICATION.md")

MILESTONE_HEADING = "## v14.2 — Deception, Emission Discipline & Shoot-and-Scoot Warfare — IN DEVELOPMENT"
QUALIFIED_HEADING = "## v14.2 — Deception, Emission Discipline & Shoot-and-Scoot Warfare — QUALIFIED"


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
    if checklist_counts(text) != (479, 8, 487):
        raise RuntimeError(f"ROADMAP pre-state must be 479/8/487, found {checklist_counts(text)}")
    if MILESTONE_HEADING not in text:
        raise RuntimeError("v14.2 development heading missing")

    prefix, section = text.split(MILESTONE_HEADING, 1)
    open_in_section = len(re.findall(r"^- \[ \] ", section, re.M))
    if open_in_section != 8:
        raise RuntimeError(f"v14.2 must contain exactly 8 open deliverables, found {open_in_section}")
    section = re.sub(r"(?m)^- \[ \] ", "- [x] ", section)
    text = prefix + QUALIFIED_HEADING + section

    text = replace_once(text, "ROADMAP-98.4%25-blue", "ROADMAP-100.0%25-brightgreen", "roadmap badge")
    text = replace_once(text, "DONE-479%2F487-1f6feb", "DONE-487%2F487-brightgreen", "done badge")
    text = replace_once(text, "STATUS-V14.2%20IN%20DEVELOPMENT-blue", "STATUS-V14.2%20QUALIFIED-brightgreen", "status badge")
    text = replace_once(
        text,
        "| **479** | **8** | **487** | **98.4%** |",
        "| **487** | **0** | **487** | **100.0%** |",
        "progress table",
    )
    if checklist_counts(text) != (487, 0, 487):
        raise RuntimeError(f"ROADMAP post-state must be 487/0/487, found {checklist_counts(text)}")
    return text


def finalize_readme(text: str) -> str:
    text = replace_once(
        text,
        "[![Roadmap](https://img.shields.io/badge/Roadmap-98.4%25%20V14.2%20In%20Development-02050A?style=for-the-badge&logo=github&logoColor=62E5FF)](ROADMAP.md)",
        "[![Roadmap](https://img.shields.io/badge/Roadmap-100.0%25%20V14.2%20Qualified-02050A?style=for-the-badge&logo=github&logoColor=62E5FF)](ROADMAP.md)",
        "README roadmap badge",
    )
    text = replace_once(
        text,
        "[![v14.1 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/counter-observation-v141-windows.yml/badge.svg?branch=dev-v14-1)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/counter-observation-v141-windows.yml)",
        "[![v14.2 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/deception-v142-windows.yml/badge.svg?branch=dev-v14-2)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/deception-v142-windows.yml)",
        "README Windows badge",
    )
    text = replace_once(
        text,
        "| Development milestone | **V14.2 IN DEVELOPMENT** on `dev-v14-2` |",
        "| Development milestone | **V14.2 QUALIFIED** on `dev-v14-2` |",
        "README milestone status",
    )
    text = replace_once(
        text,
        "| Roadmap | **479 / 487 completed (98.4%)** — authoritative `ROADMAP.md` scope |",
        "| Roadmap | **487 / 487 completed (100.0%)** — authoritative `ROADMAP.md` scope |",
        "README roadmap counter",
    )
    text = replace_once(
        text,
        "| Latest qualified milestone | **v14.1** — exact Windows candidate `897e42ba0b02b90bc6b7de678b1b78761bd899fc` qualified in run `35497720665` |",
        f"| Latest qualified milestone | **v14.2** — exact Windows candidate `{CANDIDATE_SHA}` qualified in run `{WINDOWS_RUN}` |",
        "README latest qualified",
    )
    text = replace_once(
        text,
        "| 🥷 **Counter-recon deception v14.2 — in development** | Finite decoys, emission discipline and shoot-and-scoot timing will let the player misdirect hostile observation without hidden immunity or parallel combat authority. |",
        "| 🥷 **Counter-recon deception v14.2 — qualified** | Finite false-emission decoys, bounded EMCON relocation and shoot-and-scoot reacquisition timing now misdirect hostile observation with deterministic adaptation, explicit exposure floors and no parallel combat authority. |",
        "README v14.2 highlight",
    )
    text = replace_once(
        text,
        "| 🧪 **Exact-candidate qualification** | v14.1 passed one packaged Windows EXE through production-audio preflight, v14.1 observer-hunt smoke, v14.0/v13.9/v13.8/v13.7 regressions and late-round 80/90/100 soak. |",
        "| 🧪 **Exact-candidate qualification** | v14.2 passed one packaged Windows EXE through production-audio preflight, v14.2 deception/EMCON smoke, v14.1/v14.0/v13.9/v13.8 regressions and late-round 80/90/100 soak. |",
        "README exact qualification highlight",
    )
    text = replace_once(
        text,
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **479 / 487 (98.4%) — V14.2 IN DEVELOPMENT**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **487 / 487 (100.0%) — V14.2 QUALIFIED**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "README roadmap gate state",
    )
    anchor = "The qualified v14.1 candidate is `897e42ba0b02b90bc6b7de678b1b78761bd899fc`; Windows qualification run `35497720665` completed successfully. The same packaged EXE passed production authored-audio preflight, v14.1 counter-observation smoke, v14.0/v13.9/v13.8/v13.7 regressions and rounds 80/90/100 soak. The retained Windows artifact `TankRevivalOverdrive-v14.1-counter-observation-Windows-x64` (artifact `10601617216`, 60289129 bytes) has GitHub artifact digest `sha256:807bfc2bfd1b4d5b8b557d93ea085e0e33d89d5d24b4480adf87e8764491346c`."
    v142 = (
        f"\n\nThe qualified v14.2 candidate is `{CANDIDATE_SHA}`; Windows qualification run `{WINDOWS_RUN}` completed successfully. "
        "The same packaged EXE passed production authored-audio preflight, v14.2 deception/EMCON smoke, v14.1/v14.0/v13.9/v13.8 regressions and rounds 80/90/100 soak. "
        f"The retained Windows artifact `{ARTIFACT_NAME}` (artifact `{ARTIFACT_ID}`, {ARTIFACT_BYTES} bytes) has GitHub artifact digest `{ARTIFACT_DIGEST}`."
    )
    text = replace_once(text, anchor, anchor + v142, "README v14.2 qualification evidence")
    text = replace_once(
        text,
        "- **Development milestone:** v14.2 is in development on `dev-v14-2`; v14.1 remains the latest qualified milestone and this development scope does not automatically create or replace a public release.",
        "- **Development milestone:** v14.2 is qualified on `dev-v14-2`; this qualified development scope does not automatically create or replace a public release.",
        "README release status",
    )
    text = replace_once(
        text,
        "### Deception, Emission Discipline & Shoot-and-Scoot Warfare v14.2 — in development",
        "### Deception, Emission Discipline & Shoot-and-Scoot Warfare v14.2 — qualified",
        "README v14.2 heading",
    )
    text = replace_once(
        text,
        "v14.2 adds finite counter-recon deception above the qualified v14.1 observer-hunt layer. Decoys and emission-control windows may alter only exposure, designation quality and reacquisition timing; canonical `Health`, `EnemyTank`, `Projectile`, `TankGame`, `CounterBatteryDirectorV140` and `CounterObservationDirectorV141` remain authoritative for combat outcomes.",
        f"v14.2 is qualified above the v14.1 observer-hunt layer. Finite-charge false-emission decoys, bounded EMCON relocation and shoot-and-scoot reacquisition timing create deliberate counter-recon play, while deterministic suspicion reduces repeated decoy credibility and terrain/cohesion/command resilience prevents permanent lockout. Exact candidate `{CANDIDATE_SHA}` passed Windows run `{WINDOWS_RUN}` with v14.2 smoke, v14.1/v14.0/v13.9/v13.8 regressions and rounds 80/90/100 soak. Canonical `Health`, `EnemyTank`, `Projectile`, `TankGame`, `CounterBatteryDirectorV140` and `CounterObservationDirectorV141` remain authoritative for combat outcomes.",
        "README v14.2 body",
    )
    return text


def evidence() -> str:
    return f"""# v14.2 Windows Qualification Evidence\n\n- Milestone: **v14.2 — Deception, Emission Discipline & Shoot-and-Scoot Warfare**\n- Candidate SHA: `{CANDIDATE_SHA}`\n- Source qualification run: `{SOURCE_RUN}` — SUCCESS\n- Presentation qualification run: `{PRESENTATION_RUN}` — SUCCESS\n- C# type-uniqueness run: `{TYPE_RUN}` — SUCCESS\n- Windows qualification run: `{WINDOWS_RUN}` — SUCCESS\n- Unity: `{UNITY_VERSION}`\n- Target: Windows x64\n- Artifact: `{ARTIFACT_NAME}`\n- Artifact ID: `{ARTIFACT_ID}`\n- Artifact bytes: `{ARTIFACT_BYTES}`\n- Artifact digest: `{ARTIFACT_DIGEST}`\n\n## Same-executable qualification matrix\n\nThe exact packaged candidate passed production authored-audio presentation preflight, the v14.2 deception/EMCON runtime-authority smoke, v14.1 counter-observation regression, v14.0 counter-battery regression, v13.9 fire-support regression, v13.8 suppression/morale regression, and the rounds 80/90/100 soak on the same executable.\n\n## Authority and release boundary\n\nThe v14.2 layer may alter only bounded deception credibility, emission/exposure, designation quality and reacquisition timing. Canonical `Health`, `EnemyTank`, `Projectile`, `TankGame`, `CounterBatteryDirectorV140` and `CounterObservationDirectorV141` remain authoritative. Roadmap qualification does not itself publish a public Release or merge this development branch to `main`.\n"""


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
    print(f"v14.2 finalization prepared from exact candidate {CANDIDATE_SHA}")


if __name__ == "__main__":
    main()
