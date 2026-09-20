#!/usr/bin/env python3
"""Source/authority guard for v14.2 deception, EMCON, adaptation and resilience integration."""
from pathlib import Path
import re

V142=Path('Assets/Scripts/CounterReconDeceptionV142.cs')
CB=Path('Assets/Scripts/CounterBatteryWarfareV140.cs')
CO=Path('Assets/Scripts/CounterObservationWarfareV141.cs')

def require(text, token, label):
    if token not in text: raise SystemExit('v14.2 source FAIL: missing '+label)

def main():
    v=V142.read_text(encoding='utf-8'); cb=CB.read_text(encoding='utf-8'); co=CO.read_text(encoding='utf-8')
    for token,label in (
        ('MaxDecoyChargesPerRound = 2','finite decoy charge cap'),
        ('DecoyLifetimeSeconds = 6.0f','finite decoy lifetime'),
        ('EmconLifetimeSeconds = 4.5f','finite EMCON lifetime'),
        ('ReacquisitionDelayForRound','bounded reacquisition model'),
        ('Input.GetKeyDown(KeyCode.G)','decoy control'),
        ('Input.GetKeyDown(KeyCode.V)','EMCON control'),
        ('ResolveSupportSignaturePosition','false-emission resolver'),
        ('ResolveLockPosition','decoy lock resolver'),
        ('CounterBatteryAcquisitionScale','acquisition modifier'),
        ('DesignationHoldScale','sensor/designation tradeoff'),
        ('CounterObservationStateV141.NetworkBroken','observer-disruption handoff'),
        ('SuspicionPerDecoy = 0.18f','deterministic bounded suspicion step'),
        ('MaxSuspicion01 = 0.72f','bounded suspicion ceiling'),
        ('MinDecoyCredibility01 = 0.42f','non-zero decoy credibility floor'),
        ('SuspicionAfterDecoy','deception-break adaptation'),
        ('DecoyCredibility01(float suspicion01, float observerResilience01)','credibility adaptation model'),
        ('ReacquisitionDelayForContext','contextual reacquisition timing'),
        ('ReacquisitionAcquisitionForResilience','non-zero reacquisition acquisition floor'),
        ('CounterObservationTargetSnapshotV141 snapshot = observation.TargetSnapshot','qualified observer-resilience bridge'),
        ('observation.CandidateResilience01','candidate resilience fallback'),
        ('_emconRelocationCredited','single finite relocation credit'),
        ('Vector2.Lerp(truePosition, Instance._decoyPosition, Instance._decoyCredibility01)','bounded false-signature value')):
        require(v,token,label)

    for forbidden,label in (
        ('.TakeDamage(', 'direct damage'),
        ('SpawnProjectile(', 'projectile ownership'),
        ('Instantiate(', 'runtime actor/effect spawn'),
        ('FindObjectsByType<EnemyTank>', 'hot-path scene scan'),
        ('using System.Linq', 'LINQ hot-path dependency')):
        if forbidden in v: raise SystemExit('v14.2 source FAIL: forbidden '+label)

    require(cb,'CounterReconDeceptionDirectorV142.EnsureInstalled();','installation bridge')
    require(cb,'ResolveSupportSignaturePosition(position)','counter-battery false-emission bridge')
    require(cb,'CounterBatteryExposureScale','counter-battery exposure scale')
    require(cb,'CounterBatteryAcquisitionScale * dt','counter-battery acquisition scale')
    require(cb,'ResolveLockPosition(playerPosition, _lastSignaturePosition)','decoy lock bridge')
    require(co,'EffectiveDesignationHoldSeconds(_profile, _candidateResilience) * CounterReconDeceptionDirectorV142.DesignationHoldScale','EMCON designation cost')
    require(co,'return Mathf.Clamp01(terrainResilience * 0.38f + cohesion * 0.36f + command * 0.26f);','terrain/cohesion/command resilience composition')

    if len(re.findall(r'public const int MaxDecoyChargesPerRound',v)) != 1:
        raise SystemExit('v14.2 source FAIL: duplicate decoy cap')
    if len(re.findall(r'ExtendReacquisition\(CounterReconDeceptionModelV142\.ReacquisitionDelayForContext',v)) < 3:
        raise SystemExit('v14.2 source FAIL: contextual reacquisition not wired through all bounded handoffs')
    if 'private float AcquisitionScale => ReacquisitionBlocked\n            ? 0f' in v:
        raise SystemExit('v14.2 source FAIL: reacquisition still grants scripted immunity')

    print('v14.2 deception source: PASS — finite deception, bounded suspicion and qualified resilience integration verified')

if __name__=='__main__': main()
