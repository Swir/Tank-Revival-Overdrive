from pathlib import Path
import re

ROADMAP = Path("ROADMAP.md")
text = ROADMAP.read_text(encoding="utf-8")

marker = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
if len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', text, re.M)) != 1:
    raise SystemExit("Expected exactly one standalone SWIR roadmap marker")

heading = "## v12.9 — Component Damage & Emergency Repair Warfare"
if heading in text:
    required = [
        "badge.svg?branch=dev-v12-9",
        "ROADMAP-97.9%25-yellow?style=for-the-badge",
        "DONE-375%2F383-1f6feb?style=for-the-badge",
        "STATUS-V12.9%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
        "███████████████████░ 97.9%",
        "| **375** | **8** | **383** | **97.9%** |",
    ]
    for token in required:
        if token not in text:
            raise SystemExit(f"Existing v12.9 scope is inconsistent: missing {token}")
    section = text.split(heading, 1)[1]
    if section.count("- [ ]") != 8 or section.count("- [x]") != 0:
        raise SystemExit("Existing v12.9 section must contain exactly eight pending items and zero completed items")
    print("v12.9 scope already registered and consistent")
    raise SystemExit(0)

replacements = {
    "badge.svg?branch=dev-v12-8": "badge.svg?branch=dev-v12-9",
    "ROADMAP-100.0%25-brightgreen?style=for-the-badge": "ROADMAP-97.9%25-yellow?style=for-the-badge",
    "DONE-375%2F375-1f6feb?style=for-the-badge": "DONE-375%2F383-1f6feb?style=for-the-badge",
    "STATUS-V12.8%20QUALIFIED-brightgreen?style=for-the-badge": "STATUS-V12.9%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "████████████████████ 100.0%": "███████████████████░ 97.9%",
    "| **375** | **0** | **375** | **100.0%** |": "| **375** | **8** | **383** | **97.9%** |",
}
for old, new in replacements.items():
    if old not in text:
        raise SystemExit(f"Missing roadmap dashboard token: {old}")
    text = text.replace(old, new, 1)

anchor = "- [x] **Exact-SHA qualification finalizer** — qualify v12.8 only after the release-train gate is green for the pinned candidate SHA and the SWIR Roadmap Standard v1 dashboard/checklist is revalidated.\n"
if anchor not in text:
    raise SystemExit("Could not find v12.8 qualification anchor")

section = """

## v12.9 — Component Damage & Emergency Repair Warfare — IN DEVELOPMENT
- [ ] **Progressive component degradation 2.0** — deepen the existing v11.1 ArmorSystem into deterministic Operational/Damaged/Critical/Disabled handling for engine, tracks, gun and ammunition rack without introducing a second vehicle-life authority.
- [ ] **Ammo-to-subsystem coupling** — give Basic/Twin/AP/HE/Plasma/EMP/Incendiary distinct bounded module-damage profiles through the canonical Projectile → ArmorSystem impact path, including facing/overmatch context.
- [ ] **Emergency field repair loop** — add finite per-vehicle repair charges, interruption/cooldown rules and prioritized module recovery that can never heal Health or create infinite sustainment.
- [ ] **Canonical handling and fire-control integration** — make player/enemy movement, turning, reload, weapon function and existing fire-control systems consume progressive ArmorSystem state rather than binary parallel penalties.
- [ ] **AI casualty and mobility response** — make existing tactical navigation/platoon logic react to Critical/Disabled mobility or weapon components with bounded screening, recovery and disengagement behavior while EnemyTank/Rigidbody2D remain movement authority.
- [ ] **Component damage language and repair presentation** — integrate v12.7 pooled combat feedback/HUD with readable module-specific critical/disabled/repair cues under strict presentation budgets.
- [ ] **Deterministic component/repair runtime smoke** — verify threshold transitions, ammo profiles, finite repair invariants, handling/fire-control multipliers, AI response contracts and hard resource caps in the packaged executable.
- [ ] **Exact-candidate Windows qualification** — build Windows x64, run packaged-EXE v12.9 smoke plus v12.8 integration regression checks, record exact SHA/artifact provenance and only then finalize roadmap qualification.
"""
text = text.replace(anchor, anchor + section, 1)

section_text = text.split(heading, 1)[1]
if section_text.count("- [ ]") != 8 or section_text.count("- [x]") != 0:
    raise SystemExit("v12.9 scope must begin with exactly eight pending items and zero completed items")

ROADMAP.write_text(text, encoding="utf-8")

final_text = ROADMAP.read_text(encoding="utf-8")
required = [
    marker,
    "badge.svg?branch=dev-v12-9",
    "ROADMAP-97.9%25-yellow?style=for-the-badge",
    "DONE-375%2F383-1f6feb?style=for-the-badge",
    "STATUS-V12.9%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "## 📊 Overall progress",
    "███████████████████░ 97.9%",
    "| **375** | **8** | **383** | **97.9%** |",
]
for token in required:
    if token not in final_text:
        raise SystemExit(f"ROADMAP invariant missing after registration: {token}")
if len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', final_text, re.M)) != 1:
    raise SystemExit("Roadmap marker count changed during registration")

print("Registered v12.9 scope: 375/383 = 97.9%, status IN DEVELOPMENT")
