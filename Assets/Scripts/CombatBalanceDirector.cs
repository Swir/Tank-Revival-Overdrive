using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public static class CombatBalanceTuning
    {
        public static float EnemyHealthMultiplier(int round, EnemyKind kind)
        {
            round = Mathf.Clamp(round, 1, 100);
            float baseScale;
            if (round <= 20)
                baseScale = Mathf.Lerp(0.94f, 1.00f, (round - 1f) / 19f);
            else if (round <= 50)
                baseScale = Mathf.Lerp(1.00f, 1.06f, (round - 20f) / 30f);
            else if (round <= 75)
                baseScale = Mathf.Lerp(1.06f, 1.03f, (round - 50f) / 25f);
            else
                baseScale = Mathf.Lerp(1.03f, 0.96f, (round - 75f) / 25f);

            if (kind == EnemyKind.Boss)
                baseScale = Mathf.Lerp(baseScale, 1.00f, 0.45f);
            else if (kind == EnemyKind.Fast || kind == EnemyKind.Supply)
                baseScale *= 0.97f;
            else if (kind == EnemyKind.Siege)
                baseScale *= 1.02f;

            return Mathf.Clamp(baseScale, 0.90f, 1.10f);
        }

        public static int PressureThresholdForRelief(int round)
        {
            round = Mathf.Clamp(round, 1, 100);
            if (round < 15) return 999;
            if (round < 40) return 9;
            if (round < 70) return 11;
            return 12;
        }

        public static int ReliefCooldownRounds(int round)
        {
            round = Mathf.Clamp(round, 1, 100);
            return round < 50 ? 8 : 10;
        }

        public static string BandName(int round)
        {
            round = Mathf.Clamp(round, 1, 100);
            if (round <= 20) return "EARLY";
            if (round <= 50) return "MID";
            if (round <= 75) return "LATE";
            return "ENDGAME";
        }

        public static bool Validate(out string reason)
        {
            int[] samples = { 1, 25, 50, 75, 100 };
            EnemyKind[] kinds =
            {
                EnemyKind.Basic, EnemyKind.Fast, EnemyKind.Heavy, EnemyKind.Sniper,
                EnemyKind.Siege, EnemyKind.Elite, EnemyKind.Supply, EnemyKind.Boss
            };

            for (int s = 0; s < samples.Length; s++)
            {
                for (int k = 0; k < kinds.Length; k++)
                {
                    float value = EnemyHealthMultiplier(samples[s], kinds[k]);
                    if (value < 0.90f || value > 1.10f)
                    {
                        reason = "health multiplier outside safe bounds";
                        return false;
                    }
                }
            }

            if (PressureThresholdForRelief(1) < 100 || PressureThresholdForRelief(100) < 10)
            {
                reason = "relief thresholds are too permissive";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class BalanceTelemetrySnapshot
    {
        public int round;
        public string band;
        public int enemiesAlive;
        public int playerHealth;
        public int playerMaxHealth;
        public int eagleHealth;
        public int eagleMaxHealth;
        public int reliefsGranted;
        public int tunedEnemies;
        public float pressureIndex;
    }

    [DefaultExecutionOrder(430)]
    public sealed class CombatBalanceDirector : MonoBehaviour
    {
        private readonly HashSet<int> _tunedEnemyIds = new HashSet<int>();
        private TankGame _game;
        private int _lastRound = -1;
        private int _lastReliefRound = -999;
        private int _reliefsGranted;
        private int _tunedEnemies;
        private float _nextTelemetryAt;
        private BalanceTelemetrySnapshot _latest = new BalanceTelemetrySnapshot();

        public static CombatBalanceDirector Instance { get; private set; }
        public BalanceTelemetrySnapshot Latest => _latest;
        public int TunedEnemyCount => _tunedEnemies;
        public int ReliefsGranted => _reliefsGranted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CombatBalanceDirector>() != null) return;
            GameObject go = new GameObject("CombatBalanceDirector_v5_5");
            DontDestroyOnLoad(go);
            go.AddComponent<CombatBalanceDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            RuntimeBattleRegistry.Changed += OnRegistryChanged;
        }

        private void OnDestroy()
        {
            RuntimeBattleRegistry.Changed -= OnRegistryChanged;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_game == null)
                _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) return;

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round != _lastRound)
            {
                _lastRound = round;
                PruneTunedIds();
                ApplyTuningToLiveEnemies();
            }

            if (Time.unscaledTime >= _nextTelemetryAt)
            {
                _nextTelemetryAt = Time.unscaledTime + 1.0f;
                CaptureTelemetry(round);
                TryBoundedEagleRelief(round);
            }
        }

        private void OnRegistryChanged()
        {
            if (_game == null || !_game.IsPlaying) return;
            ApplyTuningToLiveEnemies();
        }

        private void ApplyTuningToLiveEnemies()
        {
            int round = _game != null ? Mathf.Clamp(_game.CurrentRound, 1, 100) : 1;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                int id = enemy.GetInstanceID();
                if (!_tunedEnemyIds.Add(id)) continue;

                Health health = enemy.Health;
                float scale = CombatBalanceTuning.EnemyHealthMultiplier(round, enemy.Kind);
                int targetMaximum = Mathf.Max(1, Mathf.RoundToInt(health.Maximum * scale));
                health.SetMaximum(targetMaximum, targetMaximum > health.Maximum);
                _tunedEnemies++;
            }
        }

        private void CaptureTelemetry(int round)
        {
            Health playerHealth = RuntimeBattleRegistry.Player != null ? RuntimeBattleRegistry.Player.Health : null;
            Health eagle = RuntimeBattleRegistry.Eagle;
            int enemies = RuntimeBattleRegistry.RegisteredEnemyCount;

            _latest.round = round;
            _latest.band = CombatBalanceTuning.BandName(round);
            _latest.enemiesAlive = enemies;
            _latest.playerHealth = playerHealth != null ? playerHealth.Current : 0;
            _latest.playerMaxHealth = playerHealth != null ? playerHealth.Maximum : 0;
            _latest.eagleHealth = eagle != null ? eagle.Current : 0;
            _latest.eagleMaxHealth = eagle != null ? eagle.Maximum : 0;
            _latest.reliefsGranted = _reliefsGranted;
            _latest.tunedEnemies = _tunedEnemies;

            float playerPressure = playerHealth != null && playerHealth.Maximum > 0
                ? 1f - (float)playerHealth.Current / playerHealth.Maximum : 0f;
            float eaglePressure = eagle != null && eagle.Maximum > 0
                ? 1f - (float)eagle.Current / eagle.Maximum : 0f;
            float enemyPressure = Mathf.Clamp01(enemies / 14f);
            _latest.pressureIndex = Mathf.Clamp01(enemyPressure * 0.45f + playerPressure * 0.20f + eaglePressure * 0.35f);
        }

        private void TryBoundedEagleRelief(int round)
        {
            Health eagle = RuntimeBattleRegistry.Eagle;
            if (eagle == null || eagle.IsDead || eagle.Maximum <= 0) return;
            if (round % 10 == 0) return; // boss rounds keep their authored danger.
            if (RuntimeBattleRegistry.RegisteredEnemyCount < CombatBalanceTuning.PressureThresholdForRelief(round)) return;
            if ((float)eagle.Current / eagle.Maximum > 0.34f) return;
            if (round - _lastReliefRound < CombatBalanceTuning.ReliefCooldownRounds(round)) return;

            TankGame game = _game;
            if (game == null) return;
            game.RepairEagle(1);
            _lastReliefRound = round;
            _reliefsGranted++;
            CaptureTelemetry(round);
            Debug.Log("[TankRevival] v5.5 bounded Eagle relief granted at round " + round + ".");
        }

        private void PruneTunedIds()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            _tunedEnemyIds.Clear();
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy != null) _tunedEnemyIds.Add(enemy.GetInstanceID());
            }
        }
    }
}
