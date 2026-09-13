using System;
using UnityEngine;

namespace TankRevival
{
    public enum ConvoyMissionKind
    {
        EagleEscort,
        EnemyInterdiction,
        MobileResupply
    }

    public enum ConvoyThreatState
    {
        Moving,
        AmbushWarning,
        UnderFire,
        Critical,
        Complete,
        Failed
    }

    /// <summary>
    /// v6.2 mobile objective layer. Convoys use the existing Health and Projectile authorities,
    /// while mission rewards flow through WarEconomyDirector. The director never replaces TankGame
    /// round ownership and deliberately avoids boss rounds and v6.1 multi-stage operation rounds.
    /// </summary>
    public sealed class ConvoyWarfareDirector : MonoBehaviour
    {
        public const int EarliestMissionRound = 22;
        public const int MissionInterval = 6;
        public const float RouteSecondsMin = 18f;
        public const float RouteSecondsMax = 27f;
        public const int FriendlyHealthMin = 7;
        public const int FriendlyHealthMax = 13;
        public const int EnemyHealthMin = 8;
        public const int EnemyHealthMax = 15;
        public const float AmbushCadenceMin = 5.5f;
        public const float AmbushCadenceMax = 8.5f;
        public const float AmbushTelegraphSeconds = 1.25f;
        public const int AmbushShellsMin = 2;
        public const int AmbushShellsMax = 4;
        public const float ResupplyRadius = 2.4f;
        public const float ResupplyPulseSeconds = 5.5f;
        public const int RewardMin = 10;
        public const int RewardMax = 24;

        private TankGame _game;
        private int _round = -1;
        private ConvoyMissionKind _kind;
        private ConvoyThreatState _threat;
        private GameObject _convoyRoot;
        private Rigidbody2D _convoyBody;
        private Health _convoyHealth;
        private Vector2 _routeStart;
        private Vector2 _routeEnd;
        private float _routeDuration;
        private float _routeElapsed;
        private float _nextAmbush;
        private float _ambushFireAt = -1f;
        private float _nextSupportPulse;
        private float _lastProgressTime;
        private float _lastProgress;
        private bool _missionResolved;
        private bool _midpointSupportGranted;
        private string _status = string.Empty;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _warning;

        public static int MissionCount => Enum.GetValues(typeof(ConvoyMissionKind)).Length;

        public static bool ConfigurationValid =>
            EarliestMissionRound >= 18 && EarliestMissionRound <= 30 &&
            MissionInterval >= 5 && MissionInterval <= 9 &&
            RouteSecondsMin >= 15f && RouteSecondsMax <= 32f && RouteSecondsMin < RouteSecondsMax &&
            FriendlyHealthMin >= 5 && FriendlyHealthMax <= 16 && FriendlyHealthMin < FriendlyHealthMax &&
            EnemyHealthMin >= 6 && EnemyHealthMax <= 18 && EnemyHealthMin < EnemyHealthMax &&
            AmbushCadenceMin >= 4f && AmbushCadenceMax <= 11f && AmbushCadenceMin < AmbushCadenceMax &&
            AmbushTelegraphSeconds >= 0.8f && AmbushTelegraphSeconds <= 2.4f &&
            AmbushShellsMin >= 1 && AmbushShellsMax <= 5 && AmbushShellsMin <= AmbushShellsMax &&
            ResupplyRadius >= 1.7f && ResupplyRadius <= 3.0f &&
            ResupplyPulseSeconds >= 4f && ResupplyPulseSeconds <= 8f &&
            RewardMin >= 8 && RewardMax <= 28 && RewardMin < RewardMax;

        public static bool HasMissionForRound(int round)
        {
            if (round < EarliestMissionRound || round % 10 == 0) return false;
            if ((round - EarliestMissionRound) % MissionInterval != 0) return false;
            return !MultiStageOperationDirector.HasOperationForRound(round);
        }

        public static ConvoyMissionKind MissionForRound(int round)
        {
            int slot = Mathf.Abs((round - EarliestMissionRound) / MissionInterval) % MissionCount;
            return (ConvoyMissionKind)slot;
        }

        public static int RewardForRound(int round)
        {
            return Mathf.Clamp(RewardMin + round / 8, RewardMin, RewardMax);
        }

        public static int ConvoyHealthForRound(int round, ConvoyMissionKind kind)
        {
            int r = Mathf.Clamp(round, 1, 100);
            if (kind == ConvoyMissionKind.EnemyInterdiction)
                return Mathf.Clamp(EnemyHealthMin + r / 18, EnemyHealthMin, EnemyHealthMax);
            return Mathf.Clamp(FriendlyHealthMin + r / 22, FriendlyHealthMin, FriendlyHealthMax);
        }

        public static float RouteSecondsForRound(int round)
        {
            float t = Mathf.Clamp01((round - EarliestMissionRound) / 78f);
            return Mathf.Lerp(RouteSecondsMin, RouteSecondsMax, t);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ConvoyWarfareDirector>() != null) return;
            var go = new GameObject("ConvoyWarfareDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<ConvoyWarfareDirector>();
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetMission();
                return;
            }

            if (_game.CurrentRound != _round)
            {
                ResetMission();
                _round = _game.CurrentRound;
                if (HasMissionForRound(_round)) BeginMission();
            }

            if (!HasMissionForRound(_round) || _missionResolved || _convoyHealth == null) return;
            if (_convoyHealth.IsDead)
            {
                ResolveDestroyedConvoy();
                return;
            }

            UpdateRoute();
            UpdateMissionPressure();
            UpdateThreatState();
        }

        private void BeginMission()
        {
            _kind = MissionForRound(_round);
            _missionResolved = false;
            _routeElapsed = 0f;
            _routeDuration = RouteSecondsForRound(_round);
            _lastProgress = 0f;
            _lastProgressTime = Time.time;
            _midpointSupportGranted = false;
            _ambushFireAt = -1f;
            _nextAmbush = Time.time + 3.2f;
            _nextSupportPulse = Time.time + ResupplyPulseSeconds;
            ConfigureRoute(_round, _kind, out _routeStart, out _routeEnd);
            SpawnConvoy();
            _threat = ConvoyThreatState.Moving;
            _status = MissionIntro(_kind);
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.25f, -0.03f);
            VisualFactory.RingPulse(_routeStart, MissionColor(_kind), 1.8f);
        }

        private void ConfigureRoute(int round, ConvoyMissionKind kind, out Vector2 start, out Vector2 end)
        {
            var rng = new System.Random(round * 9281 + (int)kind * 431 + 73);
            float lane = (float)(rng.NextDouble() * 4.2 - 0.3);
            float wobble = (float)(rng.NextDouble() * 1.4 - 0.7);

            if (kind == ConvoyMissionKind.EnemyInterdiction)
            {
                start = new Vector2(rng.Next(0, 2) == 0 ? -8.5f : 8.5f, Mathf.Clamp(lane + 1.0f, 0.4f, 4.8f));
                Vector2 eagle = _game != null ? _game.BasePosition : new Vector2(0f, -3.7f);
                end = eagle + new Vector2(Mathf.Sign(start.x) * 0.8f, 0.9f);
            }
            else
            {
                bool leftToRight = rng.Next(0, 2) == 0;
                start = new Vector2(leftToRight ? -8.4f : 8.4f, Mathf.Clamp(lane, -0.4f, 4.1f));
                end = new Vector2(leftToRight ? 8.4f : -8.4f, Mathf.Clamp(lane + wobble, -0.4f, 4.1f));
            }
        }

        private void SpawnConvoy()
        {
            _convoyRoot = new GameObject("CONVOY_" + _kind.ToString().ToUpperInvariant());
            _convoyRoot.transform.position = _routeStart;

            Color body = _kind == ConvoyMissionKind.EnemyInterdiction
                ? new Color(0.58f, 0.10f, 0.07f)
                : _kind == ConvoyMissionKind.MobileResupply
                    ? new Color(0.12f, 0.46f, 0.68f)
                    : new Color(0.16f, 0.52f, 0.25f);
            Color accent = MissionColor(_kind);

            VisualFactory.Rect("ConvoyHull", _convoyRoot.transform, new Vector2(1.14f, 0.72f), body, Vector3.zero, 8);
            VisualFactory.Rect("CargoDeck", _convoyRoot.transform, new Vector2(0.62f, 0.48f), Color.Lerp(body, Color.white, 0.22f), new Vector3(0f, 0.12f, 0f), 9);
            VisualFactory.Disc("ConvoyBeacon", _convoyRoot.transform, new Vector2(0.20f, 0.20f), accent, new Vector3(0f, 0.50f, 0f), 10);
            VisualFactory.Rect("RouteAntenna", _convoyRoot.transform, new Vector2(0.06f, 0.54f), accent, new Vector3(0.28f, 0.40f, 0f), 9);

            var collider = _convoyRoot.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.04f, 0.66f);

            _convoyBody = _convoyRoot.AddComponent<Rigidbody2D>();
            _convoyBody.gravityScale = 0f;
            _convoyBody.bodyType = RigidbodyType2D.Kinematic;
            _convoyBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _convoyBody.interpolation = RigidbodyInterpolation2D.Interpolate;

            _convoyHealth = _convoyRoot.AddComponent<Health>();
            Team team = _kind == ConvoyMissionKind.EnemyInterdiction ? Team.Enemy : Team.Player;
            _convoyHealth.Initialize(team, ConvoyHealthForRound(_round, _kind));
            _convoyHealth.Damaged += OnConvoyDamaged;
            _convoyHealth.Died += OnConvoyDied;
        }

        private void UpdateRoute()
        {
            if (_convoyRoot == null || _convoyBody == null) return;

            _routeElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_routeElapsed / Mathf.Max(1f, _routeDuration));
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            Vector2 target = Vector2.Lerp(_routeStart, _routeEnd, eased);
            _convoyBody.MovePosition(target);

            Vector2 direction = (_routeEnd - _routeStart).normalized;
            if (direction.sqrMagnitude > 0.01f)
                _convoyRoot.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

            // Route movement is parameter-driven rather than collision-driven. This conservative recovery
            // protects the objective from physics stalls without granting health or skipping combat pressure.
            if (progress > _lastProgress + 0.015f)
            {
                _lastProgress = progress;
                _lastProgressTime = Time.time;
            }
            else if (Time.time - _lastProgressTime > 4f)
            {
                _routeElapsed = Mathf.Min(_routeElapsed + 0.35f, _routeDuration);
                _lastProgressTime = Time.time;
            }

            if (!_midpointSupportGranted && progress >= 0.50f && _kind == ConvoyMissionKind.EagleEscort)
            {
                _midpointSupportGranted = true;
                GrantEscortSupport();
            }

            if (progress >= 1f) ResolveRouteArrival();
        }

        private void UpdateMissionPressure()
        {
            if (_kind == ConvoyMissionKind.EnemyInterdiction)
            {
                UpdateEnemyLogisticsSupport();
                if (Time.time >= _nextAmbush) FireScreeningShotAtPlayer();
                return;
            }

            if (_kind == ConvoyMissionKind.MobileResupply && Time.time >= _nextSupportPulse)
            {
                _nextSupportPulse = Time.time + ResupplyPulseSeconds;
                PulseMobileResupply();
            }

            if (_ambushFireAt > 0f && Time.time >= _ambushFireAt)
            {
                _ambushFireAt = -1f;
                FireAmbushSalvo();
                _nextAmbush = Time.time + AmbushCadenceForRound();
                return;
            }

            if (_ambushFireAt < 0f && Time.time >= _nextAmbush)
            {
                _ambushFireAt = Time.time + AmbushTelegraphSeconds;
                _threat = ConvoyThreatState.AmbushWarning;
                _status = "AMBUSH INCOMING // SCREEN THE CONVOY";
                VisualFactory.RingPulse(_convoyRoot.transform.position, new Color(1f, 0.24f, 0.08f), 1.65f);
                BattleAudio.PlayGlobal(SoundCue.EnemyShot, 0.16f, -0.12f);
            }
        }

        private void UpdateEnemyLogisticsSupport()
        {
            if (Time.time < _nextSupportPulse || _convoyRoot == null) return;
            _nextSupportPulse = Time.time + ResupplyPulseSeconds;

            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int healed = 0;
            for (int i = 0; i < enemies.Length && healed < 2; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (enemy.Kind == EnemyKind.Boss || enemy.Kind == EnemyKind.Supply) continue;
                if (Vector2.Distance(enemy.transform.position, _convoyRoot.transform.position) > ResupplyRadius) continue;
                if (enemy.Health.Current >= enemy.Health.Maximum) continue;
                enemy.Health.Heal(1);
                healed++;
                VisualFactory.RingPulse(enemy.transform.position, new Color(1f, 0.32f, 0.10f), 0.62f);
            }

            if (healed > 0) _status = "ENEMY LOGISTICS REPAIRING FRONTLINE // INTERDICT NOW";
        }

        private void FireScreeningShotAtPlayer()
        {
            _nextAmbush = Time.time + Mathf.Max(4.8f, AmbushCadenceForRound() * 0.82f);
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead || _convoyRoot == null) return;

            Vector2 origin = _convoyRoot.transform.position;
            Vector2 delta = (Vector2)player.transform.position - origin;
            if (delta.sqrMagnitude < 0.2f) delta = Vector2.down;
            _game.SpawnProjectile(origin + delta.normalized * 0.72f, delta.normalized, Team.Enemy, 1, 7.8f, new Color(1f, 0.32f, 0.08f), AmmoType.Basic);
            _status = "LOGISTICS ESCORT SCREENING FIRE";
        }

        private void FireAmbushSalvo()
        {
            if (_convoyRoot == null || _convoyHealth == null || _convoyHealth.IsDead) return;
            Vector2 convoy = _convoyRoot.transform.position;
            var rng = new System.Random(_round * 7213 + Mathf.RoundToInt(_routeElapsed * 100f));
            int shells = Mathf.Clamp(AmbushShellsMin + _round / 45, AmbushShellsMin, AmbushShellsMax);

            for (int i = 0; i < shells; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                Vector2 dirFrom = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 origin = convoy + dirFrom * (5.0f + (float)rng.NextDouble() * 1.4f);
                Vector2 direction = (convoy - origin).normalized;
                _game.SpawnProjectile(origin, direction, Team.Enemy, 1, 7.0f + _round * 0.012f, new Color(1f, 0.20f, 0.06f), AmmoType.Basic);
            }

            _threat = ConvoyThreatState.UnderFire;
            _status = "CONVOY UNDER FIRE // INTERCEPT AMBUSH SHELLS";
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.20f, 0.04f);
        }

        private void PulseMobileResupply()
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead || _convoyRoot == null) return;
            if (Vector2.Distance(player.transform.position, _convoyRoot.transform.position) > ResupplyRadius)
            {
                _status = "CLOSE ON MOBILE RESUPPLY FOR SUPPORT";
                return;
            }

            player.AddAmmo(AmmoType.ArmorPiercing, 1);
            if (_round >= 55) player.AddAmmo(AmmoType.EMP, 1);
            if (player.Health.Current < player.Health.Maximum) player.Health.Heal(1);
            _status = "MOBILE RESUPPLY PULSE // AMMO + FIELD PATCH";
            VisualFactory.RingPulse(player.transform.position, new Color(0.18f, 0.86f, 1f), 1.15f);
            BattleAudio.PlayGlobal(SoundCue.AmmoPickup, 0.36f, 0.03f);
        }

        private void GrantEscortSupport()
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead || _convoyRoot == null) return;
            if (Vector2.Distance(player.transform.position, _convoyRoot.transform.position) > ResupplyRadius + 0.8f) return;
            if (player.Health.Current < player.Health.Maximum) player.Health.Heal(1);
            player.AddAmmo(AmmoType.Explosive, 1);
            _status = "ESCORT MIDPOINT SUPPLY // EXPLOSIVE + FIELD PATCH";
            VisualFactory.RingPulse(player.transform.position, new Color(0.22f, 1f, 0.52f), 1.0f);
        }

        private void UpdateThreatState()
        {
            if (_convoyHealth == null || _convoyHealth.IsDead || _missionResolved) return;
            float ratio = (float)_convoyHealth.Current / Mathf.Max(1, _convoyHealth.Maximum);
            if (ratio <= 0.34f)
            {
                _threat = ConvoyThreatState.Critical;
                _status = _kind == ConvoyMissionKind.EnemyInterdiction ? "LOGISTICS TARGET CRITICAL // FINISH IT" : "CONVOY CRITICAL // DEFEND VEHICLE";
            }
            else if (_ambushFireAt > 0f)
            {
                _threat = ConvoyThreatState.AmbushWarning;
            }
            else if (_threat != ConvoyThreatState.UnderFire)
            {
                _threat = ConvoyThreatState.Moving;
            }
        }

        private void OnConvoyDamaged(Health health, int amount)
        {
            if (health == null || _missionResolved) return;
            _threat = ConvoyThreatState.UnderFire;
            VisualFactory.RingPulse(health.transform.position, new Color(1f, 0.34f, 0.10f), 0.78f);
        }

        private void OnConvoyDied(Health health)
        {
            ResolveDestroyedConvoy();
        }

        private void ResolveDestroyedConvoy()
        {
            if (_missionResolved) return;
            if (_kind == ConvoyMissionKind.EnemyInterdiction)
                CompleteMission("ENEMY LOGISTICS DESTROYED");
            else
                FailMission("FRIENDLY CONVOY LOST");
        }

        private void ResolveRouteArrival()
        {
            if (_missionResolved) return;
            if (_kind == ConvoyMissionKind.EnemyInterdiction)
            {
                Health eagle = CombatRoster.Eagle;
                if (eagle != null && !eagle.IsDead) eagle.Damage(2, Team.Enemy);
                FailMission("ENEMY LOGISTICS BROKE THROUGH // EAGLE HIT");
            }
            else
            {
                CompleteMission(_kind == ConvoyMissionKind.EagleEscort ? "ESCORT REACHED FRONTLINE" : "MOBILE RESUPPLY DELIVERED");
            }
        }

        private void CompleteMission(string reason)
        {
            _missionResolved = true;
            _threat = ConvoyThreatState.Complete;
            _status = reason;
            int reward = RewardForRound(_round);
            WarEconomyDirector.AwardMissionBonds(reward, "CONVOY WARFARE");
            Vector3 point = _convoyRoot != null ? _convoyRoot.transform.position : (Vector3)_routeEnd;
            VisualFactory.RingPulse(point, new Color(0.18f, 1f, 0.45f), 2.0f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.54f, 0.04f);
        }

        private void FailMission(string reason)
        {
            _missionResolved = true;
            _threat = ConvoyThreatState.Failed;
            _status = reason;
            Vector3 point = _convoyRoot != null ? _convoyRoot.transform.position : (Vector3)_routeEnd;
            VisualFactory.RingPulse(point, new Color(1f, 0.16f, 0.06f), 1.8f);
            BattleAudio.PlayGlobal(SoundCue.EnemyShot, 0.32f, -0.08f);
        }

        private float AmbushCadenceForRound()
        {
            float t = Mathf.Clamp01((_round - EarliestMissionRound) / 78f);
            return Mathf.Lerp(AmbushCadenceMax, AmbushCadenceMin, t);
        }

        private static string MissionIntro(ConvoyMissionKind kind)
        {
            switch (kind)
            {
                case ConvoyMissionKind.EnemyInterdiction: return "INTERDICT ENEMY LOGISTICS BEFORE EAGLE LINE";
                case ConvoyMissionKind.MobileResupply: return "PROTECT MOBILE RESUPPLY // STAY CLOSE FOR SUPPORT";
                default: return "ESCORT FRIENDLY COLUMN TO FRONTLINE";
            }
        }

        private static Color MissionColor(ConvoyMissionKind kind)
        {
            switch (kind)
            {
                case ConvoyMissionKind.EnemyInterdiction: return new Color(1f, 0.22f, 0.08f);
                case ConvoyMissionKind.MobileResupply: return new Color(0.18f, 0.82f, 1f);
                default: return new Color(0.22f, 1f, 0.48f);
            }
        }

        private void ResetMission()
        {
            if (_convoyHealth != null)
            {
                _convoyHealth.Damaged -= OnConvoyDamaged;
                _convoyHealth.Died -= OnConvoyDied;
            }
            if (_convoyRoot != null) Destroy(_convoyRoot);
            _convoyRoot = null;
            _convoyBody = null;
            _convoyHealth = null;
            _round = -1;
            _routeElapsed = 0f;
            _missionResolved = false;
            _status = string.Empty;
            _ambushFireAt = -1f;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.28f, 0.90f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
            _warning = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.60f, 0.20f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || !HasMissionForRound(_round) || _convoyHealth == null) return;
            EnsureStyles();

            float progress = Mathf.Clamp01(_routeElapsed / Mathf.Max(1f, _routeDuration));
            GUI.color = new Color(0.02f, 0.05f, 0.075f, 0.94f);
            GUI.Box(new Rect(Screen.width - 438f, Screen.height - 142f, 420f, 124f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - 424f, Screen.height - 136f, 392f, 20f), "CONVOY WARFARE // " + _kind.ToString().ToUpperInvariant(), _header);
            GUI.Label(new Rect(Screen.width - 424f, Screen.height - 112f, 392f, 20f), "ROUTE " + Mathf.RoundToInt(progress * 100f) + "%   HP " + _convoyHealth.Current + "/" + _convoyHealth.Maximum + "   " + _threat, _body);
            GUI.Label(new Rect(Screen.width - 424f, Screen.height - 88f, 392f, 20f), _status, _warning);
            string timer = _ambushFireAt > 0f ? "AMBUSH " + Mathf.Max(0f, _ambushFireAt - Time.time).ToString("0.0") + "s" : "PRESSURE ACTIVE";
            GUI.Label(new Rect(Screen.width - 424f, Screen.height - 64f, 392f, 20f), timer + "   REWARD " + RewardForRound(_round) + " BONDS", _body);
        }
    }
}
