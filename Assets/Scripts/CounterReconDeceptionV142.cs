using UnityEngine;
using UnityEngine.SceneManagement;

namespace TankRevival
{
    public enum CounterReconStateV142
    {
        Ready = 0,
        DecoyActive = 1,
        EmconRelocating = 2,
        Reacquiring = 3
    }

    /// <summary>
    /// Pure deterministic v14.2 deception / EMCON tuning. This layer changes only hostile
    /// observation inputs and timing. It never owns damage, spawning, movement or projectiles.
    /// </summary>
    public static class CounterReconDeceptionModelV142
    {
        public const int PlannedRounds = 100;
        public const int MaxDecoyChargesPerRound = 2;
        public const float DecoyLifetimeSeconds = 6.0f;
        public const float DecoyCooldownSeconds = 11.0f;
        public const float EmconLifetimeSeconds = 4.5f;
        public const float EmconCooldownSeconds = 13.0f;
        public const float MinReacquisitionDelaySeconds = 2.8f;
        public const float MaxReacquisitionDelaySeconds = 4.6f;
        public const float DecoyExposureScale = 0.68f;
        public const float EmconExposureScale = 0.48f;
        public const float EmconAcquisitionScale = 0.58f;
        public const float EmconDesignationHoldScale = 1.55f;
        public const float MinDecoyOffset = 2.1f;
        public const float MaxDecoyOffset = 3.4f;

        // v14.2 phase two: bounded adaptation. Repeated player deception becomes less credible,
        // but never collapses to zero value and never grants or creates scripted immunity.
        public const float SuspicionPerDecoy = 0.18f;
        public const float MaxSuspicion01 = 0.72f;
        public const float MinDecoyCredibility01 = 0.42f;
        public const float MaxDecoyCredibility01 = 0.92f;
        public const float MinReacquisitionAcquisitionScale = 0.22f;
        public const float MaxReacquisitionAcquisitionScale = 0.38f;
        public const float MinContextEmconExposureScale = 0.40f;
        public const float MaxContextEmconExposureScale = 0.62f;

        public static bool ConfigurationValid =>
            PlannedRounds == CounterBatteryModelV140.PlannedRounds &&
            MaxDecoyChargesPerRound >= 1 && MaxDecoyChargesPerRound <= 3 &&
            DecoyLifetimeSeconds >= 4f && DecoyLifetimeSeconds <= 8f &&
            DecoyCooldownSeconds >= DecoyLifetimeSeconds && DecoyCooldownSeconds <= 16f &&
            EmconLifetimeSeconds >= 3f && EmconLifetimeSeconds <= 6f &&
            EmconCooldownSeconds >= 10f && EmconCooldownSeconds <= 18f &&
            MinReacquisitionDelaySeconds >= 2f && MaxReacquisitionDelaySeconds <= 6f &&
            MinReacquisitionDelaySeconds < MaxReacquisitionDelaySeconds &&
            DecoyExposureScale >= 0.55f && DecoyExposureScale <= 0.80f &&
            EmconExposureScale >= 0.35f && EmconExposureScale <= 0.65f &&
            EmconAcquisitionScale >= 0.45f && EmconAcquisitionScale <= 0.70f &&
            EmconDesignationHoldScale >= 1.25f && EmconDesignationHoldScale <= 1.80f &&
            MinDecoyOffset >= 1.5f && MaxDecoyOffset <= 4.0f &&
            SuspicionPerDecoy > 0f && MaxSuspicion01 > SuspicionPerDecoy && MaxSuspicion01 < 1f &&
            MinDecoyCredibility01 > 0f && MaxDecoyCredibility01 < 1f && MinDecoyCredibility01 < MaxDecoyCredibility01 &&
            MinReacquisitionAcquisitionScale > 0f && MaxReacquisitionAcquisitionScale < 1f &&
            MinReacquisitionAcquisitionScale < MaxReacquisitionAcquisitionScale &&
            MinContextEmconExposureScale > 0f && MaxContextEmconExposureScale < 1f &&
            MinContextEmconExposureScale < MaxContextEmconExposureScale;

        public static float ReacquisitionDelayForRound(int requestedRound)
        {
            int round = Mathf.Clamp(requestedRound, 1, PlannedRounds);
            float t = (round - 1f) / 99f;
            return Mathf.Lerp(MaxReacquisitionDelaySeconds, MinReacquisitionDelaySeconds, t);
        }

        public static float SuspicionAfterDecoy(float currentSuspicion01)
        {
            return Mathf.Clamp(currentSuspicion01 + SuspicionPerDecoy, 0f, MaxSuspicion01);
        }

        public static float DecoyCredibility01(float suspicion01, float observerResilience01)
        {
            float suspicion = Mathf.Clamp01(suspicion01 / Mathf.Max(0.001f, MaxSuspicion01));
            float resilience = Mathf.Clamp01(observerResilience01);
            float credibility = 0.92f - suspicion * 0.34f - resilience * 0.20f;
            return Mathf.Clamp(credibility, MinDecoyCredibility01, MaxDecoyCredibility01);
        }

        public static float EmconExposureForResilience(float observerResilience01)
        {
            float resilience = Mathf.Clamp01(observerResilience01);
            return Mathf.Clamp(EmconExposureScale + resilience * 0.14f, MinContextEmconExposureScale, MaxContextEmconExposureScale);
        }

        public static float ReacquisitionAcquisitionForResilience(float observerResilience01)
        {
            return Mathf.Lerp(MinReacquisitionAcquisitionScale, MaxReacquisitionAcquisitionScale, Mathf.Clamp01(observerResilience01));
        }

        public static float ReacquisitionDelayForContext(int requestedRound, float observerResilience01, float suspicion01)
        {
            float baseDelay = ReacquisitionDelayForRound(requestedRound);
            float resilienceScale = Mathf.Lerp(1.08f, 0.82f, Mathf.Clamp01(observerResilience01));
            float suspicionScale = Mathf.Lerp(1.0f, 0.90f, Mathf.Clamp01(suspicion01 / Mathf.Max(0.001f, MaxSuspicion01)));
            return Mathf.Clamp(baseDelay * resilienceScale * suspicionScale, MinReacquisitionDelaySeconds, MaxReacquisitionDelaySeconds);
        }

        public static Vector2 DecoyOffset(int round, int ordinal)
        {
            int boundedRound = Mathf.Clamp(round, 1, PlannedRounds);
            int hash = unchecked(142 * 1009 + boundedRound * 97 + ordinal * 53);
            float x = ((hash & 1) == 0 ? -1f : 1f) * Mathf.Lerp(MinDecoyOffset, MaxDecoyOffset, ((hash >> 1) & 7) / 7f);
            float y = (((hash >> 4) & 1) == 0 ? -1f : 1f) * Mathf.Lerp(0.85f, 1.65f, ((hash >> 5) & 7) / 7f);
            return new Vector2(x, y);
        }
    }

    /// <summary>
    /// Player counter-recon utility for v14.2. G deploys one finite false emission; V starts a
    /// finite EMCON relocation window. Both operate as bounded modifiers over v14.0/v14.1.
    /// </summary>
    [DefaultExecutionOrder(-8810)]
    public sealed class CounterReconDeceptionDirectorV142 : MonoBehaviour
    {
        public static CounterReconDeceptionDirectorV142 Instance { get; private set; }

        private TankGame _game;
        private int _round = -1;
        private int _decoyCharges;
        private int _decoysDeployed;
        private int _emconActivations;
        private float _decoyUntil;
        private float _decoyCooldownUntil;
        private float _emconUntil;
        private float _emconCooldownUntil;
        private float _reacquireUntil;
        private float _suspicion01;
        private float _decoyCredibility01 = CounterReconDeceptionModelV142.MaxDecoyCredibility01;
        private float _observerResilience01;
        private bool _emconRelocationCredited;
        private Vector2 _decoyPosition;
        private Vector2 _emconStartPosition;
        private CounterObservationStateV141 _lastObservationState = CounterObservationStateV141.Idle;

        public int DecoyCharges => _decoyCharges;
        public int DecoysDeployed => _decoysDeployed;
        public int EmconActivations => _emconActivations;
        public bool DecoyActive => Time.unscaledTime < _decoyUntil;
        public bool EmconActive => Time.unscaledTime < _emconUntil;
        public bool ReacquisitionBlocked => Time.unscaledTime < _reacquireUntil;
        public Vector2 DecoyPosition => _decoyPosition;
        public float Suspicion01 => _suspicion01;
        public float DecoyCredibility01 => _decoyCredibility01;
        public float ObserverResilience01 => _observerResilience01;
        public float ReacquisitionRemaining => Mathf.Max(0f, _reacquireUntil - Time.unscaledTime);
        public CounterReconStateV142 State => EmconActive ? CounterReconStateV142.EmconRelocating : DecoyActive ? CounterReconStateV142.DecoyActive : ReacquisitionBlocked ? CounterReconStateV142.Reacquiring : CounterReconStateV142.Ready;

        public static float CounterBatteryExposureScale => Instance == null ? 1f : Instance.ExposureScale;
        public static float CounterBatteryAcquisitionScale => Instance == null ? 1f : Instance.AcquisitionScale;
        public static float DesignationHoldScale => Instance != null && Instance.EmconActive ? CounterReconDeceptionModelV142.EmconDesignationHoldScale : 1f;

        private float ExposureScale
        {
            get
            {
                if (EmconActive)
                    return CounterReconDeceptionModelV142.EmconExposureForResilience(_observerResilience01);
                if (DecoyActive)
                    return Mathf.Lerp(1f, CounterReconDeceptionModelV142.DecoyExposureScale, _decoyCredibility01);
                return 1f;
            }
        }

        private float AcquisitionScale
        {
            get
            {
                if (ReacquisitionBlocked)
                    return CounterReconDeceptionModelV142.ReacquisitionAcquisitionForResilience(_observerResilience01);
                if (EmconActive)
                    return Mathf.Lerp(CounterReconDeceptionModelV142.EmconAcquisitionScale, 0.78f, Mathf.Clamp01(_observerResilience01));
                return 1f;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static CounterReconDeceptionDirectorV142 EnsureInstalled()
        {
            if (Instance != null) return Instance;
            CounterReconDeceptionDirectorV142 existing = FindAnyObjectByType<CounterReconDeceptionDirectorV142>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            GameObject go = new GameObject("CounterReconDeceptionDirector_v14_2");
            DontDestroyOnLoad(go);
            return go.AddComponent<CounterReconDeceptionDirectorV142>();
        }

        public static Vector2 ResolveSupportSignaturePosition(Vector2 truePosition)
        {
            if (Instance == null || !Instance.DecoyActive) return truePosition;
            return Vector2.Lerp(truePosition, Instance._decoyPosition, Instance._decoyCredibility01);
        }

        public static Vector2 ResolveLockPosition(Vector2 truePosition, Vector2 lastReportedSignature)
        {
            if (Instance == null) return truePosition;
            if (Instance.DecoyActive)
                return Vector2.Lerp(truePosition, Instance._decoyPosition, Instance._decoyCredibility01);
            if (Instance.ReacquisitionBlocked) return lastReportedSignature;
            return truePosition;
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
            CounterBatteryDirectorV140.EnsureInstalled();
            CounterObservationDirectorV141.EnsureInstalled();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            _game = FindAnyObjectByType<TankGame>();
            ResetRuntime();
        }

        private void ResetRuntime()
        {
            _round = -1;
            _decoyCharges = 0;
            _decoysDeployed = 0;
            _emconActivations = 0;
            _decoyUntil = _decoyCooldownUntil = 0f;
            _emconUntil = _emconCooldownUntil = 0f;
            _reacquireUntil = 0f;
            _suspicion01 = 0f;
            _decoyCredibility01 = CounterReconDeceptionModelV142.MaxDecoyCredibility01;
            _observerResilience01 = 0f;
            _emconRelocationCredited = false;
            _decoyPosition = Vector2.zero;
            _emconStartPosition = Vector2.zero;
            _lastObservationState = CounterObservationStateV141.Idle;
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureRound(_game.CurrentRound);
            RefreshObserverResilience();
            ObserveNetworkBreak();

            PlayerTank player = RuntimeBattleRegistry.Player;
            if (player == null) return;

            if (Input.GetKeyDown(KeyCode.G)) TryDeployDecoy(player.transform.position);
            if (Input.GetKeyDown(KeyCode.V)) TryStartEmcon(player.transform.position);

            if (EmconActive &&
                !_emconRelocationCredited &&
                Vector2.Distance(player.transform.position, _emconStartPosition) >= CounterBatteryModelV140.MinimumPhysicalBreakDistance)
            {
                _emconRelocationCredited = true;
                ExtendReacquisition(CounterReconDeceptionModelV142.ReacquisitionDelayForContext(_round, _observerResilience01, _suspicion01));
            }
        }

        private void EnsureRound(int requestedRound)
        {
            int round = Mathf.Clamp(requestedRound, 1, CounterReconDeceptionModelV142.PlannedRounds);
            if (_round == round) return;
            _round = round;
            _decoyCharges = CounterReconDeceptionModelV142.MaxDecoyChargesPerRound;
            _decoysDeployed = 0;
            _emconActivations = 0;
            _decoyUntil = _decoyCooldownUntil = 0f;
            _emconUntil = _emconCooldownUntil = 0f;
            _reacquireUntil = 0f;
            _suspicion01 = 0f;
            _decoyCredibility01 = CounterReconDeceptionModelV142.MaxDecoyCredibility01;
            _emconRelocationCredited = false;
            CounterObservationDirectorV141 observation = CounterObservationDirectorV141.Instance;
            _lastObservationState = observation != null ? observation.State : CounterObservationStateV141.Idle;
            RefreshObserverResilience();
        }

        private void RefreshObserverResilience()
        {
            CounterObservationDirectorV141 observation = CounterObservationDirectorV141.Instance;
            if (observation == null)
            {
                _observerResilience01 = 0f;
                return;
            }

            CounterObservationTargetSnapshotV141 snapshot = observation.TargetSnapshot;
            _observerResilience01 = snapshot.Valid
                ? Mathf.Clamp01(snapshot.Resilience01)
                : Mathf.Clamp01(observation.CandidateResilience01);
        }

        private void ObserveNetworkBreak()
        {
            CounterObservationDirectorV141 observation = CounterObservationDirectorV141.Instance;
            CounterObservationStateV141 state = observation != null ? observation.State : CounterObservationStateV141.Idle;
            if (state == CounterObservationStateV141.NetworkBroken && _lastObservationState != CounterObservationStateV141.NetworkBroken)
                ExtendReacquisition(CounterReconDeceptionModelV142.ReacquisitionDelayForContext(_round, _observerResilience01, _suspicion01));
            _lastObservationState = state;
        }

        private bool TryDeployDecoy(Vector2 playerPosition)
        {
            float now = Time.unscaledTime;
            if (_decoyCharges <= 0 || now < _decoyCooldownUntil || EmconActive) return false;

            RefreshObserverResilience();
            _decoyCredibility01 = CounterReconDeceptionModelV142.DecoyCredibility01(_suspicion01, _observerResilience01);
            _suspicion01 = CounterReconDeceptionModelV142.SuspicionAfterDecoy(_suspicion01);

            _decoyCharges--;
            _decoysDeployed++;
            _decoyPosition = playerPosition + CounterReconDeceptionModelV142.DecoyOffset(_round, _decoysDeployed);
            _decoyUntil = now + CounterReconDeceptionModelV142.DecoyLifetimeSeconds;
            _decoyCooldownUntil = now + CounterReconDeceptionModelV142.DecoyCooldownSeconds;
            return true;
        }

        private bool TryStartEmcon(Vector2 playerPosition)
        {
            float now = Time.unscaledTime;
            if (now < _emconCooldownUntil || DecoyActive) return false;

            RefreshObserverResilience();
            _emconActivations++;
            _emconStartPosition = playerPosition;
            _emconRelocationCredited = false;
            _emconUntil = now + CounterReconDeceptionModelV142.EmconLifetimeSeconds;
            _emconCooldownUntil = now + CounterReconDeceptionModelV142.EmconCooldownSeconds;
            ExtendReacquisition(CounterReconDeceptionModelV142.ReacquisitionDelayForContext(_round, _observerResilience01, _suspicion01));
            return true;
        }

        private void ExtendReacquisition(float seconds)
        {
            _reacquireUntil = Mathf.Max(_reacquireUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
        }
    }
}
