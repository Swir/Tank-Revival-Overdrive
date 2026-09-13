using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Demo 2 player-experience layer. It does not own combat or campaign state; it observes TankGame,
    /// replaces the old wall-of-text first-run flow with short contextual coaching, and patches stale
    /// pre-demo presentation without changing gameplay authority.
    /// </summary>
    [DefaultExecutionOrder(-11000)]
    public sealed class Demo2PlayerExperienceDirector : MonoBehaviour
    {
        public const int CoachingRoundLimit = 10;
        public const float PromptHoldSeconds = 5.5f;
        public const float PromptFadeSeconds = 0.65f;
        public const int MaxPromptCharacters = 54;
        public const string CandidateLabel = "DEMO 2 RC1";

        private const string LegacyFirstRunKey = "TankRevival.Demo.FirstRunComplete";
        private const string CoachingKey = "TankRevival.Demo2.CoachingEnabled";

        private TankGame _game;
        private DemoExperienceDirector _legacyShell;
        private CombatReadabilityDirector _readability;
        private FieldInfo _stateField;
        private FieldInfo _legacyPageField;
        private FieldInfo _densityField;
        private int _lastRound = -1;
        private float _roundStartedAt;
        private bool _hudDefaultApplied;
        private bool _legacyFirstRunSuppressed;
        private bool _coachingEnabled = true;
        private GUIStyle _badgeStyle;
        private GUIStyle _promptStyle;
        private GUIStyle _hintStyle;

        public static Demo2PlayerExperienceDirector Instance { get; private set; }
        public static bool ConfigurationValid =>
            CoachingRoundLimit == 10 &&
            PromptHoldSeconds >= 4f && PromptHoldSeconds <= 8f &&
            PromptFadeSeconds >= 0.3f && PromptFadeSeconds <= 1.5f &&
            MaxPromptCharacters >= 40 && MaxPromptCharacters <= 72;
        public static bool CoachingEnabled => Instance != null && Instance._coachingEnabled;
        public static string RuntimeVersion => string.IsNullOrWhiteSpace(Application.version) ? "unknown" : Application.version;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<Demo2PlayerExperienceDirector>() != null) return;
            var go = new GameObject("Demo2PlayerExperienceDirector_v7");
            DontDestroyOnLoad(go);
            go.AddComponent<Demo2PlayerExperienceDirector>();
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
            _coachingEnabled = PlayerPrefs.GetInt(CoachingKey, 1) != 0;
            ResolveDependencies();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void ResolveDependencies()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                if (_game != null)
                    _stateField = typeof(TankGame).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            if (_legacyShell == null)
            {
                _legacyShell = FindAnyObjectByType<DemoExperienceDirector>();
                if (_legacyShell != null)
                    _legacyPageField = typeof(DemoExperienceDirector).GetField("_page", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            if (_readability == null)
            {
                _readability = FindAnyObjectByType<CombatReadabilityDirector>();
                if (_readability != null)
                    _densityField = typeof(CombatReadabilityDirector).GetField("_density", BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }

        private void Update()
        {
            ResolveDependencies();
            SuppressLegacyFirstRun();
            ApplyBattlefieldFirstHudDefault();

            if (Input.GetKeyDown(KeyCode.F2))
            {
                _coachingEnabled = !_coachingEnabled;
                PlayerPrefs.SetInt(CoachingKey, _coachingEnabled ? 1 : 0);
                PlayerPrefs.Save();
            }

            if (_game == null || !_game.IsPlaying) return;
            if (_game.CurrentRound != _lastRound)
            {
                _lastRound = _game.CurrentRound;
                _roundStartedAt = Time.unscaledTime;
            }
        }

        private void SuppressLegacyFirstRun()
        {
            if (_legacyFirstRunSuppressed) return;
            PlayerPrefs.SetInt(LegacyFirstRunKey, 1);
            PlayerPrefs.Save();

            if (_legacyShell == null || _legacyPageField == null) return;
            object page = _legacyPageField.GetValue(_legacyShell);
            if (page != null && page.ToString() == "FirstRun")
            {
                Array values = Enum.GetValues(page.GetType());
                for (int i = 0; i < values.Length; i++)
                {
                    object value = values.GetValue(i);
                    if (value != null && value.ToString() == "None")
                    {
                        _legacyPageField.SetValue(_legacyShell, value);
                        break;
                    }
                }
            }
            _legacyFirstRunSuppressed = true;
        }

        private void ApplyBattlefieldFirstHudDefault()
        {
            if (_hudDefaultApplied || _readability == null || _densityField == null) return;
            _densityField.SetValue(_readability, CombatHudDensity.Minimal);
            _hudDefaultApplied = true;
        }

        private string StateName()
        {
            if (_game == null || _stateField == null) return "Menu";
            object state = _stateField.GetValue(_game);
            return state != null ? state.ToString() : "Menu";
        }

        private static string CoachingPrompt(int round, float elapsed)
        {
            if (round < 1 || round > CoachingRoundLimit || elapsed > PromptHoldSeconds) return string.Empty;
            switch (round)
            {
                case 1: return "MOVE  •  WASD / ARROWS";
                case 2: return "FIRE  •  LMB / SPACE / LEFT CTRL";
                case 3: return "AIM  •  MOUSE POINTS THE TURRET";
                case 4: return "AMMO  •  Q/E OR KEYS 1–7";
                case 5: return "DEFEND ORZEŁEK  •  WATCH ITS PRESSURE";
                case 6: return "SUPPLY TANKS DROP SPECIAL AMMUNITION";
                case 7: return "F1  •  CYCLE TACTICAL HUD DENSITY";
                case 8: return "PRIORITIZE SIEGE / ELITE / BOSS THREATS";
                case 9: return "OBJECTIVES PAY WAR BONDS FOR DEFENSE";
                case 10: return "F2  •  TOGGLE THESE COACHING PROMPTS";
                default: return string.Empty;
            }
        }

        private void EnsureStyles()
        {
            if (_badgeStyle != null) return;
            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.36f, 0.92f, 1f) }
            };
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.62f, 0.72f, 0.80f) }
            };
        }

        private void OnGUI()
        {
            EnsureStyles();
            string state = StateName();
            GUI.depth = -2200;

            if (state == "Menu")
            {
                DrawMenuIdentityPatch();
                return;
            }

            if (state == "Paused" || state == "GameOver" || state == "Victory")
                DrawCornerIdentity();

            if (_game == null || !_game.IsPlaying || !_coachingEnabled) return;
            float elapsed = Time.unscaledTime - _roundStartedAt;
            string prompt = CoachingPrompt(_game.CurrentRound, elapsed);
            if (string.IsNullOrEmpty(prompt)) return;

            float fadeStart = PromptHoldSeconds - PromptFadeSeconds;
            float alpha = elapsed <= fadeStart ? 1f : Mathf.Clamp01((PromptHoldSeconds - elapsed) / PromptFadeSeconds);
            float width = Mathf.Min(620f, Screen.width - 40f);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 112f;

            Color old = GUI.color;
            GUI.color = new Color(0.015f, 0.028f, 0.045f, 0.88f * alpha);
            GUI.Box(new Rect(x, y, width, 58f), GUIContent.none);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(x + 14f, y + 8f, width - 28f, 24f), prompt, _promptStyle);
            GUI.Label(new Rect(x + 14f, y + 34f, width - 28f, 18f), "AUTO-HIDES  •  F2 COACHING", _hintStyle);
            GUI.color = old;
        }

        private void DrawMenuIdentityPatch()
        {
            float top = Mathf.Max(38f, Screen.height * 0.5f - 300f);
            Color old = GUI.color;
            GUI.color = new Color(0.012f, 0.020f, 0.032f, 1f);
            GUI.Box(new Rect(0f, top + 92f, Screen.width, 38f), GUIContent.none);
            GUI.Box(new Rect(0f, Screen.height - 40f, 470f, 40f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, top + 98f, Screen.width, 24f), "PUBLIC DEMO 2 CANDIDATE  •  BATTLEFIELD-FIRST HUD", _badgeStyle);
            GUI.Label(new Rect(18f, Screen.height - 32f, 440f, 20f), CandidateLabel + "  •  " + RuntimeVersion + "  •  WINDOWS x64", _badgeStyle);
            GUI.color = old;
        }

        private void DrawCornerIdentity()
        {
            float w = 250f;
            Color old = GUI.color;
            GUI.color = new Color(0.012f, 0.020f, 0.032f, 0.90f);
            GUI.Box(new Rect(Screen.width - w - 14f, 14f, w, 30f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - w - 8f, 18f, w - 12f, 20f), CandidateLabel + "  •  " + RuntimeVersion, _badgeStyle);
            GUI.color = old;
        }
    }
}
