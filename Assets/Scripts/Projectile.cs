using UnityEngine;

namespace TankRevival
{
    public sealed class Projectile : MonoBehaviour
    {
        public Team OwnerTeam { get; private set; }
        public int Damage { get; private set; }
        private Rigidbody2D _body;
        private float _dieAt;

        public void Initialize(Vector2 direction, Team ownerTeam, int damage, float speed, Color color)
        {
            OwnerTeam = ownerTeam;
            Damage = Mathf.Max(1, damage);

            VisualFactory.Rect("Glow", transform, new Vector2(0.24f, 0.24f), new Color(color.r, color.g, color.b, 0.28f), Vector3.zero, 30);
            VisualFactory.Rect("Core", transform, new Vector2(0.12f, 0.22f), color, Vector3.zero, 31);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.095f;
            collider.isTrigger = true;

            _body = gameObject.AddComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            _body.linearVelocity = direction.normalized * speed;

            _dieAt = Time.time + 5f;
        }

        private void Update()
        {
            if (Time.time >= _dieAt) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var health = other.GetComponent<Health>();
            if (health != null)
            {
                if (health.Team == OwnerTeam && OwnerTeam != Team.Neutral) return;
                if (health.Damage(Damage, OwnerTeam))
                {
                    VisualFactory.Explosion(transform.position, OwnerTeam == Team.Player ? new Color(0.2f, 0.95f, 1f) : new Color(1f, 0.35f, 0.16f), 0.42f);
                    Destroy(gameObject);
                }
                return;
            }

            var obstacle = other.GetComponent<Obstacle>();
            if (obstacle != null)
            {
                if (obstacle.Kind == ObstacleKind.Water) return;
                obstacle.Hit(Damage, transform.position);
                Destroy(gameObject);
            }
        }
    }
}
