using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Shared scene roster for campaign systems. v2.3 extends the cache with living-enemy and
    /// per-class counts so directors can reason about battlefield composition without extra scene scans.
    /// </summary>
    public sealed class CombatRoster : MonoBehaviour
    {
        private static EnemyTank[] _enemies = System.Array.Empty<EnemyTank>();
        private static Health[] _healthUnits = System.Array.Empty<Health>();
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

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.30f;
            Refresh();
        }

        private static void Refresh()
        {
            _enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            _healthUnits = FindObjectsByType<Health>(FindObjectsSortMode.None);
            _player = FindAnyObjectByType<PlayerTank>();
            _eagle = null;
            _livingEnemies = 0;
            System.Array.Clear(_kindCounts, 0, _kindCounts.Length);

            foreach (EnemyTank enemy in _enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                _livingEnemies++;
                int index = (int)enemy.Kind;
                if (index >= 0 && index < _kindCounts.Length) _kindCounts[index]++;
            }

            foreach (Health health in _healthUnits)
            {
                if (health != null && health.name == "ORZELEK_DEFENSE_CORE")
                {
                    _eagle = health;
                    break;
                }
            }

            _lastRefresh = Time.unscaledTime;
        }
    }
}
