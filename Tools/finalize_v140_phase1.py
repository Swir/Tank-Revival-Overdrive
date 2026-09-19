#!/usr/bin/env python3
"""Close only the source-verified v14.0 phase-one deliverables."""
from __future__ import annotations

import re
from pathlib import Path

ROADMAP = Path("ROADMAP.md")
README = Path("README.md")

FIRST_THREE = (
    "Bounded counter-battery threat authority",
    "Fire-support signature and lock acquisition across 100 rounds",
    "Canonical enemy barrage execution",
)


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"v14.0 phase-one finalizer FAIL: expected one {label}, found {count}")
    return text.replace(old, new, 1)


def finalize_roadmap(text: str) -> str:
    completed = len(re.findall(r"^- \[x\] ", text, re.M))
    opened = len(re.findall(r"^- \[ \] ", text, re.M))
    if (completed, opened) == (466, 5):
        return text
    if (completed, opened) != (463, 8):
        raise SystemExit(f"v14.0 phase-one finalizer FAIL: expected 463/8 baseline, got {completed}/{opened}")

    for label in FIRST_THREE:
        pattern = rf"^- \[ \] (\*\*{re.escape(label)}\*\*.*)$"
        text, count = re.subn(pattern, r"- [x] \1", text, count=1, flags=re.M)
        if count != 1:
            raise SystemExit("v14.0 phase-one finalizer FAIL: missing open item " + label)

    replacements = (
        ("ROADMAP-98.3%25-blue", "ROADMAP-98.9%25-blue", "ROADMAP badge"),
        ("DONE-463%2F471-1f6feb", "DONE-466%2F471-1f6feb", "DONE badge"),
        ("| **463** | **8** | **471** | **98.3%** |", "| **466** | **5** | **471** | **98.9%** |", "progress table"),
    )
    for old, new, label in replacements:
        text = replace_once(text, old, new, label)
    return text


def finalize_readme(text: str) -> str:
    if "**466 / 471 completed (98.9%)**" in text:
        return text
    replacements = (
        ("Roadmap-98.3%25%20V14.0%20In%20Development", "Roadmap-98.9%25%20V14.0%20In%20Development", "README roadmap badge"),
        ("| Roadmap | **463 / 471 completed (98.3%)** — authoritative `ROADMAP.md` scope |", "| Roadmap | **466 / 471 completed (98.9%)** — authoritative `ROADMAP.md` scope |", "roadmap row"),
        ("The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **463 / 471 (98.3%) — V14.0 IN DEVELOPMENT**.", "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **466 / 471 (98.9%) — V14.0 IN DEVELOPMENT**.", "roadmap narrative"),
        ("| 📍 **Counter-battery warfare v14.0 — in development** | Planned enemy fire-control pressure will react to repeated support signatures with readable lock/barrage states while relocation, terrain and sensor information provide direct counterplay without a parallel damage authority. |", "| 📍 **Counter-battery warfare v14.0 — in development** | Source-verified exposure, search/lock/barrage state and canonical enemy projectile execution now answer repeated fire-support use; terrain/sensor/observer presentation and packaged runtime qualification remain open. |", "v14.0 highlight"),
        ("v14.0 is being developed as a bounded enemy counter-battery layer above the qualified v13.9 fire-support system. Repeated support use will create readable exposure signatures that eligible enemy observers can turn into finite SEARCHING / LOCKED / BARRAGE pressure, while player relocation, terrain and sensor information provide direct counterplay. The new director remains intent-only; `TankGame`, `Projectile`, `Health`, `EnemyTank` and `Rigidbody2D` keep canonical gameplay authority. The milestone is not qualified until one exact packaged Windows candidate passes its dedicated smoke, historical regressions and rounds 80/90/100 soak.", "v14.0 phase one is source-verified: v13.9 support-strike intent now creates bounded exposure, eligible battlefield observers build deterministic SEARCHING / LOCKED / BARRAGE pressure across the 100-round profile, and a separate execution bridge forwards enemy barrage intent only through canonical `TankGame.SpawnProjectile`. The remaining terrain/sensor relocation integration, command-posture coupling, presentation, packaged runtime contracts and exact Windows qualification stay open; `Health`, `Projectile`, `EnemyTank` and `Rigidbody2D` retain canonical gameplay authority.", "v14.0 section body"),
    )
    for old, new, label in replacements:
        text = replace_once(text, old, new, label)
    return text


def verify(roadmap: str, readme: str) -> None:
    completed = len(re.findall(r"^- \[x\] ", roadmap, re.M))
    opened = len(re.findall(r"^- \[ \] ", roadmap, re.M))
    if (completed, opened) != (466, 5):
        raise SystemExit(f"v14.0 phase-one finalizer FAIL: checklist {completed}/{opened}, expected 466/5")
    if "| **466** | **5** | **471** | **98.9%** |" not in roadmap:
        raise SystemExit("v14.0 phase-one finalizer FAIL: roadmap table stale")
    if "STATUS-V14.0%20IN%20DEVELOPMENT-blue" not in roadmap:
        raise SystemExit("v14.0 phase-one finalizer FAIL: status must remain IN DEVELOPMENT")
    for label in FIRST_THREE:
        if not re.search(rf"^- \[x\] \*\*{re.escape(label)}\*\*", roadmap, re.M):
            raise SystemExit("v14.0 phase-one finalizer FAIL: checked item missing " + label)
    if "**466 / 471 completed (98.9%)**" not in readme:
        raise SystemExit("v14.0 phase-one finalizer FAIL: README progress stale")
    if "Latest qualified milestone | **v13.9**" not in readme:
        raise SystemExit("v14.0 phase-one finalizer FAIL: v13.9 must remain latest qualified milestone")


def main() -> None:
    roadmap = ROADMAP.read_text(encoding="utf-8")
    readme = README.read_text(encoding="utf-8")
    roadmap2 = finalize_roadmap(roadmap)
    readme2 = finalize_readme(readme)
    verify(roadmap2, readme2)
    ROADMAP.write_text(roadmap2, encoding="utf-8")
    README.write_text(readme2, encoding="utf-8")
    changed = roadmap2 != roadmap or readme2 != readme
    print("v14.0 phase-one finalizer: " + ("UPDATED 466/471 (98.9%)" if changed else "NO-OP; already finalized"))


if __name__ == "__main__":
    main()
