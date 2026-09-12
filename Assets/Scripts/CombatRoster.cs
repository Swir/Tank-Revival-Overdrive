using System;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Shared scene roster for campaign systems. v4.5 keeps the full-scene discovery centralized,
    /// adapts refresh cadence to measured load and publishes one cache refresh event so presentation
    /// systems no longer perform their own duplicate FindObjectsByType scans.
    /// </summary>
    public sealed class CombatRoster : MonoBehaviour
    {
        private static EnemyTank[] _enemies = Array.Empty<EnemyTank>();
        private static Health[] _healthUnits = Array.Empty<Health>();
        private static PlayerTank _player;
        private static Health _eagle;
        private static float _lastRefresh;
        private static int _livingEnemies;
        private static readonly int[] _kindCounts = new int[8];

        public static EnemyTank[] Enemies => _enemies;
        public static Health[] HealthUnits => _healthUnits;
        public static PlayerTank Player => _player;
        public static Health Eagle => _eagle;
        public static float LastRefresh => _lastRefresh;
        public static int LivingEnemyCount => _livingEnemies;
        public static event Action Refreshed;

        public static int Count(EnemyKind kind)
        {
            int index = (int)kind;
            return index >= 0 && index < _kindCounts.Length ? _kindCounts[index] : 0;
        }

        public static bool HasPriorityArmor()
        {
            return Count(EnemyKind.Heavy) + Count(EnemyKind.Siege) + Count(EnemyKind.Elite) + Count(EnemyKind.Boss) > 0;
        }

        private float _nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CombatRoster>() != null) return;
            var go = new GameObject("CombatRoster");
            DontDestroyOnLoad(go);
            go.AddComponent<CombatRoster>();
        }

        private void Start()
        {
            Refresh();
            _nextRefresh = Time.unscaledTime + WarfarePerformanceGovernor.RosterRefreshInterval;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + WarfarePerformanceGovernor.RosterRefreshInterval;
            Refresh();
        }

        private static void Refresh()
        {
            _enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            _healthUnits = FindObjectsByType<Health>(FindObjectsSortMode.None);

            // Resolve singleton-like scene actors from the already collected health/enemy graph first.
            // Player lookup remains one inexpensive single-object query instead of another full array scan.
            _player = FindAnyObjectByType<PlayerTank>();
            _eagle = null;
            _livingEnemies = 0;
            Array.Clear(_kindCounts, 0, _kindCounts.Length);

            for (int i = 0; i < _enemies.Length; i++)
            {
                EnemyTank enemy = _enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                _livingEnemies++;
                int index = (int)enemy.Kind;
                if (index >= 0 && index < _kindCounts.Length) _kindCounts[index]++;
            }

            for (int i = 0; i < _healthUnits.Length; i++)
            {
                Health health = _healthUnits[i];
                if (health != null && health.name == "ORZELEK_DEFENSE_CORE")
                {
                    _eagle = health;
                    break;
                }
            }

            _lastRefresh = Time.unscaledTime;
            Refreshed?.Invoke();
        }
    }
}
