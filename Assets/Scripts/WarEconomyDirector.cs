using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Persistent war economy. Surviving rounds earns War Bonds, while the player can spend
    /// them during combat on repairs, ordnance and emergency protection. Career rank and the
    /// v3.4 Logistics Command perk tree improve prices and reward yield.
    /// v3.6 exposes a single authoritative reward path for battlefield mission directives.
    /// </summary>
    public sealed class WarEconomyDirector : MonoBehaviour
    {
        private const string BondsKey = "TankRevival.WarBonds";
        private const string LifetimeKey = "TankRevival.WarBondsLifetime";

        private TankGame _game;
        private int _observedRound;
        private int _bonds;
        private int _lifetime;
        private float _nextPurchase;
        private string _banner = string.Empty;
        private float _bannerUntil;
        private GUIStyle _header, _body, _small, _bannerStyle;

        public static int CurrentBonds => Mathf.Max(0, PlayerPrefs.GetInt(BondsKey, 0));

        public static void AwardMissionBonds(int amount, string reason)
        {
            if (amount <= 0) return;
            WarEconomyDirector director = FindAnyObjectByType<WarEconomyDirector>();
            if (director != null)
            {
                director.AddBonds(amount);
                director._banner = $"MISSION REWARD +{amount} BONDS // {reason}";
                director._bannerUntil = Time.unscaledTime + 3.1f;
                return;
            }

            int bonds = Mathf.Clamp(PlayerPrefs.GetInt(BondsKey, 0) + amount, 0, 9999);
            int lifetime = Mathf.Clamp(PlayerPrefs.GetInt(LifetimeKey, 0) + amount, 0, 999999);
            PlayerPrefs.SetInt(BondsKey, bonds);
            PlayerPrefs.SetInt(LifetimeKey, lifetime);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<WarEconomyDirector>() != null) return;
            var go = new GameObject("WarEconomyDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<WarEconomyDirector>();
        }

        private void Awake()
        {
            _bonds = Mathf.Max(0, PlayerPrefs.GetInt(BondsKey, 0));
            _lifetime = Mathf.Max(0, PlayerPrefs.GetInt(LifetimeKey, 0));
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
                _observedRound = 0;
                return;
            }

            int round = _game.CurrentRound;
            if (_observedRound == 0)
                _observedRound = round;
            else if (round != _observedRound)
            {
                if (round > _observedRound) AwardRoundSurvival(_observedRound);
                _observedRound = round;
            }

            if (Time.unscaledTime < _nextPurchase) return;
            if (Input.GetKeyDown(KeyCode.F1)) TryBuyRepair();
            else if (Input.GetKeyDown(KeyCode.F2)) TryBuyOrdnance();
            else if (Input.GetKeyDown(KeyCode.F3)) TryBuyInsurance();
        }

        private int LogisticsTier()
        {
            int rank = MetaProgressionDirector.CurrentRank;
            int careerTier = _lifetime >= 500 ? 3 : _lifetime >= 220 ? 2 : _lifetime >= 80 ? 1 : 0;
            return Mathf.Clamp(Mathf.Max(careerTier, rank / 3), 0, 3);
        }

        private int Discounted(int baseCost)
        {
            return Mathf.Max(5, baseCost - LogisticsTier() * 2 - CommanderCareerDirector.EconomyDiscount);
        }

        private void AwardRoundSurvival(int completedRound)
        {
            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;
            if (player == null || player.Health == null || player.Health.IsDead || eagle == null || eagle.IsDead) return;

            int baseReward = 2 + completedRound / 20;
            if (completedRound % 10 == 0) baseReward += 4;
            if (completedRound % 20 == 0) baseReward += 4;
            float risk = CampaignVariantDirector.ActiveRewardMultiplier;
            float career = CommanderCareerDirector.WarBondRewardMultiplier;
            int reward = Mathf.Max(1, Mathf.RoundToInt(baseReward * risk * career));
            AddBonds(reward);

            _banner = career > 1.01f
                ? $"WAR BONDS +{reward} // COMMAND LOGISTICS x{career:0.00}"
                : risk > 1.01f
                    ? $"WAR BONDS +{reward} // RISK BONUS x{risk:0.0}"
                    : $"WAR BONDS +{reward} // SUPPLY ACCOUNT {_bonds}";
            _bannerUntil = Time.unscaledTime + 2.8f;
        }

        private void AddBonds(int amount)
        {
            if (amount <= 0) return;
            _bonds = Mathf.Clamp(_bonds + amount, 0, 9999);
            _lifetime = Mathf.Clamp(_lifetime + amount, 0, 999999);
            PlayerPrefs.SetInt(BondsKey, _bonds);
            PlayerPrefs.SetInt(LifetimeKey, _lifetime);
            PlayerPrefs.Save();
        }

        private bool Spend(int amount, string label)
        {
            if (_bonds < amount)
            {
                _banner = $"INSUFFICIENT WAR BONDS // {label} COST {amount}";
                _bannerUntil = Time.unscaledTime + 2.4f;
                BattleAudio.PlayGlobal(SoundCue.EnemyShot, 0.16f, -0.18f);
                _nextPurchase = Time.unscaledTime + 0.35f;
                return false;
            }

            _bonds -= amount;
            PlayerPrefs.SetInt(BondsKey, _bonds);
            PlayerPrefs.Save();
            _nextPurchase = Time.unscaledTime + 0.55f;
            return true;
        }

        private void TryBuyRepair()
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null) return;
            int cost = Discounted(12);
            if (!Spend(cost, "FIELD SERVICE")) return;

            int tier = LogisticsTier();
            player.Health.Heal(2 + tier / 2);
            _game.RepairEagle(1 + (tier >= 3 ? 1 : 0));
            ArmorSystem armor = player.GetComponent<ArmorSystem>();
            if (armor != null) armor.RepairModules(18 + tier * 7);
            _banner = $"FIELD SERVICE DEPLOYED // -{cost} BONDS";
            _bannerUntil = Time.unscaledTime + 2.8f;
            VisualFactory.RingPulse(player.transform.position, new Color(0.18f, 1f, 0.48f), 1.25f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.38f, -0.05f);
        }

        private void TryBuyOrdnance()
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null) return;
            int cost = Discounted(14);
            if (!Spend(cost, "ORDNANCE DROP")) return;

            int tier = LogisticsTier();
            player.AddAmmo(AmmoType.ArmorPiercing, 3 + tier);
            player.AddAmmo(AmmoType.Explosive, 2 + tier);
            if (tier >= 1) player.AddAmmo(AmmoType.EMP, 1 + tier);
            if (tier >= 2) player.AddAmmo(AmmoType.Plasma, tier);
            if (tier >= 3) player.AddAmmo(AmmoType.Twin, 3);

            _banner = $"ORDNANCE DROP CONFIRMED // LOGISTICS TIER {tier + 1}";
            _bannerUntil = Time.unscaledTime + 2.8f;
            VisualFactory.RingPulse(player.transform.position, new Color(0.22f, 0.78f, 1f), 1.20f);
            BattleAudio.PlayGlobal(SoundCue.AmmoPickup, 0.48f, 0.02f);
        }

        private void TryBuyInsurance()
        {
            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;
            if (player == null || player.Health == null || eagle == null) return;
            int cost = Discounted(18);
            if (!Spend(cost, "OVERDRIVE INSURANCE")) return;

            int tier = LogisticsTier();
            float duration = 2.6f + tier * 0.7f;
            player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + duration);
            eagle.InvulnerableUntil = Mathf.Max(eagle.InvulnerableUntil, Time.time + Mathf.Max(1.4f, duration * 0.62f));
            _banner = $"OVERDRIVE INSURANCE ACTIVE // {duration:0.0}s";
            _bannerUntil = Time.unscaledTime + 2.8f;
            VisualFactory.RingPulse(player.transform.position, new Color(1f, 0.82f, 0.20f), 1.45f);
            VisualFactory.RingPulse(eagle.transform.position, new Color(1f, 0.72f, 0.18f), 1.65f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.52f, 0.03f);
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.80f, 0.22f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.65f, 0.74f, 0.82f) } };
            _bannerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.83f, 0.30f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            int tier = LogisticsTier();
            float y = 394f;
            GUI.color = new Color(0.045f, 0.035f, 0.014f, 0.93f);
            GUI.Box(new Rect(14f, y, 455f, 94f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(28f, y + 7f, 420f, 20f), $"WAR ECONOMY // {_bonds} BONDS // LOGISTICS {tier + 1}", _header);
            GUI.Label(new Rect(28f, y + 31f, 420f, 18f), $"F1 SERVICE {Discounted(12)}   F2 ORDNANCE {Discounted(14)}   F3 INSURANCE {Discounted(18)}", _body);
            GUI.Label(new Rect(28f, y + 54f, 420f, 30f), $"Career logistics: -{CommanderCareerDirector.EconomyDiscount} cost // x{CommanderCareerDirector.WarBondRewardMultiplier:0.00} survival payout.", _small);

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.025f, 0.035f, 0.050f, 0.95f);
                GUI.Box(new Rect(Screen.width * 0.5f - 350f, Screen.height * 0.37f, 700f, 48f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 340f, Screen.height * 0.37f + 7f, 680f, 34f), _banner, _bannerStyle);
            }
        }
    }
}
