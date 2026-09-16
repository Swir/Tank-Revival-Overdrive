using UnityEngine;

namespace TankRevival
{
    public enum ObstacleKind
    {
        Brick,
        Steel,
        Water
    }

    public enum ObstacleImpactResult
    {
        Ignored,
        Deflected,
        Damaged,
        Breached
    }

    /// <summary>
    /// Authoritative static-cover durability path. v11.8 keeps every projectile/artillery impact
    /// flowing through this component while adding ammo-aware Brick/Steel structural response.
    /// </summary>
    public sealed class Obstacle : MonoBehaviour
    {
        public ObstacleKind Kind { get; private set; }
        public int HitPoints { get; private set; }
        public int MaximumHitPoints { get; private set; }
        public float Integrity01 => Kind == ObstacleKind.Water ? 1f : MaximumHitPoints > 0 ? Mathf.Clamp01(HitPoints / (float)MaximumHitPoints) : 0f;
        public bool IsBreachable => Kind == ObstacleKind.Brick || Kind == ObstacleKind.Steel;

        private int _damageStage;

        public void Initialize(ObstacleKind kind, int hp = 1)
        {
            Kind = kind;
            int requested = Mathf.Max(1, hp);
            // Steel used to be infinitely hard. v11.8 turns it into fortified cover with a bounded
            // integrity pool that only heavy ordnance can materially reduce.
            HitPoints = kind == ObstacleKind.Steel ? Mathf.Max(6, requested) : requested;
            MaximumHitPoints = HitPoints;
            _damageStage = 0;
        }

        /// <summary>
        /// Legacy impact entry point retained for older terrain/destruction systems. Standard damage
        /// preserves the previous rule that Steel deflects ordinary fire.
        /// </summary>
        public bool Hit(int damage, Vector3 hitPosition)
        {
            return ResolveProjectileImpact(damage, hitPosition, AmmoType.Basic, Team.Neutral, false) != ObstacleImpactResult.Ignored;
        }

        public ObstacleImpactResult ResolveProjectileImpact(int damage, Vector3 hitPosition, AmmoType ammo, Team attackerTeam, bool splash)
        {
            if (Kind == ObstacleKind.Water) return ObstacleImpactResult.Ignored;

            int raw = Mathf.Max(1, damage);
            int structural = StructuralDamageFor(Kind, ammo, raw, splash);
            bool heavy = IsHeavyOrdnance(ammo) || structural >= 2;
            WarzoneDestructionDirector.ReportStaticImpact(hitPosition, heavy);

            if (structural <= 0)
            {
                if (Kind == ObstacleKind.Steel && MassBattleFxBudget.TryConsumeMicroFx(false))
                {
                    AddSteelImpact(hitPosition, false);
                    VisualFactory.Explosion(hitPosition, new Color(0.68f, 0.80f, 0.92f), 0.38f);
                }
                DestructionReforgeDirector.Instance?.ReportObstacleImpact(hitPosition, Kind, false, heavy);
                return ObstacleImpactResult.Deflected;
            }

            HitPoints = Mathf.Max(0, HitPoints - structural);
            bool breached = HitPoints <= 0;

            if (MassBattleFxBudget.TryConsumeMicroFx(heavy || breached))
            {
                if (Kind == ObstacleKind.Steel)
                {
                    AddSteelImpact(hitPosition, heavy);
                    VisualFactory.Explosion(hitPosition, new Color(0.72f, 0.82f, 0.94f), heavy ? 0.72f : 0.48f);
                }
                else
                {
                    VisualFactory.Explosion(hitPosition, new Color(0.95f, 0.40f, 0.12f), heavy ? 0.78f : 0.55f);
                }
            }

            UpdateDamageVisuals();
            DestructionReforgeDirector.Instance?.ReportObstacleImpact(hitPosition, Kind, breached, heavy);

            if (!breached) return ObstacleImpactResult.Damaged;

            ReactiveCoverBreachDirector.ReportBreach(transform.position, attackerTeam, ammo, Kind);
            SpawnCollapseDebris();
            Destroy(gameObject);
            return ObstacleImpactResult.Breached;
        }

        /// <summary>
        /// Pure profile resolver used by gameplay and packaged qualification. EMP is non-structural,
        /// normal/twin/incendiary rounds cannot chew through Steel, and AP/HE/Plasma are deliberate
        /// breakthrough tools. Splash HE remains useful but is weaker than a direct hit.
        /// </summary>
        public static int StructuralDamageFor(ObstacleKind kind, AmmoType ammo, int rawDamage, bool splash)
        {
            if (kind == ObstacleKind.Water) return 0;
            int raw = Mathf.Max(1, rawDamage);
            float multiplier;

            if (kind == ObstacleKind.Steel)
            {
                switch (ammo)
                {
                    case AmmoType.ArmorPiercing: multiplier = 1.00f; break;
                    case AmmoType.Explosive: multiplier = 1.75f; break;
                    case AmmoType.Plasma: multiplier = 2.10f; break;
                    default: return 0;
                }
            }
            else
            {
                switch (ammo)
                {
                    case AmmoType.ArmorPiercing: multiplier = 1.30f; break;
                    case AmmoType.Explosive: multiplier = 1.65f; break;
                    case AmmoType.Plasma: multiplier = 2.00f; break;
                    case AmmoType.Incendiary: multiplier = 0.55f; break;
                    case AmmoType.EMP: return 0;
                    case AmmoType.Twin: multiplier = 0.90f; break;
                    default: multiplier = 1.00f; break;
                }
            }

            if (splash) multiplier *= 0.72f;
            return Mathf.Max(1, Mathf.CeilToInt(raw * multiplier));
        }

        public static bool IsHeavyOrdnance(AmmoType ammo)
        {
            return ammo == AmmoType.ArmorPiercing || ammo == AmmoType.Explosive || ammo == AmmoType.Plasma;
        }

        private void UpdateDamageVisuals()
        {
            if (MaximumHitPoints <= 1 || HitPoints <= 0) return;

            float damageRatio = 1f - Mathf.Clamp01(HitPoints / (float)MaximumHitPoints);
            int stage = damageRatio >= 0.66f ? 2 : damageRatio >= 0.33f ? 1 : 0;
            if (stage <= _damageStage) return;
            _damageStage = stage;
            if (!MassBattleFxBudget.TryConsumeMicroFx(stage >= 2)) return;

            if (Kind == ObstacleKind.Brick)
            {
                Color crack = stage >= 2 ? new Color(0.10f, 0.025f, 0.012f, 0.95f) : new Color(0.20f, 0.055f, 0.020f, 0.78f);
                float y = stage >= 2 ? -0.06f : 0.10f;
                VisualFactory.RectRotated("BrickCrack_" + stage, transform, new Vector2(0.07f, 0.62f), crack, new Vector3(stage == 1 ? -0.16f : 0.15f, y, 0f), stage == 1 ? -28f : 34f, 8);
                VisualFactory.Disc("BrickChip_" + stage, transform, new Vector2(0.19f, 0.14f), new Color(0.18f, 0.045f, 0.018f, 0.86f), new Vector3(stage == 1 ? 0.21f : -0.20f, stage == 1 ? -0.18f : 0.17f, 0f), 8);
                return;
            }

            if (Kind == ObstacleKind.Steel)
            {
                Color scorch = stage >= 2 ? new Color(0.08f, 0.09f, 0.11f, 0.94f) : new Color(0.16f, 0.18f, 0.21f, 0.72f);
                VisualFactory.RectRotated("SteelFracture_" + stage, transform, new Vector2(0.055f, stage >= 2 ? 0.78f : 0.55f), scorch, new Vector3(stage == 1 ? 0.17f : -0.14f, 0f, 0f), stage == 1 ? 22f : -31f, 9);
                VisualFactory.RingObject("SteelIntegrity_" + stage, transform, Vector2.one * (stage >= 2 ? 0.48f : 0.34f), new Color(0.84f, 0.38f, 0.12f, 0.46f), new Vector3(stage == 1 ? -0.18f : 0.16f, stage == 1 ? 0.13f : -0.11f, 0f), 9);
            }
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
            if (!MassBattleFxBudget.TryConsumeMicroFx(true)) return;
            Transform parent = transform.parent;
            if (parent == null) return;

            bool steel = Kind == ObstacleKind.Steel;
            int pieces = steel ? 7 : 5;
            Color debrisColor = steel ? new Color(0.28f, 0.32f, 0.37f, 0.92f) : new Color(0.32f, 0.075f, 0.026f, 0.90f);
            for (int i = 0; i < pieces; i++)
            {
                var piece = new GameObject(steel ? "CollapsedSteelDebris" : "CollapsedBrickDebris");
                piece.transform.SetParent(parent, true);
                piece.transform.position = transform.position + new Vector3(Random.Range(-0.38f, 0.38f), Random.Range(-0.30f, 0.30f), 0f);
                piece.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));
                VisualFactory.Rect("Debris", piece.transform, new Vector2(Random.Range(0.10f, 0.24f), Random.Range(0.07f, 0.16f)), debrisColor, Vector3.zero, -4);
                Destroy(piece, 18f);
            }
        }
    }
}
