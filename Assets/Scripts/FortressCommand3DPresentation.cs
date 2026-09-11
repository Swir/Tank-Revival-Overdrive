using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.9 3D presentation for player-built fortress command assets. Presentation only;
    /// authoritative colliders/health/engineering logic stay on the existing 2D gameplay plane.
    /// </summary>
    public sealed class FortressCommand3DPresentation : MonoBehaviour
    {
        private float _nextScan;
        private Transform _commandBeacon;
        private int _lastSector = -1;
        private FortressDoctrine _lastDoctrine;
        private int _lastLevel = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<FortressCommand3DPresentation>() != null) return;
            var go = new GameObject("FortressCommand3DPresentation");
            DontDestroyOnLoad(go);
            go.AddComponent<FortressCommand3DPresentation>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.28f;
            ScanConstructs();
            RefreshCommandBeacon();
        }

        private void ScanConstructs()
        {
            Health[] health = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < health.Length; i++)
            {
                Health h = health[i];
                if (h == null || h.Team != Team.Player) continue;
                string n = h.gameObject.name;
                if (!IsCommandConstruct(n) || h.GetComponent<CommandConstruct3D>() != null) continue;
                var p = h.gameObject.AddComponent<CommandConstruct3D>();
                p.Initialize(n);
            }

            FortressRecoveryNode[] nodes = FindObjectsByType<FortressRecoveryNode>(FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
            {
                FortressRecoveryNode node = nodes[i];
                if (node == null || node.GetComponent<CommandConstruct3D>() != null) continue;
                var p = node.gameObject.AddComponent<CommandConstruct3D>();
                p.Initialize("FORTRESS_RECOVERY_NODE");
            }
        }

        private static bool IsCommandConstruct(string n)
        {
            return n.StartsWith("PLAYER_BARRICADE") || n.StartsWith("PLAYER_ANTI_SIEGE");
        }

        private void RefreshCommandBeacon()
        {
            EagleFortressCommandDirector command = EagleFortressCommandDirector.Instance;
            TankGame game = FindAnyObjectByType<TankGame>();
            if (command == null || game == null || !game.IsPlaying) return;

            if (_commandBeacon != null && _lastSector == command.Sector && _lastDoctrine == command.Doctrine && _lastLevel == command.DoctrineLevel)
                return;

            if (_commandBeacon != null) Destroy(_commandBeacon.gameObject);
            _lastSector = command.Sector;
            _lastDoctrine = command.Doctrine;
            _lastLevel = command.DoctrineLevel;

            var root = new GameObject("FortressCommandBeacon3D");
            root.transform.position = (Vector3)game.BasePosition + new Vector3(0f, -1.35f, 0f);
            _commandBeacon = root.transform;

            Color doctrine = DoctrineColor(command.Doctrine);
            Runtime3DFactory.Cylinder("BeaconBase3D", _commandBeacon, new Vector3(0f, 0f, -0.18f), 0.74f, 0.26f, new Color(0.08f, 0.15f, 0.20f), 0.72f, 0.38f);
            Runtime3DFactory.Cylinder("BeaconCore3D", _commandBeacon, new Vector3(0f, 0f, -0.37f), 0.36f, 0.18f, doctrine, 0.18f, 0.82f);
            Runtime3DFactory.Box("BeaconMast3D", _commandBeacon, new Vector3(0f, 0.12f, -0.66f), new Vector3(0.075f, 0.62f, 0.075f), doctrine, 0.54f, 0.58f);
            for (int i = 0; i < command.DoctrineLevel; i++)
            {
                float x = (i - (command.DoctrineLevel - 1) * 0.5f) * 0.16f;
                Runtime3DFactory.Box("DoctrineRank3D", _commandBeacon, new Vector3(x, -0.22f, -0.58f), new Vector3(0.08f, 0.20f, 0.06f), doctrine, 0.22f, 0.78f);
            }
            root.AddComponent<FortressBeaconPulse>();
        }

        private static Color DoctrineColor(FortressDoctrine doctrine)
        {
            if (doctrine == FortressDoctrine.HunterGrid) return new Color(0.26f, 0.90f, 1f);
            if (doctrine == FortressDoctrine.Recovery) return new Color(0.32f, 1f, 0.54f);
            return new Color(0.30f, 0.62f, 1f);
        }
    }

    public sealed class FortressBeaconPulse : MonoBehaviour
    {
        private Vector3 _baseScale;
        private void Awake() => _baseScale = transform.localScale;
        private void Update()
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 2.7f) * 0.025f;
            transform.localScale = _baseScale * pulse;
        }
    }

    public sealed class CommandConstruct3D : MonoBehaviour
    {
        private bool _ready;
        private Transform _turretHead;
        private Health _health;
        private GameObject _warning;

        public void Initialize(string constructName)
        {
            if (_ready) return;
            _ready = true;
            _health = GetComponent<Health>();
            Runtime3DFactory.HideLegacySprites(transform);

            if (constructName.StartsWith("PLAYER_ANTI_SIEGE")) BuildAntiSiege();
            else if (constructName.StartsWith("PLAYER_BARRICADE")) BuildBarricade();
            else BuildRecoveryNode();
        }

        private void BuildBarricade()
        {
            Runtime3DFactory.Box("BarricadeBase3D", transform, new Vector3(0f, 0f, -0.13f), new Vector3(1.78f, 0.54f, 0.48f), new Color(0.13f, 0.30f, 0.38f), 0.70f, 0.35f);
            Runtime3DFactory.Box("BarricadeTop3D", transform, new Vector3(0f, 0.12f, -0.40f), new Vector3(1.48f, 0.10f, 0.06f), new Color(0.34f, 0.92f, 1f), 0.38f, 0.72f);
            for (int i = -1; i <= 1; i += 2)
                Runtime3DFactory.Box("BarricadeBrace3D", transform, new Vector3(i * 0.65f, -0.18f, -0.36f), new Vector3(0.12f, 0.30f, 0.12f), new Color(0.30f, 0.36f, 0.40f), 0.76f, 0.32f);
            CreateWarning(new Vector3(0.65f, -0.16f, -0.48f));
        }

        private void BuildAntiSiege()
        {
            Runtime3DFactory.Cylinder("AntiSiegeBase3D", transform, new Vector3(0f, 0f, -0.18f), 0.80f, 0.32f, new Color(0.10f, 0.24f, 0.32f), 0.72f, 0.42f);
            var head = new GameObject("AntiSiegeHead3D");
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, -0.48f);
            _turretHead = head.transform;
            Runtime3DFactory.Cylinder("AntiSiegeTurret3D", _turretHead, Vector3.zero, 0.48f, 0.18f, new Color(0.22f, 0.68f, 0.78f), 0.62f, 0.56f);
            Runtime3DFactory.Box("AntiSiegeBarrel3D", _turretHead, new Vector3(0f, 0.52f, -0.02f), new Vector3(0.13f, 0.92f, 0.13f), new Color(0.48f, 0.94f, 1f), 0.66f, 0.54f);
            Runtime3DFactory.Box("AntiSiegeBrake3D", _turretHead, new Vector3(0f, 1.01f, -0.02f), new Vector3(0.22f, 0.12f, 0.17f), new Color(0.08f, 0.20f, 0.26f), 0.78f, 0.38f);
            CreateWarning(new Vector3(0.28f, -0.25f, -0.52f));
        }

        private void BuildRecoveryNode()
        {
            Color glow = new Color(0.34f, 1f, 0.54f);
            Runtime3DFactory.Cylinder("RecoveryBase3D", transform, new Vector3(0f, 0f, -0.13f), 0.58f, 0.24f, new Color(0.08f, 0.28f, 0.16f), 0.42f, 0.45f);
            Runtime3DFactory.Cylinder("RecoveryCore3D", transform, new Vector3(0f, 0f, -0.34f), 0.25f, 0.11f, glow, 0.12f, 0.84f);
            for (int i = -1; i <= 1; i += 2)
                Runtime3DFactory.Box("RecoveryAntenna3D", transform, new Vector3(i * 0.20f, 0.10f, -0.48f), new Vector3(0.045f, 0.42f, 0.045f), glow, 0.42f, 0.68f);
        }

        private void CreateWarning(Vector3 position)
        {
            _warning = Runtime3DFactory.Cylinder("ConstructWarning3D", transform, position, 0.11f, 0.04f, new Color(1f, 0.08f, 0.03f), 0.04f, 0.86f);
            _warning.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_turretHead != null)
            {
                EnemyTank target = FindPriorityTarget();
                if (target != null)
                {
                    Vector2 d = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                    float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
                    _turretHead.rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }

            if (_warning != null && _health != null && _health.Maximum > 0)
            {
                float ratio = _health.Current / (float)_health.Maximum;
                _warning.SetActive(ratio <= 0.40f);
                if (_warning.activeSelf)
                {
                    float p = 0.8f + Mathf.Sin(Time.unscaledTime * 9f) * 0.2f;
                    _warning.transform.localScale = Vector3.one * p;
                }
            }
        }

        private EnemyTank FindPriorityTarget()
        {
            EnemyTank[] enemies = CombatRoster.Enemies;
            EnemyTank best = null;
            float score = float.MinValue;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank e = enemies[i];
                if (e == null || e.Health == null || e.Health.IsDead) continue;
                float distance = Vector2.Distance(transform.position, e.transform.position);
                if (distance > 12f) continue;
                float s = -distance;
                if (e.Kind == EnemyKind.Boss) s += 14f;
                else if (e.Kind == EnemyKind.Siege) s += 10f;
                else if (e.Kind == EnemyKind.Heavy) s += 6f;
                if (s > score) { score = s; best = e; }
            }
            return best;
        }
    }
}
