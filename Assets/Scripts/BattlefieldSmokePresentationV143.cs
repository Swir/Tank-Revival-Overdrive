using UnityEngine;

namespace TankRevival
{
    public enum SmokePresentationPhaseV143
    {
        Hidden = 0,
        Screening = 1,
        Dispersing = 2,
        BreakContact = 3,
        Cooldown = 4
    }

    /// <summary>
    /// Fixed-cap v14.3 world-space presentation for smoke screening and break-contact warfare.
    /// Presentation only: no Health, Projectile, movement, spawn, targeting or economy authority.
    /// No normal-gameplay OnGUI/debug wall is created.
    /// </summary>
    [DefaultExecutionOrder(-8770)]
    public sealed class BattlefieldSmokePresentationV143 : MonoBehaviour
    {
        public const int MaxPresentationCues = 2;
        public const float RefreshCadenceSeconds = 0.25f;
        public const float CueHoldSeconds = 0.55f;
        public const float ScreenPulseHz = 1.9f;
        public const float DispersePulseHz = 1.25f;
        public const float BreakContactPulseHz = 3.1f;
        public const float CooldownPulseHz = 0.85f;

        private readonly SmokeCueSlot[] _slots = new SmokeCueSlot[MaxPresentationCues];
        private BattlefieldSmokeScreenDirectorV143 _director;
        private float _nextRefresh;
        private int _visibleCueCount;
        private SmokePresentationPhaseV143 _phase;

        public static BattlefieldSmokePresentationV143 Instance { get; private set; }
        public int VisibleCueCount => _visibleCueCount;
        public SmokePresentationPhaseV143 Phase => _phase;
        public string PhaseLabel =>
            _phase == SmokePresentationPhaseV143.Screening ? "SCREENING" :
            _phase == SmokePresentationPhaseV143.Dispersing ? "DISPERSE" :
            _phase == SmokePresentationPhaseV143.BreakContact ? "BREAK CONTACT" :
            _phase == SmokePresentationPhaseV143.Cooldown ? "COOLDOWN" : string.Empty;

        public static bool ConfigurationValid =>
            MaxPresentationCues == 2 &&
            RefreshCadenceSeconds >= 0.20f && RefreshCadenceSeconds <= 0.50f &&
            CueHoldSeconds > RefreshCadenceSeconds && CueHoldSeconds <= 0.80f &&
            ScreenPulseHz > DispersePulseHz &&
            BreakContactPulseHz > ScreenPulseHz &&
            CooldownPulseHz > 0f && CooldownPulseHz < DispersePulseHz;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static BattlefieldSmokePresentationV143 EnsureInstalled()
        {
            if (Instance != null) return Instance;
            BattlefieldSmokePresentationV143 existing = FindAnyObjectByType<BattlefieldSmokePresentationV143>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject go = new GameObject("BattlefieldSmokePresentation_v14_3");
            DontDestroyOnLoad(go);
            return go.AddComponent<BattlefieldSmokePresentationV143>();
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
            _director = BattlefieldSmokeScreenDirectorV143.EnsureInstalled();
            BuildPool();
            _nextRefresh = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshCadenceSeconds;
                if (_director == null) _director = BattlefieldSmokeScreenDirectorV143.Instance;
                RefreshPresentation();
            }

            for (int i = 0; i < _slots.Length; i++)
                _slots[i]?.Tick();
        }

        private void BuildPool()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = new SmokeCueSlot(transform, i);
            HideAll();
        }

        private void RefreshPresentation()
        {
            _visibleCueCount = 0;
            if (_director == null || !ConfigurationValid)
            {
                _phase = SmokePresentationPhaseV143.Hidden;
                HideAll();
                return;
            }

            PlayerTank player = RuntimeBattleRegistry.Player;
            Vector2 playerPosition = player != null ? (Vector2)player.transform.position : _director.Origin;
            SmokeScreenStateV143 state = _director.State;

            if (state == SmokeScreenStateV143.Screening)
            {
                bool breakContact =
                    _director.PlayerInsideZone &&
                    BattlefieldSmokeScreenDirectorV143.BreakContactDistanceScale <= 0.84f;
                _phase = breakContact
                    ? SmokePresentationPhaseV143.BreakContact
                    : SmokePresentationPhaseV143.Screening;

                if (MassBattleFxBudget.TryConsumeTacticalCue(true))
                {
                    _slots[0].Show(
                        _director.Origin,
                        breakContact ? SmokeCueModeV143.BreakContact : SmokeCueModeV143.Screening,
                        Mathf.Clamp01(_director.ContextStrength01),
                        CueHoldSeconds,
                        _director.RadiusWorld);
                    _visibleCueCount++;
                }
                else
                {
                    _slots[0].Hide();
                }

                if (breakContact && player != null && MassBattleFxBudget.TryConsumeTacticalCue(false))
                {
                    _slots[1].Show(
                        playerPosition,
                        SmokeCueModeV143.PlayerExit,
                        1f - BattlefieldSmokeScreenDirectorV143.BreakContactDistanceScale,
                        CueHoldSeconds,
                        1.15f);
                    _visibleCueCount++;
                }
                else
                {
                    _slots[1].Hide();
                }
                return;
            }

            if (state == SmokeScreenStateV143.Dissipating)
            {
                _phase = SmokePresentationPhaseV143.Dispersing;
                if (MassBattleFxBudget.TryConsumeTacticalCue(false))
                {
                    _slots[0].Show(
                        _director.Origin,
                        SmokeCueModeV143.Dispersing,
                        Mathf.Clamp01(_director.ContextStrength01 * 0.65f),
                        CueHoldSeconds,
                        _director.RadiusWorld);
                    _visibleCueCount++;
                }
                else
                {
                    _slots[0].Hide();
                }
                _slots[1].Hide();
                return;
            }

            if (state == SmokeScreenStateV143.Cooldown && _director.Charges > 0 && player != null)
            {
                _phase = SmokePresentationPhaseV143.Cooldown;
                if (MassBattleFxBudget.TryConsumeTacticalCue(false))
                {
                    _slots[0].Show(
                        playerPosition,
                        SmokeCueModeV143.Cooldown,
                        0.45f,
                        CueHoldSeconds,
                        0.72f);
                    _visibleCueCount++;
                }
                else
                {
                    _slots[0].Hide();
                }
                _slots[1].Hide();
                return;
            }

            _phase = SmokePresentationPhaseV143.Hidden;
            HideAll();
        }

        private void HideAll()
        {
            _visibleCueCount = 0;
            for (int i = 0; i < _slots.Length; i++)
                _slots[i]?.Hide();
        }

        private enum SmokeCueModeV143
        {
            Screening,
            Dispersing,
            BreakContact,
            PlayerExit,
            Cooldown
        }

        private sealed class SmokeCueSlot
        {
            private readonly Transform _root;
            private readonly GameObject _ring;
            private readonly GameObject _north;
            private readonly GameObject _south;
            private readonly Vector3 _baseRingScale;
            private readonly int _slotIndex;
            private float _visibleUntil;
            private float _phase;
            private SmokeCueModeV143 _mode;

            public SmokeCueSlot(Transform parent, int slotIndex)
            {
                _slotIndex = slotIndex;
                GameObject root = new GameObject("SmokeCue3D_" + slotIndex);
                root.transform.SetParent(parent, false);
                _root = root.transform;

                Color cueColor = slotIndex == 0
                    ? new Color(0.56f, 0.72f, 0.78f, 0.66f)
                    : new Color(0.28f, 0.88f, 1f, 0.82f);

                _ring = Runtime3DFactory.Cylinder(
                    "SmokeCueRing3D",
                    _root,
                    Vector3.zero,
                    1.0f,
                    0.022f,
                    cueColor,
                    0.01f,
                    0.93f);
                _baseRingScale = _ring.transform.localScale;

                _north = Runtime3DFactory.Box(
                    "SmokeCueNorth3D",
                    _root,
                    new Vector3(0f, 0.68f, 0f),
                    new Vector3(0.08f, 0.28f, 0.04f),
                    cueColor,
                    0.01f,
                    0.95f);
                _south = Runtime3DFactory.Box(
                    "SmokeCueSouth3D",
                    _root,
                    new Vector3(0f, -0.68f, 0f),
                    new Vector3(0.08f, 0.28f, 0.04f),
                    cueColor,
                    0.01f,
                    0.95f);
                Hide();
            }

            public void Show(
                Vector2 worldPosition,
                SmokeCueModeV143 mode,
                float intensity01,
                float holdSeconds,
                float radiusScale)
            {
                _mode = mode;
                _visibleUntil = Time.unscaledTime + holdSeconds;
                _root.position = new Vector3(worldPosition.x, worldPosition.y, 0.38f + _slotIndex * 0.02f);
                float intensityScale = Mathf.Lerp(0.92f, 1.08f, Mathf.Clamp01(intensity01));
                float safeRadius = Mathf.Clamp(radiusScale, 0.65f, BattlefieldSmokeScreenModelV143.SmokeRadiusWorld);
                _root.localScale = Vector3.one * intensityScale * safeRadius;

                bool directional =
                    mode == SmokeCueModeV143.BreakContact ||
                    mode == SmokeCueModeV143.PlayerExit;
                _north.SetActive(directional);
                _south.SetActive(directional);
                _ring.SetActive(true);
                _root.gameObject.SetActive(true);
            }

            public void Tick()
            {
                if (!_root.gameObject.activeSelf) return;
                if (Time.unscaledTime > _visibleUntil)
                {
                    Hide();
                    return;
                }

                float hz =
                    _mode == SmokeCueModeV143.BreakContact || _mode == SmokeCueModeV143.PlayerExit
                        ? BreakContactPulseHz :
                    _mode == SmokeCueModeV143.Screening
                        ? ScreenPulseHz :
                    _mode == SmokeCueModeV143.Dispersing
                        ? DispersePulseHz : CooldownPulseHz;

                _phase += Time.unscaledDeltaTime * hz * Mathf.PI * 2f;
                float amplitude = _mode == SmokeCueModeV143.Cooldown ? 0.018f : 0.035f;
                float pulse = 1f + Mathf.Sin(_phase) * amplitude;
                _ring.transform.localScale = new Vector3(
                    _baseRingScale.x * pulse,
                    _baseRingScale.y * pulse,
                    _baseRingScale.z);

                _root.localRotation =
                    _mode == SmokeCueModeV143.BreakContact || _mode == SmokeCueModeV143.PlayerExit
                        ? Quaternion.Euler(0f, 0f, Mathf.Sin(_phase * 0.5f) * 5f)
                        : Quaternion.identity;
            }

            public void Hide()
            {
                _visibleUntil = -1f;
                _phase = 0f;
                if (_root != null)
                {
                    _root.localRotation = Quaternion.identity;
                    _root.gameObject.SetActive(false);
                }
            }
        }
    }
}
