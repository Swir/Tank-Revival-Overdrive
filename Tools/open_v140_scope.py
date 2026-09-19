#!/usr/bin/env python3
"""Open the v14.0 roadmap scope without awarding unverified progress.

Transitions the qualified v13.9 documentation snapshot into the v14.0
IN DEVELOPMENT snapshot, adds eight unchecked deliverables, and updates README
status while keeping v13.9 as the latest qualified Windows milestone.
"""
from __future__ import annotations

import re
from pathlib import Path

ROADMAP = Path("ROADMAP.md")
README = Path("README.md")

V140_HEADING = "## v14.0 — Counter-Battery & Mobile Fire-Control Warfare — IN DEVELOPMENT"
V140_SCOPE = f"""

{V140_HEADING}
- [ ] **Bounded counter-battery threat authority** — add a deterministic `CounterBatteryDirectorV140` Quiet / Searching / Locked / Barrage / Relocating state machine that publishes enemy fire intent only and never applies direct `Health` damage, teleports actors or replaces TankGame spawn authority.
- [ ] **Fire-support signature and lock acquisition across 100 rounds** — convert existing v13.9 support-strike emissions into bounded exposure signatures with deterministic decay, repeat-use penalties and round-scaled enemy acquisition pressure instead of hidden random punishment.
- [ ] **Canonical enemy barrage execution** — execute counter-battery salvos only through a separate intent bridge into existing `TankGame.SpawnProjectile` / `Projectile` authority with strict shell, cadence and damage caps and no parallel direct-damage path.
- [ ] **Terrain, sensor and relocation counterplay** — make movement between battlefield cells, v13.4 cover/exposure and v13.6 sensor information materially reduce or break enemy lock so the player can counter fire-control pressure through positioning rather than scripted immunity.
- [ ] **Observer-class and command integration** — derive bounded acquisition strength from eligible Sniper / Siege / Elite / Supply-command actors plus existing v13.3 cohesion and v13.2 command posture without creating a second enemy AI or targeting authority.
- [ ] **Readable budgeted warning and barrage presentation** — expose SEARCHING / LOCKED / INCOMING / RELOCATE states, impact telegraphs and observer pressure through fixed-cap HUD/world cues that preserve late-round battlefield visibility.
- [ ] **Deterministic runtime, authority and performance contracts** — verify exposure decay, lock/break thresholds, observer caps, shell budgets, no hot-path scene scans and unchanged Health/Projectile/TankGame ownership in the packaged executable.
- [ ] **Exact-SHA Windows qualification** — package one Windows x64 candidate, pass the v14.0 counter-battery smoke, rerun v13.9/v13.8/v13.7/v13.6 regressions plus rounds 80/90/100 soak on that same executable, and only then finalize checklist/progress SVGs.
"""

README_SECTION = """

### Counter-Battery & Mobile Fire-Control Warfare v14.0 — in development

v14.0 is being developed as a bounded enemy counter-battery layer above the qualified v13.9 fire-support system. Repeated support use will create readable exposure signatures that eligible enemy observers can turn into finite SEARCHING / LOCKED / BARRAGE pressure, while player relocation, terrain and sensor information provide direct counterplay. The new director remains intent-only; `TankGame`, `Projectile`, `Health`, `EnemyTank` and `Rigidbody2D` keep canonical gameplay authority. The milestone is not qualified until one exact packaged Windows candidate passes its dedicated smoke, historical regressions and rounds 80/90/100 soak.
"""


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"v14.0 scope opener FAIL: expected exactly one {label}, found {count}")
    return text.replace(old, new, 1)


def open_roadmap(text: str) -> str:
    if V140_HEADING in text:
        return text
    if "## v13.9 — Suppression Counterplay & Tactical Fire-Support Command — QUALIFIED" not in text:
        raise SystemExit("v14.0 scope opener FAIL: v13.9 qualified anchor missing")
    if len(re.findall(r"^- \[x\] ", text, re.M)) != 463 or re.search(r"^- \[ \] ", text, re.M):
        raise SystemExit("v14.0 scope opener FAIL: expected qualified 463/463 checklist baseline")

    replacements = (
        ("badge.svg?branch=dev-v13-9", "badge.svg?branch=dev-v14-0", "CI branch badge"),
        ("ROADMAP-100.0%25-brightgreen", "ROADMAP-98.3%25-blue", "ROADMAP badge"),
        ("DONE-463%2F463-1f6feb", "DONE-463%2F471-1f6feb", "DONE badge"),
        ("STATUS-V13.9%20QUALIFIED-brightgreen", "STATUS-V14.0%20IN%20DEVELOPMENT-blue", "STATUS badge"),
        ("| **463** | **0** | **463** | **100.0%** |", "| **463** | **8** | **471** | **98.3%** |", "progress table"),
    )
    for old, new, label in replacements:
        text = replace_once(text, old, new, label)
    text = text.rstrip() + V140_SCOPE + "\n"
    return text


def open_readme(text: str) -> str:
    if "Counter-Battery & Mobile Fire-Control Warfare v14.0 — in development" in text:
        return text
    replacements = (
        ("Roadmap-100.0%25%20V13.9%20Qualified", "Roadmap-98.3%25%20V14.0%20In%20Development", "README roadmap badge"),
        ("| Development milestone | **V13.9 QUALIFIED** on `dev-v13-9` |", "| Development milestone | **V14.0 IN DEVELOPMENT** on `dev-v14-0` |", "development milestone row"),
        ("| Roadmap | **463 / 463 completed (100.0%)** — authoritative `ROADMAP.md` scope |", "| Roadmap | **463 / 471 completed (98.3%)** — authoritative `ROADMAP.md` scope |", "roadmap row"),
        ("The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **463 / 463 (100.0%) — V13.9 QUALIFIED**.", "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **463 / 471 (98.3%) — V14.0 IN DEVELOPMENT**.", "README roadmap narrative"),
        ("- **Development milestone:** v13.9 is qualified on `dev-v13-9`; this milestone qualification does not automatically create or replace a public release.", "- **Development milestone:** v14.0 is in development on `dev-v14-0`; v13.9 remains the latest qualified milestone and this development scope does not automatically create or replace a public release.", "release milestone row"),
    )
    for old, new, label in replacements:
        text = replace_once(text, old, new, label)

    fire_support_highlight = "| 🎯 **Fire-support counterplay v13.9 — qualified** | Earned F-key command windows combine qualified suppression, cover, cohesion and verified contacts into bounded fire-support intent with class-aware reactions, anti-spam cooldowns and canonical TankGame projectile execution. |"
    counter_battery_highlight = "\n| 📍 **Counter-battery warfare v14.0 — in development** | Planned enemy fire-control pressure will react to repeated support signatures with readable lock/barrage states while relocation, terrain and sensor information provide direct counterplay without a parallel damage authority. |"
    text = replace_once(text, fire_support_highlight, fire_support_highlight + counter_battery_highlight, "v13.9 highlight anchor")

    anchor = "### Suppression Counterplay & Tactical Fire-Support Command v13.9 — qualified"
    if anchor not in text:
        raise SystemExit("v14.0 scope opener FAIL: README v13.9 section anchor missing")
    start = text.index(anchor)
    next_heading = text.find("\n### ", start + len(anchor))
    search_keywords = text.find("\n## 🔎 Search Keywords", start)
    candidates = [p for p in (next_heading, search_keywords) if p != -1]
    insert_at = min(candidates) if candidates else len(text)
    text = text[:insert_at].rstrip() + README_SECTION + "\n" + text[insert_at:].lstrip("\n")
    return text


def verify(roadmap: str, readme: str) -> None:
    completed = len(re.findall(r"^- \[x\] ", roadmap, re.M))
    open_items = len(re.findall(r"^- \[ \] ", roadmap, re.M))
    if (completed, open_items) != (463, 8):
        raise SystemExit(f"v14.0 scope opener FAIL: checklist is {completed} completed / {open_items} open, expected 463/8")
    required_roadmap = (
        "<!-- SWIR-ROADMAP-STANDARD:v1 -->",
        "assets/readme/progress-mini.svg",
        "| **463** | **8** | **471** | **98.3%** |",
        "STATUS-V14.0%20IN%20DEVELOPMENT-blue",
        V140_HEADING,
    )
    for token in required_roadmap:
        if token not in roadmap:
            raise SystemExit("v14.0 scope opener FAIL: ROADMAP missing " + token)
    required_readme = (
        "<!-- SWIR-README-STANDARD:v2 -->",
        "assets/readme/progress-card.svg",
        "## 🔎 Search Keywords",
        "**V14.0 IN DEVELOPMENT** on `dev-v14-0`",
        "**463 / 471 completed (98.3%)**",
        "Latest qualified milestone | **v13.9**",
    )
    for token in required_readme:
        if token not in readme:
            raise SystemExit("v14.0 scope opener FAIL: README missing " + token)


def main() -> None:
    roadmap = ROADMAP.read_text(encoding="utf-8")
    readme = README.read_text(encoding="utf-8")
    roadmap2 = open_roadmap(roadmap)
    readme2 = open_readme(readme)
    verify(roadmap2, readme2)
    ROADMAP.write_text(roadmap2, encoding="utf-8")
    README.write_text(readme2, encoding="utf-8")
    changed = roadmap2 != roadmap or readme2 != readme
    print("v14.0 scope opener: " + ("UPDATED 463/471 (98.3%)" if changed else "NO-OP; scope already open"))


if __name__ == "__main__":
    main()
