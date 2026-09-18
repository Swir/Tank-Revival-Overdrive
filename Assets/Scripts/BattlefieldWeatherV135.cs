using UnityEngine;

namespace TankRevival
{
    public enum BattlefieldWeatherKindV135
    {
        Clear,
        Mist,
        Rain,
        Storm,
        Snow
    }

    public readonly struct BattlefieldWeatherPlanV135
    {
        public readonly int Round;
        public readonly BattlefieldWeatherKindV135 Kind;
        public readonly int Signature;
        public readonly float VisibilityScale;
        public readonly float TractionScale;
        public readonly float PlayerSpreadScale;
        public readonly float EnemySpreadScale;
        public readonly float EnemyReloadScale;
        public readonly float OverlayAlpha;
        public readonly int StreakBudget;

        public BattlefieldWeatherPlanV135(
            int round,
            BattlefieldWeatherKindV135 kind,
            int signature,
            float visibilityScale,
            float tractionScale,
            float playerSpreadScale,
            float enemySpreadScale,
            float enemyReloadScale,
            float overlayAlpha,
            int streakBudget)
        {
            Round = round;
            Kind = kind;
            Signature = signature;
            VisibilityScale = visibilityScale;
            TractionScale = tractionScale;
            PlayerSpreadScale = playerSpreadScale;
            EnemySpreadScale = enemySpreadScale;
            EnemyReloadScale = enemyReloadScale;
            OverlayAlpha = overlayAlpha;
            StreakBudget = streakBudget;
        }

        public string Label
        {
            get
            {
                switch (Kind)
                {
                    case BattlefieldWeatherKindV135.Mist: return "MIST FRONT";
                    case BattlefieldWeatherKindV135.Rain: return "RAIN FRONT";
                    case BattlefieldWeatherKindV135.Storm: return "STORM FRONT";
                    case BattlefieldWeatherKindV135.Snow: return "SNOW FRONT";
                    default: return "CLEAR SKIES";
                }
            }
        }
    }

    /// <summary>
    /// Pure deterministic v13.5 planner. It owns no round state and mutates no gameplay object.
    /// </summary>
    public static class BattlefieldWeatherPlannerV135
    {
        public const int PlannedRounds = 100;
        public const int ProfileCount = 5;
        public const int MaxPresentationStreaks = 24;
        public const float MinVisibilityScale = 0.58f;
        public const float MinTractionScale = 0.76f;
        public const float MaxPlayerSpreadScale = 1.12f;
        public const float MaxEnemySpreadScale = 1.30f;
        public const float MaxEnemyReloadScale = 1.10f;

        public static bool ConfigurationValid =>
            PlannedRounds == 100 &&
            ProfileCount == System.Enum.GetValues(typeof(BattlefieldWeatherKindV135)).Length &&
            MaxPresentationStreaks <= 24 &&
            MinVisibilityScale >= 0.55f &&
            MinTractionScale >= 0.72f &&
            MaxPlayerSpreadScale <= 1.12f &&
            MaxEnemySpreadScale <= 1.30f &&
            MaxEnemyReloadScale <= 1.10f;

        public static BattlefieldWeatherPlanV135 PlanForRound(int round, int terrainSignature)
        {
            round = Mathf.Clamp(round, 1, PlannedRounds);
            int sector = (round - 1) / 10;

            // The +2 sector rotation makes every decade transition distinct while
            // round-to-round movement guarantees that adjacent profiles cannot repeat.
            int index = (round + sector * 2 + 1) % ProfileCount;
            BattlefieldWeatherKindV135 kind = (BattlefieldWeatherKindV135)index;

            int hash = unchecked(round * 73856093 ^ terrainSignature * 19349663 ^ sector * 83492791 ^ 1350135);
            float jitter = (hash & 1023) / 1023f;
            float campaign = sector / 9f;
            float severity = Mathf.Clamp01(0.72f + campaign * 0.18f + jitter * 0.10f);

            float visibility;
            float traction;
            float playerSpread;
            float enemySpread;
            float enemyReload;
            float overlay;
            int streaks;

            switch (kind)
            {
                case BattlefieldWeatherKindV135.Mist:
                    visibility = Effect(0.76f, severity);
                    traction = Effect(0.98f, severity);
                    playerSpread = Effect(1.055f, severity);
                    enemySpread = Effect(1.14f, severity);
                    enemyReload = Effect(1.025f, severity);
                    overlay = Mathf.Lerp(0.035f, 0.065f, severity);
                    streaks = 0;
                    break;
                case BattlefieldWeatherKindV135.Rain:
                    visibility = Effect(0.84f, severity);
                    traction = Effect(0.90f, severity);
                    playerSpread = Effect(1.065f, severity);
                    enemySpread = Effect(1.16f, severity);
                    enemyReload = Effect(1.04f, severity);
                    overlay = Mathf.Lerp(0.025f, 0.055f, severity);
                    streaks = 14 + Mathf.RoundToInt(severity * 5f);
                    break;
                case BattlefieldWeatherKindV135.Storm:
                    visibility = Effect(0.58f, severity);
                    traction = Effect(0.83f, severity);
                    playerSpread = Effect(1.11f, severity);
                    enemySpread = Effect(1.28f, severity);
                    enemyReload = Effect(1.09f, severity);
                    overlay = Mathf.Lerp(0.055f, 0.095f, severity);
                    streaks = 18 + Mathf.RoundToInt(severity * 6f);
                    break;
                case BattlefieldWeatherKindV135.Snow:
                    visibility = Effect(0.70f, severity);
                    traction = Effect(0.86f, severity);
                    playerSpread = Effect(1.085f, severity);
                    enemySpread = Effect(1.20f, severity);
                    enemyReload = Effect(1.055f, severity);
                    overlay = Mathf.Lerp(0.035f, 0.075f, severity);
                    streaks = 12 + Mathf.RoundToInt(severity * 6f);
                    break;
                default:
                    visibility = 1f;
                    traction = 1f;
                    playerSpread = 1f;
                    enemySpread = 1f;
                    enemyReload = 1f;
                    overlay = 0f;
                    streaks = 0;
                    break;
            }

            visibility = Mathf.Clamp(visibility, MinVisibilityScale, 1f);
            traction = Mathf.Clamp(traction, MinTractionScale, 1f);
            playerSpread = Mathf.Clamp(playerSpread, 1f, MaxPlayerSpreadScale);
            enemySpread = Mathf.Clamp(enemySpread, 1f, MaxEnemySpreadScale);
            enemyReload = Mathf.Clamp(enemyReload, 1f, MaxEnemyReloadScale);
            streaks = Mathf.Clamp(streaks, 0, MaxPresentationStreaks);

            int signature = unchecked(hash * 31 + index * 1009 + Mathf.RoundToInt(severity * 1000f));
            return new BattlefieldWeatherPlanV135(
                round, kind, signature, visibility, traction,
                playerSpread, enemySpread, enemyReload, overlay, streaks);
        }

        private static float Effect(float severeValue, float severity)
        {
            return Mathf.Lerp(1f, severeValue, Mathf.Clamp01(severity));
        }
    }

    /// <summary>
    /// v13.5 runtime weather/visibility coordinator. The director only supplies bounded
    /// multipliers and presentation. PlayerTank, EnemyTank, Rigidbody2D, Projectile,
    /// Health and existing fire-control systems remain canonical authorities.
    /// </summary>
    public sealed class BattlefieldWeatherDirector : MonoBehaviour
    {
        public static BattlefieldWeatherDirector Instance { get; private set; }

        private TankGame _game;
        private int _round = -1;
        private BattlefieldWeatherPlanV135 _plan;
        private GUIStyle _header;
        private GUIStyle _body;

        public BattlefieldWeatherPlanV135 CurrentPlan => _plan;
        public BattlefieldWeatherKindV135 CurrentKind => _plan.Kind;
        public static bool ConfigurationValid => BattlefieldWeatherPlannerV135.ConfigurationValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static BattlefieldWeatherDirector EnsureInstalled()
        {
            if (Instance != null) return Instance;
            BattlefieldWeatherDirector existing = FindAnyObjectByType<BattlefieldWeatherDirector>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject go = new GameObject("BattlefieldWeatherDirector_v13_5");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<BattlefieldWeatherDirector>();
            Instance._plan = BattlefieldWeatherPlannerV135.PlanForRound(1, 0);
            return Instance;
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
            if (_plan.Round <= 0)
                _plan = BattlefieldWeatherPlannerV135.PlanForRound(1, 0);
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }
            if (!_game.IsPlaying) return;
            if (_round == _game.CurrentRound) return;

            int terrainSignature = unchecked(_game.CurrentRound * 7919 + 134);
            ApplyDeterministicPlan(_game.CurrentRound, terrainSignature);
        }

        public void ApplyDeterministicPlan(int round, int terrainSignature)
        {
            _round = Mathf.Clamp(round, 1, BattlefieldWeatherPlannerV135.PlannedRounds);
            _plan = BattlefieldWeatherPlannerV135.PlanForRound(_round, terrainSignature);
        }

        private static BattlefieldWeatherPlanV135 PlanOrClear()
        {
            return Instance != null && Instance._plan.Round > 0
                ? Instance._plan
                : BattlefieldWeatherPlannerV135.PlanForRound(1, 0);
        }

        public static float MobilityScale(Team team, Vector2 position)
        {
            BattlefieldWeatherPlanV135 plan = PlanOrClear();
            float scale = plan.TractionScale;
            TacticalTerrainKind terrain = TacticalTerrainMap.TerrainAt(position);

            if (plan.Kind == BattlefieldWeatherKindV135.Rain &&
                (terrain == TacticalTerrainKind.Mud || terrain == TacticalTerrainKind.Crater))
                scale *= 0.94f;
            else if (plan.Kind == BattlefieldWeatherKindV135.Snow && terrain == TacticalTerrainKind.Ice)
                scale *= 0.93f;
            else if (plan.Kind == BattlefieldWeatherKindV135.Storm && terrain == TacticalTerrainKind.Rubble)
                scale *= 0.97f;

            if (team == Team.Enemy)
                scale = Mathf.Lerp(1f, scale, 0.94f);

            return Mathf.Clamp(scale, 0.76f, 1.02f);
        }

        public static float PlayerSpreadScale(AmmoType ammo)
        {
            float scale = PlanOrClear().PlayerSpreadScale;
            if (ammo == AmmoType.ArmorPiercing || ammo == AmmoType.Plasma)
                scale = 1f + (scale - 1f) * 0.62f;
            return Mathf.Clamp(scale, 1f, BattlefieldWeatherPlannerV135.MaxPlayerSpreadScale);
        }

        public static float EnemySpreadScale(EnemyKind kind)
        {
            BattlefieldWeatherPlanV135 plan = PlanOrClear();
            float scale = plan.EnemySpreadScale;
            if (kind == EnemyKind.Sniper && plan.VisibilityScale < 0.82f)
                scale *= 1.04f;
            else if (kind == EnemyKind.Boss)
                scale = 1f + (scale - 1f) * 0.82f;
            return Mathf.Clamp(scale, 1f, BattlefieldWeatherPlannerV135.MaxEnemySpreadScale);
        }

        public static float EnemyReloadScale(EnemyKind kind)
        {
            float scale = PlanOrClear().EnemyReloadScale;
            if (kind == EnemyKind.Boss || kind == EnemyKind.Elite)
                scale = 1f + (scale - 1f) * 0.72f;
            return Mathf.Clamp(scale, 1f, BattlefieldWeatherPlannerV135.MaxEnemyReloadScale);
        }

        public static string CurrentTelemetry
        {
            get
            {
                BattlefieldWeatherPlanV135 p = PlanOrClear();
                return p.Label + "  VIS " + Mathf.RoundToInt(p.VisibilityScale * 100f) + "%  TRACTION " +
                    Mathf.RoundToInt(p.TractionScale * 100f) + "%";
            }
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.38f, 0.90f, 1f) }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.80f, 0.90f, 0.96f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _plan.Round <= 0) return;
            if (Event.current.type == EventType.Repaint)
                DrawWeatherLayer();

            EnsureStyles();
            float x = Mathf.Max(12f, Screen.width - 292f);
            GUI.color = new Color(0.02f, 0.04f, 0.065f, 0.88f);
            GUI.Box(new Rect(x, 84f, 276f, 61f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 10f, 90f, 250f, 18f), "WEATHER // " + _plan.Label, _header);
            GUI.Label(new Rect(x + 10f, 111f, 252f, 16f),
                "Visibility " + Mathf.RoundToInt(_plan.VisibilityScale * 100f) + "%  ·  Traction " +
                Mathf.RoundToInt(_plan.TractionScale * 100f) + "%", _body);
            GUI.Label(new Rect(x + 10f, 127f, 252f, 16f),
                "Precision ammo reduces weather spread penalty", _body);
        }

        private void DrawWeatherLayer()
        {
            if (_plan.Kind == BattlefieldWeatherKindV135.Clear || _plan.OverlayAlpha <= 0f) return;

            Color tint;
            switch (_plan.Kind)
            {
                case BattlefieldWeatherKindV135.Mist: tint = new Color(0.70f, 0.82f, 0.86f, _plan.OverlayAlpha); break;
                case BattlefieldWeatherKindV135.Rain: tint = new Color(0.12f, 0.28f, 0.38f, _plan.OverlayAlpha); break;
                case BattlefieldWeatherKindV135.Storm: tint = new Color(0.07f, 0.12f, 0.20f, _plan.OverlayAlpha); break;
                case BattlefieldWeatherKindV135.Snow: tint = new Color(0.84f, 0.94f, 1f, _plan.OverlayAlpha); break;
                default: tint = Color.clear; break;
            }

            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            int count = Mathf.Min(_plan.StreakBudget, BattlefieldWeatherPlannerV135.MaxPresentationStreaks);
            if (count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                int seed = unchecked(_plan.Signature + i * 1013);
                float nx = ((seed & 2047) / 2047f);
                float ny = (((seed >> 5) & 2047) / 2047f);
                float speed = _plan.Kind == BattlefieldWeatherKindV135.Snow ? 42f + (i % 5) * 7f : 150f + (i % 6) * 18f;
                float x = Mathf.Repeat(nx * Screen.width + Time.unscaledTime * ((i % 3) - 1) * 5f, Mathf.Max(1f, Screen.width));
                float y = Mathf.Repeat(ny * Screen.height + Time.unscaledTime * speed, Mathf.Max(1f, Screen.height));

                bool snow = _plan.Kind == BattlefieldWeatherKindV135.Snow;
                Rect mark = snow
                    ? new Rect(x, y, 3f + (i % 2), 3f + (i % 2))
                    : new Rect(x, y, 1f + (i % 2), _plan.Kind == BattlefieldWeatherKindV135.Storm ? 22f : 15f);
                GUI.color = snow
                    ? new Color(0.90f, 0.97f, 1f, 0.54f)
                    : new Color(0.58f, 0.82f, 0.96f, _plan.Kind == BattlefieldWeatherKindV135.Storm ? 0.42f : 0.30f);
                GUI.DrawTexture(mark, Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }
    }
}
