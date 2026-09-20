using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Final v14.2 player-facing release policy. It does not own combat state. It only enforces the
    /// first-launch fullscreen default, bridges basic gamepad menu/pause input, and suppresses legacy
    /// development shell text while live combat is visible.
    /// </summary>
    [DefaultExecutionOrder(15000)]
    public sealed class FullPlayReleaseGuardV142 : MonoBehaviour
    {
        private const string FullscreenKey = "TankRevival.Demo.Fullscreen";
        private const string ResolutionKey = "TankRevival.Demo.Resolution";

        private TankGame _game;
        private DemoExperienceDirector _shell;
        private FieldInfo _stateField;
        private MethodInfo _startCampaign;
        private MethodInfo _togglePause;
        private GUIStyle _menuLabel;

        public static FullPlayReleaseGuardV142 Instance { get; private set; }
        public static bool Installed => Instance != null;
        public static bool GameplayShellSuppressed => Instance != null && Instance._shell != null && !Instance._shell.enabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<FullPlayReleaseGuardV142>() != null) return;
            var go = new GameObject("FullPlayReleaseGuard_v14.2");
            DontDestroyOnLoad(go);
            go.AddComponent<FullPlayReleaseGuardV142>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ApplyFirstLaunchFullscreenDefault();
            Resolve();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static void ApplyFirstLaunchFullscreenDefault()
        {
            bool firstDisplayLaunch = !PlayerPrefs.HasKey(FullscreenKey);
            if (!firstDisplayLaunch) return;

            Resolution[] resolutions = Screen.resolutions;
            int nativeWidth = Display.main != null ? Display.main.systemWidth : Screen.width;
            int nativeHeight = Display.main != null ? Display.main.systemHeight : Screen.height;
            int best = -1;
            int bestDistance = int.MaxValue;
            if (resolutions != null)
            {
                for (int i = 0; i < resolutions.Length; i++)
                {
                    int distance = Mathf.Abs(resolutions[i].width - nativeWidth) + Mathf.Abs(resolutions[i].height - nativeHeight);
                    if (distance >= bestDistance) continue;
                    best = i;
                    bestDistance = distance;
                }
            }

            PlayerPrefs.SetInt(FullscreenKey, 1);
            if (best >= 0) PlayerPrefs.SetInt(ResolutionKey, best);
            PlayerPrefs.Save();
            DemoPlayerSettings.LoadAndApply();

            if (nativeWidth > 0 && nativeHeight > 0)
                Screen.SetResolution(nativeWidth, nativeHeight, FullScreenMode.FullScreenWindow);
        }

        private void Resolve()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                if (_game != null)
                {
                    Type type = typeof(TankGame);
                    _stateField = type.GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
                    _startCampaign = type.GetMethod("StartCampaign", BindingFlags.Instance | BindingFlags.NonPublic);
                    _togglePause = type.GetMethod("TogglePause", BindingFlags.Instance | BindingFlags.NonPublic);
                }
            }
            if (_shell == null) _shell = FindAnyObjectByType<DemoExperienceDirector>();
        }

        private string StateName()
        {
            if (_game == null || _stateField == null) return "Menu";
            object value = _stateField.GetValue(_game);
            return value != null ? value.ToString() : "Menu";
        }

        private void Update()
        {
            Resolve();
            string state = StateName();

            if (_shell != null)
            {
                bool shouldShowShell = state != "Playing";
                if (_shell.enabled != shouldShowShell) _shell.enabled = shouldShowShell;
            }

            if (Input.GetKeyDown(KeyCode.JoystickButton7) && _togglePause != null && (state == "Playing" || state == "Paused"))
            {
                _togglePause.Invoke(_game, null);
                return;
            }

            if (Input.GetKeyDown(KeyCode.JoystickButton0) && _startCampaign != null &&
                (state == "Menu" || state == "GameOver" || state == "Victory"))
            {
                _startCampaign.Invoke(_game, null);
            }
        }

        private void EnsureStyle()
        {
            if (_menuLabel != null) return;
            _menuLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = new Color(0.58f, 0.70f, 0.78f) }
            };
        }

        private static void Cover(Rect rect, float alpha = 1f)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.012f, 0.020f, 0.032f, alpha);
            GUI.Box(rect, GUIContent.none);
            GUI.color = old;
        }

        private void OnGUI()
        {
            if (_game == null) return;
            EnsureStyle();
            GUI.depth = -3000;
            string state = StateName();

            if (state == "Menu")
            {
                float top = Mathf.Max(38f, Screen.height * 0.5f - 300f);
                Cover(new Rect(0f, top + 90f, Screen.width, 42f));
                GUI.Label(new Rect(0f, top + 98f, Screen.width, 24f), "100-ROUND ARMORED DEFENSE CAMPAIGN", _menuLabel);
                Cover(new Rect(0f, Screen.height - 42f, Screen.width, 42f));
            }
            else if (state == "Paused" || state == "GameOver" || state == "Victory")
            {
                Cover(new Rect(Screen.width - 286f, 8f, 282f, 44f), 0.96f);
            }
        }
    }
}
