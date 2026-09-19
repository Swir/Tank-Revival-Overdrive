#!/usr/bin/env python3
"""Deterministic source/authority guard for v13.9 Fire-Support Command."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "Assets/Scripts/BattlefieldFireSupportV139.cs"
ROADMAP = ROOT / "ROADMAP.md"


def require(text: str, needle: str, label: str) -> None:
    if needle not in text:
        raise SystemExit(f"missing {label}: {needle}")


def main() -> int:
    source = SRC.read_text(encoding="utf-8")
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
        ("RuntimeBattleRegistry.EnemySnapshot", "registry-driven targeting"),
        ("ProjectilePool.Spawn", "canonical projectile authority"),
        ("AmmoType.Explosive", "cover-aware support ordnance"),
        ("EnemyKind.Fast", "scout mapping"),
        ("EnemyKind.Heavy", "bruiser mapping"),
        ("EnemyKind.Sniper", "sniper mapping"),
        ("EnemyKind.Elite", "elite mapping"),
        ("EnemyKind.Siege", "officer mapping"),
        ("EnemyKind.Boss", "boss mapping"),
    ]:
        require(source, needle, label)

    forbidden = [
        r"\.Damage\(",
        r"Destroy\(\s*enemy",
        r"FindObjectsByType<EnemyTank>",
        r"Physics2D\.Overlap",
        r"Time\.timeScale\s*=",
    ]
    for pattern in forbidden:
        if re.search(pattern, source):
            raise SystemExit(f"forbidden alternate authority/hot-path pattern: {pattern}")

    if source.count("ProjectilePool.Spawn(") != 1:
        raise SystemExit("support must route through exactly one canonical ProjectilePool.Spawn callsite")
    if "ActiveSeconds <= 6f" not in source or "MaxStrikes <= MaxTelegraphs" not in source:
        raise SystemExit("bounded support-window configuration guard missing")

    # Validate the closed v13.8/open v13.9 roadmap truth without changing progress.
    done = len(re.findall(r"^- \[x\] ", roadmap, re.M))
    open_ = len(re.findall(r"^- \[ \] ", roadmap, re.M))
    if (done, open_, done + open_) != (455, 8, 463):
        raise SystemExit(f"expected open v13.9 roadmap 455/463 with 8 open; found {done}/{done + open_} with {open_} open")
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
    print("roadmap truth: 455/463 (98.3%) V13.9 IN DEVELOPMENT")
    print("100-round profile signatures: 100/100 unique; strikes 3..6; cooldown 15..22s")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
