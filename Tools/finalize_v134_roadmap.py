#!/usr/bin/env python3
"""Finalize v13.4 docs only after the pinned exact Windows candidate has qualified."""
from __future__ import annotations

import argparse
import re
from pathlib import Path

README = Path("README.md")
ROADMAP = Path("ROADMAP.md")
EVIDENCE = Path("docs/qualification/V13_4_WINDOWS_QUALIFICATION.md")


def fail(message: str) -> None:
    raise SystemExit("v13.4 finalizer FAIL: " + message)


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        fail(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


def finalize_roadmap(text: str) -> str:
    text = replace_once(text, "ROADMAP-98.1%25-yellow", "ROADMAP-100.0%25-brightgreen", "roadmap badge")
    text = replace_once(text, "DONE-415%2F423-1f6feb", "DONE-423%2F423-1f6feb", "done badge")
    text = replace_once(text, "STATUS-V13.4%20IN%20DEVELOPMENT-yellow", "STATUS-V13.4%20QUALIFIED-brightgreen", "status badge")
    text = replace_once(text,
        "Roadmap progress: 415 / 423 completed (98.1%) — V13.4 IN DEVELOPMENT. Release readiness is tracked separately by Windows qualification gates.",
        "Roadmap progress: 423 / 423 completed (100.0%) — V13.4 QUALIFIED. Release readiness is tracked separately by Windows qualification gates.",
        "roadmap fallback")
    text = replace_once(text, "| **415** | **8** | **423** | **98.1%** |", "| **423** | **0** | **423** | **100.0%** |", "roadmap table")
    start = "## v13.4 — Tactical Terrain & Cover Warfare — IN DEVELOPMENT"
    text = replace_once(text, start, "## v13.4 — Tactical Terrain & Cover Warfare — QUALIFIED", "v13.4 heading")
    section_start = text.index("## v13.4 — Tactical Terrain & Cover Warfare — QUALIFIED")
    section = text[section_start:]
    if section.count("- [ ] ") != 8 or section.count("- [x] ") != 0:
        fail("v13.4 checklist is not the expected eight-item open scope")
    section = section.replace("- [ ] ", "- [x] ")
    text = text[:section_start] + section
    return text


def finalize_readme(text: str, candidate_sha: str, run_id: str) -> str:
    text = replace_once(text,
        "[![Roadmap](https://img.shields.io/badge/Roadmap-98.1%25%20V13.4%20In%20Development-02050A?style=for-the-badge&logo=github&logoColor=62E5FF)](ROADMAP.md)",
        "[![Roadmap](https://img.shields.io/badge/Roadmap-100.0%25%20V13.4%20Qualified-02050A?style=for-the-badge&logo=github&logoColor=62E5FF)](ROADMAP.md)",
        "README roadmap badge")
    text = replace_once(text,
        "[![v13.3 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-cohesion-v133-windows.yml/badge.svg?branch=dev-v13-3)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-cohesion-v133-windows.yml)",
        "[![v13.4 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/tactical-terrain-v134-windows.yml/badge.svg?branch=dev-v13-4)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/tactical-terrain-v134-windows.yml)",
        "README Windows gate badge")
    text = replace_once(text,
        "Roadmap progress: 415 / 423 completed (98.1%) — V13.4 IN DEVELOPMENT. Release readiness is tracked separately by Windows qualification gates.",
        "Roadmap progress: 423 / 423 completed (100.0%) — V13.4 QUALIFIED. Release readiness is tracked separately by Windows qualification gates.",
        "README progress fallback")
    text = replace_once(text, "| Development milestone | **V13.4 IN DEVELOPMENT** on `dev-v13-4` |", "| Development milestone | **V13.4 QUALIFIED** on `dev-v13-4` |", "README milestone")
    text = replace_once(text, "| Roadmap | **415 / 423 completed (98.1%)** — authoritative `ROADMAP.md` scope |", "| Roadmap | **423 / 423 completed (100.0%)** — authoritative `ROADMAP.md` scope |", "README roadmap row")
    text = replace_once(text, "| Latest qualified milestone | **v13.3** — exact Windows candidate qualified |", "| Latest qualified milestone | **v13.4** — exact Windows candidate qualified |", "README qualified row")
    text = replace_once(text, "**Tactical terrain v13.4 — in development**", "**Tactical terrain v13.4 — qualified**", "README highlight")
    old_exact = "| 🧪 **Exact-candidate qualification** | v13.3 passed one packaged Windows EXE through production-audio preflight, v13.3 smoke, historical regressions and late-round 80/90/100 soak. v13.4 remains unqualified until its own exact-candidate gate is green. |"
    new_exact = "| 🧪 **Exact-candidate qualification** | v13.4 passed one packaged Windows EXE through production-audio preflight, v13.4 smoke, v13.3/v13.2/v13.1/v13.0/v12.9/v12.8 regressions and late-round 80/90/100 soak. |"
    text = replace_once(text, old_exact, new_exact, "README exact gate highlight")
    text = replace_once(text, "### Tactical Terrain & Cover Warfare v13.4 — in development", "### Tactical Terrain & Cover Warfare v13.4 — qualified", "README v13.4 section")
    text = replace_once(text,
        "The source and integration contracts are present, but the milestone remains open until one exact Windows candidate passes the full qualification matrix.",
        "The exact Windows candidate passed the full production-audio, v13.4 terrain, historical regression and rounds 80/90/100 soak matrix while preserving the existing gameplay authority boundaries.",
        "README v13.4 qualification sentence")
    text = replace_once(text,
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **415 / 423 (98.1%) — V13.4 IN DEVELOPMENT**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **423 / 423 (100.0%) — V13.4 QUALIFIED**. Release readiness is tracked separately by exact-candidate Windows qualification gates.",
        "README roadmap summary")
    old_quality = "The qualified v13.3 candidate is `0e3496ce095d27b2a686efbb5539cc7e475e3213`; Windows qualification run `35304207429` completed successfully. v13.4 now has source/integration contracts and a dedicated packaged smoke, but its eight roadmap items remain pending until the v13.4 exact-candidate Windows qualification train is complete."
    new_quality = f"The qualified v13.4 candidate is `{candidate_sha}`; Windows qualification run `{run_id}` completed successfully. The same packaged EXE passed production authored-audio preflight, v13.4 tactical-terrain smoke, v13.3/v13.2/v13.1/v13.0/v12.9/v12.8 regressions and rounds 80/90/100 soak."
    text = replace_once(text, old_quality, new_quality, "README qualification evidence")
    text = replace_once(text,
        "- **Development milestone:** v13.4 is in development on `dev-v13-4`; v13.3 is the last qualified milestone and has not been published as a new public v13.3 release.",
        "- **Development milestone:** v13.4 is qualified on `dev-v13-4`; it remains a development milestone and has not been published as a new public v13.4 release.",
        "README releases status")
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

    roadmap = finalize_roadmap(ROADMAP.read_text(encoding="utf-8"))
    readme = finalize_readme(README.read_text(encoding="utf-8"), args.candidate_sha, args.run_id)
    ROADMAP.write_text(roadmap, encoding="utf-8")
    README.write_text(readme, encoding="utf-8")

    EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
    EVIDENCE.write_text(
        "# v13.4 Windows Qualification Evidence\n\n"
        "This file records the exact candidate that justified closing the v13.4 roadmap scope. "
        "It does not by itself publish a public release.\n\n"
        f"- Candidate commit: `{args.candidate_sha}`\n"
        f"- Qualification run: `{args.run_id}` — `success`\n"
        "- Workflow: `Tactical Terrain v13.4 Windows Qualification Gate`\n"
        "- Unity / platform: `6000.3.17f1` / `Windows x64`\n"
        f"- Candidate ZIP SHA-256: `{args.candidate_zip_sha256}`\n"
        f"- GitHub artifact digest: `{args.artifact_sha256}`\n"
        "- Runtime matrix: production authored-audio preflight; v13.4; v13.3; v13.2; v13.1; v13.0; v12.9; v12.8; rounds 80/90/100 soak — all PASS.\n"
        "- Public release status: unchanged; roadmap qualification and release readiness/publication remain separate.\n",
        encoding="utf-8",
    )
    print("v13.4 roadmap/README qualification transition prepared: 423/423")


if __name__ == "__main__":
    main()
