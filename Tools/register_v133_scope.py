from pathlib import Path
import re

ROADMAP = Path("ROADMAP.md")
README = Path("README.md")
text = ROADMAP.read_text(encoding="utf-8")
marker = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
heading = "## v13.3 — Battlefield Cohesion & Squad Command Warfare"

if len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', text, re.M)) != 1:
    raise SystemExit("Expected exactly one standalone SWIR roadmap marker")

pending_items = [
    "**Deterministic bounded squad roster** — assign eligible enemy tanks into fixed four-vehicle squads at spawn registration time with at most 24 tracked actors / six squads, no scene scans, no unbounded collections and no second spawn authority.",
    "**Leader, wingman, breacher and support roles** — maintain one effective leader per live squad, deterministic member roles and bounded leader promotion after casualties while existing EnemyTank / platoon systems retain movement, targeting and firing authority.",
    "**Cohesion state machine and regroup warfare** — expose Forming / Cohesive / Shocked / Regrouping squad states with hard timers and deterministic recovery so formations react to losses without frame-by-frame command thrashing.",
    "**Leader-loss counterplay** — destroying a squad leader creates a finite, readable cohesion shock that temporarily softens movement/fire-control intent and forces regroup before a promoted leader restores coordination; no hidden HP changes, enemy deletion or scripted stun authority.",
    "**Formation intent integration** — feed bounded spacing/regroup direction plus movement/reload/spread multipliers through existing EnemyTank and AdaptivePlatoonManeuver paths only; Rigidbody2D, Projectile, Health, ArmorSystem and TankGame remain canonical authorities.",
    "**Squad readability and tactical telemetry** — provide bounded world-space leader markers plus compact squad/cohesion/leader-loss telemetry with fixed refresh and actor budgets so the player can deliberately break enemy command structure.",
    "**Packaged-EXE v13.3 runtime smoke** — validate roster capacity, deterministic assignments, leader promotion, shock/regroup timing, bounded posture/formation intent and authority contracts, then rerun v13.2/v13.1/v13.0/v12.9/v12.8 regressions plus rounds 80/90/100 soak on the same executable.",
    "**Exact-SHA Windows qualification** — deterministically package one Windows x64 candidate with provenance, require the complete v13.3 gate to pass and only then finalize the SWIR roadmap dashboard/checklist and progress SVGs.",
]

if heading in text:
    required = [
        "badge.svg?branch=dev-v13-3",
        "ROADMAP-98.1%25-yellow?style=for-the-badge",
        "DONE-407%2F415-1f6feb?style=for-the-badge",
        "STATUS-V13.3%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
        "███████████████████░ 98.1%",
        "| **407** | **8** | **415** | **98.1%** |",
    ]
    for token in required:
        if token not in text:
            raise SystemExit(f"Existing v13.3 scope is inconsistent: missing {token}")
    section = text.split(heading, 1)[1]
    if section.count("- [ ]") != 8 or section.count("- [x]") != 0:
        raise SystemExit("Existing v13.3 section must contain exactly eight pending items and zero completed items")
    print("v13.3 scope already registered and consistent")
else:
    required_before = [
        "badge.svg?branch=dev-v13-2",
        "ROADMAP-100.0%25-brightgreen?style=for-the-badge",
        "DONE-407%2F407-1f6feb?style=for-the-badge",
        "STATUS-V13.2%20QUALIFIED-brightgreen?style=for-the-badge",
        "████████████████████ 100.0%",
        "| **407** | **0** | **407** | **100.0%** |",
    ]
    for token in required_before:
        if token not in text:
            raise SystemExit(f"Missing qualified v13.2 dashboard token: {token}")

    checked_before = len(re.findall(r'^- \[x\] ', text, re.M))
    pending_before = len(re.findall(r'^- \[ \] ', text, re.M))
    if (checked_before, pending_before) != (407, 0):
        raise SystemExit(f"Expected qualified 407/407 baseline, got checked={checked_before}, pending={pending_before}")

    replacements = {
        "badge.svg?branch=dev-v13-2": "badge.svg?branch=dev-v13-3",
        "ROADMAP-100.0%25-brightgreen?style=for-the-badge": "ROADMAP-98.1%25-yellow?style=for-the-badge",
        "DONE-407%2F407-1f6feb?style=for-the-badge": "DONE-407%2F415-1f6feb?style=for-the-badge",
        "STATUS-V13.2%20QUALIFIED-brightgreen?style=for-the-badge": "STATUS-V13.3%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
        "████████████████████ 100.0%": "███████████████████░ 98.1%",
        "| **407** | **0** | **407** | **100.0%** |": "| **407** | **8** | **415** | **98.1%** |",
    }
    for old, new in replacements.items():
        if text.count(old) < 1:
            raise SystemExit(f"Dashboard replacement source missing: {old}")
        text = text.replace(old, new, 1)

    section_lines = ["", "", heading + " — IN DEVELOPMENT"] + [f"- [ ] {item}" for item in pending_items] + [""]
    text = text.rstrip() + "\n" + "\n".join(section_lines)
    ROADMAP.write_text(text, encoding="utf-8")

final_text = ROADMAP.read_text(encoding="utf-8")
checked = len(re.findall(r'^- \[x\] ', final_text, re.M))
pending = len(re.findall(r'^- \[ \] ', final_text, re.M))
if (checked, pending, checked + pending) != (407, 8, 415):
    raise SystemExit(f"Checklist mismatch after scope registration: checked={checked}, pending={pending}, total={checked + pending}")
section_text = final_text.split(heading, 1)[1]
if section_text.count("- [ ]") != 8 or section_text.count("- [x]") != 0:
    raise SystemExit("v13.3 scope must begin with exactly eight pending items and zero completed items")
for token in [marker, "## 📊 Overall progress", "badge.svg?branch=dev-v13-3", "ROADMAP-98.1%25-yellow?style=for-the-badge", "DONE-407%2F415-1f6feb?style=for-the-badge", "STATUS-V13.3%20IN%20DEVELOPMENT-yellow?style=for-the-badge", "███████████████████░ 98.1%", "| **407** | **8** | **415** | **98.1%** |"]:
    if token not in final_text:
        raise SystemExit(f"ROADMAP invariant missing after registration: {token}")
if len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', final_text, re.M)) != 1:
    raise SystemExit("Roadmap marker count changed during registration")

readme = README.read_text(encoding="utf-8")
if "<!-- SWIR-README-STANDARD:v2 -->" not in readme or "## 🔎 Search Keywords" not in readme:
    raise SystemExit("README PRO v2/Search Keywords contract missing")
repls = {
    "Roadmap-100.0%25%20V13.2%20Qualified-02050A": "Roadmap-98.1%25%20V13.3%20In%20Development-02050A",
    "| Development milestone | **V13.2 QUALIFIED** on `dev-v13-2` |": "| Development milestone | **V13.3 IN DEVELOPMENT** on `dev-v13-3` |",
    "| Roadmap | **407 / 407 completed (100.0%)** — authoritative `ROADMAP.md` scope |": "| Roadmap | **407 / 415 completed (98.1%)** — authoritative `ROADMAP.md` scope |",
    "| Source development | Unity 6000.3.17f1 for the qualified v13.2 CI path |": "| Source development | Unity 6000.3.17f1 for the current Windows qualification path |",
    "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **407 / 407 (100.0%) — V13.2 QUALIFIED**. Release readiness is tracked separately by exact-candidate Windows qualification gates.": "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **407 / 415 (98.1%) — V13.3 IN DEVELOPMENT**. The last qualified gameplay milestone is v13.2; release readiness remains separate from roadmap scope and is tracked by exact-candidate Windows qualification gates.",
    "- **Development milestone:** v13.2 is qualified on `dev-v13-2` but has not been published as a new public v13.2 release.": "- **Development milestone:** v13.3 is in development on `dev-v13-3`; v13.2 remains the last qualified milestone and has not been published as a new public v13.2 release.",
}
for old, new in repls.items():
    if old not in readme:
        raise SystemExit(f"README scope-sync source missing: {old}")
    readme = readme.replace(old, new, 1)
README.write_text(readme, encoding="utf-8")
print("Registered v13.3 scope: 407/415 = 98.1%, status IN DEVELOPMENT; README status synchronized")
