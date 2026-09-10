using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public static class BattlefieldDestruction
    {
        private const int MaxScars = 90;
        private static readonly Queue<GameObject> Scars = new Queue<GameObject>();

        public static void AddImpactScar(Vector3 position, float strength, Color tint)
        {
            var root = new GameObject("ImpactScar");
            root.transform.position = new Vector3(position.x, position.y, 0f);

            float size = Mathf.Clamp(0.28f + strength * 0.28f, 0.30f, 1.45f);
            VisualFactory.Disc("CraterOuter", root.transform, Vector2.one * size, new Color(0.015f, 0.012f, 0.010f, 0.72f), Vector3.zero, -45);
            VisualFactory.Disc("CraterInner", root.transform, Vector2.one * (size * 0.66f), new Color(0.07f, 0.045f, 0.025f, 0.68f), Vector3.zero, -44);
            VisualFactory.RingObject("HotEdge", root.transform, Vector2.one * (size * 0.82f), new Color(tint.r, tint.g, tint.b, 0.28f), Vector3.zero, -43);
            root.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            Scars.Enqueue(root);
            Trim();
        }

        public static void AddWreck(Vector3 position, Quaternion rotation, EnemyKind kind)
        {
            var root = new GameObject("BurningWreck_" + kind);
            root.transform.position = new Vector3(position.x, position.y, 0f);
            root.transform.rotation = rotation;

            float scale = kind == EnemyKind.Boss ? 1.65f : kind == EnemyKind.Heavy || kind == EnemyKind.Siege ? 1.22f : 0.92f;
            VisualFactory.Rect("WreckShadow", root.transform, new Vector2(0.96f, 0.72f) * scale, new Color(0f, 0f, 0f, 0.58f), new Vector3(0.07f, -0.07f, 0f), -36);
            VisualFactory.Rect("WreckHull", root.transform, new Vector2(0.82f, 0.62f) * scale, new Color(0.10f, 0.085f, 0.075f), Vector3.zero, -35);
            VisualFactory.Rect("WreckPlate", root.transform, new Vector2(0.55f, 0.14f) * scale, new Color(0.28f, 0.12f, 0.055f), new Vector3(0.04f, 0.08f, 0f), -34);
            VisualFactory.Disc("WreckTurret", root.transform, new Vector2(0.48f, 0.48f) * scale, new Color(0.075f, 0.070f, 0.065f), new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.10f, 0.10f), 0f), -33);

            var collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.72f, 0.54f) * scale;

            var decay = root.AddComponent<WreckDecay>();
            decay.Initialize(kind == EnemyKind.Boss ? 42f : 20f + Random.Range(0f, 9f));
            Scars.Enqueue(root);
            Trim();
        }

        public static void AddExplosionDamage(Vector3 center, float radius, int damage, Team owner)
        {
            AddImpactScar(center, Mathf.Clamp(radius, 0.6f, 2.4f), new Color(1f, 0.28f, 0.04f));
            var hits = Physics2D.OverlapCircleAll(center, radius);
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                var obstacle = hit.GetComponent<Obstacle>();
                if (obstacle != null && obstacle.Kind == ObstacleKind.Brick)
                    obstacle.Hit(Mathf.Max(1, damage), center);
            }
        }

        private static void Trim()
        {
            while (Scars.Count > MaxScars)
            {
                var oldest = Scars.Dequeue();
                if (oldest != null) Object.Destroy(oldest);
            }
        }
    }

    public sealed class WreckDecay : MonoBehaviour
    {
        private float _dieAt;
        private float _nextSmoke;

        public void Initialize(float lifetime)
        {
            _dieAt = Time.time + Mathf.Max(8f, lifetime);
            _nextSmoke = Time.time + Random.Range(0.2f, 0.8f);
        }

        private void Update()
        {
            if (Time.time >= _dieAt)
            {
                Destroy(gameObject);
                return;
            }

            if (Time.time >= _nextSmoke)
            {
                _nextSmoke = Time.time + Random.Range(0.48f, 1.05f);
                VisualFactory.MicroBurst(transform.position + new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(-0.10f, 0.20f), 0f), new Color(0.18f, 0.16f, 0.15f), Random.Range(0.28f, 0.52f));
            }
        }
    }
}
