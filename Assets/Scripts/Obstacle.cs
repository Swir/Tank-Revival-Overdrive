using UnityEngine;

namespace TankRevival
{
    public enum ObstacleKind
    {
        Brick,
        Steel,
        Water
    }

    public sealed class Obstacle : MonoBehaviour
    {
        public ObstacleKind Kind { get; private set; }
        public int HitPoints { get; private set; }

        public void Initialize(ObstacleKind kind, int hp = 1)
        {
            Kind = kind;
            HitPoints = Mathf.Max(1, hp);
        }

        public bool Hit(int damage, Vector3 hitPosition)
        {
            if (Kind == ObstacleKind.Water) return false;

            if (Kind == ObstacleKind.Steel)
            {
                VisualFactory.Explosion(hitPosition, new Color(0.75f, 0.85f, 1f), 0.45f);
                return true;
            }

            HitPoints -= Mathf.Max(1, damage);
            VisualFactory.Explosion(hitPosition, new Color(0.95f, 0.40f, 0.12f), 0.55f);

            if (HitPoints <= 0)
            {
                Destroy(gameObject);
            }

            return true;
        }
    }
}
