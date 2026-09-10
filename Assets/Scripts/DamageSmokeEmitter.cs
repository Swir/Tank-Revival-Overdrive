using UnityEngine;

namespace TankRevival
{
    public sealed class DamageSmokeEmitter : MonoBehaviour
    {
        private Health _health;
        private float _nextSmoke;
        private float _nextSpark;

        private void Start()
        {
            _health = GetComponent<Health>();
            _nextSmoke = Time.time + Random.Range(0.1f, 0.4f);
            _nextSpark = Time.time + Random.Range(0.3f, 0.8f);
        }

        private void Update()
        {
            if (_health == null || _health.IsDead || _health.Maximum <= 1) return;
            float ratio = _health.Current / (float)_health.Maximum;
            if (ratio > 0.58f) return;

            if (Time.time >= _nextSmoke)
            {
                _nextSmoke = Time.time + Mathf.Lerp(0.16f, 0.42f, ratio);
                SpawnSmoke(ratio);
            }

            if (ratio <= 0.34f && Time.time >= _nextSpark)
            {
                _nextSpark = Time.time + Random.Range(0.28f, 0.65f);
                Vector3 pos = transform.position + (Vector3)Random.insideUnitCircle * 0.24f;
                VisualFactory.MicroBurst(pos, new Color(1f, 0.46f, 0.08f), Random.Range(0.20f, 0.34f));
            }
        }

        private void SpawnSmoke(float healthRatio)
        {
            var go = new GameObject("DamageSmoke");
            go.transform.position = transform.position + new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(-0.12f, 0.18f), 0f);
            float size = Random.Range(0.18f, 0.32f) * Mathf.Lerp(1.35f, 0.85f, healthRatio);
            var disc = VisualFactory.Disc("SmokePuff", go.transform, Vector2.one * size, new Color(0.10f, 0.11f, 0.12f, 0.30f), Vector3.zero, 40);
            var puff = go.AddComponent<SmokePuffFx>();
            puff.Renderer = disc.GetComponent<SpriteRenderer>();
            puff.Drift = new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(0.16f, 0.34f));
        }
    }

    public sealed class SmokePuffFx : MonoBehaviour
    {
        public SpriteRenderer Renderer;
        public Vector2 Drift;
        private float _age;
        private const float Life = 0.85f;

        private void Update()
        {
            _age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_age / Life);
            transform.position += (Vector3)(Drift * Time.unscaledDeltaTime);
            transform.localScale *= 1f + Time.unscaledDeltaTime * 0.52f;
            if (Renderer != null)
            {
                var c = Renderer.color;
                c.a = (1f - t) * 0.30f;
                Renderer.color = c;
            }
            if (_age >= Life) Destroy(gameObject);
        }
    }
}
