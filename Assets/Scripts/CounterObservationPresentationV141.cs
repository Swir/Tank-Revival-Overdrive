using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v14.1 presentation-only bridge for the counter-observation hunter-killer loop.
    /// It reads the director snapshot and renders a fixed world-space cue pool. It never
    /// moves combat actors, changes Health, owns projectiles or mutates mission state.
    /// </summary>
    [DefaultExecutionOrder(-8790)]
    public sealed class CounterObservationPresentationV141 : MonoBehaviour
    {
        public const int MaxPresentationCues = 2;
        public const float RefreshCadenceSeconds = 0.25f;
        public const float CueHoldSeconds = 0.55f;
        public const float DesignatedPulseHz = 2.4f;
        public const float NetworkBrokenPulseHz = 4.2f;

        private readonly ObserverCueSlot[] _slots = new ObserverCueSlot[MaxPresentationCues];
        private CounterObservationDirectorV141 _director;
        private float _nextRefresh;
        private int _visibleCueCount;

        public static CounterObservationPresentationV141 Instance { get; private set; }
        public int VisibleCueCount => _visibleCueCount;

        public static bool ConfigurationValid =>
            MaxPresentationCues == 2 &&
            MaxPresentationCues <= CounterObservationModelV141.MaxObserverCandidates &&
            RefreshCadenceSeconds >= 0.20f && RefreshCadenceSeconds <= 0.50f &&
            CueHoldSeconds > RefreshCadenceSeconds && CueHoldSeconds <= 0.80f &&
            DesignatedPulseHz > 0f && NetworkBrokenPulseHz > DesignatedPulseHz;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static CounterObservationPresentationV141 EnsureInstalled()
        {
            if (Instance != null) return Instance;
            CounterObservationPresentationV141 existing = FindAnyObjectByType<CounterObservationPresentationV141>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject go = new GameObject("CounterObservationPresentation_v14_1");
            DontDestroyOnLoad(go);
            return go.AddComponent<CounterObservationPresentationV141>();
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
            _director = CounterObservationDirectorV141.EnsureInstalled();
            BuildPool();
            _nextRefresh = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh)
            {
                TickVisibleCues();
                return;
            }

            _nextRefresh = Time.unscaledTime + RefreshCadenceSeconds;
            if (_director == null) _director = CounterObservationDirectorV141.Instance;
            RefreshFromHunterKillerState();
            TickVisibleCues();
        }

        private void BuildPool()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = new ObserverCueSlot(transform, i);
            HideAll();
        }

        private void RefreshFromHunterKillerState()
        {
            _visibleCueCount = 0;
            if (_director == null || !ConfigurationValid)
            {
                HideAll();
                return;
            }

            CounterObservationStateV141 state = _director.State;
            CounterObservationTargetSnapshotV141 snapshot = _director.TargetSnapshot;
            bool designated = state == CounterObservationStateV141.Designated && snapshot.Valid;
            bool broken = state == CounterObservationStateV141.NetworkBroken && snapshot.Valid && snapshot.ConfirmedKill;
            if (!designated && !broken)
            {
                HideAll();
                return;
            }

            bool highPriority = broken || snapshot.Confidence >= 0.75f;
            if (MassBattleFxBudget.TryConsumeTacticalCue(highPriority))
            {
                _slots[0].Show(snapshot.Position, broken ? ObserverCueMode.NetworkBroken : ObserverCueMode.Designated,
                    snapshot.Confidence, snapshot.Resilience01, CueHoldSeconds);
                _visibleCueCount++;
            }
            else
            {
                _slots[0].Hide();
            }

            // A second, wider threat ring is intentionally separate and budgeted. This keeps
            // world emphasis readable while preserving the strict one-target gameplay authority.
            if (designated && MassBattleFxBudget.TryConsumeTacticalCue(false))
            {
                _slots[1].Show(snapshot.Position, ObserverCueMode.ThreatPerimeter,
                    snapshot.Confidence, snapshot.Resilience01, CueHoldSeconds);
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

        private enum ObserverCueMode
        {
            Designated,
            ThreatPerimeter,
            NetworkBroken
        }

        private sealed class ObserverCueSlot
        {
            private readonly Transform _root;
            private readonly GameObject _ring;
            private readonly GameObject _north;
            private readonly GameObject _south;
            private readonly GameObject _east;
            private readonly GameObject _west;
            private readonly Vector3 _baseRingScale;
            private readonly int _slotIndex;
            private float _visibleUntil;
            private float _phase;
            private ObserverCueMode _mode;

            public ObserverCueSlot(Transform parent, int slotIndex)
            {
                _slotIndex = slotIndex;
                GameObject root = new GameObject("ObserverHuntCue3D_" + slotIndex);
                root.transform.SetParent(parent, false);
                _root = root.transform;

                Color lockColor = new Color(1f, 0.28f, 0.08f, 0.86f);
                _ring = Runtime3DFactory.Cylinder("ObserverThreatRing3D", _root, Vector3.zero,
                    slotIndex == 0 ? 1.70f : 2.35f, 0.028f, lockColor, 0.01f, 0.94f);
                _baseRingScale = _ring.transform.localScale;
                _north = Runtime3DFactory.Box("ObserverCueNorth3D", _root, new Vector3(0f, 1.05f, 0f),
                    new Vector3(0.13f, 0.38f, 0.04f), lockColor, 0.01f, 0.95f);
                _south = Runtime3DFactory.Box("ObserverCueSouth3D", _root, new Vector3(0f, -1.05f, 0f),
                    new Vector3(0.13f, 0.38f, 0.04f), lockColor, 0.01f, 0.95f);
                _east = Runtime3DFactory.Box("ObserverCueEast3D", _root, new Vector3(1.05f, 0f, 0f),
                    new Vector3(0.38f, 0.13f, 0.04f), lockColor, 0.01f, 0.95f);
                _west = Runtime3DFactory.Box("ObserverCueWest3D", _root, new Vector3(-1.05f, 0f, 0f),
                    new Vector3(0.38f, 0.13f, 0.04f), lockColor, 0.01f, 0.95f);
                Hide();
            }

            public void Show(Vector2 worldPosition, ObserverCueMode mode, float confidence, float resilience01, float holdSeconds)
            {
                _mode = mode;
                _visibleUntil = Time.unscaledTime + holdSeconds;
                _root.position = new Vector3(worldPosition.x, worldPosition.y, 0.38f + _slotIndex * 0.02f);

                float confidenceScale = Mathf.Lerp(0.92f, 1.10f, Mathf.Clamp01(confidence));
                float resilienceScale = Mathf.Lerp(1.06f, 0.94f, Mathf.Clamp01(resilience01));
                float modeScale = mode == ObserverCueMode.ThreatPerimeter ? 1.08f : 1f;
                _root.localScale = Vector3.one * confidenceScale * resilienceScale * modeScale;

                bool crosshair = mode != ObserverCueMode.ThreatPerimeter;
                _north.SetActive(crosshair);
                _south.SetActive(crosshair);
                _east.SetActive(crosshair);
                _west.SetActive(crosshair);
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

                float hz = _mode == ObserverCueMode.NetworkBroken ? NetworkBrokenPulseHz : DesignatedPulseHz;
                _phase += Time.unscaledDeltaTime * hz * Mathf.PI * 2f;
                float amplitude = _mode == ObserverCueMode.ThreatPerimeter ? 0.025f : 0.055f;
                float pulse = 1f + Mathf.Sin(_phase) * amplitude;
                _ring.transform.localScale = new Vector3(
                    _baseRingScale.x * pulse,
                    _baseRingScale.y * pulse,
                    _baseRingScale.z);

                if (_mode == ObserverCueMode.NetworkBroken)
                    _root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_phase * 0.5f) * 5f);
                else
                    _root.localRotation = Quaternion.identity;
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
