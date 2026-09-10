using UnityEngine;

namespace TankRevival
{
    public enum WaveDoctrineKind
    {
        Recon,
        Blitz,
        Siege,
        Marksmen,
        HeavyColumn,
        SupplyRaid,
        EliteHunt,
        Crossfire,
        IronStorm,
        BossProtocol
    }

    public readonly struct WaveDoctrineProfile
    {
        public readonly WaveDoctrineKind Kind;
        public readonly string Name;
        public readonly string Description;
        public readonly Color Color;
        public readonly int BonusArmor;
        public readonly float ExtraFireChance;
        public readonly float ExtraShotDelay;
        public readonly float EagleTargetBias;
        public readonly float ProjectileSpeed;
        public readonly float SalvageMultiplier;

        public WaveDoctrineProfile(
            WaveDoctrineKind kind,
            string name,
            string description,
            Color color,
            int bonusArmor,
            float extraFireChance,
            float extraShotDelay,
            float eagleTargetBias,
            float projectileSpeed,
            float salvageMultiplier)
        {
            Kind = kind;
            Name = name;
            Description = description;
            Color = color;
            BonusArmor = bonusArmor;
            ExtraFireChance = extraFireChance;
            ExtraShotDelay = extraShotDelay;
            EagleTargetBias = eagleTargetBias;
            ProjectileSpeed = projectileSpeed;
            SalvageMultiplier = salvageMultiplier;
        }
    }

    public static class WaveDoctrineDirector
    {
        public static WaveDoctrineProfile Create(int round)
        {
            round = Mathf.Clamp(round, 1, 100);

            if (round % 10 == 0)
            {
                return new WaveDoctrineProfile(
                    WaveDoctrineKind.BossProtocol,
                    "BOSS PROTOCOL",
                    "Command tank supported by reinforced assault doctrine",
                    new Color(1f, 0.16f, 0.08f),
                    round >= 50 ? 2 : 1,
                    0.18f,
                    Mathf.Max(1.70f, 2.55f - round * 0.006f),
                    0.58f,
                    11.5f + round * 0.018f,
                    1.35f);
            }

            if (round <= 5)
            {
                return new WaveDoctrineProfile(
                    WaveDoctrineKind.Recon,
                    "RECON PATROL",
                    "Light probing force — learn the battlefield",
                    new Color(0.35f, 0.82f, 1f),
                    0,
                    0.04f,
                    3.3f,
                    0.22f,
                    8.8f,
                    1f);
            }

            if (round >= 85 && round % 4 == 1)
            {
                return new WaveDoctrineProfile(
                    WaveDoctrineKind.IronStorm,
                    "IRON STORM",
                    "Veteran crews fire coordinated saturation salvos",
                    new Color(1f, 0.28f, 0.08f),
                    2,
                    0.58f,
                    1.85f,
                    0.52f,
                    13.6f,
                    1.45f);
            }

            if (round >= 70 && round % 6 == 0)
            {
                return new WaveDoctrineProfile(
                    WaveDoctrineKind.Crossfire,
                    "CROSSFIRE",
                    "Enemy sections coordinate paired flank shots",
                    new Color(1f, 0.52f, 0.10f),
                    1,
                    0.50f,
                    2.05f,
                    0.46f,
                    12.9f,
                    1.30f);
            }

            if (round >= 60 && round % 5 == 2)
            {
                return new WaveDoctrineProfile(
                    WaveDoctrineKind.EliteHunt,
                    "ELITE HUNTERS",
                    "High-skill crews prioritize the player and punish exposure",
                    new Color(0.35f, 0.45f, 1f),
                    2,
                    0.46f,
                    2.15f,
                    0.20f,
                    13.3f,
                    1.38f);
            }

            if (round >= 40 && round % 7 == 0)
            {
                return new WaveDoctrineProfile(
                    WaveDoctrineKind.SupplyRaid,
                    "SUPPLY RAID",
                    "Valuable logistics convoy protected by aggressive escorts",
                    new Color(0.24f, 1f, 0.66f),
                    1,
                    0.24f,
                    2.65f,
                    0.30f,
                    10.7f,
                    1.70f);
            }

            int selector = (round * 7 + round / 10) % 5;
            if (selector == 0 && round >= 8)
            {
                return new WaveDoctrineProfile(
                    WaveDoctrineKind.Blitz,
                    "BLITZ WAVE",
                    "Fast crews pressure lanes with rapid follow-up fire",
                    new Color(1f, 0.80f, 0.16f),
                    0,
                    0.34f,
                    2.20f,
                    0.28f,
                    11.8f,
                    1.15f);
            }

            if (selector == 1 && round >= 16)
            {
                return new WaveDoctrineProfile(
                    WaveDoctrineKind.Siege,
                    "SIEGE COLUMN",
                    "Assault crews focus fire toward the Orzełek stronghold",
                    new Color(1f, 0.34f, 0.16f),
                    1,
                    0.32f,
                    2.45f,
                    0.82f,
                    10.8f,
                    1.20f);
            }

            if (selector == 2 && round >= 24)
            {
                return new WaveDoctrineProfile(
                    WaveDoctrineKind.Marksmen,
                    "MARKSMEN",
                    "Long-range gunners launch high-velocity aimed shots",
                    new Color(0.42f, 1f, 0.60f),
                    0,
                    0.38f,
                    2.75f,
                    0.18f,
                    14.5f,
                    1.22f);
            }

            if (selector == 3 && round >= 32)
            {
                return new WaveDoctrineProfile(
                    WaveDoctrineKind.HeavyColumn,
                    "HEAVY COLUMN",
                    "Reinforced armor advances under disciplined cannon fire",
                    new Color(0.78f, 0.44f, 1f),
                    2,
                    0.26f,
                    2.60f,
                    0.48f,
                    10.9f,
                    1.30f);
            }

            return new WaveDoctrineProfile(
                WaveDoctrineKind.Recon,
                "COMBINED ARMS",
                "Mixed assault force adapts between player and stronghold targets",
                new Color(0.42f, 0.84f, 1f),
                round >= 55 ? 1 : 0,
                Mathf.Lerp(0.10f, 0.28f, round / 100f),
                Mathf.Lerp(3.05f, 2.25f, round / 100f),
                0.38f,
                Mathf.Lerp(9.2f, 12.0f, round / 100f),
                1.10f);
        }
    }

    public sealed class DoctrineAugment : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _enemy;
        private WaveDoctrineProfile _profile;
        private bool _extraWeaponEnabled;
        private float _nextExtraShot;

        public void Initialize(TankGame game, EnemyTank enemy, WaveDoctrineProfile profile)
        {
            _game = game;
            _enemy = enemy;
            _profile = profile;

            if (_enemy != null && _enemy.Health != null && profile.BonusArmor > 0)
                _enemy.Health.SetMaximum(_enemy.Health.Maximum + profile.BonusArmor, true);

            _extraWeaponEnabled = _enemy != null && _enemy.Kind != EnemyKind.Boss && Random.value < profile.ExtraFireChance;
            _nextExtraShot = Time.time + Random.Range(profile.ExtraShotDelay * 0.75f, profile.ExtraShotDelay * 1.35f);

            Color auraColor = new Color(profile.Color.r, profile.Color.g, profile.Color.b, 0.18f);
            var aura = VisualFactory.RingObject("DoctrineAura", transform, new Vector2(1.12f, 1.12f), auraColor, Vector3.zero, 1);
            aura.AddComponent<DoctrineAuraPulse>();
        }

        private void Update()
        {
            if (!_extraWeaponEnabled || _game == null || _enemy == null || !_game.IsPlaying) return;
            if (_enemy.Health == null || _enemy.Health.IsDead) return;
            if (Time.time < _nextExtraShot) return;

            FireDoctrineShot();
            _nextExtraShot = Time.time + Random.Range(_profile.ExtraShotDelay * 0.82f, _profile.ExtraShotDelay * 1.22f);
        }

        private void FireDoctrineShot()
        {
            bool targetEagle = Random.value < _profile.EagleTargetBias;
            Vector2 target = targetEagle ? _game.BasePosition : _game.PlayerPosition;
            Vector2 origin = transform.position;
            Vector2 direction = (target - origin).normalized;
            if (direction.sqrMagnitude < 0.1f) direction = Vector2.down;

            int damage = _profile.Kind == WaveDoctrineKind.IronStorm || _profile.Kind == WaveDoctrineKind.Siege ? 2 : 1;
            Color c = _profile.Color;
            Vector2 muzzle = origin + direction * 0.70f;

            if (_profile.Kind == WaveDoctrineKind.Crossfire || _profile.Kind == WaveDoctrineKind.IronStorm)
            {
                Vector2 side = new Vector2(-direction.y, direction.x);
                _game.SpawnProjectile(muzzle + side * 0.12f, (direction + side * 0.10f).normalized, Team.Enemy, damage, _profile.ProjectileSpeed, c, AmmoType.Basic);
                _game.SpawnProjectile(muzzle - side * 0.12f, (direction - side * 0.10f).normalized, Team.Enemy, damage, _profile.ProjectileSpeed, c, AmmoType.Basic);
            }
            else
            {
                _game.SpawnProjectile(muzzle, direction, Team.Enemy, damage, _profile.ProjectileSpeed, c, AmmoType.Basic);
            }

            VisualFactory.MuzzleFlash(muzzle, c, _profile.Kind == WaveDoctrineKind.Siege ? 0.95f : 0.62f);
            BattleAudio.PlayGlobal(_profile.Kind == WaveDoctrineKind.Siege ? SoundCue.HeavyShot : SoundCue.EnemyShot, 0.12f, 0.08f);
        }
    }

    public sealed class DoctrineAuraPulse : MonoBehaviour
    {
        private Vector3 _baseScale;
        private float _phase;

        private void Start()
        {
            _baseScale = transform.localScale;
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.2f + _phase) * 0.06f;
            transform.localScale = _baseScale * pulse;
            transform.Rotate(0f, 0f, 14f * Time.unscaledDeltaTime);
        }
    }
}
