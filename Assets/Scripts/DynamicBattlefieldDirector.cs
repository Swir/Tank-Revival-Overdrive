using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum BattlefieldObjectiveKind
    {
        SecureRelay,
        DemolishArtilleryUplink,
        RestoreFortification
    }

    public enum BattlefieldHazardKind
    {
        ArtilleryDangerZone,
        Minefield
    }

    public sealed class DynamicBattlefieldDirector : MonoBehaviour
    {
        public const int EarliestObjectiveRound = 6;
        public const int ObjectiveInterval = 3;
        public const float ObjectiveRadius = 1.65f;
        public const float SecureSeconds = 7.5f;
        public const float RestoreSeconds = 6.5f;
        public const float ArtilleryTelegraphSeconds = 2.8f;
        public const float ArtilleryRadius = 1.85f;
        public const float HazardCadence = 13.0f;
        public const int MaxMinefields = 2;

        private TankGame _game;
        private int _round = -1;
        private BattlefieldObjectiveKind _kind;
        private Vector2 _objectivePosition;
        private GameObject _objectiveRoot;
        private Health _objectiveHealth;
        private float _objectiveProgress;
        private bool _objectiveComplete;
        private float _nextHazard;
        private readonly List<Minefield> _mines = new List<Minefield>(MaxMinefields);
        private ArtilleryWarning _artillery;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _warning;

        public static int ObjectiveCount => Enum.GetValues(typeof(BattlefieldObjectiveKind)).Length;
        public static int HazardCount => Enum.GetValues(typeof(BattlefieldHazardKind)).Length;
        public static bool ConfigurationValid =>
            EarliestObjectiveRound >= 4 && EarliestObjectiveRound <= 10 &&
            ObjectiveInterval >= 2 && ObjectiveInterval <= 5 &&
            ObjectiveRadius >= 1.2f && ObjectiveRadius <= 2.2f &&
            SecureSeconds >= 5f && SecureSeconds <= 10f &&
            RestoreSeconds >= 5f && RestoreSeconds <= 10f &&
            ArtilleryTelegraphSeconds >= 2.0f && ArtilleryTelegraphSeconds <= 4.5f &&
            ArtilleryRadius >= 1.2f && ArtilleryRadius <= 2.4f &&
            HazardCadence >= 9f && HazardCadence <= 18f &&
            MaxMinefields >= 1 && MaxMinefields <= 3;

        public static bool HasObjectiveForRound(int round) => round >= EarliestObjectiveRound && round % ObjectiveInterval == 0 && round % 10 != 0;
        public static BattlefieldObjectiveKind ObjectiveForRound(int round) => (BattlefieldObjectiveKind)(Mathf.Abs(round / ObjectiveInterval) % ObjectiveCount);
        public static int RewardForRound(int round) => Mathf.Clamp(5 + round / 12, 5, 13);
        public static int DemolitionHealthForRound(int round) => Mathf.Clamp(4 + round / 16, 4, 10);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<DynamicBattlefieldDirector>() != null) return;
            var go = new GameObject("DynamicBattlefieldDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<DynamicBattlefieldDirector>();
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetRoundState();
                return;
            }

            int currentRound = _game.CurrentRound;
            if (currentRound != _round)
            {
                ResetRoundState();
                _round = currentRound;
                BeginRound(currentRound);
            }

            UpdateObjective();
            UpdateHazards();
        }

        private void BeginRound(int round)
        {
            _nextHazard = Time.time + Mathf.Lerp(10f, 6.5f, Mathf.Clamp01(round / 100f));
            SpawnMinefields(round);
            if (!HasObjectiveForRound(round)) return;

            _kind = ObjectiveForRound(round);
            _objectivePosition = ObjectivePosition(round, _kind);
            _objectiveRoot = new GameObject("BATTLEFIELD_OBJECTIVE_" + _kind);
            _objectiveRoot.transform.position = _objectivePosition;
            _objectiveRoot.transform.SetParent(transform, true);

            Color color = _kind == BattlefieldObjectiveKind.SecureRelay
                ? new Color(0.15f, 0.82f, 1f)
                : _kind == BattlefieldObjectiveKind.DemolishArtilleryUplink
                    ? new Color(1f, 0.28f, 0.10f)
                    : new Color(0.20f, 1f, 0.50f);

            VisualFactory.Disc("ObjectiveZone", _objectiveRoot.transform, Vector2.one * ObjectiveRadius * 2f, new Color(color.r, color.g, color.b, 0.16f), Vector3.zero, -3);
            VisualFactory.RingPulse(_objectivePosition, color, ObjectiveRadius * 1.35f);
            VisualFactory.Rect("ObjectiveBeacon", _objectiveRoot.transform, new Vector2(0.16f, 1.1f), color, Vector3.zero, 6);
            VisualFactory.Disc("ObjectiveCore", _objectiveRoot.transform, new Vector2(0.42f, 0.42f), Color.white, new Vector3(0f, 0.48f, 0f), 7);

            if (_kind == BattlefieldObjectiveKind.DemolishArtilleryUplink)
            {
                BoxCollider2D collider = _objectiveRoot.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.95f, 0.95f);
                _objectiveHealth = _objectiveRoot.AddComponent<Health>();
                _objectiveHealth.Initialize(Team.Enemy, DemolitionHealthForRound(round));
                _objectiveHealth.Died += OnObjectiveTargetDestroyed;
            }
        }

        private static Vector2 ObjectivePosition(int round, BattlefieldObjectiveKind kind)
        {
            var rng = new System.Random(round * 3571 + (int)kind * 211);
            float x = (float)(rng.NextDouble() * 12.0 - 6.0);
            float y = (float)(rng.NextDouble() * 5.0 - 0.5);
            if (Mathf.Abs(x) < 1.5f) x += x >= 0f ? 1.8f : -1.8f;
            return new Vector2(Mathf.Clamp(x, -8.5f, 8.5f), Mathf.Clamp(y, -1.2f, 4.1f));
        }

        private void UpdateObjective()
        {
            if (_objectiveRoot == null || _objectiveComplete) return;
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead)
            {
                _objectiveProgress = 0f;
                return;
            }

            bool inside = Vector2.Distance(player.transform.position, _objectivePosition) <= ObjectiveRadius;
            if (_kind == BattlefieldObjectiveKind.SecureRelay)
            {
                _objectiveProgress = inside ? Mathf.Min(SecureSeconds, _objectiveProgress + Time.deltaTime) : Mathf.Max(0f, _objectiveProgress - Time.deltaTime * 0.35f);
                if (_objectiveProgress >= SecureSeconds) CompleteObjective("RELAY SECURED");
            }
            else if (_kind == BattlefieldObjectiveKind.RestoreFortification)
            {
                _objectiveProgress = inside ? Mathf.Min(RestoreSeconds, _objectiveProgress + Time.deltaTime) : Mathf.Max(0f, _objectiveProgress - Time.deltaTime * 0.22f);
                if (_objectiveProgress >= RestoreSeconds)
                {
                    _game.RepairEagle(1);
                    CompleteObjective("FORTIFICATION RESTORED");
                }
            }
            else if (_objectiveHealth != null && _objectiveHealth.IsDead)
            {
                CompleteObjective("ARTILLERY UPLINK DESTROYED");
            }
        }

        private void OnObjectiveTargetDestroyed(Health target)
        {
            if (!_objectiveComplete) CompleteObjective("ARTILLERY UPLINK DESTROYED");
        }

        private void CompleteObjective(string reason)
        {
            if (_objectiveComplete) return;
            _objectiveComplete = true;
            WarEconomyDirector.AwardMissionBonds(RewardForRound(_round), reason);
            if (_objectiveRoot != null) VisualFactory.RingPulse(_objectiveRoot.transform.position, new Color(0.18f, 1f, 0.54f), 1.9f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.52f, 0.05f);
        }

        private void SpawnMinefields(int round)
        {
            if (round < EarliestObjectiveRound) return;
            int count = round >= 45 ? MaxMinefields : 1;
            var rng = new System.Random(round * 881 + 73);
            for (int i = 0; i < count; i++)
            {
                Vector2 pos = new Vector2((float)(rng.NextDouble() * 15.0 - 7.5), (float)(rng.NextDouble() * 5.8 - 0.8));
                if (Vector2.Distance(pos, new Vector2(0f, -4.05f)) < 2.6f) pos.y += 3f;
                var root = new GameObject("Minefield_" + i);
                root.transform.SetParent(transform, false);
                root.transform.position = pos;
                VisualFactory.Disc("MineWarning", root.transform, Vector2.one * 1.35f, new Color(1f, 0.62f, 0.08f, 0.12f), Vector3.zero, -4);
                for (int m = 0; m < 5; m++)
                {
                    float a = m / 5f * Mathf.PI * 2f;
                    Vector3 local = new Vector3(Mathf.Cos(a) * 0.42f, Mathf.Sin(a) * 0.42f, 0f);
                    VisualFactory.Disc("Mine", root.transform, new Vector2(0.13f, 0.13f), new Color(0.95f, 0.38f, 0.08f), local, 4);
                }
                _mines.Add(new Minefield(root, pos));
            }
        }

        private void UpdateHazards()
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead) return;

            for (int i = 0; i < _mines.Count; i++)
            {
                Minefield mine = _mines[i];
                if (mine.Triggered || mine.Root == null) continue;
                if (Vector2.Distance(player.transform.position, mine.Position) <= 0.82f)
                {
                    mine.Triggered = true;
                    player.Health.Damage(1, Team.Enemy);
                    VisualFactory.Explosion(mine.Position, new Color(1f, 0.38f, 0.06f), 0.9f);
                    Destroy(mine.Root);
                }
            }

            if (_artillery.Active)
            {
                if (Time.time >= _artillery.StrikeAt) ResolveArtilleryStrike(player);
                return;
            }

            if (_round < 12 || Time.time < _nextHazard) return;
            Vector2 target = player.transform.position;
            target.x = Mathf.Clamp(target.x + UnityEngine.Random.Range(-0.8f, 0.8f), -9.5f, 9.5f);
            target.y = Mathf.Clamp(target.y + UnityEngine.Random.Range(-0.6f, 0.8f), -4.5f, 5.0f);
            BeginArtilleryWarning(target);
            _nextHazard = Time.time + HazardCadence;
        }

        private void BeginArtilleryWarning(Vector2 position)
        {
            GameObject root = new GameObject("ARTILLERY_DANGER_ZONE");
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            VisualFactory.Disc("DangerFill", root.transform, Vector2.one * ArtilleryRadius * 2f, new Color(1f, 0.08f, 0.04f, 0.13f), Vector3.zero, -2);
            VisualFactory.Disc("DangerCore", root.transform, new Vector2(0.22f, 0.22f), new Color(1f, 0.72f, 0.15f), Vector3.zero, 5);
            VisualFactory.RingPulse(position, new Color(1f, 0.12f, 0.05f), ArtilleryRadius * 1.15f);
            _artillery = new ArtilleryWarning(root, position, Time.time + ArtilleryTelegraphSeconds);
            BattleAudio.PlayGlobal(SoundCue.EagleAlarm, 0.34f, 0f);
        }

        private void ResolveArtilleryStrike(PlayerTank player)
        {
            Vector2 pos = _artillery.Position;
            if (player != null && player.Health != null && !player.Health.IsDead && Vector2.Distance(player.transform.position, pos) <= ArtilleryRadius)
                player.Health.Damage(1, Team.Enemy);

            Health[] units = RuntimeBattleRegistry.HealthSnapshot;
            int affected = 0;
            for (int i = 0; i < units.Length && affected < 5; i++)
            {
                Health health = units[i];
                if (health == null || health.IsDead || health.Team != Team.Enemy || health == _objectiveHealth) continue;
                if (Vector2.Distance(health.transform.position, pos) > ArtilleryRadius) continue;
                if (health.Damage(1, Team.Player)) affected++;
            }

            VisualFactory.Explosion(pos, new Color(1f, 0.20f, 0.04f), 1.65f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.70f, 0f);
            if (_artillery.Root != null) Destroy(_artillery.Root);
            _artillery = default(ArtilleryWarning);
        }

        private void ResetRoundState()
        {
            if (_objectiveRoot != null) Destroy(_objectiveRoot);
            if (_artillery.Root != null) Destroy(_artillery.Root);
            for (int i = 0; i < _mines.Count; i++) if (_mines[i].Root != null) Destroy(_mines[i].Root);
            _mines.Clear();
            _objectiveRoot = null;
            _objectiveHealth = null;
            _objectiveProgress = 0f;
            _objectiveComplete = false;
            _artillery = default(ArtilleryWarning);
            _round = -1;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.35f, 0.90f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
            _warning = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.38f, 0.18f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            if (_objectiveRoot != null && !_objectiveComplete)
            {
                string label;
                string progress;
                if (_kind == BattlefieldObjectiveKind.SecureRelay)
                {
                    label = "SECURE RELAY // HOLD THE ZONE";
                    progress = _objectiveProgress.ToString("0.0") + " / " + SecureSeconds.ToString("0.0") + "s";
                }
                else if (_kind == BattlefieldObjectiveKind.DemolishArtilleryUplink)
                {
                    label = "DEMOLISH ARTILLERY UPLINK";
                    progress = _objectiveHealth == null ? "TARGET LOST" : "UPLINK HP " + _objectiveHealth.Current + "/" + _objectiveHealth.Maximum;
                }
                else
                {
                    label = "RESTORE FORWARD FORTIFICATION // HOLD";
                    progress = _objectiveProgress.ToString("0.0") + " / " + RestoreSeconds.ToString("0.0") + "s";
                }

                GUI.color = new Color(0.025f, 0.055f, 0.075f, 0.92f);
                GUI.Box(new Rect(Screen.width - 405f, 18f, 387f, 70f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width - 392f, 25f, 360f, 20f), "DYNAMIC OBJECTIVE // +" + RewardForRound(_round) + " BONDS", _header);
                GUI.Label(new Rect(Screen.width - 392f, 47f, 360f, 18f), label, _body);
                GUI.Label(new Rect(Screen.width - 392f, 65f, 360f, 18f), progress, _body);
            }

            if (_artillery.Active)
            {
                float left = Mathf.Max(0f, _artillery.StrikeAt - Time.time);
                GUI.Label(new Rect(Screen.width * 0.5f - 180f, 92f, 360f, 24f), "ARTILLERY DANGER // MOVE // " + left.ToString("0.0") + "s", _warning);
            }
        }

        private sealed class Minefield
        {
            public readonly GameObject Root;
            public readonly Vector2 Position;
            public bool Triggered;
            public Minefield(GameObject root, Vector2 position) { Root = root; Position = position; }
        }

        private struct ArtilleryWarning
        {
            public readonly GameObject Root;
            public readonly Vector2 Position;
            public readonly float StrikeAt;
            public bool Active => Root != null;
            public ArtilleryWarning(GameObject root, Vector2 position, float strikeAt) { Root = root; Position = position; StrikeAt = strikeAt; }
        }
    }
}
