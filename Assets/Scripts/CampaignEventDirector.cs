using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v4.2 campaign event layer.
    /// Adds deterministic sector dilemmas with safe/risky branches, persistent per-run consequences,
    /// kill-driven rewards and a strategic approach for the late campaign without replacing combat authority.
    /// </summary>
    [DefaultExecutionOrder(6100)]
    public sealed class CampaignEventDirector : MonoBehaviour
    {
        private enum EventKind
        {
            BrokenArrow = 0,
            BlackSignal = 1,
            IronBridge = 2,
            LastLight = 3
        }

        private enum Choice
        {
            None = 0,
            Safe = 1,
            Risk = 2
        }

        private TankGame _game;
        private bool _wasPlaying;
        private int _runId;
        private int _round;
        private int _sector = -1;
        private int _choiceSector = -1;
        private bool _choiceOpen;
        private float _choiceAt;
        private float _choiceUntil;
        private float _scanAt;
        private float _bannerUntil;
        private string _banner = string.Empty;
        private readonly HashSet<int> _hooked = new HashSet<int>();
        private readonly HashSet<int> _reinforced = new HashSet<int>();
        private int _sectorKills;
        private int _heavyKills;
        private int _specialKills;

        private GUIStyle _title;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _safe;
        private GUIStyle _risk;
        private GUIStyle _center;

        public static string CurrentApproach
        {
            get
            {
                int run = PlayerPrefs.GetInt("TankRevival.CampaignEventRunId", 0);
                int support = PlayerPrefs.GetInt($"TankRevival.EventSupport.{run}", 0);
                int assault = PlayerPrefs.GetInt($"TankRevival.EventAssault.{run}", 0);
                int intel = PlayerPrefs.GetInt($"TankRevival.EventIntel.{run}", 0);
                if (assault >= support && assault >= intel) return "BREAKTHROUGH";
                if (intel >= support) return "GHOST SPEAR";
                return "IRON SHIELD";
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CampaignEventDirector>() != null) return;
            var go = new GameObject("CampaignEventDirector_v4_2");
            DontDestroyOnLoad(go);
            go.AddComponent<CampaignEventDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            bool playing = _game.IsPlaying;
            if (playing && !_wasPlaying)
                BeginCampaignRun();
            else if (!playing && _wasPlaying)
                EndCampaignRun();
            _wasPlaying = playing;

            if (!playing) return;

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            int sector = Mathf.Clamp((round - 1) / 10, 0, 9);
            if (round != _round)
            {
                int previousSector = _sector;
                _round = round;
                _sector = sector;
                _hooked.Clear();
                _reinforced.Clear();

                if (sector != previousSector)
                {
                    _sectorKills = 0;
                    _heavyKills = 0;
                    _specialKills = 0;
                    ScheduleSectorEvent(sector);
                }

                ApplyRoundConsequence(round, sector);
            }

            if (!_choiceOpen && _choiceSector >= 0 && Time.unscaledTime >= _choiceAt && GetChoice(_choiceSector) == Choice.None)
                OpenChoice(_choiceSector);

            if (_choiceOpen)
            {
                if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                    ResolveChoice(Choice.Safe);
                else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
                    ResolveChoice(Choice.Risk);
                else if (Time.unscaledTime >= _choiceUntil)
                    ResolveChoice(Choice.Safe);
            }

            if (Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.30f;
                HookEnemies();
                ApplyRiskPressure();
            }
        }

        private void BeginCampaignRun()
        {
            _runId = PlayerPrefs.GetInt("TankRevival.CampaignEventRunId", 0) + 1;
            PlayerPrefs.SetInt("TankRevival.CampaignEventRunId", _runId);
            PlayerPrefs.SetInt($"TankRevival.EventSupport.{_runId}", 0);
            PlayerPrefs.SetInt($"TankRevival.EventAssault.{_runId}", 0);
            PlayerPrefs.SetInt($"TankRevival.EventIntel.{_runId}", 0);
            PlayerPrefs.Save();
            _round = 0;
            _sector = -1;
            _choiceOpen = false;
            _choiceSector = -1;
            _hooked.Clear();
            _reinforced.Clear();
        }

        private void EndCampaignRun()
        {
            _choiceOpen = false;
            _choiceSector = -1;
            _hooked.Clear();
            _reinforced.Clear();
        }

        private string ChoiceKey(int sector) => $"TankRevival.CampaignEvent.{_runId}.{sector}";

        private Choice GetChoice(int sector)
        {
            if (sector < 0 || sector > 9) return Choice.None;
            return (Choice)Mathf.Clamp(PlayerPrefs.GetInt(ChoiceKey(sector), 0), 0, 2);
        }

        private EventKind KindFor(int sector)
        {
            return (EventKind)(Mathf.Abs(sector * 7 + 3) % 4);
        }

        private void ScheduleSectorEvent(int sector)
        {
            if (sector <= 0 || sector >= 9)
            {
                _choiceSector = -1;
                if (sector == 9)
                    Announce($"ZERO FRONT APPROACH // {CurrentApproach}", 4.0f);
                return;
            }

            if (GetChoice(sector) != Choice.None)
            {
                _choiceSector = -1;
                return;
            }

            _choiceSector = sector;
            _choiceAt = Time.unscaledTime + 10.8f;
            Announce("STRATEGIC EVENT INBOUND // 4/5 DECISION AFTER ROUTE DEPLOYMENT", 3.2f);
        }

        private void OpenChoice(int sector)
        {
            _choiceOpen = true;
            _choiceUntil = Time.unscaledTime + 12f;
            EventKind kind = KindFor(sector);
            Announce($"{EventTitle(kind)} // COMMAND DECISION REQUIRED", 12f);
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.42f, -0.18f);
        }

        private void ResolveChoice(Choice choice)
        {
            if (!_choiceOpen || _choiceSector < 0) return;

            int sector = _choiceSector;
            EventKind kind = KindFor(sector);
            _choiceOpen = false;
            PlayerPrefs.SetInt(ChoiceKey(sector), (int)choice);
            PlayerPrefs.SetInt("TankRevival.LastCampaignEventSector", sector);
            PlayerPrefs.SetInt("TankRevival.LastCampaignEventChoice", (int)choice);

            ApplyImmediateChoice(kind, choice, sector);
            PlayerPrefs.Save();
            _choiceSector = -1;
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.72f, 0f);
        }

        private void ApplyImmediateChoice(EventKind kind, Choice choice, int sector)
        {
            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;
            int support = 0;
            int assault = 0;
            int intel = 0;

            switch (kind)
            {
                case EventKind.BrokenArrow:
                    if (choice == Choice.Safe)
                    {
                        support = 2;
                        if (player != null)
                        {
                            player.AddAmmo(AmmoType.ArmorPiercing, 3 + sector / 2);
                            if (player.Health != null) player.Health.Heal(2);
                            ArmorSystem armor = player.GetComponent<ArmorSystem>();
                            if (armor != null) armor.RepairModules(30 + sector * 2);
                        }
                        _game.RepairEagle(2);
                        Announce("RESCUE CONVOY // FIELD REPAIR NETWORK ONLINE", 4.0f);
                    }
                    else
                    {
                        assault = 2;
                        WarEconomyDirector.AwardMissionBonds(8 + sector, "FUEL DEPOT RAID");
                        if (player != null)
                        {
                            player.AddAmmo(AmmoType.Explosive, 4 + sector / 3);
                            player.AddAmmo(AmmoType.Plasma, sector >= 5 ? 2 : 1);
                        }
                        Announce("RAID FUEL DEPOT // HIGH YIELD // HEAVY RESPONSE EXPECTED", 4.4f);
                    }
                    break;

                case EventKind.BlackSignal:
                    if (choice == Choice.Safe)
                    {
                        intel = 2;
                        if (player != null)
                        {
                            player.AddAmmo(AmmoType.EMP, 3);
                            player.AddAmmo(AmmoType.ArmorPiercing, 2);
                        }
                        WarEconomyDirector.AwardMissionBonds(4 + sector / 2, "SIGNAL JAMMING");
                        Announce("JAM ENEMY NETWORK // EMP RESERVE + INTEL CONTROL", 4.0f);
                    }
                    else
                    {
                        intel = 1;
                        assault = 1;
                        if (player != null)
                        {
                            player.AddAmmo(AmmoType.ArmorPiercing, 4);
                            player.AddAmmo(AmmoType.Plasma, 2);
                        }
                        WarEconomyDirector.AwardMissionBonds(7 + sector, "DEEP RECON STRIKE");
                        Announce("DEEP RECON // PRIORITY HUNTS PAY MORE // ELITES ALERTED", 4.3f);
                    }
                    break;

                case EventKind.IronBridge:
                    if (choice == Choice.Safe)
                    {
                        support = 2;
                        _game.RepairEagle(3);
                        if (eagle != null)
                            eagle.InvulnerableUntil = Mathf.Max(eagle.InvulnerableUntil, Time.time + 3f);
                        if (player != null) player.AddAmmo(AmmoType.EMP, 2);
                        Announce("FORTIFY CROSSING // ORZELEK DEFENSE CORRIDOR ESTABLISHED", 4.0f);
                    }
                    else
                    {
                        assault = 2;
                        if (player != null)
                        {
                            player.AddAmmo(AmmoType.Explosive, 5);
                            player.AddAmmo(AmmoType.ArmorPiercing, 3);
                        }
                        WarEconomyDirector.AwardMissionBonds(6 + sector, "DEMOLITION RAID");
                        Announce("DEMOLITION RAID // SIEGE COLUMN DISRUPTED // COUNTERATTACK INBOUND", 4.4f);
                    }
                    break;

                default:
                    if (choice == Choice.Safe)
                    {
                        support = 2;
                        if (player != null && player.Health != null)
                        {
                            player.Health.Heal(3);
                            player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 2.5f);
                            ArmorSystem armor = player.GetComponent<ArmorSystem>();
                            if (armor != null) armor.RepairModules(40);
                        }
                        _game.RepairEagle(2);
                        Announce("FIELD HOSPITAL // SUSTAINMENT PRIORITY", 4.0f);
                    }
                    else
                    {
                        assault = 1;
                        intel = 1;
                        WarEconomyDirector.AwardMissionBonds(10 + sector, "NIGHT RAID");
                        if (player != null)
                        {
                            player.AddAmmo(AmmoType.Plasma, 3);
                            player.AddAmmo(AmmoType.Explosive, 3);
                        }
                        Announce("NIGHT RAID // MAXIMUM SPOILS // ELITE HUNTERS DEPLOYED", 4.4f);
                    }
                    break;
            }

            AddApproachScore("EventSupport", support);
            AddApproachScore("EventAssault", assault);
            AddApproachScore("EventIntel", intel);
        }

        private void AddApproachScore(string key, int amount)
        {
            if (amount <= 0) return;
            string full = $"TankRevival.{key}.{_runId}";
            PlayerPrefs.SetInt(full, PlayerPrefs.GetInt(full, 0) + amount);
        }

        private void ApplyRoundConsequence(int round, int sector)
        {
            if (sector <= 0 || sector >= 9) return;
            Choice choice = GetChoice(sector);
            if (choice == Choice.None) return;

            EventKind kind = KindFor(sector);
            int localRound = ((round - 1) % 10) + 1;
            PlayerTank player = CombatRoster.Player;

            if (choice == Choice.Safe)
            {
                if (kind == EventKind.BrokenArrow && localRound % 3 == 0)
                {
                    if (player != null && player.Health != null) player.Health.Heal(1);
                    _game.RepairEagle(1);
                }
                else if (kind == EventKind.BlackSignal && (localRound == 4 || localRound == 8))
                {
                    if (player != null) player.AddAmmo(AmmoType.EMP, 1);
                }
                else if (kind == EventKind.IronBridge && localRound % 3 == 0)
                {
                    _game.RepairEagle(1);
                }
                else if (kind == EventKind.LastLight && localRound % 4 == 0 && player != null)
                {
                    if (player.Health != null) player.Health.Heal(1);
                    ArmorSystem armor = player.GetComponent<ArmorSystem>();
                    if (armor != null) armor.RepairModules(12);
                }
            }
            else
            {
                if (kind == EventKind.BrokenArrow && localRound % 3 == 0 && player != null)
                    player.AddAmmo(AmmoType.Explosive, 1);
                else if (kind == EventKind.BlackSignal && localRound % 4 == 0 && player != null)
                    player.AddAmmo(AmmoType.ArmorPiercing, 1);
                else if (kind == EventKind.IronBridge && localRound % 3 == 0 && player != null)
                    player.AddAmmo(AmmoType.Explosive, 1);
                else if (kind == EventKind.LastLight && localRound % 5 == 0 && player != null)
                    player.AddAmmo(AmmoType.Plasma, 1);
            }
        }

        private void HookEnemies()
        {
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null) continue;
                int id = enemy.GetInstanceID();
                if (!_hooked.Add(id)) continue;
                EnemyTank captured = enemy;
                enemy.Health.Died += _ => OnEnemyDestroyed(captured);
            }
        }

        private void OnEnemyDestroyed(EnemyTank enemy)
        {
            if (enemy == null || _game == null || !_game.IsPlaying || _sector <= 0 || _sector >= 9) return;
            _sectorKills++;
            bool heavy = enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss;
            bool special = enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Sniper || enemy.Kind == EnemyKind.Supply || enemy.Kind == EnemyKind.Boss;
            if (heavy) _heavyKills++;
            if (special) _specialKills++;

            Choice choice = GetChoice(_sector);
            if (choice == Choice.None) return;
            EventKind kind = KindFor(_sector);

            if (choice == Choice.Risk)
            {
                if (kind == EventKind.BrokenArrow && heavy && _heavyKills % 4 == 0)
                {
                    WarEconomyDirector.AwardMissionBonds(3, "DEPOT RAID ARMOR KILL");
                    CombatRoster.Player?.AddAmmo(AmmoType.Explosive, 1);
                }
                else if (kind == EventKind.BlackSignal && special && _specialKills % 3 == 0)
                {
                    WarEconomyDirector.AwardMissionBonds(4, "DEEP RECON PRIORITY KILL");
                    CombatRoster.Player?.AddAmmo(AmmoType.ArmorPiercing, 1);
                }
                else if (kind == EventKind.IronBridge && heavy && _heavyKills % 3 == 0)
                {
                    CombatRoster.Player?.AddAmmo(AmmoType.Explosive, 1);
                    WarEconomyDirector.AwardMissionBonds(2, "DEMOLITION TARGET");
                }
                else if (kind == EventKind.LastLight && _sectorKills % 10 == 0)
                {
                    WarEconomyDirector.AwardMissionBonds(5, "NIGHT RAID STREAK");
                    CombatRoster.Player?.AddAmmo(AmmoType.Plasma, 1);
                }
            }
            else if (choice == Choice.Safe && _sectorKills > 0 && _sectorKills % 14 == 0)
            {
                PlayerTank player = CombatRoster.Player;
                if (player != null && player.Health != null) player.Health.Heal(1);
                _game.RepairEagle(1);
            }
        }

        private void ApplyRiskPressure()
        {
            if (_sector <= 0 || _sector >= 9 || GetChoice(_sector) != Choice.Risk) return;
            EventKind kind = KindFor(_sector);

            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                int id = enemy.GetInstanceID();
                if (!_reinforced.Add(id)) continue;

                int extra = 0;
                if (kind == EventKind.BrokenArrow && (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss))
                    extra = enemy.Kind == EnemyKind.Boss ? 2 : 1;
                else if (kind == EventKind.BlackSignal && (enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Sniper || enemy.Kind == EnemyKind.Boss))
                    extra = 1;
                else if (kind == EventKind.IronBridge && (enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Heavy))
                    extra = 1;
                else if (kind == EventKind.LastLight && (enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Boss))
                    extra = enemy.Kind == EnemyKind.Boss ? 2 : 1;

                if (extra > 0)
                    enemy.Health.SetMaximum(enemy.Health.Max + extra, true);
            }
        }

        private static string EventTitle(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.BrokenArrow: return "BROKEN ARROW";
                case EventKind.BlackSignal: return "BLACK SIGNAL";
                case EventKind.IronBridge: return "IRON BRIDGE";
                default: return "LAST LIGHT";
            }
        }

        private static string SafeTitle(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.BrokenArrow: return "4 // RESCUE CONVOY";
                case EventKind.BlackSignal: return "4 // JAM ENEMY NETWORK";
                case EventKind.IronBridge: return "4 // FORTIFY CROSSING";
                default: return "4 // FIELD HOSPITAL";
            }
        }

        private static string RiskTitle(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.BrokenArrow: return "5 // RAID FUEL DEPOT";
                case EventKind.BlackSignal: return "5 // DEEP RECON STRIKE";
                case EventKind.IronBridge: return "5 // DEMOLITION RAID";
                default: return "5 // NIGHT RAID";
            }
        }

        private static string SafeBody(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.BrokenArrow: return "Save allied logistics. Repairs and sustainment continue through the sector.";
                case EventKind.BlackSignal: return "Suppress enemy command traffic. Gain EMP stock and controlled intel rewards.";
                case EventKind.IronBridge: return "Turn the crossing into a defensive corridor for Orzelek.";
                default: return "Preserve combat strength with hull, armor and Eagle recovery.";
            }
        }

        private static string RiskBody(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.BrokenArrow: return "Hit enemy fuel reserves. Large payout and explosives, but heavy armor reinforces.";
                case EventKind.BlackSignal: return "Penetrate deep command lines. Priority kills pay more; elite response strengthens.";
                case EventKind.IronBridge: return "Demolish enemy approach routes. More explosive support, tougher siege counterattack.";
                default: return "Strike under darkness. Maximum spoils and plasma support against reinforced elite hunters.";
            }
        }

        private void Announce(string text, float duration)
        {
            _banner = text;
            _bannerUntil = Time.unscaledTime + duration;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(0.48f, 0.94f, 1f);
            _header = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _header.normal.textColor = Color.white;
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _body.normal.textColor = new Color(0.84f, 0.91f, 0.96f);
            _small = new GUIStyle(GUI.skin.label) { fontSize = 10 };
            _small.normal.textColor = new Color(0.60f, 0.72f, 0.80f);
            _safe = new GUIStyle(_header);
            _safe.normal.textColor = new Color(0.45f, 1f, 0.62f);
            _risk = new GUIStyle(_header);
            _risk.normal.textColor = new Color(1f, 0.48f, 0.26f);
            _center = new GUIStyle(_header) { alignment = TextAnchor.MiddleCenter };
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (_game == null || !_game.IsPlaying) return;

            if (Time.unscaledTime < _bannerUntil && !_choiceOpen)
            {
                float w = Mathf.Min(820f, Screen.width - 40f);
                GUI.color = new Color(0.006f, 0.018f, 0.034f, 0.95f);
                GUI.Box(new Rect((Screen.width - w) * 0.5f, 138f, w, 48f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect((Screen.width - w) * 0.5f + 10f, 149f, w - 20f, 26f), _banner, _center);
            }

            if (_sector >= 1)
            {
                Choice active = GetChoice(_sector);
                string eventStatus = active == Choice.None ? "PENDING" : active == Choice.Safe ? "STABILIZE" : "HIGH RISK";
                GUI.Label(new Rect(Screen.width - 330f, 52f, 315f, 18f), $"CAMPAIGN EVENT: {eventStatus}   //   ZERO FRONT: {CurrentApproach}", _small);
            }

            if (!_choiceOpen || _choiceSector < 0) return;

            EventKind kind = KindFor(_choiceSector);
            float wPanel = Mathf.Min(860f, Screen.width - 40f);
            float hPanel = 270f;
            float x = (Screen.width - wPanel) * 0.5f;
            float y = Mathf.Max(90f, Screen.height * 0.18f);

            GUI.depth = -260;
            GUI.color = new Color(0.004f, 0.012f, 0.024f, 0.985f);
            GUI.Box(new Rect(x, y, wPanel, hPanel), string.Empty);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 24f, y + 16f, wPanel - 48f, 32f), $"STRATEGIC EVENT // {EventTitle(kind)}", _title);
            GUI.Label(new Rect(x + 24f, y + 50f, wPanel - 48f, 20f), "Choose operational priority. 4 = stabilize front, 5 = accept escalation for greater reward.", _small);

            float half = (wPanel - 66f) * 0.5f;
            float left = x + 22f;
            float right = left + half + 22f;
            GUI.color = new Color(0.02f, 0.10f, 0.07f, 0.88f);
            GUI.Box(new Rect(left, y + 84f, half, 132f), string.Empty);
            GUI.color = new Color(0.12f, 0.045f, 0.025f, 0.90f);
            GUI.Box(new Rect(right, y + 84f, half, 132f), string.Empty);
            GUI.color = Color.white;

            GUI.Label(new Rect(left + 14f, y + 98f, half - 28f, 22f), SafeTitle(kind), _safe);
            GUI.Label(new Rect(left + 14f, y + 126f, half - 28f, 74f), SafeBody(kind), _body);
            GUI.Label(new Rect(right + 14f, y + 98f, half - 28f, 22f), RiskTitle(kind), _risk);
            GUI.Label(new Rect(right + 14f, y + 126f, half - 28f, 74f), RiskBody(kind), _body);

            float remain = Mathf.Max(0f, _choiceUntil - Time.unscaledTime);
            GUI.Label(new Rect(x + 24f, y + 230f, wPanel - 48f, 22f), $"AUTO-STABILIZE IN {remain:0.0}s   //   CURRENT MOMENTUM {StrategicWarMapDirector.Momentum}%", _center);
        }
    }
}
