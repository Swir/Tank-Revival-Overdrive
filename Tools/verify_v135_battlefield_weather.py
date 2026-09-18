#!/usr/bin/env python3
"""Verify v13.5 Battlefield Weather & Visibility Warfare source/authority contracts."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WEATHER = ROOT / "Assets/Scripts/BattlefieldWeatherV135.cs"
SMOKE = ROOT / "Assets/Scripts/BattlefieldWeatherCISmokeProbe.cs"
PLAYER = ROOT / "Assets/Scripts/PlayerTank.cs"
ENEMY = ROOT / "Assets/Scripts/EnemyTank.cs"
ROADMAP = ROOT / "ROADMAP.md"
README = ROOT / "README.md"
VERSION = ROOT / "VERSION"


def fail(message: str) -> None:
    raise SystemExit("v13.5 weather verifier FAIL: " + message)


def need(text: str, token: str, label: str) -> None:
    if token not in text:
        fail(f"{label} missing token: {token}")


def main() -> None:
    for path in (WEATHER, SMOKE, PLAYER, ENEMY, ROADMAP, README, VERSION):
        if not path.is_file():
            fail(f"missing required file: {path.relative_to(ROOT)}")

    weather = WEATHER.read_text(encoding="utf-8")
    smoke = SMOKE.read_text(encoding="utf-8")
    player = PLAYER.read_text(encoding="utf-8")
    enemy = ENEMY.read_text(encoding="utf-8")
    roadmap = ROADMAP.read_text(encoding="utf-8")
    readme = README.read_text(encoding="utf-8")
    version = VERSION.read_text(encoding="utf-8").strip()

    for token in (
        "public enum BattlefieldWeatherKindV135",
        "public readonly struct BattlefieldWeatherPlanV135",
        "public static class BattlefieldWeatherPlannerV135",
        "public const int PlannedRounds = 100;",
        "public const int ProfileCount = 5;",
        "public const int MaxPresentationStreaks = 24;",
        "public const float MinTractionScale = 0.76f;",
        "public const float MaxPlayerSpreadScale = 1.12f;",
        "public const float MaxEnemySpreadScale = 1.30f;",
        "public sealed class BattlefieldWeatherDirector",
        "public static float MobilityScale(Team team, Vector2 position)",
        "public static float PlayerSpreadScale(AmmoType ammo)",
        "public static float EnemySpreadScale(EnemyKind kind)",
        "public static float EnemyReloadScale(EnemyKind kind)",
        "TacticalTerrainMap.TerrainAt(position)",
        "AmmoType.ArmorPiercing || ammo == AmmoType.Plasma",
        "DrawWeatherLayer()",
    ):
        need(weather, token, "weather runtime")

    for token in (
        "Health.Damage(",
        "SpawnProjectile(",
        "AddComponent<Health>",
        "Destroy(enemy",
        "FindObjectsByType<EnemyTank>",
        "Physics2D.OverlapCircleAll",
    ):
        if token in weather:
            fail(f"weather runtime must not create parallel combat authority: {token}")

    for token in (
        "BattlefieldWeatherDirector.MobilityScale(Team.Player, transform.position)",
        "BattlefieldWeatherDirector.PlayerSpreadScale(ammo)",
    ):
        need(player, token, "PlayerTank integration")

    for token in (
        "BattlefieldWeatherDirector.MobilityScale(Team.Enemy, transform.position)",
        "BattlefieldWeatherDirector.EnemySpreadScale(Kind)",
        "BattlefieldWeatherDirector.EnemyReloadScale(Kind)",
    ):
        need(enemy, token, "EnemyTank integration")

    for token in (
        'PassMarker = "V13_5_BATTLEFIELD_WEATHER_OK.txt"',
        'FailMarker = "V13_5_BATTLEFIELD_WEATHER_FAIL.txt"',
        '"-tr-v135-smoke"',
        "for (int round = 1; round <= 100; round++)",
        "adjacent weather profile repeated",
        "precision-ammo weather counterplay regressed",
        "runtime weather multipliers escaped hard bounds",
    ):
        need(smoke, token, "v13.5 packaged smoke")

    if version != "v13.5.0-dev":
        fail(f"VERSION mismatch: {version!r}")

    need(roadmap, "## v13.5 — Battlefield Weather & Visibility Warfare — IN DEVELOPMENT", "roadmap")
    v135 = roadmap.split("## v13.5 — Battlefield Weather & Visibility Warfare — IN DEVELOPMENT", 1)[1]
    if len(re.findall(r"^- \[ \] ", v135, re.M)) != 8:
        fail("v13.5 roadmap must contain exactly 8 open deliverables before qualification")
    completed = len(re.findall(r"^- \[x\] ", roadmap, re.M))
    remaining = len(re.findall(r"^- \[ \] ", roadmap, re.M))
    if (completed, remaining, completed + remaining) != (423, 8, 431):
        fail(f"roadmap counts mismatch: completed={completed} remaining={remaining} total={completed + remaining}")
    need(roadmap, "V13.5%20IN%20DEVELOPMENT", "roadmap status badge")
    need(roadmap, "| **423** | **8** | **431** | **98.1%** |", "roadmap numeric table")
    need(readme, "<!-- SWIR-README-STANDARD:v2 -->", "README standard")
    need(readme, "assets/readme/progress-card.svg", "README progress card")
    need(readme, "## 🔎 Search Keywords", "README Search Keywords")

    print("v13.5 Battlefield Weather source contract: PASS — planner/runtime/player/enemy/smoke/roadmap authority verified")


if __name__ == "__main__":
    main()
