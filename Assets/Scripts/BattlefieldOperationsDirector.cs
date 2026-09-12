using UnityEngine;

namespace TankRevival
{
    public enum BattlefieldOperationStage
    {
        None,
        SecureLandingZone,
        EscortColumn,
        Breakthrough,
        Complete,
        Failed
    }

    /// <summary>
    /// v3.7 multi-stage operation layer. Selected non-boss rounds become compact combined-arms
    /// operations: secure an LZ, receive allied armor, keep the column alive and finish a combat
    /// breakthrough. It layers over v3.6 directives without replacing TankGame round-clear rules.
    /// </summary>
    [DefaultExecutionOrder(3360)]
    public sealed class BattlefieldOperationsDirector : MonoBehaviour
    {
        private TankGame _game;
        private int _round;
        private BattlefieldOperationStage _stage;
        private bool _operationActive;
        private Vector2 _lzPosition;
        private float _stageStartedAt;
        private float _secureProgress;
        private int _breakthroughKills;
        private int _breakthroughTarget;
        private bool _emergencySupportUsed;
        private GameObject _lzMarker;
        private GameObject _supplyCache;
        private FriendlySupportUnit _guardian;
        private FriendlySupportUnit _medic;
        private FriendlySupportUnit _hunter;
        private string _headline = "NO ACTIVE OPERATION";
        private string _detail = string.Empty;
        private string _reward = string.Empty;
        private GUIStyle _titleStyle, _bodyStyle, _goodStyle, _warnStyle;

        public static BattlefieldOperationsDirector Instance { get; private set; }
        public static string CurrentOperation => Instance != null ? Instance._headline : "NO ACTIVE OPERATION";
        public static BattlefieldOperationStage CurrentStage => Instance != null ? Instance._stage : BattlefieldOperationStage.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<BattlefieldOperationsDirector>() != null) return;
            var go = new GameObject("BattlefieldOperationsDirector_v3_7");
            DontDestroyOnLoad(go);
            go.AddComponent<BattlefieldOperationsDirector>();
        }

        private void Awake()
        {
            Instance = this;
            Projectile.DamageResolved += OnDamageResolved;
        }

        private void OnDestroy()
        {
            Projectile.DamageResolved -= OnDamageResolved;
            if (Instance == this) Instance = null;
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
                ResetOperation();
                return;
            }

            if (_round != _game.CurrentRound)
                BeginRound(_game.CurrentRound);

            if (!_operationActive)
            {
                UpdateEmergencySupport();
                return;
            }

            switch (_stage)
            {
                case BattlefieldOperationStage.SecureLandingZone: UpdateLandingZone(); break;
                case BattlefieldOperationStage.EscortColumn: UpdateEscort(); break;
                case BattlefieldOperationStage.Breakthrough: UpdateBreakthrough(); break;
            }

            UpdateSupplyCache();
            UpdateEmergencySupport();
        }

        private void BeginRound(int round)
        {
            ClearRuntimeObjects();
            _round = round;
            _emergencySupportUsed = false;
            _secureProgress = 0f;
            _breakthroughKills = 0;
            _reward = string.Empty;

            // Major combined-arms operations recur often enough to matter but do not replace
            // v3.6 objectives on every round. Boss rounds stay dedicated Legend engagements.
            _operationActive = round % 10 != 0 && (round % 5 == 2 || round % 7 == 4);
            if (!_operationActive)
            {
                _stage = BattlefieldOperationStage.None;
                _headline = "BATTLE NET // standard combat directive";
                _detail = "Dynamic support remains available under critical pressure.";
                return;
            }

            _stage = BattlefieldOperationStage.SecureLandingZone;
            _stageStartedAt = Time.time;
            float side = ((round / 3) % 2 == 0) ? -1f : 1f;
            _lzPosition = new Vector2(side * (5.2f + (round % 2) * 1.1f), 1.0f + ((round % 3) - 1) * 1.2f);
            SpawnLandingZoneMarker();
            _headline = $"OPERATION IRON WING // ROUND {round:000}";
            _detail = "PHASE 1/3 // secure landing zone for allied armor";
        }

        private void UpdateLandingZone()
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null) return;

            float distance = Vector2.Distance(player.transform.position, _lzPosition);
            if (distance <= 1.75f)
            {
                _secureProgress += Time.deltaTime;
                if (Time.frameCount % 30 == 0)
                    VisualFactory.RingPulse(_lzPosition, new Color(0.18f, 0.82f, 1f), 0.74f);
            }
            else
            {
                _secureProgress = Mathf.Max(0f, _secureProgress - Time.deltaTime * 0.22f);
            }

            _detail = $"PHASE 1/3 // LZ secure {_secureProgress:0.0}/6.0s // range {distance:0.0}m";
            if (_secureProgress >= 6f)
            {
                SpawnAlliedColumn();
                _stage = BattlefieldOperationStage.EscortColumn;
                _stageStartedAt = Time.time;
                _detail = "PHASE 2/3 // keep allied column combat-effective for 16s";
                if (_lzMarker != null) Destroy(_lzMarker);
                BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.42f, 0.08f);
            }
        }

        private void UpdateEscort()
        {
            int alive = AlliedAliveCount();
            if (alive <= 0)
            {
                FailOperation("ALLIED COLUMN DESTROYED");
                return;
            }

            float elapsed = Time.time - _stageStartedAt;
            int requiredAlive = _round >= 55 ? 2 : 1;
            _detail = $"PHASE 2/3 // escort {elapsed:0}/{16}s // allied armor {alive}/3 // need {requiredAlive}";

            if (elapsed >= 16f && alive >= requiredAlive)
            {
                _stage = BattlefieldOperationStage.Breakthrough;
                _stageStartedAt = Time.time;
                _breakthroughKills = 0;
                _breakthroughTarget = Mathf.Clamp(3 + _round / 28, 3, 6);
                _detail = $"PHASE 3/3 // combined-arms breakthrough 0/{_breakthroughTarget}";
                SpawnSupplyCache(false);
            }
            else if (elapsed >= 22f && alive < requiredAlive)
            {
                FailOperation("COLUMN TOO DAMAGED TO ADVANCE");
            }
        }

        private void UpdateBreakthrough()
        {
            if (AlliedAliveCount() <= 0)
            {
                FailOperation("BREAKTHROUGH FORCE LOST");
                return;
            }

            _detail = $"PHASE 3/3 // destroy priority hostiles {_breakthroughKills}/{_breakthroughTarget} // allies {AlliedAliveCount()}/3";
            if (_breakthroughKills >= _breakthroughTarget)
                CompleteOperation();
        }

        private void OnDamageResolved(Projectile projectile, Health target, int damage, bool killed)
        {
            if (!_operationActive || _stage != BattlefieldOperationStage.Breakthrough || !killed) return;
            if (projectile == null || projectile.OwnerTeam != Team.Player || target == null || target.Team != Team.Enemy) return;
            _breakthroughKills++;
            if (_breakthroughKills <= _breakthroughTarget)
                VisualFactory.RingPulse(target.transform.position, new Color(0.28f, 0.78f, 1f), 0.52f);
        }

        private void CompleteOperation()
        {
            _stage = BattlefieldOperationStage.Complete;
            _operationActive = false;
            int survivors = AlliedAliveCount();
            int payout = 8 + _round / 20 + survivors * 2;
            WarEconomyDirector.AwardMissionBonds(payout, "OPERATION IRON WING");
            _headline = "OPERATION IRON WING // COMPLETE";
            _detail = $"Combined-arms breakthrough secured // allied survivors {survivors}/3";
            _reward = $"+{payout} WAR BONDS // field resupply delivered";
            SpawnSupplyCache(true);

            PlayerTank player = CombatRoster.Player;
            if (player != null)
            {
                player.AddAmmo(AmmoType.ArmorPiercing, 3 + survivors);
                player.AddAmmo(AmmoType.Explosive, 2);
                if (player.Health != null) player.Health.Heal(1 + survivors / 2);
                player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 1.25f);
            }

            Health eagle = CombatRoster.Eagle;
            if (eagle != null && !eagle.IsDead)
            {
                eagle.Heal(1);
                eagle.InvulnerableUntil = Mathf.Max(eagle.InvulnerableUntil, Time.time + 1.2f);
            }

            VisualFactory.RingPulse(_game.BasePosition, new Color(0.22f, 0.95f, 0.60f), 1.28f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.68f, 0f);
        }

        private void FailOperation(string reason)
        {
            _stage = BattlefieldOperationStage.Failed;
            _operationActive = false;
            _headline = "OPERATION IRON WING // ABORTED";
            _detail = reason + " // round continues under standard combat rules";
            _reward = "No operation bonus";
            if (_lzMarker != null) Destroy(_lzMarker);
        }

        private void UpdateEmergencySupport()
        {
            if (_emergencySupportUsed || _game == null || !_game.IsPlaying || _round <= 2) return;
            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;
            bool playerCritical = player != null && player.Health != null && !player.Health.IsDead && player.Health.Current <= Mathf.Max(1, player.Health.Maximum / 3);
            bool eagleCritical = eagle != null && !eagle.IsDead && eagle.Current <= 2;
            if (!playerCritical && !eagleCritical) return;

            _emergencySupportUsed = true;
            Vector2 spawn = eagleCritical ? _game.BasePosition + new Vector2(2.2f, 1.0f) : (Vector2)player.transform.position + new Vector2(-2.0f, 1.0f);
            FriendlySupportRole role = eagleCritical ? FriendlySupportRole.Guardian : FriendlySupportRole.Medic;
            FriendlySupportUnit unit = SpawnSupport(role, spawn);
            if (_guardian == null && role == FriendlySupportRole.Guardian) _guardian = unit;
            if (_medic == null && role == FriendlySupportRole.Medic) _medic = unit;
            SpawnSupplyCache(false);
            VisualFactory.RingPulse(spawn, new Color(0.18f, 0.78f, 1f), 1.15f);
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.34f, 0.10f);
            _reward = "EMERGENCY SUPPORT DEPLOYED";
        }

        private void SpawnAlliedColumn()
        {
            Vector2 center = _lzPosition;
            _guardian = SpawnSupport(FriendlySupportRole.Guardian, center + new Vector2(-0.95f, 0f));
            _medic = SpawnSupport(FriendlySupportRole.Medic, center + new Vector2(0.95f, -0.25f));
            _hunter = SpawnSupport(FriendlySupportRole.Hunter, center + new Vector2(0f, 1.05f));
            SpawnSupplyCache(false);
            VisualFactory.RingPulse(center, new Color(0.18f, 0.84f, 1f), 1.3f);
        }

        private FriendlySupportUnit SpawnSupport(FriendlySupportRole role, Vector2 position)
        {
            var go = new GameObject("ALLY_" + role.ToString().ToUpperInvariant());
            var unit = go.AddComponent<FriendlySupportUnit>();
            unit.Initialize(_game, role, position);
            return unit;
        }

        private int AlliedAliveCount()
        {
            int count = 0;
            if (_guardian != null && _guardian.IsAlive) count++;
            if (_medic != null && _medic.IsAlive) count++;
            if (_hunter != null && _hunter.IsAlive) count++;
            return count;
        }

        private void SpawnLandingZoneMarker()
        {
            if (_lzMarker != null) Destroy(_lzMarker);
            _lzMarker = new GameObject("IRON_WING_LZ");
            _lzMarker.transform.position = _lzPosition;
            VisualFactory.Disc("LZFill", _lzMarker.transform, new Vector2(2.8f, 2.8f), new Color(0.10f, 0.55f, 0.85f, 0.12f), Vector3.zero, 4);
            VisualFactory.RingObject("LZRing", _lzMarker.transform, new Vector2(3.1f, 3.1f), new Color(0.18f, 0.82f, 1f, 0.76f), Vector3.zero, 5);
            VisualFactory.Rect("LZCrossV", _lzMarker.transform, new Vector2(0.18f, 1.15f), new Color(0.22f, 0.90f, 1f, 0.82f), Vector3.zero, 6);
            VisualFactory.Rect("LZCrossH", _lzMarker.transform, new Vector2(1.15f, 0.18f), new Color(0.22f, 0.90f, 1f, 0.82f), Vector3.zero, 6);
        }

        private void SpawnSupplyCache(bool enhanced)
        {
            if (_supplyCache != null) Destroy(_supplyCache);
            PlayerTank player = CombatRoster.Player;
            Vector2 pos = player != null ? (Vector2)player.transform.position + new Vector2(1.4f, 0.8f) : _game.BasePosition + new Vector2(1.6f, 1.0f);
            _supplyCache = new GameObject(enhanced ? "OPERATION_REWARD_CACHE" : "FIELD_SUPPORT_CACHE");
            _supplyCache.transform.position = pos;
            VisualFactory.Rect("CacheBody", _supplyCache.transform, new Vector2(0.80f, 0.60f), enhanced ? new Color(0.22f, 0.88f, 0.48f) : new Color(0.18f, 0.58f, 0.86f), Vector3.zero, 12);
            VisualFactory.Rect("CacheBand", _supplyCache.transform, new Vector2(0.86f, 0.13f), Color.white, Vector3.zero, 13);
            VisualFactory.RingObject("CacheGlow", _supplyCache.transform, new Vector2(1.15f, 1.15f), new Color(0.22f, 0.90f, 1f, 0.52f), Vector3.zero, 11);
        }

        private void UpdateSupplyCache()
        {
            if (_supplyCache == null) return;
            PlayerTank player = CombatRoster.Player;
            if (player == null || Vector2.Distance(player.transform.position, _supplyCache.transform.position) > 1.15f) return;

            player.AddAmmo(AmmoType.ArmorPiercing, 2);
            player.AddAmmo(AmmoType.Explosive, 1);
            player.AddAmmo(AmmoType.EMP, 1);
            if (player.Health != null) player.Health.Heal(1);
            VisualFactory.RingPulse(_supplyCache.transform.position, new Color(0.22f, 1f, 0.62f), 0.85f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.54f, 0.05f);
            Destroy(_supplyCache);
        }

        private void ResetOperation()
        {
            if (_round == 0 && !_operationActive && _stage == BattlefieldOperationStage.None) return;
            ClearRuntimeObjects();
            _round = 0;
            _operationActive = false;
            _stage = BattlefieldOperationStage.None;
            _headline = "NO ACTIVE OPERATION";
            _detail = string.Empty;
            _reward = string.Empty;
        }

        private void ClearRuntimeObjects()
        {
            if (_lzMarker != null) Destroy(_lzMarker);
            if (_supplyCache != null) Destroy(_supplyCache);
            if (_guardian != null) Destroy(_guardian.gameObject);
            if (_medic != null) Destroy(_medic.gameObject);
            if (_hunter != null) Destroy(_hunter.gameObject);
            _lzMarker = null;
            _supplyCache = null;
            _guardian = null;
            _medic = null;
            _hunter = null;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _titleStyle.normal.textColor = new Color(0.45f, 0.88f, 1f);
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            _bodyStyle.normal.textColor = new Color(0.82f, 0.90f, 0.96f);
            _goodStyle = new GUIStyle(_bodyStyle); _goodStyle.normal.textColor = new Color(0.35f, 1f, 0.58f);
            _warnStyle = new GUIStyle(_bodyStyle); _warnStyle.normal.textColor = new Color(1f, 0.62f, 0.24f);
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || (_round == 0)) return;
            EnsureStyles();
            float width = Mathf.Min(510f, Screen.width * 0.42f);
            Rect box = new Rect(18f, Screen.height - 114f, width, 94f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 12f, box.y + 8f, width - 24f, 22f), _headline, _titleStyle);
            GUI.Label(new Rect(box.x + 12f, box.y + 32f, width - 24f, 20f), _detail, _stage == BattlefieldOperationStage.Failed ? _warnStyle : _bodyStyle);
            if (!string.IsNullOrEmpty(_reward))
                GUI.Label(new Rect(box.x + 12f, box.y + 56f, width - 24f, 20f), _reward, _goodStyle);
            if (_operationActive)
                GUI.Label(new Rect(box.x + width - 150f, box.y + 70f, 138f, 18f), $"ALLIES {AlliedAliveCount()}/3", _bodyStyle);
        }
    }
}
