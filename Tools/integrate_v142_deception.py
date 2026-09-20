#!/usr/bin/env python3
"""Integrate v14.2 deception/EMCON intent into qualified v14.0/v14.1 authority paths."""
from pathlib import Path

CB=Path('Assets/Scripts/CounterBatteryWarfareV140.cs')
CO=Path('Assets/Scripts/CounterObservationWarfareV141.cs')

def once(text, old, new, label):
    count=text.count(old)
    if count != 1:
        raise SystemExit(f'v14.2 integration FAIL: expected one {label}, found {count}')
    return text.replace(old,new,1)

def main():
    cb=CB.read_text(encoding='utf-8')
    co=CO.read_text(encoding='utf-8')
    install='CounterObservationDirectorV141.EnsureInstalled(); CounterReconDeceptionDirectorV142.EnsureInstalled();'
    if install not in cb:
        cb=once(cb,
            'CounterBatteryExecutionBridgeV140.EnsureInstalled(); CounterObservationDirectorV141.EnsureInstalled();',
            'CounterBatteryExecutionBridgeV140.EnsureInstalled(); CounterObservationDirectorV141.EnsureInstalled(); CounterReconDeceptionDirectorV142.EnsureInstalled();',
            'v14.2 installation bridge')
    old='Vector2 position = player.transform.position; float relocation = _hasSignature ? Vector2.Distance(position, _lastSignaturePosition) : CounterBatteryModelV140.BreakDistance * 2f;\n            _exposure = CounterBatteryModelV140.AddSupportSignature(_exposure, relocation); _lastSignaturePosition = position; _hasSignature = true; _signaturesObserved++;'
    new='Vector2 position = player.transform.position; Vector2 reportedPosition = CounterReconDeceptionDirectorV142.ResolveSupportSignaturePosition(position);\n            float relocation = _hasSignature ? Vector2.Distance(reportedPosition, _lastSignaturePosition) : CounterBatteryModelV140.BreakDistance * 2f;\n            _exposure = CounterBatteryModelV140.AddSupportSignature(_exposure, relocation) * CounterReconDeceptionDirectorV142.CounterBatteryExposureScale;\n            _lastSignaturePosition = reportedPosition; _hasSignature = true; _signaturesObserved++;'
    if 'Vector2 reportedPosition = CounterReconDeceptionDirectorV142.ResolveSupportSignaturePosition(position);' not in cb:
        cb=once(cb,old,new,'false-emission signature bridge')
    old='_acquisition = Mathf.Clamp01(_acquisition + CounterBatteryModelV140.AcquisitionGainPerSecond(_exposure * _terrainExposure, _observerStrength, _profile) * dt);'
    new='_acquisition = Mathf.Clamp01(_acquisition + CounterBatteryModelV140.AcquisitionGainPerSecond(_exposure * _terrainExposure, _observerStrength, _profile) * CounterReconDeceptionDirectorV142.CounterBatteryAcquisitionScale * dt);'
    if 'CounterReconDeceptionDirectorV142.CounterBatteryAcquisitionScale * dt' not in cb:
        cb=once(cb,old,new,'EMCON acquisition bridge')
    old='if (_acquisition >= _profile.LockThreshold) { _state = CounterBatteryStateV140.Locked; _lockedPosition = playerPosition; _lockOrigin = playerPosition; _stateUntil = now + CounterBatteryModelV140.LockWarningSeconds; }'
    new='if (_acquisition >= _profile.LockThreshold) { _state = CounterBatteryStateV140.Locked; _lockedPosition = CounterReconDeceptionDirectorV142.ResolveLockPosition(playerPosition, _lastSignaturePosition); _lockOrigin = playerPosition; _stateUntil = now + CounterBatteryModelV140.LockWarningSeconds; }'
    if 'ResolveLockPosition(playerPosition, _lastSignaturePosition)' not in cb:
        cb=once(cb,old,new,'decoy lock-position bridge')
    old='float requiredHold = CounterObservationModelV141.EffectiveDesignationHoldSeconds(_profile, _candidateResilience);'
    new='float requiredHold = CounterObservationModelV141.EffectiveDesignationHoldSeconds(_profile, _candidateResilience) * CounterReconDeceptionDirectorV142.DesignationHoldScale;'
    if 'CounterReconDeceptionDirectorV142.DesignationHoldScale' not in co:
        co=once(co,old,new,'EMCON designation throughput tradeoff')
    CB.write_text(cb,encoding='utf-8')
    CO.write_text(co,encoding='utf-8')
    print('v14.2 integration: deception + EMCON + reacquisition bridges installed')

if __name__=='__main__': main()
