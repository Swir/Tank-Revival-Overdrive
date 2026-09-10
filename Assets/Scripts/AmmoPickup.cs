using UnityEngine;

namespace TankRevival
{
    public sealed class AmmoPickup : MonoBehaviour
    {
        public AmmoType Kind { get; private set; }
        public int Amount { get; private set; }

        private float _dieAt;
        private float _phase;
        private Transform _ring;

        public void Initialize(AmmoType kind, int amount)
        {
            Kind = kind;
            Amount = Mathf.Max(1, amount);
            _phase = Random.Range(0f, 10f);
            _dieAt = Time.time + 14f;

            Color c = AmmoDatabase.Color(kind);
            VisualFactory.Disc("AmmoAura", transform, new Vector2(1.02f, 1.02f), new Color(c.r, c.g, c.b, 0.16f), Vector3.zero, 27);
            VisualFactory.Rect("AmmoCrateShadow", transform, new Vector2(0.66f, 0.58f), new Color(0f, 0f, 0f, 0.34f), new Vector3(0.05f, -0.05f, 0f), 28);
            VisualFactory.Rect("AmmoCrate", transform, new Vector2(0.62f, 0.54f), Color.Lerp(c, Color.black, 0.42f), Vector3.zero, 29);
            VisualFactory.Rect("AmmoBand", transform, new Vector2(0.14f, 0.54f), c, Vector3.zero, 30);
            VisualFactory.Rect("AmmoTop", transform, new Vector2(0.48f, 0.10f), Color.Lerp(c, Color.white, 0.28f), new Vector3(0f, 0.17f, 0f), 31);
            VisualFactory.Disc("AmmoCore", transform, new Vector2(0.16f, 0.16f), Color.white, Vector3.zero, 32);
            _ring = VisualFactory.RingObject("AmmoRing", transform, new Vector2(0.82f, 0.82f), new Color(c.r, c.g, c.b, 0.72f), Vector3.zero, 26).transform;

            var trigger = gameObject.AddComponent<CircleCollider2D>();
            trigger.radius = 0.42f;
            trigger.isTrigger = true;
        }

        private void Update()
        {
            float pulse = 1f + Mathf.Sin(Time.time * 5.5f + _phase) * 0.08f;
            transform.localScale = Vector3.one * pulse;
            if (_ring != null)
                _ring.Rotate(0f, 0f, 52f * Time.deltaTime);

            if (Time.time >= _dieAt)
                Destroy(gameObject);
        }
    }
}
