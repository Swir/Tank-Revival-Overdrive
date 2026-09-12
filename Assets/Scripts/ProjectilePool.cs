using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Persistent projectile pool for the 100-round campaign. Projectile GameObjects, colliders,
    /// rigidbodies and renderers are created once and reused across shots and round rebuilds.
    /// </summary>
    public static class ProjectilePool
    {
        private const int WarmCount = 48;
        private const int MaxRetained = 160;

        private static readonly Queue<Projectile> Inactive = new Queue<Projectile>(MaxRetained);
        private static readonly HashSet<Projectile> Active = new HashSet<Projectile>();
        private static readonly List<Projectile> ReleaseBuffer = new List<Projectile>(MaxRetained);
        private static Transform _root;
        private static int _created;
        private static int _reused;
        private static bool _warmed;

        public static int ActiveCount => Active.Count;
        public static int InactiveCount => Inactive.Count;
        public static int CreatedCount => _created;
        public static int ReusedCount => _reused;

        public static Projectile Spawn(Vector2 position, Vector2 direction, Team team, int damage, float speed, Color color, AmmoType ammo)
        {
            EnsureRoot();
            WarmIfNeeded();

            Projectile projectile = null;
            while (Inactive.Count > 0 && projectile == null)
                projectile = Inactive.Dequeue();

            if (projectile == null)
            {
                projectile = CreateProjectile();
            }
            else
            {
                _reused++;
                projectile.gameObject.SetActive(true);
            }

            Active.Add(projectile);
            projectile.transform.SetParent(_root, false);
            projectile.transform.position = position;
            projectile.gameObject.name = team == Team.Player ? $"PlayerProjectile_{ammo}" : $"EnemyProjectile_{ammo}";
            projectile.Initialize(direction, team, damage, speed, color, ammo);
            return projectile;
        }

        public static void Release(Projectile projectile)
        {
            if (projectile == null) return;
            if (!Active.Remove(projectile) && !projectile.gameObject.activeSelf) return;

            projectile.PrepareForPool();
            projectile.transform.SetParent(_root, false);
            projectile.gameObject.SetActive(false);

            if (Inactive.Count < MaxRetained)
                Inactive.Enqueue(projectile);
            else
                Object.Destroy(projectile.gameObject);
        }

        public static void ReleaseAllActive()
        {
            if (Active.Count == 0) return;
            ReleaseBuffer.Clear();
            foreach (Projectile projectile in Active)
                if (projectile != null) ReleaseBuffer.Add(projectile);

            for (int i = 0; i < ReleaseBuffer.Count; i++)
                Release(ReleaseBuffer[i]);

            ReleaseBuffer.Clear();
        }

        public static void Forget(Projectile projectile)
        {
            if (projectile == null) return;
            Active.Remove(projectile);
        }

        private static void WarmIfNeeded()
        {
            if (_warmed) return;
            _warmed = true;
            for (int i = 0; i < WarmCount; i++)
            {
                Projectile projectile = CreateProjectile();
                projectile.PrepareForPool();
                projectile.gameObject.SetActive(false);
                Inactive.Enqueue(projectile);
            }
        }

        private static Projectile CreateProjectile()
        {
            EnsureRoot();
            var go = new GameObject("PooledProjectile");
            go.transform.SetParent(_root, false);
            var projectile = go.AddComponent<Projectile>();
            _created++;
            return projectile;
        }

        private static void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("ProjectilePool_Runtime");
            Object.DontDestroyOnLoad(go);
            _root = go.transform;
        }
    }
}
