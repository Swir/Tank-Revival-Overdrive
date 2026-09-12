using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v5.8 player-triggered active defense for Orzelek. Uses the existing War Bond balance,
    /// authoritative Health state and RuntimeBattleRegistry actors. It never creates a second
    /// currency or replaces TankGame combat authority.
    /// </summary>
    public sealed class OrzelekFortressDirector : MonoBehaviour
    {
        private const string BondsKey = "TankRevival.WarBonds";
        public const int ShieldCost = 18;
        public const int RepairCost = 16;
        public const int CounterBatteryCost = 22;
        public const float ShieldCooldown = 15f;
        public const float RepairCooldown = 18f;
        public const float CounterBatteryCooldown = 14f;
        public const float ShieldSeconds = 3.2f;
        public const float CounterBatteryRadius = 7.0f;
        public const int CounterBatteryTargets = 4;

        private TankGame _game;
        private Health _eagle;
        private float _shieldReady;
        private float _repairReady;
        private float _counterReady;
        private float _pressureFlashUntil;
        private int _damageObserved;
        private string _banner = string.Empty;
        private float _bannerUntil;
        private GUIStyle _header, _body, _small;

        public int ObservedEagleDamage => _damageObserved;
        public static bool ConfigurationValid => ShieldCost >= 10 && RepairCost >= 10 && CounterBatteryCost >= 10 && ShieldSeconds >= 2f && ShieldSeconds <= 5f && CounterBatteryTargets >= 2 && CounterBatteryTargets <= 6 && CounterBatteryRadius >= 5f && CounterBatteryRadius <= 9f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<OrzelekFortressDirector>() != null) return;
            var go = new GameObject("OrzelekFortressDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<OrzelekFortressDirector>();
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            Health eagle = CombatRoster.Eagle;
            if (eagle != _eagle)
            {
                if (_eagle != null) _eagle.Damaged -= OnEagleDamaged;
                _eagle = eagle;
                if (_eagle != null) _eagle.Damaged += OnEagleDamaged;
            }
            if (_game == null || !_game.IsPlaying || _eagle == null || _eagle.IsDead) return;

            if (Input.GetKeyDown(KeyCode.F6)) ActivateShield();
            else if (Input.GetKeyDown(KeyCode.F7)) ActivateRepairDrone();
            else if (Input.GetKeyDown(KeyCode.F8)) ActivateCounterBattery();
        }

        private void OnDestroy()
        {
            if (_eagle != null) _eagle.Damaged -= OnEagleDamaged;
        }

        private void OnEagleDamaged(Health health, int amount)
        {
            _damageObserved += Mathf.Max(0, amount);
            _pressureFlashUntil = Time.unscaledTime + 1.4f;
        }

        private bool Spend(int cost, string label)
        {
            int current = WarEconomyDirector.CurrentBonds;
            if (current < cost)
            {
                _banner = "FORTRESS // INSUFFICIENT BONDS FOR " + label;
                _bannerUntil = Time.unscaledTime + 2.2f;
                return false;
            }
            PlayerPrefs.SetInt(BondsKey, current - cost);
            PlayerPrefs.Save();
            WarEconomyDirector economy = FindAnyObjectByType<WarEconomyDirector>();
            if (economy != null) economy.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
            return true;
        }

        private void ActivateShield()
        {
            if (Time.unscaledTime < _shieldReady) return;
            if (!Spend(ShieldCost, "AEGIS SHIELD")) return;
            _shieldReady = Time.unscaledTime + ShieldCooldown;
            _eagle.InvulnerableUntil = Mathf.Max(_eagle.InvulnerableUntil, Time.time + ShieldSeconds);
            VisualFactory.RingPulse(_eagle.transform.position, new Color(0.18f, 0.82f, 1f), 1.8f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.55f, 0.08f);
            Banner("AEGIS SHIELD ONLINE // " + ShieldSeconds.ToString("0.0") + "s");
        }

        private void ActivateRepairDrone()
        {
            if (Time.unscaledTime < _repairReady || _eagle.Current >= _eagle.Maximum) return;
            if (!Spend(RepairCost, "REPAIR DRONE")) return;
            _repairReady = Time.unscaledTime + RepairCooldown;
            _eagle.Heal(2);
            VisualFactory.RingPulse(_eagle.transform.position, new Color(0.22f, 1f, 0.42f), 1.55f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.42f, -0.02f);
            Banner("FORTRESS REPAIR DRONE // +2 EAGLE HP");
        }

        private void ActivateCounterBattery()
        {
            if (Time.unscaledTime < _counterReady) return;
            if (!Spend(CounterBatteryCost, "COUNTER BATTERY")) return;
            _counterReady = Time.unscaledTime + CounterBatteryCooldown;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int struck = 0;
            for (int pass = 0; pass < CounterBatteryTargets; pass++)
            {
                EnemyTank best = null;
                float bestSq = CounterBatteryRadius * CounterBatteryRadius;
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                    float sq = ((Vector2)enemy.transform.position - (Vector2)_eagle.transform.position).sqrMagnitude;
                    if (sq > bestSq) continue;
                    bool alreadyHit = enemy.gameObject.CompareTag("Respawn");
                    if (alreadyHit) continue;
                    best = enemy;
                    bestSq = sq;
                }
                if (best == null) break;
                best.gameObject.tag = "Respawn";
                best.Health.Damage(1, Team.Player);
                VisualFactory.RingPulse(best.transform.position, new Color(1f, 0.55f, 0.12f), 0.75f);
                struck++;
            }
            for (int i = 0; i < enemies.Length; i++) if (enemies[i] != null && enemies[i].gameObject.CompareTag("Respawn")) enemies[i].gameObject.tag = "Untagged";
            BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.55f, 0.03f);
            Banner("COUNTER BATTERY // " + struck + " HOSTILES SUPPRESSED");
        }

        private void Banner(string text)
        {
            _banner = text;
            _bannerUntil = Time.unscaledTime + 2.5f;
        }

        private static string Ready(float t) => Time.unscaledTime >= t ? "READY" : Mathf.CeilToInt(t - Time.unscaledTime) + "s";

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.30f, 0.90f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.68f, 0.78f, 0.86f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _eagle == null) return;
            EnsureStyles();
            float y = 494f;
            GUI.color = Time.unscaledTime < _pressureFlashUntil ? new Color(0.18f, 0.04f, 0.03f, 0.96f) : new Color(0.025f, 0.055f, 0.075f, 0.93f);
            GUI.Box(new Rect(14f, y, 455f, 90f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(28f, y + 6f, 420f, 20f), "ORZEŁEK FORTRESS // " + _eagle.Current + "/" + _eagle.Maximum + " HP // " + WarEconomyDirector.CurrentBonds + " BONDS", _header);
            GUI.Label(new Rect(28f, y + 30f, 420f, 18f), "F6 AEGIS " + ShieldCost + " [" + Ready(_shieldReady) + "]   F7 REPAIR " + RepairCost + " [" + Ready(_repairReady) + "]", _body);
            GUI.Label(new Rect(28f, y + 50f, 420f, 18f), "F8 COUNTER BATTERY " + CounterBatteryCost + " [" + Ready(_counterReady) + "]", _body);
            GUI.Label(new Rect(28f, y + 69f, 420f, 16f), "Damage pressure observed: " + _damageObserved, _small);
            if (Time.unscaledTime < _bannerUntil) GUI.Label(new Rect(Screen.width * 0.5f - 280f, Screen.height * 0.43f, 560f, 28f), _banner, _header);
        }

        public static bool ProbeSpend(int amount)
        {
            int before = WarEconomyDirector.CurrentBonds;
            if (before < amount) return false;
            PlayerPrefs.SetInt(BondsKey, before - amount);
            PlayerPrefs.Save();
            return WarEconomyDirector.CurrentBonds == before - amount;
        }
    }
}
