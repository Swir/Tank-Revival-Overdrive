using System.Collections;
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

            var presentation = root.AddComponent<ImpactScar3DPresentation>();
            presentation.Initialize(size, tint);

            // v3.8: artillery and mine scars are now persistent tactical terrain.
            // The visual scar remains the lifetime owner, so the mobility field disappears
            // naturally when the existing bounded destruction queue trims old impacts.
            root.AddComponent<TacticalTerrainField>().Initialize(TacticalTerrainKind.Crater, size * 0.52f, 0.82f, 0f, 0.97f);

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
            root.AddComponent<WreckDecay>().Initialize(kind == EnemyKind.Boss ? 42f : 20f + Random.Range(0f, 9f));

            var presentation = root.AddComponent<Wreck3DPresentation>();
            presentation.Initialize();

            Scars.Enqueue(root);
            Trim();
        }

        public static void AddExplosionDamage(Vector3 center, float radius, int damage)
        {
            AddImpactScar(center, radius, new Color(1f, 0.28f, 0.04f));
            foreach (var hit in Physics2D.OverlapCircleAll(center, radius))
            {
                if (hit == null) continue;
                var health = hit.GetComponent<Health>();
                if (health != null) health.Damage(Mathf.Max(1, damage), Team.Neutral);
                var obstacle = hit.GetComponent<Obstacle>();
                if (obstacle != null && obstacle.Kind == ObstacleKind.Brick) obstacle.Hit(Mathf.Max(1, damage), center);
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

    public sealed class BattlefieldDirector : MonoBehaviour
    {
        private readonly HashSet<int> _hooked = new HashSet<int>();
        private TankGame _game;
        private float _nextScan;
        private float _nextBarrage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindAnyObjectByType<BattlefieldDirector>() != null) return;
            new GameObject("BattlefieldDirector").AddComponent<BattlefieldDirector>();
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) return;

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.7f;
                HookEnemies();
            }

            int round = _game.CurrentRound;
            if (round >= 20 && Time.time >= _nextBarrage)
            {
                float cadence = Mathf.Lerp(15f, 6.5f, Mathf.InverseLerp(20f, 100f, round));
                _nextBarrage = Time.time + Random.Range(cadence * 0.8f, cadence * 1.25f);
                StartCoroutine(ArtilleryStrike(round));
            }
        }

        private void HookEnemies()
        {
            foreach (var enemy in FindObjectsByType<EnemyTank>(FindObjectsSortMode.None))
            {
                if (enemy == null || enemy.Health == null) continue;
                int id = enemy.GetInstanceID();
                if (_hooked.Contains(id)) continue;
                _hooked.Add(id);
                EnemyTank captured = enemy;
                enemy.Health.Died += _ => BattlefieldDestruction.AddWreck(captured.transform.position, captured.transform.rotation, captured.Kind);
            }
        }

        private IEnumerator ArtilleryStrike(int round)
        {
            int shells = round >= 70 ? 3 : round >= 40 ? 2 : 1;
            for (int i = 0; i < shells; i++)
            {
                Vector3 target = new Vector3(Random.Range(-10.2f, 10.2f), Random.Range(-4.8f, 5.0f), 0f);
                float radius = round >= 70 ? 1.30f : 1.05f;
                VisualFactory.RingPulse(target, new Color(1f, 0.16f, 0.05f), radius * 1.35f);
                yield return new WaitForSeconds(0.85f);
                VisualFactory.Explosion(target, new Color(1f, 0.20f, 0.04f), radius * 1.35f);
                BattlefieldDestruction.AddExplosionDamage(target, radius, round >= 65 ? 2 : 1);
                BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.58f, 0.05f);
                _game.KickCamera(0.25f, 0.15f);
                yield return new WaitForSeconds(0.22f);
            }
        }
    }

    public sealed class WreckDecay : MonoBehaviour
    {
        private float _dieAt;
        private float _nextSmoke;
        public void Initialize(float lifetime) { _dieAt = Time.time + Mathf.Max(8f, lifetime); _nextSmoke = Time.time + Random.Range(0.2f, 0.8f); }
        private void Update()
        {
            if (Time.time >= _dieAt) { Destroy(gameObject); return; }
            if (Time.time >= _nextSmoke)
            {
                _nextSmoke = Time.time + Random.Range(0.48f, 1.05f);
                VisualFactory.MicroBurst(transform.position + new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(-0.10f, 0.20f), 0f), new Color(0.18f, 0.16f, 0.15f), Random.Range(0.28f, 0.52f));
            }
        }
    }
}
