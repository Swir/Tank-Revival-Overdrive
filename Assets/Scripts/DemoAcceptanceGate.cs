using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Development/RC acceptance overlay for the public-demo path.
    /// It observes the authoritative TankGame state and records whether the essential hand-off flow
    /// was actually exercised: menu -> play -> pause -> resume -> terminal state/restart.
    /// No gameplay state is owned or modified by this component.
    /// </summary>
    [DefaultExecutionOrder(9000)]
    public sealed class DemoAcceptanceGate : MonoBehaviour
    {
        private const string Version = "v5.0.0-rc1";
        private readonly HashSet<string> _statesSeen = new HashSet<string>(StringComparer.Ordinal);
        private TankGame _game;
        private FieldInfo _stateField;
        private GUIStyle _style;
        private float _startedAt;
        private float _playingSeconds;
        private float _worstFrameMs;
        private int _frameSamples;
        private bool _armed;
        private bool _passed;
        private string _lastState = "Unknown";

        public static bool Passed { get; private set; }
        public static string Summary { get; private set; } = "Acceptance gate not run";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (FindAnyObjectByType<DemoAcceptanceGate>() != null) return;
            var go = new GameObject("DemoAcceptanceGate_v5");
            DontDestroyOnLoad(go);
            go.AddComponent<DemoAcceptanceGate>();
#endif
        }

        private void Awake()
        {
            _startedAt = Time.realtimeSinceStartup;
            ResolveGame();
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_game == null) ResolveGame();
            if (Input.GetKeyDown(KeyCode.F10))
            {
                _armed = !_armed;
                if (_armed) ResetGate();
            }
            if (!_armed || _game == null || _stateField == null) return;

            string state = ReadState();
            _statesSeen.Add(state);
            _lastState = state;
            if (state == "Playing") _playingSeconds += Time.unscaledDeltaTime;

            float ms = Time.unscaledDeltaTime * 1000f;
            if (ms > _worstFrameMs) _worstFrameMs = ms;
            _frameSamples++;

            bool hasMenu = _statesSeen.Contains("Menu");
            bool hasPlay = _statesSeen.Contains("Playing");
            bool hasPause = _statesSeen.Contains("Paused");
            bool hasTerminal = _statesSeen.Contains("Victory") || _statesSeen.Contains("GameOver") || _playingSeconds >= 45f;
            bool shell = DemoExperienceDirector.HasPlayerFacingShell;

            _passed = shell && hasMenu && hasPlay && hasPause && hasTerminal && _playingSeconds >= 5f;
            Passed = _passed;
            Summary = $"{Version} shell={shell} menu={hasMenu} play={hasPlay} pause={hasPause} terminal/soak={hasTerminal} playTime={_playingSeconds:0.0}s worstFrame={_worstFrameMs:0.0}ms samples={_frameSamples}";
#endif
        }

        private void ResetGate()
        {
            _statesSeen.Clear();
            _playingSeconds = 0f;
            _worstFrameMs = 0f;
            _frameSamples = 0;
            _passed = false;
            Passed = false;
            Summary = "Acceptance gate armed";
            _startedAt = Time.realtimeSinceStartup;
            _statesSeen.Add(ReadState());
        }

        private void ResolveGame()
        {
            _game = FindAnyObjectByType<TankGame>();
            if (_game == null) return;
            _stateField = typeof(TankGame).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private string ReadState()
        {
            if (_game == null || _stateField == null) return "Unknown";
            object state = _stateField.GetValue(_game);
            return state != null ? state.ToString() : "Unknown";
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_armed) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 13,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
            }

            string status = _passed ? "PASS" : "RUNNING";
            string text = $"DEMO ACCEPTANCE {Version}  [{status}]\nF10: stop gate\nstate={_lastState} uptime={Time.realtimeSinceStartup - _startedAt:0}s\n{Summary}";
            GUI.Box(new Rect(16f, Screen.height - 126f, Mathf.Min(760f, Screen.width - 32f), 110f), text, _style);
#endif
        }
    }
}
