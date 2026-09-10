using UnityEngine;

namespace TankRevival
{
    public enum PowerUpKind
    {
        Repair,
        RapidFire,
        PowerShot,
        Speed,
        Shield
    }

    public sealed class PowerUp : MonoBehaviour
    {
        public PowerUpKind Kind { get; private set; }
        private float _dieAt;
        private float _phase;

        public void Initialize(PowerUpKind kind)
        {
            Kind = kind;
            _phase = Random.Range(0f, 10f);

            Color color = kind switch
            {
                PowerUpKind.Repair => new Color(0.30f, 1f, 0.35f),
                PowerUpKind.RapidFire => new Color(1f, 0.75f, 0.12f),
                PowerUpKind.PowerShot => new Color(1f, 0.22f, 0.20f),
                PowerUpKind.Speed => new Color(0.25f, 0.90f, 1f),
                _ => new Color(0.72f, 0.42f, 1f)
            };

            VisualFactory.Rect("Aura", transform, new Vector2(0.88f, 0.88f), new Color(color.r, color.g, color.b, 0.20f), Vector3.zero, 20);
            VisualFactory.Rect("Core", transform, new Vector2(0.56f, 0.56f), color, Vector3.zero, 21);
            VisualFactory.Rect("Glint", transform, new Vector2(0.18f, 0.18f), Color.white, new Vector3(-0.12f, 0.12f, 0f), 22);

            var trigger = gameObject.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(0.72f, 0.72f);
            trigger.isTrigger = true;
            _dieAt = Time.time + 10f;
        }

        private void Update()
        {
            float pulse = 1f + Mathf.Sin(Time.time * 6f + _phase) * 0.10f;
            transform.localScale = Vector3.one * pulse;
            transform.Rotate(0f, 0f, 42f * Time.deltaTime);
            if (Time.time >= _dieAt) Destroy(gameObject);
        }
    }
}
