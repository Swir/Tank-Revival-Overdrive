#!/usr/bin/env python3
"""Static contract guard for Battlefield Suppression & Morale Warfare v13.8."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
CORE = ROOT / "Assets/Scripts/BattlefieldSuppressionMoraleV138.cs"
SMOKE = ROOT / "Assets/Scripts/BattlefieldSuppressionMoraleCISmokeProbe.cs"
ENEMY = ROOT / "Assets/Scripts/EnemyTank.cs"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit("v13.8 suppression verifier FAIL: " + message)


def main() -> None:
    core = CORE.read_text(encoding="utf-8")
    smoke = SMOKE.read_text(encoding="utf-8")
    enemy = ENEMY.read_text(encoding="utf-8")

    for token in (
        "BattlefieldMoraleStateV138",
        "Steady", "Pressed", "Suppressed", "Broken", "Recovering",
        "MaxTrackedEnemies = 24", "MaxHudMarkers = 8",
        "PressedEnter = 20f", "SuppressedEnter = 45f", "BrokenEnter = 72f",
        "MaxBrokenSeconds = 4.5f", "RecoveryFloorSeconds = 1.25f",
        "Projectile.DamageResolved += OnDamageResolved",
        "Projectile.ShotSpawned3D += OnShotSpawned",
        "RuntimeBattleRegistry.EnemySnapshot",
        "LateRoundPerformanceDirector.CurrentProfile.Band",
        "BattlefieldCohesionDirector.SpreadScale",
        "AmmoType.ArmorPiercing", "AmmoType.Plasma",
    ):
        require(token in core, f"missing core contract: {token}")

    # Director may observe Projectile/Health events, but must not create competing combat authority.
    forbidden = (
        "AddComponent<Rigidbody2D>", "SpawnProjectile(", ".Damage(", "Health.Initialize(",
        "Destroy(e.Enemy", "Instantiate(", "CombatRoster.Spawn", "OnEnemyDestroyed(",
    )
    for token in forbidden:
        require(token not in core, f"director owns forbidden combat authority: {token}")

    for token in (
        "BattlefieldSuppressionMoraleDirector.EnsureInstalled().Register(this, Kind)",
        "BattlefieldSuppressionMoraleDirector.ReloadScale(this)",
        "BattlefieldSuppressionMoraleDirector.MovementScale(this)",
        "BattlefieldSuppressionMoraleDirector.SpreadScale(this)",
        "BattlefieldSuppressionMoraleDirector.AdjustDirection(this",
        "BattlefieldSuppressionMoraleDirector.Instance?.Unregister(this)",
    ):
        require(token in enemy, f"EnemyTank bounded integration missing: {token}")

    require("_body.MovePosition" in enemy, "EnemyTank must remain movement authority")
    require("_game.SpawnProjectile" in enemy, "EnemyTank/TankGame must remain fire authority")
    require("FireControlBallisticsDirector.ApplySpread" in enemy, "canonical ballistics must remain active")

    for token in (
        "-tr-v138-smoke", "V13_8_SUPPRESSION_MORALE_OK.txt",
        "anti-lock", "AP/Plasma counterplay ordering", "late-round density anti-cheap-pressure budget",
        "bounded movement", "bounded reload", "bounded spread",
    ):
        require(token in smoke, f"smoke contract missing: {token}")

    # Prevent obvious per-frame scene scans in the v13.8 pressure sample/update path.
    update_block = re.search(r"private void Update\(\).*?\n        }", core, re.S)
    require(update_block is not None, "Update block missing")
    require("FindObjectsByType" not in update_block.group(0), "per-frame scene scan in Update")
    require("new List<" not in core and "new Dictionary<" not in core, "dynamic collection allocation in v13.8 director")

    print("v13.8 suppression/morale source contract: PASS")


if __name__ == "__main__":
    main()
