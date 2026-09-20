using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TankRevival
{
    public enum CounterObservationStateV141
    {
        Idle = 0,
        Searching = 1,
        Designated = 2,
        NetworkBroken = 3,
        Cooldown = 4
    }

    public readonly struct CounterObservationProfileV141
    {
        public readonly int Round;
        public readonly float DesignationHoldSeconds;
        public readonly float DesignationLeaseSeconds;
        public readonly float NetworkBreakSeconds;
        public readonly float CooldownSeconds;
        public readonly float AcquisitionScale;
        public readonly int Signature;

        public CounterObservationProfileV141(
            int round,
            float designationHoldSeconds,
            float designationLeaseSeconds,
            float networkBreakSeconds,
            float cooldownSeconds,
            float acquisitionScale,
            int signature)
        {
            Round = round;
            DesignationHoldSeconds = designationHoldSeconds;
            DesignationLeaseSeconds = designationLeaseSeconds;
            NetworkBreakSeconds = networkBreakSeconds;
            CooldownSeconds = cooldownSeconds;
            AcquisitionScale = acquisitionScale;
            Signature = signature;
        }
    }

    public struct CounterObservationTargetSnapshotV141
    {
        public bool Valid;
        public bool ConfirmedKill;
        public EnemyKind Kind;
        public Vector2 Position;
        public float Range;
        public float Confidence;
        public float Resilience01;
        public int ContactSignature;
        public int RegistryRevision;

        public string CompactLabel => !Valid
            ? "NO DESIGNATION"
            : Kind.ToString().ToUpperInvariant() + " " + Range.ToString("0.0") + "m R" + Mathf.RoundToInt(Resilience01 * 100f).ToString("00");
    }

    /// <summary>
    /// Pure v14.1 counter-observation math. It ranks only already-existing observer actors and
    /// publishes bounded counter-battery disruption after canonical Health/runtime-registry lifecycle confirms removal.
    /// </summary>
    public static class CounterObservationModelV141
    {
        public const int PlannedRounds = 100;
        public const int MaxTrackedHostiles = 24;
        public const int MaxObserverCandidates = 6;
        public const float EvaluationCadenceSeconds = 0.25f;
        public const float MinDesignationHoldSeconds = 1.20f;
        public const float MaxDesignationHoldSeconds = 2.10f;
        public const float MinDesignationLeaseSeconds = 7.0f;
        public const float MaxDesignationLeaseSeconds = 10.0f;
        public const float MinNetworkBreakSeconds = 5.0f;
        public const float MaxNetworkBreakSeconds = 8.0f;
        public const float MinCooldownSeconds = 10.0f;
        public const float MaxCooldownSeconds = 15.0f;
        public const float MinAcquisitionScale = 0.46f;
        public const float MaxAcquisitionScale = 0.62f;
        public const float MaxDesignationRange = BattlefieldSensorFusionPlannerV136.SweepRadius + 4.0f;
        public const float MinEffectiveDesignationHoldScale = 0.90f;
        public const float MaxEffectiveDesignationHoldScale = 1.35f;
        public const float MinEffectiveNetworkBreakScale = 0.68f;
        public const float MaxEffectiveNetworkBreakScale = 1.05f;
        public const float MinimumEffectiveNetworkBreakSeconds = 3.40f;

        public static bool ConfigurationValid =>
            PlannedRounds == CounterBatteryModelV140.PlannedRounds &&
            MaxTrackedHostiles == CounterBatteryModelV140.MaxTrackedHostiles &&
            MaxObserverCandidates == CounterBatteryModelV140.MaxObservers &&
            EvaluationCadenceSeconds >= 0.20f && EvaluationCadenceSeconds <= 0.50f &&
            MinDesignationHoldSeconds >= 1.0f && MaxDesignationHoldSeconds <= 2.5f && MinDesignationHoldSeconds < MaxDesignationHoldSeconds &&
            MinDesignationLeaseSeconds >= 6f && MaxDesignationLeaseSeconds <= 12f && MinDesignationLeaseSeconds < MaxDesignationLeaseSeconds &&
            MinNetworkBreakSeconds >= 4f && MaxNetworkBreakSeconds <= 10f && MinNetworkBreakSeconds < MaxNetworkBreakSeconds &&
            MinCooldownSeconds >= 8f && MaxCooldownSeconds <= 18f && MinCooldownSeconds < MaxCooldownSeconds &&
            MinAcquisitionScale >= 0.40f && MaxAcquisitionScale <= 0.70f && MinAcquisitionScale < MaxAcquisitionScale &&
            MinEffectiveDesignationHoldScale >= 0.85f && MaxEffectiveDesignationHoldScale <= 1.40f &&
            MinEffectiveNetworkBreakScale >= 0.60f && MaxEffectiveNetworkBreakScale <= 1.10f &&
            MinimumEffectiveNetworkBreakSeconds >= 3.0f && MaxDesignationRange > BattlefieldSensorFusionPlannerV136.SweepRadius;

        public static CounterObservationProfileV141 ProfileForRound(int requestedRound)
        {
            int round = Mathf.Clamp(requestedRound, 1, PlannedRounds);
            float t = (round - 1f) / 99f;
            float hold = Mathf.Lerp(MinDesignationHoldSeconds, MaxDesignationHoldSeconds, t);
            float lease = Mathf.Lerp(MaxDesignationLeaseSeconds, MinDesignationLeaseSeconds, t);
            float networkBreak = Mathf.Lerp(MaxNetworkBreakSeconds, MinNetworkBreakSeconds, t);
            float cooldown = Mathf.Lerp(MinCooldownSeconds, MaxCooldownSeconds, t);
            float acquisitionScale = Mathf.Lerp(MinAcquisitionScale, MaxAcquisitionScale, t);
            int signature = unchecked(141 * 1009 + round * 97 + Mathf.RoundToInt(hold * 100f) * 31 + Mathf.RoundToInt(networkBreak * 100f));
            return new CounterObservationProfileV141(round, hold, lease, networkBreak, cooldown, acquisitionScale, signature);
        }

        public static bool IsObserverKind(EnemyKind kind) => CounterBatteryModelV140.ObserverWeight(kind) > 0f;

        public static bool HasDesignationEvidence(SensorContactStateV136 state, bool sweepActive)
        {
            return state == SensorContactStateV136.Verified ||
                   (state >= SensorContactStateV136.Tracked && sweepActive);
        }

        public static float CandidateScore(EnemyKind kind, float confidence, float distance)
        {
            float observer = Mathf.Clamp01(CounterBatteryModelV140.ObserverWeight(kind) / 1.25f);
            float sensor = Mathf.Clamp01(confidence);
            float range = Mathf.Clamp01(1f - Mathf.Max(0f, distance) / Mathf.Max(1f, MaxDesignationRange));
            return Mathf.Clamp01(sensor * 0.62f + observer * 0.26f + range * 0.12f);
        }

        public static float TerrainExposure01(EnemyTank actor)
        {
            if (actor == null) return 1f;
            TacticalTerrainDirector terrain = TacticalTerrainDirector.Instance;
            if (terrain == null || terrain.CurrentPlan.Round < 1) return 0.70f;

            TacticalTerrainPlanV134 plan = terrain.CurrentPlan;
            float doctrineExposure;
            switch (plan.Doctrine)
            {
                case TacticalTerrainDoctrineV134.FortifiedCorridor: doctrineExposure = 0.34f; break;
                case TacticalTerrainDoctrineV134.SiegeApproach: doctrineExposure = 0.31f; break;
                case TacticalTerrainDoctrineV134.BreachBelt: doctrineExposure = 0.48f; break;
                case TacticalTerrainDoctrineV134.RiverCuts: doctrineExposure = 0.52f; break;
                case TacticalTerrainDoctrineV134.CrossfireGrid: doctrineExposure = 0.60f; break;
                case TacticalTerrainDoctrineV134.CounterattackLanes: doctrineExposure = 0.68f; break;
                default: doctrineExposure = 0.82f; break;
            }

            float coverRatio = plan.CoverCount > 0
                ? Mathf.Clamp01(terrain.ActiveCoverCount / (float)plan.CoverCount)
                : 0f;
            float x = Mathf.Abs(actor.transform.position.x);
            float laneExposure = 1f - Mathf.Clamp01((x - plan.SafeLaneHalfWidth) / 4.5f);
            return Mathf.Clamp01(doctrineExposure * 0.50f + laneExposure * 0.22f + (1f - coverRatio) * 0.28f);
        }

        public static float Cohesion01(EnemyTank actor)
        {
            if (actor == null) return 0.48f;
            return Mathf.Clamp01(BattlefieldCohesionDirector.IntentFor(actor).Cohesion01);
        }

        public static float CommandDiscipline01(EnemyTank actor)
        {
            if (actor == null) return 0.35f;
            AdaptiveEnemyCommandDirector command = AdaptiveEnemyCommandDirector.Instance;
            EnemyCommandPostureV132 posture = AdaptiveEnemyCommandDirector.PostureFor(actor.Kind);
            float confidence = command != null ? Mathf.Clamp01(command.CurrentPlan.Confidence01) : 0.35f;
            float precision = Mathf.Clamp01((AdaptiveEnemyCommandPlannerV132.MaxSpreadScale - posture.SpreadScale) /
                                            Mathf.Max(0.001f, AdaptiveEnemyCommandPlannerV132.MaxSpreadScale - AdaptiveEnemyCommandPlannerV132.MinSpreadScale));
            return Mathf.Clamp01(confidence * 0.72f + precision * 0.28f);
        }

        public static float ObserverResilience01(EnemyTank actor)
        {
            float exposure = TerrainExposure01(actor);
            float terrainResilience = 1f - exposure;
            float cohesion = Cohesion01(actor);
            float command = CommandDiscipline01(actor);
            return Mathf.Clamp01(terrainResilience * 0.38f + cohesion * 0.36f + command * 0.26f);
        }

        public static float EffectiveDesignationHoldSeconds(CounterObservationProfileV141 profile, float resilience01)
        {
            float scale = Mathf.Lerp(MinEffectiveDesignationHoldScale, MaxEffectiveDesignationHoldScale, Mathf.Clamp01(resilience01));
            return Mathf.Max(0.25f, profile.DesignationHoldSeconds * scale);
        }

        public static float EffectiveNetworkBreakSeconds(CounterObservationProfileV141 profile, float resilience01)
        {
            float scale = Mathf.Lerp(MaxEffectiveNetworkBreakScale, MinEffectiveNetworkBreakScale, Mathf.Clamp01(resilience01));
            return Mathf.Max(MinimumEffectiveNetworkBreakSeconds, profile.NetworkBreakSeconds * scale);
        }
    }

    /// <summary>
    /// v14.1 hunter-killer layer. It never deals damage, moves actors, spawns/despawns units or
    /// owns projectiles. Success is confirmed only by canonical Health or runtime-registry lifecycle.
    /// </summary>
    [DefaultExecutionOrder(-8820)]
    public sealed class CounterObservationDirectorV141 : MonoBehaviour
    {
        public static CounterObservationDirectorV141 Instance { get; private set; }

        private TankGame _game;
        private CounterObservationStateV141 _state = CounterObservationStateV141.Idle;
        private CounterObservationProfileV141 _profile;
        private EnemyTank _candidate;
        private EnemyTank _designated;
        private Health _designatedHealth;
        private float _candidateSince;
        private float _designationUntil;
        private float _stateUntil;
        private float _nextEvaluation;
        private float _candidateConfidence;
        private float _candidateResilience;
        private float _designatedResilience;
        private float _candidateRange;
        private int _candidateSignature;
        private int _designationRegistryRevision;
        private int _round = -1;
        private int _candidateCount;
        private int _designations;
        private int _confirmedKills;
        private CounterObservationTargetSnapshotV141 _targetSnapshot;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _stateStyle;

        public CounterObservationStateV141 State => _state;
        public CounterObservationProfileV141 CurrentProfile => _profile;
        public EnemyTank DesignatedObserver => _designated;
        public CounterObservationTargetSnapshotV141 TargetSnapshot => _targetSnapshot;
        public int CandidateCount => _candidateCount;
        public int Designations => _designations;
        public int ConfirmedKills => _confirmedKills;
        public float CandidateConfidence => _candidateConfidence;
        public float CandidateResilience01 => _candidateResilience;
        public float CounterBatteryAcquisitionScale => _state == CounterObservationStateV141.NetworkBroken ? _profile.AcquisitionScale : 1f;
        public static float CounterBatteryNetworkScale => Instance != null ? Instance.CounterBatteryAcquisitionScale : 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static CounterObservationDirectorV141 EnsureInstalled()
        {
            if (Instance != null) return Instance;
            CounterObservationDirectorV141 existing = FindAnyObjectByType<CounterObservationDirectorV141>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            GameObject go = new GameObject("CounterObservationDirector_v14_1");
            DontDestroyOnLoad(go);
            return go.AddComponent<CounterObservationDirectorV141>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _game = FindAnyObjectByType<TankGame>();
            BattlefieldSensorFusionDirector.EnsureInstalled();
            CounterBatteryDirectorV140.EnsureInstalled();
            SceneManager.sceneLoaded += OnSceneLoaded;
            _nextEvaluation = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ClearDesignationSubscription();
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            _game = FindAnyObjectByType<TankGame>();
            ResetRuntime();
        }

        private void ResetRuntime()
        {
            ClearDesignationSubscription();
            _state = CounterObservationStateV141.Idle;
            _profile = default;
            _candidate = null;
            _designated = null;
            _candidateSince = 0f;
            _designationUntil = 0f;
            _stateUntil = 0f;
            _candidateConfidence = 0f;
            _candidateResilience = 0f;
            _designatedResilience = 0f;
            _candidateRange = 0f;
            _candidateSignature = 0;
            _designationRegistryRevision = 0;
            _targetSnapshot = default;
            _candidateCount = 0;
            _round = -1;
            _nextEvaluation = Time.unscaledTime;
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetRuntime();
                return;
            }

            EnsureRound(_game.CurrentRound);
            float now = Time.unscaledTime;

            if (_state == CounterObservationStateV141.NetworkBroken)
            {
                if (now >= _stateUntil)
                {
                    _state = CounterObservationStateV141.Cooldown;
                    _stateUntil = now + _profile.CooldownSeconds;
                }
                return;
            }
            if (_state == CounterObservationStateV141.Cooldown)
            {
                if (now >= _stateUntil)
                {
                    _state = CounterObservationStateV141.Idle;
                    _targetSnapshot = default;
                }
                return;
            }

            if (_state == CounterObservationStateV141.Designated)
            {
                if (_designatedHealth != null && _designatedHealth.IsDead)
                {
                    EnterNetworkBroken(now);
                    return;
                }

                bool registryChanged = RuntimeBattleRegistry.Revision != _designationRegistryRevision;
                if (registryChanged && !IsDesignatedStillRegistered())
                {
                    EnterNetworkBroken(now);
                    return;
                }

                if (_designated == null || _designatedHealth == null)
                {
                    ClearDesignationSubscription();
                    _designated = null;
                    _targetSnapshot = default;
                    _state = CounterObservationStateV141.Searching;
                    _candidateSince = now;
                }
                else if (now >= _designationUntil)
                {
                    ClearDesignationSubscription();
                    _designated = null;
                    _targetSnapshot = default;
                    _state = CounterObservationStateV141.Searching;
                    _candidateSince = now;
                }
                else
                {
                    RefreshTargetSnapshot(false);
                }
            }

            if (now < _nextEvaluation) return;
            _nextEvaluation = now + CounterObservationModelV141.EvaluationCadenceSeconds;

            if (!CounterBatteryPressureActive())
            {
                if (_state != CounterObservationStateV141.Designated)
                {
                    _state = CounterObservationStateV141.Idle;
                    _candidate = null;
                    _candidateConfidence = 0f;
                    _candidateResilience = 0f;
                    _candidateCount = 0;
                }
                return;
            }

            if (_state == CounterObservationStateV141.Idle)
            {
                _state = CounterObservationStateV141.Searching;
                _candidateSince = now;
            }

            if (_state == CounterObservationStateV141.Searching)
                EvaluateDesignation(now);
        }

        private void EnsureRound(int requestedRound)
        {
            int round = Mathf.Clamp(requestedRound, 1, CounterObservationModelV141.PlannedRounds);
            if (_round == round) return;
            ClearDesignationSubscription();
            _round = round;
            _profile = CounterObservationModelV141.ProfileForRound(round);
            _state = CounterObservationStateV141.Idle;
            _candidate = null;
            _designated = null;
            _candidateSince = 0f;
            _designationUntil = 0f;
            _stateUntil = 0f;
            _candidateConfidence = 0f;
            _candidateResilience = 0f;
            _designatedResilience = 0f;
            _candidateRange = 0f;
            _candidateSignature = 0;
            _designationRegistryRevision = 0;
            _targetSnapshot = default;
            _candidateCount = 0;
        }

        private bool CounterBatteryPressureActive()
        {
            CounterBatteryDirectorV140 counterBattery = CounterBatteryDirectorV140.Instance;
            if (counterBattery == null) return false;
            return counterBattery.State == CounterBatteryStateV140.Searching ||
                   counterBattery.State == CounterBatteryStateV140.Locked ||
                   counterBattery.State == CounterBatteryStateV140.Barrage ||
                   counterBattery.Exposure >= CounterBatteryModelV140.SearchThreshold;
        }

        private void EvaluateDesignation(float now)
        {
            BattlefieldSensorFusionDirector sensor = BattlefieldSensorFusionDirector.Instance;
            PlayerTank player = RuntimeBattleRegistry.Player;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (sensor == null || player == null || enemies == null)
            {
                ResetCandidate(now);
                return;
            }

            EnemyTank best = null;
            float bestScore = -1f;
            float bestConfidence = 0f;
            float bestResilience = 0f;
            float bestRange = 0f;
            int bestSignature = 0;
            int eligible = 0;
            int count = Mathf.Min(enemies.Length, CounterObservationModelV141.MaxTrackedHostiles);
            bool sweepActive = sensor.SweepActive;
            Vector2 playerPosition = player.transform.position;

            for (int i = 0; i < count; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (!CounterObservationModelV141.IsObserverKind(enemy.Kind)) continue;
                if (eligible >= CounterObservationModelV141.MaxObserverCandidates) break;
                eligible++;

                SensorContactStateV136 contactState;
                float confidence;
                int signature;
                if (!sensor.TryGetContact(enemy, out contactState, out confidence, out signature)) continue;
                if (!CounterObservationModelV141.HasDesignationEvidence(contactState, sweepActive)) continue;

                float distance = Vector2.Distance(playerPosition, enemy.transform.position);
                if (distance > CounterObservationModelV141.MaxDesignationRange) continue;
                float resilience = CounterObservationModelV141.ObserverResilience01(enemy);
                float score = CounterObservationModelV141.CandidateScore(enemy.Kind, confidence, distance) * Mathf.Lerp(1.04f, 0.90f, resilience);
                if (score <= bestScore) continue;
                best = enemy;
                bestScore = score;
                bestConfidence = confidence;
                bestResilience = resilience;
                bestRange = distance;
                bestSignature = signature;
            }

            _candidateCount = eligible;
            _candidateConfidence = bestConfidence;
            _candidateResilience = bestResilience;
            _candidateRange = bestRange;
            _candidateSignature = bestSignature;
            if (best == null)
            {
                ResetCandidate(now);
                return;
            }

            if (_candidate != best)
            {
                _candidate = best;
                _candidateSince = now;
                return;
            }

            float requiredHold = CounterObservationModelV141.EffectiveDesignationHoldSeconds(_profile, _candidateResilience);
            if (now - _candidateSince >= requiredHold)
                Designate(best, now);
        }

        private void ResetCandidate(float now)
        {
            _candidate = null;
            _candidateConfidence = 0f;
            _candidateResilience = 0f;
            _candidateRange = 0f;
            _candidateSignature = 0;
            _candidateSince = now;
        }

        private void Designate(EnemyTank target, float now)
        {
            if (target == null || target.Health == null || target.Health.IsDead) return;
            ClearDesignationSubscription();
            _designated = target;
            _designatedHealth = target.Health;
            _designatedHealth.Died += OnDesignatedObserverDied;
            _designatedResilience = _candidateResilience;
            _designationRegistryRevision = RuntimeBattleRegistry.Revision;
            _designationUntil = now + _profile.DesignationLeaseSeconds;
            _state = CounterObservationStateV141.Designated;
            _designations++;
            _targetSnapshot = new CounterObservationTargetSnapshotV141
            {
                Valid = true,
                ConfirmedKill = false,
                Kind = target.Kind,
                Position = target.transform.position,
                Range = _candidateRange,
                Confidence = _candidateConfidence,
                Resilience01 = _designatedResilience,
                ContactSignature = _candidateSignature,
                RegistryRevision = _designationRegistryRevision
            };
            _candidate = null;
            _candidateConfidence = 0f;
            _candidateResilience = 0f;
            _candidateRange = 0f;
            _candidateSignature = 0;
            VisualFactory.RingPulse(target.transform.position, new Color(0.16f, 0.92f, 1f), 0.72f);
        }

        private void OnDesignatedObserverDied(Health health)
        {
            if (_state != CounterObservationStateV141.Designated || health == null || health != _designatedHealth) return;
            EnterNetworkBroken(Time.unscaledTime);
        }

        private bool IsDesignatedStillRegistered()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || _designated == null) return false;
            int count = Mathf.Min(enemies.Length, CounterObservationModelV141.MaxTrackedHostiles);
            for (int i = 0; i < count; i++)
                if (enemies[i] == _designated) return true;
            return false;
        }

        private void RefreshTargetSnapshot(bool confirmedKill)
        {
            if (!_targetSnapshot.Valid) return;
            if (_designated != null)
            {
                _targetSnapshot.Position = _designated.transform.position;
                PlayerTank player = RuntimeBattleRegistry.Player;
                if (player != null)
                    _targetSnapshot.Range = Vector2.Distance(player.transform.position, _designated.transform.position);
            }
            _targetSnapshot.ConfirmedKill = confirmedKill;
            _targetSnapshot.RegistryRevision = RuntimeBattleRegistry.Revision;
        }

        private void EnterNetworkBroken(float now)
        {
            RefreshTargetSnapshot(true);
            ClearDesignationSubscription();
            _designated = null;
            _candidate = null;
            _candidateConfidence = 0f;
            _candidateResilience = 0f;
            _state = CounterObservationStateV141.NetworkBroken;
            _stateUntil = now + CounterObservationModelV141.EffectiveNetworkBreakSeconds(_profile, _designatedResilience);
            _confirmedKills++;
        }

        private void ClearDesignationSubscription()
        {
            if (_designatedHealth != null)
                _designatedHealth.Died -= OnDesignatedObserverDied;
            _designatedHealth = null;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.38f, 0.92f, 1f) }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.80f, 0.90f, 0.96f) }
            };
            _stateStyle = new GUIStyle(_header) { alignment = TextAnchor.MiddleRight };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            float x = Mathf.Max(12f, Screen.width - 306f);
            float y = Mathf.Max(12f, Screen.height - 110f);
            GUI.color = new Color(0.02f, 0.04f, 0.065f, 0.90f);
            GUI.Box(new Rect(x, y, 294f, 94f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 10f, y + 7f, 185f, 18f), "COUNTER-OBSERVATION", _header);
            GUI.Label(new Rect(x + 190f, y + 7f, 94f, 18f), StateLabel(), _stateStyle);

            string target;
            if (_targetSnapshot.Valid)
            {
                target = (_targetSnapshot.ConfirmedKill ? "KILL CONFIRMED  " : "TARGET  ") + _targetSnapshot.CompactLabel;
            }
            else if (_candidate != null)
            {
                target = "CONTACT " + _candidate.Kind.ToString().ToUpperInvariant() + "  " + _candidateRange.ToString("0.0") + "m R" +
                         Mathf.RoundToInt(_candidateResilience * 100f).ToString("00");
            }
            else
            {
                target = "NO DESIGNATION";
            }

            GUI.Label(new Rect(x + 10f, y + 30f, 270f, 18f), target, _body);
            GUI.Label(new Rect(x + 10f, y + 49f, 270f, 18f),
                "Observers " + _candidateCount + "/" + CounterObservationModelV141.MaxObserverCandidates +
                "  ·  CB scale " + CounterBatteryAcquisitionScale.ToString("0.00"), _body);
            string hint = _state == CounterObservationStateV141.Searching ? "SEARCH · sensor evidence + terrain/cohesion/command" :
                _state == CounterObservationStateV141.NetworkBroken ? "NETWORK BROKEN · canonical kill confirmed" :
                _state == CounterObservationStateV141.Designated ? "DESIGNATED · destroy target through normal combat" :
                _state == CounterObservationStateV141.Cooldown ? "COOLDOWN · network reacquiring" : "Counter-observation standing by";
            GUI.Label(new Rect(x + 10f, y + 68f, 274f, 18f), hint, _body);
        }

        private string StateLabel()
        {
            switch (_state)
            {
                case CounterObservationStateV141.Searching: return "SEARCH";
                case CounterObservationStateV141.Designated: return "DESIGNATED";
                case CounterObservationStateV141.NetworkBroken: return "NETWORK BROKEN";
                case CounterObservationStateV141.Cooldown: return "COOLDOWN";
                default: return "IDLE";
            }
        }
    }
}
