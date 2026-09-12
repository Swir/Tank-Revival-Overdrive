using System;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Shared combat snapshot. v4.6 consumes RuntimeBattleRegistry membership instead of repeatedly
    /// scanning the Unity scene. Arrays are rebuilt only when units spawn/despawn; per-kind counters
    /// are recalculated from the cached snapshot.
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
        private bool _pendingRefresh;
        private int _lastRevision = -1;

        public static EnemyTank[] Enemies => _enemies;
        public static Health[] HealthUnits => _healthUnits;
        public static PlayerTank Player => _player;
        public static Health Eagle => _eagle;
        public static float LastRefresh => _lastRefresh;
        public static int LivingEnemyCount => _livingEnemies;
        public static int RegistryRevision => RuntimeBattleRegistry.Revision;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CombatRoster>() != null) return;
            var go = new GameObject("CombatRoster");
            DontDestroyOnLoad(go);
            go.AddComponent<CombatRoster>();
        }

        private void OnEnable()
        {
            RuntimeBattleRegistry.Changed -= OnRegistryChanged;
            RuntimeBattleRegistry.Changed += OnRegistryChanged;
            _pendingRefresh = true;
        }

        private void Start()
        {
            RuntimeBattleRegistry.ReconcileOnce();
            RefreshFromRegistry();
        }

        private void OnDisable()
        {
            RuntimeBattleRegistry.Changed -= OnRegistryChanged;
        }

        private void OnRegistryChanged()
        {
            _pendingRefresh = true;
        }

        private void LateUpdate()
        {
            if (!_pendingRefresh && _lastRevision == RuntimeBattleRegistry.Revision) return;
            RefreshFromRegistry();
        }

        private void RefreshFromRegistry()
        {
            _pendingRefresh = false;
            _lastRevision = RuntimeBattleRegistry.Revision;
            _enemies = RuntimeBattleRegistry.EnemySnapshot;
            _healthUnits = RuntimeBattleRegistry.HealthSnapshot;
            _player = RuntimeBattleRegistry.Player;
            _eagle = RuntimeBattleRegistry.Eagle;
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

            _lastRefresh = Time.unscaledTime;
            Refreshed?.Invoke();
        }
    }
}
