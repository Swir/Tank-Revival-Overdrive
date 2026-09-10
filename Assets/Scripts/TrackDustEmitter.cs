using UnityEngine;

namespace TankRevival
{
    public sealed class TrackDustEmitter : MonoBehaviour
    {
        private Vector3 _lastPosition;
        private float _distanceAccumulator;
        private float _nextDust;

        private void Start()
        {
            _lastPosition = transform.position;
            _nextDust = Time.time + Random.Range(0.02f, 0.12f);
        }

        private void Update()
        {
            Vector3 current = transform.position;
            float distance = Vector3.Distance(current, _lastPosition);
            _lastPosition = current;
            if (distance <= 0.001f) return;

            _distanceAccumulator += distance;
            if (_distanceAccumulator < 0.20f || Time.time < _nextDust) return;
            _distanceAccumulator = 0f;
            _nextDust = Time.time + 0.035f;

            Vector2 jitter = Random.insideUnitCircle * 0.09f;
            SpawnTrackMark(current + (Vector3)jitter + transform.TransformVector(new Vector3(-0.26f, -0.22f, 0f)));
            SpawnTrackMark(current - (Vector3)jitter + transform.TransformVector(new Vector3(0.26f, -0.22f, 0f)));

            if (Random.value < 0.42f)
            {
                var dust = new GameObject("TreadDust");
                dust.transform.position = current + (Vector3)Random.insideUnitCircle * 0.15f;
                var disc = VisualFactory.Disc("Dust", dust.transform, Vector2.one * Random.Range(0.10f, 0.20f), new Color(0.30f, 0.28f, 0.23f, 0.20f), Vector3.zero, 1);
                var fade = dust.AddComponent<AfterglowFx>();
                fade.Renderer = disc.GetComponent<SpriteRenderer>();
            }
        }

        private static void SpawnTrackMark(Vector3 position)
        {
            var mark = new GameObject("TrackMark");
            mark.transform.position = position;
            var rect = VisualFactory.Rect("Mark", mark.transform, new Vector2(0.08f, 0.18f), new Color(0.02f, 0.025f, 0.025f, 0.16f), Vector3.zero, -50);
            var fade = mark.AddComponent<GroundMarkFade>();
            fade.Renderer = rect.GetComponent<SpriteRenderer>();
        }
    }

    public sealed class GroundMarkFade : MonoBehaviour
    {
        public SpriteRenderer Renderer;
        private float _age;
        private const float Life = 2.4f;

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Life);
            if (Renderer != null)
            {
                var c = Renderer.color;
                c.a = (1f - t) * 0.16f;
                Renderer.color = c;
            }
            if (_age >= Life) Destroy(gameObject);
        }
    }
}
