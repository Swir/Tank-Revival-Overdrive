using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(555)]
    public sealed class FireMissionNetworkDirector : MonoBehaviour
    {
        public const int EarliestRound = 36;
        public const int MaxDecoys = 2;
        public const int MaxCounterSurveillanceRedeploys = 1;
        public const float BaseTargetLockSeconds = 5.5f;
        public const float DegradedEnemyLockBonusSeconds = 1.5f;
        public const float CounterSurveillancePenaltySeconds = 1.25f;
        public const float DecoyRedeployDelay = 4.5f;

        private static FireMissionNetworkDirector _instance;
        private TankGame _game;
        private readonly GameObject[] _decoys = new GameObject[MaxDecoys];
        private int _round = -1;
        private float _targetLockUntil;
        private bool _wasRevealed;
        private float _revealEndedAt;
        private int _redeploys;
        private int _wastedCounterBatteryCycles;
        private string _status = string.Empty;
        private float _statusUntil;

        public static FireMissionNetworkDirector Instance => _instance;
        public bool HasTargetLock => Time.time < _targetLockUntil;
        public float TargetLockRemaining => Mathf.Max(0f, _targetLockUntil - Time.time);
        public int ActiveDecoys => CountActiveDecoys();
        public int WastedCounterBatteryCycles => _wastedCounterBatteryCycles;
        public static bool ConfigurationValid => EarliestRound >= CommandNetworkHuntDirector.MinimumRelayRound && EarliestRound <= 50 && MaxDecoys == 2 && MaxCounterSurveillanceRedeploys == 1 && BaseTargetLockSeconds >= 4f && BaseTargetLockSeconds <= 8f && DegradedEnemyLockBonusSeconds <= 2f && CounterSurveillancePenaltySeconds <= 2f && DecoyRedeployDelay >= 3f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<FireMissionNetworkDirector>() != null) return;
            GameObject go = new GameObject("FireMissionNetworkDirector_v9_5");
            DontDestroyOnLoad(go);
            go.AddComponent<FireMissionNetworkDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            CleanupDecoys();
            if (_instance == this) _instance = null;
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
            if (!IsFireMissionRound(current)) return;

            CommandNetworkHuntDirector hunt = CommandNetworkHuntDirector.Instance;
            bool revealed = hunt != null && hunt.TargetsRevealed;
            if (revealed)
            {
                float duration = ResolveLockDuration();
                _targetLockUntil = Mathf.Max(_targetLockUntil, Time.time + duration);
                if (!_wasRevealed)
                {
                    _wasRevealed = true;
                    IdentifyDecoys();
                    ShowStatus("SIGINT FIRE MISSION // TRUE BATTERY LOCK");
                }
            }
            else if (_wasRevealed)
            {
                _wasRevealed = false;
                _revealEndedAt = Time.time;
            }

            TryCounterSurveillanceRedeploy();
        }

        public static bool IsFireMissionRound(int round)
        {
            return round >= EarliestRound && round <= 100 && SiegeLogisticsFireControlDirector.IsLogisticsSiegeRound(round) && round % 10 != 0;
        }

        public static int DecoyCountForRound(int round)
        {
            if (!IsFireMissionRound(round)) return 0;
            return round >= 70 ? 2 : 1;
        }

        public static float LockDuration(bool supplyAlive, bool spotterAlive)
        {
            float seconds = BaseTargetLockSeconds;
            if (!supplyAlive || !spotterAlive) seconds += DegradedEnemyLockBonusSeconds;
            if (spotterAlive) seconds -= CounterSurveillancePenaltySeconds;
            return Mathf.Clamp(seconds, 4f, 7f);
        }

        public bool TryConsumeDecoy(out Vector2 falseTarget)
        {
            falseTarget = Vector2.zero;
            if (!IsFireMissionRound(_round) || HasTargetLock) return false;
            for (int i = 0; i < _decoys.Length; i++)
            {
                if (_decoys[i] == null) continue;
                falseTarget = _decoys[i].transform.position;
                Destroy(_decoys[i]);
                _decoys[i] = null;
                _wastedCounterBatteryCycles++;
                VisualFactory.RingPulse(falseTarget, new Color(1f, 0.44f, 0.10f), 0.86f);
                ShowStatus("FALSE BATTERY HIT // COUNTER-BATTERY CYCLE WASTED");
                return true;
            }
            return false;
        }

        public bool CanEngageTrueBattery()
        {
            return !IsFireMissionRound(_round) || HasTargetLock;
        }

        public void NotifyBatteryRelocated()
        {
            if (!IsFireMissionRound(_round)) return;
            _targetLockUntil = Mathf.Min(_targetLockUntil, Time.time + 1.0f);
            ShowStatus("BATTERY DISPLACED // TARGET LOCK DECAYING");
        }

        private void BeginRound(int round)
        {
            CleanupDecoys();
            _round = round;
            _targetLockUntil = 0f;
            _wasRevealed = false;
            _revealEndedAt = 0f;
            _redeploys = 0;
            _wastedCounterBatteryCycles = 0;
            if (!IsFireMissionRound(round)) return;
            BuildDecoyScreen(DecoyCountForRound(round));
            ShowStatus("ENEMY DECEPTION NET // USE G SIGINT OR H RECON");
        }

        private float ResolveLockDuration()
        {
            SiegeLogisticsFireControlDirector logistics = SiegeLogisticsFireControlDirector.Instance;
            bool supply = logistics == null || logistics.SupplyAlive;
            bool spotter = logistics == null || logistics.SpotterAlive;
            return LockDuration(supply, spotter);
        }

        private void TryCounterSurveillanceRedeploy()
        {
            if (_wasRevealed || HasTargetLock || _redeploys >= MaxCounterSurveillanceRedeploys || CountActiveDecoys() > 0) return;
            SiegeLogisticsFireControlDirector logistics = SiegeLogisticsFireControlDirector.Instance;
            if (logistics == null || !logistics.SpotterAlive) return;
            if (_revealEndedAt <= 0f || Time.time < _revealEndedAt + DecoyRedeployDelay) return;

            _redeploys++;
            BuildDecoyScreen(1);
            ShowStatus("SPOTTER COUNTER-SURVEILLANCE // FALSE EMITTER REDEPLOYED");
        }

        private void IdentifyDecoys()
        {
            for (int i = 0; i < _decoys.Length; i++)
            {
                if (_decoys[i] == null) continue;
                Vector2 p = _decoys[i].transform.position;
                VisualFactory.RingPulse(p, new Color(0.18f, 0.92f, 1f), 0.72f);
                Destroy(_decoys[i]);
                _decoys[i] = null;
            }
        }

        private void BuildDecoyScreen(int count)
        {
            int created = 0;
            for (int i = 0; i < _decoys.Length && created < count; i++)
            {
                if (_decoys[i] != null) continue;
                int lane = (_round + i + _redeploys) % 3;
                Vector2 lanePos = DynamicFrontlineTerritoryDirector.LanePosition(lane);
                Vector2 pos = new Vector2(lanePos.x + (i == 0 ? 0.38f : -0.38f), 4.02f + i * 0.30f);
                GameObject go = new GameObject("ENEMY_DECOY_BATTERY_V95_" + i);
                go.transform.SetParent(transform, false);
                go.transform.position = pos;
                VisualFactory.Rect("DecoyBase", go.transform, new Vector2(1.05f, 0.60f), new Color(0.30f, 0.17f, 0.12f, 0.82f), Vector3.zero, 7);
                VisualFactory.Rect("FalseGun", go.transform, new Vector2(0.12f, 0.86f), new Color(0.74f, 0.30f, 0.12f, 0.85f), new Vector3(0f, -0.38f, 0f), 8);
                _decoys[i] = go;
                created++;
            }
        }

        private int CountActiveDecoys()
        {
            int count = 0;
            for (int i = 0; i < _decoys.Length; i++) if (_decoys[i] != null) count++;
            return count;
        }

        private void CleanupDecoys()
        {
            for (int i = 0; i < _decoys.Length; i++)
            {
                if (_decoys[i] != null) Destroy(_decoys[i]);
                _decoys[i] = null;
            }
        }

        private void ResetRun()
        {
            CleanupDecoys();
            _round = -1;
            _targetLockUntil = 0f;
            _wasRevealed = false;
            _revealEndedAt = 0f;
            _redeploys = 0;
            _wastedCounterBatteryCycles = 0;
            _status = string.Empty;
        }

        private void ShowStatus(string text)
        {
            _status = text;
            _statusUntil = Time.time + 3.0f;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_status) || Time.time > _statusUntil) return;
            GUI.Box(new Rect(Screen.width * 0.5f - 250f, 146f, 500f, 30f), _status);
        }
    }
}
