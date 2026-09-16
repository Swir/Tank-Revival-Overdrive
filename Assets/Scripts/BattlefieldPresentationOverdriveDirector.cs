using UnityEngine;

namespace TankRevival
{
    public enum TacticalPresentationChannel
    {
        Front,
        Sustainment,
        Route,
        Recon,
        Signal,
        FireSupport,
        Breach
    }

    public enum TacticalPresentationSeverity
    {
        Routine,
        Objective,
        Warning,
        Critical
    }

    public struct TacticalPresentationBudget
    {
        public readonly float RefreshSeconds;
        public readonly int MaxTelegraphs;
        public readonly int MaxAlerts;
        public readonly int RingSegments;
        public readonly float PulseDensity;

        public TacticalPresentationBudget(float refreshSeconds, int maxTelegraphs, int maxAlerts, int ringSegments, float pulseDensity)
        {
            RefreshSeconds = refreshSeconds;
            MaxTelegraphs = maxTelegraphs;
            MaxAlerts = maxAlerts;
            RingSegments = ringSegments;
            PulseDensity = pulseDensity;
        }
    }

    /// <summary>
    /// Immutable read-only projection of the v12.0-v12.5 operational stack. It intentionally contains
    /// only presentation data and never becomes combat, movement, objective, economy or projectile authority.
    /// Existing directors remain the single source of truth; this snapshot is refreshed at a bounded cadence.
    /// </summary>
    public struct TacticalPresentationSnapshot
    {
        public int Round;
        public float CapturedAt;

        public bool FrontActive;
        public MobileFrontOperationKind FrontKind;
        public int FrontLane;
        public float FrontProgress;
        public int FrontHealth;
        public int FrontMaxHealth;
        public int BreachSequence;
        public Vector2 FrontPosition;

        public bool SustainmentActive;
        public Team SustainmentTeam;
        public OperationalSustainmentState SustainmentState;
        public int Fuel;
        public int Ammo;
        public int Repair;
        public int Escorts;
        public Vector2 SustainmentPosition;

        public bool RouteActive;
        public LogisticsRoutePlan RoutePlan;
        public RouteIntelState RouteIntel;
        public int RouteLane;
        public int Reroutes;
        public float RouteThreat;
        public bool DecoyActive;
        public Vector2 RoutePosition;

        public bool ReconActive;
        public ReconEWState ReconState;
        public float SignalQuality;
        public bool SpoofRisk;
        public bool JammerActive;
        public int SuppressedRelays;
        public bool HasRelayA;
        public bool HasRelayB;
        public Vector2 RelayA;
        public Vector2 RelayB;
        public Vector2 JammerPosition;

        public bool MobileSignalActive;
        public MobileSignalWarfareState MobileSignalState;
        public bool MobileJammerActive;
        public float InterceptProgress;
        public int RaidCount;
        public Vector2 MobileJammerPosition;

        public bool SigintActive;
        public SignalsFireSupportState SigintState;
        public float TriangulationQuality;
        public bool FireWindow;
        public float FireWindowRemaining;
        public int PendingShells;
        public bool TrueSignalVerified;
        public bool DecoySignalVerified;
        public Vector2 TrueEmitterPosition;
        public Vector2 DecoyEmitterPosition;

        public int ActiveChannelCount
        {
            get
            {
                int count = 0;
                if (FrontActive) count++;
                if (SustainmentActive) count++;
                if (RouteActive) count++;
                if (ReconActive) count++;
                if (MobileSignalActive) count++;
                if (SigintActive) count++;
                return count;
            }
        }
    }

    /// <summary>
    /// v12.6 unified battlefield presentation layer. It consumes bounded read-only state from the
    /// v12.0-v12.5 directors, ranks the most important objectives, and reuses a fixed world-space
    /// telegraph pool. It never fires projectiles, damages actors, moves gameplay rigidbodies or mutates
    /// tactical AI. Presentation density becomes cheaper as round pressure rises while critical cues keep
    /// priority over routine telemetry.
    /// </summary>
    [DefaultExecutionOrder(760)]
    public sealed class BattlefieldPresentationOverdriveDirector : MonoBehaviour
    {
        public const int MaxTelegraphs = 12;
        public const int MinTelegraphs = 7;
        public const int MaxAlerts = 4;
        public const int MinAlerts = 3;
        public const int MaxRingSegments = 28;
        public const int MinRingSegments = 16;
        public const float MinRefreshSeconds = 0.075f;
        public const float MaxRefreshSeconds = 0.165f;
        public const float SnapshotFreshnessMultiplier = 2.75f;
        public const float SnapshotFreshnessSlack = 0.08f;

        private struct AlertEntry
        {
            public string Text;
            public TacticalPresentationSeverity Severity;
            public TacticalPresentationChannel Channel;
            public int Score;
        }

        private sealed class TelegraphSlot
        {
            public GameObject Object;
            public LineRenderer Line;
        }

        private static BattlefieldPresentationOverdriveDirector _instance;

        private readonly AlertEntry[] _alerts = new AlertEntry[MaxAlerts];
        private readonly TelegraphSlot[] _telegraphs = new TelegraphSlot[MaxTelegraphs];
        private readonly Vector3[] _ringPoints = new Vector3[MaxRingSegments];
        private TankGame _game;
        private Material _lineMaterial;
        private TacticalPresentationSnapshot _snapshot;
        private TacticalPresentationBudget _budget;
        private float _nextRefresh;
        private int _alertCount;
        private int _visibleTelegraphs;
        private GUIStyle _titleStyle;
        private GUIStyle _alertStyle;
        private GUIStyle _criticalStyle;
        private GUIStyle _footerStyle;
        private string _headerText = "TACTICAL COMMAND";
        private string _footerText = string.Empty;

        public static BattlefieldPresentationOverdriveDirector Instance => _instance;
        public TacticalPresentationSnapshot Snapshot => _snapshot;
        public TacticalPresentationBudget CurrentBudget => _budget;
        public int VisibleTelegraphs => _visibleTelegraphs;
        public int VisibleAlerts => _alertCount;
        public float SnapshotAge => Mathf.Max(0f, Time.unscaledTime - _snapshot.CapturedAt);

        public static bool ConfigurationValid =>
            MaxTelegraphs == 12 && MinTelegraphs >= 6 && MinTelegraphs < MaxTelegraphs &&
            MaxAlerts == 4 && MinAlerts == 3 &&
            MaxRingSegments <= 32 && MinRingSegments >= 12 && MinRingSegments < MaxRingSegments &&
            MinRefreshSeconds >= 0.05f && MaxRefreshSeconds <= 0.20f && MinRefreshSeconds < MaxRefreshSeconds &&
            SnapshotFreshnessMultiplier >= 2f && SnapshotFreshnessMultiplier <= 4f &&
            CombinedArmsMobileFrontDirector.ConfigurationValid && OperationalSustainmentDirector.ConfigurationValid &&
            LogisticsRouteIntelligenceDirector.ConfigurationValid && ReconElectronicWarfareDirector.ConfigurationValid &&
            MobileSignalWarfareDirector.ConfigurationValid && SignalsIntelligenceFireSupportDirector.ConfigurationValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<BattlefieldPresentationOverdriveDirector>() != null) return;
            GameObject go = new GameObject("BattlefieldPresentationOverdriveDirector_v12_6");
            DontDestroyOnLoad(go);
            go.AddComponent<BattlefieldPresentationOverdriveDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            CreateTelegraphPool();
            _budget = ComputeBudget(1, 0, false);
        }

        private void OnDestroy()
        {
            HideAllTelegraphs();
            if (_lineMaterial != null) Destroy(_lineMaterial);
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                _alertCount = 0;
                _snapshot = default(TacticalPresentationSnapshot);
                HideAllTelegraphs();
                return;
            }

            if (Time.unscaledTime < _nextRefresh) return;
            RefreshPresentation();
            _nextRefresh = Time.unscaledTime + _budget.RefreshSeconds;
        }

        private void RefreshPresentation()
        {
            _snapshot = CaptureSnapshot(_game);
            bool critical = _snapshot.FireWindow || _snapshot.PendingShells > 0 ||
                            (_snapshot.FrontActive && _snapshot.FrontKind == MobileFrontOperationKind.EnemyBreakthrough && _snapshot.FrontProgress >= 0.72f);
            int pressure = ComputePresentationPressure(_snapshot);
            _budget = ComputeBudget(_snapshot.Round, pressure, critical);
            BuildAlerts();
            BuildTelegraphs();
            _headerText = "TACTICAL COMMAND // R" + _snapshot.Round + " // " + PressureLabel(pressure);
            _footerText = "ACTIVE " + _snapshot.ActiveChannelCount + " // CUES " + _visibleTelegraphs + "/" + _budget.MaxTelegraphs +
                          " // REFRESH " + _budget.RefreshSeconds.ToString("0.000") + "s // SNAP " + SnapshotAge.ToString("0.00") + "s";
        }

        public static TacticalPresentationSnapshot CaptureSnapshot(TankGame game)
        {
            TacticalPresentationSnapshot s = new TacticalPresentationSnapshot();
            s.CapturedAt = Time.unscaledTime;
            s.Round = game != null ? Mathf.Clamp(game.CurrentRound, 1, 100) : 1;

            CombinedArmsMobileFrontDirector front = CombinedArmsMobileFrontDirector.Instance;
            if (front != null && front.IsOperationActive)
            {
                s.FrontActive = true;
                s.FrontKind = front.CurrentKind;
                s.FrontLane = front.CurrentLane;
                s.FrontProgress = front.CurrentProgress;
                s.FrontHealth = front.CurrentNodeHealth;
                s.FrontMaxHealth = front.CurrentNodeMaxHealth;
                s.BreachSequence = front.CurrentBreachSequence;
                s.FrontPosition = FrontObjectivePosition(front.CurrentLane, front.CurrentProgress, front.CurrentKind == MobileFrontOperationKind.EnemyBreakthrough);
            }

            OperationalSustainmentDirector sustainment = OperationalSustainmentDirector.Instance;
            if (sustainment != null && sustainment.ColumnActive)
            {
                s.SustainmentActive = true;
                s.SustainmentTeam = sustainment.ColumnTeam;
                s.SustainmentState = sustainment.State;
                s.Fuel = sustainment.FuelRemaining;
                s.Ammo = sustainment.AmmoRemaining;
                s.Repair = sustainment.RepairRemaining;
                s.Escorts = sustainment.EscortCount;
                int lane = s.FrontActive ? s.FrontLane : 1;
                float progress = s.FrontActive ? s.FrontProgress : 0.5f;
                s.SustainmentPosition = OperationalSustainmentDirector.SupportAnchor(lane, progress, sustainment.ColumnTeam == Team.Enemy);
            }

            LogisticsRouteIntelligenceDirector route = LogisticsRouteIntelligenceDirector.Instance;
            if (route != null && route.RouteActive)
            {
                s.RouteActive = true;
                s.RoutePlan = route.CurrentPlan;
                s.RouteIntel = route.IntelState;
                s.RouteLane = route.CurrentRouteLane;
                s.Reroutes = route.ReroutesThisColumn;
                s.RouteThreat = route.CurrentThreat;
                s.DecoyActive = route.DecoyActive;
                float progress = s.FrontActive ? s.FrontProgress : 0.5f;
                bool enemy = sustainment != null && sustainment.ColumnTeam == Team.Enemy;
                s.RoutePosition = OperationalSustainmentDirector.SupportAnchor(route.CurrentRouteLane, progress, enemy);
            }

            ReconElectronicWarfareDirector recon = ReconElectronicWarfareDirector.Instance;
            if (recon != null && recon.OperationActive)
            {
                s.ReconActive = true;
                s.ReconState = recon.State;
                s.SignalQuality = recon.SignalQuality;
                s.SpoofRisk = recon.SpoofRisk;
                s.JammerActive = recon.JammerActive;
                s.SuppressedRelays = recon.SuppressedRelays;
                s.HasRelayA = recon.TryGetRelayPosition(0, out s.RelayA);
                s.HasRelayB = recon.TryGetRelayPosition(1, out s.RelayB);
                int jammerLane = Mathf.Max(0, recon.JammerLane);
                s.JammerPosition = ReconElectronicWarfareDirector.JammerAnchor(jammerLane, s.Round);
            }

            MobileSignalWarfareDirector signal = MobileSignalWarfareDirector.Instance;
            if (signal != null && signal.OperationActive)
            {
                s.MobileSignalActive = true;
                s.MobileSignalState = signal.State;
                s.MobileJammerActive = signal.MobileJammerActive;
                s.InterceptProgress = signal.InterceptProgress01;
                s.RaidCount = signal.RaidCount;
                int lane = s.RouteActive ? s.RouteLane : (s.FrontActive ? s.FrontLane : 1);
                float progress = s.FrontActive ? s.FrontProgress : 0.5f;
                s.MobileJammerPosition = MobileSignalWarfareDirector.MobileJammerAnchor(lane, progress, s.Round);
            }

            SignalsIntelligenceFireSupportDirector sigint = SignalsIntelligenceFireSupportDirector.Instance;
            if (sigint != null && sigint.OperationActive)
            {
                s.SigintActive = true;
                s.SigintState = sigint.State;
                s.TriangulationQuality = sigint.TriangulationQuality;
                s.FireWindow = sigint.FireSupportWindowActive;
                s.FireWindowRemaining = sigint.FireSupportWindowRemaining;
                s.PendingShells = sigint.PendingShells;
                s.TrueSignalVerified = sigint.TrueSignalVerified;
                s.DecoySignalVerified = sigint.DecoySignalVerified;
                int lane = s.RouteActive ? s.RouteLane : (s.FrontActive ? s.FrontLane : 1);
                float progress = s.FrontActive ? s.FrontProgress : 0.5f;
                s.TrueEmitterPosition = SignalsIntelligenceFireSupportDirector.EmitterAnchor(lane, progress, s.Round, false);
                int decoyLane = SignalsIntelligenceFireSupportDirector.AlternateLane(lane, s.Round);
                s.DecoyEmitterPosition = SignalsIntelligenceFireSupportDirector.EmitterAnchor(decoyLane, progress, s.Round, true);
            }

            return s;
        }

        public static TacticalPresentationBudget ComputeBudget(int round, int activePressure, bool criticalWindow)
        {
            int r = Mathf.Clamp(round, 1, 100);
            int pressure = Mathf.Clamp(activePressure, 0, 10);
            int tier = 0;
            if (r >= 60 || pressure >= 4) tier = 1;
            if (r >= 85 || pressure >= 7) tier = 2;

            if (tier == 0)
                return new TacticalPresentationBudget(0.075f, 12, 4, 28, criticalWindow ? 1.00f : 0.88f);
            if (tier == 1)
                return new TacticalPresentationBudget(0.105f, 10, 4, 22, criticalWindow ? 0.90f : 0.72f);
            return new TacticalPresentationBudget(0.155f, 7, criticalWindow ? 4 : 3, 16, criticalWindow ? 0.82f : 0.58f);
        }

        public static int ComputePresentationPressure(TacticalPresentationSnapshot snapshot)
        {
            int pressure = snapshot.ActiveChannelCount;
            if (snapshot.SpoofRisk) pressure += 2;
            if (snapshot.DecoyActive) pressure++;
            if (snapshot.SuppressedRelays > 0) pressure++;
            if (snapshot.RaidCount > 0) pressure++;
            if (snapshot.PendingShells > 0 || snapshot.FireWindow) pressure += 2;
            if (snapshot.FrontActive && snapshot.FrontKind == MobileFrontOperationKind.EnemyBreakthrough && snapshot.FrontProgress >= 0.65f) pressure += 2;
            return Mathf.Clamp(pressure, 0, 10);
        }

        public static int PriorityScore(TacticalPresentationSeverity severity, TacticalPresentationChannel channel, bool activeWindow)
        {
            int score = severity == TacticalPresentationSeverity.Critical ? 400 :
                        severity == TacticalPresentationSeverity.Warning ? 300 :
                        severity == TacticalPresentationSeverity.Objective ? 200 : 100;
            if (activeWindow) score += 24;
            switch (channel)
            {
                case TacticalPresentationChannel.FireSupport: score += 9; break;
                case TacticalPresentationChannel.Breach: score += 8; break;
                case TacticalPresentationChannel.Recon: score += 7; break;
                case TacticalPresentationChannel.Front: score += 6; break;
                case TacticalPresentationChannel.Signal: score += 5; break;
                case TacticalPresentationChannel.Route: score += 4; break;
                default: score += 3; break;
            }
            return score;
        }

        public static int ClampTelegraphCount(int requested, TacticalPresentationBudget budget)
        {
            return Mathf.Clamp(requested, 0, Mathf.Min(MaxTelegraphs, budget.MaxTelegraphs));
        }

        public static bool IsSnapshotFresh(float capturedAt, float now, float refreshSeconds)
        {
            if (capturedAt <= 0f || now < capturedAt) return false;
            float maxAge = Mathf.Clamp(refreshSeconds, MinRefreshSeconds, MaxRefreshSeconds) * SnapshotFreshnessMultiplier + SnapshotFreshnessSlack;
            return now - capturedAt <= maxAge;
        }

        public static Vector2 FrontObjectivePosition(int lane, float progress, bool enemyOperation)
        {
            int boundedLane = Mathf.Clamp(lane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            float x = DynamicFrontlineTerritoryDirector.LanePosition(boundedLane).x;
            float start = enemyOperation ? CombinedArmsMobileFrontDirector.ArenaYLimit : -CombinedArmsMobileFrontDirector.ArenaYLimit;
            float goal = enemyOperation ? -CombinedArmsMobileFrontDirector.ArenaYLimit : CombinedArmsMobileFrontDirector.ArenaYLimit;
            return new Vector2(x, Mathf.Lerp(start, goal, Mathf.Clamp01(progress)));
        }

        public static Vector2 RingPoint(Vector2 center, float radius, int index, int segments)
        {
            int seg = Mathf.Clamp(segments, MinRingSegments, MaxRingSegments);
            int i = Mathf.Clamp(index, 0, seg - 1);
            float angle = (i / (float)seg) * Mathf.PI * 2f;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Max(0.02f, radius);
        }

        private void BuildAlerts()
        {
            _alertCount = 0;
            for (int i = 0; i < MaxAlerts; i++) _alerts[i] = default(AlertEntry);

            if (_snapshot.FireWindow)
                OfferAlert("FIRE SUPPORT READY // PRESS F // " + _snapshot.FireWindowRemaining.ToString("0.0") + "s", TacticalPresentationSeverity.Critical, TacticalPresentationChannel.FireSupport, true);
            else if (_snapshot.PendingShells > 0)
                OfferAlert("HE SALVO INBOUND // SHELLS " + _snapshot.PendingShells, TacticalPresentationSeverity.Critical, TacticalPresentationChannel.FireSupport, true);

            if (_snapshot.FrontActive)
            {
                bool enemy = _snapshot.FrontKind == MobileFrontOperationKind.EnemyBreakthrough;
                TacticalPresentationSeverity severity = enemy && _snapshot.FrontProgress >= 0.72f ? TacticalPresentationSeverity.Critical : TacticalPresentationSeverity.Objective;
                string side = enemy ? "ENEMY BREAKTHROUGH" : "FRIENDLY ADVANCE";
                OfferAlert(side + " // L" + (_snapshot.FrontLane + 1) + " // " + Mathf.RoundToInt(_snapshot.FrontProgress * 100f) + "% // CP " + _snapshot.FrontHealth + "/" + _snapshot.FrontMaxHealth,
                    severity, TacticalPresentationChannel.Front, enemy && _snapshot.FrontProgress >= 0.72f);
            }

            if (_snapshot.SpoofRisk || _snapshot.SuppressedRelays > 0)
                OfferAlert("RECON DEGRADED // SIGNAL " + Mathf.RoundToInt(_snapshot.SignalQuality * 100f) + "% // SUPP " + _snapshot.SuppressedRelays + (_snapshot.SpoofRisk ? " // SPOOF" : string.Empty),
                    TacticalPresentationSeverity.Warning, TacticalPresentationChannel.Recon, _snapshot.SpoofRisk);
            else if (_snapshot.ReconActive)
                OfferAlert("RECON " + _snapshot.ReconState.ToString().ToUpperInvariant() + " // SIGNAL " + Mathf.RoundToInt(_snapshot.SignalQuality * 100f) + "%",
                    TacticalPresentationSeverity.Objective, TacticalPresentationChannel.Recon, false);

            if (_snapshot.MobileSignalActive && !_snapshot.FireWindow)
                OfferAlert("MOBILE EW " + _snapshot.MobileSignalState.ToString().ToUpperInvariant() + " // INTERCEPT " + Mathf.RoundToInt(_snapshot.InterceptProgress * 100f) + "% // RAID " + _snapshot.RaidCount,
                    _snapshot.RaidCount > 0 ? TacticalPresentationSeverity.Warning : TacticalPresentationSeverity.Objective, TacticalPresentationChannel.Signal, _snapshot.RaidCount > 0);

            if (_snapshot.SigintActive && !_snapshot.FireWindow && _snapshot.PendingShells <= 0)
                OfferAlert("SIGINT " + _snapshot.SigintState.ToString().ToUpperInvariant() + " // TRI " + Mathf.RoundToInt(_snapshot.TriangulationQuality * 100f) + "%",
                    _snapshot.SigintState == SignalsFireSupportState.DeceptionRisk ? TacticalPresentationSeverity.Warning : TacticalPresentationSeverity.Objective,
                    TacticalPresentationChannel.FireSupport, false);

            if (_snapshot.RouteActive)
                OfferAlert("ROUTE " + _snapshot.RoutePlan.ToString().ToUpperInvariant() + " // " + _snapshot.RouteIntel.ToString().ToUpperInvariant() + " // THREAT " + _snapshot.RouteThreat.ToString("0.0") + " // REROUTE " + _snapshot.Reroutes,
                    _snapshot.DecoyActive ? TacticalPresentationSeverity.Warning : TacticalPresentationSeverity.Routine, TacticalPresentationChannel.Route, _snapshot.DecoyActive);

            if (_snapshot.SustainmentActive)
                OfferAlert("LOGISTICS " + _snapshot.SustainmentTeam.ToString().ToUpperInvariant() + " // F" + _snapshot.Fuel + " A" + _snapshot.Ammo + " R" + _snapshot.Repair + " // ESC " + _snapshot.Escorts,
                    _snapshot.SustainmentTeam == Team.Enemy ? TacticalPresentationSeverity.Objective : TacticalPresentationSeverity.Routine,
                    TacticalPresentationChannel.Sustainment, false);

            if (_snapshot.BreachSequence > 0)
                OfferAlert("BREACH CORRIDOR " + _snapshot.BreachSequence + " // MOBILE FRONT ROUTE ACTIVE", TacticalPresentationSeverity.Objective, TacticalPresentationChannel.Breach, true);

            if (_alertCount > _budget.MaxAlerts) _alertCount = _budget.MaxAlerts;
        }

        private void OfferAlert(string text, TacticalPresentationSeverity severity, TacticalPresentationChannel channel, bool activeWindow)
        {
            int score = PriorityScore(severity, channel, activeWindow);
            int limit = Mathf.Min(MaxAlerts, _budget.MaxAlerts);
            int insert = _alertCount;
            if (insert < limit) _alertCount++;
            else
            {
                if (_alerts[limit - 1].Score >= score) return;
                insert = limit - 1;
            }

            while (insert > 0 && _alerts[insert - 1].Score < score)
            {
                if (insert < limit) _alerts[insert] = _alerts[insert - 1];
                insert--;
            }
            _alerts[insert] = new AlertEntry { Text = text, Severity = severity, Channel = channel, Score = score };
        }

        private void CreateTelegraphPool()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader != null) _lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            for (int i = 0; i < MaxTelegraphs; i++)
            {
                GameObject go = new GameObject("PresentationTelegraph_" + i);
                go.transform.SetParent(transform, false);
                LineRenderer line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = true;
                line.positionCount = 0;
                line.widthMultiplier = 0.055f;
                line.numCapVertices = 0;
                line.numCornerVertices = 0;
                line.sortingOrder = 18;
                if (_lineMaterial != null) line.sharedMaterial = _lineMaterial;
                go.SetActive(false);
                _telegraphs[i] = new TelegraphSlot { Object = go, Line = line };
            }
        }

        private void HideAllTelegraphs()
        {
            _visibleTelegraphs = 0;
            for (int i = 0; i < MaxTelegraphs; i++)
                if (_telegraphs[i] != null && _telegraphs[i].Object != null) _telegraphs[i].Object.SetActive(false);
        }

        private void BuildTelegraphs()
        {
            HideAllTelegraphs();
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * (3.1f + _budget.PulseDensity)) * 0.055f * _budget.PulseDensity;

            if (_snapshot.FireWindow || _snapshot.PendingShells > 0)
                AddRing(_snapshot.TrueEmitterPosition, 1.18f * pulse, new Color(1f, 0.48f, 0.08f, 0.94f), 0.090f);

            if (_snapshot.SigintActive)
            {
                AddRing(_snapshot.TrueEmitterPosition, 0.78f * pulse, _snapshot.TrueSignalVerified ? new Color(0.18f, 1f, 0.58f, 0.88f) : new Color(1f, 0.20f, 0.08f, 0.82f), 0.070f);
                AddRing(_snapshot.DecoyEmitterPosition, 0.62f, _snapshot.DecoySignalVerified ? new Color(0.38f, 0.55f, 0.65f, 0.62f) : new Color(0.76f, 0.24f, 1f, 0.76f), 0.052f);
            }

            if (_snapshot.MobileSignalActive && _snapshot.MobileJammerActive)
                AddRing(_snapshot.MobileJammerPosition, 0.74f * pulse, new Color(1f, 0.12f, 0.68f, 0.82f), 0.060f);

            if (_snapshot.ReconActive)
            {
                if (_snapshot.HasRelayA) AddRing(_snapshot.RelayA, 0.54f * pulse, new Color(0.18f, 0.92f, 1f, 0.72f), 0.045f);
                if (_snapshot.HasRelayB) AddRing(_snapshot.RelayB, 0.54f * pulse, new Color(0.18f, 0.92f, 1f, 0.72f), 0.045f);
                if (_snapshot.JammerActive) AddRing(_snapshot.JammerPosition, 0.72f, new Color(1f, 0.18f, 0.42f, 0.76f), 0.055f);
            }

            if (_snapshot.FrontActive)
            {
                Color frontColor = _snapshot.FrontKind == MobileFrontOperationKind.EnemyBreakthrough ? new Color(1f, 0.24f, 0.10f, 0.86f) : new Color(0.18f, 0.92f, 1f, 0.84f);
                AddRing(_snapshot.FrontPosition, 0.92f * pulse, frontColor, 0.072f);
                if (_snapshot.BreachSequence > 0) AddRing(_snapshot.FrontPosition, 1.42f * pulse, new Color(1f, 0.64f, 0.08f, 0.56f), 0.040f);
            }

            if (_snapshot.SustainmentActive)
            {
                Color logiColor = _snapshot.SustainmentTeam == Team.Enemy ? new Color(1f, 0.36f, 0.12f, 0.66f) : new Color(0.12f, 0.84f, 1f, 0.66f);
                AddRing(_snapshot.SustainmentPosition, 0.56f, logiColor, 0.045f);
                if (_snapshot.FrontActive) AddVector(_snapshot.SustainmentPosition, _snapshot.FrontPosition, logiColor, 0.030f);
            }

            if (_snapshot.RouteActive && _snapshot.RouteLane >= 0)
                AddRing(_snapshot.RoutePosition, 0.38f, _snapshot.DecoyActive ? new Color(0.82f, 0.28f, 1f, 0.62f) : new Color(0.32f, 0.76f, 1f, 0.52f), 0.034f);
        }

        private void AddRing(Vector2 center, float radius, Color color, float width)
        {
            if (_visibleTelegraphs >= Mathf.Min(MaxTelegraphs, _budget.MaxTelegraphs)) return;
            TelegraphSlot slot = _telegraphs[_visibleTelegraphs++];
            LineRenderer line = slot.Line;
            int segments = Mathf.Clamp(_budget.RingSegments, MinRingSegments, MaxRingSegments);
            line.loop = true;
            line.positionCount = segments;
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = width;
            for (int i = 0; i < segments; i++)
            {
                Vector2 point = RingPoint(center, radius, i, segments);
                _ringPoints[i] = new Vector3(point.x, point.y, -0.08f);
            }
            for (int i = 0; i < segments; i++) line.SetPosition(i, _ringPoints[i]);
            slot.Object.SetActive(true);
        }

        private void AddVector(Vector2 from, Vector2 to, Color color, float width)
        {
            if (_visibleTelegraphs >= Mathf.Min(MaxTelegraphs, _budget.MaxTelegraphs)) return;
            TelegraphSlot slot = _telegraphs[_visibleTelegraphs++];
            LineRenderer line = slot.Line;
            line.loop = false;
            line.positionCount = 2;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.35f);
            line.widthMultiplier = width;
            line.SetPosition(0, new Vector3(from.x, from.y, -0.07f));
            line.SetPosition(1, new Vector3(to.x, to.y, -0.07f));
            slot.Object.SetActive(true);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.38f, 0.88f, 1f) } };
            _alertStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.88f, 0.94f, 1f) } };
            _criticalStyle = new GUIStyle(_alertStyle) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.42f, 0.16f) } };
            _footerStyle = new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.52f, 0.66f, 0.76f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _alertCount <= 0) return;
            EnsureStyles();
            float width = Mathf.Min(470f, Screen.width * 0.42f);
            float height = 48f + _alertCount * 23f;
            float x = Screen.width - width - 18f;
            float y = 18f;
            GUI.color = new Color(0.010f, 0.026f, 0.042f, 0.92f);
            GUI.Box(new Rect(x, y, width, height), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 4f, width - 24f, 20f), _headerText, _titleStyle);
            float rowY = y + 24f;
            for (int i = 0; i < _alertCount; i++)
            {
                AlertEntry alert = _alerts[i];
                GUIStyle style = alert.Severity == TacticalPresentationSeverity.Critical ? _criticalStyle : _alertStyle;
                GUI.Label(new Rect(x + 12f, rowY, width - 24f, 20f), alert.Text, style);
                rowY += 21f;
            }
            GUI.Label(new Rect(x + 12f, y + height - 18f, width - 24f, 16f), _footerText, _footerStyle);
        }

        private static string PressureLabel(int pressure)
        {
            if (pressure >= 7) return "DENSE BATTLE MODE";
            if (pressure >= 4) return "TACTICAL LOAD";
            return "FULL DETAIL";
        }
    }
}
