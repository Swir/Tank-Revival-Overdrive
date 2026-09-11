using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Adds bounded mesh-based smoke/fire to damaged vehicles and battlefield wrecks.
    /// It supplements the legacy presentation without touching damage, status or wreck lifetime rules.
    /// </summary>
    public sealed class BattlefieldSmoke3DDirector : MonoBehaviour
    {
        private float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<BattlefieldSmoke3DDirector>() != null) return;
            var go = new GameObject("BattlefieldSmoke3DDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<BattlefieldSmoke3DDirector>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.42f;

            Health[] health = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < health.Length; i++)
            {
                Health h = health[i];
                if (h == null || (h.GetComponent<PlayerTank>() == null && h.GetComponent<EnemyTank>() == null)) continue;
                if (h.GetComponent<DamageSmoke3D>() == null)
                    h.gameObject.AddComponent<DamageSmoke3D>().Initialize(h);
            }

            WreckDecay[] wrecks = FindObjectsByType<WreckDecay>(FindObjectsSortMode.None);
            for (int i = 0; i < wrecks.Length; i++)
            {
                WreckDecay wreck = wrecks[i];
                if (wreck == null || wreck.GetComponent<WreckSmoke3D>() != null) continue;
                wreck.gameObject.AddComponent<WreckSmoke3D>().Initialize();
            }
        }
    }

    public sealed class DamageSmoke3D : MonoBehaviour
    {
        private Health _health;
        private float _nextSmoke;
        private bool _ready;

        public void Initialize(Health health)
        {
            _health = health;
            _ready = true;
            _nextSmoke = Time.time + Random.Range(0.18f, 0.50f);
        }

        private void Update()
        {
            if (!_ready || _health == null || _health.IsDead || _health.Maximum <= 0) return;
            float ratio = _health.Current / (float)_health.Maximum;
            if (ratio > 0.58f || Time.time < _nextSmoke) return;

            float damage = 1f - ratio;
            _nextSmoke = Time.time + Mathf.Lerp(0.60f, 0.22f, damage) * Random.Range(0.82f, 1.18f);
            Vector3 point = transform.position + new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(-0.16f, 0.12f), 0f);
            SmokePuff3D.Spawn(point, damage, ratio <= 0.30f);
        }
    }

    public sealed class WreckSmoke3D : MonoBehaviour
    {
        private float _nextSmoke;
        private float _nextEmber;
        private bool _ready;

        public void Initialize()
        {
            _ready = true;
            _nextSmoke = Time.time + Random.Range(0.10f, 0.40f);
            _nextEmber = Time.time + Random.Range(0.25f, 0.75f);
        }

        private void Update()
        {
            if (!_ready) return;
            if (Time.time >= _nextSmoke)
            {
                _nextSmoke = Time.time + Random.Range(0.40f, 0.82f);
                Vector3 point = transform.position + new Vector3(Random.Range(-0.22f, 0.22f), Random.Range(-0.15f, 0.16f), 0f);
                SmokePuff3D.Spawn(point, Random.Range(0.52f, 0.90f), true);
            }

            if (Time.time >= _nextEmber)
            {
                _nextEmber = Time.time + Random.Range(0.48f, 1.05f);
                Ember3D.Spawn(transform.position + new Vector3(Random.Range(-0.20f, 0.20f), Random.Range(-0.15f, 0.16f), 0f));
            }
        }
    }

    public sealed class SmokePuff3D : MonoBehaviour
    {
        private float _born;
        private float _life;
        private Vector3 _velocity;
        private Vector3 _baseScale;
        private bool _released;

        public static void Spawn(Vector3 position, float intensity, bool burning)
        {
            if (!CombatFX3DDirector.TryReserveFx()) return;
            var root = new GameObject("SmokePuff3D");
            root.transform.position = new Vector3(position.x, position.y, -0.30f);
            root.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(-8f, 8f), Random.Range(0f, 180f));

            float size = Mathf.Lerp(0.16f, 0.34f, Mathf.Clamp01(intensity));
            Color smoke = Color.Lerp(new Color(0.24f, 0.25f, 0.27f), new Color(0.075f, 0.070f, 0.068f), Mathf.Clamp01(intensity));
            Runtime3DFactory.Cylinder("SmokeCore3D", root.transform, Vector3.zero, size, 0.10f, smoke, 0.01f, 0.04f);
            Runtime3DFactory.Cylinder("SmokeLobe3D", root.transform, new Vector3(size * 0.32f, size * 0.18f, -0.05f), size * 0.72f, 0.08f,
                Color.Lerp(smoke, Color.white, 0.08f), 0.01f, 0.04f);
            if (burning)
                Runtime3DFactory.Cylinder("FireCore3D", root.transform, new Vector3(-size * 0.18f, -size * 0.08f, -0.08f), size * 0.36f, 0.05f,
                    new Color(1f, 0.18f, 0.025f), 0.01f, 0.70f);

            var puff = root.AddComponent<SmokePuff3D>();
            puff._born = Time.unscaledTime;
            puff._life = Random.Range(0.72f, 1.18f);
            puff._velocity = new Vector3(Random.Range(-0.10f, 0.10f), Random.Range(0.08f, 0.24f), -Random.Range(0.18f, 0.36f));
            puff._baseScale = Vector3.one;
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _born) / Mathf.Max(0.01f, _life));
            transform.position += _velocity * Time.unscaledDeltaTime;
            transform.Rotate(7f * Time.unscaledDeltaTime, 11f * Time.unscaledDeltaTime, 22f * Time.unscaledDeltaTime, Space.Self);
            float scale = Mathf.Lerp(0.62f, 1.85f, t) * Mathf.Lerp(1f, 0.52f, t);
            transform.localScale = _baseScale * scale;
            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_released) return;
            _released = true;
            CombatFX3DDirector.ReleaseFx();
        }
    }

    public sealed class Ember3D : MonoBehaviour
    {
        private float _born;
        private float _life;
        private Vector3 _velocity;
        private bool _released;

        public static void Spawn(Vector3 position)
        {
            if (!CombatFX3DDirector.TryReserveFx()) return;
            var root = new GameObject("Ember3D");
            root.transform.position = new Vector3(position.x, position.y, -0.42f);
            Runtime3DFactory.Box("EmberMesh3D", root.transform, Vector3.zero,
                new Vector3(0.035f, 0.09f, 0.025f), new Color(1f, 0.18f, 0.025f), 0.02f, 0.82f);
            var ember = root.AddComponent<Ember3D>();
            ember._born = Time.unscaledTime;
            ember._life = Random.Range(0.36f, 0.65f);
            ember._velocity = new Vector3(Random.Range(-0.22f, 0.22f), Random.Range(0.25f, 0.70f), -Random.Range(0.30f, 0.85f));
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _born) / Mathf.Max(0.01f, _life));
            transform.position += _velocity * Time.unscaledDeltaTime;
            transform.Rotate(0f, 0f, 420f * Time.unscaledDeltaTime, Space.Self);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.08f, t);
            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_released) return;
            _released = true;
            CombatFX3DDirector.ReleaseFx();
        }
    }
}
