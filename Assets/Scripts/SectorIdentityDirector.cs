using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum SectorDoctrine
    {
        BorderContact,
        BlitzCorridor,
        HunterGrounds,
        FortressBelt,
        ElectronicFront,
        ArtilleryBasin,
        ArmoredReserve,
        BrokenHighway,
        CommandDepth,
        FinalRedoubt
    }

    public enum EncounterDeck
    {
        Spearhead,
        Attrition,
        Disruption
    }

    public readonly struct SectorIdentityProfile
    {
        public readonly int Sector;
        public readonly SectorDoctrine Doctrine;
        public readonly EncounterDeck Deck;
        public readonly string Name;
        public readonly string Directive;
        public readonly float HealthMultiplier;
        public readonly float FireSupportMultiplier;
        public readonly float StrikeCadenceMultiplier;
        public readonly bool PreferChampion;
        public readonly bool PreferStrategicStrikes;

        public SectorIdentityProfile(int sector, SectorDoctrine doctrine, EncounterDeck deck, string name, string directive,
            float healthMultiplier, float fireSupportMultiplier, float strikeCadenceMultiplier,
            bool preferChampion, bool preferStrategicStrikes)
        {
            Sector = sector;
            Doctrine = doctrine;
            Deck = deck;
            Name = name;
            Directive = directive;
            HealthMultiplier = healthMultiplier;
            FireSupportMultiplier = fireSupportMultiplier;
            StrikeCadenceMultiplier = strikeCadenceMultiplier;
            PreferChampion = preferChampion;
            PreferStrategicStrikes = preferStrategicStrikes;
        }
    }

    [DefaultExecutionOrder(245)]
    public sealed class SectorIdentityDirector : MonoBehaviour
    {
        public const int SectorCount = 10;
        public const int DeckCount = 3;
        public const float MinHealthMultiplier = 0.94f;
        public const float MaxHealthMultiplier = 1.08f;
        public const float MinFireSupportMultiplier = 0.90f;
        public const float MaxFireSupportMultiplier = 1.12f;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo EncounterProfileField = typeof(CampaignEncounterDirector).GetField("_profile", PrivateInstance);
        private static readonly FieldInfo EncounterNextStrikeField = typeof(CampaignEncounterDirector).GetField("_nextStrike", PrivateInstance);

        private static SectorIdentityDirector _instance;
        private TankGame _game;
        private int _lastRound;
        private int _runSalt;
        private SectorIdentityProfile _active;
        private float _briefUntil;
        private GUIStyle _header;
        private GUIStyle _body;

        public static SectorIdentityDirector Instance => _instance;
        public SectorIdentityProfile ActiveProfile => _active;
        public static bool BridgeAvailable => EncounterProfileField != null && EncounterNextStrikeField != null;

        public static bool ConfigurationValid
        {
            get
            {
                if (!BridgeAvailable) return false;
                for (int sector = 0; sector < SectorCount; sector++)
                {
                    for (int deck = 0; deck < DeckCount; deck++)
                    {
                        SectorIdentityProfile p = ResolveSector(sector, (EncounterDeck)deck);
                        if (p.Sector != sector || p.HealthMultiplier < MinHealthMultiplier || p.HealthMultiplier > MaxHealthMultiplier) return false;
                        if (p.FireSupportMultiplier < MinFireSupportMultiplier || p.FireSupportMultiplier > MaxFireSupportMultiplier) return false;
                        if (p.StrikeCadenceMultiplier < 0.88f || p.StrikeCadenceMultiplier > 1.12f) return false;
                    }
                }
                return true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<SectorIdentityDirector>() != null) return;
            GameObject go = new GameObject("SectorIdentityDirector_v8_2");
            DontDestroyOnLoad(go);
            go.AddComponent<SectorIdentityDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _runSalt = Mathf.Abs(Environment.TickCount ^ DateTime.UtcNow.Millisecond) % 997;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                _lastRound = 0;
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round == _lastRound) return;

            if (_lastRound == 0)
                _runSalt = Mathf.Abs(Environment.TickCount ^ (round * 131)) % 997;

            _lastRound = round;
            int sector = Mathf.Clamp((round - 1) / 10, 0, 9);
            EncounterDeck deck = ResolveDeckForRun(sector, _runSalt);
            _active = ResolveSector(sector, deck);
            ApplyToEncounter(round, _active);
            _briefUntil = Time.unscaledTime + 3.2f;
        }

        public static EncounterDeck ResolveDeckForRun(int sector, int runSalt)
        {
            int normalized = Mathf.Abs(runSalt);
            return (EncounterDeck)((normalized + sector * 2 + (sector / 3)) % DeckCount);
        }

        public static SectorIdentityProfile ResolveSector(int sector, EncounterDeck deck)
        {
            sector = Mathf.Clamp(sector, 0, 9);
            float deckHealth = deck == EncounterDeck.Attrition ? 1.04f : deck == EncounterDeck.Spearhead ? 1.01f : 0.98f;
            float deckSupport = deck == EncounterDeck.Disruption ? 1.07f : deck == EncounterDeck.Spearhead ? 1.03f : 0.97f;
            float deckCadence = deck == EncounterDeck.Disruption ? 0.94f : deck == EncounterDeck.Spearhead ? 0.98f : 1.06f;
            bool champion = deck != EncounterDeck.Disruption;
            bool strikes = deck == EncounterDeck.Disruption;

            SectorDoctrine doctrine = (SectorDoctrine)sector;
            string name;
            string directive;
            float doctrineHealth = 1f;
            float doctrineSupport = 1f;

            switch (doctrine)
            {
                case SectorDoctrine.BorderContact:
                    name = "BORDER CONTACT"; directive = "READ THE FIELD // BREAK FIRST CONTACT"; doctrineHealth = 0.97f; doctrineSupport = 0.96f; break;
                case SectorDoctrine.BlitzCorridor:
                    name = "BLITZ CORRIDOR"; directive = "DENY FAST LANES // STOP THE SPEARHEAD"; doctrineHealth = 0.98f; doctrineSupport = 1.03f; break;
                case SectorDoctrine.HunterGrounds:
                    name = "HUNTER GROUNDS"; directive = "COUNTER FLANKERS // HUNT PRECISION TEAMS"; doctrineHealth = 0.99f; doctrineSupport = 1.02f; break;
                case SectorDoctrine.FortressBelt:
                    name = "FORTRESS BELT"; directive = "BREACH HEAVY SCREENS // HOLD ORZELEK"; doctrineHealth = 1.04f; doctrineSupport = 0.98f; break;
                case SectorDoctrine.ElectronicFront:
                    name = "ELECTRONIC FRONT"; directive = "BREAK EW NODES // USE COUNTERMEASURES"; doctrineHealth = 1.00f; doctrineSupport = 1.05f; break;
                case SectorDoctrine.ArtilleryBasin:
                    name = "ARTILLERY BASIN"; directive = "KEEP MOVING // DISRUPT FIRE SUPPORT"; doctrineHealth = 0.98f; doctrineSupport = 1.08f; break;
                case SectorDoctrine.ArmoredReserve:
                    name = "ARMORED RESERVE"; directive = "DEFEAT HEAVY COLUMNS // PRESERVE DEFENSES"; doctrineHealth = 1.06f; doctrineSupport = 1.00f; break;
                case SectorDoctrine.BrokenHighway:
                    name = "BROKEN HIGHWAY"; directive = "SURVIVE MIXED CONTACT // PROTECT THE ROUTE"; doctrineHealth = 1.02f; doctrineSupport = 1.04f; break;
                case SectorDoctrine.CommandDepth:
                    name = "COMMAND DEPTH"; directive = "HUNT RELAYS // COLLAPSE COMMAND"; doctrineHealth = 1.04f; doctrineSupport = 1.07f; strikes = true; break;
                default:
                    name = "FINAL REDOUBT"; directive = "BREAK THE LAST LINE // DEFEND ORZELEK"; doctrineHealth = 1.06f; doctrineSupport = 1.08f; champion = true; strikes = true; break;
            }

            return new SectorIdentityProfile(
                sector, doctrine, deck, name, directive,
                Mathf.Clamp(doctrineHealth * deckHealth, MinHealthMultiplier, MaxHealthMultiplier),
                Mathf.Clamp(doctrineSupport * deckSupport, MinFireSupportMultiplier, MaxFireSupportMultiplier),
                Mathf.Clamp(deckCadence, 0.88f, 1.12f), champion, strikes);
        }

        public static RoundEncounterProfile RefineEncounter(RoundEncounterProfile baseline, SectorIdentityProfile sector)
        {
            if (baseline.BossRound) return baseline;

            EncounterArchetype archetype = ResolveArchetype(baseline, sector);
            bool champion = baseline.ChampionEnabled || (sector.PreferChampion && baseline.SectorRound >= 5);
            bool strikes = baseline.StrategicStrikes || (sector.PreferStrategicStrikes && baseline.Round >= 35 && baseline.SectorRound >= 6);
            float health = Mathf.Clamp(baseline.HealthMultiplier * sector.HealthMultiplier, 0.84f, 1.42f);
            float support = Mathf.Clamp(baseline.FireSupportMultiplier * sector.FireSupportMultiplier, 0.82f, 1.48f);
            float cadence = Mathf.Clamp(baseline.StrikeCadence * sector.StrikeCadenceMultiplier, 4.6f, 14.5f);
            string code = sector.Name + " // " + DeckCode(sector.Deck);

            return new RoundEncounterProfile(
                baseline.Round, baseline.Sector, baseline.SectorRound, archetype, code, sector.Directive,
                health, support, cadence, champion, strikes, baseline.BossRound);
        }

        private static EncounterArchetype ResolveArchetype(RoundEncounterProfile baseline, SectorIdentityProfile sector)
        {
            if (baseline.SectorRound == 5) return EncounterArchetype.EliteHunt;
            if (baseline.SectorRound == 1) return baseline.Archetype;

            switch (sector.Doctrine)
            {
                case SectorDoctrine.BlitzCorridor: return sector.Deck == EncounterDeck.Attrition ? EncounterArchetype.ArmoredColumn : EncounterArchetype.Wolfpack;
                case SectorDoctrine.HunterGrounds: return sector.Deck == EncounterDeck.Spearhead ? EncounterArchetype.Wolfpack : EncounterArchetype.SniperNet;
                case SectorDoctrine.FortressBelt: return sector.Deck == EncounterDeck.Disruption ? EncounterArchetype.ArtilleryScreen : EncounterArchetype.SiegePush;
                case SectorDoctrine.ElectronicFront: return sector.Deck == EncounterDeck.Attrition ? EncounterArchetype.ArmoredColumn : EncounterArchetype.SniperNet;
                case SectorDoctrine.ArtilleryBasin: return sector.Deck == EncounterDeck.Spearhead ? EncounterArchetype.SiegePush : EncounterArchetype.ArtilleryScreen;
                case SectorDoctrine.ArmoredReserve: return sector.Deck == EncounterDeck.Disruption ? EncounterArchetype.ArtilleryScreen : EncounterArchetype.ArmoredColumn;
                case SectorDoctrine.BrokenHighway: return sector.Deck == EncounterDeck.Spearhead ? EncounterArchetype.Wolfpack : EncounterArchetype.SupplyInterdiction;
                case SectorDoctrine.CommandDepth: return sector.Deck == EncounterDeck.Attrition ? EncounterArchetype.SiegePush : EncounterArchetype.EliteHunt;
                case SectorDoctrine.FinalRedoubt: return baseline.SectorRound >= 7 ? EncounterArchetype.LastStand : EncounterArchetype.SiegePush;
                default: return sector.Deck == EncounterDeck.Spearhead ? EncounterArchetype.FrontlineAssault : baseline.Archetype;
            }
        }

        private static string DeckCode(EncounterDeck deck)
        {
            switch (deck)
            {
                case EncounterDeck.Spearhead: return "SPEARHEAD";
                case EncounterDeck.Attrition: return "ATTRITION";
                default: return "DISRUPTION";
            }
        }

        private static void ApplyToEncounter(int round, SectorIdentityProfile sector)
        {
            CampaignEncounterDirector director = CampaignEncounterDirector.Instance;
            if (director == null || EncounterProfileField == null) return;

            RoundEncounterProfile baseline = director.ActiveProfile.Round == round
                ? director.ActiveProfile
                : CampaignEncounterDirector.Resolve(round);
            RoundEncounterProfile refined = RefineEncounter(baseline, sector);
            EncounterProfileField.SetValue(director, refined);

            if (refined.StrategicStrikes && EncounterNextStrikeField != null)
                EncounterNextStrikeField.SetValue(director, Time.time + Mathf.Max(3.6f, refined.StrikeCadence * 0.74f));
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(0.45f, 0.88f, 1f);
            _body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            _body.normal.textColor = new Color(0.83f, 0.90f, 0.96f);
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || Time.unscaledTime > _briefUntil) return;
            EnsureStyles();
            float width = Mathf.Min(520f, Screen.width - 40f);
            Rect box = new Rect((Screen.width - width) * 0.5f, 82f, width, 48f);
            GUI.Label(new Rect(box.x, box.y, box.width, 22f), $"SECTOR {_active.Sector + 1}/10 // {_active.Name} // {DeckCode(_active.Deck)}", _header);
            GUI.Label(new Rect(box.x, box.y + 21f, box.width, 20f), _active.Directive, _body);
        }
    }
}
