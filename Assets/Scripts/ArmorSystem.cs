using UnityEngine;

namespace TankRevival
{
    public enum ArmorZone
    {
        Front,
        Side,
        Rear
    }

    public enum TankModule
    {
        None,
        Engine,
        Tracks,
        Gun,
        AmmoRack
    }

    public enum ModuleCondition
    {
        Operational,
        Damaged,
        Critical,
        Disabled
    }

    /// <summary>
    /// v11.1 ARMOR FACINGS / COMPONENT DAMAGE REFORGE.
    /// Keeps Health as the only vehicle life authority while making directional armor,
    /// ammunition and four persistent modules create explicit mobility/weapon outcomes.
    /// </summary>
    public sealed class ArmorSystem : MonoBehaviour
    {
        public const int DamagedThreshold = 70;
        public const int CriticalThreshold = 35;
        public const int DisabledThreshold = 12;

        public float MobilityMultiplier { get; private set; } = 1f;
        public float ReloadMultiplier { get; private set; } = 1f;
        public float WeaponFunctionMultiplier { get; private set; } = 1f;
        public ArmorZone LastZone { get; private set; } = ArmorZone.Front;
        public bool LastCritical { get; private set; }
        public bool LastOvermatch { get; private set; }
        public TankModule LastDamagedModule { get; private set; } = TankModule.None;
        public AmmoType LastAmmo { get; private set; } = AmmoType.Basic;
        public float LastImpactAt { get; private set; } = -100f;
        public int LastResolvedDamage { get; private set; }

        public int EngineIntegrity { get; private set; } = 100;
        public int TrackIntegrity { get; private set; } = 100;
        public int GunIntegrity { get; private set; } = 100;
        public int AmmoRackIntegrity { get; private set; } = 100;

        public ModuleCondition EngineCondition => ConditionFor(EngineIntegrity);
        public ModuleCondition TrackCondition => ConditionFor(TrackIntegrity);
        public ModuleCondition GunCondition => ConditionFor(GunIntegrity);
        public ModuleCondition AmmoRackCondition => ConditionFor(AmmoRackIntegrity);

        public bool IsMobilityCritical => EngineIntegrity <= CriticalThreshold || TrackIntegrity <= CriticalThreshold;
        public bool IsWeaponCritical => GunIntegrity <= CriticalThreshold || AmmoRackIntegrity <= CriticalThreshold;
        public bool IsMobilityKilled => EngineIntegrity <= DisabledThreshold || TrackIntegrity <= DisabledThreshold;
        public bool IsWeaponDisabled => GunIntegrity <= DisabledThreshold || AmmoRackIntegrity <= DisabledThreshold;
        public bool IsAmmoRackVolatile => AmmoRackIntegrity <= CriticalThreshold;
        public int AverageIntegrity => (EngineIntegrity + TrackIntegrity + GunIntegrity + AmmoRackIntegrity) / 4;

        private float _frontDamage = 0.72f;
        private float _sideDamage = 0.94f;
        private float _rearDamage = 1.34f;
        private float _frontRicochet = 0.22f;
        private float _sideRicochet = 0.08f;
        private float _criticalChance = 0.10f;
        private bool _player;

        public static bool ConfigurationValid =>
            DamagedThreshold > CriticalThreshold &&
            CriticalThreshold > DisabledThreshold &&
            DisabledThreshold >= 8 && DisabledThreshold <= 18;

        public static ModuleCondition ConditionFor(int integrity)
        {
            if (integrity <= DisabledThreshold) return ModuleCondition.Disabled;
            if (integrity <= CriticalThreshold) return ModuleCondition.Critical;
            if (integrity <= DamagedThreshold) return ModuleCondition.Damaged;
            return ModuleCondition.Operational;
        }

        public static float ZoneDamageFactor(ArmorZone zone)
        {
            return zone == ArmorZone.Front ? 0.72f : zone == ArmorZone.Side ? 1.0f : 1.34f;
        }

        public static float AmmoPenetrationFactor(AmmoType ammo)
        {
            switch (ammo)
            {
                case AmmoType.ArmorPiercing: return 1.38f;
                case AmmoType.Plasma: return 1.62f;
                case AmmoType.EMP: return 0.72f;
                case AmmoType.Explosive: return 0.82f;
                case AmmoType.Incendiary: return 0.78f;
                default: return 1f;
            }
        }

        public void InitializePlayer()
        {
            _player = true;
            _frontDamage = 0.70f;
            _sideDamage = 0.92f;
            _rearDamage = 1.30f;
            _frontRicochet = 0.26f;
            _sideRicochet = 0.10f;
            _criticalChance = 0.08f;
            ResetModules();
        }

        public void InitializeEnemy(EnemyKind kind, int round)
        {
            _player = false;
            float progress = Mathf.Clamp01((round - 1f) / 99f);

            switch (kind)
            {
                case EnemyKind.Fast:
                    _frontDamage = 0.86f; _sideDamage = 1.00f; _rearDamage = 1.42f;
                    _frontRicochet = 0.08f; _sideRicochet = 0.02f;
                    break;
                case EnemyKind.Heavy:
                    _frontDamage = 0.54f; _sideDamage = 0.78f; _rearDamage = 1.24f;
                    _frontRicochet = 0.38f; _sideRicochet = 0.16f;
                    break;
                case EnemyKind.Siege:
                    _frontDamage = 0.50f; _sideDamage = 0.75f; _rearDamage = 1.32f;
                    _frontRicochet = 0.42f; _sideRicochet = 0.18f;
                    break;
                case EnemyKind.Elite:
                    _frontDamage = 0.58f; _sideDamage = 0.82f; _rearDamage = 1.28f;
                    _frontRicochet = 0.34f; _sideRicochet = 0.14f;
                    break;
                case EnemyKind.Supply:
                    _frontDamage = 0.78f; _sideDamage = 0.96f; _rearDamage = 1.44f;
                    _frontRicochet = 0.12f; _sideRicochet = 0.04f;
                    break;
                case EnemyKind.Boss:
                    _frontDamage = Mathf.Lerp(0.48f, 0.36f, progress);
                    _sideDamage = Mathf.Lerp(0.72f, 0.58f, progress);
                    _rearDamage = Mathf.Lerp(1.18f, 1.06f, progress);
                    _frontRicochet = Mathf.Lerp(0.42f, 0.55f, progress);
                    _sideRicochet = Mathf.Lerp(0.16f, 0.26f, progress);
                    break;
                default:
                    _frontDamage = 0.76f; _sideDamage = 0.96f; _rearDamage = 1.38f;
                    _frontRicochet = 0.18f; _sideRicochet = 0.07f;
                    break;
            }

            _criticalChance = Mathf.Lerp(0.10f, 0.16f, progress);
            ResetModules();
        }

        public int ResolveIncoming(int rawDamage, AmmoType ammo, Vector2 projectileVelocity, Vector2 hitPoint, out bool ricochet, out bool critical)
        {
            rawDamage = Mathf.Max(1, rawDamage);
            ricochet = false;
            critical = false;
            LastCritical = false;
            LastOvermatch = false;
            LastDamagedModule = TankModule.None;
            LastAmmo = ammo;
            LastImpactAt = Time.time;
            LastResolvedDamage = 0;

            Vector2 incoming = projectileVelocity.sqrMagnitude > 0.001f ? projectileVelocity.normalized : Vector2.down;
            Vector2 hullForward = transform.up;
            float dot = Vector2.Dot(incoming, hullForward);

            if (dot <= -0.48f) LastZone = ArmorZone.Front;
            else if (dot >= 0.48f) LastZone = ArmorZone.Rear;
            else LastZone = ArmorZone.Side;

            float damageMultiplier = LastZone == ArmorZone.Front ? _frontDamage : LastZone == ArmorZone.Side ? _sideDamage : _rearDamage;
            float ricochetChance = LastZone == ArmorZone.Front ? _frontRicochet : LastZone == ArmorZone.Side ? _sideRicochet : 0.01f;
            float penetration = AmmoPenetrationFactor(ammo) * (0.82f + rawDamage * 0.18f);
            float facingResistance = LastZone == ArmorZone.Front ? 1.24f : LastZone == ArmorZone.Side ? 0.94f : 0.72f;
            LastOvermatch = penetration >= facingResistance * 1.42f;

            switch (ammo)
            {
                case AmmoType.ArmorPiercing:
                    damageMultiplier *= 1.22f;
                    ricochetChance *= LastOvermatch ? 0.08f : 0.24f;
                    break;
                case AmmoType.Plasma:
                    damageMultiplier *= 1.32f;
                    ricochetChance = 0f;
                    break;
                case AmmoType.Explosive:
                    ricochetChance *= 0.32f;
                    if (LastZone == ArmorZone.Front) damageMultiplier *= 0.92f;
                    break;
                case AmmoType.EMP:
                    damageMultiplier *= 0.72f;
                    ricochetChance *= 0.20f;
                    break;
                case AmmoType.Incendiary:
                    damageMultiplier *= LastZone == ArmorZone.Rear ? 1.12f : 0.90f;
                    break;
            }

            ricochetChance /= Mathf.Max(1f, 0.72f + rawDamage * 0.28f);
            if (LastOvermatch) ricochetChance *= 0.20f;
            ricochet = Random.value < ricochetChance;
            if (ricochet)
            {
                VisualFactory.MicroBurst(hitPoint, new Color(1f, 0.82f, 0.48f), 0.66f);
                VisualFactory.RingPulse(hitPoint, new Color(1f, 0.62f, 0.18f), 0.45f);
                BattleAudio.PlayGlobal(SoundCue.Ricochet, 0.48f, 0.05f);
                return 0;
            }

            float zoneCrit = LastZone == ArmorZone.Rear ? 0.24f : LastZone == ArmorZone.Side ? 0.10f : 0f;
            float ammoCrit = ammo == AmmoType.ArmorPiercing ? 0.10f : ammo == AmmoType.Plasma ? 0.16f : ammo == AmmoType.EMP ? 0.12f : 0f;
            if (LastOvermatch) ammoCrit += 0.08f;
            critical = Random.value < Mathf.Clamp01(_criticalChance + zoneCrit + ammoCrit);
            LastCritical = critical;

            bool forcedModuleEffect = ammo == AmmoType.EMP || (ammo == AmmoType.Explosive && LastZone == ArmorZone.Side);
            if (critical || forcedModuleEffect)
            {
                LastDamagedModule = PickModuleForImpact(LastZone, ammo);
                int moduleDamage = ComputeModuleDamage(rawDamage, ammo, LastZone, critical, LastOvermatch);
                ApplyModuleDamage(LastDamagedModule, moduleDamage, hitPoint);
                if (critical)
                    damageMultiplier *= LastDamagedModule == TankModule.AmmoRack ? 1.42f : 1.25f;
            }

            if (AmmoRackIntegrity <= DisabledThreshold && LastDamagedModule == TankModule.AmmoRack)
                damageMultiplier *= 1.25f;

            LastResolvedDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * damageMultiplier));
            return LastResolvedDamage;
        }

        public int RepairModules(int amount)
        {
            amount = Mathf.Clamp(amount, 0, 100);
            if (amount <= 0) return 0;
            int before = EngineIntegrity + TrackIntegrity + GunIntegrity + AmmoRackIntegrity;
            EngineIntegrity = Mathf.Min(100, EngineIntegrity + amount);
            TrackIntegrity = Mathf.Min(100, TrackIntegrity + amount);
            GunIntegrity = Mathf.Min(100, GunIntegrity + amount);
            AmmoRackIntegrity = Mathf.Min(100, AmmoRackIntegrity + amount);
            RecalculatePerformance();
            int after = EngineIntegrity + TrackIntegrity + GunIntegrity + AmmoRackIntegrity;
            if (after > before)
            {
                VisualFactory.RingPulse(transform.position, new Color(0.20f, 1f, 0.48f), 0.92f);
                BattleAudio.PlayGlobal(SoundCue.Pickup, 0.48f, 0.04f);
            }
            return after - before;
        }

        public string CompactStatus()
        {
            return $"ENG {EngineIntegrity}%/{EngineCondition}  TRK {TrackIntegrity}%/{TrackCondition}  GUN {GunIntegrity}%/{GunCondition}  AMMO {AmmoRackIntegrity}%/{AmmoRackCondition}";
        }

        private void ResetModules()
        {
            EngineIntegrity = TrackIntegrity = GunIntegrity = AmmoRackIntegrity = 100;
            LastDamagedModule = TankModule.None;
            LastImpactAt = -100f;
            LastResolvedDamage = 0;
            LastOvermatch = false;
            RecalculatePerformance();
        }

        private TankModule PickModuleForImpact(ArmorZone zone, AmmoType ammo)
        {
            float r = Random.value;
            if (ammo == AmmoType.EMP)
                return r < 0.46f ? TankModule.Engine : r < 0.78f ? TankModule.Gun : TankModule.Tracks;
            if (ammo == AmmoType.Explosive)
                return r < 0.58f ? TankModule.Tracks : r < 0.78f ? TankModule.Gun : TankModule.Engine;
            if (ammo == AmmoType.Incendiary && zone == ArmorZone.Rear)
                return r < 0.52f ? TankModule.Engine : TankModule.AmmoRack;
            if (zone == ArmorZone.Rear)
                return r < 0.46f ? TankModule.Engine : r < 0.72f ? TankModule.AmmoRack : r < 0.88f ? TankModule.Tracks : TankModule.Gun;
            if (zone == ArmorZone.Side)
                return r < 0.34f ? TankModule.Tracks : r < 0.58f ? TankModule.AmmoRack : r < 0.79f ? TankModule.Engine : TankModule.Gun;
            return r < 0.43f ? TankModule.Gun : r < 0.69f ? TankModule.Tracks : r < 0.86f ? TankModule.AmmoRack : TankModule.Engine;
        }

        private static int ComputeModuleDamage(int rawDamage, AmmoType ammo, ArmorZone zone, bool critical, bool overmatch)
        {
            int value = 12 + rawDamage * 7;
            if (critical) value += 8;
            if (overmatch) value += 8;
            if (zone == ArmorZone.Rear) value += 6;
            switch (ammo)
            {
                case AmmoType.Plasma: value += 14; break;
                case AmmoType.ArmorPiercing: value += 8; break;
                case AmmoType.EMP: value += 12; break;
                case AmmoType.Explosive: value += 6; break;
                case AmmoType.Incendiary: value += 4; break;
            }
            return Mathf.Clamp(value, 14, 58);
        }

        private void ApplyModuleDamage(TankModule module, int amount, Vector2 hitPoint)
        {
            switch (module)
            {
                case TankModule.Engine: EngineIntegrity = Mathf.Max(0, EngineIntegrity - amount); break;
                case TankModule.Tracks: TrackIntegrity = Mathf.Max(0, TrackIntegrity - amount); break;
                case TankModule.Gun: GunIntegrity = Mathf.Max(0, GunIntegrity - amount); break;
                case TankModule.AmmoRack: AmmoRackIntegrity = Mathf.Max(0, AmmoRackIntegrity - amount); break;
            }

            RecalculatePerformance();
            Color pulse = module == TankModule.Engine ? new Color(1f, 0.34f, 0.08f) :
                          module == TankModule.Tracks ? new Color(1f, 0.65f, 0.12f) :
                          module == TankModule.Gun ? new Color(1f, 0.10f, 0.05f) : new Color(1f, 0.05f, 0.22f);
            float intensity = ConditionFor(ModuleIntegrity(module)) == ModuleCondition.Disabled ? 1.18f : 0.82f;
            VisualFactory.RingPulse(hitPoint, pulse, intensity);
            VisualFactory.MicroBurst(hitPoint, new Color(1f, 0.18f, 0.05f), intensity);
            BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.38f, 0.04f);
        }

        private int ModuleIntegrity(TankModule module)
        {
            switch (module)
            {
                case TankModule.Engine: return EngineIntegrity;
                case TankModule.Tracks: return TrackIntegrity;
                case TankModule.Gun: return GunIntegrity;
                case TankModule.AmmoRack: return AmmoRackIntegrity;
                default: return 100;
            }
        }

        private void RecalculatePerformance()
        {
            float engineFloor = _player ? 0.42f : 0.34f;
            float trackFloor = _player ? 0.38f : 0.30f;
            float engineFactor = Mathf.Lerp(engineFloor, 1f, EngineIntegrity / 100f);
            float trackFactor = Mathf.Lerp(trackFloor, 1f, TrackIntegrity / 100f);
            MobilityMultiplier = Mathf.Min(engineFactor, trackFactor);
            if (IsMobilityKilled) MobilityMultiplier = Mathf.Min(MobilityMultiplier, _player ? 0.26f : 0.20f);
            if (EngineIntegrity <= DisabledThreshold && TrackIntegrity <= DisabledThreshold)
                MobilityMultiplier = _player ? 0.12f : 0.08f;

            float gunPenalty = Mathf.Lerp(_player ? 1.85f : 2.10f, 1f, GunIntegrity / 100f);
            float rackPenalty = Mathf.Lerp(_player ? 1.55f : 1.80f, 1f, AmmoRackIntegrity / 100f);
            ReloadMultiplier = Mathf.Clamp(Mathf.Max(gunPenalty, rackPenalty), 1f, _player ? 1.85f : 2.10f);

            float gunFunction = Mathf.Lerp(0.30f, 1f, GunIntegrity / 100f);
            float rackFunction = Mathf.Lerp(0.45f, 1f, AmmoRackIntegrity / 100f);
            WeaponFunctionMultiplier = Mathf.Min(gunFunction, rackFunction);
            if (IsWeaponDisabled) WeaponFunctionMultiplier = Mathf.Min(WeaponFunctionMultiplier, 0.22f);
        }
    }
}
