using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v1.8 COMBAT PRESENTATION
    /// Adds bounded world-space threat markers, enemy health bars and readable damage/module
    /// feedback without changing combat authority. Uses existing Health and ArmorSystem events.
    /// </summary>
    public sealed class CombatPresentationDirector : MonoBehaviour
    {
        private struct Callout
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Born;
            public float Life;
        }

        private TankGame _game;
        private Camera _camera;
        private readonly HashSet<Health> _observed = new HashSet<Health>();
        private readonly List<EnemyTank> _marked = new List<EnemyTank>(14);
        private readonly List<Callout> _callouts = new List<Callout>(32);
        private float _nextScan;
        private GUIStyle _marker;
        private GUIStyle _markerCritical;
        private GUIStyle _damage;
        private GUIStyle _critical;
        private GUIStyle _small;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CombatPresentationDirector>() != null) return;
            var go = new GameObject("CombatPresentationDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<CombatPresentationDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                _camera = Camera.main;
                return;
            }

            if (!_game.IsPlaying)
            {
                _marked.Clear();
                _callouts.Clear();
                return;
            }

            if (_camera == null) _camera = Camera.main;

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.38f;
                ScanBattlefield();
            }

            float now = Time.unscaledTime;
            for (int i = _callouts.Count - 1; i >= 0; i--)
            {
                if (now - _callouts[i].Born > _callouts[i].Life)
                    _callouts.RemoveAt(i);
            }
        }

        private void ScanBattlefield()
        {
            Health[] health = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < health.Length; i++)
            {
                Health h = health[i];
                if (h == null || _observed.Contains(h)) continue;
                _observed.Add(h);
                h.Damaged -= OnDamaged;
                h.Damaged += OnDamaged;
            }

            _observed.RemoveWhere(h => h == null);
            _marked.Clear();

            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            EnemyTank priority = TacticalThreatDirector.Instance != null ? TacticalThreatDirector.Instance.PriorityTarget : null;
            Vector2 eagle = _game.BasePosition;

            if (priority != null)
                _marked.Add(priority);

            for (int i = 0; i < enemies.Length && _marked.Count < 14; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy == priority || enemy.Health == null || enemy.Health.IsDead) continue;

                bool commander = enemy.GetComponent<WarCommander>() != null;
                bool major = enemy.Kind == EnemyKind.Boss || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Elite;
                bool close = Vector2.Distance(enemy.transform.position, eagle) <= 4.8f;
                if (commander || major || close)
                    _marked.Add(enemy);
            }
        }

        private void OnDamaged(Health health, int amount)
        {
            if (health == null || amount <= 0) return;

            ArmorSystem armor = health.GetComponent<ArmorSystem>();
            string text = "-" + amount;
            Color color = health.Team == Team.Player ? new Color(0.30f, 0.88f, 1f) : new Color(1f, 0.78f, 0.20f);
            float life = 0.78f;

            if (armor != null && armor.LastCritical)
            {
                text = $"CRIT {armor.LastDamagedModule.ToString().ToUpperInvariant()}  -{amount}";
                color = new Color(1f, 0.16f, 0.08f);
                life = 1.25f;
            }
            else if (armor != null && armor.LastZone == ArmorZone.Rear)
            {
                text = $"REAR -{amount}";
                color = new Color(1f, 0.42f, 0.10f);
                life = 0.95f;
            }

            if (health.gameObject.name.Contains("ORZELEK"))
            {
                text = $"ORZELEK HIT  -{amount}";
                color = new Color(1f, 0.08f, 0.05f);
                life = 1.40f;
            }

            if (_callouts.Count >= 30)
                _callouts.RemoveAt(0);

            _callouts.Add(new Callout
            {
                World = health.transform.position + Vector3.up * 0.62f,
                Text = text,
                Color = color,
                Born = Time.unscaledTime,
                Life = life
            });
        }

        private void EnsureStyles()
        {
            if (_marker != null) return;
            _marker = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.74f, 0.18f) }
            };
            _markerCritical = new GUIStyle(_marker)
            {
                fontSize = 11,
                normal = { textColor = new Color(1f, 0.12f, 0.08f) }
            };
            _damage = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _critical = new GUIStyle(_damage)
            {
                fontSize = 15,
                normal = { textColor = new Color(1f, 0.18f, 0.08f) }
            };
            _small = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                normal = { textColor = new Color(0.82f, 0.88f, 0.94f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _camera == null) return;
            EnsureStyles();

            DrawThreatMarkers();
            DrawCallouts();
        }

        private void DrawThreatMarkers()
        {
            EnemyTank priority = TacticalThreatDirector.Instance != null ? TacticalThreatDirector.Instance.PriorityTarget : null;

            for (int i = 0; i < _marked.Count; i++)
            {
                EnemyTank enemy = _marked[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;

                Vector3 screen = _camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * 0.82f);
                if (screen.z < 0f) continue;
                float x = screen.x;
                float y = Screen.height - screen.y;
                bool isPriority = enemy == priority;
                bool commander = enemy.GetComponent<WarCommander>() != null;
                string label = isPriority ? "▼ PRIORITY" : commander ? "◆ COMMANDER" : "• " + enemy.Kind.ToString().ToUpperInvariant();
                GUIStyle style = isPriority ? _markerCritical : _marker;

                GUI.Label(new Rect(x - 70f, y - 28f, 140f, 18f), label, style);

                float ratio = enemy.Health.Maximum > 0 ? Mathf.Clamp01(enemy.Health.Current / (float)enemy.Health.Maximum) : 0f;
                Rect bar = new Rect(x - 34f, y - 8f, 68f, 5f);
                GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.90f);
                GUI.Box(bar, string.Empty);
                GUI.color = isPriority ? new Color(1f, 0.12f, 0.07f) : new Color(1f, 0.58f, 0.12f);
                GUI.Box(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * ratio, bar.height - 2f), string.Empty);
                GUI.color = Color.white;

                ArmorSystem armor = enemy.GetComponent<ArmorSystem>();
                if (armor != null && armor.AverageIntegrity <= 40)
                    GUI.Label(new Rect(x - 62f, y + 1f, 124f, 15f), "MODULES " + armor.AverageIntegrity + "%", _small);
            }
        }

        private void DrawCallouts()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _callouts.Count; i++)
            {
                Callout c = _callouts[i];
                float t = Mathf.Clamp01((now - c.Born) / c.Life);
                Vector3 world = c.World + Vector3.up * (t * 0.55f);
                Vector3 screen = _camera.WorldToScreenPoint(world);
                if (screen.z < 0f) continue;

                Color previous = GUI.color;
                GUI.color = new Color(c.Color.r, c.Color.g, c.Color.b, 1f - t);
                GUIStyle style = c.Text.StartsWith("CRIT") || c.Text.StartsWith("ORZELEK") ? _critical : _damage;
                GUI.Label(new Rect(screen.x - 90f, Screen.height - screen.y - 12f, 180f, 24f), c.Text, style);
                GUI.color = previous;
            }
        }
    }
}
