using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v14.2 presentation-only bridge for deception, EMCON and hunter-killer adaptation.
    /// Uses a fixed cue pool plus immediate-mode HUD readout; it never mutates combat state,
    /// movement, Health, projectiles, observation authority or counter-battery authority.
    /// </summary>
    [DefaultExecutionOrder(-8780)]
    public sealed class CounterReconPresentationV142 : MonoBehaviour
    {
        public const int MaxPresentationCues = 2;
        public const float RefreshCadenceSeconds = 0.25f;
        public const float CueHoldSeconds = 0.55f;
        public const float DecoyPulseHz = 2.0f;
        public const float EmconPulseHz = 3.0f;
        public const float ReacquirePulseHz = 4.0f;

        private readonly ReconCueSlot[] _slots = new ReconCueSlot[MaxPresentationCues];
        private CounterReconDeceptionDirectorV142 _director;
        private float _nextRefresh;
        private int _visibleCueCount;

        public static CounterReconPresentationV142 Instance { get; private set; }
        public int VisibleCueCount => _visibleCueCount;

        public static bool ConfigurationValid =>
            MaxPresentationCues == 2 &&
            RefreshCadenceSeconds >= 0.20f && RefreshCadenceSeconds <= 0.50f &&
            CueHoldSeconds > RefreshCadenceSeconds && CueHoldSeconds <= 0.80f &&
            DecoyPulseHz > 0f && EmconPulseHz > DecoyPulseHz && ReacquirePulseHz > EmconPulseHz;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static CounterReconPresentationV142 EnsureInstalled()
        {
            if (Instance != null) return Instance;
            CounterReconPresentationV142 existing = FindAnyObjectByType<CounterReconPresentationV142>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject go = new GameObject("CounterReconPresentation_v14_2");
            DontDestroyOnLoad(go);
            return go.AddComponent<CounterReconPresentationV142>();
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
            _director = CounterReconDeceptionDirectorV142.EnsureInstalled();
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
                if (_director == null) _director = CounterReconDeceptionDirectorV142.Instance;
                RefreshPresentation();
            }

            TickVisibleCues();
        }

        private void BuildPool()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = new ReconCueSlot(transform, i);
            HideAll();
        }

        private void RefreshPresentation()
        {
            _visibleCueCount = 0;
            if (_director == null || !ConfigurationValid)
            {
                HideAll();
                return;
            }

            CounterReconStateV142 state = _director.State;
            PlayerTank player = RuntimeBattleRegistry.Player;
            Vector2 playerPosition = player != null ? (Vector2)player.transform.position : Vector2.zero;

            if (state == CounterReconStateV142.DecoyActive && _director.DecoyActive && MassBattleFxBudget.TryConsumeTacticalCue(false))
            {
                _slots[0].Show(_director.DecoyPosition, ReconCueMode.Decoy,
                    _director.DecoyCredibility01, CueHoldSeconds);
                _visibleCueCount++;
            }
            else if (state == CounterReconStateV142.EmconRelocating && _director.EmconActive && player != null && MassBattleFxBudget.TryConsumeTacticalCue(true))
            {
                _slots[0].Show(playerPosition, ReconCueMode.Emcon,
                    1f - _director.ObserverResilience01 * 0.35f, CueHoldSeconds);
                _visibleCueCount++;
            }
            else if (state == CounterReconStateV142.Reacquiring && _director.ReacquisitionBlocked && player != null && MassBattleFxBudget.TryConsumeTacticalCue(true))
            {
                _slots[0].Show(playerPosition, ReconCueMode.Reacquiring,
                    1f - _director.ObserverResilience01 * 0.25f, CueHoldSeconds);
                _visibleCueCount++;
            }
            else
            {
                _slots[0].Hide();
            }

            bool adaptationReadable = state != CounterReconStateV142.Ready && _director.Suspicion01 > 0.01f;
            if (adaptationReadable && player != null && MassBattleFxBudget.TryConsumeTacticalCue(false))
            {
                _slots[1].Show(playerPosition, ReconCueMode.Adaptation,
                    Mathf.Clamp01(_director.Suspicion01 / CounterReconDeceptionModelV142.MaxSuspicion01), CueHoldSeconds);
                _visibleCueCount++;
            }
            else
            {
                _slots[1].Hide();
            }
        }

        private void TickVisibleCues()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i]?.Tick();
        }

        private void HideAll()
        {
            _visibleCueCount = 0;
            for (int i = 0; i < _slots.Length; i++)
                _slots[i]?.Hide();
        }

        private void OnGUI()
        {
            if (_director == null || _director.State == CounterReconStateV142.Ready) return;

            const float width = 278f;
            const float height = 58f;
            Rect panel = new Rect(Screen.width - width - 18f, 18f, width, height);
            GUI.Box(panel, GUIContent.none);

            CounterReconStateV142 currentState = _director.State;
            string state = currentState == CounterReconStateV142.DecoyActive ? "DECOY ACTIVE" :
                currentState == CounterReconStateV142.EmconRelocating ? "EMCON / RELOCATE" :
                currentState == CounterReconStateV142.Reacquiring ? "REACQUIRING" : "READY";
            GUI.Label(new Rect(panel.x + 10f, panel.y + 7f, width - 20f, 20f), "COUNTER-RECON: " + state);

            int suspicionPct = Mathf.RoundToInt(Mathf.Clamp01(_director.Suspicion01 / CounterReconDeceptionModelV142.MaxSuspicion01) * 100f);
            int credibilityPct = Mathf.RoundToInt(_director.DecoyCredibility01 * 100f);
            GUI.Label(new Rect(panel.x + 10f, panel.y + 30f, width - 20f, 20f),
                "Adapt " + suspicionPct + "%  |  Decoy " + credibilityPct + "%  |  G:" + _director.DecoyCharges + " V:EMCON");
        }

        private enum ReconCueMode
        {
            Decoy,
            Emcon,
            Reacquiring,
            Adaptation
        }

        private sealed class ReconCueSlot
        {
            private readonly Transform _root;
            private readonly GameObject _ring;
            private readonly GameObject _north;
            private readonly GameObject _south;
            private readonly Vector3 _baseRingScale;
            private readonly int _slotIndex;
            private float _visibleUntil;
            private float _phase;
            private ReconCueMode _mode;

            public ReconCueSlot(Transform parent, int slotIndex)
            {
                _slotIndex = slotIndex;
                GameObject root = new GameObject("CounterReconCue3D_" + slotIndex);
                root.transform.SetParent(parent, false);
                _root = root.transform;

                Color cueColor = slotIndex == 0
                    ? new Color(0.20f, 0.78f, 1f, 0.84f)
                    : new Color(1f, 0.62f, 0.10f, 0.80f);
                _ring = Runtime3DFactory.Cylinder("CounterReconRing3D", _root, Vector3.zero,
                    slotIndex == 0 ? 1.55f : 2.05f, 0.025f, cueColor, 0.01f, 0.94f);
                _baseRingScale = _ring.transform.localScale;
                _north = Runtime3DFactory.Box("CounterReconNorth3D", _root, new Vector3(0f, 0.92f, 0f),
                    new Vector3(0.10f, 0.30f, 0.04f), cueColor, 0.01f, 0.95f);
                _south = Runtime3DFactory.Box("CounterReconSouth3D", _root, new Vector3(0f, -0.92f, 0f),
                    new Vector3(0.10f, 0.30f, 0.04f), cueColor, 0.01f, 0.95f);
                Hide();
            }

            public void Show(Vector2 worldPosition, ReconCueMode mode, float intensity01, float holdSeconds)
            {
                _mode = mode;
                _visibleUntil = Time.unscaledTime + holdSeconds;
                _root.position = new Vector3(worldPosition.x, worldPosition.y, 0.40f + _slotIndex * 0.02f);
                _root.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.08f, Mathf.Clamp01(intensity01));

                bool directional = mode == ReconCueMode.Emcon || mode == ReconCueMode.Reacquiring;
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

                float hz = _mode == ReconCueMode.Decoy ? DecoyPulseHz :
                    _mode == ReconCueMode.Emcon ? EmconPulseHz : ReacquirePulseHz;
                _phase += Time.unscaledDeltaTime * hz * Mathf.PI * 2f;
                float amplitude = _mode == ReconCueMode.Adaptation ? 0.025f : 0.05f;
                float pulse = 1f + Mathf.Sin(_phase) * amplitude;
                _ring.transform.localScale = new Vector3(
                    _baseRingScale.x * pulse,
                    _baseRingScale.y * pulse,
                    _baseRingScale.z);
                _root.localRotation = _mode == ReconCueMode.Reacquiring
                    ? Quaternion.Euler(0f, 0f, Mathf.Sin(_phase * 0.5f) * 6f)
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
