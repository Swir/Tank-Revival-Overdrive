using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(550)]
    public sealed class SiegeLogisticsFireControlDirector : MonoBehaviour
    {
        public const int EarliestRound = 30;
        public const int SupplyHealth = 6;
        public const int MaxSupplyNodes = 1;
        public const int MaxSpotters = 1;
        public const int MaxRelocationsPerBattery = 2;
        public const float SupplyPenaltySeconds = 3.0f;
        public const float SpotterPenaltySeconds = 2.5f;
        public const float RelocationCooldown = 7.0f;
        public const float SupplyMoveSpeed = 0.42f;

        private static SiegeLogisticsFireControlDirector _instance;
        private TankGame _game;
        private GameObject _supply;
        private Health _supplyHealth;
        private EnemyTank _spotter;
        private int _round = -1;
        private int[] _lastBatteryHp = new int[SiegeLineWarfareDirector.MaxBatteries];
        private int[] _relocations = new int[SiegeLineWarfareDirector.MaxBatteries];
        private float[] _nextRelocation = new float[SiegeLineWarfareDirector.MaxBatteries];
        private Vector2 _supplyDirection = Vector2.right;
        private bool _supplyLost;
        private bool _spotterLost;
        private string _status = string.Empty;
        private float _statusUntil;

        public static SiegeLogisticsFireControlDirector Instance => _instance;
        public bool SupplyAlive => _supply != null && _supplyHealth != null && !_supplyHealth.IsDead;
        public bool SpotterAlive => _spotter != null && _spotter.Health != null && !_spotter.Health.IsDead;
        public static bool ConfigurationValid => EarliestRound >= 24 && SupplyHealth >= 5 && MaxSupplyNodes == 1 && MaxSpotters == 1 && MaxRelocationsPerBattery <= 2 && SupplyPenaltySeconds <= 4f && SpotterPenaltySeconds <= 3f && RelocationCooldown >= 6f && SupplyMoveSpeed <= 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<SiegeLogisticsFireControlDirector>() != null) return;
            GameObject go = new GameObject("SiegeLogisticsFireControlDirector_v9_4");
            DontDestroyOnLoad(go);
            go.AddComponent<SiegeLogisticsFireControlDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetRun();
                return;
            }

            int current = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (current != _round) BeginRound(current);
            if (!IsLogisticsSiegeRound(current)) return;

            MoveSupply();
            RefreshSpotter();
            ApplyInterdictionConsequences();
            DetectCounterBatteryPressure();
        }

        public static bool IsLogisticsSiegeRound(int round)
        {
            return round >= EarliestRound && round <= 100 && SiegeLineWarfareDirector.IsSiegeRound(round) && round % 10 != 0;
        }

        public static int RelocationBudgetForRound(int round)
        {
            return round >= 70 ? 2 : 1;
        }

        public static float FireControlMultiplier(bool supplyAlive, bool spotterAlive)
        {
            if (supplyAlive && spotterAlive) return 1f;
            if (!supplyAlive && !spotterAlive) return 0.55f;
            return supplyAlive ? 0.78f : 0.68f;
        }

        private void BeginRound(int round)
        {
            CleanupSupply();
            _round = round;
            _spotter = null;
            _supplyLost = false;
            _spotterLost = false;
            for (int i = 0; i < _relocations.Length; i++)
            {
                _relocations[i] = 0;
                _nextRelocation[i] = Time.time + 6f + i;
                _lastBatteryHp[i] = -1;
            }
            if (!IsLogisticsSiegeRound(round)) return;
            BuildSupplyNode();
            RefreshSpotter();
            ShowStatus("SIEGE LOGISTICS ACTIVE // HUNT SUPPLY + SPOTTER");
        }

        private void BuildSupplyNode()
        {
            GameObject go = new GameObject("ENEMY_SIEGE_AMMO_CONVOY_V94");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector2(-4.4f, 3.2f);
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.0f, 0.62f);
            Health hp = go.AddComponent<Health>();
            hp.Initialize(Team.Enemy, SupplyHealth + (_round >= 75 ? 1 : 0));
            hp.Died += OnSupplyDestroyed;
            VisualFactory.Rect("Hull", go.transform, new Vector2(0.95f, 0.55f), new Color(0.38f, 0.22f, 0.08f), Vector3.zero, 8);
            VisualFactory.Rect("Ammo", go.transform, new Vector2(0.50f, 0.32f), new Color(0.95f, 0.55f, 0.08f), new Vector3(0f, 0.04f, 0f), 9);
            _supply = go;
            _supplyHealth = hp;
            _supplyDirection = Vector2.right;
        }

        private void MoveSupply()
        {
            if (!SupplyAlive) return;
            Vector3 pos = _supply.transform.position;
            pos += (Vector3)(_supplyDirection * SupplyMoveSpeed * Time.deltaTime);
            if (pos.x > 4.6f) { pos.x = 4.6f; _supplyDirection = Vector2.left; }
            else if (pos.x < -4.6f) { pos.x = -4.6f; _supplyDirection = Vector2.right; }
            _supply.transform.position = pos;
        }

        private void RefreshSpotter()
        {
            if (SpotterAlive) return;
            if (_spotter != null && !_spotterLost)
            {
                _spotterLost = true;
                ShowStatus("FIRE-CONTROL SPOTTER DOWN // ENEMY ACCURACY DEGRADED");
            }
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Kind == EnemyKind.Boss || enemy.Kind == EnemyKind.Supply) continue;
                    bool preferred = enemy.Kind == EnemyKind.Sniper || enemy.Kind == EnemyKind.Elite;
                    if ((pass == 0 && !preferred) || (pass == 1 && preferred)) continue;
                    _spotter = enemy;
                    _spotterLost = false;
                    VisualFactory.RingPulse(enemy.transform.position, new Color(1f, 0.34f, 0.10f), 0.7f);
                    return;
                }
            }
        }

        private void ApplyInterdictionConsequences()
        {
            SiegeLineWarfareDirector siege = SiegeLineWarfareDirector.Instance;
            if (siege == null || siege.ActiveBatteryCount <= 0) return;
            float penalty = 0f;
            if (!SupplyAlive) penalty += SupplyPenaltySeconds;
            if (!SpotterAlive) penalty += SpotterPenaltySeconds;
            if (penalty > 0f) siege.ApplyExternalFireDelay(penalty * Time.deltaTime);
        }

        private void DetectCounterBatteryPressure()
        {
            SiegeLineWarfareDirector siege = SiegeLineWarfareDirector.Instance;
            if (siege == null) return;
            for (int i = 0; i < SiegeLineWarfareDirector.MaxBatteries; i++)
            {
                if (!siege.TryGetBatteryState(i, out Vector2 pos, out int lane, out int hp)) { _lastBatteryHp[i] = -1; continue; }
                if (_lastBatteryHp[i] >= 0 && hp < _lastBatteryHp[i] && Time.time >= _nextRelocation[i] && _relocations[i] < RelocationBudgetForRound(_round))
                {
                    Vector2 shifted = pos + new Vector2((i == 0 ? 1f : -1f) * (1.35f + 0.25f * _relocations[i]), 0.15f * ((_relocations[i] & 1) == 0 ? 1f : -1f));
                    shifted.x = Mathf.Clamp(shifted.x, -4.8f, 4.8f);
                    if (siege.TryRelocateBattery(i, lane, shifted))
                    {
                        _relocations[i]++;
                        _nextRelocation[i] = Time.time + RelocationCooldown;
                        ShowStatus("ENEMY BATTERY DISPLACING // COUNTER-BATTERY CONTACT");
                    }
                }
                _lastBatteryHp[i] = hp;
            }
        }

        private void OnSupplyDestroyed(Health hp)
        {
            _supplyLost = true;
            ShowStatus("SIEGE AMMUNITION INTERDICTED // ENEMY FIRE RATE CUT");
        }

        private void CleanupSupply()
        {
            if (_supplyHealth != null) _supplyHealth.Died -= OnSupplyDestroyed;
            if (_supply != null) Destroy(_supply);
            _supply = null;
            _supplyHealth = null;
        }

        private void ResetRun()
        {
            CleanupSupply();
            _round = -1;
            _spotter = null;
            _status = string.Empty;
        }

        private void ShowStatus(string text)
        {
            _status = text;
            _statusUntil = Time.time + 3.1f;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_status) || Time.time > _statusUntil) return;
            GUI.Box(new Rect(Screen.width * 0.5f - 250f, 112f, 500f, 30f), _status);
        }
    }
}