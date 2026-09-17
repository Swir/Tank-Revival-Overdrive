from pathlib import Path
import re

ROADMAP = Path("ROADMAP.md")
text = ROADMAP.read_text(encoding="utf-8")
marker = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
heading = "## v13.1 — Dynamic Objective Warfare & Battlefield Mutators"

if len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', text, re.M)) != 1:
    raise SystemExit("Expected exactly one standalone SWIR roadmap marker")

pending_items = [
    "**Deterministic 100-round objective planner** — assign bounded objective doctrine across rounds 1–100 with anti-repetition, boss-safe scheduling and at least seven playable archetypes while v13.0 Encounter Planner remains the campaign pressure source.",
    "**Playable objective runtime orchestration** — connect objective progress/success/failure to the existing TankGame round loop and existing objective/operation/convoy systems without creating a second round, spawn, Health, movement or projectile authority.",
    "**Bounded battlefield mutators** — deterministically layer readable combat conditions over objectives with strict spawn/concurrency/timing bounds and no hidden direct HP damage, forced player lockout or outcome-changing presentation degradation.",
    "**Orzeł mission defense and counterattack logic** — support sector defense, command-post breakthrough and counterattack objectives around Orzełek with explicit fair completion/failure rules and a permanently breakable player route.",
    "**Cross-system objective doctrine** — fold Combined Arms, sustainment/logistics, Route Intelligence, Recon/EW, Mobile Signal and SIGINT public readiness into objective/mutator selection through bounded read-only directives rather than duplicating those systems.",
    "**Objective HUD and tactical telemetry** — expose active objective, progress, time/pressure state, mutator, doctrine and deterministic signature through the existing HUD/presentation stack with strict refresh/cue limits.",
    "**Packaged-EXE v13.1 runtime smoke** — validate all 100 objective plans, archetype coverage, anti-repetition, bounded mutators, authority contracts and objective state transitions, then rerun v13.0/v12.9/v12.8 regressions plus rounds 80/90/100 soak on the same executable.",
    "**Exact-SHA Windows qualification** — deterministically package one Windows x64 candidate with provenance, require the complete v13.1 gate to pass and only then finalize the SWIR roadmap dashboard/checklist.",
]

if heading in text:
    required = [
        "badge.svg?branch=dev-v13-1",
        "ROADMAP-98.0%25-yellow?style=for-the-badge",
        "DONE-391%2F399-1f6feb?style=for-the-badge",
        "STATUS-V13.1%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
        "███████████████████░ 98.0%",
        "| **391** | **8** | **399** | **98.0%** |",
    ]
    for token in required:
        if token not in text:
            raise SystemExit(f"Existing v13.1 scope is inconsistent: missing {token}")
    section = text.split(heading, 1)[1]
    if section.count("- [ ]") != 8 or section.count("- [x]") != 0:
        raise SystemExit("Existing v13.1 section must contain exactly eight pending items and zero completed items")
    print("v13.1 scope already registered and consistent")
    raise SystemExit(0)

required_before = [
    "badge.svg?branch=dev-v13-0",
    "ROADMAP-100.0%25-brightgreen?style=for-the-badge",
    "DONE-391%2F391-1f6feb?style=for-the-badge",
    "STATUS-V13.0%20QUALIFIED-brightgreen?style=for-the-badge",
    "████████████████████ 100.0%",
    "| **391** | **0** | **391** | **100.0%** |",
]
for token in required_before:
    if token not in text:
        raise SystemExit(f"Missing qualified v13.0 dashboard token: {token}")

checked_before = len(re.findall(r'^- \[x\] ', text, re.M))
pending_before = len(re.findall(r'^- \[ \] ', text, re.M))
if (checked_before, pending_before) != (391, 0):
    raise SystemExit(f"Expected qualified 391/391 baseline, got checked={checked_before}, pending={pending_before}")

replacements = {
    "badge.svg?branch=dev-v13-0": "badge.svg?branch=dev-v13-1",
    "ROADMAP-100.0%25-brightgreen?style=for-the-badge": "ROADMAP-98.0%25-yellow?style=for-the-badge",
    "DONE-391%2F391-1f6feb?style=for-the-badge": "DONE-391%2F399-1f6feb?style=for-the-badge",
    "STATUS-V13.0%20QUALIFIED-brightgreen?style=for-the-badge": "STATUS-V13.1%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "████████████████████ 100.0%": "███████████████████░ 98.0%",
    "| **391** | **0** | **391** | **100.0%** |": "| **391** | **8** | **399** | **98.0%** |",
}
for old, new in replacements.items():
    if text.count(old) < 1:
        raise SystemExit(f"Dashboard replacement source missing: {old}")
    text = text.replace(old, new, 1)

section_lines = ["", "", heading + " — IN DEVELOPMENT"] + [f"- [ ] {item}" for item in pending_items] + [""]
text = text.rstrip() + "\n" + "\n".join(section_lines)

checked = len(re.findall(r'^- \[x\] ', text, re.M))
pending = len(re.findall(r'^- \[ \] ', text, re.M))
if (checked, pending, checked + pending) != (391, 8, 399):
    raise SystemExit(f"Checklist mismatch after scope registration: checked={checked}, pending={pending}, total={checked + pending}")

section_text = text.split(heading, 1)[1]
if section_text.count("- [ ]") != 8 or section_text.count("- [x]") != 0:
    raise SystemExit("v13.1 scope must begin with exactly eight pending items and zero completed items")

ROADMAP.write_text(text, encoding="utf-8")
final_text = ROADMAP.read_text(encoding="utf-8")
required_after = [
    marker,
    "## 📊 Overall progress",
    "badge.svg?branch=dev-v13-1",
    "ROADMAP-98.0%25-yellow?style=for-the-badge",
    "DONE-391%2F399-1f6feb?style=for-the-badge",
    "STATUS-V13.1%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "███████████████████░ 98.0%",
    "| **391** | **8** | **399** | **98.0%** |",
]
for token in required_after:
    if token not in final_text:
        raise SystemExit(f"ROADMAP invariant missing after registration: {token}")
if len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', final_text, re.M)) != 1:
    raise SystemExit("Roadmap marker count changed during registration")
print("Registered v13.1 scope: 391/399 = 98.0%, status IN DEVELOPMENT")
