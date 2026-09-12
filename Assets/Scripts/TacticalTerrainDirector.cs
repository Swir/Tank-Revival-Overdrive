using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum TacticalTerrainKind
    {
        Clear,
        Mud,
        Rubble,
        Ice,
        Crater
    }

    /// <summary>
    /// Shared, allocation-light query surface used by vehicle movement. Fields register
    /// themselves and remain authoritative only for mobility; combat damage stays in
    /// Projectile/Health/Obstacle.
    /// </summary>
    public static class TacticalTerrainMap
    {
        private static readonly List<TacticalTerrainField> Fields = new List<TacticalTerrainField>();

        public static void Register(TacticalTerrainField field)
        {
            if (field != null && !Fields.Contains(field)) Fields.Add(field);
        }

        public static void Unregister(TacticalTerrainField field)
        {
            Fields.Remove(field);
        }

        public static float MobilityMultiplierAt(Vector2 position, Team team)
        {
            float multiplier = 1f;
            for (int i = Fields.Count - 1; i >= 0; i--)
            {
                TacticalTerrainField field = Fields[i];
                if (field == null)
                {
                    Fields.RemoveAt(i);
                    continue;
                }

                if (!field.Contains(position)) continue;
                multiplier = Mathf.Min(multiplier, field.GetMobilityMultiplier(team));
            }
            return Mathf.Clamp(multiplier, 0.42f, 1.18f);
        }

        public static TacticalTerrainKind TerrainAt(Vector2 position)
        {
            TacticalTerrainKind strongest = TacticalTerrainKind.Clear;
            float lowest = 1f;
            for (int i = Fields.Count - 1; i >= 0; i--)
            {
                TacticalTerrainField field = Fields[i];
                if (field == null)
                {
                    Fields.RemoveAt(i);
                    continue;
                }
                if (!field.Contains(position)) continue;
                float mobility = field.GetMobilityMultiplier(Team.Player);
                if (mobility <= lowest)
                {
                    lowest = mobility;
                    strongest = field.Kind;
                }
            }
            return strongest;
        }
    }

    public sealed class TacticalTerrainField : MonoBehaviour
    {
        public TacticalTerrainKind Kind { get; private set; }
        public float Radius { get; private set; }
        private float _mobility;
        private float _enemyBias;
        private float _expiresAt;

        public void Initialize(TacticalTerrainKind kind, float radius, float mobility, float duration = 0f, float enemyBias = 1f)
        {
            Kind = kind;
            Radius = Mathf.Max(0.25f, radius);
            _mobility = Mathf.Clamp(mobility, 0.42f, 1.18f);
            _enemyBias = Mathf.Clamp(enemyBias, 0.72f, 1.25f);
            _expiresAt = duration > 0f ? Time.time + duration : 0f;
            TacticalTerrainMap.Register(this);
        }

        public bool Contains(Vector2 point)
        {
            return ((Vector2)transform.position - point).sqrMagnitude <= Radius * Radius;
        }

        public float GetMobilityMultiplier(Team team)
        {
            return team == Team.Enemy ? Mathf.Clamp(_mobility * _enemyBias, 0.42f, 1.18f) : _mobility;
        }

        private void Update()
        {
            if (_expiresAt > 0f && Time.time >= _expiresAt) Destroy(gameObject);
        }

        private void OnEnable() { TacticalTerrainMap.Register(this); }
        private void OnDisable() { TacticalTerrainMap.Unregister(this); }
    }

    public sealed class TacticalMine : MonoBehaviour
    {
        private int _damage;
        private float _radius;
        private bool _armed;
        private float _armedAt;

        public void Initialize(int damage, float radius)
        {
            _damage = Mathf.Max(1, damage);
            _radius = Mathf.Clamp(radius, 0.55f, 1.35f);
            _armedAt = Time.time + 0.75f;
        }

        private void Update()
        {
            if (!_armed && Time.time >= _armedAt) _armed = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_armed) return;
            PlayerTank player = other.GetComponent<PlayerTank>();
            EnemyTank enemy = other.GetComponent<EnemyTank>();
            if (player == null && enemy == null) return;

            _armed = false;
            Team victimTeam = player != null ? Team.Player : Team.Enemy;
            Team source = victimTeam == Team.Player ? Team.Enemy : Team.Player;
            Vector3 center = transform.position;

            VisualFactory.Explosion(center, new Color(1f, 0.26f, 0.04f), 0.95f);
            VisualFactory.RingPulse(center, new Color(1f, 0.62f, 0.12f), _radius * 1.15f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.68f, 0.04f);

            Collider2D[] hits = Physics2D.OverlapCircleAll(center, _radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Health health = hits[i] != null ? hits[i].GetComponent<Health>() : null;
                if (health == null || health.Team != victimTeam) continue;
                health.Damage(_damage, source);
            }

            BattlefieldDestruction.AddImpactScar(center, _radius * 0.62f, new Color(1f, 0.22f, 0.04f));
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// v3.8 TACTICAL TERRAIN
    /// Rebuilds each round into a deterministic tactical layout of destructible cover,
    /// mobility terrain and mine lanes. Generated features avoid Orzelek's immediate
    /// footprint and use existing projectile/obstacle/health authority.
    /// </summary>
    public sealed class TacticalTerrainDirector : MonoBehaviour
    {
        private TankGame _game;
        private Transform _root;
        private int _round = -1;
        private int _coverAlive;
        private int _mineCount;
        private string _layoutName = "OPEN FIELD";
        private float _bannerUntil;
        private GUIStyle _header;
        private GUIStyle _body;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TacticalTerrainDirector>() != null) return;
            GameObject go = new GameObject("TacticalTerrainDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<TacticalTerrainDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying) return;
            if (_round != _game.CurrentRound) BuildRound(_game.CurrentRound);

            if (_root != null && Time.frameCount % 45 == 0)
            {
                Obstacle[] obstacles = _root.GetComponentsInChildren<Obstacle>();
                _coverAlive = obstacles != null ? obstacles.Length : 0;
            }
        }

        private void BuildRound(int round)
        {
            _round = round;
            if (_root != null) Destroy(_root.gameObject);
            _root = new GameObject("TacticalTerrain_R" + round.ToString("000")).transform;
            DontDestroyOnLoad(_root.gameObject);

            int sector = Mathf.Clamp((round - 1) / 10, 0, 9);
            System.Random rng = new System.Random(round * 1777 + sector * 419);
            int pattern = (round + sector * 3) % 5;
            _layoutName = pattern == 0 ? "BROKEN LINE" : pattern == 1 ? "MUD CORRIDORS" : pattern == 2 ? "RUBBLE POCKETS" : pattern == 3 ? "MINE BELT" : "ARMORED CROSSING";

            BuildTerrainFields(rng, sector, pattern);
            BuildCover(rng, round, sector, pattern);
            BuildMineLanes(rng, round, sector, pattern);
            _bannerUntil = Time.unscaledTime + 3.1f;
        }

        private void BuildTerrainFields(System.Random rng, int sector, int pattern)
        {
            int count = 2 + (sector >= 4 ? 1 : 0) + (pattern == 1 || pattern == 2 ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                Vector2 p = PickPoint(rng, 2.6f);
                float radius = Range(rng, 1.15f, 2.05f);
                TacticalTerrainKind kind;
                float mobility;
                Color tint;
                float enemyBias = 1f;

                if (sector == 5)
                {
                    kind = TacticalTerrainKind.Ice;
                    mobility = 0.86f;
                    tint = new Color(0.52f, 0.88f, 1f, 0.17f);
                    enemyBias = 0.96f;
                }
                else if (pattern == 2 || sector >= 6)
                {
                    kind = TacticalTerrainKind.Rubble;
                    mobility = 0.70f;
                    tint = new Color(0.50f, 0.44f, 0.36f, 0.20f);
                    enemyBias = 0.94f;
                }
                else
                {
                    kind = TacticalTerrainKind.Mud;
                    mobility = 0.62f;
                    tint = new Color(0.24f, 0.16f, 0.08f, 0.25f);
                    enemyBias = 0.92f;
                }

                GameObject field = new GameObject("Terrain_" + kind);
                field.transform.SetParent(_root, false);
                field.transform.position = new Vector3(p.x, p.y, 0f);
                VisualFactory.Disc("Ground", field.transform, Vector2.one * radius * 2f, tint, Vector3.zero, -52);
                VisualFactory.RingObject("Boundary", field.transform, Vector2.one * radius * 2.05f, new Color(tint.r, tint.g, tint.b, 0.32f), Vector3.zero, -51);
                field.AddComponent<TacticalTerrainField>().Initialize(kind, radius, mobility, 0f, enemyBias);
            }
        }

        private void BuildCover(System.Random rng, int round, int sector, int pattern)
        {
            int clusters = 2 + Mathf.Min(3, sector / 2) + (pattern == 0 || pattern == 4 ? 1 : 0);
            int hp = 2 + sector / 3;
            _coverAlive = 0;

            for (int c = 0; c < clusters; c++)
            {
                Vector2 anchor = PickPoint(rng, 2.8f);
                bool vertical = rng.NextDouble() > 0.5;
                int segments = 2 + rng.Next(0, 3);
                for (int s = 0; s < segments; s++)
                {
                    Vector2 offset = vertical ? new Vector2(0f, (s - (segments - 1) * 0.5f) * 0.72f) : new Vector2((s - (segments - 1) * 0.5f) * 0.72f, 0f);
                    GameObject cover = new GameObject("TacticalCover");
                    cover.transform.SetParent(_root, false);
                    cover.transform.position = anchor + offset;
                    Vector2 size = vertical ? new Vector2(0.48f, 0.66f) : new Vector2(0.66f, 0.48f);
                    VisualFactory.Rect("CoverShadow", cover.transform, size * 1.08f, new Color(0f, 0f, 0f, 0.38f), new Vector3(0.05f, -0.05f, 0f), 3);
                    VisualFactory.Rect("CoverBody", cover.transform, size, Color.Lerp(new Color(0.28f, 0.24f, 0.20f), new Color(0.45f, 0.30f, 0.18f), sector / 9f), Vector3.zero, 4);
                    BoxCollider2D collider = cover.AddComponent<BoxCollider2D>();
                    collider.size = size;
                    cover.AddComponent<Obstacle>().Initialize(ObstacleKind.Brick, hp);
                    _coverAlive++;
                }
            }
        }

        private void BuildMineLanes(System.Random rng, int round, int sector, int pattern)
        {
            _mineCount = 0;
            if (round < 8) return;
            int lanes = pattern == 3 ? 2 : sector >= 5 ? 1 : (round % 7 == 0 ? 1 : 0);
            for (int lane = 0; lane < lanes; lane++)
            {
                Vector2 anchor = PickPoint(rng, 3.1f);
                bool vertical = rng.NextDouble() > 0.5;
                int mines = 3 + Mathf.Min(3, sector / 3);
                for (int i = 0; i < mines; i++)
                {
                    Vector2 offset = vertical ? new Vector2(0f, (i - (mines - 1) * 0.5f) * 0.80f) : new Vector2((i - (mines - 1) * 0.5f) * 0.80f, 0f);
                    SpawnMine(anchor + offset, 1 + (round >= 65 ? 1 : 0));
                }
            }
        }

        private void SpawnMine(Vector2 position, int damage)
        {
            GameObject mine = new GameObject("TacticalMine");
            mine.transform.SetParent(_root, false);
            mine.transform.position = position;
            VisualFactory.Disc("Mine", mine.transform, new Vector2(0.28f, 0.28f), new Color(0.14f, 0.12f, 0.09f), Vector3.zero, 7);
            VisualFactory.RingObject("MineWarning", mine.transform, new Vector2(0.38f, 0.38f), new Color(1f, 0.26f, 0.05f, 0.52f), Vector3.zero, 6);
            CircleCollider2D trigger = mine.AddComponent<CircleCollider2D>();
            trigger.radius = 0.32f;
            trigger.isTrigger = true;
            mine.AddComponent<TacticalMine>().Initialize(damage, 0.90f);
            _mineCount++;
        }

        private Vector2 PickPoint(System.Random rng, float safeBase)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector2 p = new Vector2(Range(rng, -9.7f, 9.7f), Range(rng, -3.9f, 4.8f));
                if (_game != null && Vector2.Distance(p, _game.BasePosition) < safeBase) continue;
                if (_game != null && Vector2.Distance(p, _game.PlayerPosition) < 1.6f) continue;
                return p;
            }
            return new Vector2(Range(rng, -7f, 7f), 1.8f);
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.76f, 0.28f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.84f, 0.88f, 0.90f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            TacticalTerrainKind underPlayer = TacticalTerrainMap.TerrainAt(_game.PlayerPosition);
            float mobility = TacticalTerrainMap.MobilityMultiplierAt(_game.PlayerPosition, Team.Player);

            GUI.color = new Color(0.02f, 0.025f, 0.035f, 0.88f);
            GUI.Box(new Rect(14f, Screen.height - 94f, 292f, 78f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(26f, Screen.height - 88f, 270f, 20f), "TACTICAL TERRAIN // " + _layoutName, _header);
            GUI.Label(new Rect(26f, Screen.height - 66f, 270f, 18f), "Ground: " + underPlayer + "  Mobility: " + Mathf.RoundToInt(mobility * 100f) + "%", _body);
            GUI.Label(new Rect(26f, Screen.height - 48f, 270f, 18f), "Cover: " + _coverAlive + "  Mines deployed: " + _mineCount, _body);

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.02f, 0.025f, 0.035f, 0.91f);
                GUI.Box(new Rect(Screen.width * 0.5f - 250f, Screen.height * 0.32f - 24f, 500f, 48f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 235f, Screen.height * 0.32f - 13f, 470f, 24f), "TACTICAL LAYOUT // " + _layoutName, _header);
            }
        }
    }
}
