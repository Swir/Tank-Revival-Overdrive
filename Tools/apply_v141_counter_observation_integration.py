#!/usr/bin/env python3
"""Apply the bounded v14.1 counter-observation bridge to v14.0 counter-battery authority."""
from pathlib import Path
import re

PATH = Path('Assets/Scripts/CounterBatteryWarfareV140.cs')

INSTALL_BASE = (
    'AdaptiveEnemyCommandDirector.EnsureInstalled(); BattlefieldCohesionDirector.EnsureInstalled(); '
    'CounterBatteryExecutionBridgeV140.EnsureInstalled();'
)
INSTALL_CALL = 'CounterObservationDirectorV141.EnsureInstalled();'


def once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count == 0 and new in text:
        return text
    if count != 1:
        raise SystemExit(f'v14.1 integration FAIL: expected one {label}, found {count}')
    return text.replace(old, new, 1)


def normalize_installation_bridge(text: str) -> str:
    """Install exactly one v14.1 director call and collapse legacy duplicate calls.

    The integration finalizer is push-triggered and therefore must be strictly idempotent.
    The old generic substring replacement treated INSTALL_BASE as absent only after the call,
    but INSTALL_BASE remains a prefix of the integrated line, causing one extra call per run.
    """
    pattern = re.compile(
        re.escape(INSTALL_BASE) + r'(?:\s+' + re.escape(INSTALL_CALL) + r')*'
    )
    matches = list(pattern.finditer(text))
    if len(matches) != 1:
        raise SystemExit(f'v14.1 integration FAIL: expected one installation bridge site, found {len(matches)}')
    return pattern.sub(INSTALL_BASE + ' ' + INSTALL_CALL, text, count=1)


def main() -> None:
    text = PATH.read_text(encoding='utf-8')
    original = text
    text = normalize_installation_bridge(text)
    text = once(
        text,
        '_observerStrength = _observerCount > 0 ? Mathf.Clamp01(weight / maxWeight) : 0f;',
        'float rawObserverStrength = _observerCount > 0 ? Mathf.Clamp01(weight / maxWeight) : 0f;\n            _observerStrength = Mathf.Clamp01(rawObserverStrength * CounterObservationDirectorV141.CounterBatteryNetworkScale);',
        'counter-battery network scale bridge')
    if text.count(INSTALL_CALL) != 1:
        raise SystemExit(f'v14.1 integration FAIL: installation bridge count is {text.count(INSTALL_CALL)}, expected 1')
    PATH.write_text(text, encoding='utf-8')
    print('v14.1 counter-observation integration: ' + ('UPDATED' if text != original else 'NO-OP'))


if __name__ == '__main__':
    main()
