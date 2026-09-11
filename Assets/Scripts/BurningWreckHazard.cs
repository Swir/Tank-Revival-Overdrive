using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Heavy destroyed armor briefly becomes a neutral battlefield hazard.
    /// It can hurt mobile units from either side, but never damages the Orzelek core directly.
    /// </summary>
    public sealed class BurningWreckHazard : MonoBehaviour
    {
        private float _dieAt;
        private float _nextPulse;
        private float _radius;
        private float _nextVisual;

        public void Initialize(float lifetime, float radius)
        {
            _dieAt = Time.time + Mathf.Max(2f, lifetime);
            _radius = Mathf.Clamp(radius, 0.55f, 1.25f);
            _nextPulse = Time.time + 0.65f;
            _nextVisual = Time.time;
        }

        private void Update()
        {
            if (Time.time >= _dieAt)
            {
                Destroy(this);
                return;
            }

            if (Time.time >= _nextVisual)
            {
                _nextVisual = Time.time + 0.22f;
                Vector3 p = transform.position + new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(-0.08f, 0.24f), 0f);
                VisualFactory.MicroBurst(p, new Color(1f, 0.24f, 0.035f), Random.Range(0.34f, 0.52f));
            }

            if (Time.time < _nextPulse) return;
            _nextPulse = Time.time + 0.78f;

            var hits = Physics2D.OverlapCircleAll(transform.position, _radius);
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                var health = hit.GetComponent<Health>();
                if (health == null || health.IsDead) continue;

                string upper = health.gameObject.name.ToUpperInvariant();
                if (upper.Contains("ORZELEK")) continue;

                health.Damage(1, Team.Neutral);
            }

            VisualFactory.RingPulse(transform.position, new Color(1f, 0.16f, 0.03f, 0.70f), _radius * 0.72f);
        }
    }
}
