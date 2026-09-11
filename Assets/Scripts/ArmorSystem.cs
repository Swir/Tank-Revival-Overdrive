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

    /// <summary>
    /// v1.7 ARMORED WARFARE REFORGED
    /// Directional armor, ricochets and persistent four-module damage model shared by
    /// player and enemy armor. Critical hits now damage a concrete module and feed
    /// directly into mobility/reload performance instead of applying an opaque penalty.
    /// </summary>
    public sealed class ArmorSystem : MonoBehaviour
    {
        public float MobilityMultiplier { get; private set; } = 1f;
        public float ReloadMultiplier { get; private set; } = 1f;
        public ArmorZone LastZone { get; private set; } = ArmorZone.Front;
        public bool LastCritical { get; private set; }
        public TankModule LastDamagedModule { get; private set; } = TankModule.None;

        public int EngineIntegrity { get; private set; } = 100;
        public int TrackIntegrity { get; private set; } = 100;
        public int GunIntegrity { get; private set; } = 100;
        public int AmmoRackIntegrity { get; private set; } = 100;
        public bool IsMobilityCritical => EngineIntegrity <= 35 || TrackIntegrity <= 35;
        public bool IsWeaponCritical => GunIntegrity <= 35 || AmmoRackIntegrity <= 35;
        public int AverageIntegrity => (EngineIntegrity + TrackIntegrity + GunIntegrity + AmmoRackIntegrity) / 4;

        private float _frontDamage = 0.72f;
        private float _sideDamage = 0.94f;
        private float _rearDamage = 1.34f;
        private float _frontRicochet = 0.22f;
        private float _sideRicochet = 0.08f;
        private float _criticalChance = 0.10f;
        private bool _player;

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
            LastDamagedModule = TankModule.None;

            Vector2 incoming = projectileVelocity.sqrMagnitude > 0.001f ? projectileVelocity.normalized : Vector2.down;
            Vector2 hullForward = transform.up;
            float dot = Vector2.Dot(incoming, hullForward);

            if (dot <= -0.48f) LastZone = ArmorZone.Front;
            else if (dot >= 0.48f) LastZone = ArmorZone.Rear;
            else LastZone = ArmorZone.Side;

            float damageMultiplier = LastZone == ArmorZone.Front ? _frontDamage : LastZone == ArmorZone.Side ? _sideDamage : _rearDamage;
            float ricochetChance = LastZone == ArmorZone.Front ? _frontRicochet : LastZone == ArmorZone.Side ? _sideRicochet : 0.01f;

            if (ammo == AmmoType.ArmorPiercing)
            {
                damageMultiplier *= 1.22f;
                ricochetChance *= 0.24f;
            }
            else if (ammo == AmmoType.Plasma)
            {
                damageMultiplier *= 1.32f;
                ricochetChance = 0f;
            }
            else if (ammo == AmmoType.Explosive)
            {
                ricochetChance *= 0.32f;
            }

            ricochetChance /= Mathf.Max(1f, 0.72f + rawDamage * 0.28f);
            ricochet = Random.value < ricochetChance;
            if (ricochet)
            {
                VisualFactory.MicroBurst(hitPoint, new Color(1f, 0.82f, 0.48f), 0.66f);
                VisualFactory.RingPulse(hitPoint, new Color(1f, 0.62f, 0.18f), 0.45f);
                BattleAudio.PlayGlobal(SoundCue.Ricochet, 0.48f, 0.05f);
                return 0;
            }

            float zoneCrit = LastZone == ArmorZone.Rear ? 0.24f : LastZone == ArmorZone.Side ? 0.10f : 0f;
            float ammoCrit = ammo == AmmoType.ArmorPiercing ? 0.10f : ammo == AmmoType.Plasma ? 0.16f : 0f;
            critical = Random.value < Mathf.Clamp01(_criticalChance + zoneCrit + ammoCrit);
            LastCritical = critical;

            if (critical)
            {
                LastDamagedModule = PickModuleForZone(LastZone);
                int moduleDamage = Mathf.Clamp(18 + rawDamage * 7 + (ammo == AmmoType.Plasma ? 10 : ammo == AmmoType.ArmorPiercing ? 6 : 0), 18, 52);
                ApplyModuleDamage(LastDamagedModule, moduleDamage, hitPoint);
                damageMultiplier *= LastDamagedModule == TankModule.AmmoRack ? 1.42f : 1.25f;
            }

            return Mathf.Max(1, Mathf.RoundToInt(rawDamage * damageMultiplier));
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
            return $"ENG {EngineIntegrity}%  TRK {TrackIntegrity}%  GUN {GunIntegrity}%  AMMO {AmmoRackIntegrity}%";
        }

        private void ResetModules()
        {
            EngineIntegrity = TrackIntegrity = GunIntegrity = AmmoRackIntegrity = 100;
            LastDamagedModule = TankModule.None;
            RecalculatePerformance();
        }

        private TankModule PickModuleForZone(ArmorZone zone)
        {
            float r = Random.value;
            if (zone == ArmorZone.Rear)
                return r < 0.46f ? TankModule.Engine : r < 0.72f ? TankModule.AmmoRack : r < 0.88f ? TankModule.Tracks : TankModule.Gun;
            if (zone == ArmorZone.Side)
                return r < 0.34f ? TankModule.Tracks : r < 0.58f ? TankModule.AmmoRack : r < 0.79f ? TankModule.Engine : TankModule.Gun;
            return r < 0.43f ? TankModule.Gun : r < 0.69f ? TankModule.Tracks : r < 0.86f ? TankModule.AmmoRack : TankModule.Engine;
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
            VisualFactory.RingPulse(hitPoint, pulse, 0.82f);
            VisualFactory.MicroBurst(hitPoint, new Color(1f, 0.18f, 0.05f), 0.95f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.38f, 0.04f);
        }

        private void RecalculatePerformance()
        {
            float engineFactor = Mathf.Lerp(_player ? 0.58f : 0.46f, 1f, EngineIntegrity / 100f);
            float trackFactor = Mathf.Lerp(_player ? 0.52f : 0.42f, 1f, TrackIntegrity / 100f);
            MobilityMultiplier = Mathf.Clamp(Mathf.Min(engineFactor, trackFactor), _player ? 0.52f : 0.42f, 1f);

            float gunPenalty = Mathf.Lerp(_player ? 1.60f : 1.85f, 1f, GunIntegrity / 100f);
            float rackPenalty = Mathf.Lerp(_player ? 1.38f : 1.58f, 1f, AmmoRackIntegrity / 100f);
            ReloadMultiplier = Mathf.Clamp(Mathf.Max(gunPenalty, rackPenalty), 1f, _player ? 1.60f : 1.85f);
        }
    }
}
