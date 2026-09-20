from pathlib import Path

smoke = Path('Assets/Scripts/BattlefieldSmokeScreenV143.cs').read_text(encoding='utf-8')
presentation = Path('Assets/Scripts/BattlefieldSmokePresentationV143.cs').read_text(encoding='utf-8')
probe = Path('Assets/Scripts/BattlefieldSmokeCISmokeProbeV143.cs').read_text(encoding='utf-8')
workflow = Path('.github/workflows/smoke-v143-windows.yml').read_text(encoding='utf-8')
packager = Path('.github/scripts/package_v143_smoke.py').read_text(encoding='utf-8')
package_test = Path('.github/scripts/test_package_v143_smoke.py').read_text(encoding='utf-8')
progress_card = Path('assets/readme/progress-card.svg').read_text(encoding='utf-8')
progress_mini = Path('assets/readme/progress-mini.svg').read_text(encoding='utf-8')

for token in [
    '-tr-v143-smoke',
    'V14_3_SMOKE_OK.txt',
    'V14_3_SMOKE_FAIL.txt',
    'BattlefieldSmokeScreenModelV143.ConfigurationValid',
    'BattlefieldSmokePresentationV143.ConfigurationValid',
    'MaxActiveSmokeZones == 1',
    'MaxSmokeChargesPerRound == 2',
    'weather dispersion ordering',
    'player sensor throughput retains non-zero floor',
    'fixed presentation cap',
]:
    assert token in probe, f'missing runtime probe contract: {token}'

for token in [
    'TankRevivalOverdrive-v14.3-smoke-Windows-x64.zip',
    'V143_SMOKE_SHA256.txt',
    'V143_SMOKE_PROVENANCE.json',
    'v14.3.0-dev',
    'tank-revival-v14.3-smoke/v1',
]:
    assert token in packager, f'missing v14.3 package contract: {token}'

assert 'package_v143_smoke.py' in package_test
assert 'v14.3.0-dev' in package_test
assert 'tank-revival-v14.3-smoke/v1' in package_test

for token in [
    'Build and package one exact v14.3 Windows x64 candidate',
    "test \"${GITHUB_REF_NAME}\" = 'dev-v14-3'",
    'Enforce clean Unity qualification state',
    "Invoke-Smoke 'v14.3 smoke and break-contact warfare' '-tr-v143-smoke'",
    "Invoke-Smoke 'v14.2 deception regression' '-tr-v142-smoke'",
    "Invoke-Smoke 'v14.1 counter-observation regression' '-tr-v141-smoke'",
    "Invoke-Smoke 'v14.0 counter-battery regression' '-tr-v140-smoke'",
    "Invoke-Smoke 'v13.9 fire-support regression' '-tr-v139-smoke'",
    "Invoke-Smoke 'round 80/90/100 soak regression' '-demo-ci-soak'",
    'V14_3_WINDOWS_MATRIX_OK.txt',
]:
    assert token in workflow, f'missing Windows qualification contract: {token}'

for forbidden in [
    'actions/cache@',
    'restore-keys:',
]:
    assert forbidden not in workflow, f'forbidden cross-candidate cache contract: {forbidden}'

for text, label in [(smoke, 'smoke'), (presentation, 'presentation')]:
    for forbidden in [
        'FindObjectsOfType',
        'FindGameObjectsWithTag',
        'GameObject.Find(',
        'SpawnProjectile(',
        'ProjectilePool.Spawn',
        '.TakeDamage(',
    ]:
        assert forbidden not in text, f'forbidden authority/hot-path token in {label}: {forbidden}'

for svg, label, fill in [
    (progress_card, 'card', 'width="645.40"'),
    (progress_mini, 'mini', 'width="354.18"'),
]:
    assert '98.4%' in svg, f'v14.3 {label} percentage is stale'
    assert '487 / 495 completed' in svg, f'v14.3 {label} counter is stale'
    assert 'V14.3 IN DEVELOPMENT' in svg, f'v14.3 {label} status is stale'
    assert fill in svg, f'v14.3 {label} geometry is stale'

assert 'OnGUI(' not in smoke
assert 'OnGUI(' not in presentation
print('v14.3 runtime / authority / performance / progress / Windows qualification contracts PASS')
