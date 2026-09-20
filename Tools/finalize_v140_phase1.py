#!/usr/bin/env python3
"""Close the source-verified v14.0 counterplay deliverables after static qualification."""
from __future__ import annotations

import re
from pathlib import Path

ROADMAP = Path("ROADMAP.md")
README = Path("README.md")

PHASE_ONE = (
    "Bounded counter-battery threat authority",
    "Fire-support signature and lock acquisition across 100 rounds",
    "Canonical enemy barrage execution",
)
COUNTERPLAY = (
    "Terrain, sensor and relocation counterplay",
    "Observer-class and command integration",
    "Readable budgeted warning and barrage presentation",
)
FINAL_PHASE = (
    "Deterministic runtime, authority and performance contracts",
    "Exact-SHA Windows qualification",
)
V140_HEADING = "## v14.0 — Counter-Battery & Mobile Fire-Control Warfare — IN DEVELOPMENT"


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"v14.0 source-progress finalizer FAIL: expected one {label}, found {count}")
    return text.replace(old, new, 1)


def v140_section(text: str) -> str:
    start = text.find(V140_HEADING)
    if start < 0:
        raise SystemExit("v14.0 source-progress finalizer FAIL: active milestone heading missing")
    tail = text[start + len(V140_HEADING):]
    next_heading = re.search(r"^## ", tail, re.M)
    return tail[:next_heading.start()] if next_heading else tail


def close_v140_item(text: str, label: str) -> str:
    start = text.find(V140_HEADING)
    if start < 0:
        raise SystemExit("v14.0 source-progress finalizer FAIL: active milestone heading missing")
    tail_start = start + len(V140_HEADING)
    tail = text[tail_start:]
    next_heading = re.search(r"^## ", tail, re.M)
    end = tail_start + (next_heading.start() if next_heading else len(tail))
    section = text[tail_start:end]
    pattern = rf"^- \[ \] (\*\*{re.escape(label)}\*\*.*)$"
    section2, count = re.subn(pattern, r"- [x] \1", section, count=1, flags=re.M)
    if count != 1:
        raise SystemExit("v14.0 source-progress finalizer FAIL: missing open v14.0 item " + label)
    return text[:tail_start] + section2 + text[end:]


def finalize_roadmap(text: str) -> str:
    completed = len(re.findall(r"^- \[x\] ", text, re.M))
    opened = len(re.findall(r"^- \[ \] ", text, re.M))
    if (completed, opened) == (469, 2):
        return text
    if (completed, opened) != (466, 5):
        raise SystemExit(f"v14.0 source-progress finalizer FAIL: expected 466/5 baseline, got {completed}/{opened}")

    for label in COUNTERPLAY:
        text = close_v140_item(text, label)

    replacements = (
        ("ROADMAP-98.9%25-blue", "ROADMAP-99.6%25-blue", "ROADMAP badge"),
        ("DONE-466%2F471-1f6feb", "DONE-469%2F471-1f6feb", "DONE badge"),
        ("| **466** | **5** | **471** | **98.9%** |", "| **469** | **2** | **471** | **99.6%** |", "progress table"),
    )
    for old, new, label in replacements:
        text = replace_once(text, old, new, label)
    return text


def finalize_readme(text: str) -> str:
    if "**469 / 471 completed (99.6%)**" in text:
        return text
    replacements = (
        ("Roadmap-98.9%25%20V14.0%20In%20Development", "Roadmap-99.6%25%20V14.0%20In%20Development", "README roadmap badge"),
        ("| Roadmap | **466 / 471 completed (98.9%)** — authoritative `ROADMAP.md` scope |", "| Roadmap | **469 / 471 completed (99.6%)** — authoritative `ROADMAP.md` scope |", "roadmap row"),
        ("The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **466 / 471 (98.9%) — V14.0 IN DEVELOPMENT**.", "The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **469 / 471 (99.6%) — V14.0 IN DEVELOPMENT**.", "roadmap narrative"),
        ("| 📍 **Counter-battery warfare v14.0 — in development** | Source-verified exposure, search/lock/barrage state and canonical enemy projectile execution now answer repeated fire-support use; terrain/sensor/observer presentation and packaged runtime qualification remain open. |", "| 📍 **Counter-battery warfare v14.0 — in development** | Source-verified terrain/sensor relocation, bounded observer/command weighting and readable SEARCHING/LOCKED/INCOMING/RELOCATE presentation now extend the counter-battery barrage authority; packaged runtime contracts and exact Windows qualification remain open. |", "v14.0 highlight"),
        ("v14.0 phase one is source-verified: v13.9 support-strike intent now creates bounded exposure, eligible battlefield observers build deterministic SEARCHING / LOCKED / BARRAGE pressure across the 100-round profile, and a separate execution bridge forwards enemy barrage intent only through canonical `TankGame.SpawnProjectile`. The remaining terrain/sensor relocation integration, command-posture coupling, presentation, packaged runtime contracts and exact Windows qualification stay open; `Health`, `Projectile`, `EnemyTank` and `Rigidbody2D` retain canonical gameplay authority.", "v14.0 source counterplay is now verified: v13.9 support-strike intent creates bounded exposure, terrain and sensor state reduce lock pressure through explicit relocation counterplay, and at most six eligible observers contribute cohesion- and command-weighted acquisition. SEARCHING / WARNING / LOCKED / INCOMING / RELOCATE presentation is bounded and readable while the separate execution bridge remains the only v14.0 path into canonical `TankGame.SpawnProjectile`. Packaged runtime contracts and exact Windows qualification remain open; `Health`, `Projectile`, `EnemyTank` and `Rigidbody2D` retain canonical gameplay authority.", "v14.0 section body"),
    )
    for old, new, label in replacements:
        text = replace_once(text, old, new, label)
    return text


def checked_in_v140(roadmap: str, label: str) -> bool:
    section = v140_section(roadmap)
    return bool(re.search(rf"^- \[x\] \*\*{re.escape(label)}\*\*", section, re.M))


def open_in_v140(roadmap: str, label: str) -> bool:
    section = v140_section(roadmap)
    return bool(re.search(rf"^- \[ \] \*\*{re.escape(label)}\*\*", section, re.M))


def verify(roadmap: str, readme: str) -> None:
    completed = len(re.findall(r"^- \[x\] ", roadmap, re.M))
    opened = len(re.findall(r"^- \[ \] ", roadmap, re.M))
    if (completed, opened) != (469, 2):
        raise SystemExit(f"v14.0 source-progress finalizer FAIL: checklist {completed}/{opened}, expected 469/2")
    if "| **469** | **2** | **471** | **99.6%** |" not in roadmap:
        raise SystemExit("v14.0 source-progress finalizer FAIL: roadmap table stale")
    if "STATUS-V14.0%20IN%20DEVELOPMENT-blue" not in roadmap:
        raise SystemExit("v14.0 source-progress finalizer FAIL: status must remain IN DEVELOPMENT")
    for label in PHASE_ONE + COUNTERPLAY:
        if not checked_in_v140(roadmap, label):
            raise SystemExit("v14.0 source-progress finalizer FAIL: checked item missing " + label)
    for label in FINAL_PHASE:
        if not open_in_v140(roadmap, label):
            raise SystemExit("v14.0 source-progress finalizer FAIL: final qualification item must remain open " + label)
    if "**469 / 471 completed (99.6%)**" not in readme:
        raise SystemExit("v14.0 source-progress finalizer FAIL: README progress stale")
    if "Latest qualified milestone | **v13.9**" not in readme:
        raise SystemExit("v14.0 source-progress finalizer FAIL: v13.9 must remain latest qualified milestone")


def main() -> None:
    roadmap = ROADMAP.read_text(encoding="utf-8")
    readme = README.read_text(encoding="utf-8")
    roadmap2 = finalize_roadmap(roadmap)
    readme2 = finalize_readme(readme)
    verify(roadmap2, readme2)
    ROADMAP.write_text(roadmap2, encoding="utf-8")
    README.write_text(readme2, encoding="utf-8")
    changed = roadmap2 != roadmap or readme2 != readme
    print("v14.0 source-progress finalizer: " + ("UPDATED 469/471 (99.6%)" if changed else "NO-OP; already finalized"))


if __name__ == "__main__":
    main()
