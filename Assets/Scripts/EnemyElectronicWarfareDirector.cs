using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(447)]
    public sealed class EnemyElectronicWarfareDirector : MonoBehaviour
    {
        public const int MinimumCommandRound = 20;
        public const float CommandHealthMultiplier = 1.40f;
        public const float CommandReacquireDelay = 8.0f;
        public const float TacticalSuperiorityDuration = 6.0f;
        public const float SmokeFlankCadence = 0.75f;
        public const int MaxSmokeResponders = 4;
        public const float DecoyDuration = 6.0f;
        public const float DecoyCooldown = 20.0f;
        public const float CommandDecoyDetectionDelay = 3.0f;
        public const float DecoyFireCadence = 1.15f;
        public const int MaxDecoyShotsPerBeat = 3;

        private static EnemyElectronicWarfareDirector _instance;
        private TankGame _game;
        private EnemyTank _commandVehicle;
        private Health _commandHealth;
        private float _nextCommandSearch;
        private float _nextSmokeResponse;
        private float _superiorityUntil;
        private bool _ownsAdaptiveSuppression;
        private bool _ownsSquadSuppression;
        private bool _ownsBossSuppression;

        private bool _decoyActive;
        private Vector2 _decoyPosition;
        private float _decoyExpiresAt;
        private float _nextDecoy;
        private float _nextDecoyFire;
        private int _commandVehiclesDestroyed;
        private int _decoyShotsRedirected;
        private int _smokeFlankOrders;

        public static EnemyElectronicWarfareDirector Instance => _instance;
        public static bool ConfigurationValid =>
            MinimumCommandRound >= 15 && MinimumCommandRound <= 30 &&
            CommandHealthMultiplier >= 1.20f && CommandHealthMultiplier <= 1.65f &&
            CommandReacquireDelay >= 5f && CommandReacquireDelay <= 12f &&
            TacticalSuperiorityDuration >= 4f && TacticalSuperiorityDuration <= 8f &&
            SmokeFlankCadence >= 0.45f && SmokeFlankCadence <= 1.2f &&
            MaxSmokeResponders >= 2 && MaxSmokeResponders <= 6 &&
            DecoyDuration >= 4f && DecoyDuration <= 8f &&
            DecoyCooldown >= 15f && DecoyCooldown <= 28f &&
            CommandDecoyDetectionDelay >= 2f && CommandDecoyDetectionDelay < DecoyDuration &&
            DecoyFireCadence >= 0.8f && DecoyFireCadence <= 1.8f &&
            MaxDecoyShotsPerBeat >= 2 && MaxDecoyShotsPerBeat <= 4;

        public static bool CommandNetworkOnline => _instance != null && _instance.HasLiveCommandVehicle;
        public bool HasLiveCommandVehicle => _commandVehicle != null && _commandHealth != null && !_commandHealth.IsDead;
        public EnemyTank CommandVehicle => HasLiveCommandVehicle ? _commandVehicle : null;
        public bool TacticalSuperiorityActive => Time.time < _superiorityUntil;
        public float TacticalSuperiorityRemaining => Mathf.Max(0f, _superiorityUntil - Time.time);
        public bool DecoyActive => _decoyActive && Time.time < _decoyExpiresAt;
        public Vector2 DecoyPosition => _decoyPosition;
        public float DecoyReadyIn => Mathf.Max(0f, _nextDecoy - Time.time);
        public float DecoyRemaining => DecoyActive ? Mathf.Max(0f, _decoyExpiresAt - Time.time) : 0f;
        public int CommandVehiclesDestroyed => _commandVehiclesDestroyed;
        public int DecoyShotsRedirected => _decoyShotsRedirected;
        public int SmokeFlankOrders => _smokeFlankOrders;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EnemyElectronicWarfareDirector>() != null) return;
            var go = new GameObject("EnemyElectronicWarfareDirector_v7_7");
            DontDestroyOnLoad(go);
            go.AddComponent<EnemyElectronicWarfareDirector>();
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
            UnsubscribeCommand();
            RestoreOwnedSuppression(null, true);
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                _decoyActive = false;
                RestoreOwnedSuppression(null, true);
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            MaintainCommandVehicle(round);

            if (Input.GetKeyDown(KeyCode.V) && Time.time >= _nextDecoy)
                DeployDecoy();

            TacticalCounterplayDirector counterplay = TacticalCounterplayDirector.Instance;
            bool jammed = counterplay != null && counterplay.NetworkJammed;
            bool smoked = counterplay != null && counterplay.PlayerInsideSmoke;

            if (smoked && Time.time >= _nextSmokeResponse)
            {
                _nextSmokeResponse = Time.time + SmokeFlankCadence;
                ExecuteSmokeCounterManeuver(round);
            }

            UpdateDecoy(round, counterplay);
            UpdateTacticalSuperiority(counterplay);

            // A live command vehicle partially hardens the network against jammer effects,
            // but never defeats physical concealment, decoy deception or superiority windows.
            if (HasLiveCommandVehicle && jammed && !smoked && !DecoyActive && !TacticalSuperiorityActive)
            {
                AdaptiveFireControlDirector adaptive = AdaptiveFireControlDirector.Instance;
                if (adaptive != null) adaptive.enabled = true;
            }

            UpdateCommandPresentation();
        }

        private void MaintainCommandVehicle(int round)
        {
            if (HasLiveCommandVehicle) return;
            if (_commandVehicle != null || _commandHealth != null) UnsubscribeCommand();
            if (round < MinimumCommandRound || Time.time < _nextCommandSearch) return;

            EnemyTank candidate = SelectCommandCandidate();
            if (candidate == null)
            {
                _nextCommandSearch = Time.time + 2f;
                return;
            }

            _commandVehicle = candidate;
            _commandHealth = candidate.Health;
            int boosted = Mathf.Max(_commandHealth.Maximum + 2, Mathf.CeilToInt(_commandHealth.Maximum * CommandHealthMultiplier));
            _commandHealth.SetMaximum(boosted, true);
            _commandHealth.Died += OnCommandVehicleDied;
            _nextCommandSearch = float.PositiveInfinity;

            Vector2 pos = candidate.transform.position;
            VisualFactory.RingPulse(pos, new Color(0.12f, 0.92f, 1f), 1.8f);
            VisualFactory.RingPulse(pos, new Color(0.65f, 0.24f, 1f), 1.15f);
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.24f, 0.04f);
        }

        private EnemyTank SelectCommandCandidate()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            EnemyTank best = null;
            float bestScore = float.MinValue;
            Vector2 player = _game != null ? _game.PlayerPosition : Vector2.zero;

            for (int i = 0; i < enemies.Length && i < 48; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsLive(enemy) || enemy.Kind == EnemyKind.Supply || enemy.Kind == EnemyKind.Boss || enemy.GetComponent<EnemyCommandNode>() != null) continue;
                float kindScore = enemy.Kind == EnemyKind.Elite ? 5f : enemy.Kind == EnemyKind.Heavy ? 4f : enemy.Kind == EnemyKind.Sniper ? 3f : enemy.Kind == EnemyKind.Siege ? 2f : 0f;
                if (kindScore <= 0f) continue;
                float score = kindScore + Mathf.Clamp(Vector2.Distance(enemy.transform.position, player) * 0.05f, 0f, 1f);
                if (score <= bestScore) continue;
                bestScore = score;
                best = enemy;
            }

            if (best != null) best.gameObject.AddComponent<EnemyCommandNode>();
            return best;
        }

        private void OnCommandVehicleDied(Health dead)
        {
            if (dead == null || dead != _commandHealth) return;
            Vector2 pos = _commandVehicle != null ? (Vector2)_commandVehicle.transform.position : Vector2.zero;
            _commandVehiclesDestroyed++;
            _superiorityUntil = Mathf.Max(_superiorityUntil, Time.time + TacticalSuperiorityDuration);
            _nextCommandSearch = Time.time + CommandReacquireDelay;
            VisualFactory.RingPulse(pos, new Color(0.16f, 1f, 0.72f), 2.5f);
            VisualFactory.MicroBurst(pos, new Color(0.36f, 0.95f, 1f), 1.5f);
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.36f, -0.05f);
            UnsubscribeCommand(false);
        }

        private void ExecuteSmokeCounterManeuver(int round)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            Vector2 player = _game.PlayerPosition;
            int ordered = 0;
            float radius = Mathf.Lerp(3.4f, 5.2f, (round - 1f) / 99f);

            for (int i = 0; i < enemies.Length && ordered < MaxSmokeResponders; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsLive(enemy)) continue;
                if (enemy.Kind != EnemyKind.Fast && enemy.Kind != EnemyKind.Elite && enemy.Kind != EnemyKind.Sniper) continue;
                TacticalNavigationAgent agent = enemy.GetComponent<TacticalNavigationAgent>();
                if (agent == null) continue;

                Vector2 pos = enemy.transform.position;
                Vector2 toPlayer = player - pos;
                if (toPlayer.sqrMagnitude < 0.16f) continue;
                Vector2 side = new Vector2(-toPlayer.y, toPlayer.x).normalized;
                float sign = ((enemy.GetInstanceID() + round + ordered) & 1) == 0 ? 1f : -1f;
                Vector2 flank = player + side * sign * radius;
                agent.SetOrder(flank, enemy.Kind == EnemyKind.Sniper ? 4.6f : 1.6f, enemy.Kind == EnemyKind.Fast ? 1.18f : 1.05f, enemies);
                ordered++;
                _smokeFlankOrders++;
            }

            if (ordered > 0) VisualFactory.RingPulse(player, new Color(1f, 0.46f, 0.10f), 0.74f);
        }

        private void DeployDecoy()
        {
            _decoyActive = true;
            _decoyPosition = _game.PlayerPosition;
            _decoyExpiresAt = Time.time + DecoyDuration;
            _nextDecoy = Time.time + DecoyCooldown;
            _nextDecoyFire = Time.time + 0.45f;
            VisualFactory.RingPulse(_decoyPosition, new Color(0.22f, 0.94f, 1f), 1.3f);
            VisualFactory.MicroBurst(_decoyPosition, new Color(0.50f, 0.38f, 1f), 0.9f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.24f, 0.08f);
        }

        private void UpdateDecoy(int round, TacticalCounterplayDirector counterplay)
        {
            if (_decoyActive && Time.time >= _decoyExpiresAt) _decoyActive = false;

            if (!DecoyActive)
            {
                ReleaseAdaptiveIfSafe(counterplay);
                return;
            }

            float activeFor = DecoyDuration - DecoyRemaining;
            bool commandResolved = HasLiveCommandVehicle && activeFor >= CommandDecoyDetectionDelay;
            if (commandResolved)
            {
                ReleaseAdaptiveIfSafe(counterplay);
                return;
            }

            SuppressAdaptive();
            if (Time.time >= _nextDecoyFire)
            {
                _nextDecoyFire = Time.time + DecoyFireCadence;
                RedirectFireToDecoy(round);
            }

            if (Time.frameCount % 18 == 0)
                VisualFactory.RingPulse(_decoyPosition, new Color(0.20f, 0.80f, 1f, 0.62f), 0.68f);
        }

        private void RedirectFireToDecoy(int round)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int shots = 0;
            float progress = (round - 1f) / 99f;

            for (int i = 0; i < enemies.Length && shots < MaxDecoyShotsPerBeat; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsLive(enemy)) continue;
                if (enemy.Kind != EnemyKind.Sniper && enemy.Kind != EnemyKind.Elite && enemy.Kind != EnemyKind.Fast) continue;

                Vector2 origin = enemy.transform.position;
                Vector2 delta = _decoyPosition - origin;
                if (delta.sqrMagnitude < 1f) continue;
                Vector2 direction = delta.normalized;
                Vector2 muzzle = origin + direction * 0.78f;
                float speed = Mathf.Lerp(9.4f, 11.8f, progress);
                _game.SpawnProjectile(muzzle, direction, Team.Enemy, 1, speed, new Color(0.38f, 0.84f, 1f), AmmoType.Basic);
                VisualFactory.MuzzleFlash(muzzle, new Color(0.38f, 0.84f, 1f), 0.54f);
                shots++;
                _decoyShotsRedirected++;

                TacticalNavigationAgent agent = enemy.GetComponent<TacticalNavigationAgent>();
                if (agent != null) agent.SetOrder(_decoyPosition, enemy.Kind == EnemyKind.Sniper ? 5.4f : 1.8f, 0.96f, enemies);
            }
        }

        private void UpdateTacticalSuperiority(TacticalCounterplayDirector counterplay)
        {
            if (TacticalSuperiorityActive)
            {
                SuppressAdaptive();
                SuppressSquad();
                SuppressBoss();
            }
            else
            {
                RestoreOwnedSuppression(counterplay, false);
            }
        }

        private void SuppressAdaptive()
        {
            AdaptiveFireControlDirector adaptive = AdaptiveFireControlDirector.Instance;
            if (adaptive != null && adaptive.enabled)
            {
                adaptive.enabled = false;
                _ownsAdaptiveSuppression = true;
            }
        }

        private void SuppressSquad()
        {
            EnemySquadTacticsDirector squad = EnemySquadTacticsDirector.Instance;
            if (squad != null && squad.enabled)
            {
                squad.enabled = false;
                _ownsSquadSuppression = true;
            }
        }

        private void SuppressBoss()
        {
            BossCommandTacticsDirector boss = BossCommandTacticsDirector.Instance;
            if (boss != null && boss.enabled)
            {
                boss.enabled = false;
                _ownsBossSuppression = true;
            }
        }

        private void ReleaseAdaptiveIfSafe(TacticalCounterplayDirector counterplay)
        {
            if (!_ownsAdaptiveSuppression) return;
            bool jammed = counterplay != null && counterplay.NetworkJammed;
            bool smoked = counterplay != null && counterplay.PlayerInsideSmoke;
            if (jammed || smoked || TacticalSuperiorityActive || DecoyActive) return;
            if (AdaptiveFireControlDirector.Instance != null) AdaptiveFireControlDirector.Instance.enabled = true;
            _ownsAdaptiveSuppression = false;
        }

        private void RestoreOwnedSuppression(TacticalCounterplayDirector counterplay, bool force)
        {
            bool jammed = !force && counterplay != null && counterplay.NetworkJammed;
            bool smoked = !force && counterplay != null && counterplay.PlayerInsideSmoke;

            if (_ownsAdaptiveSuppression && (force || (!jammed && !smoked && !DecoyActive && !TacticalSuperiorityActive)))
            {
                if (AdaptiveFireControlDirector.Instance != null) AdaptiveFireControlDirector.Instance.enabled = true;
                _ownsAdaptiveSuppression = false;
            }
            if (_ownsSquadSuppression && (force || (!jammed && !TacticalSuperiorityActive)))
            {
                if (EnemySquadTacticsDirector.Instance != null) EnemySquadTacticsDirector.Instance.enabled = true;
                _ownsSquadSuppression = false;
            }
            if (_ownsBossSuppression && (force || (!jammed && !TacticalSuperiorityActive)))
            {
                if (BossCommandTacticsDirector.Instance != null) BossCommandTacticsDirector.Instance.enabled = true;
                _ownsBossSuppression = false;
            }
        }

        private void UpdateCommandPresentation()
        {
            if (!HasLiveCommandVehicle || Time.frameCount % 24 != 0) return;
            VisualFactory.RingPulse(_commandVehicle.transform.position, new Color(0.16f, 0.88f, 1f, 0.54f), 0.82f);
        }

        private void UnsubscribeCommand(bool clearNode = true)
        {
            if (_commandHealth != null) _commandHealth.Died -= OnCommandVehicleDied;
            if (clearNode && _commandVehicle != null)
            {
                EnemyCommandNode node = _commandVehicle.GetComponent<EnemyCommandNode>();
                if (node != null) Destroy(node);
            }
            _commandVehicle = null;
            _commandHealth = null;
        }

        private static bool IsLive(EnemyTank enemy)
        {
            return enemy != null && enemy.Health != null && !enemy.Health.IsDead;
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            float width = Mathf.Min(520f, Screen.width - 28f);
            Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 94f, width, 32f);
            string command = HasLiveCommandVehicle ? "EW COMMAND: ONLINE" : TacticalSuperiorityActive ? $"TACTICAL EDGE {TacticalSuperiorityRemaining:0.0}s" : "EW COMMAND: DOWN";
            string decoy = DecoyActive ? $"DECOY {DecoyRemaining:0.0}s" : DecoyReadyIn <= 0f ? "DECOY [V] READY" : $"DECOY {DecoyReadyIn:0}s";
            GUI.Box(rect, command + "     " + decoy);
        }
    }

    public sealed class EnemyCommandNode : MonoBehaviour
    {
        public float PromotedAt { get; private set; }
        private void Awake() => PromotedAt = Time.time;
    }
}
