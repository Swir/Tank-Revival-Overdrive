#!/usr/bin/env python3
"""Source/authority guard for v14.2 deception, EMCON and shoot-and-scoot phase one."""
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
        ('CounterObservationStateV141.NetworkBroken','observer-disruption handoff')):
        require(v,token,label)
    for forbidden,label in (
        ('.TakeDamage(', 'direct damage'),
        ('SpawnProjectile(', 'projectile ownership'),
        ('Instantiate(', 'runtime actor/effect spawn'),
        ('FindObjectsByType<EnemyTank>', 'hot-path scene scan')):
        if forbidden in v: raise SystemExit('v14.2 source FAIL: forbidden '+label)
    require(cb,'CounterReconDeceptionDirectorV142.EnsureInstalled();','installation bridge')
    require(cb,'ResolveSupportSignaturePosition(position)','counter-battery false-emission bridge')
    require(cb,'CounterBatteryExposureScale','counter-battery exposure scale')
    require(cb,'CounterBatteryAcquisitionScale * dt','counter-battery acquisition scale')
    require(cb,'ResolveLockPosition(playerPosition, _lastSignaturePosition)','decoy lock bridge')
    require(co,'EffectiveDesignationHoldSeconds(_profile, _candidateResilience) * CounterReconDeceptionDirectorV142.DesignationHoldScale','EMCON designation cost')
    if len(re.findall(r'public const int MaxDecoyChargesPerRound',v)) != 1: raise SystemExit('v14.2 source FAIL: duplicate decoy cap')
    print('v14.2 deception source: PASS — finite decoy, EMCON and reacquisition authority bridges verified')

if __name__=='__main__': main()
