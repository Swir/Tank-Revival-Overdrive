using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum ForwardRecoveryPhase
    {
        Idle,
        Convoy,
        Hold,
        Support,
        Failed
    }

    [DefaultExecutionOrder(505)]
    public sealed class ForwardRecoveryFrontlineDirector : MonoBehaviour
    {
        public const float CommitmentRange = 1.60f;
        public const float ConvoySpeed = 1.05f;
        public const int ConvoyHealthMin = 7;
        public const int ConvoyHealthMax = 12;
        public const int BaseHealthMin = 11;
        public const int BaseHealthMax = 18;
        public const float HoldDuration = 13.5f;
        public const float SupportDuration = 18f;
        public const float SupportPulseCadence = 7.5f;
        public const int MaxSupportPulses = 2;
        public const int MaxCounterUnits = 4;
        public const int MaxCounterShotsPerBeat = 3;
        public const float CounterOrderCadence = 0.65f;
        public const float CounterFireCadence = 2.35f;
        public const float CounterContestRange = 1.35f;
        public const int SupplyAmmoPerPulse = 1;
        public const int CompletionBondReward = 5;
        public const int CompletionLogisticsBondReward = 7;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo SalvageListField = typeof(BattlefieldSalvageDirector).GetField("_salvage", PrivateInstance);
        private static readonly MethodInfo ResolveSalvageMethod = typeof(BattlefieldSalvageDirector).GetMethod("ResolveEntry", PrivateInstance);
        private static readonly FieldInfo EagleHealthField = typeof(TankGame).GetField("_baseHealth", PrivateInstance);

        private static ForwardRecoveryFrontlineDirector _instance;
        private TankGame _game;
        private BattlefieldSalvageDirector _salvage;
        private ForwardRecoveryPhase _phase;
        private GameObject _convoy;
        private Health _convoyHealth;
        private GameObject _base;
        private Health _baseHealth;
        private StrategicReserveKind _reserveKind;
        private bool _logisticsGrade;
        private Vector2 _destination;
        private float _holdRemaining;
        private float _supportEndsAt;
        private float _nextSupportPulse;
        private float _nextCounterOrder;
        private float _nextCounterFire;
        private int _supportPulses;
        private int _activeRound;
        private int _operationsCommitted;
        private int _operationsSecured;
        private int _operationsFailed;
        private int _counterShots;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _style;

        public static ForwardRecoveryFrontlineDirector Instance => _instance;
        public ForwardRecoveryPhase Phase => _phase;
        public int OperationsCommitted => _operationsCommitted;
        public int OperationsSecured => _operationsSecured;
        public int OperationsFailed => _operationsFailed;
        public int CounterShotsFired => _counterShots;
        public static bool BridgeAvailable => SalvageListField != null && ResolveSalvageMethod != null && EagleHealthField != null;
        public static bool ConfigurationValid =>
            CommitmentRange >= 1.2f && CommitmentRange <= 2.0f &&
            ConvoySpeed >= 0.7f && ConvoySpeed <= 1.4f &&
            ConvoyHealthMin >= 5 && ConvoyHealthMax <= 14 && ConvoyHealthMin < ConvoyHealthMax &&
            BaseHealthMin >= 9 && BaseHealthMax <= 22 && BaseHealthMin < BaseHealthMax &&
            HoldDuration >= 10f && HoldDuration <= 18f && SupportDuration >= 12f && SupportDuration <= 24f &&
            SupportPulseCadence >= 5f && SupportPulseCadence <= 10f && MaxSupportPulses >= 1 && MaxSupportPulses <= 3 &&
            MaxCounterUnits >= 2 && MaxCounterUnits <= 5 && MaxCounterShotsPerBeat >= 1 && MaxCounterShotsPerBeat <= 4 &&
            CounterOrderCadence >= 0.4f && CounterOrderCadence <= 0.9f && CounterFireCadence >= 1.8f && CounterFireCadence <= 3.2f &&
            CounterContestRange >= 1.0f && CounterContestRange <= 1.7f && SupplyAmmoPerPulse == 1 &&
            CompletionBondReward >= 3 && CompletionLogisticsBondReward > CompletionBondReward && CompletionLogisticsBondReward <= 9 && BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ForwardRecoveryFrontlineDirector>() != null) return;
            GameObject go = new GameObject("ForwardRecoveryFrontlineDirector_v8_9");
            DontDestroyOnLoad(go);
            go.AddComponent<ForwardRecoveryFrontlineDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            CleanupOperation();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_salvage == null) _salvage = BattlefieldSalvageDirector.Instance;

            if (_game == null || !_game.IsPlaying)
            {
                if (_activeRound != 0) ResetRun();
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round != _activeRound)
            {
                if (_phase == ForwardRecoveryPhase.Convoy || _phase == ForwardRecoveryPhase.Hold) FailOperation("ROUND CHANGED // RECOVERY ABORTED");
                else if (_phase == ForwardRecoveryPhase.Support) CleanupOperation();
                _activeRound = round;
            }

            if (_phase == ForwardRecoveryPhase.Idle)
            {
                TryCommitNearestSalvage();
                return;
            }

            if (_phase == ForwardRecoveryPhase.Convoy)
            {
                UpdateConvoy();
                UpdateCounteroffensive();
                return;
            }

            if (_phase == ForwardRecoveryPhase.Hold)
            {
                UpdateHold();
                UpdateCounteroffensive();
                return;
            }

            if (_phase == ForwardRecoveryPhase.Support)
            {
                UpdateSupport();
                UpdateCounteroffensive();
            }
        }

        public static bool CanCommit(float distance, bool operationBusy, bool salvageResolved, float remainingLifetime)
        {
            return !operationBusy && !salvageResolved && remainingLifetime > 0f && distance <= CommitmentRange;
        }

        public static int ConvoyHealthForRound(int round)
        {
            return Mathf.Clamp(ConvoyHealthMin + Mathf.Clamp(round, 1, 100) / 20, ConvoyHealthMin, ConvoyHealthMax);
        }

        public static int BaseHealthForRound(int round)
        {
            return Mathf.Clamp(BaseHealthMin + Mathf.Clamp(round, 1, 100) / 14, BaseHealthMin, BaseHealthMax);
        }

        public static int CounterUnitsForRound(int round)
        {
            if (round < 30) return 2;
            if (round < 65) return 3;
            return MaxCounterUnits;
        }

        public static AmmoType SupplyAmmoForReserve(StrategicReserveKind kind)
        {
            switch (kind)
            {
                case StrategicReserveKind.Armor: return AmmoType.ArmorPiercing;
                case StrategicReserveKind.ElectronicWarfare: return AmmoType.EMP;
                default: return AmmoType.Explosive;
            }
        }

        public static int CompletionReward(bool logisticsGrade)
        {
            return logisticsGrade ? CompletionLogisticsBondReward : CompletionBondReward;
        }

        private void TryCommitNearestSalvage()
        {
            if (_salvage == null || !BridgeAvailable || !Input.GetKeyDown(KeyCode.B)) return;
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player == null || player.Health == null || player.Health.IsDead) return;

            object entry = FindNearestSalvageEntry(player.transform.position, out GameObject root, out StrategicReserveKind reserve, out bool logistics, out bool resolved, out float remaining);
            if (entry == null || root == null) return;
            float distance = Vector2.Distance(player.transform.position, root.transform.position);
            if (!CanCommit(distance, false, resolved, remaining)) return;

            _reserveKind = reserve;
            _logisticsGrade = logistics;
            ResolveSalvageMethod.Invoke(_salvage, new object[] { entry, SalvageRecoveryMode.Strategic });
            BeginConvoy(root.transform.position);
        }

        private object FindNearestSalvageEntry(Vector2 position, out GameObject root, out StrategicReserveKind reserveKind, out bool logisticsGrade, out bool resolved, out float remainingLifetime)
        {
            root = null;
            reserveKind = StrategicReserveKind.Armor;
            logisticsGrade = false;
            resolved = true;
            remainingLifetime = 0f;
            if (_salvage == null || SalvageListField == null) return null;

            IEnumerable list = SalvageListField.GetValue(_salvage) as IEnumerable;
            if (list == null) return null;
            object best = null;
            float bestDistance = float.MaxValue;

            foreach (object item in list)
            {
                if (item == null) continue;
                Type t = item.GetType();
                GameObject candidateRoot = t.GetField("Root", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(item) as GameObject;
                if (candidateRoot == null) continue;
                bool candidateResolved = (bool)(t.GetField("Resolved", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(item) ?? true);
                float expiresAt = Convert.ToSingle(t.GetField("ExpiresAt", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(item) ?? 0f);
                float candidateRemaining = expiresAt - Time.time;
                float distance = Vector2.Distance(position, candidateRoot.transform.position);
                if (!CanCommit(distance, false, candidateResolved, candidateRemaining) || distance >= bestDistance) continue;

                bestDistance = distance;
                best = item;
                root = candidateRoot;
                reserveKind = (StrategicReserveKind)(t.GetField("ReserveKind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(item) ?? StrategicReserveKind.Armor);
                logisticsGrade = (bool)(t.GetField("LogisticsGrade", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(item) ?? false);
                resolved = candidateResolved;
                remainingLifetime = candidateRemaining;
            }
            return best;
        }

        private void BeginConvoy(Vector3 start)
        {
            CleanupObjectsOnly();
            _phase = ForwardRecoveryPhase.Convoy;
            _operationsCommitted++;
            _destination = ChooseDestination(start, _activeRound);

            _convoy = new GameObject("FRIENDLY_SALVAGE_CONVOY");
            _convoy.transform.position = start;
            Color color = ReserveColor(_reserveKind);
            VisualFactory.Rect("Hull", _convoy.transform, new Vector2(1.12f, 0.62f), new Color(0.12f, 0.20f, 0.16f), Vector3.zero, 12);
            VisualFactory.Rect("Cargo", _convoy.transform, new Vector2(0.66f, 0.34f), color, new Vector3(0f, 0.08f, 0f), 13);
            VisualFactory.Disc("WheelL", _convoy.transform, new Vector2(0.22f, 0.22f), Color.black, new Vector3(-0.38f, -0.30f, 0f), 14);
            VisualFactory.Disc("WheelR", _convoy.transform, new Vector2(0.22f, 0.22f), Color.black, new Vector3(0.38f, -0.30f, 0f), 14);
            BoxCollider2D collider = _convoy.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.08f, 0.60f);
            _convoyHealth = _convoy.AddComponent<Health>();
            _convoyHealth.Initialize(Team.Player, ConvoyHealthForRound(_activeRound));
            _convoyHealth.Died += OnConvoyDied;
            _nextCounterOrder = Time.time + 0.45f;
            _nextCounterFire = Time.time + 1.2f;
            _status = "FORWARD RECOVERY // ESCORT SALVAGE CONVOY [B committed]";
            _statusUntil = Time.unscaledTime + 4f;
            VisualFactory.RingPulse(start, color, 1.25f);
        }

        private void UpdateConvoy()
        {
            if (_convoy == null || _convoyHealth == null || _convoyHealth.IsDead)
            {
                FailOperation("SALVAGE CONVOY LOST");
                return;
            }
            Vector2 current = _convoy.transform.position;
            Vector2 next = Vector2.MoveTowards(current, _destination, ConvoySpeed * Time.deltaTime);
            _convoy.transform.position = next;
            if (Vector2.Distance(next, _destination) <= 0.08f)
                EstablishBase();
        }

        private void EstablishBase()
        {
            int carryHealth = _convoyHealth != null ? Mathf.Max(1, _convoyHealth.Current) : 1;
            if (_convoyHealth != null) _convoyHealth.Died -= OnConvoyDied;
            if (_convoy != null) Destroy(_convoy);
            _convoy = null;
            _convoyHealth = null;

            _base = new GameObject("FORWARD_RECOVERY_BASE");
            _base.transform.position = _destination;
            Color color = ReserveColor(_reserveKind);
            VisualFactory.Rect("Pad", _base.transform, new Vector2(1.58f, 1.06f), new Color(0.10f, 0.18f, 0.14f), Vector3.zero, 12);
            VisualFactory.Rect("Supply", _base.transform, new Vector2(0.86f, 0.54f), color, new Vector3(-0.18f, 0.02f, 0f), 13);
            VisualFactory.Rect("Mast", _base.transform, new Vector2(0.08f, 0.86f), Color.white, new Vector3(0.48f, 0.50f, 0f), 14);
            VisualFactory.Disc("Beacon", _base.transform, new Vector2(0.20f, 0.20f), color, new Vector3(0.48f, 0.94f, 0f), 15);
            BoxCollider2D collider = _base.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.50f, 1.00f);
            _baseHealth = _base.AddComponent<Health>();
            int maximum = BaseHealthForRound(_activeRound);
            _baseHealth.Initialize(Team.Player, maximum);
            if (carryHealth < ConvoyHealthForRound(_activeRound))
                _baseHealth.Damage(Mathf.Clamp(ConvoyHealthForRound(_activeRound) - carryHealth, 0, maximum - 1), Team.Enemy);
            _baseHealth.Died += OnBaseDied;
            _phase = ForwardRecoveryPhase.Hold;
            _holdRemaining = HoldDuration;
            _nextCounterOrder = Time.time + 0.25f;
            _nextCounterFire = Time.time + 0.9f;
            _status = "FORWARD BASE DEPLOYED // HOLD THE RECOVERY ZONE";
            _statusUntil = Time.unscaledTime + 4f;
            VisualFactory.RingPulse(_destination, color, 1.8f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.32f, 0.02f);
        }

        private void UpdateHold()
        {
            if (_base == null || _baseHealth == null || _baseHealth.IsDead)
            {
                FailOperation("FORWARD BASE DESTROYED");
                return;
            }
            bool contested = IsBaseContested();
            if (!contested) _holdRemaining -= Time.deltaTime;
            if (_holdRemaining <= 0f)
                SecureBase();
        }

        private void SecureBase()
        {
            _phase = ForwardRecoveryPhase.Support;
            _operationsSecured++;
            _supportEndsAt = Time.time + SupportDuration;
            _nextSupportPulse = Time.time + 0.5f;
            _supportPulses = 0;
            int reward = CompletionReward(_logisticsGrade);
            WarEconomyDirector.AwardMissionBonds(reward, "FORWARD RECOVERY BASE SECURED");
            _status = "FRONTLINE CONTROL SECURED // +" + reward + " WAR BONDS";
            _statusUntil = Time.unscaledTime + 4.5f;
            VisualFactory.RingPulse(_destination, new Color(0.20f, 1f, 0.62f), 2.2f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.42f, 0.04f);
        }

        private void UpdateSupport()
        {
            if (_base == null || _baseHealth == null || _baseHealth.IsDead)
            {
                CleanupOperation();
                return;
            }
            if (_supportPulses < MaxSupportPulses && Time.time >= _nextSupportPulse)
            {
                _nextSupportPulse = Time.time + SupportPulseCadence;
                ApplySupportPulse();
            }
            if (Time.time >= _supportEndsAt)
                CleanupOperation();
        }

        private void ApplySupportPulse()
        {
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player != null && player.Health != null && !player.Health.IsDead)
            {
                player.Health.Heal(1);
                player.AddAmmo(SupplyAmmoForReserve(_reserveKind), SupplyAmmoPerPulse);
            }
            if (_baseHealth != null && !_baseHealth.IsDead) _baseHealth.Heal(1);
            if (_supportPulses == 0 && (_logisticsGrade || _reserveKind == StrategicReserveKind.FireSupport))
            {
                Health eagle = EagleHealthField.GetValue(_game) as Health;
                eagle?.Heal(1);
            }
            _supportPulses++;
            _status = "RECOVERY BASE SUPPLY PULSE " + _supportPulses + "/" + MaxSupportPulses + " // +1 REPAIR +1 " + SupplyAmmoForReserve(_reserveKind);
            _statusUntil = Time.unscaledTime + 3.0f;
            VisualFactory.RingPulse(_destination, ReserveColor(_reserveKind), 1.15f);
        }

        private void UpdateCounteroffensive()
        {
            Vector2 target = _phase == ForwardRecoveryPhase.Convoy && _convoy != null ? (Vector2)_convoy.transform.position : _destination;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int desired = Mathf.Min(MaxCounterUnits, CounterUnitsForRound(_activeRound));
            int ordered = 0;

            if (Time.time >= _nextCounterOrder)
            {
                _nextCounterOrder = Time.time + CounterOrderCadence;
                for (int i = 0; i < enemies.Length && ordered < desired; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (!IsCounterCandidate(enemy)) continue;
                    TacticalNavigationAgent agent = enemy.GetComponent<TacticalNavigationAgent>();
                    if (agent == null)
                    {
                        agent = enemy.gameObject.AddComponent<TacticalNavigationAgent>();
                        agent.Initialize(enemy);
                    }
                    agent.SetRole(SquadTacticalRole.Breaker);
                    agent.SetOrder(target, 0.95f, enemy.Kind == EnemyKind.Fast ? 1.10f : 1.0f, enemies);
                    ordered++;
                }
            }

            if (Time.time >= _nextCounterFire)
            {
                _nextCounterFire = Time.time + CounterFireCadence;
                FireCounteroffensive(enemies, target);
            }
        }

        private void FireCounteroffensive(EnemyTank[] enemies, Vector2 target)
        {
            if (_game == null) return;
            int shots = 0;
            float progress = (_activeRound - 1f) / 99f;
            for (int i = 0; i < enemies.Length && shots < MaxCounterShotsPerBeat; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsCounterCandidate(enemy)) continue;
                Vector2 origin = enemy.transform.position;
                Vector2 delta = target - origin;
                float sqr = delta.sqrMagnitude;
                if (sqr < 1.2f || sqr > 100f) continue;
                Vector2 direction = delta.normalized;
                Vector2 muzzle = origin + direction * 0.76f;
                _game.SpawnProjectile(muzzle, direction, Team.Enemy, 1, Mathf.Lerp(8.8f, 11.8f, progress), new Color(1f, 0.52f, 0.14f), AmmoType.Basic);
                VisualFactory.MuzzleFlash(muzzle, new Color(1f, 0.44f, 0.10f), 0.52f);
                shots++;
                _counterShots++;
            }
        }

        private bool IsBaseContested()
        {
            if (_base == null) return false;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsCounterCandidate(enemy)) continue;
                if (Vector2.Distance(enemy.transform.position, _base.transform.position) <= CounterContestRange) return true;
            }
            return false;
        }

        private static bool IsCounterCandidate(EnemyTank enemy)
        {
            return enemy != null && enemy.Health != null && !enemy.Health.IsDead && enemy.Kind != EnemyKind.Supply && enemy.Kind != EnemyKind.Boss;
        }

        private void OnConvoyDied(Health health)
        {
            if (health == _convoyHealth) FailOperation("SALVAGE CONVOY DESTROYED");
        }

        private void OnBaseDied(Health health)
        {
            if (health == _baseHealth) FailOperation("FORWARD RECOVERY BASE DESTROYED");
        }

        private void FailOperation(string reason)
        {
            if (_phase == ForwardRecoveryPhase.Idle || _phase == ForwardRecoveryPhase.Failed) return;
            _operationsFailed++;
            _phase = ForwardRecoveryPhase.Failed;
            _status = reason;
            _statusUntil = Time.unscaledTime + 4f;
            Vector2 pos = _base != null ? (Vector2)_base.transform.position : _convoy != null ? (Vector2)_convoy.transform.position : _destination;
            VisualFactory.MicroBurst(pos, new Color(1f, 0.24f, 0.12f), 1.15f);
            CleanupObjectsOnly();
            _phase = ForwardRecoveryPhase.Idle;
        }

        private void CleanupOperation()
        {
            CleanupObjectsOnly();
            _phase = ForwardRecoveryPhase.Idle;
            _holdRemaining = 0f;
            _supportEndsAt = 0f;
            _supportPulses = 0;
        }

        private void CleanupObjectsOnly()
        {
            if (_convoyHealth != null) _convoyHealth.Died -= OnConvoyDied;
            if (_baseHealth != null) _baseHealth.Died -= OnBaseDied;
            if (_convoy != null) Destroy(_convoy);
            if (_base != null) Destroy(_base);
            _convoy = null;
            _convoyHealth = null;
            _base = null;
            _baseHealth = null;
        }

        private void ResetRun()
        {
            CleanupOperation();
            _activeRound = 0;
            _operationsCommitted = 0;
            _operationsSecured = 0;
            _operationsFailed = 0;
            _counterShots = 0;
            _status = string.Empty;
        }

        private static Vector2 ChooseDestination(Vector2 origin, int round)
        {
            float x = Mathf.Clamp(-origin.x * 0.45f + (((round / 10) & 1) == 0 ? -1.2f : 1.2f), -5.2f, 5.2f);
            float y = Mathf.Clamp(-2.8f + ((round % 3) - 1) * 0.55f, -3.6f, -1.7f);
            return new Vector2(x, y);
        }

        private static Color ReserveColor(StrategicReserveKind kind)
        {
            switch (kind)
            {
                case StrategicReserveKind.Armor: return new Color(0.30f, 0.78f, 1f);
                case StrategicReserveKind.ElectronicWarfare: return new Color(0.72f, 0.34f, 1f);
                default: return new Color(1f, 0.64f, 0.18f);
            }
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                _style.normal.textColor = new Color(0.82f, 1f, 0.90f);
            }

            if (_phase == ForwardRecoveryPhase.Idle && _salvage != null && _salvage.ActiveSalvageCount > 0)
                GUI.Label(new Rect(Screen.width * 0.5f - 280f, Screen.height - 88f, 560f, 24f), "B: COMMIT NEARBY SALVAGE TO FORWARD RECOVERY", _style);
            else if (_phase == ForwardRecoveryPhase.Convoy && _convoyHealth != null)
                GUI.Label(new Rect(Screen.width * 0.5f - 280f, Screen.height - 88f, 560f, 24f), "SALVAGE CONVOY " + _convoyHealth.Current + "/" + _convoyHealth.Maximum + " HP", _style);
            else if (_phase == ForwardRecoveryPhase.Hold && _baseHealth != null)
                GUI.Label(new Rect(Screen.width * 0.5f - 280f, Screen.height - 88f, 560f, 24f), "FORWARD BASE " + _baseHealth.Current + "/" + _baseHealth.Maximum + " HP // HOLD " + Mathf.CeilToInt(_holdRemaining) + "s", _style);
            else if (_phase == ForwardRecoveryPhase.Support)
                GUI.Label(new Rect(Screen.width * 0.5f - 280f, Screen.height - 88f, 560f, 24f), "RECOVERY BASE ONLINE // PULSES " + _supportPulses + "/" + MaxSupportPulses, _style);

            if (!string.IsNullOrEmpty(_status) && Time.unscaledTime < _statusUntil)
                GUI.Label(new Rect(Screen.width * 0.5f - 330f, 54f, 660f, 26f), _status, _style);
        }
    }
}
