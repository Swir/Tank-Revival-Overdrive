using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v3.5 COMMAND CENTER & CAMPAIGN UX CONSOLIDATION.
    /// A single interactive front-end over the existing War Garage, v3.1 loadouts,
    /// v3.2 weapon mastery, v3.4 Commander career and runtime fortress/economy state.
    /// It intentionally writes the same PlayerPrefs keys consumed by the authoritative systems;
    /// no parallel combat/progression state is introduced.
    /// </summary>
    [DefaultExecutionOrder(5000)]
    public sealed class CommandCenterDirector : MonoBehaviour
    {
        private const string ChassisKey = "TankRevival.Garage.Chassis";
        private const string CannonKey = "TankRevival.Garage.Cannon";
        private const string LoaderKey = "TankRevival.Garage.Loader";
        private const string EngineKey = "TankRevival.Garage.Engine";
        private const string ArmorKey = "TankRevival.Garage.Armor";
        private const string LoadoutSpentKey = "TankRevival.Garage.LoadoutSpent";
        private const string PrimaryUnlockKey = "TankRevival.Garage.Loadout.PrimaryUnlock";
        private const string ReactorUnlockKey = "TankRevival.Garage.Loadout.ReactorUnlock";
        private const string HullUnlockKey = "TankRevival.Garage.Loadout.HullUnlock";
        private const string PrimarySelectedKey = "TankRevival.Garage.Loadout.Primary";
        private const string ReactorSelectedKey = "TankRevival.Garage.Loadout.Reactor";
        private const string HullSelectedKey = "TankRevival.Garage.Loadout.Hull";
        private const string WeaponKey = "TankRevival.WeaponFamily.Selected";
        private const string WeaponXpPrefix = "TankRevival.WeaponFamily.XP.";
        private const string VanguardKey = "TankRevival.CommanderPerks.Vanguard";
        private const string EngineerKey = "TankRevival.CommanderPerks.Engineer";
        private const string LogisticsKey = "TankRevival.CommanderPerks.Logistics";

        private enum Page { Overview, Vehicle, Modules, Weapons, Commander, Campaign }

        public static CommandCenterDirector Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._open;

        private TankGame _game;
        private bool _open;
        private Page _page;
        private string _toast = string.Empty;
        private float _toastUntil;
        private Vector2 _scroll;

        private GUIStyle _header;
        private GUIStyle _subheader;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _good;
        private GUIStyle _warn;
        private GUIStyle _dim;
        private GUIStyle _button;
        private GUIStyle _selectedButton;
        private GUIStyle _metric;

        private static readonly string[] ChassisNames = { "ORZEL MK-I ASSAULT", "BASTION HEAVY", "WICHER SCOUT", "HUNTER TD" };
        private static readonly string[] ChassisRoles =
        {
            "Balanced assault / cannon + loader / Barrage Drive",
            "Heavy survival / armor / Iron Citadel",
            "High mobility / EMP / Ghost Overboost",
            "Tank destroyer / rail focus / Predator Lock"
        };
        private static readonly string[] PrimaryNames = { "STANDARD FEED", "VOLLEY FEED", "BREACH CORE" };
        private static readonly string[] ReactorNames = { "STANDARD REACTOR", "CAPACITOR BANK", "THERMAL SINK" };
        private static readonly string[] HullNames = { "STANDARD HULL", "REACTIVE PLATING", "REPAIR LATTICE" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CommandCenterDirector>() != null) return;
            var go = new GameObject("CommandCenterDirector_v3_5");
            DontDestroyOnLoad(go);
            go.AddComponent<CommandCenterDirector>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _open = !_open;
                _scroll = Vector2.zero;
                if (_open) BattleAudio.PlayGlobal(SoundCue.Pickup, 0.18f, 0.08f);
            }

            if (_open && Input.GetKeyDown(KeyCode.Escape))
                _open = false;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(0.48f, 0.92f, 1f);
            _subheader = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _subheader.normal.textColor = new Color(0.88f, 0.94f, 1f);
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _body.normal.textColor = new Color(0.84f, 0.90f, 0.96f);
            _small = new GUIStyle(_body) { fontSize = 10 };
            _small.normal.textColor = new Color(0.61f, 0.70f, 0.78f);
            _good = new GUIStyle(_body) { fontStyle = FontStyle.Bold };
            _good.normal.textColor = new Color(0.40f, 1f, 0.62f);
            _warn = new GUIStyle(_body) { fontStyle = FontStyle.Bold };
            _warn.normal.textColor = new Color(1f, 0.67f, 0.22f);
            _dim = new GUIStyle(_body);
            _dim.normal.textColor = new Color(0.47f, 0.52f, 0.57f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _button.normal.textColor = new Color(0.84f, 0.92f, 1f);
            _selectedButton = new GUIStyle(_button);
            _selectedButton.normal.textColor = new Color(0.36f, 1f, 0.64f);
            _metric = new GUIStyle(_subheader) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            _metric.normal.textColor = new Color(1f, 0.84f, 0.28f);
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (!_open)
            {
                GUI.Label(new Rect(Screen.width - 246f, 8f, 230f, 20f), "TAB // COMMAND CENTER", _small);
                return;
            }

            GUI.depth = -200;
            GUI.color = new Color(0.006f, 0.012f, 0.020f, 0.985f);
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), string.Empty);
            GUI.color = Color.white;

            float margin = 28f;
            GUI.Label(new Rect(margin, 18f, Screen.width - 56f, 34f), "ORZEŁ OVERDRIVE // COMMAND CENTER", _header);
            GUI.Label(new Rect(margin, 50f, Screen.width - 56f, 20f), _game != null && _game.IsPlaying
                ? $"LIVE CAMPAIGN // ROUND {_game.CurrentRound:000} // tactical data is read-only where configuration is deployment-locked"
                : "PRE-DEPLOYMENT // configure vehicle, modules, weapon family and Commander career in one place", _small);

            DrawTabs(margin, 82f);
            Rect content = new Rect(margin, 128f, Screen.width - margin * 2f, Screen.height - 172f);
            GUI.color = new Color(0.020f, 0.038f, 0.060f, 0.96f);
            GUI.Box(content, string.Empty);
            GUI.color = Color.white;

            Rect view = new Rect(content.x + 16f, content.y + 14f, content.width - 32f, content.height - 28f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, view.width - 22f, Mathf.Max(view.height - 8f, 620f)));
            switch (_page)
            {
                case Page.Vehicle: DrawVehicle(view.width - 36f); break;
                case Page.Modules: DrawModules(view.width - 36f); break;
                case Page.Weapons: DrawWeapons(view.width - 36f); break;
                case Page.Commander: DrawCommander(view.width - 36f); break;
                case Page.Campaign: DrawCampaign(view.width - 36f); break;
                default: DrawOverview(view.width - 36f); break;
            }
            GUI.EndScrollView();

            if (Time.unscaledTime < _toastUntil)
            {
                GUI.color = new Color(0.02f, 0.09f, 0.11f, 0.98f);
                GUI.Box(new Rect(Screen.width * 0.5f - 320f, Screen.height - 38f, 640f, 30f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 308f, Screen.height - 33f, 616f, 22f), _toast, _good);
            }
        }

        private void DrawTabs(float x, float y)
        {
            string[] names = { "OVERVIEW", "VEHICLE", "MODULES", "WEAPONS", "COMMANDER", "CAMPAIGN" };
            float available = Screen.width - x * 2f;
            float w = Mathf.Max(110f, available / names.Length - 7f);
            for (int i = 0; i < names.Length; i++)
            {
                Rect r = new Rect(x + i * (w + 6f), y, w, 32f);
                if (GUI.Button(r, names[i], _page == (Page)i ? _selectedButton : _button))
                {
                    _page = (Page)i;
                    _scroll = Vector2.zero;
                }
            }
        }

        private void DrawOverview(float width)
        {
            int chassis = SelectedChassis();
            PrimaryWeaponFamily family = SelectedFamily();
            int mastery = WeaponFamilyDirector.LevelForXp(WeaponXp(family));
            int marks = AvailableMarks();
            int career = CommanderCareerDirector.AvailablePoints;
            int relics = CommanderCareerDirector.UniqueBossRelics;

            GUI.Label(new Rect(8f, 4f, width, 26f), "DEPLOYMENT READINESS", _subheader);
            DrawMetric(new Rect(8f, 42f, 180f, 70f), "CHASSIS", ChassisNames[chassis]);
            DrawMetric(new Rect(200f, 42f, 210f, 70f), "PRIMARY", WeaponFamilyDirector.FamilyName(family));
            DrawMetric(new Rect(422f, 42f, 150f, 70f), "MASTERY", "LEVEL " + mastery);
            DrawMetric(new Rect(584f, 42f, 150f, 70f), "WAR BONDS", WarEconomyDirector.CurrentBonds.ToString());

            float y = 132f;
            GUI.Label(new Rect(8f, y, width, 22f), "CURRENT BUILD", _subheader);
            y += 29f;
            GUI.Label(new Rect(16f, y, width, 20f), $"Primary hardpoint: {PrimaryNames[(int)GarageLoadoutDirector.SelectedPrimary]}", _body); y += 21f;
            GUI.Label(new Rect(16f, y, width, 20f), $"Reactor bay: {ReactorNames[(int)GarageLoadoutDirector.SelectedReactor]}", _body); y += 21f;
            GUI.Label(new Rect(16f, y, width, 20f), $"Hull system: {HullNames[(int)GarageLoadoutDirector.SelectedHull]}", _body); y += 26f;
            GUI.Label(new Rect(16f, y, width, 20f), $"Garage Marks: {marks} free // Commander Points: {career} free // Legend Relics: {relics}/10", _good); y += 30f;

            GUI.Label(new Rect(8f, y, width, 22f), "SYNERGY ANALYSIS", _subheader); y += 28f;
            bool synergy = IsChassisFamilySynergy(chassis, family);
            GUI.Label(new Rect(16f, y, width, 20f), synergy
                ? "OPTIMAL // chassis specialization matches the selected weapon family."
                : "MIXED BUILD // viable, but the selected weapon family does not receive its chassis synergy bonus.", synergy ? _good : _warn); y += 23f;
            string moduleSynergy = ModuleSynergy(family);
            GUI.Label(new Rect(16f, y, width, 40f), moduleSynergy, _body); y += 48f;

            GUI.Label(new Rect(8f, y, width, 22f), "CAMPAIGN CAREER", _subheader); y += 27f;
            GUI.Label(new Rect(16f, y, width, 20f), $"Command Rank {MetaProgressionDirector.CurrentRank} // Vanguard {CommanderCareerDirector.VanguardLevel}/5 // Engineering {CommanderCareerDirector.EngineerLevel}/5 // Logistics {CommanderCareerDirector.LogisticsLevel}/5", _body); y += 22f;
            GUI.Label(new Rect(16f, y, width, 20f), $"Best sector {PlayerPrefs.GetInt("TankRevival.BestSector", 0)} // Contracts {PlayerPrefs.GetInt("TankRevival.ContractsCompleted", 0)} // Boss relics {relics}/10", _small); y += 32f;

            EagleFortressCommandDirector fortress = EagleFortressCommandDirector.Instance;
            if (_game != null && _game.IsPlaying && fortress != null)
            {
                GUI.Label(new Rect(8f, y, width, 22f), "LIVE ORZEŁ FORTRESS", _subheader); y += 27f;
                GUI.Label(new Rect(16f, y, width, 20f), $"{fortress.Doctrine.ToString().ToUpperInvariant()} L{fortress.DoctrineLevel} // {fortress.CommandCharges} command charges // sector {fortress.Sector:00}", _good);
            }
        }

        private void DrawVehicle(float width)
        {
            bool editable = !IsCampaignActive();
            GUI.Label(new Rect(8f, 4f, width, 24f), "ARMORED VEHICLE PLATFORM", _subheader);
            GUI.Label(new Rect(8f, 30f, width, 20f), editable ? $"{AvailableMarks()} Garage Marks available" : "Configuration locked during active campaign", editable ? _good : _warn);

            int selected = SelectedChassis();
            float y = 66f;
            for (int i = 0; i < 4; i++)
            {
                bool unlocked = ChassisUnlocked(i);
                Rect button = new Rect(8f, y, 245f, 34f);
                string suffix = selected == i ? "  [ACTIVE]" : unlocked ? string.Empty : $"  [LOCKED @ {ChassisUnlockCost(i)}]";
                if (GUI.Button(button, ChassisNames[i] + suffix, selected == i ? _selectedButton : _button) && editable)
                {
                    if (!unlocked) Announce("CHASSIS LOCKED // earn more Garage Marks");
                    else { PlayerPrefs.SetInt(ChassisKey, i); Save("ACTIVE CHASSIS // " + ChassisNames[i]); }
                }
                GUI.Label(new Rect(270f, y + 4f, width - 280f, 32f), ChassisRoles[i], unlocked ? _body : _dim);
                y += 44f;
            }

            y += 12f;
            GUI.Label(new Rect(8f, y, width, 24f), "PERMANENT VEHICLE UPGRADES", _subheader); y += 34f;
            string[] labels = { "CANNON", "AUTOLOADER", "ENGINE", "ARMOR" };
            string[] keys = { CannonKey, LoaderKey, EngineKey, ArmorKey };
            for (int i = 0; i < 4; i++)
            {
                int level = Mathf.Clamp(PlayerPrefs.GetInt(keys[i], 0), 0, 2);
                int cost = level <= 0 ? 2 : 4;
                bool max = level >= 2;
                Rect r = new Rect(8f + (i % 2) * 310f, y + (i / 2) * 58f, 290f, 42f);
                string text = $"{labels[i]}  L{level}/2" + (max ? "  MAX" : $"  // {cost} MARKS");
                if (GUI.Button(r, text, _button) && editable && !max)
                {
                    if (AvailableMarks() < cost) Announce($"{labels[i]} // NEED {cost} FREE MARKS");
                    else { PlayerPrefs.SetInt(keys[i], level + 1); Save($"{labels[i]} UPGRADED // LEVEL {level + 1}"); }
                }
            }
        }

        private void DrawModules(float width)
        {
            bool editable = !IsCampaignActive();
            GUI.Label(new Rect(8f, 4f, width, 24f), "WAR GARAGE MODULE BAY", _subheader);
            GUI.Label(new Rect(8f, 30f, width, 20f), editable ? $"Shared Garage Marks available: {AvailableMarks()}" : "Module changes locked until redeployment", editable ? _good : _warn);
            float y = 66f;
            y = DrawModuleRow("PRIMARY HARDPOINT", PrimaryNames, PrimaryUnlockKey, PrimarySelectedKey, y, editable, width);
            y = DrawModuleRow("REACTOR BAY", ReactorNames, ReactorUnlockKey, ReactorSelectedKey, y + 12f, editable, width);
            y = DrawModuleRow("HULL SYSTEM", HullNames, HullUnlockKey, HullSelectedKey, y + 12f, editable, width);
            y += 16f;
            GUI.Label(new Rect(8f, y, width, 50f), "Unlocks cost 3 marks for package II and 5 marks for package III. These marks are shared with Cannon/Loader/Engine/Armor upgrades, so every choice is a real build tradeoff.", _small);
        }

        private float DrawModuleRow(string label, string[] names, string unlockKey, string selectedKey, float y, bool editable, float width)
        {
            int unlocked = Mathf.Clamp(PlayerPrefs.GetInt(unlockKey, 0), 0, 2);
            int selected = Mathf.Clamp(PlayerPrefs.GetInt(selectedKey, 0), 0, unlocked);
            GUI.Label(new Rect(8f, y, width, 22f), $"{label} // {names[selected]}", _subheader);
            y += 28f;
            for (int i = 0; i < 3; i++)
            {
                bool available = i <= unlocked;
                Rect r = new Rect(8f + i * 220f, y, 205f, 34f);
                string text = names[i] + (selected == i ? " [ACTIVE]" : available ? string.Empty : " [LOCKED]");
                if (GUI.Button(r, text, selected == i ? _selectedButton : _button) && editable)
                {
                    if (available)
                    {
                        PlayerPrefs.SetInt(selectedKey, i);
                        Save(label + " // " + names[i]);
                    }
                    else if (i == unlocked + 1)
                    {
                        int cost = unlocked == 0 ? 3 : 5;
                        if (AvailableMarks() < cost) Announce(label + $" // NEED {cost} FREE MARKS");
                        else
                        {
                            PlayerPrefs.SetInt(LoadoutSpentKey, GarageLoadoutDirector.PersistentSpentMarks + cost);
                            PlayerPrefs.SetInt(unlockKey, i);
                            PlayerPrefs.SetInt(selectedKey, i);
                            Save(label + " UNLOCKED // " + names[i]);
                        }
                    }
                }
            }
            return y + 44f;
        }

        private void DrawWeapons(float width)
        {
            bool editable = !IsCampaignActive();
            PrimaryWeaponFamily selected = SelectedFamily();
            GUI.Label(new Rect(8f, 4f, width, 24f), "PRIMARY WEAPON FAMILIES & COMBAT MASTERY", _subheader);
            GUI.Label(new Rect(8f, 30f, width, 20f), editable ? "Select a family for the next deployment." : "Family locked for this deployment.", editable ? _good : _warn);
            float y = 68f;
            for (int i = 0; i < 4; i++)
            {
                PrimaryWeaponFamily family = (PrimaryWeaponFamily)i;
                int xp = WeaponXp(family);
                int level = WeaponFamilyDirector.LevelForXp(xp);
                int next = level >= 5 ? xp : WeaponFamilyDirector.NextThreshold(level);
                Rect r = new Rect(8f, y, 300f, 40f);
                if (GUI.Button(r, WeaponFamilyDirector.FamilyName(family) + (selected == family ? " [ACTIVE]" : string.Empty), selected == family ? _selectedButton : _button) && editable)
                {
                    PlayerPrefs.SetInt(WeaponKey, i);
                    Save("PRIMARY FAMILY // " + WeaponFamilyDirector.FamilyName(family));
                }
                string progress = level >= 5 ? $"MASTERY L5 // {xp} XP // MAX" : $"MASTERY L{level} // {xp}/{next} XP";
                GUI.Label(new Rect(326f, y + 2f, width - 336f, 20f), progress, level >= 5 ? _good : _body);
                GUI.Label(new Rect(326f, y + 20f, width - 336f, 20f), FamilyRole(family), _small);
                y += 54f;
            }
            y += 8f;
            GUI.Label(new Rect(8f, y, width, 24f), "BUILD COMPATIBILITY", _subheader); y += 29f;
            GUI.Label(new Rect(16f, y, width, 22f), IsChassisFamilySynergy(SelectedChassis(), selected) ? "CHASSIS SYNERGY // ONLINE" : "CHASSIS SYNERGY // OFF", IsChassisFamilySynergy(SelectedChassis(), selected) ? _good : _warn); y += 22f;
            GUI.Label(new Rect(16f, y, width, 42f), ModuleSynergy(selected), _body);
        }

        private void DrawCommander(float width)
        {
            bool editable = !IsCampaignActive();
            GUI.Label(new Rect(8f, 4f, width, 24f), "COMMANDER CAREER", _subheader);
            GUI.Label(new Rect(8f, 30f, width, 20f), $"Rank {MetaProgressionDirector.CurrentRank} // {CommanderCareerDirector.AvailablePoints} free points // {CommanderCareerDirector.UniqueBossRelics}/10 Legend Relics", _good);
            float y = 70f;
            y = DrawCareerTrack("VANGUARD DOCTRINE", VanguardKey, CommanderCareerDirector.VanguardLevel,
                "Kills feed ammo sustain; high tiers unlock momentum shields and EMP combat surges.", y, editable, width);
            y = DrawCareerTrack("COMBAT ENGINEERING", EngineerKey, CommanderCareerDirector.EngineerLevel,
                "Hull capacity, module servicing, Orzeł repairs and a once-per-round critical failsafe.", y + 10f, editable, width);
            y = DrawCareerTrack("LOGISTICS COMMAND", LogisticsKey, CommanderCareerDirector.LogisticsLevel,
                "War Bond discounts, improved survival payouts and recurring ordnance support.", y + 10f, editable, width);
            y += 20f;
            GUI.Label(new Rect(8f, y, width, 46f), "Commander points are finite. New points come from Command Rank and unique boss relics, so specializing one doctrine delays the others.", _small);
        }

        private float DrawCareerTrack(string label, string key, int level, string description, float y, bool editable, float width)
        {
            GUI.Label(new Rect(8f, y, width, 22f), $"{label} // L{level}/5", _subheader);
            GUI.Label(new Rect(16f, y + 24f, width - 240f, 38f), description, _body);
            if (level < 5)
            {
                Rect r = new Rect(width - 205f, y + 18f, 190f, 38f);
                if (GUI.Button(r, "SPEND 1 COMMAND POINT", _button) && editable)
                {
                    if (CommanderCareerDirector.AvailablePoints <= 0) Announce("NO COMMANDER POINTS AVAILABLE");
                    else { PlayerPrefs.SetInt(key, level + 1); Save(label + $" // LEVEL {level + 1}"); }
                }
            }
            else GUI.Label(new Rect(width - 185f, y + 28f, 170f, 24f), "MASTERED", _good);
            return y + 70f;
        }

        private void DrawCampaign(float width)
        {
            GUI.Label(new Rect(8f, 4f, width, 24f), "CAMPAIGN OPERATIONS", _subheader);
            int round = _game != null ? _game.CurrentRound : 0;
            int sector = round > 0 ? Mathf.Clamp((round - 1) / 10 + 1, 1, 10) : PlayerPrefs.GetInt("TankRevival.BestSector", 0);
            GUI.Label(new Rect(8f, 36f, width, 22f), _game != null && _game.IsPlaying ? $"ACTIVE ROUND {round:000} // SECTOR {sector:00}" : $"CAREER BEST SECTOR {sector:00}", _good);
            GUI.Label(new Rect(8f, 64f, width, 20f), $"War Bonds: {WarEconomyDirector.CurrentBonds} // Contracts completed: {PlayerPrefs.GetInt("TankRevival.ContractsCompleted", 0)} // Boss relics: {CommanderCareerDirector.UniqueBossRelics}/10", _body);
            GUI.Label(new Rect(8f, 92f, width, 20f), $"Logistics discount: -{CommanderCareerDirector.EconomyDiscount} // survival payout x{CommanderCareerDirector.WarBondRewardMultiplier:0.00}", _body);

            float y = 132f;
            EagleFortressCommandDirector fortress = EagleFortressCommandDirector.Instance;
            GUI.Label(new Rect(8f, y, width, 24f), "ORZEŁ FORTRESS STATUS", _subheader); y += 30f;
            if (fortress == null || _game == null || !_game.IsPlaying)
            {
                GUI.Label(new Rect(16f, y, width, 42f), "Fortress doctrine is selected at the start of each live sector. Command Center will report doctrine level, charges and bonuses once deployed.", _small);
                y += 52f;
            }
            else
            {
                GUI.Label(new Rect(16f, y, width, 20f), $"Doctrine {fortress.Doctrine.ToString().ToUpperInvariant()} L{fortress.DoctrineLevel} // Charges {fortress.CommandCharges} // Sector {fortress.Sector:00}", _good); y += 22f;
                GUI.Label(new Rect(16f, y, width, 20f), $"Module bonus +{fortress.FortressModuleBonus} // sentry dmg +{fortress.SentryDamageBonus} // range +{fortress.SentryRangeBonus:0.0} // repair +{fortress.RepairBonus}", _body); y += 30f;
            }

            GUI.Label(new Rect(8f, y, width, 24f), "OPERATIONAL CONTROL MAP", _subheader); y += 30f;
            GUI.Label(new Rect(16f, y, width, 100f), "PRE-DEPLOYMENT", _good); y += 22f;
            GUI.Label(new Rect(32f, y, width, 20f), "Command Center: TAB // configure chassis, modules, primary weapon and Commander perks", _body); y += 22f;
            GUI.Label(new Rect(16f, y, width, 100f), "LIVE COMBAT", _warn); y += 22f;
            GUI.Label(new Rect(32f, y, width, 20f), "Arsenal: R secondary / SHIFT chassis ability / G Overdrive", _body); y += 20f;
            GUI.Label(new Rect(32f, y, width, 20f), "Fortress: F1-F3 doctrine at sector start / F4 upgrade / F5-F7 commands", _body); y += 20f;
            GUI.Label(new Rect(32f, y, width, 20f), "War Economy: F1 service / F2 ordnance / F3 insurance (after doctrine lock)", _body); y += 20f;
            GUI.Label(new Rect(32f, y, width, 20f), "Special ammo: 1-7 / cycle Q-E", _body);
        }

        private void DrawMetric(Rect rect, string label, string value)
        {
            GUI.color = new Color(0.028f, 0.055f, 0.080f, 0.95f);
            GUI.Box(rect, string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 7f, rect.width - 16f, 18f), label, _small);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 28f, rect.width - 16f, 30f), value, _metric);
        }

        private bool IsCampaignActive() => _game != null && _game.IsPlaying;
        private int SelectedChassis() => Mathf.Clamp(PlayerPrefs.GetInt(ChassisKey, 0), 0, 3);
        private PrimaryWeaponFamily SelectedFamily() => (PrimaryWeaponFamily)Mathf.Clamp(PlayerPrefs.GetInt(WeaponKey, 0), 0, 3);
        private int WeaponXp(PrimaryWeaponFamily family) => Mathf.Max(0, PlayerPrefs.GetInt(WeaponXpPrefix + (int)family, 0));

        private static int LifetimeMarks()
        {
            return Mathf.Max(0, PlayerPrefs.GetInt("TankRevival.ContractsCompleted", 0)
                + PlayerPrefs.GetInt("TankRevival.BossLegendsDefeated", 0) * 3
                + PlayerPrefs.GetInt("TankRevival.BestSector", 0) * 2);
        }

        private static int UpgradeSpent(int level)
        {
            if (level <= 0) return 0;
            return level == 1 ? 2 : 6;
        }

        private static int LegacySpent()
        {
            return UpgradeSpent(PlayerPrefs.GetInt(CannonKey, 0)) + UpgradeSpent(PlayerPrefs.GetInt(LoaderKey, 0))
                + UpgradeSpent(PlayerPrefs.GetInt(EngineKey, 0)) + UpgradeSpent(PlayerPrefs.GetInt(ArmorKey, 0));
        }

        private static int AvailableMarks() => Mathf.Max(0, LifetimeMarks() - LegacySpent() - GarageLoadoutDirector.PersistentSpentMarks);
        private static int ChassisUnlockCost(int chassis) => chassis == 1 ? 4 : chassis == 2 ? 8 : chassis == 3 ? 14 : 0;
        private static bool ChassisUnlocked(int chassis) => LifetimeMarks() >= ChassisUnlockCost(chassis);

        private static bool IsChassisFamilySynergy(int chassis, PrimaryWeaponFamily family)
        {
            return (chassis == 0 && family == PrimaryWeaponFamily.Autocannon)
                || (chassis == 1 && family == PrimaryWeaponFamily.HeavyCannon)
                || (chassis == 2 && family == PrimaryWeaponFamily.PlasmaRepeater)
                || (chassis == 3 && family == PrimaryWeaponFamily.Railgun);
        }

        private static string ModuleSynergy(PrimaryWeaponFamily family)
        {
            GaragePrimaryPackage primary = GarageLoadoutDirector.SelectedPrimary;
            GarageReactorPackage reactor = GarageLoadoutDirector.SelectedReactor;
            if (family == PrimaryWeaponFamily.Autocannon && primary == GaragePrimaryPackage.VolleyFeed)
                return "STRONG SYNERGY // Volley Feed accelerates the Autocannon escort-shot cadence.";
            if (family == PrimaryWeaponFamily.Railgun && primary == GaragePrimaryPackage.BreachCore)
                return "STRONG SYNERGY // Breach Core adds damage to Railgun AP shots and periodic AP escorts.";
            if (family == PrimaryWeaponFamily.PlasmaRepeater && reactor == GarageReactorPackage.ThermalSink)
                return "STRONG SYNERGY // Thermal Sink offsets the Plasma Repeater's heat pressure and rewards reactor lock recovery.";
            if (reactor == GarageReactorPackage.CapacitorBank)
                return "OVERDRIVE SYNERGY // Capacitor Bank reinforces any family with plasma reserve and ignition protection.";
            return "GENERALIST MODULE PACKAGE // no direct family combo; utility remains fully active.";
        }

        private static string FamilyRole(PrimaryWeaponFamily family)
        {
            switch (family)
            {
                case PrimaryWeaponFamily.Autocannon: return "HIGH ROF // ESCORT BURSTS // close-mid pressure";
                case PrimaryWeaponFamily.HeavyCannon: return "HE SPLASH // BREAKTHROUGH // slow cadence";
                case PrimaryWeaponFamily.Railgun: return "AP PENETRATION // VELOCITY // precision damage";
                case PrimaryWeaponFamily.PlasmaRepeater: return "MULTI-PEN // HEAT PRESSURE // sustained energy fire";
                default: return string.Empty;
            }
        }

        private void Save(string message)
        {
            PlayerPrefs.Save();
            Announce(message);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.25f, 0.05f);
        }

        private void Announce(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 2.6f;
        }
    }
}
