#!/usr/bin/env python3
"""Source/budget guard for the v14.2 deception/EMCON presentation layer."""
from pathlib import Path
import re

PRESENTATION = Path('Assets/Scripts/CounterReconPresentationV142.cs')
DECEPTION = Path('Assets/Scripts/CounterReconDeceptionV142.cs')


def require(text: str, token: str, label: str) -> None:
    if token not in text:
        raise SystemExit('v14.2 presentation FAIL: missing ' + label)


def main() -> None:
    p = PRESENTATION.read_text(encoding='utf-8')
    d = DECEPTION.read_text(encoding='utf-8')

    for token, label in (
        ('MaxPresentationCues = 2', 'fixed cue cap'),
        ('RefreshCadenceSeconds = 0.25f', 'bounded refresh cadence'),
        ('CueHoldSeconds = 0.55f', 'bounded cue hold'),
        ('MassBattleFxBudget.TryConsumeTacticalCue', 'mass-battle FX budget bridge'),
        ('CounterReconStateV142.DecoyActive', 'decoy presentation state'),
        ('CounterReconStateV142.EmconRelocating', 'EMCON presentation state'),
        ('CounterReconStateV142.Reacquiring', 'reacquisition presentation state'),
        ('CounterReconDeceptionModelV142.MaxSuspicion01', 'adaptation/suspicion presentation'),
        ('_director.DecoyCredibility01', 'decoy credibility HUD bridge'),
        ('private void OnGUI()', 'bounded HUD readout'),
        ('RuntimeBattleRegistry.Player', 'player-relative EMCON cue bridge'),
        ('Runtime3DFactory.Cylinder', 'pooled world-space ring'),
        ('Runtime3DFactory.Box', 'pooled world-space directional cue'),
        ('[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]', 'runtime installation')):
        require(p, token, label)

    for token, label in (
        ('public float Suspicion01', 'suspicion snapshot'),
        ('public float DecoyCredibility01', 'credibility snapshot'),
        ('public float ObserverResilience01', 'resilience snapshot'),
        ('public CounterReconStateV142 State', 'counter-recon state snapshot')):
        require(d, token, label)

    for forbidden, label in (
        ('Instantiate(', 'unbounded runtime instantiate path'),
        ('FindObjectsByType<', 'scene-wide presentation scan'),
        ('using System.Linq', 'LINQ presentation dependency'),
        ('.TakeDamage(', 'damage authority mutation'),
        ('SpawnProjectile(', 'projectile authority mutation'),
        ('.AddForce(', 'physics authority mutation')):
        if forbidden in p:
            raise SystemExit('v14.2 presentation FAIL: forbidden ' + label)

    game_objects = len(re.findall(r'new GameObject\(', p))
    if game_objects != 2:
        raise SystemExit(f'v14.2 presentation FAIL: expected exactly 2 bounded GameObject construction sites, got {game_objects}')

    if len(re.findall(r'new ReconCueSlot\(', p)) != 1:
        raise SystemExit('v14.2 presentation FAIL: cue slots must be created only by the fixed pool loop')

    print('v14.2 presentation: PASS — fixed-cap cues, budget bridge, HUD state and authority bounds verified')


if __name__ == '__main__':
    main()
