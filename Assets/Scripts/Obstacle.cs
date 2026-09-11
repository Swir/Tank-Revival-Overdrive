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
        public int MaximumHitPoints { get; private set; }

        private int _damageStage;

        public void Initialize(ObstacleKind kind, int hp = 1)
        {
            Kind = kind;
            HitPoints = Mathf.Max(1, hp);
            MaximumHitPoints = HitPoints;
            _damageStage = 0;
        }

        public bool Hit(int damage, Vector3 hitPosition)
        {
            if (Kind == ObstacleKind.Water) return false;

            int resolved = Mathf.Max(1, damage);
            bool heavy = resolved >= 2;
            WarzoneDestructionDirector.ReportStaticImpact(hitPosition, heavy);

            if (Kind == ObstacleKind.Steel)
            {
                AddSteelImpact(hitPosition, heavy);
                VisualFactory.Explosion(hitPosition, new Color(0.75f, 0.85f, 1f), heavy ? 0.62f : 0.45f);
                return true;
            }

            HitPoints -= resolved;
            VisualFactory.Explosion(hitPosition, new Color(0.95f, 0.40f, 0.12f), heavy ? 0.72f : 0.55f);
            UpdateDamageVisuals();

            if (HitPoints <= 0)
            {
                SpawnCollapseDebris();
                Destroy(gameObject);
            }

            return true;
        }

        private void UpdateDamageVisuals()
        {
            if (Kind != ObstacleKind.Brick || MaximumHitPoints <= 1 || HitPoints <= 0) return;

            float damageRatio = 1f - Mathf.Clamp01(HitPoints / (float)MaximumHitPoints);
            int stage = damageRatio >= 0.66f ? 2 : damageRatio >= 0.33f ? 1 : 0;
            if (stage <= _damageStage) return;
            _damageStage = stage;

            Color crack = stage >= 2 ? new Color(0.10f, 0.025f, 0.012f, 0.95f) : new Color(0.20f, 0.055f, 0.020f, 0.78f);
            float y = stage >= 2 ? -0.06f : 0.10f;
            VisualFactory.RectRotated("BrickCrack_" + stage, transform, new Vector2(0.07f, 0.62f), crack, new Vector3(stage == 1 ? -0.16f : 0.15f, y, 0f), stage == 1 ? -28f : 34f, 8);
            VisualFactory.Disc("BrickChip_" + stage, transform, new Vector2(0.19f, 0.14f), new Color(0.18f, 0.045f, 0.018f, 0.86f), new Vector3(stage == 1 ? 0.21f : -0.20f, stage == 1 ? -0.18f : 0.17f, 0f), 8);
        }

        private void AddSteelImpact(Vector3 hitPosition, bool heavy)
        {
            Vector3 local = transform.InverseTransformPoint(hitPosition);
            local.z = 0f;
            VisualFactory.Disc("SteelDent", transform, Vector2.one * (heavy ? 0.24f : 0.14f), new Color(0.08f, 0.10f, 0.12f, 0.92f), local, 8);
            VisualFactory.RingObject("SteelDentEdge", transform, Vector2.one * (heavy ? 0.31f : 0.20f), new Color(0.72f, 0.80f, 0.86f, 0.54f), local, 9);
        }

        private void SpawnCollapseDebris()
        {
            Transform parent = transform.parent;
            if (parent == null) return;

            for (int i = 0; i < 5; i++)
            {
                var piece = new GameObject("CollapsedBrickDebris");
                piece.transform.SetParent(parent, true);
                piece.transform.position = transform.position + new Vector3(Random.Range(-0.38f, 0.38f), Random.Range(-0.30f, 0.30f), 0f);
                piece.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));
                VisualFactory.Rect("Debris", piece.transform, new Vector2(Random.Range(0.10f, 0.24f), Random.Range(0.07f, 0.16f)), new Color(0.32f, 0.075f, 0.026f, 0.90f), Vector3.zero, -4);
                Destroy(piece, 18f);
            }
        }
    }
}
