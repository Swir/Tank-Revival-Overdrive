#!/usr/bin/env python3
"""Apply the narrow v13.4 runtime integration to existing canonical game authorities."""
from pathlib import Path


def replace_once(path: str, old: str, new: str) -> bool:
    p = Path(path)
    text = p.read_text(encoding='utf-8')
    if new in text:
        return False
    if text.count(old) != 1:
        raise RuntimeError(f'{path}: expected exactly one integration anchor, found {text.count(old)}')
    p.write_text(text.replace(old, new, 1), encoding='utf-8')
    return True


changed = False
# Cover composition must be a true permutation for every cover-count value, including 7.
changed |= replace_once(
    'Assets/Scripts/TacticalTerrainV134.cs',
    '            int rank = PositiveMod(ordinal * 7 + plan.SlotOffset, Mathf.Max(1, plan.CoverCount));\n',
    '            int rank = PositiveMod(ordinal + plan.SlotOffset, Mathf.Max(1, plan.CoverCount));\n',
)
changed |= replace_once(
    'Assets/Scripts/TankGame.cs',
    '            BuildArena(round);\n',
    '            BuildArena(round);\n            TacticalTerrainDirector.EnsureInstalled().BeginRound(this, round, _encounterPlan, _objectivePlan);\n',
)
changed |= replace_once(
    'Assets/Scripts/EnemyTank.cs',
    '            desired = BattlefieldCohesionDirector.AdjustDirection(this, transform.position, desired);\n',
    '            desired = BattlefieldCohesionDirector.AdjustDirection(this, transform.position, desired);\n            desired = TacticalTerrainDirector.AdjustDirection(this, transform.position, _game.PlayerPosition, desired);\n',
)
changed |= replace_once(
    'Assets/Scripts/TankGame.cs',
    '            GUI.Box(new Rect(14f, 12f, 560f, 233f), string.Empty);\n',
    '            GUI.Box(new Rect(14f, 12f, 560f, 283f), string.Empty);\n',
)
changed |= replace_once(
    'Assets/Scripts/TankGame.cs',
    '            GUI.Label(new Rect(28f, 203f, 530f, 25f), commandHud, _smallStyle);\n',
    '            GUI.Label(new Rect(28f, 203f, 530f, 25f), commandHud, _smallStyle);\n            string squadHud = BattlefieldCohesionDirector.Instance != null ? BattlefieldCohesionDirector.Instance.HudText : "SQD STANDBY";\n            GUI.Label(new Rect(28f, 228f, 530f, 25f), squadHud, _smallStyle);\n            string terrainHud = TacticalTerrainDirector.Instance != null ? TacticalTerrainDirector.Instance.HudText : "TRN STANDBY";\n            GUI.Label(new Rect(28f, 253f, 530f, 25f), terrainHud, _smallStyle);\n',
)

version = Path('VERSION')
current = version.read_text(encoding='utf-8').strip()
if current == 'v13.3.0-dev':
    version.write_text('v13.4.0-dev\n', encoding='utf-8')
    changed = True
elif current != 'v13.4.0-dev':
    raise RuntimeError('unexpected VERSION: ' + current)

print('v13.4 runtime integration:', 'updated' if changed else 'no-op')
