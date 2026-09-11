using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum CommanderPerkTrack
    {
        Vanguard = 0,
        Engineer = 1,
        Logistics = 2
    }

    /// <summary>
    /// v3.4 PLAYER PROGRESSION, COMMANDER PERKS & META CAMPAIGN.
    /// Extends the existing MetaProgressionDirector instead of replacing it. Command Rank and
    /// unique legendary boss kills generate a finite pool of perk points spent across three
    /// persistent career tracks. Perks feed the existing PlayerTank, ArmorSystem, War Economy,
    /// Eagle repair and CombatStatus systems so the career layer changes real campaign play.
    /// </summary>
    [DefaultExecutionOrder(320)]
    public sealed class CommanderCareerDirector : MonoBehaviour
    {
        private const string VanguardKey = "TankRevival.CommanderPerks.Vanguard";
        private const string EngineerKey = "TankRevival.CommanderPerks.Engineer";
        private const string LogisticsKey = "TankRevival.CommanderPerks.Logistics";
        private const string BossRelicMaskKey = "TankRevival.CommanderPerks.BossRelics";
        private const string LifetimeBossVictoriesKey = "TankRevival.CommanderPerks.BossVictories";

        public static CommanderCareerDirector Instance { get; private set; }

        private TankGame _game;
        private PlayerTank _player;
        private Health _playerHealth;
        private int _observedRound = -1;
        private int _combatKills;
        private int _emergencyShieldRound = -1;
        private float _nextScan;
        private readonly HashSet<Health> _hookedBosses = new HashSet<Health>();

        private string _toast = string.Empty;
        private float _toastUntil;
        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _accent;
        private GUIStyle _toastStyle;

        public static int VanguardLevel => Mathf.Clamp(PlayerPrefs.GetInt(VanguardKey, 0), 0, 5);
        public static int EngineerLevel => Mathf.Clamp(PlayerPrefs.GetInt(EngineerKey, 0), 0, 5);
        public static int LogisticsLevel => Mathf.Clamp(PlayerPrefs.GetInt(LogisticsKey, 0), 0, 5);
        public static int BossRelicMask => PlayerPrefs.GetInt(BossRelicMaskKey, 0) & 0x3FF;
        public static int UniqueBossRelics => CountBits(BossRelicMask);
        public static int TotalSpentPoints => VanguardLevel + EngineerLevel + LogisticsLevel;
        public static int EarnedPoints => Mathf.Clamp(MetaProgressionDirector.CurrentRank + UniqueBossRelics / 2, 0, 15);
        public static int AvailablePoints => Mathf.Max(0, EarnedPoints - TotalSpentPoints);

        public static int EconomyDiscount => LogisticsLevel >= 5 ? 4 : LogisticsLevel >= 4 ? 3 : LogisticsLevel >= 2 ? 2 : LogisticsLevel >= 1 ? 1 : 0;
        public static float WarBondRewardMultiplier => 1f + (LogisticsLevel >= 5 ? 0.35f : LogisticsLevel >= 3 ? 0.24f : LogisticsLevel >= 2 ? 0.12f : 0f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CommanderCareerDirector>() != null) return;
            var go = new GameObject("CommanderCareerDirector_v3_4");
            DontDestroyOnLoad(go);
            go.AddComponent<CommanderCareerDirector>();
        }

        private void Awake()
        {
            Instance = this;
            Projectile.DamageResolved += OnDamageResolved;
        }

        private void OnDestroy()
        {
            Projectile.DamageResolved -= OnDamageResolved;
            foreach (Health h in _hookedBosses)
                if (h != null) h.Died -= OnBossDied;
            UnhookPlayer();
            if (Instance == this) Instance = null;
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
                _observedRound = -1;
                _combatKills = 0;
                UnhookPlayer();
                HandleCareerInput();
                return;
            }

            AcquirePlayer();
            int round = _game.CurrentRound;
            if (round != _observedRound)
            {
                _observedRound = round;
                _combatKills = 0;
                ApplyRoundBenefits(round);
            }

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.55f;
                HookBosses();
            }
        }

        private void HandleCareerInput()
        {
            if (Input.GetKeyDown(KeyCode.F9)) TryBuyTrack(CommanderPerkTrack.Vanguard);
            else if (Input.GetKeyDown(KeyCode.F10)) TryBuyTrack(CommanderPerkTrack.Engineer);
            else if (Input.GetKeyDown(KeyCode.F11)) TryBuyTrack(CommanderPerkTrack.Logistics);
        }

        private void TryBuyTrack(CommanderPerkTrack track)
        {
            int level = GetTrackLevel(track);
            if (level >= 5)
            {
                Announce(TrackName(track) + " // MAX LEVEL");
                return;
            }
            if (AvailablePoints <= 0)
            {
                Announce("NO COMMANDER POINTS // EARN RANKS OR BOSS RELICS");
                return;
            }

            int next = level + 1;
            PlayerPrefs.SetInt(TrackKey(track), next);
            PlayerPrefs.Save();
            Announce(TrackName(track) + " // LEVEL " + next + " UNLOCKED");
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.62f, 0.02f);
        }

        private void AcquirePlayer()
        {
            if (_player != null && _playerHealth != null) return;
            _player = CombatRoster.Player;
            if (_player == null) _player = FindAnyObjectByType<PlayerTank>();
            if (_player == null) return;

            _playerHealth = _player.Health;
            if (_playerHealth != null)
            {
                _playerHealth.Damaged -= OnPlayerDamaged;
                _playerHealth.Damaged += OnPlayerDamaged;
            }
            ApplyDeploymentBenefits();
        }

        private void UnhookPlayer()
        {
            if (_playerHealth != null) _playerHealth.Damaged -= OnPlayerDamaged;
            _playerHealth = null;
            _player = null;
        }

        private void ApplyDeploymentBenefits()
        {
            if (_player == null) return;

            int v = VanguardLevel;
            if (v >= 1) _player.AddAmmo(AmmoType.ArmorPiercing, 1 + (v >= 4 ? 1 : 0));
            if (v >= 2) _player.AddAmmo(AmmoType.Explosive, 1);
            if (v >= 4) _player.AddAmmo(AmmoType.Plasma, 1);

            int e = EngineerLevel;
            if (_player.Health != null && e >= 1)
            {
                int bonusHull = e >= 5 ? 3 : e >= 3 ? 2 : 1;
                _player.Health.SetMaximum(_player.Health.Maximum + bonusHull, true);
            }

            ArmorSystem armor = _player.GetComponent<ArmorSystem>();
            if (armor != null && e >= 2) armor.RepairModules(8 + e * 4);
        }

        private void ApplyRoundBenefits(int round)
        {
            if (_player == null) return;

            int v = VanguardLevel;
            int e = EngineerLevel;
            int l = LogisticsLevel;

            if (v >= 2 && round % 5 == 1)
                _player.AddAmmo(AmmoType.Explosive, 1 + (v >= 5 ? 1 : 0));
            if (v >= 4 && round % 10 == 0)
            {
                _player.AddAmmo(AmmoType.Plasma, 2);
                _player.AddAmmo(AmmoType.ArmorPiercing, 2);
            }

            ArmorSystem armor = _player.GetComponent<ArmorSystem>();
            if (armor != null && e >= 2)
                armor.RepairModules(5 + e * 3);
            if (e >= 3 && _player.Health != null && round % 3 == 1)
                _player.Health.Heal(1);
            if (e >= 4 && round > 1 && round % 5 == 1)
                _game.RepairEagle(1 + (e >= 5 && round % 10 == 1 ? 1 : 0));

            if (l >= 3 && round % 5 == 1)
            {
                _player.AddAmmo(AmmoType.ArmorPiercing, 1);
                _player.AddAmmo(AmmoType.EMP, l >= 5 ? 2 : 1);
            }
        }

        private void OnDamageResolved(Projectile projectile, Health target, int damage, bool killed)
        {
            if (!killed || projectile == null || projectile.OwnerTeam != Team.Player || target == null || target.Team != Team.Enemy) return;
            _combatKills++;

            int v = VanguardLevel;
            if (v >= 3 && _player != null && _combatKills % Mathf.Max(4, 8 - v) == 0)
            {
                _player.AddAmmo(AmmoType.ArmorPiercing, 1);
                if (v >= 4) _player.AddAmmo(AmmoType.Explosive, 1);
                VisualFactory.RingPulse(_player.transform.position, new Color(1f, 0.58f, 0.12f), 0.95f);
            }

            if (v >= 5 && _combatKills % 10 == 0 && _playerHealth != null)
            {
                _playerHealth.InvulnerableUntil = Mathf.Max(_playerHealth.InvulnerableUntil, Time.time + 1.25f);
                PulseEnemySystems(_player.transform.position, 3.1f, 0.72f);
                Announce("VANGUARD MOMENTUM // COMBAT SURGE");
            }
        }

        private void OnPlayerDamaged(Health health, int amount)
        {
            if (health == null || health.IsDead || EngineerLevel < 5 || _game == null) return;
            if (_emergencyShieldRound == _game.CurrentRound) return;
            if (health.Maximum <= 0 || health.Current / (float)health.Maximum > 0.34f) return;

            _emergencyShieldRound = _game.CurrentRound;
            health.InvulnerableUntil = Mathf.Max(health.InvulnerableUntil, Time.time + 2.0f);
            health.Heal(1);
            ArmorSystem armor = _player != null ? _player.GetComponent<ArmorSystem>() : null;
            if (armor != null) armor.RepairModules(24);
            VisualFactory.RingPulse(health.transform.position, new Color(0.18f, 0.88f, 1f), 1.45f);
            Announce("ENGINEER FAILSAFE // EMERGENCY FIELD ONLINE");
        }

        private void HookBosses()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Kind != EnemyKind.Boss || enemy.Health == null || _hookedBosses.Contains(enemy.Health)) continue;
                _hookedBosses.Add(enemy.Health);
                enemy.Health.Died += OnBossDied;
            }
            _hookedBosses.RemoveWhere(h => h == null);
        }

        private void OnBossDied(Health health)
        {
            if (_game == null || health == null) return;
            int tier = Mathf.Clamp(_game.CurrentRound / 10, 1, 10);
            int bit = 1 << (tier - 1);
            int mask = BossRelicMask;
            bool first = (mask & bit) == 0;

            PlayerPrefs.SetInt(LifetimeBossVictoriesKey, PlayerPrefs.GetInt(LifetimeBossVictoriesKey, 0) + 1);
            if (first)
            {
                mask |= bit;
                PlayerPrefs.SetInt(BossRelicMaskKey, mask);
                PlayerPrefs.SetInt("TankRevival.BossLegendsDefeated", UniqueBossRelicsAfter(mask));
                Announce("LEGEND RELIC SECURED // " + tier + "/10 // CAREER POWER INCREASED");
            }
            else
            {
                Announce("LEGEND VICTORY // RELIC ALREADY RECOVERED");
            }
            PlayerPrefs.Save();

            if (_player != null)
            {
                _player.AddAmmo(AmmoType.Plasma, 1 + tier / 4);
                _player.AddAmmo(AmmoType.Explosive, 1 + tier / 5);
            }
            if (EngineerLevel >= 3) _game.RepairEagle(1);
            if (_playerHealth != null && VanguardLevel >= 4)
                _playerHealth.InvulnerableUntil = Mathf.Max(_playerHealth.InvulnerableUntil, Time.time + 2.2f);
        }

        private static int UniqueBossRelicsAfter(int mask) => CountBits(mask & 0x3FF);

        private static int CountBits(int mask)
        {
            int count = 0;
            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }
            return count;
        }

        private void PulseEnemySystems(Vector3 center, float radius, float duration)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Health h = hits[i] != null ? hits[i].GetComponent<Health>() : null;
                if (h == null || h.Team != Team.Enemy || h.IsDead) continue;
                CombatStatus status = h.GetComponent<CombatStatus>();
                if (status == null) status = h.gameObject.AddComponent<CombatStatus>();
                status.ApplyEmp(duration);
            }
        }

        private static int GetTrackLevel(CommanderPerkTrack track)
        {
            switch (track)
            {
                case CommanderPerkTrack.Vanguard: return VanguardLevel;
                case CommanderPerkTrack.Engineer: return EngineerLevel;
                default: return LogisticsLevel;
            }
        }

        private static string TrackKey(CommanderPerkTrack track)
        {
            switch (track)
            {
                case CommanderPerkTrack.Vanguard: return VanguardKey;
                case CommanderPerkTrack.Engineer: return EngineerKey;
                default: return LogisticsKey;
            }
        }

        private static string TrackName(CommanderPerkTrack track)
        {
            switch (track)
            {
                case CommanderPerkTrack.Vanguard: return "VANGUARD DOCTRINE";
                case CommanderPerkTrack.Engineer: return "COMBAT ENGINEERING";
                default: return "LOGISTICS COMMAND";
            }
        }

        private void Announce(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 3.2f;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(0.92f, 0.82f, 0.30f);
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            _body.normal.textColor = new Color(0.88f, 0.91f, 0.95f);
            _small = new GUIStyle(_body) { fontSize = 9 };
            _small.normal.textColor = new Color(0.60f, 0.70f, 0.78f);
            _accent = new GUIStyle(_body) { fontStyle = FontStyle.Bold };
            _accent.normal.textColor = new Color(0.36f, 0.92f, 1f);
            _toastStyle = new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
        }

        private void OnGUI()
        {
            if (_game == null) return;
            EnsureStyles();

            if (!_game.IsPlaying)
            {
                Rect panel = new Rect(Screen.width - 470f, Mathf.Max(128f, Screen.height - 252f), 448f, 214f);
                GUI.color = new Color(0.025f, 0.034f, 0.052f, 0.95f);
                GUI.Box(panel, string.Empty);
                GUI.color = Color.white;

                GUI.Label(new Rect(panel.x + 14f, panel.y + 9f, 420f, 20f), "COMMANDER CAREER // PERK COMMAND", _title);
                GUI.Label(new Rect(panel.x + 14f, panel.y + 32f, 420f, 18f), $"RANK {MetaProgressionDirector.CurrentRank}   •   RELICS {UniqueBossRelics}/10   •   POINTS {AvailablePoints}/{EarnedPoints}", _accent);

                GUI.Label(new Rect(panel.x + 14f, panel.y + 58f, 420f, 18f), $"F9  VANGUARD {VanguardLevel}/5", _body);
                GUI.Label(new Rect(panel.x + 14f, panel.y + 77f, 420f, 18f), "Ammo tempo • kill-chain resupply • combat surge", _small);
                GUI.Label(new Rect(panel.x + 14f, panel.y + 101f, 420f, 18f), $"F10 ENGINEERING {EngineerLevel}/5", _body);
                GUI.Label(new Rect(panel.x + 14f, panel.y + 120f, 420f, 18f), "Hull capacity • module service • Eagle repair • failsafe", _small);
                GUI.Label(new Rect(panel.x + 14f, panel.y + 144f, 420f, 18f), $"F11 LOGISTICS {LogisticsLevel}/5", _body);
                GUI.Label(new Rect(panel.x + 14f, panel.y + 163f, 420f, 18f), $"War Bond discount -{EconomyDiscount} • payout x{WarBondRewardMultiplier:0.00} • supply cadence", _small);
                GUI.Label(new Rect(panel.x + 14f, panel.y + 188f, 420f, 18f), "Every 2 unique legend relics grant +1 extra career point.", _small);
            }

            if (Time.unscaledTime < _toastUntil && !string.IsNullOrEmpty(_toast))
            {
                Rect toast = new Rect(Screen.width * 0.5f - 330f, Screen.height * 0.29f, 660f, 42f);
                GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.96f);
                GUI.Box(toast, string.Empty);
                GUI.color = Color.white;
                GUI.Label(toast, _toast, _toastStyle);
            }
        }
    }
}
