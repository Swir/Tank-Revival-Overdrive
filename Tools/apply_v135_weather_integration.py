#!/usr/bin/env python3
"""Apply the bounded v13.5 weather integration to canonical PlayerTank/EnemyTank paths."""
from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLAYER = ROOT / "Assets/Scripts/PlayerTank.cs"
ENEMY = ROOT / "Assets/Scripts/EnemyTank.cs"
VERSION = ROOT / "VERSION"


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count == 0:
        if new in text:
            return text
        raise RuntimeError(f"{label}: anchor missing")
    if count != 1:
        raise RuntimeError(f"{label}: expected one anchor, found {count}")
    return text.replace(old, new, 1)


def main() -> None:
    player = PLAYER.read_text(encoding="utf-8")
    player = replace_once(
        player,
        "            _body.MovePosition(_body.position + _move * (EffectiveMoveSpeed * moduleMobility * Time.fixedDeltaTime));",
        "            float weatherMobility = BattlefieldWeatherDirector.MobilityScale(Team.Player, transform.position);\n"
        "            _body.MovePosition(_body.position + _move * (EffectiveMoveSpeed * moduleMobility * weatherMobility * Time.fixedDeltaTime));",
        "PlayerTank mobility integration",
    )
    player = replace_once(
        player,
        "            float spread = FireControlBallisticsDirector.SpreadDegrees(ammo, _move.sqrMagnitude > 0.01f ? 1f : 0f, _armor);",
        "            float spread = FireControlBallisticsDirector.SpreadDegrees(ammo, _move.sqrMagnitude > 0.01f ? 1f : 0f, _armor)\n"
        "                * BattlefieldWeatherDirector.PlayerSpreadScale(ammo);",
        "PlayerTank spread integration",
    )
    PLAYER.write_text(player, encoding="utf-8")

    enemy = ENEMY.read_text(encoding="utf-8")
    enemy = replace_once(
        enemy,
        "                _nextShot = Time.time + Random.Range(_shotDelay * .82f, _shotDelay * 1.18f) * reload * AdaptiveEnemyCommandDirector.ReloadScale(Kind) * BattlefieldCohesionDirector.ReloadScale(this);",
        "                _nextShot = Time.time + Random.Range(_shotDelay * .82f, _shotDelay * 1.18f) * reload\n"
        "                    * AdaptiveEnemyCommandDirector.ReloadScale(Kind) * BattlefieldCohesionDirector.ReloadScale(this)\n"
        "                    * BattlefieldWeatherDirector.EnemyReloadScale(Kind);",
        "EnemyTank reload integration",
    )
    enemy = replace_once(
        enemy,
        "            _body.MovePosition(_body.position + _facing * (_speed * m * casualtyScale * AdaptiveEnemyCommandDirector.MovementScale(Kind) * BattlefieldCohesionDirector.MovementScale(this) * Time.fixedDeltaTime));",
        "            float weatherMobility = BattlefieldWeatherDirector.MobilityScale(Team.Enemy, transform.position);\n"
        "            _body.MovePosition(_body.position + _facing * (_speed * m * casualtyScale\n"
        "                * AdaptiveEnemyCommandDirector.MovementScale(Kind) * BattlefieldCohesionDirector.MovementScale(this)\n"
        "                * weatherMobility * Time.fixedDeltaTime));",
        "EnemyTank mobility integration",
    )
    enemy = replace_once(
        enemy,
        "            float spread = FireControlBallisticsDirector.EnemySpreadDegrees(Kind, movement, _armor, coordinated) * AdvancedGunneryDoctrineDirector.SpreadMultiplier(Kind, _round) * CounterFireThreatMemory.AccuracyMultiplier(this) * AdaptiveEnemyCommandDirector.SpreadScale(Kind) * BattlefieldCohesionDirector.SpreadScale(this);",
        "            float spread = FireControlBallisticsDirector.EnemySpreadDegrees(Kind, movement, _armor, coordinated)\n"
        "                * AdvancedGunneryDoctrineDirector.SpreadMultiplier(Kind, _round)\n"
        "                * CounterFireThreatMemory.AccuracyMultiplier(this)\n"
        "                * AdaptiveEnemyCommandDirector.SpreadScale(Kind)\n"
        "                * BattlefieldCohesionDirector.SpreadScale(this)\n"
        "                * BattlefieldWeatherDirector.EnemySpreadScale(Kind);",
        "EnemyTank spread integration",
    )
    ENEMY.write_text(enemy, encoding="utf-8")

    VERSION.write_text("v13.5.0-dev\n", encoding="utf-8")
    print("v13.5 weather integration applied to PlayerTank/EnemyTank; VERSION=v13.5.0-dev")


if __name__ == "__main__":
    main()
