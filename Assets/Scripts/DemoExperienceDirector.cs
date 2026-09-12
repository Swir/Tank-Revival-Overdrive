using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Player-facing shell for the Windows demo path. It deliberately sits on top of the existing
    /// TankGame authority instead of replacing campaign, combat or HUD state.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class DemoExperienceDirector : MonoBehaviour
    {
        private enum Page
        {
            None,
            Settings,
            Controls,
            FirstRun
        }

        private const string FirstRunKey = "TankRevival.Demo.FirstRunComplete";
        private const string VersionLabel = "v4.9.0-dev";

        private TankGame _game;
        private FieldInfo _stateField;
        private MethodInfo _startCampaign;
        private MethodInfo _togglePause;
        private Page _page;
        private string _lastState = "Menu";
        private GUIStyle _title;
        private GUIStyle _heading;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _button;
        private GUIStyle _right;
        private Vector2 _settingsScroll;

        public static DemoExperienceDirector Instance { get; private set; }
        public static bool HasPlayerFacingShell => Instance != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<DemoExperienceDirector>() != null) return;
            var go = new GameObject("DemoExperienceDirector_v4.9");
            DontDestroyOnLoad(go);
            go.AddComponent<DemoExperienceDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DemoPlayerSettings.LoadAndApply();
            _page = PlayerPrefs.GetInt(FirstRunKey, 0) == 0 ? Page.FirstRun : Page.None;
            ResolveGame();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_game == null) ResolveGame();
            string state = StateName();

            // TankGame owns ESC/P pause transitions. If it resumes while a pause subpage was open,
            // close that subpage so no stale settings overlay survives over live combat.
            if (_lastState == "Paused" && state == "Playing" && _page != Page.FirstRun)
                _page = Page.None;

            _lastState = state;

            if ((state == "Menu" || state == "GameOver" || state == "Victory") && Input.GetKeyDown(KeyCode.Escape) && _page != Page.None)
                _page = Page.None;
        }

        private void ResolveGame()
        {
            _game = FindAnyObjectByType<TankGame>();
            if (_game == null) return;

            Type type = typeof(TankGame);
            _stateField = type.GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
            _startCampaign = type.GetMethod("StartCampaign", BindingFlags.Instance | BindingFlags.NonPublic);
            _togglePause = type.GetMethod("TogglePause", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private string StateName()
        {
            if (_game == null || _stateField == null) return "Menu";
            object value = _stateField.GetValue(_game);
            return value != null ? value.ToString() : "Menu";
        }

        private void StartCampaign()
        {
            if (_game == null || _startCampaign == null) return;
            _page = Page.None;
            _startCampaign.Invoke(_game, null);
        }

        private void ResumeCampaign()
        {
            if (_game == null || _togglePause == null) return;
            _page = Page.None;
            if (StateName() == "Paused") _togglePause.Invoke(_game, null);
        }

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.30f, 0.91f, 1f) }
            };
            _heading = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = new Color(0.82f, 0.90f, 0.95f) }
            };
            _small = new GUIStyle(_body)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.58f, 0.70f, 0.78f) }
            };
            _button = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                fixedHeight = 44f
            };
            _right = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight };
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUI.depth = -1000;
            string state = StateName();

            if (_page == Page.FirstRun)
            {
                DrawBackdrop();
                DrawFirstRun();
                return;
            }

            if (_page == Page.Settings)
            {
                DrawBackdrop();
                DrawSettings(state);
                return;
            }

            if (_page == Page.Controls)
            {
                DrawBackdrop();
                DrawControls(state);
                return;
            }

            if (state == "Menu")
            {
                DrawBackdrop();
                DrawMainMenu();
                return;
            }

            if (state == "Paused")
            {
                DrawBackdrop(0.86f);
                DrawPauseMenu();
                return;
            }

            if (state == "GameOver" || state == "Victory")
            {
                DrawBackdrop(0.90f);
                DrawEndScreen(state == "Victory");
                return;
            }

            DrawBuildIdentity();
        }

        private static void DrawBackdrop(float alpha = 1f)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.012f, 0.020f, 0.032f, alpha);
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            GUI.color = old;
        }

        private void DrawMainMenu()
        {
            float cx = Screen.width * 0.5f;
            float top = Mathf.Max(38f, Screen.height * 0.5f - 300f);
            GUI.Label(new Rect(0f, top, Screen.width, 62f), "TANK REVIVAL", _title);
            GUI.Label(new Rect(0f, top + 58f, Screen.width, 36f), "ORZEŁ OVERDRIVE", _heading);
            GUI.Label(new Rect(0f, top + 97f, Screen.width, 28f), "100-round armored defense campaign • pre-demo build", _small);

            float y = top + 154f;
            if (GUI.Button(new Rect(cx - 170f, y, 340f, 44f), "GRAJ / PLAY", _button)) StartCampaign();
            y += 54f;
            if (GUI.Button(new Rect(cx - 170f, y, 340f, 44f), "USTAWIENIA / SETTINGS", _button)) _page = Page.Settings;
            y += 54f;
            if (GUI.Button(new Rect(cx - 170f, y, 340f, 44f), "STEROWANIE / CONTROLS", _button)) _page = Page.Controls;
            y += 54f;
            if (GUI.Button(new Rect(cx - 170f, y, 340f, 44f), "WYJŚCIE / EXIT", _button)) Application.Quit();

            GUI.Label(new Rect(0f, y + 64f, Screen.width, 26f), "WASD/strzałki: ruch • mysz: celowanie • LPM/Spacja/Ctrl: ogień • Q/E lub 1–7: amunicja", _small);
            GUI.Label(new Rect(18f, Screen.height - 34f, 400f, 22f), "DEMO PREPARATION  •  " + VersionLabel, _small);
            GUI.Label(new Rect(Screen.width - 418f, Screen.height - 34f, 400f, 22f), DemoPlayerSettings.StatusLine, _right);
        }

        private void DrawPauseMenu()
        {
            float cx = Screen.width * 0.5f;
            float top = Screen.height * 0.5f - 190f;
            GUI.Label(new Rect(0f, top, Screen.width, 52f), "PAUZA", _title);
            GUI.Label(new Rect(0f, top + 56f, Screen.width, 28f), "Runda " + (_game != null ? _game.CurrentRound.ToString("000") : "---") + " / 100", _small);
            float y = top + 104f;
            if (GUI.Button(new Rect(cx - 170f, y, 340f, 44f), "WZNÓW / RESUME", _button)) ResumeCampaign();
            y += 54f;
            if (GUI.Button(new Rect(cx - 170f, y, 340f, 44f), "USTAWIENIA / SETTINGS", _button)) _page = Page.Settings;
            y += 54f;
            if (GUI.Button(new Rect(cx - 170f, y, 340f, 44f), "STEROWANIE / CONTROLS", _button)) _page = Page.Controls;
            y += 54f;
            if (GUI.Button(new Rect(cx - 170f, y, 340f, 44f), "WYJŚCIE DO WINDOWS", _button)) Application.Quit();
            GUI.Label(new Rect(0f, y + 60f, Screen.width, 22f), "ESC lub P również wznawia grę", _small);
        }

        private void DrawEndScreen(bool victory)
        {
            float cx = Screen.width * 0.5f;
            float top = Screen.height * 0.5f - 180f;
            GUI.Label(new Rect(0f, top, Screen.width, 58f), victory ? "ORZEŁEK OCALONY" : "OBRONA PRZERWANA", _title);
            GUI.Label(new Rect(0f, top + 62f, Screen.width, 32f), victory ? "100 rund ukończone" : "Spróbuj ponownie i wzmocnij obronę", _body);
            float y = top + 118f;
            if (GUI.Button(new Rect(cx - 170f, y, 340f, 44f), "ZAGRAJ PONOWNIE", _button)) StartCampaign();
            y += 54f;
            if (GUI.Button(new Rect(cx - 170f, y, 340f, 44f), "USTAWIENIA", _button)) _page = Page.Settings;
            y += 54f;
            if (GUI.Button(new Rect(cx - 170f, y, 340f, 44f), "WYJŚCIE", _button)) Application.Quit();
        }

        private void DrawFirstRun()
        {
            float cx = Screen.width * 0.5f;
            float w = Mathf.Min(760f, Screen.width - 36f);
            float x = cx - w * 0.5f;
            float y = Mathf.Max(24f, Screen.height * 0.5f - 300f);

            GUI.Label(new Rect(0f, y, Screen.width, 58f), "WITAJ W ORZEŁ OVERDRIVE", _title);
            GUI.Label(new Rect(x + 34f, y + 70f, w - 68f, 64f), "Broń Orzełka przez 100 coraz trudniejszych rund. Poluj na czołgi zaopatrzenia, zmieniaj amunicję i rozwijaj maszynę pomiędzy kolejnymi sektorami.", _body);
            GUI.Label(new Rect(x + 34f, y + 146f, w - 68f, 28f), "NAJWAŻNIEJSZE STEROWANIE", _heading);
            GUI.Label(new Rect(x + 34f, y + 184f, w - 68f, 112f), "WASD / strzałki — ruch\nMysz — kierunek działa\nLPM / Spacja / Lewy Ctrl — ogień\nQ / E — zmiana amunicji • 1–7 — wybór bezpośredni\nESC / P — pauza", _body);
            GUI.Label(new Rect(x + 34f, y + 306f, w - 68f, 72f), "Wskazówka: kolor świecącego czołgu zaopatrzenia odpowiada typowi amunicji, którą zostawi po zniszczeniu.", _small);

            if (GUI.Button(new Rect(cx - 180f, y + 395f, 360f, 46f), "ROZUMIEM — PRZEJDŹ DO MENU", _button))
            {
                PlayerPrefs.SetInt(FirstRunKey, 1);
                PlayerPrefs.Save();
                _page = Page.None;
            }
            if (GUI.Button(new Rect(cx - 180f, y + 451f, 360f, 42f), "USTAWIENIA PRZED STARTEM", _button))
            {
                PlayerPrefs.SetInt(FirstRunKey, 1);
                PlayerPrefs.Save();
                _page = Page.Settings;
            }
            GUI.Label(new Rect(0f, y + 510f, Screen.width, 24f), VersionLabel + " • Windows x64 demo preparation", _small);
        }

        private void DrawSettings(string state)
        {
            float w = Mathf.Min(760f, Screen.width - 36f);
            float h = Mathf.Min(650f, Screen.height - 36f);
            Rect area = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);
            GUI.Box(area, GUIContent.none);
            GUI.Label(new Rect(area.x, area.y + 14f, area.width, 42f), "USTAWIENIA", _heading);

            Rect view = new Rect(area.x + 28f, area.y + 68f, area.width - 56f, area.height - 138f);
            Rect content = new Rect(0f, 0f, view.width - 22f, 520f);
            _settingsScroll = GUI.BeginScrollView(view, _settingsScroll, content);

            float y = 6f;
            DrawSettingRow(content.width, ref y, "Jakość grafiki / Graphics", DemoPlayerSettings.QualityName,
                () => DemoPlayerSettings.CycleQuality(-1), () => DemoPlayerSettings.CycleQuality(1));
            DrawSettingRow(content.width, ref y, "Tryb ekranu / Display", DemoPlayerSettings.Fullscreen ? "FULLSCREEN" : "WINDOWED",
                DemoPlayerSettings.ToggleFullscreen, DemoPlayerSettings.ToggleFullscreen);
            DrawSettingRow(content.width, ref y, "Rozdzielczość / Resolution", DemoPlayerSettings.ResolutionName,
                () => DemoPlayerSettings.CycleResolution(-1), () => DemoPlayerSettings.CycleResolution(1));
            DrawSettingRow(content.width, ref y, "V-Sync", DemoPlayerSettings.VSync ? "ON" : "OFF",
                DemoPlayerSettings.ToggleVSync, DemoPlayerSettings.ToggleVSync);
            DrawSettingRow(content.width, ref y, "Limit FPS", DemoPlayerSettings.FrameLimitName,
                () => DemoPlayerSettings.CycleFrameLimit(-1), () => DemoPlayerSettings.CycleFrameLimit(1));

            y += 12f;
            GUI.Label(new Rect(0f, y, content.width, 28f), "GŁOŚNOŚĆ / MASTER VOLUME  " + Mathf.RoundToInt(DemoPlayerSettings.MasterVolume * 100f) + "%", _body);
            y += 32f;
            float volume = GUI.HorizontalSlider(new Rect(42f, y, content.width - 84f, 24f), DemoPlayerSettings.MasterVolume, 0f, 1f);
            if (Mathf.Abs(volume - DemoPlayerSettings.MasterVolume) > 0.005f) DemoPlayerSettings.SetMasterVolume(volume);
            y += 44f;

            GUI.Label(new Rect(24f, y, content.width - 48f, 72f), "AUTO pozwala silnikowi dynamicznie obniżać gęstość efektów przy ciężkich rundach. BALANCED i PERFORMANCE ustawiają dodatkowy minimalny poziom oszczędzania efektów, ale nie zmieniają obrażeń, AI ani liczby wrogów.", _small);
            y += 88f;
            GUI.Label(new Rect(0f, y, content.width, 28f), DemoPlayerSettings.StatusLine, _small);
            GUI.EndScrollView();

            string back = state == "Paused" ? "WRÓĆ DO PAUZY" : "WRÓĆ";
            if (GUI.Button(new Rect(area.x + area.width * 0.5f - 150f, area.yMax - 58f, 300f, 42f), back, _button)) _page = Page.None;
        }

        private void DrawSettingRow(float width, ref float y, string label, string value, Action previous, Action next)
        {
            GUI.Label(new Rect(20f, y, width * 0.48f, 34f), label, _body);
            if (GUI.Button(new Rect(width * 0.53f, y, 42f, 34f), "<")) previous();
            GUI.Label(new Rect(width * 0.53f + 48f, y, width * 0.29f - 96f, 34f), value, _body);
            if (GUI.Button(new Rect(width * 0.82f, y, 42f, 34f), ">")) next();
            y += 44f;
        }

        private void DrawControls(string state)
        {
            float w = Mathf.Min(760f, Screen.width - 36f);
            float h = Mathf.Min(610f, Screen.height - 36f);
            Rect area = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);
            GUI.Box(area, GUIContent.none);
            GUI.Label(new Rect(area.x, area.y + 14f, area.width, 42f), "STEROWANIE", _heading);

            string controls =
                "RUCH\nW / A / S / D  lub  STRZAŁKI\n\n" +
                "CELOWANIE I OGIEŃ\nMysz — obrót działa\nLPM / Spacja / Lewy Ctrl — strzał\n\n" +
                "AMUNICJA\nQ / E — poprzednia / następna\n1 Standard • 2 AP • 3 HE • 4 Fire • 5 EMP • 6 Twin • 7 Plasma\n\n" +
                "SYSTEMY BOJOWE\nR — broń dodatkowa • Shift — zdolność podwozia • G — Overdrive\n\n" +
                "MENU\nESC / P — pauza / powrót do gry\nF3 — telemetria wydajności w buildzie developerskim";

            GUI.Label(new Rect(area.x + 48f, area.y + 78f, area.width - 96f, area.height - 158f), controls, _body);
            string back = state == "Paused" ? "WRÓĆ DO PAUZY" : "WRÓĆ";
            if (GUI.Button(new Rect(area.x + area.width * 0.5f - 150f, area.yMax - 58f, 300f, 42f), back, _button)) _page = Page.None;
        }

        private void DrawBuildIdentity()
        {
            GUI.Label(new Rect(Screen.width - 260f, Screen.height - 27f, 246f, 20f), "PRE-DEMO " + VersionLabel, _right);
        }
    }

    /// <summary>
    /// Persistent user-facing presentation settings. The quality preset exposes a minimum performance
    /// budget tier consumed by WarfarePerformanceGovernor; gameplay authority is never modified.
    /// </summary>
    public static class DemoPlayerSettings
    {
        public enum QualityPreset
        {
            Auto,
            Quality,
            Balanced,
            Performance
        }

        private const string Prefix = "TankRevival.Demo.";
        private static readonly int[] FrameLimits = { 60, 90, 120, 144 };
        private static QualityPreset _quality = QualityPreset.Auto;
        private static float _masterVolume = 0.82f;
        private static bool _fullscreen = true;
        private static bool _vsync = true;
        private static int _frameLimitIndex = 2;
        private static int _resolutionIndex = -1;

        public static QualityPreset Quality => _quality;
        public static float MasterVolume => _masterVolume;
        public static bool Fullscreen => _fullscreen;
        public static bool VSync => _vsync;
        public static string QualityName => _quality.ToString().ToUpperInvariant();
        public static string FrameLimitName => FrameLimits[_frameLimitIndex] + " FPS";
        public static string ResolutionName
        {
            get
            {
                Resolution[] list = Screen.resolutions;
                if (list == null || list.Length == 0) return Screen.width + " x " + Screen.height;
                int index = Mathf.Clamp(_resolutionIndex, 0, list.Length - 1);
                return list[index].width + " x " + list[index].height;
            }
        }

        public static WarfarePerformanceGovernor.BudgetTier MinimumBudgetTier
        {
            get
            {
                if (_quality == QualityPreset.Performance) return WarfarePerformanceGovernor.BudgetTier.Survival;
                if (_quality == QualityPreset.Balanced) return WarfarePerformanceGovernor.BudgetTier.Balanced;
                return WarfarePerformanceGovernor.BudgetTier.Full;
            }
        }

        public static string StatusLine => QualityName + " • " + ResolutionName + " • " + FrameLimitName + " • VOL " + Mathf.RoundToInt(_masterVolume * 100f) + "%";

        public static void LoadAndApply()
        {
            _quality = (QualityPreset)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Quality", 0), 0, 3);
            _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Volume", 0.82f));
            _fullscreen = PlayerPrefs.GetInt(Prefix + "Fullscreen", 1) != 0;
            _vsync = PlayerPrefs.GetInt(Prefix + "VSync", 1) != 0;
            _frameLimitIndex = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "FrameLimit", 2), 0, FrameLimits.Length - 1);
            _resolutionIndex = PlayerPrefs.GetInt(Prefix + "Resolution", -1);

            Resolution[] resolutions = Screen.resolutions;
            if (resolutions != null && resolutions.Length > 0)
            {
                if (_resolutionIndex < 0 || _resolutionIndex >= resolutions.Length)
                    _resolutionIndex = FindClosestResolution(resolutions, Screen.width, Screen.height);
            }

            ApplyAll(false);
        }

        public static void CycleQuality(int delta)
        {
            int count = Enum.GetValues(typeof(QualityPreset)).Length;
            int value = ((int)_quality + delta) % count;
            if (value < 0) value += count;
            _quality = (QualityPreset)value;
            ApplyQuality();
            Save();
        }

        public static void ToggleFullscreen()
        {
            _fullscreen = !_fullscreen;
            ApplyResolution();
            Save();
        }

        public static void ToggleVSync()
        {
            _vsync = !_vsync;
            ApplyFramePolicy();
            Save();
        }

        public static void CycleFrameLimit(int delta)
        {
            _frameLimitIndex = (_frameLimitIndex + delta) % FrameLimits.Length;
            if (_frameLimitIndex < 0) _frameLimitIndex += FrameLimits.Length;
            ApplyFramePolicy();
            Save();
        }

        public static void CycleResolution(int delta)
        {
            Resolution[] list = Screen.resolutions;
            if (list == null || list.Length == 0) return;
            _resolutionIndex = (_resolutionIndex + delta) % list.Length;
            if (_resolutionIndex < 0) _resolutionIndex += list.Length;
            ApplyResolution();
            Save();
        }

        public static void SetMasterVolume(float value)
        {
            _masterVolume = Mathf.Clamp01(value);
            AudioListener.volume = _masterVolume;
            PlayerPrefs.SetFloat(Prefix + "Volume", _masterVolume);
            PlayerPrefs.Save();
        }

        private static void ApplyAll(bool save)
        {
            ApplyQuality();
            ApplyFramePolicy();
            ApplyResolution();
            AudioListener.volume = _masterVolume;
            if (save) Save();
        }

        private static void ApplyQuality()
        {
            int qualityLevel = Mathf.Clamp(QualitySettings.names.Length - 1, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            if (_quality == QualityPreset.Performance) qualityLevel = 0;
            else if (_quality == QualityPreset.Balanced) qualityLevel = Mathf.Min(1, Mathf.Max(0, QualitySettings.names.Length - 1));
            else if (_quality == QualityPreset.Quality) qualityLevel = Mathf.Max(0, QualitySettings.names.Length - 1);
            else qualityLevel = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, Mathf.Max(0, QualitySettings.names.Length - 1));

            if (QualitySettings.names.Length > 0)
                QualitySettings.SetQualityLevel(qualityLevel, true);
        }

        private static void ApplyFramePolicy()
        {
            QualitySettings.vSyncCount = _vsync ? 1 : 0;
            Application.targetFrameRate = FrameLimits[_frameLimitIndex];
        }

        private static void ApplyResolution()
        {
            Resolution[] list = Screen.resolutions;
            FullScreenMode mode = _fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (list == null || list.Length == 0)
            {
                Screen.fullScreenMode = mode;
                return;
            }

            _resolutionIndex = Mathf.Clamp(_resolutionIndex, 0, list.Length - 1);
            Resolution resolution = list[_resolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, mode, resolution.refreshRate);
        }

        private static int FindClosestResolution(Resolution[] list, int width, int height)
        {
            int best = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < list.Length; i++)
            {
                int distance = Mathf.Abs(list[i].width - width) + Mathf.Abs(list[i].height - height);
                if (distance >= bestDistance) continue;
                best = i;
                bestDistance = distance;
            }
            return best;
        }

        private static void Save()
        {
            PlayerPrefs.SetInt(Prefix + "Quality", (int)_quality);
            PlayerPrefs.SetFloat(Prefix + "Volume", _masterVolume);
            PlayerPrefs.SetInt(Prefix + "Fullscreen", _fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "VSync", _vsync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "FrameLimit", _frameLimitIndex);
            PlayerPrefs.SetInt(Prefix + "Resolution", _resolutionIndex);
            PlayerPrefs.Save();
        }
    }
}
