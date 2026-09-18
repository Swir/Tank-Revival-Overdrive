#!/usr/bin/env python3
"""Static authority/contracts gate for v13.3 Battlefield Cohesion & Squad Command.

Gameplay/authority invariants remain strict. ROADMAP validation is semantic so
qualification and SVG-only presentation cannot be broken by stale hard-coded
progress-meter strings.
"""
from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]
CORE = ROOT / "Assets/Scripts/BattlefieldCohesionV133.cs"
ENEMY = ROOT / "Assets/Scripts/EnemyTank.cs"
SMOKE = ROOT / "Assets/Scripts/BattlefieldCohesionCISmokeProbe.cs"
ROADMAP = ROOT / "ROADMAP.md"
README = ROOT / "README.md"
GENERATOR = ROOT / "Tools/generate_progress_svgs.py"
LEGACY_METER = re.compile(r"(?m)^[ \t]*[█▓▒░▉▊▋▌▍▎▏■□]{8,}(?:[ \t]+[0-9]+(?:\.[0-9]+)?%)?[ \t]*$")


def fail(message: str) -> None:
    raise SystemExit("v13.3 contract FAIL: " + message)


def require(text: str, token: str, label: str) -> None:
    if token not in text:
        fail(f"{label}: missing {token!r}")


def verify_roadmap(roadmap: str) -> tuple[int, int, int, float, str]:
    if len(re.findall(r"^<!-- SWIR-ROADMAP-STANDARD:v1 -->$", roadmap, re.M)) != 1:
        fail("ROADMAP standalone standard marker count must be exactly 1")
    for token in (
        "<!-- ROADMAP-PROGRESS:START -->",
        "<!-- ROADMAP-PROGRESS:END -->",
        "## 📊 Overall progress",
        "| ✅ Completed | ⏳ Remaining | 📦 Total | 🎯 Progress |",
        "assets/readme/progress-mini.svg",
    ):
        require(roadmap, token, "ROADMAP v13.3 dashboard")
    if roadmap.count("assets/readme/progress-mini.svg") != 1:
        fail("ROADMAP must embed exactly one progress-mini.svg")
    if "assets/readme/progress-card.svg" in roadmap or "progress-template.svg" in roadmap:
        fail("ROADMAP must not embed card/template SVG")
    if LEGACY_METER.search(roadmap) or "20-segment bar" in roadmap:
        fail("ROADMAP contains a forbidden legacy text progress meter")

    completed = len(re.findall(r"^- \[x\] ", roadmap, re.M))
    remaining = len(re.findall(r"^- \[ \] ", roadmap, re.M))
    total = completed + remaining
    if total <= 0:
        fail("ROADMAP checklist scope is empty")
    percent = round(completed * 100.0 / total, 1)

    table = re.search(
        r"\| \*\*(\d+)\*\* \| \*\*(\d+)\*\* \| \*\*(\d+)\*\* \| \*\*([0-9.]+)%\*\* \|",
        roadmap,
    )
    if not table:
        fail("ROADMAP progress table row missing")
    actual_table = (int(table.group(1)), int(table.group(2)), int(table.group(3)), float(table.group(4)))
    expected_table = (completed, remaining, total, percent)
    if actual_table != expected_table:
        fail(f"ROADMAP table mismatch: table={actual_table} checklist={expected_table}")

    done_badge = re.search(r"DONE-(\d+)%2F(\d+)-", roadmap)
    road_badge = re.search(r"ROADMAP-([0-9.]+)%25-", roadmap)
    status_badge = re.search(r"STATUS-([^\"?]+?)-(?:yellow|brightgreen|red|orange|blue|1f6feb)\?", roadmap)
    if not (done_badge and road_badge and status_badge):
        fail("ROADMAP protected badges are incomplete")
    if (int(done_badge.group(1)), int(done_badge.group(2))) != (completed, total):
        fail("DONE badge disagrees with checklist")
    if float(road_badge.group(1)) != percent:
        fail("ROADMAP badge percentage disagrees with checklist")
    status = unquote(status_badge.group(1)).strip()

    section_match = re.search(
        r"## v13\.3 — Battlefield Cohesion & Squad Command Warfare — (IN DEVELOPMENT|QUALIFIED)\n(?P<body>.*?)(?=\n## |\Z)",
        roadmap,
        re.S,
    )
    if not section_match:
        fail("v13.3 roadmap section missing")
    body = section_match.group("body")
    v_checked = len(re.findall(r"^- \[x\] ", body, re.M))
    v_open = len(re.findall(r"^- \[ \] ", body, re.M))
    if v_checked + v_open != 8:
        fail(f"v13.3 section must contain exactly eight deliverables, found {v_checked + v_open}")

    if remaining == 0:
        if (completed, total, percent) != (415, 415, 100.0):
            fail(f"qualified v13.3 total must be 415/415, found {completed}/{total} ({percent:.1f}%)")
        if status != "V13.3 QUALIFIED" or section_match.group(1) != "QUALIFIED" or (v_checked, v_open) != (8, 0):
            fail("complete v13.3 scope is not consistently QUALIFIED")
    else:
        if (completed, remaining, total, percent) != (407, 8, 415, 98.1):
            fail(f"open v13.3 scope must be 407/415 with 8 remaining, found {completed}/{total}")
        if status != "V13.3 IN DEVELOPMENT" or section_match.group(1) != "IN DEVELOPMENT" or (v_checked, v_open) != (0, 8):
            fail("open v13.3 scope is not consistently IN DEVELOPMENT")
    return completed, remaining, total, percent, status


def main() -> None:
    for path in (CORE, ENEMY, SMOKE, ROADMAP, README, GENERATOR):
        if not path.is_file():
            fail(f"missing required file {path.relative_to(ROOT)}")

    core = CORE.read_text(encoding="utf-8")
    enemy = ENEMY.read_text(encoding="utf-8")
    smoke = SMOKE.read_text(encoding="utf-8")
    roadmap = ROADMAP.read_text(encoding="utf-8")
    readme = README.read_text(encoding="utf-8")

    for token in (
        "public const int SquadSize = 4;",
        "public const int MaxSquads = 6;",
        "public const int MaxTrackedActors = SquadSize * MaxSquads;",
        "public enum SquadRoleV133",
        "Leader = 0",
        "Wingman = 1",
        "Breacher = 2",
        "Support = 3",
        "public enum SquadCohesionStateV133",
        "Forming = 0",
        "Cohesive = 1",
        "Shocked = 2",
        "Regrouping = 3",
        "private readonly EnemyTank[] _actors = new EnemyTank[BattlefieldCohesionModelV133.MaxTrackedActors];",
        "public void Unregister(EnemyTank actor)",
        "BattlefieldCohesionModelV133.LeaderShockSeconds",
        "BattlefieldCohesionModelV133.RegroupSeconds",
        "V13_3_SQUAD_LEADER_MARKER",
    ):
        require(core, token, "cohesion core")

    for forbidden in (
        "FindObjectsByType<", "FindObjectsOfType<", "GameObject.FindGameObjectsWithTag",
        "SpawnEnemy(", "SpawnProjectile(", ".TakeDamage(", ".Heal(", "PlayerPrefs.",
    ):
        if forbidden in core:
            fail(f"cohesion core owns forbidden gameplay/scan path {forbidden!r}")

    for token in (
        "BattlefieldCohesionDirector.EnsureInstalled().Register(this, Kind, _round);",
        "BattlefieldCohesionDirector.ReloadScale(this)",
        "BattlefieldCohesionDirector.PreferPlayer(this)",
        "BattlefieldCohesionDirector.MovementScale(this)",
        "BattlefieldCohesionDirector.AdjustDirection(this, transform.position, desired);",
        "BattlefieldCohesionDirector.SpreadScale(this)",
        "BattlefieldCohesionDirector.Instance?.Unregister(this);",
    ):
        require(enemy, token, "EnemyTank integration")
    require(enemy, "_body.MovePosition(", "EnemyTank movement authority")
    require(enemy, "_game.SpawnProjectile(", "EnemyTank projectile authority")
    require(enemy, "Health = gameObject.AddComponent<Health>();", "EnemyTank Health authority")

    for token in (
        '"-tr-v133-smoke"', "V13_3_BATTLEFIELD_COHESION_OK.txt", "V13_3_BATTLEFIELD_COHESION_FAIL.txt",
        "for (int round = 1; round <= 100; round++)", "MaxTrackedActors != 24", "MaxSquads != 6",
        "SquadSize != 4", "PromotedLeaderSlot(0b1010) != 1", "leader-loss shock does not soften squad pressure",
    ):
        require(smoke, token, "v13.3 smoke")

    completed, remaining, total, percent, status = verify_roadmap(roadmap)

    for token in (
        "<!-- SWIR-README-STANDARD:v2 -->", "<!-- SWIR-PROGRESS-SVG-PRO:v1 -->",
        "## 🔎 Search Keywords", "assets/readme/hero.svg", "assets/readme/progress-card.svg",
    ):
        require(readme, token, "README PRO v2")
    if readme.count("assets/readme/progress-card.svg") != 1 or "assets/readme/progress-mini.svg" in readme:
        fail("README must embed exactly one project progress card and no mini SVG")
    if LEGACY_METER.search(readme):
        fail("README contains a forbidden legacy text progress meter")
    require(readme, status, "README roadmap status")
    require(readme, f"{completed} / {total}", "README roadmap counter")

    try:
        subprocess.run([sys.executable, str(GENERATOR), "--check"], cwd=ROOT, check=True)
    except subprocess.CalledProcessError as exc:
        fail(f"SWIR Progress SVG Pro deterministic check failed with exit code {exc.returncode}")

    print("v13.3 battlefield cohesion source/authority/roadmap contracts: PASS")
    print(f"scope={completed}/{total} ({percent:.1f}%) remaining={remaining} status={status}")
    print("roster=24 actors / 6 squads / 4 roles; movement/fire/health authorities preserved")


if __name__ == "__main__":
    main()
