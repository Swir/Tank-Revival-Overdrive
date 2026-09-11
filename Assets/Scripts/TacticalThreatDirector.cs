using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v1.8 COMBAT PRESENTATION & TACTICAL HUD
    /// Converts the crowded late-game battlefield into a readable tactical picture.
    /// Scores enemies by type, commander status and distance to Orzelek, exposes the
    /// highest-priority target and drives a dedicated Eagle-threat meter.
    /// </summary>
    public sealed class TacticalThreatDirector : MonoBehaviour
    {
        public static TacticalThreatDirector Instance { get; private set; }

        public int ThreatPercent { get; private set; }
        public int CriticalThreats { get; private set; }
        public EnemyTank PriorityTarget { get; private set; }
        public string ThreatLabel { get; private set; } = "CLEAR";
        public bool EagleUnderSiege => ThreatPercent >= 70;

        private TankGame _game;
        private float _nextScan;
        private float _nextAlarm;
        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _warning;
        private GUIStyle _critical;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TacticalThreatDirector>() != null) return;
            var go = new GameObject("TacticalThreatDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<TacticalThreatDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying)
            {
                ThreatPercent = 0;
                CriticalThreats = 0;
                PriorityTarget = null;
                ThreatLabel = "CLEAR";
                return;
            }

            if (Time.time < _nextScan) return;
            _nextScan = Time.time + 0.28f;
            RebuildThreatPicture();
        }

        private void RebuildThreatPicture()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            Vector2 eagle = _game.BasePosition;
            float total = 0f;
            float best = -1f;
            EnemyTank bestEnemy = null;
            int critical = 0;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;

                float distance = Vector2.Distance(enemy.transform.position, eagle);
                float score = KindWeight(enemy.Kind);
                score *= Mathf.Lerp(2.30f, 0.55f, Mathf.Clamp01(distance / 15f));

                if (enemy.GetComponent<WarCommander>() != null)
                    score *= 1.65f;

                ArmorSystem armor = enemy.GetComponent<ArmorSystem>();
                if (armor != null && armor.AverageIntegrity <= 35)
                    score *= 0.82f;

                if (distance <= 4.8f)
                {
                    score *= 1.55f;
                    critical++;
                }

                total += score;
                if (score > best)
                {
                    best = score;
                    bestEnemy = enemy;
                }
            }

            PriorityTarget = bestEnemy;
            CriticalThreats = critical;
            float roundPressure = Mathf.Lerp(1f, 0.78f, (_game.CurrentRound - 1f) / 99f);
            ThreatPercent = Mathf.Clamp(Mathf.RoundToInt(total * 3.4f * roundPressure), 0, 100);

            if (ThreatPercent >= 85) ThreatLabel = "EAGLE BREACH IMMINENT";
            else if (ThreatPercent >= 70) ThreatLabel = "HEAVY SIEGE";
            else if (ThreatPercent >= 45) ThreatLabel = "PRESSURE";
            else if (ThreatPercent >= 20) ThreatLabel = "CONTACT";
            else ThreatLabel = "CLEAR";

            if (ThreatPercent >= 85 && Time.time >= _nextAlarm)
            {
                _nextAlarm = Time.time + 6f;
                BattleAudio.PlayGlobal(SoundCue.EagleAlarm, 0.48f, 0.04f);
                VisualFactory.RingPulse(_game.BasePosition, new Color(1f, 0.08f, 0.03f, 0.82f), 1.45f);
            }
        }

        public static float KindWeight(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Boss: return 8.0f;
                case EnemyKind.Siege: return 5.7f;
                case EnemyKind.Elite: return 4.2f;
                case EnemyKind.Heavy: return 3.6f;
                case EnemyKind.Sniper: return 2.8f;
                case EnemyKind.Fast: return 2.3f;
                case EnemyKind.Supply: return 1.2f;
                default: return 1.5f;
            }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 0.90f, 1f) }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.80f, 0.87f, 0.92f) }
            };
            _warning = new GUIStyle(_title)
            {
                normal = { textColor = new Color(1f, 0.62f, 0.12f) }
            };
            _critical = new GUIStyle(_title)
            {
                normal = { textColor = new Color(1f, 0.16f, 0.10f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            const float width = 330f;
            float x = Mathf.Max(12f, Screen.width - width - 14f);
            float y = 96f;
            GUIStyle headline = ThreatPercent >= 70 ? _critical : ThreatPercent >= 45 ? _warning : _title;

            GUI.color = new Color(0.018f, 0.025f, 0.040f, 0.92f);
            GUI.Box(new Rect(x, y, width, 88f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 7f, width - 24f, 20f), "ORZELEK THREAT // " + ThreatLabel, headline);

            Rect bar = new Rect(x + 12f, y + 32f, width - 24f, 14f);
            GUI.color = new Color(0.08f, 0.10f, 0.14f, 1f);
            GUI.Box(bar, string.Empty);
            Color fill = ThreatPercent >= 70 ? new Color(1f, 0.12f, 0.06f) : ThreatPercent >= 45 ? new Color(1f, 0.55f, 0.10f) : new Color(0.16f, 0.76f, 0.92f);
            GUI.color = fill;
            GUI.Box(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * ThreatPercent / 100f, bar.height - 4f), string.Empty);
            GUI.color = Color.white;

            string priority = PriorityTarget != null ? PriorityTarget.Kind.ToString().ToUpperInvariant() : "NONE";
            GUI.Label(new Rect(x + 12f, y + 51f, width - 24f, 18f), $"THREAT {ThreatPercent}%   CLOSE {CriticalThreats}   PRIORITY {priority}", _body);
            GUI.Label(new Rect(x + 12f, y + 68f, width - 24f, 16f), "Red marker = destroy before it reaches the Eagle line.", _body);
        }
    }
}
