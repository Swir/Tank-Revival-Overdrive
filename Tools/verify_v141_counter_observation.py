#!/usr/bin/env python3
"""Static authority/scope verification for v14.1 counter-observation warfare."""
from __future__ import annotations
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / 'Assets/Scripts/CounterObservationWarfareV141.cs'
SMOKE = ROOT / 'Assets/Scripts/CounterObservationCISmokeProbeV141.cs'
COUNTER_BATTERY = ROOT / 'Assets/Scripts/CounterBatteryWarfareV140.cs'
ROADMAP = ROOT / 'ROADMAP.md'
README = ROOT / 'README.md'
VERSION = ROOT / 'VERSION'


def require(value: bool, message: str) -> None:
    if not value:
        raise SystemExit('v14.1 counter-observation verifier FAIL: ' + message)


def main() -> None:
    runtime = RUNTIME.read_text(encoding='utf-8')
    smoke = SMOKE.read_text(encoding='utf-8')
    cb = COUNTER_BATTERY.read_text(encoding='utf-8')
    roadmap = ROADMAP.read_text(encoding='utf-8')
    readme = README.read_text(encoding='utf-8')

    require(VERSION.read_text(encoding='utf-8').strip() == 'v14.1.0-dev', 'VERSION must be v14.1.0-dev')
    require('public enum CounterObservationStateV141' in runtime, 'state model missing')
    for state in ('Idle', 'Searching', 'Designated', 'NetworkBroken', 'Cooldown'):
        require(re.search(r'\b' + state + r'\b', runtime) is not None, 'state missing: ' + state)
    for token in (
        'PlannedRounds = 100',
        'MaxTrackedHostiles = 24',
        'MaxObserverCandidates = 6',
        'RuntimeBattleRegistry.EnemySnapshot',
        'BattlefieldSensorFusionDirector.Instance',
        'sensor.TryGetContact(',
        'HasDesignationEvidence(',
        '_designatedHealth.Died += OnDesignatedObserverDied',
        'CounterBatteryNetworkScale',
        'CounterBatteryDirectorV140.Instance',
        'CounterObservationTargetSnapshotV141',
        'TargetSnapshot => _targetSnapshot',
        'RuntimeBattleRegistry.Revision',
        'IsDesignatedStillRegistered()',
        'TacticalTerrainDirector.Instance',
        'terrain.ActiveCoverCount',
        'BattlefieldCohesionDirector.IntentFor(actor)',
        'AdaptiveEnemyCommandDirector.Instance',
        'EffectiveDesignationHoldSeconds(',
        'EffectiveNetworkBreakSeconds(',
        'KILL CONFIRMED',
    ):
        require(token in runtime, 'runtime contract missing: ' + token)

    install = 'CounterObservationDirectorV141.EnsureInstalled();'
    require(cb.count(install) == 1, 'counter-observation installation bridge must exist exactly once')
    bridge = '_observerStrength = Mathf.Clamp01(rawObserverStrength * CounterObservationDirectorV141.CounterBatteryNetworkScale);'
    require(cb.count(bridge) == 1, 'counter-battery acquisition bridge must exist exactly once')
    require(cb.count('float rawObserverStrength = _observerCount > 0 ? Mathf.Clamp01(weight / maxWeight) : 0f;') == 1,
            'raw observer-strength authority split missing')

    forbidden_runtime = (
        r'\.Damage\s*\(', r'SpawnProjectile\s*\(', r'ProjectilePool\.Spawn',
        r'FindObjectsByType\s*<\s*EnemyTank', r'Physics2D\.Overlap', r'Rigidbody2D\.MovePosition',
        r'Destroy\s*\(\s*enemy', r'Instantiate\s*\('
    )
    for pattern in forbidden_runtime:
        require(re.search(pattern, runtime) is None, 'forbidden runtime authority pattern: ' + pattern)
    for pattern in (r'\.Damage\s*\(', r'SpawnProjectile\s*\(', r'ProjectilePool\.Spawn', r'FindObjectsByType\s*<\s*EnemyTank', r'Physics2D\.Overlap'):
        require(re.search(pattern, smoke) is None, 'smoke must not own gameplay authority: ' + pattern)

    for token in (
        'CounterObservationModelV141.ConfigurationValid',
        'for (int round = 1; round <= 100; round++)',
        'tracked requires finite sweep',
        'finite acquisition suppression',
        'disciplined observers take longer to designate',
        'disciplined observers recover network faster',
        'observer network is never immune',
        'bounded target handoff snapshot',
        'CounterObservationDirectorV141.EnsureInstalled()',
    ):
        require(token in smoke, 'smoke contract missing: ' + token)

    completed = len(re.findall(r'^- \[x\] ', roadmap, re.M))
    opened = len(re.findall(r'^- \[ \] ', roadmap, re.M))
    require((completed, opened) in ((471, 8), (474, 5)), f'unexpected roadmap state {completed}/{opened}')
    require('## v14.1 — Counter-Observation & Hunter-Killer Warfare — IN DEVELOPMENT' in roadmap, 'active milestone missing')
    require('<!-- SWIR-ROADMAP-STANDARD:v1 -->' in roadmap, 'roadmap marker missing')
    require('<!-- SWIR-README-STANDARD:v2 -->' in readme, 'README v2 marker missing')
    require('assets/readme/progress-card.svg' in readme and 'assets/readme/progress-mini.svg' in roadmap, 'progress SVG embedding missing')
    require('## 🔎 Search Keywords' in readme, 'Search Keywords missing')
    require(not re.search(r'(?m)^[\s>*-]*[█▓▒░]{6,}', roadmap), 'legacy character meter found')
    require(not re.search(r'(?m)^[\s>*-]*\[[=#█▓▒░-]{8,}\]', roadmap), 'legacy bracket meter found')

    print(f'v14.1 counter-observation verifier: PASS — roadmap {completed}/{completed + opened}, hunter-killer handoff/resilience integrated')


if __name__ == '__main__':
    main()
