using System;
using UnityEngine;

namespace TankRevival
{
    public enum MultiStageOperationPhase
    {
        CaptureSector,
        InterdictCommand,
        FinalHold,
        Complete
    }

    public enum MultiStageOperationDoctrine
    {
        Spearhead,
        CounterSiege,
        DeepStrike
    }

    public sealed class MultiStageOperationDirector : MonoBehaviour
    {
        public const int EarliestOperationRound = 15;
        public const int OperationInterval = 8;
        public const float CaptureRadius = 1.75f;
        public const float ContestRadius = 2.15f;
        public const float CaptureSeconds = 7.5f;
        public const float FinalHoldSeconds = 8.5f;
        public const float EnemyContestRate = 0.55f;
        public const int MaxContesters = 4;
        public const int MinReward = 10;
        public const int MaxReward = 22;

        private TankGame _game;
        private int _round = -1;
        private MultiStageOperationDoctrine _doctrine;
        private MultiStageOperationPhase _phase;
        private Vector2 _phasePosition;
        private float _progress;
        private GameObject _markerRoot;
        private EnemyTank _commandTarget;
        private Health _commandHealth;
        private float _nextTargetSearch;
        private string _status = string.Empty;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _warning;

        public static int DoctrineCount => Enum.GetValues(typeof(MultiStageOperationDoctrine)).Length;
        public static int PlayablePhaseCount => 3;
        public static bool ConfigurationValid =>
            EarliestOperationRound >= 12 && EarliestOperationRound <= 25 &&
            OperationInterval >= 6 && OperationInterval <= 12 &&
            CaptureRadius >= 1.3f && CaptureRadius <= 2.3f &&
            ContestRadius >= CaptureRadius && ContestRadius <= 2.8f &&
            CaptureSeconds >= 5f && CaptureSeconds <= 11f &&
            FinalHoldSeconds >= 6f && FinalHoldSeconds <= 12f &&
            EnemyContestRate >= 0.30f && EnemyContestRate <= 0.85f &&
            MaxContesters >= 2 && MaxContesters <= 6 &&
            MinReward >= 8 && MaxReward <= 25 && MinReward < MaxReward;

        public static bool HasOperationForRound(int round)
        {
            return round >= EarliestOperationRound && round % OperationInterval == 0 && round % 10 != 0;
        }

        public static MultiStageOperationDoctrine DoctrineForRound(int round)
        {
            int slot = Mathf.Abs(round / OperationInterval) % DoctrineCount;
            return (MultiStageOperationDoctrine)slot;
        }

        public static int RewardForRound(int round)
        {
            return Mathf.Clamp(MinReward + round / 9, MinReward, MaxReward);
        }

        public static float ContestDelta(float deltaTime, int enemyCount)
        {
            int bounded = Mathf.Clamp(enemyCount, 0, MaxContesters);
            return deltaTime * bounded * EnemyContestRate;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<MultiStageOperationDirector>() != null) return;
            var go = new GameObject("MultiStageOperationDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<MultiStageOperationDirector>();
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetOperation();
                return;
            }

            if (_game.CurrentRound != _round)
            {
                ResetOperation();
                _round = _game.CurrentRound;
                if (HasOperationForRound(_round)) BeginOperation();
            }

            if (!HasOperationForRound(_round) || _phase == MultiStageOperationPhase.Complete) return;

            switch (_phase)
            {
                case MultiStageOperationPhase.CaptureSector:
                    UpdateCapturePhase();
                    break;
                case MultiStageOperationPhase.InterdictCommand:
                    UpdateInterdictionPhase();
                    break;
                case MultiStageOperationPhase.FinalHold:
                    UpdateFinalHoldPhase();
                    break;
            }
        }

        private void BeginOperation()
        {
            _doctrine = DoctrineForRound(_round);
            _phase = MultiStageOperationPhase.CaptureSector;
            _progress = 0f;
            _status = "SECURE FORWARD CONTROL POINT";
            _phasePosition = PositionFor(_round, 0);
            BuildZoneMarker("CONTROL_POINT", _phasePosition, new Color(0.14f, 0.78f, 1f));
            BattleAudio.PlayGlobal(SoundCue.RoundStart, 0.42f, 0f);
        }

        private void UpdateCapturePhase()
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead) return;

            bool playerInside = Vector2.Distance(player.transform.position, _phasePosition) <= CaptureRadius;
            int contesters = CountEnemyContesters(_phasePosition);
            if (playerInside) _progress += Time.deltaTime;
            else _progress -= Time.deltaTime * 0.22f;
            _progress -= ContestDelta(Time.deltaTime, contesters);
            _progress = Mathf.Clamp(_progress, 0f, CaptureSeconds);
            _status = contesters > 0 ? "SECTOR CONTESTED // CLEAR HOSTILES" : (playerInside ? "CAPTURING SECTOR" : "ENTER CONTROL ZONE");

            if (_progress >= CaptureSeconds)
                BeginInterdiction();
        }

        private void BeginInterdiction()
        {
            DestroyMarker();
            _phase = MultiStageOperationPhase.InterdictCommand;
            _progress = 0f;
            _status = "LOCATING ENEMY COMMAND VEHICLE";
            _nextTargetSearch = 0f;
            AcquireCommandTarget();
        }

        private void UpdateInterdictionPhase()
        {
            if (_commandHealth != null && !_commandHealth.IsDead)
            {
                _status = "DESTROY MARKED COMMAND VEHICLE";
                return;
            }

            if (_commandHealth != null && _commandHealth.IsDead)
            {
                BeginFinalHold();
                return;
            }

            if (Time.time >= _nextTargetSearch)
            {
                AcquireCommandTarget();
                _nextTargetSearch = Time.time + 0.75f;
            }
        }

        private void AcquireCommandTarget()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            EnemyTank best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (enemy.Kind == EnemyKind.Boss || enemy.Kind == EnemyKind.Supply) continue;

                int score = Priority(enemy.Kind) * 100 - Mathf.RoundToInt(Vector2.Distance(enemy.transform.position, _game.BasePosition) * 3f);
                if (score <= bestScore) continue;
                bestScore = score;
                best = enemy;
            }

            if (best == null) return;
            DetachCommandTarget();
            _commandTarget = best;
            _commandHealth = best.Health;
            _commandHealth.Died += OnCommandTargetDestroyed;
            _status = "COMMAND TARGET ACQUIRED";
            VisualFactory.RingPulse(best.transform.position, new Color(1f, 0.22f, 0.10f), 1.45f);
            BuildAttachedTargetMarker(best.transform);
        }

        private static int Priority(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Elite: return 6;
                case EnemyKind.Siege: return 5;
                case EnemyKind.Sniper: return 4;
                case EnemyKind.Heavy: return 3;
                case EnemyKind.Fast: return 2;
                default: return 1;
            }
        }

        private void OnCommandTargetDestroyed(Health target)
        {
            if (_phase == MultiStageOperationPhase.InterdictCommand) BeginFinalHold();
        }

        private void BeginFinalHold()
        {
            DetachCommandTarget();
            DestroyMarker();
            _phase = MultiStageOperationPhase.FinalHold;
            _progress = 0f;
            _phasePosition = PositionFor(_round, 1);
            _status = "HOLD EXTRACTION CORRIDOR";
            BuildZoneMarker("FINAL_HOLD", _phasePosition, new Color(0.24f, 1f, 0.48f));
            BattleAudio.PlayGlobal(SoundCue.Powerup, 0.46f, 0.05f);
        }

        private void UpdateFinalHoldPhase()
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead) return;

            bool playerInside = Vector2.Distance(player.transform.position, _phasePosition) <= CaptureRadius;
            int contesters = CountEnemyContesters(_phasePosition);
            if (playerInside) _progress += Time.deltaTime;
            _progress -= ContestDelta(Time.deltaTime, contesters) * 0.65f;
            _progress = Mathf.Clamp(_progress, 0f, FinalHoldSeconds);
            _status = contesters > 0 ? "EXTRACTION CONTESTED" : (playerInside ? "HOLDING EXTRACTION" : "RETURN TO EXTRACTION ZONE");

            if (_progress >= FinalHoldSeconds) CompleteOperation();
        }

        private int CountEnemyContesters(Vector2 point)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int count = 0;
            for (int i = 0; i < enemies.Length && count < MaxContesters; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (Vector2.Distance(enemy.transform.position, point) <= ContestRadius) count++;
            }
            return count;
        }

        private void CompleteOperation()
        {
            _phase = MultiStageOperationPhase.Complete;
            _status = "OPERATION COMPLETE";
            int reward = RewardForRound(_round);
            WarEconomyDirector.AwardMissionBonds(reward, "MULTI-STAGE OPERATION");
            VisualFactory.RingPulse(_phasePosition, new Color(0.22f, 1f, 0.48f), 2.1f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.58f, 0.04f);
            DestroyMarker();
        }

        private static Vector2 PositionFor(int round, int phaseIndex)
        {
            var rng = new System.Random(round * 6151 + phaseIndex * 997 + 41);
            float x = (float)(rng.NextDouble() * 14.0 - 7.0);
            float y = (float)(rng.NextDouble() * 5.6 - 0.6);
            if (Mathf.Abs(x) < 1.4f) x += x >= 0f ? 2.1f : -2.1f;
            return new Vector2(Mathf.Clamp(x, -8.4f, 8.4f), Mathf.Clamp(y, -1.0f, 4.4f));
        }

        private void BuildZoneMarker(string name, Vector2 position, Color color)
        {
            DestroyMarker();
            _markerRoot = new GameObject(name);
            _markerRoot.transform.SetParent(transform, false);
            _markerRoot.transform.position = position;
            VisualFactory.Disc("ZoneFill", _markerRoot.transform, Vector2.one * CaptureRadius * 2f, new Color(color.r, color.g, color.b, 0.15f), Vector3.zero, -3);
            VisualFactory.Rect("Beacon", _markerRoot.transform, new Vector2(0.12f, 1.15f), color, Vector3.zero, 6);
            VisualFactory.Disc("Core", _markerRoot.transform, new Vector2(0.38f, 0.38f), Color.white, new Vector3(0f, 0.5f, 0f), 7);
            VisualFactory.RingPulse(position, color, CaptureRadius * 1.25f);
        }

        private void BuildAttachedTargetMarker(Transform target)
        {
            DestroyMarker();
            _markerRoot = new GameObject("COMMAND_TARGET_MARKER");
            _markerRoot.transform.SetParent(target, false);
            _markerRoot.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            VisualFactory.Disc("CommandCore", _markerRoot.transform, new Vector2(0.26f, 0.26f), new Color(1f, 0.18f, 0.08f), Vector3.zero, 9);
            VisualFactory.Rect("CommandStem", _markerRoot.transform, new Vector2(0.09f, 0.52f), new Color(1f, 0.68f, 0.10f), new Vector3(0f, 0.22f, 0f), 8);
        }

        private void DestroyMarker()
        {
            if (_markerRoot != null) Destroy(_markerRoot);
            _markerRoot = null;
        }

        private void DetachCommandTarget()
        {
            if (_commandHealth != null) _commandHealth.Died -= OnCommandTargetDestroyed;
            _commandHealth = null;
            _commandTarget = null;
        }

        private void ResetOperation()
        {
            DetachCommandTarget();
            DestroyMarker();
            _round = -1;
            _progress = 0f;
            _status = string.Empty;
            _phase = MultiStageOperationPhase.Complete;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.40f, 0.92f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
            _warning = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.55f, 0.22f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || !HasOperationForRound(_round) || _phase == MultiStageOperationPhase.Complete) return;
            EnsureStyles();

            GUI.color = new Color(0.02f, 0.05f, 0.08f, 0.93f);
            GUI.Box(new Rect(18f, Screen.height - 126f, 408f, 108f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(30f, Screen.height - 120f, 380f, 22f), "MULTI-STAGE OP // " + _doctrine.ToString().ToUpperInvariant(), _header);
            GUI.Label(new Rect(30f, Screen.height - 96f, 380f, 20f), "PHASE " + ((int)_phase + 1) + "/3 // " + _phase, _body);
            GUI.Label(new Rect(30f, Screen.height - 74f, 380f, 20f), _status, _warning);

            string progress = string.Empty;
            if (_phase == MultiStageOperationPhase.CaptureSector) progress = _progress.ToString("0.0") + " / " + CaptureSeconds.ToString("0.0") + "s";
            else if (_phase == MultiStageOperationPhase.InterdictCommand) progress = _commandHealth == null ? "WAITING FOR VALID COMMAND TARGET" : "TARGET HP " + _commandHealth.Current + "/" + _commandHealth.Maximum;
            else if (_phase == MultiStageOperationPhase.FinalHold) progress = _progress.ToString("0.0") + " / " + FinalHoldSeconds.ToString("0.0") + "s";
            GUI.Label(new Rect(30f, Screen.height - 50f, 380f, 20f), progress + "   REWARD " + RewardForRound(_round) + " BONDS", _body);
        }
    }
}
