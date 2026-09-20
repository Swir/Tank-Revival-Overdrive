#!/usr/bin/env python3
"""Source qualification for the v14.1 observer-hunt presentation pass."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PRESENTATION_PATH = ROOT / 'Assets/Scripts/CounterObservationPresentationV141.cs'
CORE_PATH = ROOT / 'Assets/Scripts/CounterObservationWarfareV141.cs'


def require(text: str, token: str, label: str) -> None:
    if token not in text:
        raise SystemExit(f'v14.1 presentation FAIL: missing {label}: {token}')


def forbid(text: str, token: str, label: str) -> None:
    if token in text:
        raise SystemExit(f'v14.1 presentation FAIL: forbidden {label}: {token}')


def main() -> int:
    presentation = PRESENTATION_PATH.read_text(encoding='utf-8-sig')
    core = CORE_PATH.read_text(encoding='utf-8-sig')

    for token, label in (
        ('class CounterObservationPresentationV141', 'presentation director'),
        ('MaxPresentationCues = 2', 'fixed two-cue pool'),
        ('new ObserverCueSlot[MaxPresentationCues]', 'fixed cue storage'),
        ('RefreshCadenceSeconds = 0.25f', 'bounded refresh cadence'),
        ('CounterObservationDirectorV141.Instance', 'hunter-killer state source'),
        ('CounterObservationTargetSnapshotV141 snapshot', 'target snapshot bridge'),
        ('CounterObservationStateV141.Designated', 'designated presentation state'),
        ('CounterObservationStateV141.NetworkBroken', 'network-broken presentation state'),
        ('snapshot.ConfirmedKill', 'canonical kill confirmation visualization'),
        ('MassBattleFxBudget.TryConsumeTacticalCue', 'existing pooled FX budget'),
        ('Runtime3DFactory.Cylinder', 'pooled world-space ring geometry'),
        ('Runtime3DFactory.Box', 'pooled world-space crosshair geometry'),
        ('BuildPool()', 'one-time cue pool construction'),
        ('VisibleCueCount', 'bounded presentation telemetry'),
    ):
        require(presentation, token, label)

    match = re.search(r'public const int MaxPresentationCues\s*=\s*(\d+)', presentation)
    if not match or int(match.group(1)) > 2:
        raise SystemExit('v14.1 presentation FAIL: cue pool must remain <= 2')

    cadence = re.search(r'public const float RefreshCadenceSeconds\s*=\s*([0-9.]+)f', presentation)
    if not cadence or not 0.20 <= float(cadence.group(1)) <= 0.50:
        raise SystemExit('v14.1 presentation FAIL: refresh cadence outside 0.20-0.50s')

    if presentation.count('MassBattleFxBudget.TryConsumeTacticalCue') != 2:
        raise SystemExit('v14.1 presentation FAIL: both fixed world cues must consume tactical budget')

    # No presentation path may become a combat/gameplay authority or start broad hot-path scans.
    for token, label in (
        ('ProjectilePool', 'projectile-pool authority'),
        ('SpawnProjectile(', 'canonical projectile execution'),
        ('ApplyDamage(', 'damage authority'),
        ('TakeDamage(', 'damage authority'),
        ('RuntimeBattleRegistry.EnemySnapshot', 'parallel enemy scan'),
        ('FindObjectsByType<EnemyTank>', 'enemy scene scan'),
        ('FindObjectsOfType<EnemyTank>', 'enemy scene scan'),
        ('Physics2D.OverlapCircleAll', 'allocating physics scan'),
        ('Instantiate(', 'runtime presentation allocation'),
    ):
        forbid(presentation, token, label)

    # The existing compact HUD owns textual SEARCH/DESIGNATED/NETWORK BROKEN/COOLDOWN,
    # target class/range and counter-battery pressure. The new pass adds only world emphasis.
    for token, label in (
        ('case CounterObservationStateV141.Searching: return "SEARCH"', 'SEARCH HUD state'),
        ('case CounterObservationStateV141.Designated: return "DESIGNATED"', 'DESIGNATED HUD state'),
        ('case CounterObservationStateV141.NetworkBroken: return "NETWORK BROKEN"', 'NETWORK BROKEN HUD state'),
        ('case CounterObservationStateV141.Cooldown: return "COOLDOWN"', 'COOLDOWN HUD state'),
        ('_targetSnapshot.CompactLabel', 'target class/range HUD'),
        ('"  ·  CB scale "', 'counter-battery pressure HUD'),
        ('CounterObservationModelV141.MaxObserverCandidates', 'observer cap telemetry'),
    ):
        require(core, token, label)

    if 'MaxPresentationCues <= CounterObservationModelV141.MaxObserverCandidates' not in presentation:
        raise SystemExit('v14.1 presentation FAIL: presentation cap must be tied below observer cap')

    print('v14.1 observer-hunt presentation qualification: PASS')
    print(' - fixed world-space cue pool: 2')
    print(' - refresh cadence: 0.25s')
    print(' - all cues consume MassBattleFxBudget tactical tokens')
    print(' - HUD covers SEARCH/DESIGNATED/NETWORK BROKEN/COOLDOWN + target/range/CB scale')
    print(' - no projectile/damage/enemy-scan authority introduced')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
