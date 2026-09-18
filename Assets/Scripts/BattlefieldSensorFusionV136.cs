using UnityEngine;

namespace TankRevival
{
    public enum SensorContactStateV136
    {
        Unknown = 0,
        Detected = 1,
        Tracked = 2,
        Verified = 3
    }

    public readonly struct SensorContactSampleV136
    {
        public readonly int Round;
        public readonly int Signature;
        public readonly SensorContactStateV136 State;
        public readonly float Confidence;
        public readonly float Distance;
        public readonly float WeatherVisibility;
        public readonly float TerrainVisibility;
        public readonly float ReconQuality;
        public readonly bool SweepBoosted;

        public SensorContactSampleV136(
            int round,
            int signature,
            SensorContactStateV136 state,
            float confidence,
            float distance,
            float weatherVisibility,
            float terrainVisibility,
            float reconQuality,
            bool sweepBoosted)
        {
            Round = round;
            Signature = signature;
            State = state;
            Confidence = confidence;
            Distance = distance;
            WeatherVisibility = weatherVisibility;
            TerrainVisibility = terrainVisibility;
            ReconQuality = reconQuality;
            SweepBoosted = sweepBoosted;
        }
    }

    /// <summary>
    /// Pure deterministic v13.6 sensor-fusion math. It never owns targeting, movement, damage,
    /// spawning, Health, economy or projectile state; it only converts existing read-only
    /// battlefield telemetry into bounded contact confidence.
    /// </summary>
    public static class BattlefieldSensorFusionPlannerV136
    {
        public const int PlannedRounds = 100;
        public const int MaxTrackedContacts = 24;
        public const int MaxWorldMarkers = 8;
        public const float EvaluationCadence = 0.20f;
        public const float ContactMemorySeconds = 2.40f;
        public const float SweepCooldownSeconds = 12.0f;
        public const float SweepDurationSeconds = 2.60f;
        public const float SweepRadius = 11.50f;
        public const float CounterJamWindowSeconds = 3.0f;
        public const float CloseRangeFloor = 3.20f;
        public const float DetectionThreshold = 0.28f;
        public const float TrackingThreshold = 0.48f;
        public const float VerificationThreshold = 0.72f;
        public const float MinWeatherVisibility = 0.58f;
        public const float MinTerrainVisibility = 0.70f;
        public const float MinReconQuality = 0.18f;
        public const float SweepBoost = 0.22f;

        public static bool ConfigurationValid =>
            PlannedRounds == 100 &&
            MaxTrackedContacts == 24 &&
            MaxWorldMarkers >= 4 && MaxWorldMarkers <= 8 &&
            EvaluationCadence >= 0.15f && EvaluationCadence <= 0.35f &&
            ContactMemorySeconds >= 1.5f && ContactMemorySeconds <= 3.5f &&
            SweepCooldownSeconds >= 10f && SweepCooldownSeconds <= 16f &&
            SweepDurationSeconds >= 2f && SweepDurationSeconds <= 3.5f &&
            SweepRadius >= 9f && SweepRadius <= 13f &&
            CounterJamWindowSeconds >= 2f && CounterJamWindowSeconds <= 4f &&
            DetectionThreshold > 0f && DetectionThreshold < TrackingThreshold &&
            TrackingThreshold < VerificationThreshold && VerificationThreshold < 0.85f &&
            MinWeatherVisibility >= BattlefieldWeatherPlannerV135.MinVisibilityScale &&
            MinTerrainVisibility >= 0.65f && MinReconQuality >= 0.15f &&
            SweepBoost >= 0.15f && SweepBoost <= 0.28f;

        public static float EnemySignature(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Fast: return 0.72f;
                case EnemyKind.Sniper: return 0.62f;
                case EnemyKind.Supply: return 0.86f;
                case EnemyKind.Elite: return 0.82f;
                case EnemyKind.Heavy: return 0.90f;
                case EnemyKind.Siege: return 0.94f;
                case EnemyKind.Boss: return 1.00f;
                default: return 0.78f;
            }
        }

        public static float TerrainVisibility(TacticalTerrainKind kind)
        {
            switch (kind)
            {
                case TacticalTerrainKind.Mud: return 0.92f;
                case TacticalTerrainKind.Rubble: return 0.82f;
                case TacticalTerrainKind.Ice: return 0.96f;
                case TacticalTerrainKind.Crater: return 0.74f;
                default: return 1.00f;
            }
        }

        public static float DistanceVisibility(float distance)
        {
            float d = Mathf.Max(0f, distance);
            if (d <= CloseRangeFloor) return 1f;
            return Mathf.Clamp01(1f - (d - CloseRangeFloor) / 14.8f);
        }

        public static float ReconQuality(float rawSignal, bool reconOperationActive)
        {
            if (!reconOperationActive) return 0.45f;
            return Mathf.Clamp(rawSignal, MinReconQuality, 1f);
        }

        public static float Confidence(
            EnemyKind kind,
            float distance,
            float weatherVisibility,
            TacticalTerrainKind terrain,
            float reconSignal,
            bool reconOperationActive,
            bool sweepActive)
        {
            float signature = EnemySignature(kind);
            float range = DistanceVisibility(distance);
            float weather = Mathf.Clamp(weatherVisibility, MinWeatherVisibility, 1f);
            float terrainVisibility = Mathf.Clamp(TerrainVisibility(terrain), MinTerrainVisibility, 1f);
            float recon = ReconQuality(reconSignal, reconOperationActive);

            float score =
                signature * 0.24f +
                range * 0.34f +
                weather * 0.18f +
                terrainVisibility * 0.12f +
                recon * 0.12f;

            if (sweepActive && distance <= SweepRadius)
                score += SweepBoost;

            if (distance <= CloseRangeFloor)
                score = Mathf.Max(score, TrackingThreshold + 0.04f);
            if (kind == EnemyKind.Boss)
                score = Mathf.Max(score, DetectionThreshold + 0.08f);

            return Mathf.Clamp01(score);
        }

        public static SensorContactStateV136 StateForConfidence(float confidence)
        {
            float c = Mathf.Clamp01(confidence);
            if (c >= VerificationThreshold) return SensorContactStateV136.Verified;
            if (c >= TrackingThreshold) return SensorContactStateV136.Tracked;
            if (c >= DetectionThreshold) return SensorContactStateV136.Detected;
            return SensorContactStateV136.Unknown;
        }

        public static int StableSignature(int round, EnemyKind kind, int actorOrdinal)
        {
            int r = Mathf.Clamp(round, 1, PlannedRounds);
            int o = Mathf.Clamp(actorOrdinal, 0, MaxTrackedContacts - 1);
            return unchecked((r * 73856093) ^ (((int)kind + 1) * 19349663) ^ ((o + 1) * 83492791) ^ 1360136);
        }

        public static SensorContactSampleV136 Sample(
            int round,
            EnemyKind kind,
            int actorOrdinal,
            float distance,
            float weatherVisibility,
            TacticalTerrainKind terrain,
            float reconSignal,
            bool reconOperationActive,
            bool sweepActive)
        {
            float confidence = Confidence(kind, distance, weatherVisibility, terrain, reconSignal, reconOperationActive, sweepActive);
            return new SensorContactSampleV136(
                Mathf.Clamp(round, 1, PlannedRounds),
                StableSignature(round, kind, actorOrdinal),
                StateForConfidence(confidence),
                confidence,
                Mathf.Max(0f, distance),
                Mathf.Clamp(weatherVisibility, MinWeatherVisibility, 1f),
                Mathf.Clamp(TerrainVisibility(terrain), MinTerrainVisibility, 1f),
                ReconQuality(reconSignal, reconOperationActive),
                sweepActive && distance <= SweepRadius);
        }
    }

    /// <summary>
    /// v13.6 runtime sensor-fusion layer. It observes RuntimeBattleRegistry, weather, terrain and
    /// the existing Recon/EW service. Contacts are informational only: enemies stay rendered,
    /// EnemyTank keeps target/fire authority and no movement/damage/economy path is introduced.
    /// </summary>
    [DefaultExecutionOrder(710)]
    public sealed class BattlefieldSensorFusionDirector : MonoBehaviour
    {
        public static BattlefieldSensorFusionDirector Instance { get; private set; }

        private readonly EnemyTank[] _contacts = new EnemyTank[BattlefieldSensorFusionPlannerV136.MaxTrackedContacts];
        private readonly float[] _confidence = new float[BattlefieldSensorFusionPlannerV136.MaxTrackedContacts];
        private readonly float[] _lastPositiveAt = new float[BattlefieldSensorFusionPlannerV136.MaxTrackedContacts];
        private readonly SensorContactStateV136[] _states = new SensorContactStateV136[BattlefieldSensorFusionPlannerV136.MaxTrackedContacts];
        private readonly int[] _signatures = new int[BattlefieldSensorFusionPlannerV136.MaxTrackedContacts];
        private readonly int[] _markerIndices = new int[BattlefieldSensorFusionPlannerV136.MaxWorldMarkers];
        private readonly bool[] _retained = new bool[BattlefieldSensorFusionPlannerV136.MaxTrackedContacts];
        private readonly float[] _markerPriority = new float[BattlefieldSensorFusionPlannerV136.MaxWorldMarkers];

        private TankGame _game;
        private Camera _camera;
        private int _round = -1;
        private int _contactCount;
        private int _detectedCount;
        private int _trackedCount;
        private int _verifiedCount;
        private int _sweepsUsed;
        private float _nextEvaluation;
        private float _sweepUntil;
        private float _nextSweepReady;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _marker;

        public int ContactCount => _contactCount;
        public int DetectedCount => _detectedCount;
        public int TrackedCount => _trackedCount;
        public int VerifiedCount => _verifiedCount;
        public int SweepsUsed => _sweepsUsed;
        public bool SweepActive => Time.time < _sweepUntil;
        public float SweepRemaining => SweepActive ? Mathf.Max(0f, _sweepUntil - Time.time) : 0f;
        public float SweepCooldownRemaining => Mathf.Max(0f, _nextSweepReady - Time.time);
        public static bool ConfigurationValid => BattlefieldSensorFusionPlannerV136.ConfigurationValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static BattlefieldSensorFusionDirector EnsureInstalled()
        {
            if (Instance != null) return Instance;
            BattlefieldSensorFusionDirector existing = FindAnyObjectByType<BattlefieldSensorFusionDirector>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            GameObject go = new GameObject("BattlefieldSensorFusionDirector_v13_6");
            DontDestroyOnLoad(go);
            return go.AddComponent<BattlefieldSensorFusionDirector>();
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
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0 || _contactCount > 0) ResetRun();
                return;
            }

            int currentRound = Mathf.Clamp(_game.CurrentRound, 1, BattlefieldSensorFusionPlannerV136.PlannedRounds);
            if (_round != currentRound)
            {
                _round = currentRound;
                ResetContacts();
                _nextEvaluation = Time.time;
            }

            if (Input.GetKeyDown(KeyCode.C))
                TryActivateSweep();

            if (Time.time >= _nextEvaluation)
            {
                _nextEvaluation = Time.time + BattlefieldSensorFusionPlannerV136.EvaluationCadence;
                EvaluateContacts();
            }
        }

        public bool TryActivateSweep()
        {
            if (_game == null || !_game.IsPlaying || Time.time < _nextSweepReady) return false;
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead) return false;

            _sweepUntil = Time.time + BattlefieldSensorFusionPlannerV136.SweepDurationSeconds;
            _nextSweepReady = Time.time + BattlefieldSensorFusionPlannerV136.SweepCooldownSeconds;
            _sweepsUsed++;

            VisualFactory.RingPulse(player.transform.position, new Color(0.14f, 0.88f, 1f), BattlefieldSensorFusionPlannerV136.SweepRadius * 0.72f);
            BattleAudio.PlayGlobal(SoundCue.AmmoPickup, 0.18f, 0.02f);

            ReconElectronicWarfareDirector recon = ReconElectronicWarfareDirector.Instance;
            if (recon != null && recon.OperationActive)
                recon.ApplyCounterJamming(BattlefieldSensorFusionPlannerV136.CounterJamWindowSeconds);

            _nextEvaluation = 0f;
            return true;
        }

        private void EvaluateContacts()
        {
            PlayerTank player = CombatRoster.Player;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (player == null || player.Health == null || player.Health.IsDead || enemies == null)
            {
                DecayAllContacts();
                RecountStates();
                return;
            }

            Vector2 playerPosition = player.transform.position;
            BattlefieldWeatherPlanV135 weather = BattlefieldWeatherDirector.Instance != null
                ? BattlefieldWeatherDirector.Instance.CurrentPlan
                : BattlefieldWeatherPlannerV135.PlanForRound(_round > 0 ? _round : 1, 0);

            ReconElectronicWarfareDirector recon = ReconElectronicWarfareDirector.Instance;
            bool reconActive = recon != null && recon.OperationActive;
            float reconSignal = reconActive ? recon.SignalQuality : 0.45f;
            bool sweep = SweepActive;

            for (int i = 0; i < _retained.Length; i++) _retained[i] = false;
            int ordinal = 0;
            for (int i = 0; i < enemies.Length && ordinal < BattlefieldSensorFusionPlannerV136.MaxTrackedContacts; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;

                int slot = FindSlot(enemy);
                if (slot < 0) slot = FindFreeSlot();
                if (slot < 0) break;
                _retained[slot] = true;
                _contacts[slot] = enemy;

                float distance = Vector2.Distance(playerPosition, enemy.transform.position);
                TacticalTerrainKind terrain = TacticalTerrainMap.TerrainAt(enemy.transform.position);
                SensorContactSampleV136 sample = BattlefieldSensorFusionPlannerV136.Sample(
                    _round, enemy.Kind, ordinal, distance, weather.VisibilityScale, terrain,
                    reconSignal, reconActive, sweep);

                float previous = _confidence[slot];
                float step = sample.Confidence >= previous ? 0.18f : 0.09f;
                float next = Mathf.MoveTowards(previous, sample.Confidence, step);
                if (sample.State >= SensorContactStateV136.Detected)
                    _lastPositiveAt[slot] = Time.time;
                else if (_lastPositiveAt[slot] > 0f && Time.time - _lastPositiveAt[slot] <= BattlefieldSensorFusionPlannerV136.ContactMemorySeconds)
                    next = Mathf.Max(next, BattlefieldSensorFusionPlannerV136.DetectionThreshold + 0.01f);

                // Anti-blindness floors are enforced in the live tracker as well as pure planner math.
                if (distance <= BattlefieldSensorFusionPlannerV136.CloseRangeFloor)
                    next = Mathf.Max(next, BattlefieldSensorFusionPlannerV136.TrackingThreshold + 0.01f);
                if (enemy.Kind == EnemyKind.Boss)
                    next = Mathf.Max(next, BattlefieldSensorFusionPlannerV136.DetectionThreshold + 0.01f);

                _confidence[slot] = Mathf.Clamp01(next);
                _states[slot] = BattlefieldSensorFusionPlannerV136.StateForConfidence(_confidence[slot]);
                _signatures[slot] = sample.Signature;
                ordinal++;
            }

            for (int i = 0; i < _contacts.Length; i++)
            {
                if (_contacts[i] == null) continue;
                if (_retained[i]) continue;
                _confidence[i] = Mathf.MoveTowards(_confidence[i], 0f, 0.18f);
                if (_confidence[i] < BattlefieldSensorFusionPlannerV136.DetectionThreshold ||
                    Time.time - _lastPositiveAt[i] > BattlefieldSensorFusionPlannerV136.ContactMemorySeconds)
                    ClearSlot(i);
                else
                    _states[i] = BattlefieldSensorFusionPlannerV136.StateForConfidence(_confidence[i]);
            }

            RecountStates();
        }

        private void DecayAllContacts()
        {
            for (int i = 0; i < _contacts.Length; i++)
            {
                if (_contacts[i] == null) continue;
                _confidence[i] = Mathf.MoveTowards(_confidence[i], 0f, 0.18f);
                if (_confidence[i] < BattlefieldSensorFusionPlannerV136.DetectionThreshold)
                    ClearSlot(i);
                else
                    _states[i] = BattlefieldSensorFusionPlannerV136.StateForConfidence(_confidence[i]);
            }
        }

        private int FindSlot(EnemyTank enemy)
        {
            for (int i = 0; i < _contacts.Length; i++)
                if (_contacts[i] == enemy) return i;
            return -1;
        }

        private int FindFreeSlot()
        {
            for (int i = 0; i < _contacts.Length; i++)
                if (_contacts[i] == null) return i;
            return -1;
        }

        private void ClearSlot(int index)
        {
            if (index < 0 || index >= _contacts.Length) return;
            _contacts[index] = null;
            _confidence[index] = 0f;
            _lastPositiveAt[index] = 0f;
            _states[index] = SensorContactStateV136.Unknown;
            _signatures[index] = 0;
        }

        private void ResetContacts()
        {
            for (int i = 0; i < _contacts.Length; i++) ClearSlot(i);
            _contactCount = _detectedCount = _trackedCount = _verifiedCount = 0;
        }

        private void ResetRun()
        {
            ResetContacts();
            _round = -1;
            _nextEvaluation = 0f;
            _sweepUntil = 0f;
            _nextSweepReady = 0f;
            _sweepsUsed = 0;
        }

        private void RecountStates()
        {
            _contactCount = _detectedCount = _trackedCount = _verifiedCount = 0;
            for (int i = 0; i < _contacts.Length; i++)
            {
                if (_contacts[i] == null) continue;
                _contactCount++;
                if (_states[i] >= SensorContactStateV136.Detected) _detectedCount++;
                if (_states[i] >= SensorContactStateV136.Tracked) _trackedCount++;
                if (_states[i] >= SensorContactStateV136.Verified) _verifiedCount++;
            }
        }

        public bool TryGetContact(EnemyTank enemy, out SensorContactStateV136 state, out float confidence, out int signature)
        {
            int slot = FindSlot(enemy);
            if (slot < 0)
            {
                state = SensorContactStateV136.Unknown;
                confidence = 0f;
                signature = 0;
                return false;
            }
            state = _states[slot];
            confidence = _confidence[slot];
            signature = _signatures[slot];
            return state != SensorContactStateV136.Unknown;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.38f, 0.90f, 1f) }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.80f, 0.90f, 0.96f) }
            };
            _marker = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.52f, 0.94f, 1f) }
            };
        }

        private void BuildMarkerSelection()
        {
            for (int i = 0; i < _markerIndices.Length; i++)
            {
                _markerIndices[i] = -1;
                _markerPriority[i] = -1f;
            }

            PlayerTank player = CombatRoster.Player;
            Vector2 playerPosition = player != null ? (Vector2)player.transform.position : Vector2.zero;

            for (int contact = 0; contact < _contacts.Length; contact++)
            {
                EnemyTank enemy = _contacts[contact];
                if (enemy == null || _states[contact] < SensorContactStateV136.Tracked) continue;
                float distance = Vector2.Distance(playerPosition, enemy.transform.position);
                float classBoost = enemy.Kind == EnemyKind.Boss ? 0.30f :
                    enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Elite ? 0.16f : 0f;
                float priority = _confidence[contact] + classBoost + Mathf.Clamp01(1f - distance / 18f) * 0.18f;

                for (int slot = 0; slot < _markerIndices.Length; slot++)
                {
                    if (priority <= _markerPriority[slot]) continue;
                    for (int shift = _markerIndices.Length - 1; shift > slot; shift--)
                    {
                        _markerPriority[shift] = _markerPriority[shift - 1];
                        _markerIndices[shift] = _markerIndices[shift - 1];
                    }
                    _markerPriority[slot] = priority;
                    _markerIndices[slot] = contact;
                    break;
                }
            }
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            float x = Mathf.Max(12f, Screen.width - 292f);
            GUI.color = new Color(0.02f, 0.04f, 0.065f, 0.90f);
            GUI.Box(new Rect(x, 151f, 276f, 72f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 10f, 157f, 250f, 18f), "SENSOR FUSION // CONTACT WARFARE", _header);
            GUI.Label(new Rect(x + 10f, 178f, 252f, 16f),
                "Detected " + _detectedCount + "  ·  Tracked " + _trackedCount + "  ·  Verified " + _verifiedCount, _body);
            string sweep = SweepActive
                ? "SWEEP ACTIVE " + SweepRemaining.ToString("0.0") + "s"
                : SweepCooldownRemaining <= 0f ? "C: ACTIVE SWEEP READY" : "Sweep cooldown " + SweepCooldownRemaining.ToString("0.0") + "s";
            GUI.Label(new Rect(x + 10f, 195f, 252f, 16f), sweep, _body);

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            BuildMarkerSelection();
            PlayerTank player = CombatRoster.Player;
            Vector2 playerPosition = player != null ? (Vector2)player.transform.position : Vector2.zero;
            for (int i = 0; i < _markerIndices.Length; i++)
            {
                int contact = _markerIndices[i];
                if (contact < 0) break;
                EnemyTank enemy = _contacts[contact];
                if (enemy == null) continue;
                Vector3 screen = _camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * 0.78f);
                if (screen.z < 0f) continue;
                float distance = Vector2.Distance(playerPosition, enemy.transform.position);
                string label = _states[contact] == SensorContactStateV136.Verified
                    ? enemy.Kind.ToString().ToUpperInvariant() + "  " + distance.ToString("0.0") + "m  " + Mathf.RoundToInt(_confidence[contact] * 100f) + "%"
                    : "TRACK  " + distance.ToString("0.0") + "m";
                float gy = Screen.height - screen.y;
                GUI.Label(new Rect(screen.x - 70f, gy - 10f, 140f, 20f), label, _marker);
            }
        }
    }
}
