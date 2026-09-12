using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(500)]
    public sealed class TacticalTerrainMotor : MonoBehaviour
    {
        private Rigidbody2D _body;
        private Health _health;
        private Vector2 _lastPosition;
        private bool _primed;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _primed = false;
        }

        private void FixedUpdate()
        {
            if (_body == null || _health == null) return;

            Vector2 now = _body.position;
            if (!_primed)
            {
                _lastPosition = now;
                _primed = true;
                return;
            }

            float multiplier = TacticalTerrainMap.MobilityMultiplierAt(now, _health.Team);
            Vector2 delta = now - _lastPosition;
            if (multiplier < 0.999f && delta.sqrMagnitude > 0.000001f)
            {
                Vector2 corrected = _lastPosition + delta * multiplier;
                _body.position = corrected;
                now = corrected;
            }

            _lastPosition = now;
        }
    }

    public sealed class TacticalTerrainVehicleBinder : MonoBehaviour
    {
        private float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TacticalTerrainVehicleBinder>() != null) return;
            GameObject go = new GameObject("TacticalTerrainVehicleBinder");
            DontDestroyOnLoad(go);
            go.AddComponent<TacticalTerrainVehicleBinder>();
        }

        private void Update()
        {
            if (Time.time < _nextScan) return;
            _nextScan = Time.time + 0.75f;

            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player != null && player.GetComponent<TacticalTerrainMotor>() == null)
                player.gameObject.AddComponent<TacticalTerrainMotor>();

            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy != null && enemy.GetComponent<TacticalTerrainMotor>() == null)
                    enemy.gameObject.AddComponent<TacticalTerrainMotor>();
            }

            FriendlySupportUnit[] allies = FindObjectsByType<FriendlySupportUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < allies.Length; i++)
            {
                FriendlySupportUnit ally = allies[i];
                if (ally != null && ally.GetComponent<TacticalTerrainMotor>() == null)
                    ally.gameObject.AddComponent<TacticalTerrainMotor>();
            }
        }
    }
}
