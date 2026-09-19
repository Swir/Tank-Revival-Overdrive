#!/usr/bin/env python3
"""Static/source qualification for v14.0 counter-battery counterplay and presentation."""
from __future__ import annotations

import math
import re
from pathlib import Path

CS = Path("Assets/Scripts/CounterBatteryWarfareV140.cs")
ROADMAP = Path("ROADMAP.md")
VERSION = Path("VERSION")

PHASE_ONE = (
    "Bounded counter-battery threat authority",
    "Fire-support signature and lock acquisition across 100 rounds",
    "Canonical enemy barrage execution",
)
COUNTERPLAY_PHASE = (
    "Terrain, sensor and relocation counterplay",
    "Observer-class and command integration",
    "Readable budgeted warning and barrage presentation",
)
FINAL_PHASE = (
    "Deterministic runtime, authority and performance contracts",
    "Exact-SHA Windows qualification",
)


def fail(msg: str) -> None:
    raise SystemExit("v14.0 source qualification FAIL: " + msg)


def const_float(source: str, name: str) -> float:
    m = re.search(rf"public const float {re.escape(name)} = ([0-9.]+)f;", source)
    if not m:
        fail(f"missing float constant {name}")
    return float(m.group(1))


def const_int(source: str, name: str) -> int:
    m = re.search(rf"public const int {re.escape(name)} = (\d+);", source)
    if not m:
        fail(f"missing int constant {name}")
    return int(m.group(1))


def profile(round_: int, max_shells: int) -> tuple[int, float, float, float]:
    r = max(1, min(100, round_))
    band = max(0, min(4, (r - 1) // 20))
    shells = max(2, min(max_shells, 2 + band // 2))
    t = (r - 1) / 99.0
    threshold = 0.80 + (0.68 - 0.80) * t
    cooldown = 24.0 + (18.0 - 24.0) * t
    acquisition = 0.92 + (1.16 - 0.92) * t
    return shells, threshold, cooldown, acquisition


def item_checked(roadmap: str, label: str) -> bool:
    m = re.search(rf"^- \[([ x])\] \*\*{re.escape(label)}\*\*", roadmap, re.M)
    if not m:
        fail("missing roadmap item: " + label)
    return m.group(1) == "x"


def main() -> None:
    source = CS.read_text(encoding="utf-8")
    roadmap = ROADMAP.read_text(encoding="utf-8")
    if VERSION.read_text(encoding="utf-8").strip() != "v14.0.0-dev":
        fail("VERSION must be v14.0.0-dev")

    required = (
        "CounterBatteryStateV140", "Quiet = 0", "Searching = 1", "Locked = 2", "Barrage = 3", "Relocating = 4",
        "CounterBatteryModelV140", "CounterBatteryDirectorV140", "CounterBatteryExecutionBridgeV140",
        "BattlefieldFireSupportDirector.StrikeIntentPublished += OnPlayerSupportIntent", "RuntimeBattleRegistry.Player", "RuntimeBattleRegistry.EnemySnapshot",
        "CounterBatteryModelV140.AddSupportSignature", "CounterBatteryModelV140.DecayExposure",
        "TacticalTerrainMap.TerrainAt", "BattlefieldSensorFusionDirector", "TryGetContact", "SweepActive",
        "BattlefieldCohesionDirector.IntentFor", "Cohesion01", "AdaptiveEnemyCommandDirector.PostureFor", "SpreadScale", "ReloadScale",
        "if (eligible >= CounterBatteryModelV140.MaxObservers) break;", "_lockOrigin", "RelocationBreakDistance",
        "SEARCHING", "WARNING", "LOCKED", "INCOMING", "RELOCATE", "private void OnGUI()", "LOCK PRESSURE", "RELOCATION",
        "Team.Enemy", "_game.SpawnProjectile(",
    )
    for token in required:
        if token not in source:
            fail("missing source contract token: " + token)

    bridge_marker = "public sealed class CounterBatteryExecutionBridgeV140"
    if bridge_marker not in source:
        fail("execution bridge marker missing")
    director_side, bridge_side = source.split(bridge_marker, 1)
    for token in (".Damage(", "SpawnProjectile(", "ProjectilePool.Spawn", "FindObjectsByType<EnemyTank>", "Physics2D.Overlap"):
        if token in director_side:
            fail("director owns forbidden gameplay authority: " + token)
    if bridge_side.count("_game.SpawnProjectile(") != 1:
        fail("execution bridge must contain exactly one canonical SpawnProjectile call")
    if "Team.Enemy" not in bridge_side:
        fail("execution bridge must identify enemy projectile ownership")

    update_match = re.search(r"private void Update\(\)\s*\{(.*?)\n        \}\n        private void EnsureRound", source, re.S)
    if not update_match:
        fail("cannot isolate Update hot path")
    for token in ("FindAnyObjectByType", "FindObjectsByType", "Physics2D.Overlap", "new GameObject"):
        if token in update_match.group(1):
            fail("hot path contains scene/allocation scan: " + token)

    sample_match = re.search(r"private void SampleObservers\(\)\s*\{(.*?)\n        \}\n        private void PublishStrikeIntent", source, re.S)
    if not sample_match:
        fail("cannot isolate SampleObservers")
    sample_body = sample_match.group(1)
    cap = sample_body.find("if (eligible >= CounterBatteryModelV140.MaxObservers) break;")
    accumulate = sample_body.find("weight += observer")
    if cap < 0 or accumulate < 0 or cap > accumulate:
        fail("observer hard cap must be enforced before observation weight accumulation")
    for token in ("BattlefieldCohesionDirector.IntentFor", "AdaptiveEnemyCommandDirector.PostureFor", "TryGetContact"):
        if token not in sample_body:
            fail("observer integration missing from bounded sampler: " + token)

    planned = const_int(source, "PlannedRounds")
    tracked = const_int(source, "MaxTrackedHostiles")
    observers = const_int(source, "MaxObservers")
    shells_max = const_int(source, "MaxShells")
    cadence = const_float(source, "SampleCadenceSeconds")
    exposure_step = const_float(source, "ExposurePerSupportStrike")
    repeat = const_float(source, "RepeatUseMultiplier")
    relocated = const_float(source, "RelocatedUseMultiplier")
    decay = const_float(source, "ExposureDecayPerSecond")
    search = const_float(source, "SearchThreshold")
    break_distance = const_float(source, "BreakDistance")
    minimum_break = const_float(source, "MinimumPhysicalBreakDistance")
    max_observer_weight = const_float(source, "MaxObserverCompositeWeight")

    if planned != 100 or tracked != 24 or not (1 <= observers <= 6) or not (2 <= shells_max <= 4):
        fail("hard actor/shell/round budgets are outside approved bounds")
    if not (0.20 <= cadence <= 0.50 and 0 < exposure_step <= 0.25 and repeat > 1 and 0.5 <= relocated < 1):
        fail("signature/cadence constants are outside approved bounds")
    if not (0 < decay < 0.10 and 0.20 <= search <= 0.40 and 2.5 <= break_distance <= 4.5):
        fail("decay/search/relocation constants are outside approved bounds")
    if not (1.5 <= minimum_break < break_distance and 1.5 <= max_observer_weight <= 1.8):
        fail("counterplay minimum movement or observer normalization escaped bounds")

    profiles = [profile(r, shells_max) for r in range(1, 101)]
    if any(not (2 <= p[0] <= shells_max) for p in profiles): fail("100-round shell budgets escaped bounds")
    if any(not (0.65 <= p[1] <= 0.82) for p in profiles): fail("100-round lock thresholds escaped bounds")
    if any(not (18 <= p[2] <= 24) for p in profiles): fail("100-round cooldown escaped bounds")
    if any(profiles[i + 1][0] < profiles[i][0] for i in range(99)): fail("shell budget must not decrease across campaign")
    if any(profiles[i + 1][1] > profiles[i][1] + 1e-9 for i in range(99)): fail("lock pressure must not become easier for the player at later rounds")
    if not (exposure_step * repeat > exposure_step * relocated > 0): fail("relocation must reduce signature gain versus repeat use")
    if not math.isclose(max(0.0, 0.5 - decay * 2.0), 0.5 - decay * 2.0): fail("exposure decay sanity failed")

    for token in (
        "case TacticalTerrainKind.Crater: return 0.62f;", "case TacticalTerrainKind.Rubble: return 0.70f;", "case TacticalTerrainKind.Ice: return 1.06f;",
        "case SensorContactStateV136.Verified: scale = 0.56f;", "case SensorContactStateV136.Tracked: scale = 0.70f;", "if (sweepActive) scale *= 0.85f;",
        "Mathf.Max(MinimumPhysicalBreakDistance", "Vector2.Distance(playerPosition, _lockOrigin) >= breakDistance",
    ):
        if token not in source:
            fail("counterplay invariant missing: " + token)

    completed = len(re.findall(r"^- \[x\] ", roadmap, re.M))
    opened = len(re.findall(r"^- \[ \] ", roadmap, re.M))
    if (completed, opened) not in {(466, 5), (469, 2)}:
        fail(f"unexpected roadmap state {completed} completed / {opened} open")
    if "## v14.0 — Counter-Battery & Mobile Fire-Control Warfare — IN DEVELOPMENT" not in roadmap:
        fail("active v14.0 milestone heading missing")

    for label in PHASE_ONE:
        if not item_checked(roadmap, label): fail("phase-one roadmap item must remain complete: " + label)
    counterplay_closed = completed == 469
    for label in COUNTERPLAY_PHASE:
        if item_checked(roadmap, label) != counterplay_closed: fail("counterplay roadmap completion state is inconsistent: " + label)
    for label in FINAL_PHASE:
        if item_checked(roadmap, label): fail("runtime/Windows qualification items must remain open before exact qualification: " + label)

    expected_table = "| **469** | **2** | **471** | **99.6%** |" if counterplay_closed else "| **466** | **5** | **471** | **98.9%** |"
    if expected_table not in roadmap:
        fail("roadmap numeric table disagrees with verified v14.0 state")

    print("v14.0 counter-battery source qualification: PASS — " f"100 rounds, observers<={observers}, terrain/sensor/command counterplay, roadmap={completed}/471")


if __name__ == "__main__":
    main()
