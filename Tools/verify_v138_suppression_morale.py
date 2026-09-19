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
        "RetreatCadenceSeconds = 0.50f", "RetreatPhase(",
        "public bool Occupied;",
        "Projectile.DamageResolved += OnDamageResolved",
        "Projectile.ShotSpawned3D += OnShotSpawned",
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

    # Fixed roster + state-age cadence are deliberate determinism contracts. HashSet snapshot ordering,
    # Unity instance ids and absolute Time.time must never choose which actors receive suppression.
    require("RuntimeBattleRegistry.EnemySnapshot" not in core, "HashSet enemy snapshot reintroduced into suppression fanout")
    require("GetInstanceID()" not in core, "Unity instance-id nondeterminism reintroduced")
    require("Time.time" not in core, "absolute frame-time retreat phase reintroduced")

    shot_start = core.find("private void OnShotSpawned")
    shot_end = core.find("private void BroadcastAllyLoss", shot_start)
    require(shot_start >= 0 and shot_end > shot_start, "OnShotSpawned block missing")
    shot_block = core[shot_start:shot_end]
    require("for (int i = 0; i < _entries.Length; i++)" in shot_block, "near-miss fanout must iterate fixed entries")
    require("if (!e.Occupied || e.Enemy == null) continue;" in shot_block, "near-miss fanout must use occupied fixed slots")
    require("Register(" not in shot_block and "Find(" not in shot_block, "per-shot register/find churn reintroduced")

    sample_start = core.find("private void Sample(float now)")
    sample_end = core.find("private void OnDamageResolved", sample_start)
    require(sample_start >= 0 and sample_end > sample_start, "Sample block missing")
    sample_block = core[sample_start:sample_end]
    require("if (!e.Occupied) continue;" in sample_block, "sample occupancy guard missing")
    require("_count = Mathf.Max(0, _count - 1);" in sample_block, "destroyed-entry count repair missing")

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
        "deterministic retreat cadence", "retreat=PASS", "fixed-fanout=PASS", "occupancy=PASS",
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
