using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum DeceptionAxis
    {
        West = 0,
        Center = 1,
        East = 2
    }

    public enum DeceptionWarPhase
    {
        None,
        Masking,
        MainEffort
    }

    [DefaultExecutionOrder(545)]
    public sealed class HighCommandDeceptionWarDirector : MonoBehaviour
    {
        public const int MinimumOperationRound = 45;
        public const int PlanningOffset = 5;
        public const int AssaultStartOffset = 6;
        public const int AssaultEndOffset = 7;
        public const int MaxCommittedCombatants = 4;
        public const int MaxFeintCombatants = 1;
        public const int MaxSupportShells = 2;
        public const float DirectiveRefreshCadence = 0.85f;
        public const float SupportFireCadence = 6.25f;
        public const float DirectiveLifetime = 1.45f;
        public const float IntelPulseCadence = 0.95f;

        private static HighCommandDeceptionWarDirector _instance;
        private readonly List<EnemyTank> _mainCommitted = new List<EnemyTank>(MaxCommittedCombatants);
        private readonly List<EnemyTank> _feintCommitted = new List<EnemyTank>(MaxFeintCombatants);
        private TankGame _game;
        private int _round = -1;
        private int _planSector = -1;
        private EnemyCounterDoctrine _doctrine;
        private DeceptionAxis _mainAxis;
        private DeceptionAxis _feintAxis;
        private bool _intelligenceConfirmed;
        private int _reserveCommitmentCap;
        private float _nextDirectiveRefresh;
        private float _nextSupportFire;
        private float _nextIntelPulse;
        private Transform _mainAnchor;
        private Transform _feintAnchor;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        public static HighCommandDeceptionWarDirector Instance => _instance;
        public DeceptionAxis MainAxis => _mainAxis;
        public DeceptionAxis FeintAxis => _feintAxis;
        public bool IntelligenceConfirmed => _intelligenceConfirmed;
        public EnemyCounterDoctrine ActiveDoctrine => _doctrine;
        public int ReserveCommitmentCap => _reserveCommitmentCap;
        public int MainCommittedCount => _mainCommitted.Count;
        public int FeintCommittedCount => _feintCommitted.Count;
        public DeceptionWarPhase Phase => ResolvePhase(_round);

        public static bool ConfigurationValid =>
            MinimumOperationRound >= 40 && MinimumOperationRound <= 60 &&
            PlanningOffset >= 4 && PlanningOffset < AssaultStartOffset &&
            AssaultStartOffset <= AssaultEndOffset && AssaultEndOffset <= 8 &&
            MaxCommittedCombatants >= 3 && MaxCommittedCombatants <= 4 &&
            MaxFeintCombatants == 1 && MaxSupportShells <= 2 &&
            DirectiveRefreshCadence >= 0.65f && DirectiveRefreshCadence <= 1.20f &&
            SupportFireCadence >= 5f && SupportFireCadence <= 8f &&
            DirectiveLifetime > DirectiveRefreshCadence &&
            IntelPulseCadence >= 0.70f && IntelPulseCadence <= 1.30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<HighCommandDeceptionWarDirector>() != null) return;
            GameObject go = new GameObject("HighCommandDeceptionWarDirector_v10_4");
            DontDestroyOnLoad(go);
            go.AddComponent<HighCommandDeceptionWarDirector>();
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
        }

        private void OnDestroy()
        {
            CleanupAnchors();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetRun();
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round != _round) BeginRound(round);
            if (!HasOperationForRound(round)) return;

            EnsurePlan(round);
            ObserveCounterIntelligence();

            if (Time.time >= _nextIntelPulse)
            {
                _nextIntelPulse = Time.time + IntelPulseCadence;
                PresentAxisSignals();
            }

            if (Time.time >= _nextDirectiveRefresh)
            {
                _nextDirectiveRefresh = Time.time + DirectiveRefreshCadence;
                ApplyTwoAxisOperation(round);
            }

            if (ResolvePhase(round) == DeceptionWarPhase.MainEffort && Time.time >= _nextSupportFire)
            {
                _nextSupportFire = Time.time + SupportFireCadence;
                ExecuteBoundedSupportBeat(round);
            }
        }

        private void BeginRound(int round)
        {
            _round = round;
            _mainCommitted.Clear();
            _feintCommitted.Clear();
            _nextDirectiveRefresh = Time.time + 0.15f;
            _nextSupportFire = Time.time + 1.2f;
            _nextIntelPulse = Time.time + 0.2f;

            if (!HasOperationForRound(round))
            {
                if ((round - 1) % 10 > AssaultEndOffset)
                {
                    _planSector = -1;
                    _intelligenceConfirmed = false;
                    CleanupAnchors();
                }
                return;
            }

            EnsurePlan(round);
        }

        private void EnsurePlan(int round)
        {
            int sector = SectorForRound(round);
            AdaptiveEnemyHighCommandDirector high = AdaptiveEnemyHighCommandDirector.Instance;
            EnemyCounterDoctrine doctrine = high != null ? high.ActiveCounterDoctrine : EnemyCounterDoctrine.None;
            if (doctrine == EnemyCounterDoctrine.None)
                doctrine = ResolveFallbackDoctrine(sector);

            if (_planSector == sector && _mainAnchor != null && _feintAnchor != null)
            {
                _doctrine = doctrine;
                RefreshReserveCap();
                return;
            }

            _planSector = sector;
            _doctrine = doctrine;
            ResolveAxes(round, doctrine, out _mainAxis, out _feintAxis);
            _intelligenceConfirmed = false;
            RefreshReserveCap();
            BuildAnchors();
            PresentAxisSignals();
        }

        private void RefreshReserveCap()
        {
            StrategicReserveAttritionDirector reserves = StrategicReserveAttritionDirector.Instance;
            _reserveCommitmentCap = reserves != null
                ? ResolveReserveCommitmentCap(reserves.Current, _doctrine)
                : 2;
        }

        private void ObserveCounterIntelligence()
        {
            if (_intelligenceConfirmed) return;
            CommandNetworkHuntDirector intelligence = CommandNetworkHuntDirector.Instance;
            if (intelligence == null || !intelligence.TargetsRevealed) return;
            _intelligenceConfirmed = true;
            PresentAxisSignals();
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.28f, 0.08f);
        }

        private void ApplyTwoAxisOperation(int round)
        {
            _mainCommitted.Clear();
            _feintCommitted.Clear();
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return;

            DeceptionWarPhase phase = ResolvePhase(round);
            int totalCap = Mathf.Clamp(_reserveCommitmentCap, 2, MaxCommittedCombatants);
            int feintCap = MaxFeintCombatants;
            int mainCap = phase == DeceptionWarPhase.MainEffort ? Mathf.Max(1, totalCap - feintCap) : 0;
            if (_intelligenceConfirmed && mainCap > 1) mainCap--;

            if (phase == DeceptionWarPhase.Masking)
            {
                CommitFeint(enemies, feintCap);
                return;
            }

            CommitMain(enemies, mainCap);
            CommitFeint(enemies, Mathf.Min(feintCap, MaxCommittedCombatants - _mainCommitted.Count));
        }

        private void CommitMain(EnemyTank[] enemies, int cap)
        {
            for (int pass = 0; pass < 2 && _mainCommitted.Count < cap; pass++)
            {
                for (int i = 0; i < enemies.Length && _mainCommitted.Count < cap; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (!IsEligible(enemy) || _mainCommitted.Contains(enemy) || _feintCommitted.Contains(enemy)) continue;
                    bool preferred = IsPreferredForDoctrine(enemy.Kind, _doctrine);
                    if ((pass == 0 && !preferred) || (pass == 1 && preferred)) continue;
                    if (pass == 1 && enemy.Kind == EnemyKind.Basic) continue;
                    _mainCommitted.Add(enemy);
                    IssueAxisDirective(enemy, _mainAnchor, false, _mainCommitted.Count - 1);
                }
            }
        }

        private void CommitFeint(EnemyTank[] enemies, int cap)
        {
            for (int i = 0; i < enemies.Length && _feintCommitted.Count < cap; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsEligible(enemy) || _mainCommitted.Contains(enemy) || _feintCommitted.Contains(enemy)) continue;
                if (enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Heavy) continue;
                _feintCommitted.Add(enemy);
                IssueAxisDirective(enemy, _feintAnchor, true, _feintCommitted.Count - 1);
            }

            if (_feintCommitted.Count >= cap) return;
            for (int i = 0; i < enemies.Length && _feintCommitted.Count < cap; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsEligible(enemy) || _mainCommitted.Contains(enemy) || _feintCommitted.Contains(enemy)) continue;
                _feintCommitted.Add(enemy);
                IssueAxisDirective(enemy, _feintAnchor, true, _feintCommitted.Count - 1);
            }
        }

        private void IssueAxisDirective(EnemyTank enemy, Transform anchor, bool feint, int index)
        {
            if (enemy == null || anchor == null) return;
            HighCommandNavigationDirective directive = enemy.GetComponent<HighCommandNavigationDirective>();
            if (directive == null) directive = enemy.gameObject.AddComponent<HighCommandNavigationDirective>();

            SquadTacticalRole role;
            float standoff;
            float speed;
            if (feint)
            {
                role = index % 2 == 0 ? SquadTacticalRole.Flanker : SquadTacticalRole.Suppressor;
                standoff = enemy.Kind == EnemyKind.Sniper ? 5.4f : 2.4f;
                speed = 0.92f;
            }
            else
            {
                switch (_doctrine)
                {
                    case EnemyCounterDoctrine.ArmorTrap:
                        role = enemy.Kind == EnemyKind.Sniper ? SquadTacticalRole.Suppressor : SquadTacticalRole.Hunter;
                        standoff = enemy.Kind == EnemyKind.Sniper ? 5.8f : 2.5f;
                        speed = enemy.Kind == EnemyKind.Heavy ? 0.88f : 1.0f;
                        break;
                    case EnemyCounterDoctrine.DispersedLogistics:
                        role = SquadTacticalRole.Escort;
                        standoff = 2.2f;
                        speed = 0.94f;
                        break;
                    default:
                        role = SquadTacticalRole.Breaker;
                        standoff = enemy.Kind == EnemyKind.Siege ? 2.0f : 1.25f;
                        speed = enemy.Kind == EnemyKind.Heavy ? 0.92f : 1.04f;
                        break;
                }
            }

            directive.Configure(_game, role, HighCommandObjective.FixedTarget, anchor, standoff, speed, Time.time + DirectiveLifetime);
        }

        private void ExecuteBoundedSupportBeat(int round)
        {
            if (_mainCommitted.Count == 0) return;
            StrategicReserveAttritionDirector reserves = StrategicReserveAttritionDirector.Instance;
            StrategicReserveSnapshot snapshot = reserves != null ? reserves.Current : default;
            int shellCap = ResolveSupportShellCap(snapshot, _intelligenceConfirmed, reserves != null);
            if (shellCap <= 0) return;

            Vector2 target = _doctrine == EnemyCounterDoctrine.SiegeBreach ? _game.BasePosition : _game.PlayerPosition;
            AmmoType ammo = _doctrine == EnemyCounterDoctrine.SiegeBreach ? AmmoType.Explosive : AmmoType.ArmorPiercing;
            float speed = ammo == AmmoType.Explosive ? 8.9f : 10.4f;
            Color color = AmmoDatabase.Color(ammo);
            int fired = 0;

            for (int i = 0; i < _mainCommitted.Count && fired < shellCap; i++)
            {
                EnemyTank enemy = _mainCommitted[i];
                if (!IsEligible(enemy)) continue;
                Vector2 origin = enemy.transform.position;
                Vector2 delta = target - origin;
                if (delta.sqrMagnitude < 0.36f) continue;
                int damage = _doctrine == EnemyCounterDoctrine.SiegeBreach && round >= 75 && !_intelligenceConfirmed ? 2 : 1;
                _game.SpawnProjectile(origin + delta.normalized * 0.58f, delta.normalized, Team.Enemy, damage, speed, color, ammo);
                fired++;
            }
        }

        private void BuildAnchors()
        {
            CleanupAnchors();
            GameObject main = new GameObject("HIGH_COMMAND_MAIN_AXIS_" + _mainAxis.ToString().ToUpperInvariant());
            main.transform.SetParent(transform, false);
            main.transform.position = DynamicFrontlineTerritoryDirector.LanePosition((int)_mainAxis);
            _mainAnchor = main.transform;

            GameObject feint = new GameObject("HIGH_COMMAND_FEINT_AXIS_" + _feintAxis.ToString().ToUpperInvariant());
            feint.transform.SetParent(transform, false);
            feint.transform.position = DynamicFrontlineTerritoryDirector.LanePosition((int)_feintAxis);
            _feintAnchor = feint.transform;
        }

        private void PresentAxisSignals()
        {
            if (_mainAnchor == null || _feintAnchor == null) return;
            Color masked = new Color(1f, 0.34f, 0.12f, 0.82f);
            Color main = _intelligenceConfirmed ? new Color(1f, 0.08f, 0.08f, 0.94f) : masked;
            Color feint = _intelligenceConfirmed ? new Color(0.68f, 0.42f, 1f, 0.58f) : masked;
            VisualFactory.RingPulse(_mainAnchor.position, main, _intelligenceConfirmed ? 1.45f : 1.05f);
            VisualFactory.RingPulse(_feintAnchor.position, feint, 1.05f);
        }

        private void CleanupAnchors()
        {
            if (_mainAnchor != null) Destroy(_mainAnchor.gameObject);
            if (_feintAnchor != null) Destroy(_feintAnchor.gameObject);
            _mainAnchor = null;
            _feintAnchor = null;
        }

        private void ResetRun()
        {
            CleanupAnchors();
            _round = -1;
            _planSector = -1;
            _doctrine = EnemyCounterDoctrine.None;
            _intelligenceConfirmed = false;
            _reserveCommitmentCap = 0;
            _mainCommitted.Clear();
            _feintCommitted.Clear();
        }

        public static bool HasOperationForRound(int round)
        {
            if (round < MinimumOperationRound || round > 100 || round % 10 == 0) return false;
            int offset = (round - 1) % 10;
            return offset >= PlanningOffset && offset <= AssaultEndOffset;
        }

        public static DeceptionWarPhase ResolvePhase(int round)
        {
            if (!HasOperationForRound(round)) return DeceptionWarPhase.None;
            int offset = (round - 1) % 10;
            return offset == PlanningOffset ? DeceptionWarPhase.Masking : DeceptionWarPhase.MainEffort;
        }

        public static int SectorForRound(int round) => Mathf.Clamp((round - 1) / 10, 0, 9);

        public static void ResolveAxes(int round, EnemyCounterDoctrine doctrine, out DeceptionAxis main, out DeceptionAxis feint)
        {
            int sector = SectorForRound(round);
            int doctrineSeed = doctrine == EnemyCounterDoctrine.None ? 1 : (int)doctrine;
            int mainIndex = Mathf.Abs((sector * 2 + doctrineSeed + 1) % DynamicFrontlineTerritoryDirector.LaneCount);
            int offset = (sector & 1) == 0 ? 1 : 2;
            int feintIndex = (mainIndex + offset) % DynamicFrontlineTerritoryDirector.LaneCount;
            main = (DeceptionAxis)mainIndex;
            feint = (DeceptionAxis)feintIndex;
        }

        public static int ResolveReserveCommitmentCap(StrategicReserveSnapshot snapshot, EnemyCounterDoctrine doctrine)
        {
            float ratio;
            switch (doctrine)
            {
                case EnemyCounterDoctrine.ArmorTrap:
                    ratio = snapshot.ArmorRatio;
                    break;
                case EnemyCounterDoctrine.DispersedLogistics:
                    ratio = snapshot.ElectronicWarfareRatio;
                    break;
                case EnemyCounterDoctrine.SiegeBreach:
                    ratio = snapshot.FireSupportRatio;
                    break;
                default:
                    ratio = Mathf.Max(snapshot.ArmorRatio, Mathf.Max(snapshot.FireSupportRatio, snapshot.ElectronicWarfareRatio));
                    break;
            }

            if (ratio >= 0.72f) return 4;
            if (ratio >= 0.45f) return 3;
            return 2;
        }

        public static int ResolveEffectiveMainCap(int reserveCap, bool intelligenceConfirmed)
        {
            int main = Mathf.Max(1, Mathf.Clamp(reserveCap, 2, MaxCommittedCombatants) - MaxFeintCombatants);
            if (intelligenceConfirmed && main > 1) main--;
            return main;
        }

        public static int ResolveSupportShellCap(StrategicReserveSnapshot snapshot, bool intelligenceConfirmed, bool reserveSystemPresent = true)
        {
            int cap;
            if (!reserveSystemPresent) cap = 1;
            else if (snapshot.FireSupportRatio <= StrategicReserveAttritionDirector.CriticalReserveThreshold) cap = 0;
            else if (snapshot.FireSupportRatio <= StrategicReserveAttritionDirector.LowReserveThreshold) cap = 1;
            else cap = MaxSupportShells;
            if (intelligenceConfirmed && cap > 0) cap--;
            return Mathf.Clamp(cap, 0, MaxSupportShells);
        }

        private static EnemyCounterDoctrine ResolveFallbackDoctrine(int sector)
        {
            switch (sector % 3)
            {
                case 0: return EnemyCounterDoctrine.ArmorTrap;
                case 1: return EnemyCounterDoctrine.DispersedLogistics;
                default: return EnemyCounterDoctrine.SiegeBreach;
            }
        }

        private static bool IsEligible(EnemyTank enemy)
        {
            return enemy != null && enemy.Health != null && !enemy.Health.IsDead && enemy.Kind != EnemyKind.Boss && enemy.Kind != EnemyKind.Supply;
        }

        private static bool IsPreferredForDoctrine(EnemyKind kind, EnemyCounterDoctrine doctrine)
        {
            switch (doctrine)
            {
                case EnemyCounterDoctrine.ArmorTrap:
                    return kind == EnemyKind.Heavy || kind == EnemyKind.Sniper || kind == EnemyKind.Elite;
                case EnemyCounterDoctrine.DispersedLogistics:
                    return kind == EnemyKind.Elite || kind == EnemyKind.Heavy || kind == EnemyKind.Sniper;
                case EnemyCounterDoctrine.SiegeBreach:
                    return kind == EnemyKind.Siege || kind == EnemyKind.Heavy || kind == EnemyKind.Elite;
                default:
                    return kind == EnemyKind.Elite || kind == EnemyKind.Heavy;
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight,
                normal = { textColor = new Color(1f, 0.34f, 0.14f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.UpperRight,
                normal = { textColor = new Color(0.88f, 0.90f, 0.96f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || !HasOperationForRound(_round)) return;
            EnsureStyles();
            string phase = ResolvePhase(_round) == DeceptionWarPhase.Masking ? "MASKING" : "MAIN EFFORT";
            string intel = _intelligenceConfirmed ? "INTEL CONFIRMED" : "AXES UNRESOLVED // G SIGINT / H RECON";
            string axis = _intelligenceConfirmed
                ? "MAIN " + _mainAxis.ToString().ToUpperInvariant() + " // FEINT " + _feintAxis.ToString().ToUpperInvariant()
                : "TWO AXES DETECTED // MAIN UNKNOWN";
            GUI.Label(new Rect(Screen.width - 470f, 116f, 450f, 20f), "HIGH COMMAND DECEPTION WAR // " + phase, _titleStyle);
            GUI.Label(new Rect(Screen.width - 470f, 136f, 450f, 18f), axis + " // " + intel, _bodyStyle);
            GUI.Label(new Rect(Screen.width - 470f, 153f, 450f, 18f), "RESERVE COMMIT " + _reserveCommitmentCap + "/" + MaxCommittedCombatants + " // MAIN " + _mainCommitted.Count + " // FEINT " + _feintCommitted.Count, _bodyStyle);
        }
    }
}
