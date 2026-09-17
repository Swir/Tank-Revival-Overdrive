#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one anchor, found {count}")
    return text.replace(old, new, 1)


def patch_encounter() -> None:
    path = ROOT / "Assets/Scripts/EncounterWarfareV130.cs"
    s = path.read_text(encoding="utf-8")

    anchor = """        public static void BuildAll(EncounterPlan[] destination)\n        {\n            if (destination == null || destination.Length < PlannedRounds) return;\n            for (int i = 0; i < PlannedRounds; i++) destination[i] = PlanForRound(i + 1);\n        }\n"""
    replacement = """        /// <summary>\n        /// Deterministic support-wave composition consumed by TankGame. No Unity random state, allocations or authority writes.\n        /// Boss rounds keep the boss itself in TankGame's existing boss path while this helper produces its escort wave.\n        /// </summary>\n        public static EnemyKind EnemyForSpawn(EncounterPlan plan, int spawnOrdinal)\n        {\n            int ordinal = Mathf.Max(0, spawnOrdinal);\n            int roll = PositiveMod(unchecked(plan.Signature * 397 + ordinal * 101 + plan.Round * 53), 100);\n\n            if (plan.BossRound)\n            {\n                if (roll < 24) return plan.Round >= 60 ? EnemyKind.Elite : EnemyKind.Heavy;\n                if (roll < 44) return EnemyKind.Siege;\n                if (roll < 61) return EnemyKind.Sniper;\n                if (roll < 75) return EnemyKind.Fast;\n                if (roll < 84) return EnemyKind.Supply;\n                return plan.Round >= 40 ? EnemyKind.Heavy : EnemyKind.Basic;\n            }\n\n            if (roll < 52) return plan.PrimaryEnemy;\n            if (roll < 62 && plan.Round >= 3) return EnemyKind.Supply;\n\n            switch (plan.Doctrine)\n            {\n                case EncounterDoctrine.ArmoredAssault: return roll < 84 ? EnemyKind.Heavy : EnemyKind.Siege;\n                case EncounterDoctrine.HunterKiller: return roll < 82 ? EnemyKind.Fast : EnemyKind.Sniper;\n                case EncounterDoctrine.ArtillerySiege: return roll < 84 ? EnemyKind.Siege : EnemyKind.Heavy;\n                case EncounterDoctrine.LogisticsInterdiction: return roll < 82 ? EnemyKind.Fast : EnemyKind.Supply;\n                case EncounterDoctrine.ElectronicSuppression: return roll < 84 ? EnemyKind.Sniper : EnemyKind.Elite;\n                case EncounterDoctrine.RouteBreakthrough: return roll < 84 ? EnemyKind.Fast : EnemyKind.Heavy;\n                case EncounterDoctrine.CombinedArms:\n                    if (roll < 74) return EnemyKind.Heavy;\n                    if (roll < 88) return EnemyKind.Sniper;\n                    return plan.Round >= 60 ? EnemyKind.Elite : EnemyKind.Fast;\n                default: return EnemyKind.Basic;\n            }\n        }\n\n        public static EagleDefensePlanV130 EagleDefenseFor(EncounterPlan plan)\n        {\n            float pressure = Mathf.Clamp01(plan.EaglePressure);\n            int tier = pressure >= 0.78f ? 3 : pressure >= 0.52f ? 2 : 1;\n            int wallHp = tier == 3 ? 4 : tier == 2 ? 3 : 2;\n            return new EagleDefensePlanV130\n            {\n                Tier = tier,\n                WallHitPoints = wallHp,\n                SteelSides = tier >= 2,\n                SteelCrown = tier >= 3\n            };\n        }\n\n        public static void BuildAll(EncounterPlan[] destination)\n        {\n            if (destination == null || destination.Length < PlannedRounds) return;\n            for (int i = 0; i < PlannedRounds; i++) destination[i] = PlanForRound(i + 1);\n        }\n\n        private static int PositiveMod(int value, int divisor)\n        {\n            int result = value % divisor;\n            return result < 0 ? result + divisor : result;\n        }\n"""
    s = replace_once(s, anchor, replacement, "EncounterPlanner helper insertion")

    struct_anchor = """    /// <summary>Pure phase policy used by the real boss weapon authority.</summary>\n    public static class BossPhaseWarfareV130\n"""
    struct_replacement = """    public struct EagleDefensePlanV130\n    {\n        public int Tier;\n        public int WallHitPoints;\n        public bool SteelSides;\n        public bool SteelCrown;\n    }\n\n    /// <summary>Pure phase policy used by the real boss weapon authority.</summary>\n    public static class BossPhaseWarfareV130\n"""
    s = replace_once(s, struct_anchor, struct_replacement, "EagleDefensePlan insertion")
    path.write_text(s, encoding="utf-8")


def patch_tank_game() -> None:
    path = ROOT / "Assets/Scripts/TankGame.cs"
    s = path.read_text(encoding="utf-8")

    constants = """        private const int FinalRound = 100;\n        private const int EagleMaxHealth = 6;\n"""
    constants_new = """        private const int FinalRound = 100;\n        private const int EagleMaxHealth = 6;\n        private static readonly Vector2[] EnemySpawnPoints =\n        {\n            new Vector2(-9.5f, 5.65f),\n            new Vector2(-3.2f, 5.65f),\n            new Vector2(3.2f, 5.65f),\n            new Vector2(9.5f, 5.65f)\n        };\n"""
    s = replace_once(s, constants, constants_new, "TankGame static spawn points")

    fields = """        private bool _bossPending;\n        private float _nextSpawn;\n        private float _roundClearAt = -1f;\n"""
    fields_new = """        private bool _bossPending;\n        private float _nextSpawn;\n        private int _spawnOrdinal;\n        private EncounterPlan _encounterPlan;\n        private float _roundClearAt = -1f;\n"""
    s = replace_once(s, fields, fields_new, "TankGame encounter fields")

    props = """        public Vector2 BasePosition => _baseObject != null ? (Vector2)_baseObject.transform.position : new Vector2(0f, -5.95f);\n        public int CurrentRound => _round;\n"""
    props_new = """        public Vector2 BasePosition => _baseObject != null ? (Vector2)_baseObject.transform.position : new Vector2(0f, -5.95f);\n        public int CurrentRound => _round;\n        public EncounterPlan CurrentEncounterPlan => _encounterPlan;\n"""
    s = replace_once(s, props, props_new, "TankGame current plan property")

    begin = """        private void BeginRound(int round)\n        {\n            _roundClearAt = -1f;\n            _aliveEnemies = 0;\n            _enemiesToSpawn = Mathf.Min(62, 6 + Mathf.CeilToInt(round * 0.50f));\n            _maxAlive = Mathf.Min(14, 4 + round / 10);\n            _bossPending = round % 10 == 0;\n            _nextSpawn = Time.time + 0.70f;\n            BuildArena(round);\n\n            if (_bossPending)\n            {\n                ShowToast($\"ROUND {round:000} // BOSS ASSAULT\", 2.0f);\n                BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.72f, 0f);\n            }\n            else\n            {\n                ShowToast($\"ROUND {round:000} // DEFEND THE EAGLE\", 1.45f);\n            }\n        }\n"""
    begin_new = """        private void BeginRound(int round)\n        {\n            _roundClearAt = -1f;\n            _aliveEnemies = 0;\n            _encounterPlan = EncounterPlannerV130.PlanForRound(round);\n            _enemiesToSpawn = _encounterPlan.EnemyCount;\n            _maxAlive = _encounterPlan.MaxAlive;\n            _bossPending = _encounterPlan.BossRound;\n            _spawnOrdinal = 0;\n            _nextSpawn = Time.time + _encounterPlan.SpawnInterval;\n            BuildArena(round);\n\n            if (_bossPending)\n            {\n                ShowToast($\"ROUND {round:000} // {_encounterPlan.Doctrine} // BOSS ASSAULT\", 2.0f);\n                BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.72f, 0f);\n            }\n            else\n            {\n                ShowToast($\"ROUND {round:000} // {_encounterPlan.Doctrine} // DEFEND THE EAGLE\", 1.55f);\n            }\n        }\n"""
    s = replace_once(s, begin, begin_new, "TankGame BeginRound plan consumption")

    eagle = """            ObstacleKind sideKind = round >= 70 ? ObstacleKind.Steel : ObstacleKind.Brick;\n            int wallHp = round >= 45 ? 3 : 2;\n            CreateObstacle(new Vector2(-1.15f, -5.85f), sideKind, new Vector2(0.75f, 0.75f), wallHp);\n            CreateObstacle(new Vector2(1.15f, -5.85f), sideKind, new Vector2(0.75f, 0.75f), wallHp);\n            CreateObstacle(new Vector2(-0.75f, -5.05f), ObstacleKind.Brick, new Vector2(0.75f, 0.75f), wallHp);\n            CreateObstacle(new Vector2(0.75f, -5.05f), ObstacleKind.Brick, new Vector2(0.75f, 0.75f), wallHp);\n"""
    eagle_new = """            EagleDefensePlanV130 defense = EncounterPlannerV130.EagleDefenseFor(_encounterPlan);\n            ObstacleKind sideKind = defense.SteelSides ? ObstacleKind.Steel : ObstacleKind.Brick;\n            ObstacleKind crownKind = defense.SteelCrown ? ObstacleKind.Steel : ObstacleKind.Brick;\n            int wallHp = defense.WallHitPoints;\n            CreateObstacle(new Vector2(-1.15f, -5.85f), sideKind, new Vector2(0.75f, 0.75f), wallHp);\n            CreateObstacle(new Vector2(1.15f, -5.85f), sideKind, new Vector2(0.75f, 0.75f), wallHp);\n            CreateObstacle(new Vector2(-0.75f, -5.05f), crownKind, new Vector2(0.75f, 0.75f), wallHp);\n            CreateObstacle(new Vector2(0.75f, -5.05f), crownKind, new Vector2(0.75f, 0.75f), wallHp);\n"""
    s = replace_once(s, eagle, eagle_new, "TankGame Orzelek fortification coupling")

    spawn = """        private void HandleSpawning()\n        {\n            if (_aliveEnemies >= _maxAlive || Time.time < _nextSpawn) return;\n\n            if (_enemiesToSpawn > 0)\n            {\n                _enemiesToSpawn--;\n                SpawnEnemy(ChooseEnemyKind(_round));\n                _nextSpawn = Time.time + Mathf.Max(0.28f, 1.10f - _round * 0.0068f);\n                return;\n            }\n\n            if (_bossPending)\n            {\n                _bossPending = false;\n                SpawnEnemy(EnemyKind.Boss);\n                ShowToast($\"BOSS // ROUND {_round:000}\", 2.1f);\n                KickCamera(0.38f, 0.16f);\n                BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.78f, 0f);\n            }\n        }\n"""
    spawn_new = """        private void HandleSpawning()\n        {\n            if (_aliveEnemies >= _maxAlive || Time.time < _nextSpawn) return;\n\n            if (_enemiesToSpawn > 0)\n            {\n                EnemyKind kind = EncounterPlannerV130.EnemyForSpawn(_encounterPlan, _spawnOrdinal);\n                _spawnOrdinal++;\n                _enemiesToSpawn--;\n                SpawnEnemy(kind);\n                _nextSpawn = Time.time + Mathf.Max(EncounterPlannerV130.MinSpawnInterval, _encounterPlan.SpawnInterval);\n                return;\n            }\n\n            if (_bossPending)\n            {\n                _bossPending = false;\n                SpawnEnemy(EnemyKind.Boss);\n                ShowToast($\"BOSS // ROUND {_round:000} // {BossPhaseWarfareV130.PhaseCountForRound(_round)} PHASES\", 2.1f);\n                KickCamera(0.38f, 0.16f);\n                BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.78f, 0f);\n            }\n        }\n"""
    s = replace_once(s, spawn, spawn_new, "TankGame deterministic spawn consumption")

    spawn_array = """        private void SpawnEnemy(EnemyKind kind)\n        {\n            Vector2[] spawnPoints =\n            {\n                new Vector2(-9.5f, 5.65f),\n                new Vector2(-3.2f, 5.65f),\n                new Vector2(3.2f, 5.65f),\n                new Vector2(9.5f, 5.65f)\n            };\n\n            Vector2 pos = spawnPoints[Random.Range(0, spawnPoints.Length)];\n"""
    spawn_array_new = """        private void SpawnEnemy(EnemyKind kind)\n        {\n            Vector2 pos = EnemySpawnPoints[Random.Range(0, EnemySpawnPoints.Length)];\n"""
    s = replace_once(s, spawn_array, spawn_array_new, "TankGame static spawn array consumption")

    hud = """            GUI.Box(new Rect(14f, 12f, 480f, 108f), string.Empty);\n            GUI.Label(new Rect(28f, 19f, 455f, 27f), $\"ROUND {_round:000}/100     SCORE {_score:N0}     ENEMY {remaining}\", _hudStyle);\n            GUI.Label(new Rect(28f, 47f, 455f, 27f), $\"LIVES {_lives}   ARMOR {playerHp}   ORZEŁEK {eagleHp}/{EagleMaxHealth}\", eagleHp <= 2 ? _warningStyle : _hudStyle);\n            GUI.Label(new Rect(28f, 75f, 455f, 27f), $\"AMMO {AmmoDatabase.DisplayName(active)}  [{ammoCount}]   •   Q/E switch\", _hudStyle);\n"""
    hud_new = """            GUI.Box(new Rect(14f, 12f, 480f, 158f), string.Empty);\n            GUI.Label(new Rect(28f, 19f, 455f, 27f), $\"ROUND {_round:000}/100     SCORE {_score:N0}     ENEMY {remaining}\", _hudStyle);\n            GUI.Label(new Rect(28f, 47f, 455f, 27f), $\"LIVES {_lives}   ARMOR {playerHp}   ORZEŁEK {eagleHp}/{EagleMaxHealth}\", eagleHp <= 2 ? _warningStyle : _hudStyle);\n            GUI.Label(new Rect(28f, 75f, 455f, 27f), $\"AMMO {AmmoDatabase.DisplayName(active)}  [{ammoCount}]   •   Q/E switch\", _hudStyle);\n            GUI.Label(new Rect(28f, 103f, 455f, 25f), $\"ACT {_encounterPlan.Act}   DOCTRINE {_encounterPlan.Doctrine}\", _smallStyle);\n            string bossTelemetry = _encounterPlan.BossRound && EncounterWarfareDirector.Instance != null && EncounterWarfareDirector.Instance.BossPhase > 0\n                ? $\"   BOSS {EncounterWarfareDirector.Instance.BossPhase}/{EncounterWarfareDirector.Instance.BossPhaseCount} {EncounterWarfareDirector.Instance.BossPhaseLabel}\"\n                : string.Empty;\n            GUI.Label(new Rect(28f, 128f, 455f, 25f), $\"THREAT {_encounterPlan.EnemyCount}/{_encounterPlan.MaxAlive}   EAGLE P{Mathf.RoundToInt(_encounterPlan.EaglePressure * 100f):00}   SIG {_encounterPlan.Signature:X8}{bossTelemetry}\", _smallStyle);\n"""
    s = replace_once(s, hud, hud_new, "TankGame v13 HUD telemetry")

    if "ChooseEnemyKind(_round)" in s:
        raise SystemExit("legacy random enemy composition still consumed by live spawn path")
    path.write_text(s, encoding="utf-8")


def patch_smoke() -> None:
    path = ROOT / "Assets/Scripts/EncounterWarfareCISmokeProbe.cs"
    s = path.read_text(encoding="utf-8")
    anchor = """                if (bossRounds != 10) { Fail(\"expected exactly ten boss rounds, got \" + bossRounds); return; }\n"""
    block = """                // Live-consumption contracts: deterministic wave composition and monotonic Orzelek fortification.\n                int spawnFold = 23;\n                int supplySpawns = 0;\n                for (int r = 0; r < plans.Length; r++)\n                {\n                    EncounterPlan plan = plans[r];\n                    for (int ordinal = 0; ordinal < plan.EnemyCount; ordinal++)\n                    {\n                        EnemyKind a = EncounterPlannerV130.EnemyForSpawn(plan, ordinal);\n                        EnemyKind b = EncounterPlannerV130.EnemyForSpawn(plan, ordinal);\n                        if (a != b) { Fail(\"spawn composition is not deterministic at round \" + plan.Round); return; }\n                        if (a == EnemyKind.Boss) { Fail(\"support wave leaked boss authority at round \" + plan.Round); return; }\n                        if (a == EnemyKind.Supply) supplySpawns++;\n                        spawnFold = unchecked(spawnFold * 31 + (int)a);\n                    }\n                }\n                if (supplySpawns <= 0) { Fail(\"deterministic campaign composition contains no supply units\"); return; }\n\n                EagleDefensePlanV130 earlyDefense = EncounterPlannerV130.EagleDefenseFor(plans[0]);\n                EagleDefensePlanV130 midDefense = EncounterPlannerV130.EagleDefenseFor(plans[49]);\n                EagleDefensePlanV130 lateDefense = EncounterPlannerV130.EagleDefenseFor(plans[99]);\n                if (earlyDefense.Tier < 1 || lateDefense.Tier > 3 ||\n                    earlyDefense.Tier > midDefense.Tier || midDefense.Tier > lateDefense.Tier ||\n                    earlyDefense.WallHitPoints > midDefense.WallHitPoints || midDefense.WallHitPoints > lateDefense.WallHitPoints ||\n                    !lateDefense.SteelSides || !lateDefense.SteelCrown)\n                { Fail(\"Orzelek fortification policy is not bounded/monotonic\"); return; }\n\n                if (bossRounds != 10) { Fail(\"expected exactly ten boss rounds, got \" + bossRounds); return; }\n"""
    s = replace_once(s, anchor, block, "v13 smoke live-consumption assertions")

    report_anchor = """                    \"bossPhases=3..4 maxTrackedRounds=\" + EncounterWarfareDirector.MaxTrackedRounds + \" maxTrackedBosses=\" + EncounterWarfareDirector.MaxTrackedBosses + \"\\n\";\n"""
    report_new = """                    \"bossPhases=3..4 maxTrackedRounds=\" + EncounterWarfareDirector.MaxTrackedRounds + \" maxTrackedBosses=\" + EncounterWarfareDirector.MaxTrackedBosses + \"\\n\" +\n                    \"spawnFold=\" + spawnFold + \" supplySpawns=\" + supplySpawns + \" fortificationTiers=\" + earlyDefense.Tier + \"/\" + midDefense.Tier + \"/\" + lateDefense.Tier + \"\\n\";\n"""
    s = replace_once(s, report_anchor, report_new, "v13 smoke report extension")
    path.write_text(s, encoding="utf-8")


def patch_workflow() -> None:
    path = ROOT / ".github/workflows/encounter-warfare-v130-windows.yml"
    s = path.read_text(encoding="utf-8")
    path_anchor = """      - 'Assets/Scripts/EncounterWarfareCISmokeProbe.cs.meta'\n      - 'Assets/Scripts/BossWeaponController.cs'\n"""
    path_new = """      - 'Assets/Scripts/EncounterWarfareCISmokeProbe.cs.meta'\n      - 'Assets/Scripts/TankGame.cs'\n      - 'Assets/Scripts/BossWeaponController.cs'\n"""
    s = replace_once(s, path_anchor, path_new, "v13 gate TankGame path trigger")

    contract_anchor = """          grep -q 'bossRounds != 10' Assets/Scripts/EncounterWarfareCISmokeProbe.cs\n          grep -q 'doctrineCount != 8' Assets/Scripts/EncounterWarfareCISmokeProbe.cs\n"""
    contract_new = """          grep -q 'bossRounds != 10' Assets/Scripts/EncounterWarfareCISmokeProbe.cs\n          grep -q 'doctrineCount != 8' Assets/Scripts/EncounterWarfareCISmokeProbe.cs\n          grep -q 'EnemyForSpawn' Assets/Scripts/EncounterWarfareV130.cs\n          grep -q 'EagleDefenseFor' Assets/Scripts/EncounterWarfareV130.cs\n          grep -q '_encounterPlan = EncounterPlannerV130.PlanForRound(round)' Assets/Scripts/TankGame.cs\n          grep -q '_enemiesToSpawn = _encounterPlan.EnemyCount' Assets/Scripts/TankGame.cs\n          grep -q '_maxAlive = _encounterPlan.MaxAlive' Assets/Scripts/TankGame.cs\n          grep -q 'EncounterPlannerV130.EnemyForSpawn(_encounterPlan, _spawnOrdinal)' Assets/Scripts/TankGame.cs\n          grep -q 'EncounterPlannerV130.EagleDefenseFor(_encounterPlan)' Assets/Scripts/TankGame.cs\n          grep -q 'DOCTRINE {_encounterPlan.Doctrine}' Assets/Scripts/TankGame.cs\n          if grep -q 'SpawnEnemy(ChooseEnemyKind(_round))' Assets/Scripts/TankGame.cs; then\n            echo 'legacy random enemy composition still owns live spawn selection' >&2; exit 1\n          fi\n"""
    s = replace_once(s, contract_anchor, contract_new, "v13 gate live-consumption contracts")
    path.write_text(s, encoding="utf-8")


def patch_changelog() -> None:
    path = ROOT / "CHANGELOG_v13.0.md"
    s = path.read_text(encoding="utf-8")
    marker = "## Unreleased\n"
    if marker in s and "Live encounter consumption" not in s:
        s = s.replace(marker, marker + "\n### Live encounter consumption\n- `TankGame` now consumes the deterministic v13.0 encounter plan for enemy budget, concurrency, spawn cadence and doctrine-driven wave composition while remaining the sole spawn authority.\n- Orzelek fortification now scales from the plan's bounded `EaglePressure` policy without changing Eagle HP authority.\n- HUD telemetry now exposes act, doctrine, threat/concurrency budget, Eagle pressure, exact plan signature and live boss phase.\n- Enemy spawn-point storage is static to remove the previous per-spawn array allocation.\n- Packaged v13.0 smoke now verifies deterministic full-campaign spawn composition and bounded monotonic fortification tiers.\n")
    elif "Live encounter consumption" not in s:
        s += "\n## Live encounter consumption\n- `TankGame` consumes v13.0 plan budgets, deterministic doctrine composition and Orzelek pressure fortification; HUD exposes exact plan telemetry.\n"
    path.write_text(s, encoding="utf-8")


def verify() -> None:
    encounter = (ROOT / "Assets/Scripts/EncounterWarfareV130.cs").read_text(encoding="utf-8")
    tank = (ROOT / "Assets/Scripts/TankGame.cs").read_text(encoding="utf-8")
    smoke = (ROOT / "Assets/Scripts/EncounterWarfareCISmokeProbe.cs").read_text(encoding="utf-8")
    workflow = (ROOT / ".github/workflows/encounter-warfare-v130-windows.yml").read_text(encoding="utf-8")
    required = [
        ("encounter deterministic composition", "public static EnemyKind EnemyForSpawn" in encounter),
        ("encounter Orzelek policy", "public static EagleDefensePlanV130 EagleDefenseFor" in encounter),
        ("TankGame plan budget", "_enemiesToSpawn = _encounterPlan.EnemyCount;" in tank and "_maxAlive = _encounterPlan.MaxAlive;" in tank),
        ("TankGame deterministic spawn", "EncounterPlannerV130.EnemyForSpawn(_encounterPlan, _spawnOrdinal)" in tank),
        ("TankGame fortification", "EncounterPlannerV130.EagleDefenseFor(_encounterPlan)" in tank),
        ("TankGame HUD telemetry", "SIG {_encounterPlan.Signature:X8}" in tank),
        ("static spawn points", "private static readonly Vector2[] EnemySpawnPoints" in tank and "Vector2[] spawnPoints" not in tank),
        ("smoke composition", "spawnFold=" in smoke and "fortificationTiers=" in smoke),
        ("Windows gate TankGame trigger", "- 'Assets/Scripts/TankGame.cs'" in workflow),
    ]
    failed = [name for name, ok in required if not ok]
    if failed:
        raise SystemExit("verification failed: " + ", ".join(failed))
    if "SpawnEnemy(ChooseEnemyKind(_round))" in tank:
        raise SystemExit("verification failed: legacy random composition remains live")
    print("v13.0 live encounter integration registrar: PASS")


if __name__ == "__main__":
    patch_encounter()
    patch_tank_game()
    patch_smoke()
    patch_workflow()
    patch_changelog()
    verify()
