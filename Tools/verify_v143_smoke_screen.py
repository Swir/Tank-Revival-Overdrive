from pathlib import Path

smoke = Path('Assets/Scripts/BattlefieldSmokeScreenV143.cs').read_text(encoding='utf-8')
counter = Path('Assets/Scripts/CounterBatteryWarfareV140.cs').read_text(encoding='utf-8')

required_smoke = [
    'MaxActiveSmokeZones = 1',
    'MaxSmokeChargesPerRound = 2',
    'SmokeRadiusWorld = 6.0f',
    'ScreenLifetimeSeconds = 5.5f',
    'DissipationSeconds = 2.0f',
    'CooldownSeconds = 14.0f',
    'MinWeatherPersistenceScale = 0.70f',
    'MaxWeatherPersistenceScale = 1.20f',
    'BattlefieldWeatherKindV135.Mist: scale = 1.14f',
    'BattlefieldWeatherKindV135.Rain: scale = 0.82f',
    'BattlefieldWeatherKindV135.Storm: scale = 0.70f',
    'BattlefieldWeatherKindV135.Snow: scale = 1.20f',
    'BattlefieldWeatherDirector.Instance',
    'weather.CurrentKind',
    'TacticalTerrainMap.TerrainAt(playerPosition)',
    'CounterObservationTargetSnapshotV141',
    'snapshot.Resilience01',
    'RuntimeBattleRegistry.Player',
    'PlayerInsideZone',
    'Input.GetKeyDown(KeyCode.B)',
    'TryDeploy(Vector2 playerPosition)',
    'ConfigurationValid',
]
for token in required_smoke:
    assert token in smoke, f'missing smoke contract: {token}'

forbidden_smoke = [
    'FindObjectsOfType',
    'FindGameObjectsWithTag',
    'GameObject.Find(',
    'SpawnProjectile(',
    'ProjectilePool.Spawn',
    '.TakeDamage(',
    '.Damage(',
    'Instantiate(',
    'Destroy(enemy',
    'OnGUI(',
]
for token in forbidden_smoke:
    assert token not in smoke, f'forbidden authority/hot-path/presentation token: {token}'

required_bridge = [
    'BattlefieldSmokeScreenDirectorV143.EnsureInstalled()',
    'BattlefieldSmokeScreenDirectorV143.CounterBatteryExposureScale',
    'BattlefieldSmokeScreenDirectorV143.ObserverAcquisitionScale',
    'BattlefieldSmokeScreenDirectorV143.BreakContactDistanceScale',
]
for token in required_bridge:
    assert token in counter, f'missing counter-battery bridge: {token}'

assert counter.count('BattlefieldSmokeScreenDirectorV143.CounterBatteryExposureScale') == 1
assert counter.count('BattlefieldSmokeScreenDirectorV143.ObserverAcquisitionScale') == 1
assert counter.count('BattlefieldSmokeScreenDirectorV143.BreakContactDistanceScale') == 1
print('v14.3 bounded weather-coupled smoke-screen qualification PASS')
