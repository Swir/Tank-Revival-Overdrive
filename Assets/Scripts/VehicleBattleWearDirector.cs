using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Adds bounded track marks and progressive battle-wear cues to real combat vehicles.
    /// This is presentation-only: Health/Rigidbody2D remain authoritative.
    /// </summary>
    [DefaultExecutionOrder(2500)]
    public sealed class VehicleBattleWearDirector : MonoBehaviour
    {
        private readonly HashSet<Health> _bound = new HashSet<Health>();
        private float _scanAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<VehicleBattleWearDirector>() != null) return;
            var go = new GameObject("VehicleBattleWearDirector_v4_4");
            DontDestroyOnLoad(go);
            go.AddComponent<VehicleBattleWearDirector>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _scanAt) return;
            _scanAt = Time.unscaledTime + 0.55f;

            Health[] all = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Health health = all[i];
                if (health == null || !_bound.Add(health)) continue;
                bool vehicle = health.GetComponent<PlayerTank>() != null ||
                               health.GetComponent<EnemyTank>() != null ||
                               health.GetComponent<FriendlySupportUnit>() != null;
                if (!vehicle) continue;
                if (health.GetComponent<VehicleBattleWearEmitter>() == null)
                    health.gameObject.AddComponent<VehicleBattleWearEmitter>().Initialize(health);
            }
            _bound.RemoveWhere(h => h == null);
        }
    }

    public sealed class VehicleBattleWearEmitter : MonoBehaviour
    {
        private const int MaxMarks = 180;
        private static readonly Queue<GameObject> Marks = new Queue<GameObject>();

        private Health _health;
        private Rigidbody2D _body;
        private Vector3 _lastMarkPosition;
        private float _nextMarkAt;
        private float _nextDamageSpark;
        private float _damagePulse;
        private bool _initialized;

        public void Initialize(Health health)
        {
            if (_initialized) return;
            _initialized = true;
            _health = health;
            _body = GetComponent<Rigidbody2D>();
            _lastMarkPosition = transform.position;
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Damaged += OnDamaged;
            }
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        private void OnDamaged(Health health, int amount)
        {
            if (amount <= 0) return;
            _damagePulse = Mathf.Clamp01(_damagePulse + 0.45f + amount * 0.06f);
            SpawnDamageSpark(Mathf.Clamp(0.55f + amount * 0.08f, 0.6f, 1.45f));
        }

        private void Update()
        {
            if (!_initialized || _health == null || _health.IsDead) return;
            _damagePulse = Mathf.MoveTowards(_damagePulse, 0f, Time.unscaledDeltaTime * 0.65f);

            Vector3 current = transform.position;
            Vector2 velocity = _body != null ? _body.linearVelocity : Vector2.zero;
            float speed = velocity.magnitude;
            float moved = Vector2.Distance(new Vector2(current.x, current.y), new Vector2(_lastMarkPosition.x, _lastMarkPosition.y));

            if (speed > 0.55f && moved > 0.36f && Time.unscaledTime >= _nextMarkAt)
            {
                _nextMarkAt = Time.unscaledTime + 0.055f;
                SpawnTrackPair(current, velocity);
                _lastMarkPosition = current;
            }

            if (_health.Max <= 0) return;
            float ratio = _health.Current / (float)_health.Max;
            if (ratio < 0.46f && Time.unscaledTime >= _nextDamageSpark)
            {
                float interval = ratio < 0.25f ? 0.62f : 1.15f;
                _nextDamageSpark = Time.unscaledTime + interval * Random.Range(0.78f, 1.28f);
                SpawnDamageSpark(ratio < 0.25f ? 1.05f : 0.72f);
            }
        }

        private void SpawnTrackPair(Vector3 position, Vector2 velocity)
        {
            Vector2 dir = velocity.sqrMagnitude > 0.02f ? velocity.normalized : (Vector2)transform.up;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            Color track = new Color(0.055f, 0.048f, 0.040f);

            SpawnMark(position + (Vector3)(side * 0.24f), angle, track);
            SpawnMark(position - (Vector3)(side * 0.24f), angle, track);
        }

        private static void SpawnMark(Vector3 position, float angle, Color tint)
        {
            while (Marks.Count >= MaxMarks)
            {
                GameObject old = Marks.Dequeue();
                if (old != null) Destroy(old);
            }

            var root = new GameObject("TrackMark_v4_4");
            root.transform.position = new Vector3(position.x, position.y, 0f);
            root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            Runtime3DFactory.Box("TrackImprint3D", root.transform, new Vector3(0f, 0f, 0.235f),
                new Vector3(0.13f, 0.34f, 0.012f), tint, 0.72f, 0.03f);
            var life = root.AddComponent<BattleWearLifetime>();
            life.Initialize(13f + Random.Range(-2f, 4f));
            Marks.Enqueue(root);
        }

        private void SpawnDamageSpark(float scale)
        {
            Vector3 position = transform.position + new Vector3(Random.Range(-0.32f, 0.32f), Random.Range(-0.30f, 0.30f), 0f);
            Color hot = Color.Lerp(new Color(1f, 0.16f, 0.015f), new Color(1f, 0.86f, 0.28f), Random.Range(0.25f, 0.75f));
            var root = new GameObject("ArmorStressSpark_v4_4");
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));

            Runtime3DFactory.Cylinder("SparkCore3D", root.transform, new Vector3(0f, 0f, -0.54f),
                0.055f * scale, 0.03f, Color.Lerp(hot, Color.white, 0.45f), 0.02f, 0.92f);
            for (int i = 0; i < 3; i++)
            {
                float angle = Random.Range(0f, 360f);
                GameObject ray = Runtime3DFactory.Box("SparkRay3D", root.transform, Vector3.zero,
                    new Vector3(0.025f, Random.Range(0.20f, 0.42f) * scale, 0.018f), hot, 0.02f, 0.80f);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            root.AddComponent<TransientScale3D>().Initialize(0.16f, 0.55f, 1.25f, 160f, true);
        }
    }

    public sealed class BattleWearLifetime : MonoBehaviour
    {
        private float _dieAt;
        private float _startFadeAt;
        private Renderer[] _renderers;

        public void Initialize(float lifetime)
        {
            float life = Mathf.Max(2f, lifetime);
            _dieAt = Time.unscaledTime + life;
            _startFadeAt = _dieAt - Mathf.Min(4f, life * 0.35f);
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void Update()
        {
            if (Time.unscaledTime >= _dieAt)
            {
                Destroy(gameObject);
                return;
            }

            if (_renderers == null || Time.unscaledTime < _startFadeAt) return;
            float alpha = Mathf.InverseLerp(_dieAt, _startFadeAt, Time.unscaledTime);
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null || r.material == null) continue;
                Color c = r.material.color;
                c.a = Mathf.Min(c.a, alpha * 0.72f);
                r.material.color = c;
            }
        }
    }
}
