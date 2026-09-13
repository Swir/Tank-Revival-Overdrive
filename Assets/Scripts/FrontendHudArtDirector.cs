using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(10000)]
    public sealed class FrontendHudArtDirector : MonoBehaviour
    {
        public const int AmmoChipCount = 7;
        public const int MaxStatusBars = 5;
        public const float HudTopHeight = 82f;
        public const float AmmoStripHeight = 42f;
        public const float CriticalTextHoldSeconds = 2.4f;
        public const float LegacyOcclusionAlpha = 0.995f;

        private TankGame _game;
        private Type _gameType;
        private FieldInfo _stateField;
        private FieldInfo _scoreField;
        private FieldInfo _highScoreField;
        private FieldInfo _livesField;
        private FieldInfo _eagleHpField;
        private FieldInfo _aliveEnemiesField;
        private FieldInfo _enemiesToSpawnField;
        private FieldInfo _bossPendingField;
        private FieldInfo _playerField;
        private FieldInfo _toastField;
        private FieldInfo _toastUntilField;
        private MethodInfo _startCampaignMethod;
        private MethodInfo _togglePauseMethod;

        private GUIStyle _brandStyle;
        private GUIStyle _heroStyle;
        private GUIStyle _subStyle;
        private GUIStyle _tinyStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _criticalStyle;
        private Texture2D _white;
        private float _pulse;

        public static FrontendHudArtDirector Instance { get; private set; }
        public static bool ConfigurationValid =>
            AmmoChipCount == AmmoDatabase.AmmoTypeCount &&
            MaxStatusBars == 5 &&
            HudTopHeight >= 68f && HudTopHeight <= 96f &&
            AmmoStripHeight >= 32f && AmmoStripHeight <= 52f &&
            CriticalTextHoldSeconds >= 1.5f && CriticalTextHoldSeconds <= 3.5f &&
            LegacyOcclusionAlpha >= 0.95f && LegacyOcclusionAlpha <= 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<FrontendHudArtDirector>() != null) return;
            var go = new GameObject("FrontendHudArtDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<FrontendHudArtDirector>();
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
            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
        }

        private void OnDestroy()
        {
            if (_white != null) Destroy(_white);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.5f);
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                CacheReflection();
            }
        }

        private void CacheReflection()
        {
            if (_game == null) return;
            _gameType = _game.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            _stateField = _gameType.GetField("_state", flags);
            _scoreField = _gameType.GetField("_score", flags);
            _highScoreField = _gameType.GetField("_highScore", flags);
            _livesField = _gameType.GetField("_lives", flags);
            _eagleHpField = _gameType.GetField("_eagleHp", flags);
            _aliveEnemiesField = _gameType.GetField("_aliveEnemies", flags);
            _enemiesToSpawnField = _gameType.GetField("_enemiesToSpawn", flags);
            _bossPendingField = _gameType.GetField("_bossPending", flags);
            _playerField = _gameType.GetField("_player", flags);
            _toastField = _gameType.GetField("_toast", flags);
            _toastUntilField = _gameType.GetField("_toastUntil", flags);
            _startCampaignMethod = _gameType.GetMethod("StartCampaign", flags);
            _togglePauseMethod = _gameType.GetMethod("TogglePause", flags);
        }

        private string StateName()
        {
            if (_stateField == null || _game == null) return "Menu";
            object state = _stateField.GetValue(_game);
            return state != null ? state.ToString() : "Menu";
        }

        private int ReadInt(FieldInfo field, int fallback = 0)
        {
            if (field == null || _game == null) return fallback;
            object value = field.GetValue(_game);
            return value is int ? (int)value : fallback;
        }

        private bool ReadBool(FieldInfo field)
        {
            if (field == null || _game == null) return false;
            object value = field.GetValue(_game);
            return value is bool && (bool)value;
        }

        private PlayerTank Player()
        {
            if (_playerField == null || _game == null) return FindAnyObjectByType<PlayerTank>();
            return _playerField.GetValue(_game) as PlayerTank;
        }

        private void StartCampaign()
        {
            _startCampaignMethod?.Invoke(_game, null);
        }

        private void TogglePause()
        {
            _togglePauseMethod?.Invoke(_game, null);
        }

        private void EnsureStyles()
        {
            if (_brandStyle != null) return;
            _brandStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.30f, 0.92f, 1f) }
            };
            _heroStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.66f, 0.78f, 0.86f) }
            };
            _tinyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.67f, 0.74f, 0.80f) }
            };
            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                fixedHeight = 46f
            };
            _criticalStyle = new GUIStyle(_valueStyle)
            {
                fontSize = 16,
                normal = { textColor = new Color(1f, 0.35f, 0.16f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null) return;
            EnsureStyles();
            string state = StateName();

            if (state == "Menu")
            {
                DrawFullScreenBackdrop();
                DrawMenu();
                return;
            }

            if (state == "Playing" || state == "Paused")
            {
                DrawCombatHud();
                if (state == "Paused") DrawPauseOverlay();
                return;
            }

            if (state == "GameOver" || state == "Victory")
            {
                DrawFullScreenBackdrop();
                DrawEndScreen(state == "Victory");
            }
        }

        private void DrawFullScreenBackdrop()
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.010f, 0.018f, 0.030f, LegacyOcclusionAlpha));
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.48f;
            Fill(new Rect(cx - 430f, cy - 235f, 860f, 470f), new Color(0.022f, 0.040f, 0.060f, 0.98f));
            Fill(new Rect(cx - 430f, cy - 235f, 5f, 470f), new Color(0.16f, 0.78f, 0.96f, 0.95f));
            Fill(new Rect(cx + 425f, cy - 235f, 5f, 470f), new Color(0.16f, 0.78f, 0.96f, 0.30f));
        }

        private void DrawMenu()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.48f;
            GUI.Label(new Rect(cx - 360f, cy - 178f, 720f, 60f), "TANK REVIVAL", _heroStyle);
            GUI.Label(new Rect(cx - 360f, cy - 123f, 720f, 32f), "ORZEŁ OVERDRIVE", _subStyle);
            GUI.Label(new Rect(cx - 260f, cy - 70f, 520f, 28f), "100 ROUNDS  •  ONE EAGLE  •  TOTAL WAR", _brandStyle);

            int high = ReadInt(_highScoreField);
            DrawStatCard(new Rect(cx - 275f, cy - 22f, 170f, 58f), "BEST", high.ToString("N0"), new Color(0.24f, 0.82f, 1f));
            DrawStatCard(new Rect(cx - 85f, cy - 22f, 170f, 58f), "CAMPAIGN", "1–100", new Color(0.48f, 0.94f, 0.58f));
            DrawStatCard(new Rect(cx + 105f, cy - 22f, 170f, 58f), "HUD", "F1", new Color(1f, 0.74f, 0.26f));

            if (GUI.Button(new Rect(cx - 170f, cy + 65f, 340f, 46f), "START CAMPAIGN", _buttonStyle)) StartCampaign();
            GUI.Label(new Rect(cx - 320f, cy + 126f, 640f, 26f), "WASD MOVE   •   MOUSE AIM   •   LMB/SPACE FIRE   •   Q/E AMMO", _tinyStyle);
            GUI.Label(new Rect(cx - 320f, cy + 151f, 640f, 26f), "F1 HUD   •   F2 COACHING   •   ESC/P PAUSE", _tinyStyle);
        }

        private void DrawCombatHud()
        {
            PlayerTank player = Player();
            int score = ReadInt(_scoreField);
            int lives = ReadInt(_livesField, 3);
            int eagle = ReadInt(_eagleHpField, 6);
            int remaining = ReadInt(_aliveEnemiesField) + ReadInt(_enemiesToSpawnField) + (ReadBool(_bossPendingField) ? 1 : 0);
            int armor = player != null && player.Health != null ? player.Health.Current : 0;
            int armorMax = player != null && player.Health != null ? Mathf.Max(player.Health.Max, 1) : 4;
            AmmoType active = player != null ? player.ActiveAmmo : AmmoType.Basic;

            // Opaque, compact header intentionally covers the old text-heavy TankGame HUD.
            Fill(new Rect(0f, 0f, Screen.width, HudTopHeight), new Color(0.010f, 0.020f, 0.032f, LegacyOcclusionAlpha));
            Fill(new Rect(0f, HudTopHeight - 2f, Screen.width, 2f), new Color(0.15f, 0.72f, 0.92f, 0.72f));

            float margin = 14f;
            float w = Mathf.Min(176f, (Screen.width - 5f * margin) / 4f);
            DrawMeterCard(new Rect(margin, 10f, w, 58f), "ARMOR", armor, armorMax, new Color(0.20f, 0.78f, 1f), "◆");
            DrawMeterCard(new Rect(margin * 2f + w, 10f, w, 58f), "ORZEŁEK", eagle, 6, eagle <= 2 ? new Color(1f, 0.28f, 0.14f) : new Color(0.34f, 0.94f, 0.48f), "★");
            DrawCountCard(new Rect(margin * 3f + w * 2f, 10f, w, 58f), "ROUND", _game.CurrentRound.ToString("000") + "/100", new Color(0.92f, 0.78f, 0.28f), "◈");
            DrawCountCard(new Rect(margin * 4f + w * 3f, 10f, w, 58f), "THREATS", remaining.ToString(), remaining > 12 ? new Color(1f, 0.38f, 0.18f) : new Color(0.76f, 0.82f, 0.88f), "▲");

            float rightWidth = 180f;
            if (Screen.width > 920f)
            {
                DrawCountCard(new Rect(Screen.width - rightWidth - 14f, 10f, rightWidth, 58f), "SCORE", score.ToString("N0"), new Color(0.36f, 0.90f, 1f), "✦");
            }

            DrawAmmoStrip(player, active);
            DrawLives(lives);
            DrawCriticalToast();
        }

        private void DrawAmmoStrip(PlayerTank player, AmmoType active)
        {
            float totalWidth = Mathf.Min(Screen.width - 28f, 760f);
            float left = (Screen.width - totalWidth) * 0.5f;
            float top = Screen.height - AmmoStripHeight - 10f;
            Fill(new Rect(left - 4f, top - 4f, totalWidth + 8f, AmmoStripHeight + 8f), new Color(0.008f, 0.015f, 0.025f, 0.94f));

            float gap = 4f;
            float chipW = (totalWidth - gap * (AmmoChipCount - 1)) / AmmoChipCount;
            for (int i = 0; i < AmmoChipCount; i++)
            {
                AmmoType ammo = (AmmoType)i;
                bool selected = ammo == active;
                int count = ammo == AmmoType.Basic ? -1 : player != null ? player.GetAmmoCount(ammo) : 0;
                Rect rect = new Rect(left + i * (chipW + gap), top, chipW, AmmoStripHeight);
                Color ammoColor = AmmoDatabase.Color(ammo);
                Fill(rect, selected ? new Color(ammoColor.r * 0.62f, ammoColor.g * 0.62f, ammoColor.b * 0.62f, 0.98f) : new Color(0.045f, 0.060f, 0.075f, 0.94f));
                Fill(new Rect(rect.x, rect.y, rect.width, selected ? 4f : 2f), selected ? ammoColor : new Color(ammoColor.r, ammoColor.g, ammoColor.b, 0.48f));

                string shortName = AmmoShort(ammo);
                string amount = ammo == AmmoType.Basic ? "∞" : count.ToString();
                GUI.Label(new Rect(rect.x, rect.y + 3f, rect.width, 18f), (i + 1) + "  " + shortName, _valueStyle);
                GUI.Label(new Rect(rect.x, rect.y + 20f, rect.width, 17f), amount, selected ? _valueStyle : _tinyStyle);
            }
        }

        private static string AmmoShort(AmmoType ammo)
        {
            switch (ammo)
            {
                case AmmoType.ArmorPiercing: return "AP";
                case AmmoType.Explosive: return "HE";
                case AmmoType.Incendiary: return "FIRE";
                case AmmoType.EMP: return "EMP";
                case AmmoType.Twin: return "TWIN";
                case AmmoType.Plasma: return "PLASMA";
                default: return "STD";
            }
        }

        private void DrawLives(int lives)
        {
            float y = Screen.height - AmmoStripHeight - 46f;
            float x = 16f;
            GUI.Label(new Rect(x, y, 60f, 22f), "RESERVE", _tinyStyle);
            for (int i = 0; i < 3; i++)
            {
                bool alive = i < lives;
                Color c = alive ? new Color(0.22f, 0.82f, 1f, 0.95f) : new Color(0.20f, 0.24f, 0.28f, 0.75f);
                Fill(new Rect(x + 62f + i * 18f, y + 6f, 12f, 12f), c);
            }
        }

        private void DrawCriticalToast()
        {
            if (_toastField == null || _toastUntilField == null || _game == null) return;
            object textValue = _toastField.GetValue(_game);
            object untilValue = _toastUntilField.GetValue(_game);
            string text = textValue as string;
            float until = untilValue is float ? (float)untilValue : 0f;
            if (string.IsNullOrWhiteSpace(text) || Time.unscaledTime >= until) return;

            string upper = text.ToUpperInvariant();
            bool critical = upper.Contains("WARNING") || upper.Contains("DESTROY") || upper.Contains("BOSS") || upper.Contains("FAILED") || upper.Contains("LOST");
            if (!critical) return;

            float width = Mathf.Min(520f, Screen.width - 80f);
            Rect r = new Rect((Screen.width - width) * 0.5f, HudTopHeight + 12f, width, 34f);
            Fill(r, new Color(0.16f, 0.035f, 0.018f, 0.90f));
            Fill(new Rect(r.x, r.y, 4f, r.height), new Color(1f, 0.30f + 0.18f * _pulse, 0.12f, 1f));
            GUI.Label(r, Compact(text, 54), _criticalStyle);
        }

        private void DrawPauseOverlay()
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.62f));
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Fill(new Rect(cx - 230f, cy - 110f, 460f, 220f), new Color(0.020f, 0.036f, 0.054f, 0.98f));
            GUI.Label(new Rect(cx - 200f, cy - 62f, 400f, 46f), "PAUSED", _heroStyle);
            GUI.Label(new Rect(cx - 180f, cy - 12f, 360f, 26f), "Battlefield state is frozen", _subStyle);
            if (GUI.Button(new Rect(cx - 135f, cy + 42f, 270f, 46f), "RESUME", _buttonStyle)) TogglePause();
        }

        private void DrawEndScreen(bool victory)
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.48f;
            GUI.Label(new Rect(cx - 370f, cy - 165f, 740f, 64f), victory ? "ORZEŁEK HOLDS" : "DEFENSE BROKEN", _heroStyle);
            GUI.Label(new Rect(cx - 330f, cy - 108f, 660f, 30f), victory ? "100 ROUNDS SURVIVED" : "THE WAR CONTINUES", _subStyle);

            int score = ReadInt(_scoreField);
            int high = ReadInt(_highScoreField);
            DrawStatCard(new Rect(cx - 185f, cy - 42f, 170f, 62f), "SCORE", score.ToString("N0"), new Color(0.28f, 0.84f, 1f));
            DrawStatCard(new Rect(cx + 15f, cy - 42f, 170f, 62f), "BEST", high.ToString("N0"), new Color(0.46f, 0.94f, 0.58f));
            if (GUI.Button(new Rect(cx - 160f, cy + 58f, 320f, 46f), "DEPLOY AGAIN", _buttonStyle)) StartCampaign();
        }

        private void DrawMeterCard(Rect rect, string label, int value, int max, Color accent, string icon)
        {
            Fill(rect, new Color(0.030f, 0.045f, 0.060f, 0.96f));
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, 24f, 22f), icon, _valueStyle);
            GUI.Label(new Rect(rect.x + 32f, rect.y + 3f, rect.width - 40f, 18f), label, _tinyStyle);
            float pct = max > 0 ? Mathf.Clamp01(value / (float)max) : 0f;
            Rect track = new Rect(rect.x + 10f, rect.y + 31f, rect.width - 20f, 13f);
            Fill(track, new Color(0.10f, 0.12f, 0.14f, 1f));
            Fill(new Rect(track.x, track.y, track.width * pct, track.height), accent);
            GUI.Label(new Rect(rect.x, rect.y + 42f, rect.width, 14f), value + "/" + max, _tinyStyle);
        }

        private void DrawCountCard(Rect rect, string label, string value, Color accent, string icon)
        {
            Fill(rect, new Color(0.030f, 0.045f, 0.060f, 0.96f));
            Fill(new Rect(rect.x, rect.y, 3f, rect.height), accent);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, 24f, 22f), icon, _valueStyle);
            GUI.Label(new Rect(rect.x + 34f, rect.y + 4f, rect.width - 42f, 18f), label, _tinyStyle);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 24f, rect.width - 16f, 28f), value, _valueStyle);
        }

        private void DrawStatCard(Rect rect, string label, string value, Color accent)
        {
            Fill(rect, new Color(0.040f, 0.060f, 0.080f, 0.96f));
            Fill(new Rect(rect.x, rect.y, rect.width, 3f), accent);
            GUI.Label(new Rect(rect.x, rect.y + 8f, rect.width, 18f), label, _tinyStyle);
            GUI.Label(new Rect(rect.x, rect.y + 25f, rect.width, 28f), value, _valueStyle);
        }

        private void Fill(Rect rect, Color color)
        {
            if (_white == null) return;
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = old;
        }

        private static string Compact(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
            return value.Substring(0, Mathf.Max(0, max - 3)) + "...";
        }
    }
}
