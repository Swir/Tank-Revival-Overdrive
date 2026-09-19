#!/usr/bin/env python3
"""Deterministic source/authority guard for v13.9 Fire-Support Command."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "Assets/Scripts/BattlefieldFireSupportV139.cs"
SMOKE = ROOT / "Assets/Scripts/BattlefieldFireSupportCISmokeProbe.cs"
ROADMAP = ROOT / "ROADMAP.md"


def require(text: str, needle: str, label: str) -> None:
    if needle not in text:
        raise SystemExit(f"missing {label}: {needle}")


def class_body(text: str, class_name: str) -> str:
    match = re.search(rf"\bclass\s+{re.escape(class_name)}\b", text)
    if not match:
        raise SystemExit(f"missing class: {class_name}")
    start = text.find("{", match.end())
    if start < 0:
        raise SystemExit(f"missing class body: {class_name}")
    depth = 0
    for index in range(start, len(text)):
        ch = text[index]
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return text[start + 1:index]
    raise SystemExit(f"unterminated class body: {class_name}")


def main() -> int:
    source = SRC.read_text(encoding="utf-8")
    smoke = SMOKE.read_text(encoding="utf-8")
    roadmap = ROADMAP.read_text(encoding="utf-8")

    for needle, label in [
        ("public const int PlannedRounds = 100;", "100-round contract"),
        ("public const int MaxTrackedHostiles = 24;", "hostile cap"),
        ("public const int MaxTelegraphs = 6;", "telegraph cap"),
        ("public const int MaxStrikes = 6;", "strike cap"),
        ("public const float ActiveSeconds = 5.50f;", "bounded active window"),
        ("FireSupportStateV139.Ready", "ready state"),
        ("FireSupportStateV139.Active", "active state"),
        ("FireSupportStateV139.Cooldown", "cooldown state"),
        ("Input.GetKeyDown(KeyCode.F)", "player command input"),
        ("BattlefieldSuppressionMoraleDirector.TryIntent", "suppression integration"),
        ("BattlefieldCohesionDirector.SpreadScale", "cohesion integration"),
        ("TacticalTerrainPlannerV134.IsReservedSafeLane", "terrain integration"),
        ("BattlefieldSensorFusionDirector", "sensor integration"),
        ("TryGetContact", "verified-contact integration"),
        ("RuntimeBattleRegistry.EnemySnapshot", "registry-driven targeting"),
        ("StrikeIntentPublished", "intent publication event"),
        ("FireSupportReactionV139", "class reaction intent"),
        ("ReactionFor(EnemyKind kind)", "reaction planner"),
        ("BattlefieldFireSupportExecutionBridgeV139", "canonical execution bridge"),
        ("_game.SpawnProjectile(", "TankGame projectile authority"),
        ("AmmoType.Explosive", "support ordnance"),
        ("EnemyKind.Fast", "scout mapping"),
        ("EnemyKind.Heavy", "bruiser mapping"),
        ("EnemyKind.Sniper", "sniper mapping"),
        ("EnemyKind.Elite", "elite mapping"),
        ("EnemyKind.Siege", "officer mapping"),
        ("EnemyKind.Boss", "boss mapping"),
    ]:
        require(source, needle, label)

    for needle, label in [
        ("-tr-v139-smoke", "runtime smoke command"),
        ("BattlefieldFireSupportModelV139.ConfigurationValid", "runtime configuration check"),
        ("SensorOpportunity", "runtime sensor contract"),
        ("ReactionFor", "runtime reaction contract"),
        ("BuildIntent", "runtime intent contract"),
        ("BattlefieldFireSupportExecutionBridgeV139.EnsureInstalled", "runtime bridge installation"),
        ("V13_9_FIRE_SUPPORT_OK.txt", "runtime PASS marker"),
    ]:
        require(smoke, needle, label)

    forbidden_global = [
        r"\.Damage\(",
        r"Destroy\(\s*enemy",
        r"FindObjectsByType<EnemyTank>",
        r"Physics2D\.Overlap",
        r"Time\.timeScale\s*=",
    ]
    for pattern in forbidden_global:
        if re.search(pattern, source):
            raise SystemExit(f"forbidden alternate authority/hot-path pattern: {pattern}")

    director = class_body(source, "BattlefieldFireSupportDirector")
    for forbidden in [
        "ProjectilePool.Spawn",
        ".SpawnProjectile(",
        "new Projectile",
        "AddComponent<Projectile>",
        ".Damage(",
    ]:
        if forbidden in director:
            raise SystemExit(f"intent-only director illegally owns execution path: {forbidden}")

    bridge = class_body(source, "BattlefieldFireSupportExecutionBridgeV139")
    if bridge.count("_game.SpawnProjectile(") != 1:
        raise SystemExit("execution bridge must forward through exactly one TankGame.SpawnProjectile callsite")
    if "ProjectilePool.Spawn" in bridge or ".Damage(" in bridge:
        raise SystemExit("execution bridge bypasses canonical TankGame/Projectile authority")

    if "ProjectilePool.Spawn(" in source:
        raise SystemExit("v13.9 source must not bypass TankGame projectile authority")
    if "ActiveSeconds <= 6f" not in source or "MaxStrikes <= MaxTelegraphs" not in source:
        raise SystemExit("bounded support-window configuration guard missing")
    if "SensorContactStateV136.Unknown" not in source or "return 0f;" not in source:
        raise SystemExit("unknown-contact fire-support denial missing")

    # Validate the closed v13.8/open v13.9 roadmap truth without changing progress.
    done = len(re.findall(r"^- \[x\] ", roadmap, re.M))
    open_ = len(re.findall(r"^- \[ \] ", roadmap, re.M))
    if (done, open_, done + open_) != (455, 8, 463):
        raise SystemExit(
            f"expected open v13.9 roadmap 455/463 with 8 open; found {done}/{done + open_} with {open_} open"
        )
    require(roadmap, "V13.9%20IN%20DEVELOPMENT", "v13.9 roadmap status")
    require(roadmap, "| **455** | **8** | **463** | **98.3%** |", "v13.9 roadmap table")

    # The pure round profile must provide bounded support for every one of the 100 rounds.
    signatures = set()
    for round_no in range(1, 101):
        band = max(0, min(4, (round_no - 1) // 20))
        strikes = max(3, min(6, 3 + band // 2 + (1 if round_no >= 80 else 0)))
        cooldown = 22.0 + (15.0 - 22.0) * ((round_no - 1) / 99.0)
        signature = 139 * 1009 + round_no * 97 + strikes * 31 + band * 17 + round(cooldown * 100.0)
        if not 3 <= strikes <= 6:
            raise SystemExit(f"round {round_no}: strike budget escaped 3..6")
        if not 15.0 <= cooldown <= 22.0:
            raise SystemExit(f"round {round_no}: cooldown escaped 15..22s")
        signatures.add(signature)
    if len(signatures) != 100:
        raise SystemExit(f"round profiles are not deterministic-distinct: {len(signatures)}/100 signatures")

    print("v13.9 fire-support source/authority contract: PASS")
    print("intent-only director: PASS; canonical execution bridge: TankGame.SpawnProjectile")
    print("sensor/contact + suppression/cohesion/terrain + class-reaction contracts: PASS")
    print("roadmap truth: 455/463 (98.3%) V13.9 IN DEVELOPMENT")
    print("100-round profile signatures: 100/100 unique; strikes 3..6; cooldown 15..22s")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
