#!/usr/bin/env python3
"""Static authority/contracts gate for v13.3 Battlefield Cohesion & Squad Command."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CORE = ROOT / "Assets/Scripts/BattlefieldCohesionV133.cs"
ENEMY = ROOT / "Assets/Scripts/EnemyTank.cs"
SMOKE = ROOT / "Assets/Scripts/BattlefieldCohesionCISmokeProbe.cs"
ROADMAP = ROOT / "ROADMAP.md"
README = ROOT / "README.md"


def fail(message: str) -> None:
    raise SystemExit("v13.3 contract FAIL: " + message)


def require(text: str, token: str, label: str) -> None:
    if token not in text:
        fail(f"{label}: missing {token!r}")


def main() -> None:
    for path in (CORE, ENEMY, SMOKE, ROADMAP, README):
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

    # Registry ownership is fixed and explicit; no plural scene enumeration or gameplay authority is allowed.
    for forbidden in (
        "FindObjectsByType<",
        "FindObjectsOfType<",
        "GameObject.FindGameObjectsWithTag",
        "SpawnEnemy(",
        "SpawnProjectile(",
        ".TakeDamage(",
        ".Heal(",
        "PlayerPrefs.",
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

    # Canonical firing/movement authorities must remain in EnemyTank.
    require(enemy, "_body.MovePosition(", "EnemyTank movement authority")
    require(enemy, "_game.SpawnProjectile(", "EnemyTank projectile authority")
    require(enemy, "Health = gameObject.AddComponent<Health>();", "EnemyTank Health authority")

    for token in (
        '"-tr-v133-smoke"',
        "V13_3_BATTLEFIELD_COHESION_OK.txt",
        "V13_3_BATTLEFIELD_COHESION_FAIL.txt",
        "for (int round = 1; round <= 100; round++)",
        "MaxTrackedActors != 24",
        "MaxSquads != 6",
        "SquadSize != 4",
        "PromotedLeaderSlot(0b1010) != 1",
        "leader-loss shock does not soften squad pressure",
    ):
        require(smoke, token, "v13.3 smoke")

    if roadmap.count("<!-- SWIR-ROADMAP-STANDARD:v1 -->") != 1:
        fail("ROADMAP standard marker missing/duplicated")
    for token in (
        "<!-- ROADMAP-PROGRESS:START -->",
        "<!-- ROADMAP-PROGRESS:END -->",
        "## 📊 Overall progress",
        "███████████████████░ 98.1%",
        "| **407** | **8** | **415** | **98.1%** |",
        "STATUS-V13.3%20IN%20DEVELOPMENT-yellow",
        "DONE-407%2F415-1f6feb",
    ):
        require(roadmap, token, "ROADMAP v13.3 dashboard")

    section = roadmap.split("## v13.3 — Battlefield Cohesion & Squad Command Warfare — IN DEVELOPMENT", 1)
    if len(section) != 2:
        fail("v13.3 roadmap section missing")
    v133 = section[1]
    unchecked = len(re.findall(r"^- \[ \] ", v133, re.M))
    checked = len(re.findall(r"^- \[x\] ", v133, re.M))
    if unchecked != 8 or checked != 0:
        fail(f"v13.3 checklist must remain 0/8 before exact qualification, found checked={checked} unchecked={unchecked}")

    completed = len(re.findall(r"^- \[x\] ", roadmap, re.M))
    remaining = len(re.findall(r"^- \[ \] ", roadmap, re.M))
    if (completed, remaining, completed + remaining) != (407, 8, 415):
        fail(f"ROADMAP checklist count mismatch: {completed}/{completed + remaining}, remaining={remaining}")

    for token in (
        "<!-- SWIR-README-STANDARD:v2 -->",
        "<!-- SWIR-PROGRESS-SVG-PRO:v1 -->",
        "## 🔎 Search Keywords",
        "assets/readme/hero.svg",
        "assets/readme/progress-card.svg",
    ):
        require(readme, token, "README PRO v2")

    print("v13.3 battlefield cohesion source/authority/roadmap contracts: PASS")
    print("scope=407/415 (98.1%) status=V13.3 IN DEVELOPMENT")
    print("roster=24 actors / 6 squads / 4 roles; exact Windows qualification still required")


if __name__ == "__main__":
    main()
