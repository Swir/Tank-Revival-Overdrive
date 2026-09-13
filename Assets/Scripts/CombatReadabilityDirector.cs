using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum CombatHudDensity
    {
        Minimal = 0,
        Focus = 1,
        Standard = 2
    }

    [DefaultExecutionOrder(-10000)]
    public sealed class CombatReadabilityDirector : MonoBehaviour
    {
        public const int DensityModeCount = 3;
        public const int MinimalLineBudget = 1;
        public const int FocusLineBudget = 2;
        public const int StandardLineBudget = 3;
        public const int SuppressedLegacyDirectorCount = 6;
        public const float SnapshotRefreshSeconds = 0.20f;
        public const float AlertHoldSeconds = 3.25f;

        private sealed class TacticalLine
        {
            public string Label;
            public string Status;
            public int Priority;
            public bool Critical;
        }

        private readonly List<Behaviour> _legacyPanels = new List<Behaviour>(SuppressedLegacyDirectorCount);
        private readonly List<Behaviour> _disabledThisFrame = new List<Behaviour>(SuppressedLegacyDirectorCount);
        private readonly List<TacticalLine> _lines = new List<TacticalLine>(8);
        private readonly Dictionary<Type, FieldInfo> _statusFields = new Dictionary<Type, FieldInfo>();
        private readonly Dictionary<Type, FieldInfo> _roundFields = new Dictionary<Type, FieldInfo>();

        private TankGame _game;
        private CombatHudDensity _density = CombatHudDensity.Focus;
        private float _nextSnapshotAt;
        private float _nextDiscoveryAt;
        private bool _suppressionArmed;
        private string _criticalAlert = string.Empty;
        private float _criticalUntil;
        private GUIStyle _titleStyle;
        private GUIStyle _lineStyle;
        private GUIStyle _criticalStyle;
        private GUIStyle _hintStyle;

        public static CombatReadabilityDirector Instance { get; private set; }
        public static bool LegacyPanelSuppressionEnabled { get; private set; } = true;
        public static CombatHudDensity CurrentDensity => Instance != null ? Instance._density : CombatHudDensity.Focus;
        public static bool ConfigurationValid =>
            DensityModeCount == 3 &&
            MinimalLineBudget == 1 &&
            FocusLineBudget == 2 &&
            StandardLineBudget == 3 &&
            SuppressedLegacyDirectorCount == 6 &&
            SnapshotRefreshSeconds >= 0.10f && SnapshotRefreshSeconds <= 0.50f &&
            AlertHoldSeconds >= 2.0f && AlertHoldSeconds <= 5.0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CombatReadabilityDirector>() != null) return;
            var go = new GameObject("CombatReadabilityDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<CombatReadabilityDirector>();
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
            DiscoverLegacyPanels();
        }

        private void OnDestroy()
        {
            RestoreSuppressedPanels();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();

            if (Input.GetKeyDown(KeyCode.F1))
            {
                _density = (CombatHudDensity)(((int)_density + 1) % DensityModeCount);
                SetCriticalAlert("HUD " + _density.ToString().ToUpperInvariant(), 1.5f);
            }

            if (Time.unscaledTime >= _nextDiscoveryAt)
            {
                _nextDiscoveryAt = Time.unscaledTime + 2.0f;
                DiscoverLegacyPanels();
            }

            if (Time.unscaledTime >= _nextSnapshotAt)
            {
                _nextSnapshotAt = Time.unscaledTime + SnapshotRefreshSeconds;
                RefreshSnapshot();
            }
        }

        private void LateUpdate()
        {
            if (!LegacyPanelSuppressionEnabled || _game == null || !_game.IsPlaying || _suppressionArmed) return;

            _disabledThisFrame.Clear();
            for (int i = 0; i < _legacyPanels.Count; i++)
            {
                Behaviour panel = _legacyPanels[i];
                if (panel == null || !panel.enabled) continue;
                panel.enabled = false;
                _disabledThisFrame.Add(panel);
            }

            if (_disabledThisFrame.Count > 0)
            {
                _suppressionArmed = true;
                StartCoroutine(RestoreAfterRendering());
            }
        }

        private IEnumerator RestoreAfterRendering()
        {
            yield return new WaitForEndOfFrame();
            RestoreSuppressedPanels();
        }

        private void RestoreSuppressedPanels()
        {
            for (int i = 0; i < _disabledThisFrame.Count; i++)
            {
                Behaviour panel = _disabledThisFrame[i];
                if (panel != null) panel.enabled = true;
            }
            _disabledThisFrame.Clear();
            _suppressionArmed = false;
        }

        private void DiscoverLegacyPanels()
        {
            _legacyPanels.Clear();
            AddLegacy(FindAnyObjectByType<OrzelekFortressDirector>());
            AddLegacy(FindAnyObjectByType<EnemyCommandNetworkDirector>());
            AddLegacy(FindAnyObjectByType<DynamicBattlefieldDirector>());
            AddLegacy(FindAnyObjectByType<MultiStageOperationDirector>());
            AddLegacy(FindAnyObjectByType<ConvoyWarfareDirector>());
            AddLegacy(FindAnyObjectByType<CombinedArmsDirector>());
        }

        private void AddLegacy(Behaviour behaviour)
        {
            if (behaviour != null && behaviour != this) _legacyPanels.Add(behaviour);
        }

        private void RefreshSnapshot()
        {
            _lines.Clear();
            if (_game == null || !_game.IsPlaying) return;

            Capture("FORTRESS", FindAnyObjectByType<OrzelekFortressDirector>(), 45);
            Capture("COMMAND", FindAnyObjectByType<EnemyCommandNetworkDirector>(), 80);
            Capture("OBJECTIVE", FindAnyObjectByType<DynamicBattlefieldDirector>(), 60);
            Capture("OPERATION", FindAnyObjectByType<MultiStageOperationDirector>(), 75);
            Capture("CONVOY", FindAnyObjectByType<ConvoyWarfareDirector>(), 85);
            Capture("COMBINED ARMS", FindAnyObjectByType<CombinedArmsDirector>(), 90);

            _lines.Sort((a, b) =>
            {
                int critical = b.Critical.CompareTo(a.Critical);
                return critical != 0 ? critical : b.Priority.CompareTo(a.Priority);
            });

            if (_lines.Count > 0 && _lines[0].Critical)
                SetCriticalAlert(_lines[0].Label + " // " + _lines[0].Status, AlertHoldSeconds);
        }

        private void Capture(string label, Behaviour source, int priority)
        {
            if (source == null || !source.enabled) return;
            Type type = source.GetType();
            string status = ReadStatus(source, type);
            if (string.IsNullOrWhiteSpace(status)) return;

            int sourceRound = ReadRound(source, type);
            if (sourceRound >= 0 && sourceRound != _game.CurrentRound) return;

            bool critical = IsCritical(status);
            _lines.Add(new TacticalLine
            {
                Label = label,
                Status = Compact(status, 70),
                Priority = priority + (critical ? 100 : 0),
                Critical = critical
            });
        }

        private string ReadStatus(Behaviour source, Type type)
        {
            FieldInfo field;
            if (!_statusFields.TryGetValue(type, out field))
            {
                field = type.GetField("_status", BindingFlags.Instance | BindingFlags.NonPublic);
                _statusFields[type] = field;
            }
            if (field == null) return string.Empty;
            object value = field.GetValue(source);
            return value as string ?? string.Empty;
        }

        private int ReadRound(Behaviour source, Type type)
        {
            FieldInfo field;
            if (!_roundFields.TryGetValue(type, out field))
            {
                field = type.GetField("_round", BindingFlags.Instance | BindingFlags.NonPublic);
                _roundFields[type] = field;
            }
            if (field == null) return _game != null ? _game.CurrentRound : -1;
            object value = field.GetValue(source);
            return value is int ? (int)value : -1;
        }

        private static bool IsCritical(string text)
        {
            string upper = text.ToUpperInvariant();
            return upper.Contains("CRITICAL") || upper.Contains("WARNING") || upper.Contains("UNDER FIRE") ||
                   upper.Contains("INBOUND") || upper.Contains("FAILED") || upper.Contains("DESTROY") ||
                   upper.Contains("SIEGE") || upper.Contains("AMBUSH") || upper.Contains("BREAKER");
        }

        private static string Compact(string text, int max)
        {
            string value = text.Replace("  ", " ").Trim();
            if (value.Length <= max) return value;
            return value.Substring(0, Mathf.Max(0, max - 3)) + "...";
        }

        private void SetCriticalAlert(string text, float hold)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _criticalAlert = Compact(text, 86);
            _criticalUntil = Mathf.Max(_criticalUntil, Time.unscaledTime + hold);
        }

        private int LineBudget()
        {
            switch (_density)
            {
                case CombatHudDensity.Minimal: return MinimalLineBudget;
                case CombatHudDensity.Standard: return StandardLineBudget;
                default: return FocusLineBudget;
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 0.90f, 1f) }
            };
            _lineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.92f, 0.95f, 1f) }
            };
            _criticalStyle = new GUIStyle(_lineStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.48f, 0.18f) }
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.62f, 0.68f, 0.75f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            int budget = LineBudget();
            bool showCritical = Time.unscaledTime < _criticalUntil && !string.IsNullOrEmpty(_criticalAlert);
            float width = Mathf.Min(560f, Screen.width * 0.48f);
            float lineHeight = 20f;
            float height = 35f + budget * lineHeight + (showCritical ? 25f : 0f);
            Rect panel = new Rect(14f, 14f, width, height);

            GUI.color = new Color(0.02f, 0.035f, 0.055f, _density == CombatHudDensity.Minimal ? 0.76f : 0.90f);
            GUI.Box(panel, string.Empty);
            GUI.color = Color.white;

            GUI.Label(new Rect(26f, 20f, width - 150f, 20f), "TACTICAL HUD // R" + _game.CurrentRound + " // " + _density.ToString().ToUpperInvariant(), _titleStyle);
            GUI.Label(new Rect(width - 132f, 20f, 118f, 20f), "F1 HUD MODE", _hintStyle);

            float y = 43f;
            if (showCritical)
            {
                GUI.Label(new Rect(26f, y, width - 36f, 22f), _criticalAlert, _criticalStyle);
                y += 25f;
            }

            int shown = 0;
            for (int i = 0; i < _lines.Count && shown < budget; i++)
            {
                TacticalLine line = _lines[i];
                GUI.Label(new Rect(26f, y, width - 36f, 20f), line.Label + "  •  " + line.Status, line.Critical ? _criticalStyle : _lineStyle);
                y += lineHeight;
                shown++;
            }

            if (shown == 0)
                GUI.Label(new Rect(26f, y, width - 36f, 20f), "BATTLEFIELD CLEAR // NO PRIORITY TACTICAL FEED", _lineStyle);
        }
    }
}
