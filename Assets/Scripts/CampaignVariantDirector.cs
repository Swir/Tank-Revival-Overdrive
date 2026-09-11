using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.3 campaign variants. Each 10-round sector receives a deterministic high-impact rule set
    /// that modifies real wave enemies and pays a War Bond risk premium when the sector is survived.
    /// </summary>
    public sealed class CampaignVariantDirector : MonoBehaviour
    {
        private enum VariantKind
        {
            Standard,
            ArmoredSpearhead,
            BlackoutRaid,
            BlitzOverrun,
            FortressBreaker
        }

        private TankGame _game;
        private int _round;
        private int _sector;
        private VariantKind _variant;
        private readonly HashSet<int> _modified = new HashSet<int>();
        private string _banner = string.Empty;
        private float _bannerUntil;
        private GUIStyle _header, _body, _bannerStyle;

        public static float ActiveRewardMultiplier { get; private set; } = 1f;
        public static string ActiveVariantName { get; private set; } = "STANDARD FRONT";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CampaignVariantDirector>() != null) return;
            var go = new GameObject("CampaignVariantDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<CampaignVariantDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying)
            {
                _round = 0;
                _sector = 0;
                _modified.Clear();
                ActiveRewardMultiplier = 1f;
                ActiveVariantName = "STANDARD FRONT";
                return;
            }

            int round = _game.CurrentRound;
            if (round != _round)
            {
                _round = round;
                int sector = Mathf.Clamp((round - 1) / 10 + 1, 1, 10);
                if (sector != _sector)
                    BeginSector(sector);
            }

            if (_variant == VariantKind.Standard) return;
            ApplyToNewEnemies();
        }

        private void BeginSector(int sector)
        {
            _sector = sector;
            _modified.Clear();

            if (sector <= 1)
                _variant = VariantKind.Standard;
            else
                _variant = (VariantKind)(1 + ((sector - 2) % 4));

            ActiveRewardMultiplier = RewardFor(_variant, sector);
            ActiveVariantName = NameFor(_variant);
            _banner = $"SECTOR {sector} // {ActiveVariantName} // WAR BONDS x{ActiveRewardMultiplier:0.00}";
            _bannerUntil = Time.unscaledTime + 4.2f;

            if (_variant != VariantKind.Standard)
                BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.38f, -0.03f);
        }

        private static float RewardFor(VariantKind variant, int sector)
        {
            float lateBonus = Mathf.Clamp01((sector - 1) / 9f) * 0.12f;
            return variant switch
            {
                VariantKind.ArmoredSpearhead => 1.24f + lateBonus,
                VariantKind.BlackoutRaid => 1.30f + lateBonus,
                VariantKind.BlitzOverrun => 1.20f + lateBonus,
                VariantKind.FortressBreaker => 1.36f + lateBonus,
                _ => 1f
            };
        }

        private static string NameFor(VariantKind variant)
        {
            return variant switch
            {
                VariantKind.ArmoredSpearhead => "ARMORED SPEARHEAD",
                VariantKind.BlackoutRaid => "BLACKOUT RAID",
                VariantKind.BlitzOverrun => "BLITZ OVERRUN",
                VariantKind.FortressBreaker => "FORTRESS BREAKER",
                _ => "STANDARD FRONT"
            };
        }

        private string RuleText()
        {
            return _variant switch
            {
                VariantKind.ArmoredSpearhead => "Heavy / Elite armor reinforced; priority AP hunter volleys enabled.",
                VariantKind.BlackoutRaid => "Sniper / Elite / Siege units deploy EMP disruption fire.",
                VariantKind.BlitzOverrun => "Basic / Fast assault units gain reinforced hulls and burst pressure.",
                VariantKind.FortressBreaker => "Heavy / Siege units receive dedicated AP breach fire against Orzelek.",
                _ => "Baseline combat rules. No additional sector modifier."
            };
        }

        private void ApplyToNewEnemies()
        {
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                int id = enemy.GetInstanceID();
                if (!_modified.Add(id)) continue;

                if (!Eligible(enemy.Kind)) continue;

                int tier = Mathf.Clamp((_sector - 1) / 2, 0, 4);
                int hpBonus = _variant switch
                {
                    VariantKind.ArmoredSpearhead => 2 + tier,
                    VariantKind.BlackoutRaid => 1 + tier / 2,
                    VariantKind.BlitzOverrun => 1 + tier / 2,
                    VariantKind.FortressBreaker => 2 + tier,
                    _ => 0
                };

                if (hpBonus > 0)
                    enemy.Health.SetMaximum(enemy.Health.Maximum + hpBonus, true);

                CampaignVariantAgent agent = enemy.gameObject.AddComponent<CampaignVariantAgent>();
                agent.Initialize(_game, enemy, (int)_variant, tier);
            }
        }

        private bool Eligible(EnemyKind kind)
        {
            return _variant switch
            {
                VariantKind.ArmoredSpearhead => kind == EnemyKind.Heavy || kind == EnemyKind.Elite || kind == EnemyKind.Boss,
                VariantKind.BlackoutRaid => kind == EnemyKind.Sniper || kind == EnemyKind.Elite || kind == EnemyKind.Siege || kind == EnemyKind.Boss,
                VariantKind.BlitzOverrun => kind == EnemyKind.Basic || kind == EnemyKind.Fast || kind == EnemyKind.Elite,
                VariantKind.FortressBreaker => kind == EnemyKind.Heavy || kind == EnemyKind.Siege || kind == EnemyKind.Boss,
                _ => false
            };
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.48f, 0.16f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.86f, 0.90f, 0.94f) } };
            _bannerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.56f, 0.18f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            float y = 492f;
            GUI.color = new Color(0.050f, 0.024f, 0.012f, 0.91f);
            GUI.Box(new Rect(14f, y, 455f, 67f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(28f, y + 7f, 425f, 19f), $"CAMPAIGN VARIANT // {ActiveVariantName} // x{ActiveRewardMultiplier:0.00}", _header);
            GUI.Label(new Rect(28f, y + 31f, 425f, 28f), RuleText(), _body);

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.035f, 0.022f, 0.015f, 0.96f);
                GUI.Box(new Rect(Screen.width * 0.5f - 390f, Screen.height * 0.20f, 780f, 54f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 380f, Screen.height * 0.20f + 8f, 760f, 38f), _banner, _bannerStyle);
            }
        }
    }

    public sealed class CampaignVariantAgent : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _enemy;
        private Health _health;
        private int _variant;
        private int _tier;
        private float _nextAttack;
        private float _nextPulse;

        public void Initialize(TankGame game, EnemyTank enemy, int variant, int tier)
        {
            _game = game;
            _enemy = enemy;
            _health = enemy != null ? enemy.Health : null;
            _variant = variant;
            _tier = Mathf.Clamp(tier, 0, 4);
            _nextAttack = Time.time + Random.Range(2.4f, 4.0f);
            _nextPulse = Time.time + Random.Range(0.1f, 0.7f);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || _enemy == null || _health == null || _health.IsDead) return;

            if (Time.time >= _nextPulse)
            {
                _nextPulse = Time.time + 1.25f;
                VisualFactory.RingPulse(transform.position, ColorForVariant(), 0.42f + _tier * 0.04f);
            }

            if (Time.time < _nextAttack) return;
            _nextAttack = Time.time + Mathf.Max(1.8f, 4.4f - _tier * 0.35f + Random.Range(-0.35f, 0.45f));
            FireVariantAttack();
        }

        private void FireVariantAttack()
        {
            Vector2 origin = transform.position;
            Vector2 target = _variant == 4 ? _game.BasePosition : _game.PlayerPosition;
            Vector2 direction = target - origin;
            if (direction.sqrMagnitude < 0.02f) direction = Vector2.down;
            direction.Normalize();

            AmmoType ammo = AmmoType.Basic;
            int damage = 1;
            float speed = 8.5f + _tier * 0.55f;
            Color color = ColorForVariant();

            if (_variant == 1)
            {
                ammo = AmmoType.ArmorPiercing;
                damage = _tier >= 3 ? 2 : 1;
            }
            else if (_variant == 2)
            {
                ammo = AmmoType.EMP;
            }
            else if (_variant == 3)
            {
                ammo = _tier >= 3 ? AmmoType.Incendiary : AmmoType.Basic;
            }
            else if (_variant == 4)
            {
                ammo = AmmoType.ArmorPiercing;
                damage = _tier >= 2 ? 2 : 1;
            }

            _game.SpawnProjectile(origin + direction * 0.78f, direction, Team.Enemy, damage, speed, color, ammo);

            if ((_variant == 1 || _variant == 3) && _tier >= 2)
            {
                Vector2 side = new Vector2(-direction.y, direction.x);
                _game.SpawnProjectile(origin + direction * 0.72f + side * 0.18f, (direction + side * 0.10f).normalized, Team.Enemy, 1, speed * 0.96f, color, ammo);
            }

            BattleAudio.PlayGlobal(_variant == 2 ? SoundCue.Emp : SoundCue.HeavyShot, 0.17f, 0.06f);
        }

        private Color ColorForVariant()
        {
            return _variant switch
            {
                1 => new Color(1f, 0.52f, 0.12f, 0.60f),
                2 => new Color(0.30f, 0.72f, 1f, 0.62f),
                3 => new Color(1f, 0.20f, 0.08f, 0.60f),
                4 => new Color(0.94f, 0.18f, 0.28f, 0.66f),
                _ => new Color(0.70f, 0.70f, 0.70f, 0.5f)
            };
        }
    }
}
