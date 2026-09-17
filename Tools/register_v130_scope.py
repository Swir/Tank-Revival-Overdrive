from pathlib import Path
import re

ROADMAP = Path("ROADMAP.md")
text = ROADMAP.read_text(encoding="utf-8")
marker = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
heading = "## v13.0 — 100-Round Encounter Director & Boss Phase Warfare"

if len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', text, re.M)) != 1:
    raise SystemExit("Expected exactly one standalone SWIR roadmap marker")

if heading in text:
    required = [
        "badge.svg?branch=dev-v13-0",
        "ROADMAP-98.0%25-yellow?style=for-the-badge",
        "DONE-383%2F391-1f6feb?style=for-the-badge",
        "STATUS-V13.0%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
        "███████████████████░ 98.0%",
        "| **383** | **8** | **391** | **98.0%** |",
    ]
    for token in required:
        if token not in text:
            raise SystemExit(f"Existing v13.0 scope is inconsistent: missing {token}")
    section = text.split(heading, 1)[1]
    if section.count("- [ ]") != 8 or section.count("- [x]") != 0:
        raise SystemExit("Existing v13.0 section must contain exactly eight pending items and zero completed items")
    print("v13.0 scope already registered and consistent")
    raise SystemExit(0)

required_before = [
    "badge.svg?branch=dev-v12-9",
    "ROADMAP-100.0%25-brightgreen?style=for-the-badge",
    "DONE-383%2F383-1f6feb?style=for-the-badge",
    "STATUS-V12.9%20QUALIFIED-brightgreen?style=for-the-badge",
    "████████████████████ 100.0%",
    "| **383** | **0** | **383** | **100.0%** |",
]
for token in required_before:
    if token not in text:
        raise SystemExit(f"Missing qualified v12.9 dashboard token: {token}")

replacements = {
    "badge.svg?branch=dev-v12-9": "badge.svg?branch=dev-v13-0",
    "ROADMAP-100.0%25-brightgreen?style=for-the-badge": "ROADMAP-98.0%25-yellow?style=for-the-badge",
    "DONE-383%2F383-1f6feb?style=for-the-badge": "DONE-383%2F391-1f6feb?style=for-the-badge",
    "STATUS-V12.9%20QUALIFIED-brightgreen?style=for-the-badge": "STATUS-V13.0%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "████████████████████ 100.0%": "███████████████████░ 98.0%",
    "| **383** | **0** | **383** | **100.0%** |": "| **383** | **8** | **391** | **98.0%** |",
}
for old, new in replacements.items():
    text = text.replace(old, new, 1)

anchor = "- [x] **Exact-candidate Windows qualification** — build Windows x64, run packaged-EXE v12.9 smoke plus v12.8 integration regression checks, record exact SHA/artifact provenance and only then finalize roadmap qualification.\n"
if anchor not in text:
    raise SystemExit("Could not find v12.9 qualification anchor")

section = """

## v13.0 — 100-Round Encounter Director & Boss Phase Warfare — IN DEVELOPMENT
- [ ] **Deterministic 100-round encounter planner** — generate bounded encounter doctrines and threat budgets for rounds 1–100 with anti-repetition, campaign-band escalation and boss-safe scheduling while `TankGame` remains round/spawn authority.
- [ ] **Multi-phase boss warfare** — derive readable boss phases from canonical `Health` plus v12.9 component state, exposing bounded movement/fire/support directives without creating a second survivability, movement or projectile authority.
- [ ] **Orzeł defense escalation** — connect early/mid/late campaign pressure to the existing Orzełek/base-defense state so objective intensity grows across 100 rounds without hidden direct damage or scripted invulnerability.
- [ ] **Cross-system encounter doctrine** — fold Combined Arms, sustainment, route intelligence, Recon/EW, Mobile Signal and SIGINT readiness into encounter pressure/relief decisions through read-only public state instead of duplicating those systems.
- [ ] **Hard encounter/performance budgets** — enforce deterministic caps for threat score, specialist density, support actions, boss reinforcements and presentation cues with monotonic late-round degradation of visuals rather than gameplay outcomes.
- [ ] **Readable encounter and boss telemetry** — publish compact doctrine, threat, objective and boss-phase snapshots to the existing unified tactical HUD/presentation stack with strict refresh and cue limits.
- [ ] **Packaged-EXE v13.0 runtime smoke** — validate all 100 round plans, anti-repetition, boss phase transitions, authority boundaries and hard budgets, then rerun v12.9/v12.8 regression plus rounds 80/90/100 soak on the same executable.
- [ ] **Exact-SHA Windows qualification** — deterministically package one Windows x64 candidate with provenance, require the complete v13.0 gate to pass and only then finalize the SWIR roadmap dashboard/checklist.
"""
text = text.replace(anchor, anchor + section, 1)

section_text = text.split(heading, 1)[1]
if section_text.count("- [ ]") != 8 or section_text.count("- [x]") != 0:
    raise SystemExit("v13.0 scope must begin with exactly eight pending items and zero completed items")

checked = len(re.findall(r'^- \[x\] ', text, re.M))
pending = len(re.findall(r'^- \[ \] ', text, re.M))
if (checked, pending, checked + pending) != (383, 8, 391):
    raise SystemExit(f"Checklist mismatch after scope registration: checked={checked}, pending={pending}, total={checked + pending}")

ROADMAP.write_text(text, encoding="utf-8")
final_text = ROADMAP.read_text(encoding="utf-8")
for token in [marker, "## 📊 Overall progress", "badge.svg?branch=dev-v13-0", "ROADMAP-98.0%25-yellow?style=for-the-badge", "DONE-383%2F391-1f6feb?style=for-the-badge", "STATUS-V13.0%20IN%20DEVELOPMENT-yellow?style=for-the-badge", "███████████████████░ 98.0%", "| **383** | **8** | **391** | **98.0%** |"]:
    if token not in final_text:
        raise SystemExit(f"ROADMAP invariant missing after registration: {token}")
if len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', final_text, re.M)) != 1:
    raise SystemExit("Roadmap marker count changed during registration")
print("Registered v13.0 scope: 383/391 = 98.0%, status IN DEVELOPMENT")
