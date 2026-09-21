from pathlib import Path

smoke = Path('Assets/Scripts/BattlefieldSmokeScreenV143.cs').read_text(encoding='utf-8')
probe = Path('Assets/Scripts/BattlefieldSmokeCISmokeProbeV143.cs').read_text(encoding='utf-8')
counter = Path('Assets/Scripts/CounterBatteryWarfareV140.cs').read_text(encoding='utf-8')
morale = Path('Assets/Scripts/BattlefieldSuppressionMoraleV138.cs').read_text(encoding='utf-8')

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
    'KeyboardDeployKey = KeyCode.B',
    'GamepadDeployKey = KeyCode.JoystickButton3',
    'Input.GetKeyDown(BattlefieldSmokeScreenModelV143.KeyboardDeployKey)',
    'Input.GetKeyDown(BattlefieldSmokeScreenModelV143.GamepadDeployKey)',
    'TryDeploy(Vector2 playerPosition)',
    'ConfigurationValid',
    'KeyboardDeployKey == KeyCode.B && GamepadDeployKey == KeyCode.JoystickButton3',
    'MaxSuppressionRecoveryScale = 1.30f',
    'BossSuppressionRecoveryScale = 1.15f',
    'SuppressionRecoveryScale(float strength01, EnemyKind kind)',
    'SuppressionRecoveryScaleAt(Vector2 worldPosition, EnemyKind kind)',
    'if (Instance == null || !Instance.ScreenActive) return 1f;',
    'return Mathf.Clamp(Mathf.Lerp(1f, cap, Mathf.Clamp01(strength01)), 1f, cap);',
]
for token in required_smoke:
    assert token in smoke, f'missing smoke contract: {token}'

required_probe = [
    'Require(BattlefieldSmokeScreenModelV143.KeyboardDeployKey == KeyCode.B, "keyboard smoke binding")',
    'Require(BattlefieldSmokeScreenModelV143.GamepadDeployKey == KeyCode.JoystickButton3, "gamepad smoke binding")',
    'input=PASS',
]
for token in required_probe:
    assert token in probe, f'missing runtime input contract: {token}'

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

required_recovery = [
    'BattlefieldMoraleStateV138.Suppressed',
    'BattlefieldMoraleStateV138.Broken',
    'BattlefieldMoraleStateV138.Recovering',
    'BattlefieldSmokeScreenDirectorV143.SuppressionRecoveryScaleAt(e.Enemy.transform.position, e.Kind)',
]
for token in required_recovery:
    assert token in morale, f'missing suppression recovery bridge: {token}'
assert morale.count('BattlefieldSmokeScreenDirectorV143.SuppressionRecoveryScaleAt(e.Enemy.transform.position, e.Kind)') == 1
assert 'e.Pressure = Mathf.Max(0f, e.Pressure - decay * BattlefieldSuppressionModelV138.SampleCadenceSeconds);' in morale
print('v14.3 bounded weather-coupled smoke-screen + dual-input + finite suppression recovery qualification PASS')
