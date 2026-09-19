using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TankRevival
{
    public enum CounterBatteryStateV140
    {
        Quiet = 0,
        Searching = 1,
        Locked = 2,
        Barrage = 3,
        Relocating = 4
    }

    public readonly struct CounterBatteryProfileV140
    {
        public readonly int Round;
        public readonly int ShellBudget;
        public readonly float LockThreshold;
        public readonly float CooldownSeconds;
        public readonly float AcquisitionScale;
        public readonly int Signature;

        public CounterBatteryProfileV140(
            int round,
            int shellBudget,
            float lockThreshold,
            float cooldownSeconds,
            float acquisitionScale,
            int signature)
        {
            Round = round;
            ShellBudget = shellBudget;
            LockThreshold = lockThreshold;
            CooldownSeconds = cooldownSeconds;
            AcquisitionScale = acquisitionScale;
            Signature = signature;
        }
    }

    public readonly struct CounterBatteryStrikeIntentV140
    {
        public readonly Vector2 Origin;
        public readonly Vector2 Aim;
        public readonly Vector2 Direction;
        public readonly int Damage;
        public readonly float Speed;
        public readonly AmmoType Ammo;
        public readonly int StrikeOrdinal;
        public readonly int RoundSignature;

        public CounterBatteryStrikeIntentV140(
            Vector2 origin,
            Vector2 aim,
            Vector2 direction,
            int damage,
            float speed,
            AmmoType ammo,
            int strikeOrdinal,
            int roundSignature)
        {
            Origin = origin;
            Aim = aim;
            Direction = direction;
            Damage = damage;
            Speed = speed;
            Ammo = ammo;
            StrikeOrdinal = strikeOrdinal;
            RoundSignature = roundSignature;
        }
    }

    /// <summary>
    /// Pure deterministic v14.0 counter-battery model. It converts repeated player fire-support
    /// signatures into bounded enemy search pressure. It owns no Health, spawn, movement or projectile
    /// resolution authority.
    /// </summary>
    public static class CounterBatteryModelV140
    {
        public const int PlannedRounds = 100;
        public const int MaxTrackedHostiles = 24;
        public const int MaxObservers = 6;
        public const int MaxShells = 4;
        public const float SampleCadenceSeconds = 0.25f;
        public const float ExposurePerSupportStrike = 0.18f;
        public const float RepeatUseMultiplier = 1.30f;
        public const float RelocatedUseMultiplier = 0.64f;
        public const float ExposureDecayPerSecond = 0.055f;
        public const float SearchThreshold = 0.28f;
        public const float BreakDistance = 3.0f;
        public const float LockWarningSeconds = 1.85f;
        public const float BarrageCadenceSeconds = 0.92f;
        public const float MinCooldownSeconds = 18f;
        public const float MaxCooldownSeconds = 24f;

        public static bool ConfigurationValid =>
            PlannedRounds == 100 &&
            MaxTrackedHostiles == BattlefieldFireSupportModelV139.MaxTrackedHostiles &&
            MaxObservers > 0 && MaxObservers <= 6 &&
            MaxShells >= 2 && MaxShells <= BattlefieldFireSupportModelV139.MaxStrikes &&
            SampleCadenceSeconds >= 0.20f && SampleCadenceSeconds <= 0.50f &&
            ExposurePerSupportStrike > 0f && ExposurePerSupportStrike <= 0.25f &&
            RepeatUseMultiplier > 1f && RepeatUseMultiplier <= 1.5f &&
            RelocatedUseMultiplier >= 0.5f && RelocatedUseMultiplier < 1f &&
            ExposureDecayPerSecond > 0f && SearchThreshold >= 0.20f && SearchThreshold <= 0.40f &&
            BreakDistance >= 2.5f && BreakDistance <= 4.5f &&
            LockWarningSeconds >= 1.5f && LockWarningSeconds <= 3f &&
            BarrageCadenceSeconds >= 0.75f && BarrageCadenceSeconds <= 1.20f &&
            MinCooldownSeconds >= 15f && MaxCooldownSeconds <= 28f && MinCooldownSeconds < MaxCooldownSeconds;

        public static CounterBatteryProfileV140 ProfileForRound(int requestedRound)
        {
            int round = Mathf.Clamp(requestedRound, 1, PlannedRounds);
            int band = Mathf.Clamp((round - 1) / 20, 0, 4);
            int shells = Mathf.Clamp(2 + band / 2, 2, MaxShells);
            float t = (round - 1f) / 99f;
            float threshold = Mathf.Lerp(0.80f, 0.68f, t);
            float cooldown = Mathf.Lerp(MaxCooldownSeconds, MinCooldownSeconds, t);
            float acquisitionScale = Mathf.Lerp(0.92f, 1.16f, t);
            int signature = unchecked(140 * 1009 + round * 97 + shells * 31 + band * 17 + Mathf.RoundToInt(threshold * 1000f));
            return new CounterBatteryProfileV140(round, shells, threshold, cooldown, acquisitionScale, signature);
        }

        public static float DecayExposure(float current, float deltaSeconds)
        {
            return Mathf.Clamp01(current - ExposureDecayPerSecond * Mathf.Max(0f, deltaSeconds));
        }

        public static float AddSupportSignature(float current, float relocationDistance)
        {
            float multiplier = relocationDistance >= BreakDistance ? RelocatedUseMultiplier : RepeatUseMultiplier;
            return Mathf.Clamp01(current + ExposurePerSupportStrike * multiplier);
        }

        public static float ObserverWeight(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Sniper: return 1.25f;
                case EnemyKind.Siege: return 1.10f;
                case EnemyKind.Elite: return 1.00f;
                case EnemyKind.Supply: return 0.90f;
                default: return 0f;
            }
        }

        public static float AcquisitionGainPerSecond(
            float exposure,
            float observerStrength,
            CounterBatteryProfileV140 profile)
        {
            if (exposure < SearchThreshold || observerStrength <= 0f) return 0f;
            float exposure01 = Mathf.InverseLerp(SearchThreshold, 1f, Mathf.Clamp01(exposure));
            float observers = Mathf.Clamp01(observerStrength);
            return Mathf.Lerp(0.07f, 0.22f, exposure01) * Mathf.Lerp(0.55f, 1f, observers) * profile.AcquisitionScale;
        }

        public static Vector2 AimOffset(int strikeOrdinal, int roundSignature)
        {
            int h = unchecked(roundSignature * 37 + strikeOrdinal * 101 + 140 * 53);
            float x = ((h & 7) - 3) * 0.16f;
            float y = (((h >> 3) & 7) - 3) * 0.10f;
            return new Vector2(x, y);
        }

        public static CounterBatteryStrikeIntentV140 BuildIntent(
            Vector2 lockedPosition,
            int strikeOrdinal,
            CounterBatteryProfileV140 profile)
        {
            Vector2 aim = lockedPosition + AimOffset(strikeOrdinal, profile.Signature);
            Vector2 origin = aim + new Vector2((strikeOrdinal & 1) == 0 ? -0.65f : 0.65f, 4.8f);
            Vector2 direction = (aim - origin).normalized;
            return new CounterBatteryStrikeIntentV140(
                origin,
                aim,
                direction,
                1,
                10.8f,
                AmmoType.Explosive,
                strikeOrdinal,
                profile.Signature);
        }
    }

    /// <summary>
    /// Enemy counter-battery command state. v13.9 support intent creates exposure; eligible observers
    /// build a finite lock. This director publishes immutable barrage intent only and never creates
    /// projectiles, damages actors, moves rigidbodies or owns spawning.
    /// </summary>
    [DefaultExecutionOrder(-8840)]
    public sealed class CounterBatteryDirectorV140 : MonoBehaviour
    {
        public static CounterBatteryDirectorV140 Instance { get; private set; }
        public static event Action<CounterBatteryStrikeIntentV140> StrikeIntentPublished;

        private TankGame _game;
        private CounterBatteryStateV140 _state = CounterBatteryStateV140.Quiet;
        private CounterBatteryProfileV140 _profile;
        private float _exposure;
        private float _acquisition;
        private float _stateUntil;
        private float _nextSample;
        private float _nextShell;
        private int _round = -1;
        private int _shellsUsed;
        private int _observerCount;
        private float _observerStrength;
        private int _signaturesObserved;
        private int _barragesStarted;
        private int _locksBroken;
        private bool _hasSignature;
        private Vector2 _lastSignaturePosition;
        private Vector2 _lockedPosition;

        public CounterBatteryStateV140 State => _state;
        public float Exposure => _exposure;
        public float Acquisition => _acquisition;
        public int ObserverCount => _observerCount;
        public float ObserverStrength => _observerStrength;
        public int SignaturesObserved => _signaturesObserved;
        public int BarragesStarted => _barragesStarted;
        public int LocksBroken => _locksBroken;
        public int ShellsUsed => _shellsUsed;
        public CounterBatteryProfileV140 CurrentProfile => _profile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static CounterBatteryDirectorV140 EnsureInstalled()
        {
            if (Instance != null) return Instance;
            CounterBatteryDirectorV140 existing = FindAnyObjectByType<CounterBatteryDirectorV140>();
            if (existing != null) { Instance = existing; return existing; }
            GameObject go = new GameObject("CounterBatteryDirector_v14_0");
            DontDestroyOnLoad(go);
            return go.AddComponent<CounterBatteryDirectorV140>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _game = FindAnyObjectByType<TankGame>();
            BattlefieldFireSupportDirector.EnsureInstalled();
            CounterBatteryExecutionBridgeV140.EnsureInstalled();
            BattlefieldFireSupportDirector.StrikeIntentPublished += OnPlayerSupportIntent;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _nextSample = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            BattlefieldFireSupportDirector.StrikeIntentPublished -= OnPlayerSupportIntent;
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
            _state = CounterBatteryStateV140.Quiet;
            _profile = default;
            _exposure = 0f;
            _acquisition = 0f;
            _stateUntil = 0f;
            _nextShell = 0f;
            _round = -1;
            _shellsUsed = 0;
            _observerCount = 0;
            _observerStrength = 0f;
            _hasSignature = false;
        }

        private void OnPlayerSupportIntent(FireSupportStrikeIntentV139 _)
        {
            PlayerTank player = RuntimeBattleRegistry.Player;
            if (_game == null || !_game.IsPlaying || player == null) return;

            Vector2 position = player.transform.position;
            float relocation = _hasSignature
                ? Vector2.Distance(position, _lastSignaturePosition)
                : CounterBatteryModelV140.BreakDistance * 2f;
            _exposure = CounterBatteryModelV140.AddSupportSignature(_exposure, relocation);
            _lastSignaturePosition = position;
            _hasSignature = true;
            _signaturesObserved++;

            if (_state == CounterBatteryStateV140.Quiet && _exposure >= CounterBatteryModelV140.SearchThreshold)
                BeginSearching();
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureRound(_game.CurrentRound);

            float now = Time.unscaledTime;
            float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
            _exposure = CounterBatteryModelV140.DecayExposure(_exposure, dt);

            if (now >= _nextSample)
            {
                _nextSample = now + CounterBatteryModelV140.SampleCadenceSeconds;
                SampleObservers();
            }

            PlayerTank player = RuntimeBattleRegistry.Player;
            if (player == null) return;
            Vector2 playerPosition = player.transform.position;

            switch (_state)
            {
                case CounterBatteryStateV140.Quiet:
                    _acquisition = Mathf.Max(0f, _acquisition - dt * 0.10f);
                    if (_exposure >= CounterBatteryModelV140.SearchThreshold && _observerStrength > 0f)
                        BeginSearching();
                    break;

                case CounterBatteryStateV140.Searching:
                    if (_hasSignature && Vector2.Distance(playerPosition, _lastSignaturePosition) >= CounterBatteryModelV140.BreakDistance)
                    {
                        EnterRelocating(now, true);
                        break;
                    }
                    if (_observerStrength <= 0f || _exposure < CounterBatteryModelV140.SearchThreshold * 0.60f)
                    {
                        _state = CounterBatteryStateV140.Quiet;
                        _acquisition = Mathf.Max(0f, _acquisition - 0.15f);
                        break;
                    }
                    _acquisition = Mathf.Clamp01(_acquisition + CounterBatteryModelV140.AcquisitionGainPerSecond(
                        _exposure, _observerStrength, _profile) * dt);
                    if (_acquisition >= _profile.LockThreshold)
                    {
                        _state = CounterBatteryStateV140.Locked;
                        _lockedPosition = playerPosition;
                        _stateUntil = now + CounterBatteryModelV140.LockWarningSeconds;
                    }
                    break;

                case CounterBatteryStateV140.Locked:
                    if (Vector2.Distance(playerPosition, _lockedPosition) >= CounterBatteryModelV140.BreakDistance)
                    {
                        EnterRelocating(now, true);
                        break;
                    }
                    if (now >= _stateUntil)
                        BeginBarrage(now);
                    break;

                case CounterBatteryStateV140.Barrage:
                    if (Vector2.Distance(playerPosition, _lockedPosition) >= CounterBatteryModelV140.BreakDistance * 1.25f)
                    {
                        EnterRelocating(now, true);
                        break;
                    }
                    if (_shellsUsed >= _profile.ShellBudget)
                    {
                        EnterRelocating(now, false);
                        break;
                    }
                    if (now >= _nextShell)
                    {
                        PublishStrikeIntent(_shellsUsed);
                        _shellsUsed++;
                        _nextShell = now + CounterBatteryModelV140.BarrageCadenceSeconds;
                    }
                    break;

                case CounterBatteryStateV140.Relocating:
                    if (now >= _stateUntil)
                    {
                        _state = CounterBatteryStateV140.Quiet;
                        _acquisition = 0f;
                    }
                    break;
            }
        }

        private void EnsureRound(int round)
        {
            int bounded = Mathf.Clamp(round, 1, CounterBatteryModelV140.PlannedRounds);
            if (_round == bounded) return;
            _round = bounded;
            _profile = CounterBatteryModelV140.ProfileForRound(bounded);
            _state = CounterBatteryStateV140.Quiet;
            _exposure = 0f;
            _acquisition = 0f;
            _shellsUsed = 0;
            _hasSignature = false;
        }

        private void BeginSearching()
        {
            _state = CounterBatteryStateV140.Searching;
            _acquisition = Mathf.Max(_acquisition, 0.05f);
        }

        private void BeginBarrage(float now)
        {
            _state = CounterBatteryStateV140.Barrage;
            _shellsUsed = 0;
            _nextShell = now + 0.24f;
            _barragesStarted++;
        }

        private void EnterRelocating(float now, bool playerBreak)
        {
            _state = CounterBatteryStateV140.Relocating;
            _stateUntil = now + (playerBreak ? Mathf.Min(7f, _profile.CooldownSeconds * 0.35f) : _profile.CooldownSeconds);
            _acquisition = 0f;
            _exposure *= playerBreak ? 0.25f : 0.45f;
            if (playerBreak) _locksBroken++;
        }

        private void SampleObservers()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int count = Mathf.Min(enemies.Length, CounterBatteryModelV140.MaxTrackedHostiles);
            int eligible = 0;
            float weight = 0f;

            for (int i = 0; i < count; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                float observer = CounterBatteryModelV140.ObserverWeight(enemy.Kind);
                if (observer <= 0f) continue;
                eligible++;
                float cohesionScale = Mathf.Clamp(BattlefieldCohesionDirector.SpreadScale(enemy), 1f, 1.5f);
                weight += observer / cohesionScale;
            }

            _observerCount = Mathf.Min(eligible, CounterBatteryModelV140.MaxObservers);
            float maxWeight = CounterBatteryModelV140.MaxObservers * 1.25f;
            _observerStrength = _observerCount > 0 ? Mathf.Clamp01(weight / maxWeight) : 0f;
        }

        private void PublishStrikeIntent(int ordinal)
        {
            CounterBatteryStrikeIntentV140 intent = CounterBatteryModelV140.BuildIntent(_lockedPosition, ordinal, _profile);
            StrikeIntentPublished?.Invoke(intent);
        }
    }

    /// <summary>
    /// Thin canonical execution bridge. The counter-battery director chooses no damage path itself;
    /// this adapter forwards immutable enemy barrage intent through TankGame's existing Projectile path.
    /// </summary>
    [DefaultExecutionOrder(-8830)]
    public sealed class CounterBatteryExecutionBridgeV140 : MonoBehaviour
    {
        public static CounterBatteryExecutionBridgeV140 Instance { get; private set; }

        private TankGame _game;
        private int _executedIntents;

        public int ExecutedIntents => _executedIntents;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static CounterBatteryExecutionBridgeV140 EnsureInstalled()
        {
            if (Instance != null) return Instance;
            CounterBatteryExecutionBridgeV140 existing = FindAnyObjectByType<CounterBatteryExecutionBridgeV140>();
            if (existing != null) { Instance = existing; return existing; }
            GameObject go = new GameObject("CounterBatteryExecutionBridge_v14_0");
            DontDestroyOnLoad(go);
            return go.AddComponent<CounterBatteryExecutionBridgeV140>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _game = FindAnyObjectByType<TankGame>();
            CounterBatteryDirectorV140.StrikeIntentPublished += ExecuteIntent;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            CounterBatteryDirectorV140.StrikeIntentPublished -= ExecuteIntent;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            _game = FindAnyObjectByType<TankGame>();
        }

        private void ExecuteIntent(CounterBatteryStrikeIntentV140 intent)
        {
            if (_game == null || !_game.IsPlaying) return;
            VisualFactory.RingPulse(intent.Aim, new Color(1f, 0.34f, 0.12f), 0.88f);
            _game.SpawnProjectile(
                intent.Origin,
                intent.Direction,
                Team.Enemy,
                intent.Damage,
                intent.Speed,
                AmmoDatabase.Color(intent.Ammo),
                intent.Ammo);
            _executedIntents++;
        }
    }
}
