#!/usr/bin/env python3
"""Apply the bounded v14.1 counter-observation bridge to v14.0 counter-battery authority."""
from pathlib import Path

PATH = Path('Assets/Scripts/CounterBatteryWarfareV140.cs')


def once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count == 0 and new in text:
        return text
    if count != 1:
        raise SystemExit(f'v14.1 integration FAIL: expected one {label}, found {count}')
    return text.replace(old, new, 1)


def main() -> None:
    text = PATH.read_text(encoding='utf-8')
    original = text
    text = once(
        text,
        'AdaptiveEnemyCommandDirector.EnsureInstalled(); BattlefieldCohesionDirector.EnsureInstalled(); CounterBatteryExecutionBridgeV140.EnsureInstalled();',
        'AdaptiveEnemyCommandDirector.EnsureInstalled(); BattlefieldCohesionDirector.EnsureInstalled(); CounterBatteryExecutionBridgeV140.EnsureInstalled(); CounterObservationDirectorV141.EnsureInstalled();',
        'v14.1 installation bridge')
    text = once(
        text,
        '_observerStrength = _observerCount > 0 ? Mathf.Clamp01(weight / maxWeight) : 0f;',
        'float rawObserverStrength = _observerCount > 0 ? Mathf.Clamp01(weight / maxWeight) : 0f;\n            _observerStrength = Mathf.Clamp01(rawObserverStrength * CounterObservationDirectorV141.CounterBatteryNetworkScale);',
        'counter-battery network scale bridge')
    PATH.write_text(text, encoding='utf-8')
    print('v14.1 counter-observation integration: ' + ('UPDATED' if text != original else 'NO-OP'))


if __name__ == '__main__':
    main()
