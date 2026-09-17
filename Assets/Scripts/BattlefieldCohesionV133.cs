using UnityEngine;

namespace TankRevival
{
    public enum SquadRoleV133
    {
        Leader = 0,
        Wingman = 1,
        Breacher = 2,
        Support = 3
    }

    public enum SquadCohesionStateV133
    {
        Forming = 0,
        Cohesive = 1,
        Shocked = 2,
        Regrouping = 3
    }

    public struct SquadAssignmentV133
    {
        public bool Tracked;
        public int SquadId;
        public int Slot;
        public int Generation;
        public SquadRoleV133 Role;
        public int Signature;
    }

    public struct SquadCohesionIntentV133
    {
        public float MovementScale;
        public float ReloadScale;
        public float SpreadScale;
        public float Cohesion01;
        public bool PreferPlayer;
    }

    public struct SquadTelemetrySnapshotV133
    {
        public int ActiveSquads;
        public int TrackedActors;
        public int ShockedSquads;
        public int RegroupingSquads;
        public int LeaderLosses;
        public float AverageCohesion01;
        public int Signature;
    }

    /// <summary>
    /// Pure deterministic v13.3 squad model. It produces bounded role/cohesion intent only;
    /// EnemyTank/Rigidbody2D/Projectile remain the movement and firing authorities.
    /// </summary>
    public static class BattlefieldCohesionModelV133
    {
        public const int SquadSize = 4;
        public const int MaxSquads = 6;
        public const int MaxTrackedActors = SquadSize * MaxSquads;
        public const float EvaluationSeconds = 0.25f;
        public const float MinorShockSeconds = 0.65f;
        public const float LeaderShockSeconds = 1.25f;
        public const float RegroupSeconds = 1.65f;
        public const float MinMovementScale = 0.92f;
        public const float MaxMovementScale = 1.04f;
        public const float MinReloadScale = 0.95f;
        public const float MaxReloadScale = 1.08f;
        public const float MinSpreadScale = 0.95f;
        public const float MaxSpreadScale = 1.08f;
        public const float RegroupDistance = 3.20f;
        public const float SeparationDistance = 0.90f;

        public static bool ConfigurationValid =>
            SquadSize == 4 && MaxSquads == 6 && MaxTrackedActors == 24 &&
            EvaluationSeconds >= 0.20f && LeaderShockSeconds >= 1.0f && RegroupSeconds >= 1.25f &&
            MinMovementScale >= 0.90f && MaxMovementScale <= 1.06f &&
            MinReloadScale >= 0.94f && MaxReloadScale <= 1.10f &&
            MinSpreadScale >= 0.94f && MaxSpreadScale <= 1.10f;

        public static bool IsEligible(EnemyKind kind) => kind != EnemyKind.Boss && kind != EnemyKind.Supply;

        public static SquadRoleV133 RoleForSlot(int slot)
        {
            switch (PositiveMod(slot, SquadSize))
            {
                case 0: return SquadRoleV133.Leader;
                case 1: return SquadRoleV133.Wingman;
                case 2: return SquadRoleV133.Breacher;
                default: return SquadRoleV133.Support;
            }
        }

        public static SquadAssignmentV133 AssignmentForOrdinal(int round, int ordinal, EnemyKind kind)
        {
            if (!IsEligible(kind) || ordinal < 0)
                return default;
            int boundedOrdinal = ordinal % MaxTrackedActors;
            int squad = boundedOrdinal / SquadSize;
            int slot = boundedOrdinal % SquadSize;
            int generation = ordinal / MaxTrackedActors;
            SquadRoleV133 role = RoleForSlot(slot);
            int signature = 37;
            signature = unchecked(signature * 31 + Mathf.Clamp(round, 1, 100));
            signature = unchecked(signature * 31 + ordinal);
            signature = unchecked(signature * 31 + squad);
            signature = unchecked(signature * 31 + slot);
            signature = unchecked(signature * 31 + generation);
            signature = unchecked(signature * 31 + (int)kind);
            signature = unchecked(signature * 31 + (int)role);
            return new SquadAssignmentV133
            {
                Tracked = true,
                SquadId = squad,
                Slot = slot,
                Generation = generation,
                Role = role,
                Signature = signature
            };
        }

        public static int PromotedLeaderSlot(int liveMask)
        {
            int mask = liveMask & 0x0F;
            for (int slot = 0; slot < SquadSize; slot++)
                if ((mask & (1 << slot)) != 0)
                    return slot;
            return -1;
        }

        public static SquadCohesionIntentV133 IntentFor(SquadCohesionStateV133 state, SquadRoleV133 role, bool effectiveLeader)
        {
            SquadCohesionIntentV133 intent = new SquadCohesionIntentV133
            {
                MovementScale = 1f,
                ReloadScale = 1f,
                SpreadScale = 1f,
                Cohesion01 = 0.55f,
                PreferPlayer = false
            };

            switch (state)
            {
                case SquadCohesionStateV133.Cohesive:
                    intent.Cohesion01 = 1f;
                    switch (role)
                    {
                        case SquadRoleV133.Leader:
                            intent.MovementScale = 1.00f; intent.ReloadScale = 0.98f; intent.SpreadScale = 0.97f; intent.PreferPlayer = true;
                            break;
                        case SquadRoleV133.Wingman:
                            intent.MovementScale = 1.02f; intent.ReloadScale = 1.00f; intent.SpreadScale = 0.98f;
                            break;
                        case SquadRoleV133.Breacher:
                            intent.MovementScale = 1.04f; intent.ReloadScale = 0.99f; intent.SpreadScale = 1.00f; intent.PreferPlayer = true;
                            break;
                        case SquadRoleV133.Support:
                            intent.MovementScale = 0.96f; intent.ReloadScale = 0.95f; intent.SpreadScale = 0.95f;
                            break;
                    }
                    if (effectiveLeader)
                    {
                        intent.ReloadScale = Mathf.Min(intent.ReloadScale, 0.98f);
                        intent.SpreadScale = Mathf.Min(intent.SpreadScale, 0.97f);
                        intent.PreferPlayer = true;
                    }
                    break;
                case SquadCohesionStateV133.Shocked:
                    intent.MovementScale = 0.92f; intent.ReloadScale = 1.08f; intent.SpreadScale = 1.08f; intent.Cohesion01 = 0.24f;
                    break;
                case SquadCohesionStateV133.Regrouping:
                    intent.MovementScale = 0.96f; intent.ReloadScale = 1.04f; intent.SpreadScale = 1.03f; intent.Cohesion01 = 0.58f;
                    break;
                default:
                    intent.MovementScale = 0.98f; intent.ReloadScale = 1.02f; intent.SpreadScale = 1.03f; intent.Cohesion01 = 0.48f;
                    break;
            }

            intent.MovementScale = Mathf.Clamp(intent.MovementScale, MinMovementScale, MaxMovementScale);
            intent.ReloadScale = Mathf.Clamp(intent.ReloadScale, MinReloadScale, MaxReloadScale);
            intent.SpreadScale = Mathf.Clamp(intent.SpreadScale, MinSpreadScale, MaxSpreadScale);
            intent.Cohesion01 = Mathf.Clamp01(intent.Cohesion01);
            return intent;
        }

        public static Vector2 FormationDirection(SquadCohesionStateV133 state, SquadRoleV133 role, Vector2 self,
            Vector2 leader, Vector2 current, int squadId)
        {
            Vector2 delta = leader - self;
            float distance = delta.magnitude;
            if ((state == SquadCohesionStateV133.Shocked || state == SquadCohesionStateV133.Regrouping) && distance > 0.35f)
                return Cardinal(delta, current);
            if (state != SquadCohesionStateV133.Cohesive || distance <= 0.01f)
                return Cardinal(current, Vector2.down);
            if (distance > RegroupDistance)
                return Cardinal(delta, current);
            if (distance < SeparationDistance && role != SquadRoleV133.Leader)
                return Cardinal(-delta, current);
            if (role == SquadRoleV133.Wingman && distance > 1.45f)
            {
                Vector2 flank = (squadId & 1) == 0 ? new Vector2(-delta.y, delta.x) : new Vector2(delta.y, -delta.x);
                return Cardinal(flank, current);
            }
            return Cardinal(current, Vector2.down);
        }

        public static Vector2 Cardinal(Vector2 value, Vector2 fallback)
        {
            Vector2 v = value.sqrMagnitude > 0.0001f ? value : fallback;
            if (v.sqrMagnitude <= 0.0001f) return Vector2.down;
            return Mathf.Abs(v.x) > Mathf.Abs(v.y)
                ? new Vector2(Mathf.Sign(v.x), 0f)
                : new Vector2(0f, Mathf.Sign(v.y));
        }

        private static int PositiveMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }

    /// <summary>
    /// Fixed-capacity runtime registry for v13.3. It never scans the scene and never spawns, moves, fires,
    /// damages or heals actors. It only publishes bounded squad intent consumed by canonical EnemyTank logic.
    /// </summary>
    public sealed class BattlefieldCohesionDirector : MonoBehaviour
    {
        public static BattlefieldCohesionDirector Instance { get; private set; }
        public static bool ConfigurationValid => BattlefieldCohesionModelV133.ConfigurationValid;

        private readonly EnemyTank[] _actors = new EnemyTank[BattlefieldCohesionModelV133.MaxTrackedActors];
        private readonly int[] _generation = new int[BattlefieldCohesionModelV133.MaxTrackedActors];
        private readonly int[] _leaderSlot = new int[BattlefieldCohesionModelV133.MaxSquads];
        private readonly SquadCohesionStateV133[] _state = new SquadCohesionStateV133[BattlefieldCohesionModelV133.MaxSquads];
        private readonly float[] _stateUntil = new float[BattlefieldCohesionModelV133.MaxSquads];
        private readonly int[] _leaderLosses = new int[BattlefieldCohesionModelV133.MaxSquads];
        private int _round = -1;
        private int _spawnOrdinal;
        private float _nextEvaluation;
        private string _hudText = "SQD STANDBY";
        private SquadTelemetrySnapshotV133 _snapshot;

        public string HudText => _hudText;
        public SquadTelemetrySnapshotV133 Snapshot => _snapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static BattlefieldCohesionDirector EnsureInstalled()
        {
            if (Instance != null) return Instance;
            BattlefieldCohesionDirector existing = FindAnyObjectByType<BattlefieldCohesionDirector>();
            if (existing != null) return existing;
            GameObject go = new GameObject("BattlefieldCohesionDirector_v13_3");
            DontDestroyOnLoad(go);
            return go.AddComponent<BattlefieldCohesionDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ResetRegistry(-1);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Time.time < _nextEvaluation) return;
            _nextEvaluation = Time.time + BattlefieldCohesionModelV133.EvaluationSeconds;
            EvaluateSquads();
        }

        public SquadAssignmentV133 Register(EnemyTank actor, EnemyKind kind, int round)
        {
            if (actor == null || !BattlefieldCohesionModelV133.IsEligible(kind)) return default;
            int boundedRound = Mathf.Clamp(round, 1, 100);
            if (_round != boundedRound) ResetRegistry(boundedRound);

            int ordinal = _spawnOrdinal++;
            SquadAssignmentV133 preferred = BattlefieldCohesionModelV133.AssignmentForOrdinal(boundedRound, ordinal, kind);
            int slotIndex = preferred.SquadId * BattlefieldCohesionModelV133.SquadSize + preferred.Slot;
            if (_actors[slotIndex] != null)
            {
                slotIndex = FirstFreeSlot();
                if (slotIndex < 0) return default;
                preferred.SquadId = slotIndex / BattlefieldCohesionModelV133.SquadSize;
                preferred.Slot = slotIndex % BattlefieldCohesionModelV133.SquadSize;
                preferred.Role = BattlefieldCohesionModelV133.RoleForSlot(preferred.Slot);
            }

            _actors[slotIndex] = actor;
            _generation[slotIndex] = preferred.Generation;
            int squad = preferred.SquadId;
            if (_leaderSlot[squad] < 0 || ActorAt(squad, _leaderSlot[squad]) == null)
                _leaderSlot[squad] = BattlefieldCohesionModelV133.PromotedLeaderSlot(LiveMask(squad));
            if (LiveCount(squad) <= 1)
            {
                _state[squad] = SquadCohesionStateV133.Forming;
                _stateUntil[squad] = Time.time + 0.45f;
            }
            SetLeaderMarkers(squad);
            RefreshTelemetry();
            return preferred;
        }

        public void Unregister(EnemyTank actor)
        {
            if (actor == null) return;
            int actorIndex = FindActorIndex(actor);
            if (actorIndex < 0) return;
            int squad = actorIndex / BattlefieldCohesionModelV133.SquadSize;
            int memberSlot = actorIndex % BattlefieldCohesionModelV133.SquadSize;
            bool leaderLost = _leaderSlot[squad] == memberSlot;
            _actors[actorIndex] = null;
            _generation[actorIndex] = 0;
            int live = LiveCount(squad);
            if (live <= 0)
            {
                _leaderSlot[squad] = -1;
                _state[squad] = SquadCohesionStateV133.Forming;
                _stateUntil[squad] = 0f;
            }
            else if (leaderLost)
            {
                _leaderSlot[squad] = -1;
                _leaderLosses[squad]++;
                _state[squad] = SquadCohesionStateV133.Shocked;
                _stateUntil[squad] = Time.time + BattlefieldCohesionModelV133.LeaderShockSeconds;
            }
            else if (_state[squad] == SquadCohesionStateV133.Cohesive && live >= 2)
            {
                _state[squad] = SquadCohesionStateV133.Shocked;
                _stateUntil[squad] = Time.time + BattlefieldCohesionModelV133.MinorShockSeconds;
            }
            SetLeaderMarkers(squad);
            RefreshTelemetry();
        }

        public static SquadCohesionIntentV133 IntentFor(EnemyTank actor)
        {
            BattlefieldCohesionDirector d = Instance;
            return d != null ? d.ResolveIntent(actor) : BattlefieldCohesionModelV133.IntentFor(SquadCohesionStateV133.Forming, SquadRoleV133.Wingman, false);
        }

        public static float MovementScale(EnemyTank actor) => IntentFor(actor).MovementScale;
        public static float ReloadScale(EnemyTank actor) => IntentFor(actor).ReloadScale;
        public static float SpreadScale(EnemyTank actor) => IntentFor(actor).SpreadScale;
        public static bool PreferPlayer(EnemyTank actor) => IntentFor(actor).PreferPlayer;

        public static Vector2 AdjustDirection(EnemyTank actor, Vector2 self, Vector2 current)
        {
            BattlefieldCohesionDirector d = Instance;
            return d != null ? d.ResolveDirection(actor, self, current) : BattlefieldCohesionModelV133.Cardinal(current, Vector2.down);
        }

        private SquadCohesionIntentV133 ResolveIntent(EnemyTank actor)
        {
            int index = FindActorIndex(actor);
            if (index < 0)
                return new SquadCohesionIntentV133 { MovementScale = 1f, ReloadScale = 1f, SpreadScale = 1f, Cohesion01 = 0f, PreferPlayer = false };
            int squad = index / BattlefieldCohesionModelV133.SquadSize;
            int member = index % BattlefieldCohesionModelV133.SquadSize;
            bool effectiveLeader = _leaderSlot[squad] == member;
            return BattlefieldCohesionModelV133.IntentFor(_state[squad], BattlefieldCohesionModelV133.RoleForSlot(member), effectiveLeader);
        }

        private Vector2 ResolveDirection(EnemyTank actor, Vector2 self, Vector2 current)
        {
            int index = FindActorIndex(actor);
            if (index < 0) return BattlefieldCohesionModelV133.Cardinal(current, Vector2.down);
            int squad = index / BattlefieldCohesionModelV133.SquadSize;
            int member = index % BattlefieldCohesionModelV133.SquadSize;
            EnemyTank leader = ActorAt(squad, _leaderSlot[squad]);
            if (leader == null || leader == actor)
                return BattlefieldCohesionModelV133.Cardinal(current, Vector2.down);
            return BattlefieldCohesionModelV133.FormationDirection(_state[squad], BattlefieldCohesionModelV133.RoleForSlot(member),
                self, leader.transform.position, current, squad);
        }

        private void EvaluateSquads()
        {
            for (int squad = 0; squad < BattlefieldCohesionModelV133.MaxSquads; squad++)
            {
                int live = LiveCount(squad);
                if (live <= 0)
                {
                    _leaderSlot[squad] = -1;
                    _state[squad] = SquadCohesionStateV133.Forming;
                    _stateUntil[squad] = 0f;
                    continue;
                }

                if (_leaderSlot[squad] >= 0 && ActorAt(squad, _leaderSlot[squad]) == null)
                {
                    _leaderSlot[squad] = -1;
                    _leaderLosses[squad]++;
                    _state[squad] = SquadCohesionStateV133.Shocked;
                    _stateUntil[squad] = Time.time + BattlefieldCohesionModelV133.LeaderShockSeconds;
                }

                if (_state[squad] == SquadCohesionStateV133.Shocked && Time.time >= _stateUntil[squad])
                {
                    if (_leaderSlot[squad] < 0)
                        _leaderSlot[squad] = BattlefieldCohesionModelV133.PromotedLeaderSlot(LiveMask(squad));
                    _state[squad] = SquadCohesionStateV133.Regrouping;
                    _stateUntil[squad] = Time.time + BattlefieldCohesionModelV133.RegroupSeconds;
                }
                else if (_state[squad] == SquadCohesionStateV133.Regrouping && Time.time >= _stateUntil[squad])
                {
                    _state[squad] = live >= 2 ? SquadCohesionStateV133.Cohesive : SquadCohesionStateV133.Forming;
                    _stateUntil[squad] = Time.time + 0.45f;
                }
                else if (_state[squad] == SquadCohesionStateV133.Forming && Time.time >= _stateUntil[squad] && live >= 2)
                {
                    if (_leaderSlot[squad] < 0)
                        _leaderSlot[squad] = BattlefieldCohesionModelV133.PromotedLeaderSlot(LiveMask(squad));
                    _state[squad] = SquadCohesionStateV133.Cohesive;
                }
                SetLeaderMarkers(squad);
            }
            RefreshTelemetry();
        }

        private void ResetRegistry(int round)
        {
            for (int i = 0; i < _actors.Length; i++)
            {
                if (_actors[i] != null) SetActorMarker(_actors[i], false);
                _actors[i] = null;
                _generation[i] = 0;
            }
            for (int squad = 0; squad < _leaderSlot.Length; squad++)
            {
                _leaderSlot[squad] = -1;
                _state[squad] = SquadCohesionStateV133.Forming;
                _stateUntil[squad] = 0f;
                _leaderLosses[squad] = 0;
            }
            _round = round;
            _spawnOrdinal = 0;
            _nextEvaluation = Time.time;
            RefreshTelemetry();
        }

        private int FirstFreeSlot()
        {
            for (int i = 0; i < _actors.Length; i++)
                if (_actors[i] == null)
                    return i;
            return -1;
        }

        private int FindActorIndex(EnemyTank actor)
        {
            for (int i = 0; i < _actors.Length; i++)
                if (_actors[i] == actor)
                    return i;
            return -1;
        }

        private EnemyTank ActorAt(int squad, int member)
        {
            if (squad < 0 || squad >= BattlefieldCohesionModelV133.MaxSquads || member < 0 || member >= BattlefieldCohesionModelV133.SquadSize)
                return null;
            return _actors[squad * BattlefieldCohesionModelV133.SquadSize + member];
        }

        private int LiveMask(int squad)
        {
            int mask = 0;
            for (int member = 0; member < BattlefieldCohesionModelV133.SquadSize; member++)
                if (ActorAt(squad, member) != null)
                    mask |= 1 << member;
            return mask;
        }

        private int LiveCount(int squad)
        {
            int mask = LiveMask(squad);
            int count = 0;
            for (int member = 0; member < BattlefieldCohesionModelV133.SquadSize; member++)
                if ((mask & (1 << member)) != 0) count++;
            return count;
        }

        private void SetLeaderMarkers(int squad)
        {
            for (int member = 0; member < BattlefieldCohesionModelV133.SquadSize; member++)
            {
                EnemyTank actor = ActorAt(squad, member);
                if (actor != null) SetActorMarker(actor, _leaderSlot[squad] == member);
            }
        }

        private static void SetActorMarker(EnemyTank actor, bool enabled)
        {
            if (actor == null) return;
            Transform marker = actor.transform.Find("V13_3_SQUAD_LEADER_MARKER");
            if (marker == null && enabled)
            {
                GameObject root = new GameObject("V13_3_SQUAD_LEADER_MARKER");
                root.transform.SetParent(actor.transform, false);
                root.transform.localPosition = new Vector3(0f, 0.72f, 0f);
                VisualFactory.Disc("CommandHalo", root.transform, new Vector2(0.34f, 0.34f), new Color(0.10f, 0.78f, 1f, 0.28f), Vector3.zero, 28);
                VisualFactory.Rect("CommandTick", root.transform, new Vector2(0.08f, 0.30f), new Color(0.38f, 0.94f, 1f, 0.95f), new Vector3(0f, 0.04f, 0f), 29);
                marker = root.transform;
            }
            if (marker != null) marker.gameObject.SetActive(enabled);
        }

        private void RefreshTelemetry()
        {
            int activeSquads = 0, tracked = 0, shocked = 0, regrouping = 0, leaderLoss = 0;
            float cohesionSum = 0f;
            for (int squad = 0; squad < BattlefieldCohesionModelV133.MaxSquads; squad++)
            {
                int live = LiveCount(squad);
                if (live <= 0) { leaderLoss += _leaderLosses[squad]; continue; }
                activeSquads++;
                tracked += live;
                if (_state[squad] == SquadCohesionStateV133.Shocked) shocked++;
                if (_state[squad] == SquadCohesionStateV133.Regrouping) regrouping++;
                leaderLoss += _leaderLosses[squad];
                SquadRoleV133 sampleRole = BattlefieldCohesionModelV133.RoleForSlot(Mathf.Max(0, _leaderSlot[squad]));
                cohesionSum += BattlefieldCohesionModelV133.IntentFor(_state[squad], sampleRole, true).Cohesion01;
            }
            float average = activeSquads > 0 ? cohesionSum / activeSquads : 0f;
            int signature = 41;
            signature = unchecked(signature * 31 + _round);
            signature = unchecked(signature * 31 + activeSquads);
            signature = unchecked(signature * 31 + tracked);
            signature = unchecked(signature * 31 + shocked);
            signature = unchecked(signature * 31 + regrouping);
            signature = unchecked(signature * 31 + leaderLoss);
            signature = unchecked(signature * 31 + Mathf.RoundToInt(average * 1000f));
            _snapshot = new SquadTelemetrySnapshotV133
            {
                ActiveSquads = activeSquads,
                TrackedActors = tracked,
                ShockedSquads = shocked,
                RegroupingSquads = regrouping,
                LeaderLosses = leaderLoss,
                AverageCohesion01 = average,
                Signature = signature
            };
            _hudText = "SQD " + activeSquads + "/" + BattlefieldCohesionModelV133.MaxSquads +
                       " ACT " + tracked + "/" + BattlefieldCohesionModelV133.MaxTrackedActors +
                       " COH " + Mathf.RoundToInt(average * 100f).ToString("00") + "%" +
                       " SHK " + shocked + " RGP " + regrouping + " LDRLOSS " + leaderLoss + " SIG " + signature.ToString("X8");
        }

        private void OnGUI()
        {
            TankGame game = FindAnyObjectByType<TankGame>();
            if (game == null || !game.IsPlaying) return;
            float width = Mathf.Min(470f, Screen.width - 28f);
            Rect box = new Rect(Mathf.Max(14f, Screen.width - width - 14f), 12f, width, 56f);
            Color previous = GUI.color;
            GUI.color = new Color(0.025f, 0.055f, 0.085f, 0.94f);
            GUI.Box(box, string.Empty);
            GUI.color = new Color(0.70f, 0.93f, 1f, 1f);
            GUI.Label(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, 22f), "V13.3 BATTLEFIELD COHESION");
            GUI.color = Color.white;
            GUI.Label(new Rect(box.x + 12f, box.y + 28f, box.width - 24f, 22f), _hudText);
            GUI.color = previous;
        }
    }
}
