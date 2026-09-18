#!/usr/bin/env python3
"""Open the v13.5 Battlefield Weather & Visibility Warfare roadmap scope safely.

This script is intentionally one-shot/idempotent. It adds the new unchecked scope
before gameplay implementation, recomputes the protected dashboard from the
actual checklist, and leaves SVG generation to the canonical generator.
"""
from __future__ import annotations

import re
from pathlib import Path
from urllib.parse import quote

ROADMAP = Path("ROADMAP.md")
MILESTONE = "v13.5 — Battlefield Weather & Visibility Warfare"
STATUS = "V13.5 IN DEVELOPMENT"
BRANCH = "dev-v13-5"
ITEMS = [
    "**Deterministic 100-round weather planner** — derive one bounded Clear / Mist / Rain / Storm / Snow combat-weather profile for every campaign round with deterministic signatures, sector-aware intensity and an adjacent-repeat guard; TankGame remains round authority.",
    "**Canonical mobility integration with terrain coupling** — apply tightly bounded traction modifiers inside the existing PlayerTank / EnemyTank Rigidbody2D movement paths and couple Rain/Snow penalties to canonical TacticalTerrainMap state without adding a second movement or physics authority.",
    "**Visibility-aware gunnery and reload pressure** — feed bounded player/enemy spread and enemy reload scales into existing fire-control paths so low-visibility fronts materially change engagement tempo while Projectile, ArmorSystem and FireControlBallisticsDirector remain authoritative.",
    "**Ammo-aware player counterplay** — let precision/AP and Plasma ammunition retain a measured stabilization advantage in severe visibility conditions without free damage, hidden aim assist or bypassing the existing ammo economy.",
    "**Budgeted weather presentation layer** — add restrained full-screen tint, deterministic precipitation/whiteout cues and compact weather telemetry with hard visual budgets and no unbounded particle or scene-object growth.",
    "**Fairness floors and anti-snowball weather policy** — enforce hard lower/upper bounds for traction, spread and reload effects so no weather state can immobilize the player, create unavoidable fire-control failure or silently alter Health/damage authority.",
    "**Packaged-EXE v13.5 runtime smoke** — validate all 100 plans, profile coverage, anti-repeat determinism, mobility/gunnery bounds, ammo counterplay and runtime installation, then rerun v13.4/v13.3 regressions plus rounds 80/90/100 soak on the same executable.",
    "**Exact-SHA Windows qualification** — deterministically package one Windows x64 candidate with provenance, require the complete v13.5 gate to pass and only then finalize roadmap/checklist/progress SVGs.",
]


def fail(message: str) -> None:
    raise RuntimeError(message)


def main() -> None:
    text = ROADMAP.read_text(encoding="utf-8")
    if "<!-- SWIR-ROADMAP-STANDARD:v1 -->" not in text:
        fail("protected SWIR roadmap marker missing")
    if MILESTONE in text:
        print("v13.5 roadmap scope already present; no-op")
        return
    if "v13.4 — Tactical Terrain & Cover Warfare — QUALIFIED" not in text:
        fail("v13.4 qualified predecessor not present")

    block = "\n\n## " + MILESTONE + " — IN DEVELOPMENT\n" + "\n".join(f"- [ ] {item}" for item in ITEMS) + "\n"
    text = text.rstrip() + block

    completed = len(re.findall(r"^- \[x\] ", text, re.M))
    remaining = len(re.findall(r"^- \[ \] ", text, re.M))
    total = completed + remaining
    if remaining != len(ITEMS):
        fail(f"unexpected open-item count after scope registration: {remaining}")
    percent = round(completed * 100.0 / total, 1)

    text = re.sub(r"ROADMAP-[0-9.]+%25-(?:brightgreen|yellow|orange|red|blue|1f6feb)", f"ROADMAP-{percent:.1f}%25-yellow", text, count=1)
    text = re.sub(r"DONE-\d+%2F\d+-1f6feb", f"DONE-{completed}%2F{total}-1f6feb", text, count=1)
    encoded_status = quote(STATUS, safe="")
    text = re.sub(r"STATUS-[^\"?]+?-(?:yellow|brightgreen|red|orange|blue|1f6feb)\?", f"STATUS-{encoded_status}-yellow?", text, count=1)
    text = re.sub(
        r"\| \*\*\d+\*\* \| \*\*\d+\*\* \| \*\*\d+\*\* \| \*\*[0-9.]+%\*\* \|",
        f"| **{completed}** | **{remaining}** | **{total}** | **{percent:.1f}%** |",
        text,
        count=1,
    )
    text = re.sub(r"badge\.svg\?branch=dev-v13-\d+", f"badge.svg?branch={BRANCH}", text, count=1)

    ROADMAP.write_text(text, encoding="utf-8")
    print(f"v13.5 roadmap opened: {completed}/{total} ({percent:.1f}%), remaining={remaining}")


if __name__ == "__main__":
    main()
