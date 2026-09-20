#!/usr/bin/env python3
"""Static qualification for the v14.0 packaged runtime, authority and performance gate."""
from __future__ import annotations
import re
from pathlib import Path

SMOKE = Path("Assets/Scripts/CounterBatteryCISmokeProbeV140.cs")
WINDOWS = Path(".github/workflows/counter-battery-v140-windows.yml")
PACKAGE = Path(".github/scripts/package_v140_counter_battery.py")
PACKAGE_TEST = Path(".github/scripts/test_package_v140_counter_battery.py")
ROADMAP = Path("ROADMAP.md")
VERSION = Path("VERSION")


def fail(message: str) -> None:
    raise SystemExit("v14.0 runtime-gate qualification FAIL: " + message)


def require_tokens(text: str, tokens: tuple[str, ...], label: str) -> None:
    for token in tokens:
        if token not in text:
            fail(f"{label} missing token: {token}")


def main() -> None:
    for path in (SMOKE, WINDOWS, PACKAGE, PACKAGE_TEST, ROADMAP, VERSION):
        if not path.is_file():
            fail("missing required file: " + path.as_posix())

    if VERSION.read_text(encoding="utf-8").strip() != "v14.0.0-dev":
        fail("VERSION must be v14.0.0-dev")

    smoke = SMOKE.read_text(encoding="utf-8")
    workflow = WINDOWS.read_text(encoding="utf-8")
    package = PACKAGE.read_text(encoding="utf-8")
    package_test = PACKAGE_TEST.read_text(encoding="utf-8")
    roadmap = ROADMAP.read_text(encoding="utf-8")

    require_tokens(smoke, (
        '-tr-v140-smoke',
        'V14_0_COUNTER_BATTERY_OK.txt',
        'V14_0_COUNTER_BATTERY_FAIL.txt',
        'CounterBatteryModelV140.ConfigurationValid',
        'CounterBatteryModelV140.PlannedRounds == 100',
        'CounterBatteryModelV140.MaxTrackedHostiles == 24',
        'CounterBatteryModelV140.MaxObservers == 6',
        'CounterBatteryModelV140.MaxShells == 4',
        'for (int round = 1; round <= 100; round++)',
        'CounterBatteryModelV140.AddSupportSignature',
        'CounterBatteryModelV140.DecayExposure',
        'CounterBatteryModelV140.TerrainExposureScale',
        'CounterBatteryModelV140.SensorCounterplayScale',
        'CounterBatteryModelV140.RelocationBreakDistance',
        'CounterBatteryModelV140.ObserverWeight',
        'CounterBatteryModelV140.AcquisitionGainPerSecond',
        'CounterBatteryModelV140.BuildIntent',
        'CounterBatteryDirectorV140.EnsureInstalled()',
        'CounterBatteryExecutionBridgeV140.EnsureInstalled()',
    ), "runtime smoke")

    for forbidden in ('FindObjectsByType<EnemyTank>', 'Physics2D.Overlap', '.Damage(', 'ProjectilePool.Spawn'):
        if forbidden in smoke:
            fail("runtime smoke owns forbidden gameplay authority: " + forbidden)

    require_tokens(workflow, (
        'name: Counter-Battery v14.0 Windows Qualification Gate',
        "branches: ['dev-v14-0']",
        'UNITY_VERSION: 6000.3.17f1',
        'Enforce clean Unity qualification state',
        'TankRevival.Editor.CIBuild.BuildWindows',
        'package_v140_counter_battery.py',
        'test_package_v140_counter_battery.py',
        '-tr-v140-smoke',
        'V14_0_COUNTER_BATTERY_OK.txt',
        '-tr-v139-smoke',
        '-tr-v138-smoke',
        '-tr-v137-smoke',
        '-tr-v136-smoke',
        '-demo-ci-soak',
        'candidate ZIP digest mismatch',
        'candidate provenance mismatch',
        'blocking exception signature',
    ), "Windows workflow")
    cache_action = 'actions/' + 'cache@'
    if cache_action in workflow or ('restore' + '-keys:') in workflow:
        fail("Windows qualification may not use cross-candidate Unity Library cache restore")

    require_tokens(package, (
        'V140_COUNTER_BATTERY_BUILD.txt',
        'V140_COUNTER_BATTERY_SHA256.txt',
        'V140_COUNTER_BATTERY_PROVENANCE.json',
        'TankRevivalOverdrive-v14.0-counter-battery-Windows-x64.zip',
        '"tank-revival-v14.0-counter-battery/v1"',
        'version != "v14.0.0-dev"',
        'commit_sha != candidate_sha',
        'ZIP_TIME = (1980, 1, 1, 0, 0, 0)',
    ), "packager")
    require_tokens(package_test, (
        'v14.0 deterministic packager tests: PASS',
        'assert p1["sha256"] == p2["sha256"]',
        '"v14.0.0-dev"',
        '"tank-revival-v14.0-counter-battery/v1"',
    ), "packager test")

    done = len(re.findall(r"^- \[x\] ", roadmap, re.M))
    open_ = len(re.findall(r"^- \[ \] ", roadmap, re.M))
    if (done, open_, done + open_) != (469, 2, 471):
        fail(f"exact candidate must start from open v14.0 gate state 469/471; found {done}/{done + open_}")
    if "| **469** | **2** | **471** | **99.6%** |" not in roadmap:
        fail("roadmap table is not the verified open v14.0 state")

    print("v14.0 runtime-gate source qualification: PASS — packaged smoke/workflow/package contracts are bounded and exact-SHA ready")


if __name__ == "__main__":
    main()
