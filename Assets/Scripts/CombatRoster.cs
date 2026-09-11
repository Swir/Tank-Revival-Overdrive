using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.2 shared scene roster. Consolidates common FindObjectsByType scans used by campaign directors
    /// into a single bounded refresh so late-game systems can query the same snapshot.
    /// </summary>
    public sealed class CombatRoster : MonoBehaviour
    {
        private static EnemyTank[] _enemies = System.Array.Empty<EnemyTank>();
        private static Health[] _healthUnits = System.Array.Empty<Health>();
        private static PlayerTank _player;
        private static Health _eagle;
        private static float _lastRefresh;

        public static EnemyTank[] Enemies => _enemies;
        public static Health[] HealthUnits => _healthUnits;
        public static PlayerTank Player => _player;
        public static Health Eagle => _eagle;
        public static float LastRefresh => _lastRefresh;

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
