using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Event-driven runtime registry for living combat actors. v4.6 removes the need for repeated
    /// scene-wide discovery in the normal combat loop. Components register on initialization and
    /// unregister on teardown; snapshots are rebuilt only when membership actually changes.
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
            _healthDirty = true;
            Bump();
        }

        public static void UnregisterHealth(Health health)
        {
            if (health == null || !HealthSet.Remove(health)) return;
            if (_eagle == health) _eagle = null;
            _healthDirty = true;
            Bump();
        }

        public static void RegisterEnemy(EnemyTank enemy)
        {
            if (enemy == null || !EnemySet.Add(enemy)) return;
            _enemyDirty = true;
            Bump();
        }

        public static void UnregisterEnemy(EnemyTank enemy)
        {
            if (enemy == null || !EnemySet.Remove(enemy)) return;
            _enemyDirty = true;
            Bump();
        }

        public static void RegisterPlayer(PlayerTank player)
        {
            if (_player == player) return;
            _player = player;
            Bump();
        }

        public static void UnregisterPlayer(PlayerTank player)
        {
            if (_player != player) return;
            _player = null;
            Bump();
        }

        public static void ReconcileOnce()
        {
            // Safety bootstrap for legacy/scene-authored actors. Normal runtime membership is event-driven.
            Health[] health = UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
            EnemyTank[] enemies = UnityEngine.Object.FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            PlayerTank player = UnityEngine.Object.FindAnyObjectByType<PlayerTank>();

            HealthSet.Clear();
            EnemySet.Clear();
            _eagle = null;

            for (int i = 0; i < health.Length; i++)
            {
                Health h = health[i];
                if (h == null) continue;
                HealthSet.Add(h);
                if (h.name == "ORZELEK_DEFENSE_CORE") _eagle = h;
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy != null) EnemySet.Add(enemy);
            }

            _player = player;
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
