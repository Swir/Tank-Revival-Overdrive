#!/usr/bin/env python3
"""Open the v13.9 roadmap scope without awarding unverified progress.

This script is intentionally narrow and idempotent. It transitions only the
qualified v13.8 documentation snapshot into the v13.9 IN DEVELOPMENT snapshot,
adds eight unchecked deliverables, and updates README development wording while
keeping v13.8 as the latest qualified Windows milestone.
"""
from __future__ import annotations

import re
from pathlib import Path

ROADMAP = Path("ROADMAP.md")
README = Path("README.md")

V139_HEADING = "## v13.9 — Suppression Counterplay & Tactical Fire-Support Command — IN DEVELOPMENT"
V139_SCOPE = f"""

{V139_HEADING}
- [ ] **Bounded fire-support command authority** — add a deterministic `BattlefieldFireSupportDirector` readiness / active-window / cooldown state machine that publishes intent only and never spawns/despawns actors, moves rigidbodies, instantiates projectiles or applies `Health` damage.
- [ ] **Suppression-aware support opportunities across 100 rounds** — derive finite tactical support windows from existing v13.8 suppression pressure, round intensity and verified contact state without hidden damage, free kills or uncontrolled random escalation.
- [ ] **Class-aware enemy counterplay** — expose bounded Hold / Brace / Disperse / Evade reaction intent by enemy class while `EnemyTank`, `Rigidbody2D`, `Projectile` and `Health` remain canonical movement, fire and survivability authorities.
- [ ] **Terrain, cohesion and sensor integration** — combine v13.4 cover/exposure, v13.3 squad cohesion and v13.6 contact confidence as read-only inputs so fire-support choices reward positioning and information instead of bypassing those systems.
- [ ] **Player command readiness and anti-spam contract** — provide deliberate activation with hard readiness, range, active-window and cooldown limits, deterministic denial reasons and no parallel ammunition/economy authority.
- [ ] **Readable budgeted fire-support telegraphing** — communicate ready / active / cooldown state, affected zone and enemy reaction cues through bounded HUD/world presentation that respects late-round performance budgets.
- [ ] **Deterministic runtime, authority and performance contracts** — verify bounded actor/effect fan-out, stable command scheduling, class reaction floors, zero hot-path scene scans and unchanged spawn/movement/projectile/damage ownership.
- [ ] **Exact-SHA Windows qualification** — package one Windows x64 candidate, pass the v13.9 fire-support smoke, rerun v13.8/v13.7/v13.6 regressions plus rounds 80/90/100 soak on that same executable, and only then finalize the checklist/progress SVGs.
"""

README_SECTION = """

### Suppression Counterplay & Tactical Fire-Support Command v13.9 — in development

v13.9 is being developed as a bounded command-and-counterplay layer above the qualified v13.8 suppression system. The planned director may publish finite support-window and enemy-reaction intent derived from suppression, terrain, cohesion and sensor confidence, but existing `TankGame`, `EnemyTank`, `Rigidbody2D`, `Projectile` and `Health` systems remain the canonical gameplay authorities. The milestone is not qualified until one exact packaged Windows candidate passes its dedicated smoke, historical regressions and rounds 80/90/100 soak.
"""


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"v13.9 scope opener FAIL: expected exactly one {label}, found {count}")
    return text.replace(old, new, 1)


def open_roadmap(text: str) -> str:
    if V139_HEADING in text:
        return text
    if "## v13.8 — Battlefield Suppression & Morale Warfare — QUALIFIED" not in text:
        raise SystemExit("v13.9 scope opener FAIL: v13.8 qualified anchor missing")
    if len(re.findall(r"^- \[x\] ", text, re.M)) != 455 or re.search(r"^- \[ \] ", text, re.M):
        raise SystemExit("v13.9 scope opener FAIL: expected qualified 455/455 checklist baseline")

    replacements = (
        ("badge.svg?branch=dev-v13-8", "badge.svg?branch=dev-v13-9", "CI branch badge"),
        ("ROADMAP-100.0%25-brightgreen", "ROADMAP-98.3%25-blue", "ROADMAP badge"),
        ("DONE-455%2F455-1f6feb", "DONE-455%2F463-1f6feb", "DONE badge"),
        ("STATUS-V13.8%20QUALIFIED-brightgreen", "STATUS-V13.9%20IN%20DEVELOPMENT-blue", "STATUS badge"),
        ("| **455** | **0** | **455** | **100.0%** |", "| **455** | **8** | **463** | **98.3%** |", "progress table"),
    )
    for old, new, label in replacements:
        text = replace_once(text, old, new, label)
    text = text.rstrip() + V139_SCOPE + "\n"
    return text


def open_readme(text: str) -> str:
    if "Suppression Counterplay & Tactical Fire-Support Command v13.9 — in development" in text:
        return text
    replacements = (
        ("Roadmap-100.0%25%20V13.8%20Qualified", "Roadmap-98.3%25%20V13.9%20In%20Development", "README roadmap badge"),
        ("| Development milestone | **V13.8 QUALIFIED** on `dev-v13-8` |", "| Development milestone | **V13.9 IN DEVELOPMENT** on `dev-v13-9` |", "development milestone row"),
        ("| Roadmap | **455 / 455 completed (100.0%)** — authoritative `ROADMAP.md` scope |", "| Roadmap | **455 / 463 completed (98.3%)** — authoritative `ROADMAP.md` scope |", "roadmap row"),
    )
    for old, new, label in replacements:
        text = replace_once(text, old, new, label)

    suppression_highlight = "| 💢 **Battlefield suppression v13.8 — qualified** | Player impacts and near misses build bounded Steady/Suppressed/Pinned pressure that affects only existing movement/reload/spread paths, with ammo-aware pressure, cohesion-aware recovery, boss resistance and no bonus damage or permanent stun. |"
    fire_support_highlight = "\n| 🎯 **Fire-support counterplay v13.9 — in development** | Planned bounded command windows will turn qualified suppression, cover, cohesion and contact confidence into readable tactical opportunities and class-aware reactions without creating a second damage/projectile authority. |"
    text = replace_once(text, suppression_highlight, suppression_highlight + fire_support_highlight, "v13.8 highlight anchor")

    anchor = "### Battlefield Suppression & Morale Warfare v13.8 — qualified"
    if anchor not in text:
        raise SystemExit("v13.9 scope opener FAIL: README v13.8 section anchor missing")
    # Append the in-development section directly after the existing v13.8 section block by
    # inserting before the next level-3 heading when one exists, otherwise before Search Keywords.
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
    if (completed, open_items) != (455, 8):
        raise SystemExit(f"v13.9 scope opener FAIL: checklist is {completed} completed / {open_items} open, expected 455/8")
    required_roadmap = (
        "<!-- SWIR-ROADMAP-STANDARD:v1 -->",
        "assets/readme/progress-mini.svg",
        "| **455** | **8** | **463** | **98.3%** |",
        "STATUS-V13.9%20IN%20DEVELOPMENT-blue",
        V139_HEADING,
    )
    for token in required_roadmap:
        if token not in roadmap:
            raise SystemExit("v13.9 scope opener FAIL: ROADMAP missing " + token)
    required_readme = (
        "<!-- SWIR-README-STANDARD:v2 -->",
        "assets/readme/progress-card.svg",
        "## 🔎 Search Keywords",
        "**V13.9 IN DEVELOPMENT** on `dev-v13-9`",
        "**455 / 463 completed (98.3%)**",
        "Latest qualified milestone | **v13.8**",
    )
    for token in required_readme:
        if token not in readme:
            raise SystemExit("v13.9 scope opener FAIL: README missing " + token)


def main() -> None:
    roadmap = ROADMAP.read_text(encoding="utf-8")
    readme = README.read_text(encoding="utf-8")
    roadmap2 = open_roadmap(roadmap)
    readme2 = open_readme(readme)
    verify(roadmap2, readme2)
    ROADMAP.write_text(roadmap2, encoding="utf-8")
    README.write_text(readme2, encoding="utf-8")
    changed = roadmap2 != roadmap or readme2 != readme
    print("v13.9 scope opener: " + ("UPDATED 455/463 (98.3%)" if changed else "NO-OP; scope already open"))


if __name__ == "__main__":
    main()
