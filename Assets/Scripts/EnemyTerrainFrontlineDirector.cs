using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public static class DynamicFrontlineMap
    {
        public struct NodeSnapshot
        {
            public Vector2 Position;
            public float Control;
            public bool Active;
        }

        private static readonly NodeSnapshot[] Nodes = new NodeSnapshot[3];
        public static int PlayerControlled { get; private set; }
        public static int EnemyControlled { get; private set; }
        public static float FrontlinePressure { get; private set; }

        public static void SetNode(int index, Vector2 position, float control, bool active)
        {
            if (index < 0 || index >= Nodes.Length) return;
            Nodes[index] = new NodeSnapshot { Position = position, Control = Mathf.Clamp(control, -1f, 1f), Active = active };
            Recalculate();
        }

        public static void Clear()
        {
            for (int i = 0; i < Nodes.Length; i++) Nodes[i] = default;
            PlayerControlled = 0;
            EnemyControlled = 0;
            FrontlinePressure = 0f;
        }

        public static Vector2 EnemyObjective(Vector2 from, EnemyKind kind, Vector2 player, Vector2 eagle)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < Nodes.Length; i++)
            {
                NodeSnapshot node = Nodes[i];
                if (!node.Active) continue;
                float distance = Vector2.Distance(from, node.Position);
                float ownership = node.Control;
                float score = -distance * 0.16f;

                if (kind == EnemyKind.Fast || kind == EnemyKind.Elite)
                    score += ownership > -0.15f ? 3.2f : 0.4f;
                else if (kind == EnemyKind.Heavy || kind == EnemyKind.Boss)
                    score += ownership > 0.15f ? 4.4f : ownership > -0.4f ? 1.6f : 0f;
                else if (kind == EnemyKind.Siege)
                    score += ownership > 0.35f ? 2.1f : -0.8f;
                else if (kind == EnemyKind.Sniper)
                    score += Mathf.Abs(ownership) < 0.7f ? 2.7f : 0.3f;
                else
                    score += ownership > -0.2f ? 2.3f : 0.5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            if (best >= 0)
            {
                Vector2 node = Nodes[best].Position;
                if (kind == EnemyKind.Sniper)
                {
                    Vector2 away = (from - node).sqrMagnitude > 0.1f ? (from - node).normalized : Vector2.up;
                    return node + away * 3.6f;
                }
                if (kind == EnemyKind.Siege && FrontlinePressure > 0.18f)
                    return Vector2.Lerp(node, eagle, 0.58f);
                return node;
            }

            return kind == EnemyKind.Siege ? eagle : player;
        }

        private static void Recalculate()
        {
            int player = 0;
            int enemy = 0;
            float sum = 0f;
            int active = 0;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (!Nodes[i].Active) continue;
                active++;
                sum += Nodes[i].Control;
                if (Nodes[i].Control >= 0.72f) player++;
                else if (Nodes[i].Control <= -0.72f) enemy++;
            }
            PlayerControlled = player;
            EnemyControlled = enemy;
            FrontlinePressure = active > 0 ? Mathf.Clamp(sum / active, -1f, 1f) : 0f;
        }
    }

    public static class EnemyTerrainIntelligence
    {
        public static Vector2 SteerDelta(EnemyTank enemy, Vector2 position, Vector2 intendedDelta, Vector2 player, Vector2 eagle)
        {
            if (enemy == null || intendedDelta.sqrMagnitude < 0.000001f) return intendedDelta;

            float magnitude = intendedDelta.magnitude;
            Vector2 current = intendedDelta / magnitude;
            Vector2 objective = DynamicFrontlineMap.EnemyObjective(position, enemy.Kind, player, eagle);
            Vector2 towardObjective = objective - position;
            if (towardObjective.sqrMagnitude > 0.04f) towardObjective.Normalize();
            else towardObjective = current;

            float objectiveWeight = enemy.Kind == EnemyKind.Fast || enemy.Kind == EnemyKind.Elite ? 0.52f :
                                    enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss ? 0.34f : 0.42f;
            Vector2 desired = Vector2.Lerp(current, towardObjective, objectiveWeight).normalized;

            Vector2 side = new Vector2(-desired.y, desired.x);
            float probe = enemy.Kind == EnemyKind.Fast ? 1.25f : enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege ? 0.95f : 1.08f;
            Vector2[] candidates =
            {
                desired,
                (desired + side * 0.72f).normalized,
                (desired - side * 0.72f).normalized,
                side,
                -side
            };

            float best = float.MinValue;
            Vector2 bestDir = desired;
            for (int i = 0; i < candidates.Length; i++)
            {
                Vector2 dir = candidates[i];
                Vector2 sample = position + dir * probe;
                float score = Score(enemy.Kind, position, sample, dir, towardObjective);
                if (score > best)
                {
                    best = score;
                    bestDir = dir;
                }
            }

            return bestDir * magnitude;
        }

        private static float Score(EnemyKind kind, Vector2 origin, Vector2 sample, Vector2 direction, Vector2 objectiveDirection)
        {
            float mobility = TacticalTerrainMap.MobilityMultiplierAt(sample, Team.Enemy);
            TacticalTerrainKind terrain = TacticalTerrainMap.TerrainAt(sample);
            float score = mobility * 5f + Vector2.Dot(direction, objectiveDirection) * 2.4f;

            if (terrain == TacticalTerrainKind.Mud)
                score -= kind == EnemyKind.Fast ? 3.4f : kind == EnemyKind.Heavy || kind == EnemyKind.Siege ? 1.2f : 2.0f;
            else if (terrain == TacticalTerrainKind.Rubble)
                score -= kind == EnemyKind.Fast ? 2.7f : kind == EnemyKind.Heavy || kind == EnemyKind.Boss ? 0.65f : 1.45f;
            else if (terrain == TacticalTerrainKind.Crater)
                score -= kind == EnemyKind.Heavy || kind == EnemyKind.Siege || kind == EnemyKind.Boss ? 1.0f : 2.5f;
            else if (terrain == TacticalTerrainKind.Ice)
                score -= kind == EnemyKind.Fast ? 0.4f : 0.9f;

            Collider2D[] nearby = Physics2D.OverlapCircleAll(sample, 0.48f);
            for (int i = 0; i < nearby.Length; i++)
            {
                Collider2D c = nearby[i];
                if (c == null) continue;
                TacticalMine mine = c.GetComponent<TacticalMine>();
                if (mine != null) score -= kind == EnemyKind.Fast || kind == EnemyKind.Elite ? 8.5f : 5.8f;
                Obstacle obstacle = c.GetComponent<Obstacle>();
                if (obstacle != null) score -= kind == EnemyKind.Siege || kind == EnemyKind.Heavy || kind == EnemyKind.Boss ? 1.5f : 4.5f;
            }

            return score;
        }
    }

    public sealed class EnemyTerrainFrontlineDirector : MonoBehaviour
    {
        private sealed class FrontNode
        {
            public GameObject Root;
            public Vector2 Position;
            public float Control;
            public float LastControl;
            public bool Rewarded;
        }

        private readonly List<FrontNode> _nodes = new List<FrontNode>(3);
        private TankGame _game;
        private int _round = -1;
        private float _nextTick;
        private GUIStyle _header;
        private GUIStyle _body;
        private float _bannerUntil;
        private string _banner = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EnemyTerrainFrontlineDirector>() != null) return;
            GameObject go = new GameObject("EnemyTerrainFrontlineDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<EnemyTerrainFrontlineDirector>();
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
                if (_round != -1) ResetRound();
                return;
            }

            if (_round != _game.CurrentRound) BuildRound(_game.CurrentRound);
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 0.22f;
            TickControl();
        }

        private void ResetRound()
        {
            _round = -1;
            for (int i = 0; i < _nodes.Count; i++)
                if (_nodes[i].Root != null) Destroy(_nodes[i].Root);
            _nodes.Clear();
            DynamicFrontlineMap.Clear();
        }

        private void BuildRound(int round)
        {
            ResetRound();
            _round = round;
            if (round < 4 || round % 10 == 0) return;

            int sector = Mathf.Clamp((round - 1) / 10, 0, 9);
            System.Random rng = new System.Random(39001 + round * 919 + sector * 73);
            float laneY = Mathf.Lerp(-1.0f, 2.2f, sector / 9f);
            for (int i = 0; i < 3; i++)
            {
                float x = i == 0 ? -6.1f : i == 1 ? 0f : 6.1f;
                x += ((float)rng.NextDouble() - 0.5f) * 1.1f;
                float y = laneY + ((float)rng.NextDouble() - 0.5f) * 2.2f;
                Vector2 pos = new Vector2(x, y);
                if (Vector2.Distance(pos, _game.BasePosition) < 2.7f) pos.y += 3f;
                SpawnNode(i, pos);
            }
            _banner = "DYNAMIC FRONTLINE // SECURE THE THREE CONTROL NODES";
            _bannerUntil = Time.unscaledTime + 3.2f;
        }

        private void SpawnNode(int index, Vector2 position)
        {
            GameObject root = new GameObject("FrontlineNode_" + (index + 1));
            root.transform.position = position;
            DontDestroyOnLoad(root);
            VisualFactory.Disc("Zone", root.transform, Vector2.one * 2.15f, new Color(0.16f, 0.35f, 0.48f, 0.16f), Vector3.zero, -25);
            VisualFactory.RingObject("Ring", root.transform, Vector2.one * 2.2f, new Color(0.34f, 0.78f, 1f, 0.55f), Vector3.zero, -24);
            VisualFactory.Rect("Beacon", root.transform, new Vector2(0.18f, 0.9f), new Color(0.72f, 0.86f, 1f), new Vector3(0f, 0f, 0f), 5);
            _nodes.Add(new FrontNode { Root = root, Position = position, Control = 0f, LastControl = 0f });
            DynamicFrontlineMap.SetNode(index, position, 0f, true);
        }

        private void TickControl()
        {
            PlayerTank player = CombatRoster.Player;
            EnemyTank[] enemies = CombatRoster.Enemies;
            if (enemies == null || enemies.Length == 0)
                enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);

            for (int n = 0; n < _nodes.Count; n++)
            {
                FrontNode node = _nodes[n];
                int friendly = 0;
                int hostile = 0;
                if (player != null && player.Health != null && !player.Health.IsDead && Vector2.Distance(player.transform.position, node.Position) <= 1.45f)
                    friendly += 2;

                FriendlySupportUnit[] support = FindObjectsByType<FriendlySupportUnit>(FindObjectsSortMode.None);
                for (int i = 0; i < support.Length; i++)
                    if (support[i] != null && Vector2.Distance(support[i].transform.position, node.Position) <= 1.45f) friendly++;

                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                    if (Vector2.Distance(enemy.transform.position, node.Position) > 1.45f) continue;
                    hostile += enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss ? 2 : 1;
                }

                node.LastControl = node.Control;
                float delta = Mathf.Clamp(friendly - hostile, -3, 3) * 0.035f;
                if (friendly == 0 && hostile == 0) delta = -Mathf.Sign(node.Control) * 0.006f;
                node.Control = Mathf.Clamp(node.Control + delta, -1f, 1f);
                DynamicFrontlineMap.SetNode(n, node.Position, node.Control, true);

                if (!node.Rewarded && node.Control >= 0.82f)
                {
                    node.Rewarded = true;
                    int reward = 2 + Mathf.Clamp(_round / 30, 0, 3);
                    WarEconomyDirector.AwardMissionBonds(reward, "FRONTLINE NODE SECURED");
                    _banner = $"NODE {n + 1} SECURED // +{reward} WAR BONDS";
                    _bannerUntil = Time.unscaledTime + 2.4f;
                    BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.28f, 0.08f);
                    VisualFactory.RingPulse(node.Position, new Color(0.20f, 0.92f, 1f), 1.8f);
                }
            }
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.30f, 0.86f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.86f, 0.91f, 0.95f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _nodes.Count == 0) return;
            EnsureStyles();
            float y = Screen.height - 178f;
            GUI.color = new Color(0.02f, 0.035f, 0.055f, 0.90f);
            GUI.Box(new Rect(Screen.width - 314f, y, 300f, 88f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - 300f, y + 7f, 278f, 20f), "DYNAMIC FRONTLINE", _header);
            GUI.Label(new Rect(Screen.width - 300f, y + 29f, 278f, 18f), $"CONTROL  BLUE {DynamicFrontlineMap.PlayerControlled}/3   RED {DynamicFrontlineMap.EnemyControlled}/3", _body);
            GUI.Label(new Rect(Screen.width - 300f, y + 48f, 278f, 30f), $"Pressure {DynamicFrontlineMap.FrontlinePressure:+0.00;-0.00;0.00} // enemy AI reroutes by terrain, mines and node ownership", _body);

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.01f, 0.04f, 0.07f, 0.94f);
                GUI.Box(new Rect(Screen.width * 0.5f - 330f, 112f, 660f, 42f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 318f, 121f, 636f, 24f), _banner, _header);
            }
        }
    }
}
