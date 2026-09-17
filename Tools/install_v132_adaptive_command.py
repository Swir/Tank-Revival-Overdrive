from pathlib import Path


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, got {count}")
    return text.replace(old, new, 1)


def patch_tank_game() -> None:
    path = Path("Assets/Scripts/TankGame.cs")
    text = path.read_text(encoding="utf-8")
    text = replace_once(text,
        "        private ObjectiveRuntimeBudgetV131 _objectiveRuntimeBudget;\n",
        "        private ObjectiveRuntimeBudgetV131 _objectiveRuntimeBudget;\n        private AdaptiveCommandPlanV132 _adaptiveCommandPlan;\n",
        "TankGame field")
    text = replace_once(text,
        "        public ObjectiveRuntimeBudgetV131 CurrentObjectiveRuntimeBudget => _objectiveRuntimeBudget;\n",
        "        public ObjectiveRuntimeBudgetV131 CurrentObjectiveRuntimeBudget => _objectiveRuntimeBudget;\n        public AdaptiveCommandPlanV132 CurrentAdaptiveCommandPlan => _adaptiveCommandPlan;\n",
        "TankGame property")
    text = replace_once(text,
        "            ObjectiveWarfareDirector objectiveDirector = ObjectiveWarfareDirector.EnsureInstalled();\n            objectiveDirector.BeginRound(this, _encounterPlan, readiness);\n            _objectivePlan = objectiveDirector.CurrentPlan;\n            _objectiveRuntimeBudget = objectiveDirector.CurrentRuntimeBudget;\n            _maxAlive = ResolveActiveMaxAlive();\n",
        "            ObjectiveWarfareDirector objectiveDirector = ObjectiveWarfareDirector.EnsureInstalled();\n            ObjectiveRuntimeStateV131 previousObjective = objectiveDirector.CurrentState;\n            objectiveDirector.BeginRound(this, _encounterPlan, readiness);\n            _objectivePlan = objectiveDirector.CurrentPlan;\n            _objectiveRuntimeBudget = objectiveDirector.CurrentRuntimeBudget;\n            AdaptiveEnemyCommandDirector commandDirector = AdaptiveEnemyCommandDirector.EnsureInstalled();\n            commandDirector.BeginRound(_encounterPlan, _objectivePlan, previousObjective);\n            _adaptiveCommandPlan = commandDirector.CurrentPlan;\n            _maxAlive = ResolveActiveMaxAlive();\n",
        "TankGame BeginRound integration")
    text = replace_once(text,
        "            return Mathf.Clamp(_encounterRuntimeBudget.MaxAlive + _objectiveRuntimeBudget.ConcurrencyDelta, 4, EncounterPlannerV130.MaxConcurrentEnemies);\n",
        "            return Mathf.Clamp(_encounterRuntimeBudget.MaxAlive + _objectiveRuntimeBudget.ConcurrencyDelta + _adaptiveCommandPlan.ConcurrencyDelta, 4, EncounterPlannerV130.MaxConcurrentEnemies);\n",
        "TankGame concurrency integration")
    text = replace_once(text,
        "            float scaled = _encounterRuntimeBudget.SpawnInterval * _objectiveRuntimeBudget.SpawnIntervalScale;\n",
        "            float scaled = _encounterRuntimeBudget.SpawnInterval * _objectiveRuntimeBudget.SpawnIntervalScale * _adaptiveCommandPlan.SpawnIntervalScale;\n",
        "TankGame cadence integration")
    text = replace_once(text,
        "                EnemyKind kind = ObjectivePlannerV131.EnemyForObjectiveSpawn(_objectivePlan, _encounterPlan, _spawnOrdinal, baseKind);\n                _spawnOrdinal++;\n",
        "                EnemyKind kind = ObjectivePlannerV131.EnemyForObjectiveSpawn(_objectivePlan, _encounterPlan, _spawnOrdinal, baseKind);\n                kind = AdaptiveEnemyCommandPlannerV132.EnemyForSpawn(_adaptiveCommandPlan, _spawnOrdinal, kind, _encounterPlan.BossRound);\n                _spawnOrdinal++;\n",
        "TankGame spawn composition")
    text = replace_once(text,
        "            ObjectiveWarfareDirector.Instance?.NotifyEnemyDestroyed(kind);\n            int points = kind switch\n",
        "            ObjectiveWarfareDirector.Instance?.NotifyEnemyDestroyed(kind);\n            AdaptiveEnemyCommandDirector.Instance?.NotifyEnemyDestroyed(kind);\n            int points = kind switch\n",
        "TankGame kill telemetry")
    text = replace_once(text,
        "            CapturePlayerLoadout();\n            _player = null;\n            _lives--;\n",
        "            CapturePlayerLoadout();\n            _player = null;\n            AdaptiveEnemyCommandDirector.Instance?.NotifyPlayerDestroyed();\n            _lives--;\n",
        "TankGame loss telemetry")
    text = replace_once(text,
        "            GUI.Box(new Rect(14f, 12f, 560f, 208f), string.Empty);\n",
        "            GUI.Box(new Rect(14f, 12f, 560f, 233f), string.Empty);\n",
        "TankGame HUD height")
    text = replace_once(text,
        "            GUI.Label(new Rect(28f, 178f, 530f, 25f), objectiveHud, _smallStyle);\n\n            if (_player != null)\n",
        "            GUI.Label(new Rect(28f, 178f, 530f, 25f), objectiveHud, _smallStyle);\n            string commandHud = AdaptiveEnemyCommandDirector.Instance != null ? AdaptiveEnemyCommandDirector.Instance.HudText : \"CMD STANDBY\";\n            GUI.Label(new Rect(28f, 203f, 530f, 25f), commandHud, _smallStyle);\n\n            if (_player != null)\n",
        "TankGame command HUD")
    path.write_text(text, encoding="utf-8")


def patch_enemy_tank() -> None:
    path = Path("Assets/Scripts/EnemyTank.cs")
    text = path.read_text(encoding="utf-8")
    text = replace_once(text,
        "                float reload = _armor != null ? _armor.ReloadMultiplier : 1f;\n                _nextShot = Time.time + Random.Range(_shotDelay * .82f, _shotDelay * 1.18f) * reload;\n",
        "                float reload = _armor != null ? _armor.ReloadMultiplier : 1f;\n                _nextShot = Time.time + Random.Range(_shotDelay * .82f, _shotDelay * 1.18f) * reload * AdaptiveEnemyCommandDirector.ReloadScale(Kind);\n",
        "EnemyTank reload posture")
    text = replace_once(text,
        "            bool doctrine = localPreference || AdvancedGunneryDoctrineDirector.PreferPlayer(Kind, _round) || counter;\n",
        "            bool doctrine = localPreference || AdvancedGunneryDoctrineDirector.PreferPlayer(Kind, _round) || AdaptiveEnemyCommandDirector.PreferPlayer(Kind) || counter;\n",
        "EnemyTank target posture")
    text = replace_once(text,
        "            _body.MovePosition(_body.position + _facing * (_speed * m * casualtyScale * Time.fixedDeltaTime));\n",
        "            _body.MovePosition(_body.position + _facing * (_speed * m * casualtyScale * AdaptiveEnemyCommandDirector.MovementScale(Kind) * Time.fixedDeltaTime));\n",
        "EnemyTank movement posture")
    text = replace_once(text,
        "            float spread = FireControlBallisticsDirector.EnemySpreadDegrees(Kind, movement, _armor, coordinated) * AdvancedGunneryDoctrineDirector.SpreadMultiplier(Kind, _round) * CounterFireThreatMemory.AccuracyMultiplier(this);\n",
        "            float spread = FireControlBallisticsDirector.EnemySpreadDegrees(Kind, movement, _armor, coordinated) * AdvancedGunneryDoctrineDirector.SpreadMultiplier(Kind, _round) * CounterFireThreatMemory.AccuracyMultiplier(this) * AdaptiveEnemyCommandDirector.SpreadScale(Kind);\n",
        "EnemyTank spread posture")
    path.write_text(text, encoding="utf-8")


patch_tank_game()
patch_enemy_tank()

# Re-read exact integration invariants before allowing the commit.
tank = Path("Assets/Scripts/TankGame.cs").read_text(encoding="utf-8")
enemy = Path("Assets/Scripts/EnemyTank.cs").read_text(encoding="utf-8")
required_tank = [
    "AdaptiveCommandPlanV132 _adaptiveCommandPlan",
    "AdaptiveEnemyCommandDirector.EnsureInstalled()",
    "commandDirector.BeginRound(_encounterPlan, _objectivePlan, previousObjective)",
    "_adaptiveCommandPlan.ConcurrencyDelta",
    "_adaptiveCommandPlan.SpawnIntervalScale",
    "AdaptiveEnemyCommandPlannerV132.EnemyForSpawn",
    "AdaptiveEnemyCommandDirector.Instance?.NotifyEnemyDestroyed(kind)",
    "AdaptiveEnemyCommandDirector.Instance?.NotifyPlayerDestroyed()",
    "AdaptiveEnemyCommandDirector.Instance.HudText",
]
required_enemy = [
    "AdaptiveEnemyCommandDirector.ReloadScale(Kind)",
    "AdaptiveEnemyCommandDirector.PreferPlayer(Kind)",
    "AdaptiveEnemyCommandDirector.MovementScale(Kind)",
    "AdaptiveEnemyCommandDirector.SpreadScale(Kind)",
]
for token in required_tank:
    if token not in tank: raise SystemExit(f"missing TankGame integration token: {token}")
for token in required_enemy:
    if token not in enemy: raise SystemExit(f"missing EnemyTank integration token: {token}")
print("v13.2 adaptive command integration patch: PASS")
