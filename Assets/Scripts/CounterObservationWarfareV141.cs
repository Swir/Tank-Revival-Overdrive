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

    /// <summary>
    /// Pure v14.1 counter-observation math. It ranks only already-existing observer actors and
    /// publishes bounded counter-battery disruption after canonical Health confirms a kill.
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
            MaxDesignationRange > BattlefieldSensorFusionPlannerV136.SweepRadius;

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
    }

    /// <summary>
    /// v14.1 hunter-killer layer. It never deals damage, moves actors, spawns/despawns units or
    /// owns projectiles. A successful disruption exists only after canonical Health reports that
    /// the currently designated observer has died.
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
        private int _round = -1;
        private int _candidateCount;
        private int _designations;
        private int _confirmedKills;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _stateStyle;

        public CounterObservationStateV141 State => _state;
        public CounterObservationProfileV141 CurrentProfile => _profile;
        public EnemyTank DesignatedObserver => _designated;
        public int CandidateCount => _candidateCount;
        public int Designations => _designations;
        public int ConfirmedKills => _confirmedKills;
        public float CandidateConfidence => _candidateConfidence;
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
                if (now >= _stateUntil) _state = CounterObservationStateV141.Idle;
                return;
            }

            if (_state == CounterObservationStateV141.Designated)
            {
                if (_designatedHealth != null && _designatedHealth.IsDead)
                {
                    EnterNetworkBroken(now);
                    return;
                }
                if (_designated == null || _designatedHealth == null)
                {
                    ClearDesignationSubscription();
                    _designated = null;
                    _state = CounterObservationStateV141.Searching;
                    _candidateSince = now;
                }
                else if (now >= _designationUntil)
                {
                    ClearDesignationSubscription();
                    _designated = null;
                    _state = CounterObservationStateV141.Searching;
                    _candidateSince = now;
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
                float score = CounterObservationModelV141.CandidateScore(enemy.Kind, confidence, distance);
                if (score <= bestScore) continue;
                best = enemy;
                bestScore = score;
                bestConfidence = confidence;
            }

            _candidateCount = eligible;
            _candidateConfidence = bestConfidence;
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

            if (now - _candidateSince >= _profile.DesignationHoldSeconds)
                Designate(best, now);
        }

        private void ResetCandidate(float now)
        {
            _candidate = null;
            _candidateConfidence = 0f;
            _candidateSince = now;
        }

        private void Designate(EnemyTank target, float now)
        {
            if (target == null || target.Health == null || target.Health.IsDead) return;
            ClearDesignationSubscription();
            _designated = target;
            _designatedHealth = target.Health;
            _designatedHealth.Died += OnDesignatedObserverDied;
            _designationUntil = now + _profile.DesignationLeaseSeconds;
            _state = CounterObservationStateV141.Designated;
            _candidate = null;
            _candidateConfidence = 0f;
            _designations++;
            VisualFactory.RingPulse(target.transform.position, new Color(0.16f, 0.92f, 1f), 0.72f);
        }

        private void OnDesignatedObserverDied(Health health)
        {
            if (_state != CounterObservationStateV141.Designated || health == null || health != _designatedHealth) return;
            EnterNetworkBroken(Time.unscaledTime);
        }

        private void EnterNetworkBroken(float now)
        {
            ClearDesignationSubscription();
            _designated = null;
            _candidate = null;
            _candidateConfidence = 0f;
            _state = CounterObservationStateV141.NetworkBroken;
            _stateUntil = now + _profile.NetworkBreakSeconds;
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

            string target = _designated != null
                ? _designated.Kind.ToString().ToUpperInvariant() + "  " + Vector2.Distance(RuntimeBattleRegistry.Player != null ? RuntimeBattleRegistry.Player.transform.position : Vector3.zero, _designated.transform.position).ToString("0.0") + "m"
                : _candidate != null ? "CONTACT " + _candidate.Kind.ToString().ToUpperInvariant() : "NO DESIGNATION";
            GUI.Label(new Rect(x + 10f, y + 30f, 270f, 18f), target, _body);
            GUI.Label(new Rect(x + 10f, y + 49f, 270f, 18f),
                "Observers " + _candidateCount + "/" + CounterObservationModelV141.MaxObserverCandidates +
                "  ·  CB scale " + CounterBatteryAcquisitionScale.ToString("0.00"), _body);
            string hint = _state == CounterObservationStateV141.Searching ? "C sweep + tracked/verified contact to designate" :
                _state == CounterObservationStateV141.NetworkBroken ? "Observer network disrupted" :
                _state == CounterObservationStateV141.Designated ? "Destroy designated observer through normal combat" : "Counter-observation standing by";
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
