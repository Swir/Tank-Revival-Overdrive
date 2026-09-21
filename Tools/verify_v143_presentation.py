from pathlib import Path

presentation = Path('Assets/Scripts/BattlefieldSmokePresentationV143.cs').read_text(encoding='utf-8')
smoke = Path('Assets/Scripts/BattlefieldSmokeScreenV143.cs').read_text(encoding='utf-8')

required = [
    'MaxPresentationCues = 2',
    'RefreshCadenceSeconds = 0.25f',
    'CueHoldSeconds = 0.55f',
    'SmokePresentationPhaseV143.Screening',
    'SmokePresentationPhaseV143.Dispersing',
    'SmokePresentationPhaseV143.BreakContact',
    'SmokePresentationPhaseV143.Cooldown',
    '"SCREENING"',
    '"DISPERSE"',
    '"BREAK CONTACT"',
    '"COOLDOWN"',
    'MassBattleFxBudget.TryConsumeTacticalCue',
    'RuntimeBattleRegistry.Player',
    'BattlefieldSmokeScreenDirectorV143.BreakContactDistanceScale',
    'Runtime3DFactory.Cylinder',
    'Runtime3DFactory.Box',
]
for token in required:
    assert token in presentation, f'missing v14.3 presentation contract: {token}'

for forbidden in [
    'OnGUI(',
    'GUI.Label',
    'GUI.Box',
    'FindObjectsOfType',
    'FindGameObjectsWithTag',
    'GameObject.Find(',
    'SpawnProjectile(',
    'ProjectilePool.Spawn',
    '.TakeDamage(',
    '.Damage(',
]:
    assert forbidden not in presentation, f'forbidden presentation/debug/authority token: {forbidden}'

assert presentation.count('new SmokeCueSlot[') == 1
assert 'MaxPresentationCues = 2' in presentation
assert 'public int VisibleCueCount' in presentation
assert 'public SmokePresentationPhaseV143 Phase' in presentation
assert 'OnGUI(' not in smoke
print('v14.3 fixed-cap smoke presentation qualification PASS')
