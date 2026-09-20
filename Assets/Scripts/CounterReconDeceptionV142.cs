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
            MinDecoyOffset >= 1.5f && MaxDecoyOffset <= 4.0f;

        public static float ReacquisitionDelayForRound(int requestedRound)
        {
            int round = Mathf.Clamp(requestedRound, 1, PlannedRounds);
            float t = (round - 1f) / 99f;
            return Mathf.Lerp(MaxReacquisitionDelaySeconds, MinReacquisitionDelaySeconds, t);
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
        public float ReacquisitionRemaining => Mathf.Max(0f, _reacquireUntil - Time.unscaledTime);
        public CounterReconStateV142 State => EmconActive ? CounterReconStateV142.EmconRelocating : DecoyActive ? CounterReconStateV142.DecoyActive : ReacquisitionBlocked ? CounterReconStateV142.Reacquiring : CounterReconStateV142.Ready;

        public static float CounterBatteryExposureScale => Instance == null ? 1f : Instance.ExposureScale;
        public static float CounterBatteryAcquisitionScale => Instance == null ? 1f : Instance.AcquisitionScale;
        public static float DesignationHoldScale => Instance != null && Instance.EmconActive ? CounterReconDeceptionModelV142.EmconDesignationHoldScale : 1f;

        private float ExposureScale => EmconActive
            ? CounterReconDeceptionModelV142.EmconExposureScale
            : DecoyActive ? CounterReconDeceptionModelV142.DecoyExposureScale : 1f;

        private float AcquisitionScale => ReacquisitionBlocked
            ? 0f
            : EmconActive ? CounterReconDeceptionModelV142.EmconAcquisitionScale : 1f;

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
            return Instance != null && Instance.DecoyActive ? Instance._decoyPosition : truePosition;
        }

        public static Vector2 ResolveLockPosition(Vector2 truePosition, Vector2 lastReportedSignature)
        {
            if (Instance == null) return truePosition;
            if (Instance.DecoyActive) return Instance._decoyPosition;
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
            _decoyUntil = _decoyCooldownUntil = 0f;
            _emconUntil = _emconCooldownUntil = 0f;
            _reacquireUntil = 0f;
            _decoyPosition = Vector2.zero;
            _emconStartPosition = Vector2.zero;
            _lastObservationState = CounterObservationStateV141.Idle;
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureRound(_game.CurrentRound);
            ObserveNetworkBreak();

            PlayerTank player = RuntimeBattleRegistry.Player;
            if (player == null) return;

            if (Input.GetKeyDown(KeyCode.G)) TryDeployDecoy(player.transform.position);
            if (Input.GetKeyDown(KeyCode.V)) TryStartEmcon(player.transform.position);

            if (EmconActive && Vector2.Distance(player.transform.position, _emconStartPosition) >= CounterBatteryModelV140.MinimumPhysicalBreakDistance)
                ExtendReacquisition(CounterReconDeceptionModelV142.ReacquisitionDelayForRound(_round));
        }

        private void EnsureRound(int requestedRound)
        {
            int round = Mathf.Clamp(requestedRound, 1, CounterReconDeceptionModelV142.PlannedRounds);
            if (_round == round) return;
            _round = round;
            _decoyCharges = CounterReconDeceptionModelV142.MaxDecoyChargesPerRound;
            _decoyUntil = _decoyCooldownUntil = 0f;
            _emconUntil = _emconCooldownUntil = 0f;
            _reacquireUntil = 0f;
            CounterObservationDirectorV141 observation = CounterObservationDirectorV141.Instance;
            _lastObservationState = observation != null ? observation.State : CounterObservationStateV141.Idle;
        }

        private void ObserveNetworkBreak()
        {
            CounterObservationDirectorV141 observation = CounterObservationDirectorV141.Instance;
            CounterObservationStateV141 state = observation != null ? observation.State : CounterObservationStateV141.Idle;
            if (state == CounterObservationStateV141.NetworkBroken && _lastObservationState != CounterObservationStateV141.NetworkBroken)
                ExtendReacquisition(CounterReconDeceptionModelV142.ReacquisitionDelayForRound(_round));
            _lastObservationState = state;
        }

        private bool TryDeployDecoy(Vector2 playerPosition)
        {
            float now = Time.unscaledTime;
            if (_decoyCharges <= 0 || now < _decoyCooldownUntil || EmconActive) return false;
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
            _emconActivations++;
            _emconStartPosition = playerPosition;
            _emconUntil = now + CounterReconDeceptionModelV142.EmconLifetimeSeconds;
            _emconCooldownUntil = now + CounterReconDeceptionModelV142.EmconCooldownSeconds;
            ExtendReacquisition(CounterReconDeceptionModelV142.ReacquisitionDelayForRound(_round));
            return true;
        }

        private void ExtendReacquisition(float seconds)
        {
            _reacquireUntil = Mathf.Max(_reacquireUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
        }
    }
}
