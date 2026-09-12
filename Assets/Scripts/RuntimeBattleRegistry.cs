using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Event-driven runtime registry for living combat actors. v4.6 removes repeated scene-wide
    /// discovery from the normal combat loop. Health is the universal lifecycle hook: when a unit
    /// initializes, its EnemyTank/PlayerTank role is captured automatically; teardown unregisters it.
    /// </summary>
    public static class RuntimeBattleRegistry
    {
        private static readonly HashSet<Health> HealthSet = new HashSet<Health>();
        private static readonly HashSet<EnemyTank> EnemySet = new HashSet<EnemyTank>();
        private static Health[] _healthSnapshot = Array.Empty<Health>();
        private static EnemyTank[] _enemySnapshot = Array.Empty<EnemyTank>();
        private static bool _healthDirty = true;
        private static bool _enemyDirty = true;
        private static PlayerTank _player;
        private static Health _eagle;
        private static int _revision;

        public static int Revision => _revision;
        public static PlayerTank Player => _player;
        public static Health Eagle => _eagle;
        public static int RegisteredHealthCount => HealthSet.Count;
        public static int RegisteredEnemyCount => EnemySet.Count;
        public static event Action Changed;

        public static Health[] HealthSnapshot
        {
            get
            {
                if (_healthDirty) RebuildHealthSnapshot();
                return _healthSnapshot;
            }
        }

        public static EnemyTank[] EnemySnapshot
        {
            get
            {
                if (_enemyDirty) RebuildEnemySnapshot();
                return _enemySnapshot;
            }
        }

        public static void RegisterHealth(Health health)
        {
            if (health == null || !HealthSet.Add(health)) return;

            if (health.name == "ORZELEK_DEFENSE_CORE") _eagle = health;

            EnemyTank enemy = health.GetComponent<EnemyTank>();
            if (enemy != null && EnemySet.Add(enemy)) _enemyDirty = true;

            PlayerTank player = health.GetComponent<PlayerTank>();
            if (player != null) _player = player;

            _healthDirty = true;
            Bump();
        }

        public static void UnregisterHealth(Health health)
        {
            if (health == null) return;
            bool changed = HealthSet.Remove(health);
            if (_eagle == health) _eagle = null;

            EnemyTank enemy = health.GetComponent<EnemyTank>();
            if (enemy != null && EnemySet.Remove(enemy))
            {
                _enemyDirty = true;
                changed = true;
            }

            PlayerTank player = health.GetComponent<PlayerTank>();
            if (_player == player) _player = null;

            if (!changed) return;
            _healthDirty = true;
            Bump();
        }

        public static void ReconcileOnce()
        {
            // One safety bootstrap for scene-authored/legacy actors. Normal runtime updates are event-driven.
            Health[] health = UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
            HealthSet.Clear();
            EnemySet.Clear();
            _player = null;
            _eagle = null;

            for (int i = 0; i < health.Length; i++)
            {
                Health h = health[i];
                if (h == null) continue;
                HealthSet.Add(h);
                if (h.name == "ORZELEK_DEFENSE_CORE") _eagle = h;

                EnemyTank enemy = h.GetComponent<EnemyTank>();
                if (enemy != null) EnemySet.Add(enemy);

                PlayerTank player = h.GetComponent<PlayerTank>();
                if (player != null) _player = player;
            }

            _healthDirty = true;
            _enemyDirty = true;
            Bump();
        }

        private static void RebuildHealthSnapshot()
        {
            HealthSet.RemoveWhere(h => h == null);
            _healthSnapshot = new Health[HealthSet.Count];
            HealthSet.CopyTo(_healthSnapshot);
            _healthDirty = false;
        }

        private static void RebuildEnemySnapshot()
        {
            EnemySet.RemoveWhere(e => e == null);
            _enemySnapshot = new EnemyTank[EnemySet.Count];
            EnemySet.CopyTo(_enemySnapshot);
            _enemyDirty = false;
        }

        private static void Bump()
        {
            unchecked { _revision++; }
            Changed?.Invoke();
        }
    }
}
