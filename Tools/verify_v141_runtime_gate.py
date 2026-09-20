#!/usr/bin/env python3
"""Deterministic runtime/authority/performance contract gate for v14.1 counter-observation warfare."""
from __future__ import annotations
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / "Assets/Scripts/CounterObservationWarfareV141.cs"
SMOKE = ROOT / "Assets/Scripts/CounterObservationCISmokeProbeV141.cs"
COUNTER_BATTERY = ROOT / "Assets/Scripts/CounterBatteryWarfareV140.cs"
FIRE_SUPPORT = ROOT / "Assets/Scripts/BattlefieldFireSupportV139.cs"
GAME = ROOT / "Assets/Scripts/TankGame.cs"
ROADMAP = ROOT / "ROADMAP.md"


def require(value: bool, message: str) -> None:
    if not value:
        raise SystemExit("v14.1 runtime gate FAIL: " + message)


def method_body(source: str, signature: str) -> str:
    start = source.find(signature)
    require(start >= 0, "missing method: " + signature)
    brace = source.find("{", start)
    require(brace >= 0, "missing method body: " + signature)
    depth = 0
    for i in range(brace, len(source)):
        c = source[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return source[brace + 1:i]
    raise SystemExit("v14.1 runtime gate FAIL: unterminated method: " + signature)


def main() -> None:
    runtime = RUNTIME.read_text(encoding="utf-8")
    smoke = SMOKE.read_text(encoding="utf-8")
    counter_battery = COUNTER_BATTERY.read_text(encoding="utf-8")
    fire_support = FIRE_SUPPORT.read_text(encoding="utf-8")
    game = GAME.read_text(encoding="utf-8")
    roadmap = ROADMAP.read_text(encoding="utf-8")

    for token in (
        "MaxTrackedHostiles = 24",
        "MaxObserverCandidates = 6",
        "EvaluationCadenceSeconds = 0.25f",
        "MaxDesignationRange",
        "MinimumEffectiveNetworkBreakSeconds",
        "RuntimeBattleRegistry.EnemySnapshot",
        "RuntimeBattleRegistry.Revision",
        "sensor.TryGetContact(",
    ):
        require(token in runtime, "missing bounded-runtime token: " + token)

    update = method_body(runtime, "private void Update()")
    for forbidden in (
        "FindAnyObjectByType",
        "FindObjectsByType",
        "FindObjectOfType",
        "FindObjectsOfType",
        "GameObject.Find",
        "Resources.FindObjectsOfTypeAll",
        "Physics2D.Overlap",
    ):
        require(forbidden not in update, "hot-path scene/physics scan in Update: " + forbidden)
    require("_nextEvaluation" in update, "Update must retain bounded evaluation cadence")

    require(runtime.count("FindAnyObjectByType<TankGame>()") <= 2,
            "TankGame scene lookup must be bounded to bootstrap/scene load")
    require("private void OnSceneLoaded(Scene _, LoadSceneMode __)" in runtime, "scene-load refresh missing")
    require("SceneManager.sceneLoaded += OnSceneLoaded;" in runtime, "scene-load subscription missing")
    require("SceneManager.sceneLoaded -= OnSceneLoaded;" in runtime, "scene-load unsubscription missing")

    for token in (
        "private void ResetRuntime()",
        "ClearDesignationSubscription();",
        "_targetSnapshot = default;",
        "_candidate = null;",
        "_designated = null;",
        "_round = -1;",
        "_designatedHealth.Died += OnDesignatedObserverDied",
        "_designatedHealth.Died -= OnDesignatedObserverDied",
        "IsDesignatedStillRegistered()",
    ):
        require(token in runtime, "lifecycle cleanup contract missing: " + token)

    for pattern in (
        r"ProjectilePool\.Spawn",
        r"\.Damage\s*\(",
        r"SpawnProjectile\s*\(",
        r"Rigidbody2D\.MovePosition",
        r"Destroy\s*\(\s*_?designated",
    ):
        require(re.search(pattern, runtime) is None, "v14.1 director owns forbidden gameplay authority: " + pattern)
    require("CounterBatteryDirectorV140.Instance" in runtime, "counter-battery integration missing")
    require("CounterObservationDirectorV141.CounterBatteryNetworkScale" in counter_battery,
            "bounded acquisition bridge missing")
    require("BattlefieldFireSupportDirector.StrikeIntentPublished += ExecuteIntent;" in fire_support,
            "fire-support execution bridge subscription missing")
    require("_game.SpawnProjectile(" in fire_support,
            "fire-support bridge must forward immutable intent through TankGame")
    require("public void SpawnProjectile(" in game,
            "canonical TankGame projectile authority missing")

    for token in (
        'HasArgument("-tr-v141-smoke")',
        "for (int round = 1; round <= 100; round++)",
        'Require(CounterObservationModelV141.MaxTrackedHostiles == 24',
        'Require(CounterObservationModelV141.MaxObserverCandidates == 6',
        "finite acquisition suppression",
        "observer network is never immune",
        "bounded target handoff snapshot",
        "CounterObservationDirectorV141.EnsureInstalled()",
        "CounterBatteryDirectorV140.EnsureInstalled()",
        "V14_1_COUNTER_OBSERVATION_OK.txt",
        "Application.Quit(41)",
    ):
        require(token in smoke, "packaged smoke contract missing: " + token)
    for pattern in (r"ProjectilePool\.Spawn", r"\.Damage\s*\(", r"Physics2D\.Overlap"):
        require(re.search(pattern, smoke) is None, "smoke probe owns gameplay authority: " + pattern)

    completed = len(re.findall(r"^- \[x\] ", roadmap, re.M))
    opened = len(re.findall(r"^- \[ \] ", roadmap, re.M))
    require((completed, opened) in ((477, 2), (478, 1), (479, 0)),
            f"unexpected closeout state {completed}/{completed + opened}")

    print(
        "v14.1 runtime gate: PASS — bounded hot path, lifecycle cleanup, canonical authority, "
        f"100-round packaged-smoke contract; roadmap={completed}/{completed + opened}"
    )


if __name__ == "__main__":
    main()
