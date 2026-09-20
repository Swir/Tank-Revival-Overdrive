#!/usr/bin/env python3
"""Deterministic runtime/authority/performance contract gate for v14.2 deception and EMCON warfare."""
from __future__ import annotations
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / "Assets/Scripts/CounterReconDeceptionV142.cs"
SMOKE = ROOT / "Assets/Scripts/CounterReconDeceptionCISmokeProbeV142.cs"
PRESENTATION = ROOT / "Assets/Scripts/CounterReconPresentationV142.cs"
COUNTER_BATTERY = ROOT / "Assets/Scripts/CounterBatteryWarfareV140.cs"
OBSERVATION = ROOT / "Assets/Scripts/CounterObservationWarfareV141.cs"
ROADMAP = ROOT / "ROADMAP.md"


def require(value: bool, message: str) -> None:
    if not value:
        raise SystemExit("v14.2 runtime gate FAIL: " + message)


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
    raise SystemExit("v14.2 runtime gate FAIL: unterminated method: " + signature)


def main() -> None:
    runtime = RUNTIME.read_text(encoding="utf-8")
    smoke = SMOKE.read_text(encoding="utf-8")
    presentation = PRESENTATION.read_text(encoding="utf-8")
    counter_battery = COUNTER_BATTERY.read_text(encoding="utf-8")
    observation = OBSERVATION.read_text(encoding="utf-8")
    roadmap = ROADMAP.read_text(encoding="utf-8")

    for token in (
        "PlannedRounds = 100",
        "MaxDecoyChargesPerRound = 2",
        "DecoyLifetimeSeconds = 6.0f",
        "DecoyCooldownSeconds = 11.0f",
        "EmconLifetimeSeconds = 4.5f",
        "EmconCooldownSeconds = 13.0f",
        "MinReacquisitionDelaySeconds = 2.8f",
        "MaxReacquisitionDelaySeconds = 4.6f",
        "MinDecoyCredibility01 = 0.42f",
        "MaxSuspicion01 = 0.72f",
        "MinReacquisitionAcquisitionScale = 0.22f",
        "MinContextEmconExposureScale = 0.40f",
        "ReacquisitionDelayForContext",
        "SuspicionAfterDecoy",
        "DecoyCredibility01(float suspicion01, float observerResilience01)",
        "ReacquisitionAcquisitionForResilience",
        "_emconRelocationCredited",
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
        "Instantiate(",
    ):
        require(forbidden not in update, "hot-path scene/spawn scan in Update: " + forbidden)
    for token in (
        "EnsureRound(_game.CurrentRound);",
        "RefreshObserverResilience();",
        "ObserveNetworkBreak();",
        "RuntimeBattleRegistry.Player",
        "Input.GetKeyDown(KeyCode.G)",
        "Input.GetKeyDown(KeyCode.V)",
        "!_emconRelocationCredited",
        "CounterBatteryModelV140.MinimumPhysicalBreakDistance",
    ):
        require(token in update, "Update contract missing: " + token)

    require(runtime.count("FindAnyObjectByType<TankGame>()") <= 2,
            "TankGame lookup must stay bounded to bootstrap/scene load")
    require("private void OnSceneLoaded(Scene _, LoadSceneMode __)" in runtime, "scene-load refresh missing")
    require("SceneManager.sceneLoaded += OnSceneLoaded;" in runtime, "scene-load subscription missing")
    require("SceneManager.sceneLoaded -= OnSceneLoaded;" in runtime, "scene-load unsubscription missing")

    for token in (
        "private void ResetRuntime()",
        "_decoyCharges = 0;",
        "_reacquireUntil = 0f;",
        "_suspicion01 = 0f;",
        "_emconRelocationCredited = false;",
        "_lastObservationState = CounterObservationStateV141.Idle;",
        "_decoyCharges = CounterReconDeceptionModelV142.MaxDecoyChargesPerRound;",
        "_suspicion01 = CounterReconDeceptionModelV142.SuspicionAfterDecoy(_suspicion01);",
        "ExtendReacquisition(CounterReconDeceptionModelV142.ReacquisitionDelayForContext",
    ):
        require(token in runtime, "lifecycle/adaptation contract missing: " + token)

    for pattern in (
        r"ProjectilePool\.Spawn",
        r"\.TakeDamage\s*\(",
        r"\.Damage\s*\(",
        r"SpawnProjectile\s*\(",
        r"Rigidbody2D\.MovePosition",
        r"\.AddForce\s*\(",
        r"Physics2D\.Overlap",
        r"Instantiate\s*\(",
    ):
        require(re.search(pattern, runtime) is None,
                "v14.2 director owns forbidden gameplay authority: " + pattern)

    for token in (
        "CounterReconDeceptionDirectorV142.EnsureInstalled();",
        "ResolveSupportSignaturePosition(position)",
        "CounterBatteryExposureScale",
        "CounterBatteryAcquisitionScale * dt",
        "ResolveLockPosition(playerPosition, _lastSignaturePosition)",
    ):
        require(token in counter_battery, "counter-battery bridge missing: " + token)
    require(
        "EffectiveDesignationHoldSeconds(_profile, _candidateResilience) * CounterReconDeceptionDirectorV142.DesignationHoldScale"
        in observation,
        "counter-observation EMCON designation tradeoff missing",
    )

    for token in (
        "MaxPresentationCues = 2",
        "RefreshCadenceSeconds = 0.25f",
        "MassBattleFxBudget.TryConsumeTacticalCue",
        "CounterReconStateV142.DecoyActive",
        "CounterReconStateV142.EmconRelocating",
        "CounterReconStateV142.Reacquiring",
    ):
        require(token in presentation, "bounded presentation contract missing: " + token)
    for pattern in (r"\.TakeDamage\s*\(", r"SpawnProjectile\s*\(", r"\.AddForce\s*\("):
        require(re.search(pattern, presentation) is None,
                "presentation owns forbidden gameplay authority: " + pattern)

    for token in (
        'HasArgument("-tr-v142-smoke")',
        "CounterReconDeceptionModelV142.ConfigurationValid",
        "CounterReconDeceptionModelV142.PlannedRounds == 100",
        "CounterReconDeceptionModelV142.MaxDecoyChargesPerRound == 2",
        "for (int round = 1; round <= 100; round++)",
        "EMCON retains non-zero hostile exposure/acquisition",
        "reacquisition never grants scripted immunity",
        "adaptation never hard-disables deception",
        "bounded deterministic suspicion",
        "resilience-aware EMCON exposure floor",
        "resilience-aware reacquisition floor",
        "CounterBatteryDirectorV140.EnsureInstalled()",
        "CounterObservationDirectorV141.EnsureInstalled()",
        "CounterReconDeceptionDirectorV142.EnsureInstalled()",
        "V14_2_DECEPTION_OK.txt",
        "Application.Quit(42)",
    ):
        require(token in smoke, "packaged smoke contract missing: " + token)
    for pattern in (r"ProjectilePool\.Spawn", r"\.TakeDamage\s*\(", r"SpawnProjectile\s*\(", r"Physics2D\.Overlap"):
        require(re.search(pattern, smoke) is None,
                "smoke probe owns gameplay authority: " + pattern)

    completed = len(re.findall(r"^- \[x\] ", roadmap, re.M))
    opened = len(re.findall(r"^- \[ \] ", roadmap, re.M))
    total = completed + opened
    require(total == 487, f"unexpected roadmap scope total {total}; expected 487")
    require(479 <= completed <= 487,
            f"unexpected v14.2 closeout state {completed}/{total}")
    require("## v14.2 — Deception, Emission Discipline & Shoot-and-Scoot Warfare — " in roadmap,
            "v14.2 milestone heading missing")

    print(
        "v14.2 runtime gate: PASS — finite deception/EMCON, deterministic adaptation/reacquisition, "
        "bounded hot path/presentation and canonical authority contracts; "
        f"roadmap={completed}/{total}"
    )


if __name__ == "__main__":
    main()
