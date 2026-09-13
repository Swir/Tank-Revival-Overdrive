using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum CombinedManeuverPhase
    {
        Screen,
        Suppress,
        Flank,
        Breach
    }

    [DefaultExecutionOrder(435)]
    public sealed class AdaptiveFireControlDirector : MonoBehaviour
    {
        public const float DecisionCadence = 0.34f;
        public const float MinimumSequenceCadence = 4.8f;
        public const float MaximumSequenceCadence = 7.2f;
        public const float MaximumPredictionSeconds = 0.72f;
        public const float SuppressionLaneHalfWidth = 1.35f;
        public const int MaxSuppressionLanes = 2;
        public const int MaxShotsPerSequenceBeat = 5;
        public const int MaxManagedActors = 40;

        private sealed class Lane
        {
            public Vector2 Center;
            public Vector2 Axis;
            public float ExpiresAt;
        }

        private static AdaptiveFireControlDirector _instance;
        private readonly List<Lane> _lanes = new List<Lane>(MaxSuppressionLanes);
        private TankGame _game;
        private Vector2 _lastPlayerPosition;
        private Vector2 _observedPlayerVelocity;
        private float _lastPlayerSampleAt;
        private float _nextDecision;
        private float _nextSequence;
        private int _sequenceIndex;
        private CombinedManeuverPhase _phase;
        private int _lastShots;
        private int _lastManagedActors;

        public static AdaptiveFireControlDirector Instance => _instance;
        public static bool ConfigurationValid =>
            DecisionCadence >= 0.25f && DecisionCadence <= 0.50f &&
            MinimumSequenceCadence >= 4.0f && MaximumSequenceCadence <= 8.0f && MinimumSequenceCadence < MaximumSequenceCadence &&
            MaximumPredictionSeconds >= 0.35f && MaximumPredictionSeconds <= 0.90f &&
            SuppressionLaneHalfWidth >= 0.8f && SuppressionLaneHalfWidth <= 1.8f &&
            MaxSuppressionLanes >= 1 && MaxSuppressionLanes <= 3 &&
            MaxShotsPerSequenceBeat >= 3 && MaxShotsPerSequenceBeat <= 6 &&
            MaxManagedActors >= 24 && MaxManagedActors <= 48;

        public CombinedManeuverPhase ActivePhase => _phase;
        public int ActiveSuppressionLanes => _lanes.Count;
        public int LastSequenceShots => _lastShots;
        public int LastManagedActors => _lastManagedActors;
        public Vector2 ObservedPlayerVelocity => _observedPlayerVelocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<AdaptiveFireControlDirector>() != null) return;
            var go = new GameObject("AdaptiveFireControlDirector_v7_5");
            DontDestroyOnLoad(go);
            go.AddComponent<AdaptiveFireControlDirector>();
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
            _phase = CombinedManeuverPhase.Screen;
            _nextSequence = Time.time + 5.5f;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) return;

            SamplePlayerMotion();
            ExpireLanes();

            if (Time.time < _nextDecision) return;
            _nextDecision = Time.time + DecisionCadence;

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            ApplyManeuverOrders(round);

            if (round < 12 || Time.time < _nextSequence) return;
            float progress = (round - 1f) / 99f;
            _nextSequence = Time.time + Mathf.Lerp(MaximumSequenceCadence, MinimumSequenceCadence, progress);
            ExecuteSequenceBeat(round, progress);
        }

        private void SamplePlayerMotion()
        {
            Vector2 now = _game.PlayerPosition;
            float time = Time.time;
            if (_lastPlayerSampleAt > 0f)
            {
                float dt = Mathf.Max(0.02f, time - _lastPlayerSampleAt);
                Vector2 instantaneous = (now - _lastPlayerPosition) / dt;
                instantaneous = Vector2.ClampMagnitude(instantaneous, 7.0f);
                _observedPlayerVelocity = Vector2.Lerp(_observedPlayerVelocity, instantaneous, 0.42f);
            }
            _lastPlayerPosition = now;
            _lastPlayerSampleAt = time;
        }

        private void ExpireLanes()
        {
            for (int i = _lanes.Count - 1; i >= 0; i--)
                if (_lanes[i] == null || Time.time >= _lanes[i].ExpiresAt) _lanes.RemoveAt(i);
        }

        private void ExecuteSequenceBeat(int round, float progress)
        {
            _sequenceIndex++;
            _phase = (CombinedManeuverPhase)(_sequenceIndex % 4);
            _lastShots = 0;

            switch (_phase)
            {
                case CombinedManeuverPhase.Screen:
                    ExecuteScreenBeat(round, progress);
                    break;
                case CombinedManeuverPhase.Suppress:
                    ExecuteSuppressionBeat(round, progress);
                    break;
                case CombinedManeuverPhase.Flank:
                    ExecuteFlankBeat(round, progress);
                    break;
                case CombinedManeuverPhase.Breach:
                    ExecuteBreachBeat(round, progress);
                    break;
            }

            Vector2 signal = _phase == CombinedManeuverPhase.Breach ? _game.BasePosition : _game.PlayerPosition;
            Color color = _phase == CombinedManeuverPhase.Suppress ? new Color(1f, 0.30f, 0.08f) :
                          _phase == CombinedManeuverPhase.Flank ? new Color(1f, 0.72f, 0.12f) :
                          _phase == CombinedManeuverPhase.Breach ? new Color(1f, 0.12f, 0.05f) :
                          new Color(0.72f, 0.42f, 1f);
            VisualFactory.RingPulse(signal, color, 0.62f);
        }

        private void ExecuteScreenBeat(int round, float progress)
        {
            EnemyTank actor = SelectActor(SquadTacticalRole.Escort, EnemyKind.Heavy);
            if (actor == null) actor = SelectActor(SquadTacticalRole.Vanguard, EnemyKind.Heavy);
            if (actor == null) return;
            FireAdaptiveShot(actor, PredictPlayer(actor, 0.30f), AmmoType.Basic, round, progress, 0.92f, new Color(0.72f, 0.42f, 1f));
        }

        private void ExecuteSuppressionBeat(int round, float progress)
        {
            EnemyTank actor = SelectActor(SquadTacticalRole.Suppressor, EnemyKind.Sniper);
            if (actor == null) actor = SelectActor(SquadTacticalRole.Suppressor, EnemyKind.Elite);
            if (actor == null) return;

            Vector2 predicted = PredictPlayer(actor, 0.55f);
            Vector2 axis = _observedPlayerVelocity.sqrMagnitude > 0.15f ? _observedPlayerVelocity.normalized : Vector2.right;
            AddSuppressionLane(predicted, axis, Mathf.Lerp(1.8f, 2.7f, progress));

            Vector2 side = new Vector2(-axis.y, axis.x);
            int shots = round >= 60 ? 3 : 2;
            shots = Mathf.Min(shots, MaxShotsPerSequenceBeat);
            for (int i = 0; i < shots; i++)
            {
                float t = shots == 1 ? 0f : i / (float)(shots - 1);
                Vector2 target = predicted + side * Mathf.Lerp(-SuppressionLaneHalfWidth, SuppressionLaneHalfWidth, t);
                if (FireAdaptiveShot(actor, target, AmmoType.Basic, round, progress, 0.82f, new Color(1f, 0.30f, 0.08f))) _lastShots++;
            }
        }

        private void ExecuteFlankBeat(int round, float progress)
        {
            EnemyTank flanker = SelectActor(SquadTacticalRole.Flanker, EnemyKind.Fast);
            EnemyTank hunter = SelectActor(SquadTacticalRole.Hunter, EnemyKind.Elite);
            if (flanker != null && FireAdaptiveShot(flanker, PredictPlayer(flanker, 0.44f), AmmoType.Basic, round, progress, 1.08f, new Color(1f, 0.70f, 0.10f))) _lastShots++;
            if (_lastShots < MaxShotsPerSequenceBeat && hunter != null && hunter != flanker && FireAdaptiveShot(hunter, PredictPlayer(hunter, 0.62f), AmmoType.Basic, round, progress, 1.12f, new Color(0.24f, 0.88f, 1f))) _lastShots++;
        }

        private void ExecuteBreachBeat(int round, float progress)
        {
            EnemyTank breaker = SelectActor(SquadTacticalRole.Breaker, EnemyKind.Siege);
            if (breaker == null) return;
            Vector2 target = _game.BasePosition;
            if (FireAdaptiveShot(breaker, target, AmmoType.Explosive, round, progress, 0.88f, new Color(1f, 0.14f, 0.04f))) _lastShots++;
        }

        private void AddSuppressionLane(Vector2 center, Vector2 axis, float duration)
        {
            if (_lanes.Count >= MaxSuppressionLanes) _lanes.RemoveAt(0);
            _lanes.Add(new Lane
            {
                Center = center,
                Axis = axis.sqrMagnitude > 0.01f ? axis.normalized : Vector2.right,
                ExpiresAt = Time.time + Mathf.Clamp(duration, 1.4f, 3.0f)
            });
            VisualFactory.RingPulse(center, new Color(1f, 0.22f, 0.06f), SuppressionLaneHalfWidth * 0.75f);
        }

        private void ApplyManeuverOrders(int round)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null) return;
            int managed = 0;
            Vector2 player = _game.PlayerPosition;
            Vector2 eagle = _game.BasePosition;
            float progress = (round - 1f) / 99f;

            for (int i = 0; i < enemies.Length && managed < MaxManagedActors; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsLive(enemy)) continue;
                TacticalNavigationAgent agent = enemy.GetComponent<TacticalNavigationAgent>();
                if (agent == null) continue;
                managed++;

                SquadTacticalRole role = agent.Role;
                if (_phase == CombinedManeuverPhase.Screen && (role == SquadTacticalRole.Escort || enemy.Kind == EnemyKind.Heavy))
                {
                    Vector2 anchor = Vector2.Lerp(player, eagle, 0.38f);
                    agent.SetOrder(anchor, 2.0f, enemy.Kind == EnemyKind.Heavy ? 0.80f : 0.88f, enemies);
                }
                else if (_phase == CombinedManeuverPhase.Suppress && role == SquadTacticalRole.Suppressor)
                {
                    agent.SetOrder(player, enemy.Kind == EnemyKind.Sniper ? 6.3f : 4.5f, 0.78f, enemies);
                }
                else if (_phase == CombinedManeuverPhase.Flank && (role == SquadTacticalRole.Flanker || role == SquadTacticalRole.Hunter))
                {
                    Vector2 toPlayer = player - (Vector2)enemy.transform.position;
                    Vector2 side = new Vector2(-toPlayer.y, toPlayer.x).normalized;
                    float sign = ((enemy.GetInstanceID() + _sequenceIndex) & 1) == 0 ? 1f : -1f;
                    Vector2 flank = player + side * sign * Mathf.Lerp(4.0f, 5.4f, progress) + _observedPlayerVelocity * 0.18f;
                    agent.SetOrder(flank, role == SquadTacticalRole.Hunter ? 1.3f : 1.6f, 1.16f, enemies);
                }
                else if (_phase == CombinedManeuverPhase.Breach && role == SquadTacticalRole.Breaker)
                {
                    agent.SetOrder(eagle, 1.1f, 1.02f, enemies);
                }
            }
            _lastManagedActors = managed;
        }

        private Vector2 PredictPlayer(EnemyTank actor, float classWeight)
        {
            Vector2 player = _game.PlayerPosition;
            float distance = Vector2.Distance(actor.transform.position, player);
            float travelEstimate = distance / 10.5f;
            float lead = Mathf.Clamp(travelEstimate * classWeight, 0f, MaximumPredictionSeconds);
            if (actor.Kind == EnemyKind.Sniper || actor.Kind == EnemyKind.Elite) lead = Mathf.Min(MaximumPredictionSeconds, lead * 1.25f + 0.08f);
            return player + _observedPlayerVelocity * lead;
        }

        private bool FireAdaptiveShot(EnemyTank actor, Vector2 target, AmmoType ammo, int round, float progress, float speedScale, Color color)
        {
            if (!IsLive(actor) || _lastShots >= MaxShotsPerSequenceBeat) return false;
            Vector2 origin = actor.transform.position;
            Vector2 direction = target - origin;
            if (direction.sqrMagnitude < 0.16f) return false;
            direction.Normalize();

            if (!HasFireLane(actor, origin, target) && ammo != AmmoType.Explosive) return false;

            int damage = 1 + (round >= 75 && (actor.Kind == EnemyKind.Heavy || actor.Kind == EnemyKind.Siege || actor.Kind == EnemyKind.Boss) ? 1 : 0);
            float speed = Mathf.Lerp(9.0f, 12.2f, progress) * speedScale;
            Vector2 muzzle = origin + direction * (actor.Kind == EnemyKind.Boss ? 1.06f : 0.82f);
            _game.SpawnProjectile(muzzle, direction, Team.Enemy, damage, speed, color, ammo);
            VisualFactory.MuzzleFlash(muzzle, color, actor.Kind == EnemyKind.Heavy || actor.Kind == EnemyKind.Siege || actor.Kind == EnemyKind.Boss ? 0.90f : 0.58f);
            BattleAudio.PlayGlobal(actor.Kind == EnemyKind.Heavy || actor.Kind == EnemyKind.Siege || actor.Kind == EnemyKind.Boss ? SoundCue.HeavyShot : SoundCue.EnemyShot, 0.09f, 0.07f);
            return true;
        }

        private static bool HasFireLane(EnemyTank actor, Vector2 origin, Vector2 target)
        {
            Vector2 delta = target - origin;
            float distance = delta.magnitude;
            if (distance < 0.2f) return false;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, delta / distance, distance);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.isTrigger || collider.transform == actor.transform) continue;
                EnemyTank friendly = collider.GetComponent<EnemyTank>();
                if (friendly != null && friendly != actor && friendly.Health != null && !friendly.Health.IsDead) return false;
                Obstacle obstacle = collider.GetComponent<Obstacle>();
                if (obstacle != null) return false;
            }
            return true;
        }

        private EnemyTank SelectActor(SquadTacticalRole preferredRole, EnemyKind preferredKind)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null) return null;
            EnemyTank best = null;
            float bestScore = float.MinValue;
            Vector2 player = _game.PlayerPosition;

            for (int i = 0; i < enemies.Length && i < MaxManagedActors; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsLive(enemy)) continue;
                TacticalNavigationAgent agent = enemy.GetComponent<TacticalNavigationAgent>();
                float score = -Vector2.Distance(enemy.transform.position, player) * 0.12f;
                if (enemy.Kind == preferredKind) score += 4f;
                if (agent != null && agent.Role == preferredRole) score += 6f;
                if (enemy.Kind == EnemyKind.Boss) score += 0.5f;
                if (score > bestScore) { bestScore = score; best = enemy; }
            }
            return best;
        }

        private static bool IsLive(EnemyTank enemy)
        {
            return enemy != null && enemy.Health != null && !enemy.Health.IsDead && enemy.Kind != EnemyKind.Supply;
        }
    }
}
