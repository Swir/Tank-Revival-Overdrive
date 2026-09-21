using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Final v14.2 player-facing release policy. It does not own combat state. It enforces the
    /// first-launch fullscreen default, bridges basic gamepad menu/pause input, and presents a
    /// release-clean combat frame above legacy development IMGUI telemetry. F9 deliberately exposes
    /// the legacy diagnostic presentation for developers; it is OFF by default and never persisted.
    /// </summary>
    [DefaultExecutionOrder(15000)]
    public sealed class FullPlayReleaseGuardV142 : MonoBehaviour
    {
        private const string FullscreenKey = "TankRevival.Demo.Fullscreen";
        private const string ResolutionKey = "TankRevival.Demo.Resolution";
        private const int CleanPresentationDepth = -32000;

        private TankGame _game;
        private DemoExperienceDirector _shell;
        private FieldInfo _stateField;
        private FieldInfo _eagleHpField;
        private FieldInfo _aliveEnemiesField;
        private FieldInfo _enemiesToSpawnField;
        private FieldInfo _bossPendingField;
        private FieldInfo _toastField;
        private FieldInfo _toastUntilField;
        private MethodInfo _startCampaign;
        private MethodInfo _togglePause;
        private Camera _gameplayCamera;
        private RenderTexture _cleanGameplayFrame;
        private int _cleanFrameWidth;
        private int _cleanFrameHeight;
        private bool _debugPresentation;
        private GUIStyle _menuLabel;
        private GUIStyle _hudLabel;
        private GUIStyle _hudValue;
        private GUIStyle _hudCenter;
        private GUIStyle _toastStyle;

        public static FullPlayReleaseGuardV142 Instance { get; private set; }
        public static bool Installed => Instance != null;
        public static bool GameplayShellSuppressed => Instance != null && Instance._shell != null && !Instance._shell.enabled;
        public static bool CleanGameplayPresentationActive => Instance != null && Instance.IsCleanGameplayPresentationActive();

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
            ReleaseCleanGameplayFrame();
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
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    _stateField = type.GetField("_state", flags);
                    _eagleHpField = type.GetField("_eagleHp", flags);
                    _aliveEnemiesField = type.GetField("_aliveEnemies", flags);
                    _enemiesToSpawnField = type.GetField("_enemiesToSpawn", flags);
                    _bossPendingField = type.GetField("_bossPending", flags);
                    _toastField = type.GetField("_toast", flags);
                    _toastUntilField = type.GetField("_toastUntil", flags);
                    _startCampaign = type.GetMethod("StartCampaign", flags);
                    _togglePause = type.GetMethod("TogglePause", flags);
                }
            }
            if (_shell == null) _shell = FindAnyObjectByType<DemoExperienceDirector>();
            if (_gameplayCamera == null) _gameplayCamera = Camera.main;
        }

        private string StateName()
        {
            if (_game == null || _stateField == null) return "Menu";
            object value = _stateField.GetValue(_game);
            return value != null ? value.ToString() : "Menu";
        }

        private int ReadInt(FieldInfo field, int fallback = 0)
        {
            if (_game == null || field == null) return fallback;
            object value = field.GetValue(_game);
            return value is int number ? number : fallback;
        }

        private bool ReadBool(FieldInfo field)
        {
            if (_game == null || field == null) return false;
            object value = field.GetValue(_game);
            return value is bool flag && flag;
        }

        private string ReadString(FieldInfo field)
        {
            if (_game == null || field == null) return string.Empty;
            return field.GetValue(_game) as string ?? string.Empty;
        }

        private float ReadFloat(FieldInfo field)
        {
            if (_game == null || field == null) return 0f;
            object value = field.GetValue(_game);
            return value is float number ? number : 0f;
        }

        private bool IsCleanGameplayPresentationActive()
        {
            return !_debugPresentation && !Application.isBatchMode && StateName() == "Playing";
        }

        private void Update()
        {
            Resolve();
            string state = StateName();

            if (Input.GetKeyDown(KeyCode.F9))
            {
                _debugPresentation = !_debugPresentation;
                if (_debugPresentation) ReleaseCleanGameplayFrame();
            }

            if (_shell != null)
            {
                bool shouldShowShell = state != "Playing" || _debugPresentation;
                if (_shell.enabled != shouldShowShell) _shell.enabled = shouldShowShell;
            }

            if (state == "Playing" && !_debugPresentation && !Application.isBatchMode)
                EnsureCleanGameplayFrame();
            else
                ReleaseCleanGameplayFrame();

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

        private void EnsureCleanGameplayFrame()
        {
            if (_gameplayCamera == null) _gameplayCamera = Camera.main;
            if (_gameplayCamera == null) return;

            int width = Mathf.Max(Screen.width, 640);
            int height = Mathf.Max(Screen.height, 360);
            if (_cleanGameplayFrame != null && (_cleanFrameWidth != width || _cleanFrameHeight != height))
                ReleaseCleanGameplayFrame();

            if (_cleanGameplayFrame == null)
            {
                _cleanGameplayFrame = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "v14.2 Release Clean Gameplay Frame",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _cleanGameplayFrame.Create();
                _cleanFrameWidth = width;
                _cleanFrameHeight = height;
            }

            if (_gameplayCamera.targetTexture != _cleanGameplayFrame)
                _gameplayCamera.targetTexture = _cleanGameplayFrame;
        }

        private void ReleaseCleanGameplayFrame()
        {
            if (_gameplayCamera != null && _gameplayCamera.targetTexture == _cleanGameplayFrame)
                _gameplayCamera.targetTexture = null;

            if (_cleanGameplayFrame != null)
            {
                _cleanGameplayFrame.Release();
                Destroy(_cleanGameplayFrame);
                _cleanGameplayFrame = null;
            }
            _cleanFrameWidth = 0;
            _cleanFrameHeight = 0;
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
            _hudLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 10,
                normal = { textColor = new Color(0.62f, 0.75f, 0.84f) }
            };
            _hudValue = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _hudCenter = new GUIStyle(_hudValue)
            {
                alignment = TextAnchor.MiddleCenter
            };
            _toastStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.84f, 0.32f) }
            };
        }

        private static void Cover(Rect rect, float alpha = 1f)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.012f, 0.020f, 0.032f, alpha);
            GUI.Box(rect, GUIContent.none);
            GUI.color = old;
        }

        private void DrawReleaseCombatHud()
        {
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            int armor = player != null && player.Health != null ? player.Health.Current : 0;
            int armorMax = player != null && player.Health != null ? Mathf.Max(player.Health.Max, 1) : 1;
            int eagle = Mathf.Clamp(ReadInt(_eagleHpField, 6), 0, 6);
            int threats = Mathf.Max(0, ReadInt(_aliveEnemiesField) + ReadInt(_enemiesToSpawnField) + (ReadBool(_bossPendingField) ? 1 : 0));
            AmmoType active = player != null ? player.ActiveAmmo : AmmoType.Basic;
            int ammo = player != null ? player.GetAmmoCount(active) : 0;
            string ammoValue = active == AmmoType.Basic ? "∞" : Mathf.Max(0, ammo).ToString();

            const float pad = 12f;
            const float panelHeight = 58f;
            float leftWidth = Mathf.Min(310f, Screen.width * 0.30f);
            float rightWidth = Mathf.Min(300f, Screen.width * 0.29f);

            Cover(new Rect(pad, pad, leftWidth, panelHeight), 0.90f);
            GUI.Label(new Rect(pad + 12f, pad + 5f, leftWidth - 24f, 18f), "ORZEŁ OVERDRIVE", _hudLabel);
            GUI.Label(new Rect(pad + 12f, pad + 23f, leftWidth - 24f, 27f), $"ARMOR {armor}/{armorMax}   •   ORZEŁ {eagle}/6", _hudValue);

            float centerWidth = Mathf.Min(280f, Screen.width * 0.27f);
            float centerX = (Screen.width - centerWidth) * 0.5f;
            Cover(new Rect(centerX, pad, centerWidth, panelHeight), 0.90f);
            GUI.Label(new Rect(centerX + 8f, pad + 4f, centerWidth - 16f, 20f), $"ROUND {_game.CurrentRound:000}/100", _hudCenter);
            GUI.Label(new Rect(centerX + 8f, pad + 24f, centerWidth - 16f, 24f), threats > 0 ? $"DEFEND ORZEŁ   •   {threats} THREATS" : "SECTOR CLEAR", _hudCenter);

            float rightX = Screen.width - rightWidth - pad;
            Cover(new Rect(rightX, pad, rightWidth, panelHeight), 0.90f);
            GUI.Label(new Rect(rightX + 12f, pad + 5f, rightWidth - 24f, 18f), "AMMUNITION", _hudLabel);
            GUI.Label(new Rect(rightX + 12f, pad + 23f, rightWidth - 24f, 27f), $"{AmmoDatabase.DisplayName(active)}   {ammoValue}", _hudValue);

            string toast = ReadString(_toastField);
            if (!string.IsNullOrWhiteSpace(toast) && Time.unscaledTime < ReadFloat(_toastUntilField))
            {
                float toastWidth = Mathf.Min(620f, Screen.width - 48f);
                float toastX = (Screen.width - toastWidth) * 0.5f;
                Cover(new Rect(toastX, Screen.height - 64f, toastWidth, 40f), 0.88f);
                GUI.Label(new Rect(toastX + 10f, Screen.height - 59f, toastWidth - 20f, 30f), toast, _toastStyle);
            }
        }

        private void OnGUI()
        {
            if (_game == null) return;
            EnsureStyle();
            string state = StateName();

            if (state == "Playing" && _cleanGameplayFrame != null && !_debugPresentation)
            {
                GUI.depth = CleanPresentationDepth;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _cleanGameplayFrame, ScaleMode.StretchToFill, false);
                DrawReleaseCombatHud();
                return;
            }

            GUI.depth = -3000;
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

            if (_debugPresentation && state == "Playing")
            {
                Cover(new Rect(Screen.width - 220f, Screen.height - 34f, 208f, 24f), 0.82f);
                GUI.Label(new Rect(Screen.width - 216f, Screen.height - 31f, 200f, 18f), "DEBUG PRESENTATION [F9]", _menuLabel);
            }
        }
    }
}
