using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(448)]
    public sealed class CommandNetworkHuntDirector : MonoBehaviour
    {
        public const int MinimumRelayRound = 28;
        public const int MaxRelayNodes = 3;
        public const float RelayHealthMultiplier = 1.18f;
        public const float RelayReacquireDelay = 10f;
        public const float SigintCooldown = 18f;
        public const float SigintBaseRevealDuration = 4.5f;
        public const float SigintWeakNetworkBonus = 3.0f;
        public const float ReconCooldown = 32f;
        public const float ReconDuration = 8f;
        public const float ReconSweepCadence = 0.75f;
        public const float RelayDisruptionDuration = 2.75f;
        public const float FullNetworkBreakDuration = 6f;
        public const int MaxRelayCandidatesScanned = 48;

        private static CommandNetworkHuntDirector _instance;
        private readonly List<RelayBinding> _relays = new List<RelayBinding>(MaxRelayNodes);
        private TankGame _game;
        private float _nextRelaySearch;
        private float _nextSigint;
        private float _revealUntil;
        private float _nextRecon;
        private float _reconUntil;
        private float _nextReconSweep;
        private float _networkBreakUntil;
        private float _relayDisruptionUntil;
        private bool _ownsAdaptiveSuppression;
        private bool _ownsSquadSuppression;
        private bool _ownsBossSuppression;
        private int _relaysDestroyed;
        private int _fullNetworkBreaks;
        private int _sigintScans;
        private int _reconSweeps;

        public static CommandNetworkHuntDirector Instance => _instance;
        public static bool ConfigurationValid =>
            MinimumRelayRound >= 20 && MinimumRelayRound <= 40 &&
            MaxRelayNodes >= 2 && MaxRelayNodes <= 4 &&
            RelayHealthMultiplier >= 1.05f && RelayHealthMultiplier <= 1.35f &&
            RelayReacquireDelay >= 6f && RelayReacquireDelay <= 16f &&
            SigintCooldown >= 12f && SigintCooldown <= 28f &&
            SigintBaseRevealDuration >= 3f && SigintBaseRevealDuration <= 7f &&
            SigintWeakNetworkBonus >= 1f && SigintWeakNetworkBonus <= 5f &&
            ReconCooldown >= 20f && ReconCooldown <= 45f &&
            ReconDuration >= 5f && ReconDuration <= 12f &&
            ReconSweepCadence >= 0.45f && ReconSweepCadence <= 1.2f &&
            RelayDisruptionDuration >= 1.5f && RelayDisruptionDuration <= 4f &&
            FullNetworkBreakDuration >= 4f && FullNetworkBreakDuration <= 9f &&
            MaxRelayCandidatesScanned >= 24 && MaxRelayCandidatesScanned <= 64;

        public int LiveRelayCount => CountLiveRelays();
        public float NetworkStrength => Mathf.Clamp01(LiveRelayCount / (float)MaxRelayNodes);
        public bool TargetsRevealed => Time.time < _revealUntil || ReconActive;
        public float RevealRemaining => Mathf.Max(0f, _revealUntil - Time.time);
        public bool ReconActive => Time.time < _reconUntil;
        public float ReconRemaining => Mathf.Max(0f, _reconUntil - Time.time);
        public float SigintReadyIn => Mathf.Max(0f, _nextSigint - Time.time);
        public float ReconReadyIn => Mathf.Max(0f, _nextRecon - Time.time);
        public bool NetworkBreakActive => Time.time < _networkBreakUntil;
        public float NetworkBreakRemaining => Mathf.Max(0f, _networkBreakUntil - Time.time);
        public int RelaysDestroyed => _relaysDestroyed;
        public int FullNetworkBreaks => _fullNetworkBreaks;
        public int SigintScans => _sigintScans;
        public int ReconSweeps => _reconSweeps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CommandNetworkHuntDirector>() != null) return;
            var go = new GameObject("CommandNetworkHuntDirector_v7_8");
            DontDestroyOnLoad(go);
            go.AddComponent<CommandNetworkHuntDirector>();
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
            ClearRelays(true);
            RestoreOwnedSuppression(true);
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                ClearRelays(true);
                RestoreOwnedSuppression(true);
                _nextRelaySearch = 0f;
                _revealUntil = 0f;
                _reconUntil = 0f;
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            PruneDeadRelays();
            MaintainRelayNetwork(round);

            if (Input.GetKeyDown(KeyCode.G) && Time.time >= _nextSigint)
                ActivateSigint();
            if (Input.GetKeyDown(KeyCode.H) && Time.time >= _nextRecon)
                LaunchReconDrone();

            if (ReconActive && Time.time >= _nextReconSweep)
            {
                _nextReconSweep = Time.time + ReconSweepCadence;
                ExecuteReconSweep();
            }

            if (NetworkBreakActive || Time.time < _relayDisruptionUntil)
                SuppressTacticalNetwork();
            else
                RestoreOwnedSuppression(false);

            if (TargetsRevealed && Time.frameCount % 18 == 0)
                PresentRevealedTargets();
        }

        private void MaintainRelayNetwork(int round)
        {
            EnemyElectronicWarfareDirector ew = EnemyElectronicWarfareDirector.Instance;
            if (round < MinimumRelayRound || ew == null || !ew.HasLiveCommandVehicle) return;
            if (LiveRelayCount >= MaxRelayNodes || Time.time < _nextRelaySearch) return;

            EnemyTank candidate = SelectRelayCandidate(ew.CommandVehicle);
            if (candidate == null)
            {
                _nextRelaySearch = Time.time + 2f;
                return;
            }

            Health health = candidate.Health;
            if (health == null || health.IsDead) return;
            int boosted = Mathf.Max(health.Maximum + 1, Mathf.CeilToInt(health.Maximum * RelayHealthMultiplier));
            health.SetMaximum(boosted, true);
            EnemyRelayNode node = candidate.gameObject.AddComponent<EnemyRelayNode>();
            RelayBinding binding = new RelayBinding(candidate, health, node);
            _relays.Add(binding);
            health.Died += OnRelayDied;
            _nextRelaySearch = Time.time + 0.35f;
            Vector2 pos = candidate.transform.position;
            VisualFactory.RingPulse(pos, new Color(0.18f, 0.82f, 1f), 1.15f);
            VisualFactory.MicroBurst(pos, new Color(0.56f, 0.34f, 1f), 0.72f);
        }

        private EnemyTank SelectRelayCandidate(EnemyTank command)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            EnemyTank best = null;
            float bestScore = float.MinValue;
            Vector2 commandPos = command != null ? (Vector2)command.transform.position : Vector2.zero;

            for (int i = 0; i < enemies.Length && i < MaxRelayCandidatesScanned; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsLive(enemy) || enemy == command) continue;
                if (enemy.Kind == EnemyKind.Supply || enemy.Kind == EnemyKind.Boss) continue;
                if (enemy.GetComponent<EnemyCommandNode>() != null || enemy.GetComponent<EnemyRelayNode>() != null) continue;
                if (enemy.Kind != EnemyKind.Sniper && enemy.Kind != EnemyKind.Heavy && enemy.Kind != EnemyKind.Elite && enemy.Kind != EnemyKind.Siege) continue;

                float classScore = enemy.Kind == EnemyKind.Sniper ? 4.5f : enemy.Kind == EnemyKind.Elite ? 4f : enemy.Kind == EnemyKind.Heavy ? 3f : 2.5f;
                float spacing = Mathf.Clamp(Vector2.Distance(enemy.transform.position, commandPos) * 0.08f, 0f, 1.5f);
                float score = classScore + spacing;
                if (score <= bestScore) continue;
                bestScore = score;
                best = enemy;
            }
            return best;
        }

        private void ActivateSigint()
        {
            _nextSigint = Time.time + SigintCooldown;
            float duration = SigintBaseRevealDuration + (1f - NetworkStrength) * SigintWeakNetworkBonus;
            _revealUntil = Mathf.Max(_revealUntil, Time.time + duration);
            _sigintScans++;
            Vector2 player = _game.PlayerPosition;
            VisualFactory.RingPulse(player, new Color(0.20f, 0.95f, 1f), 2.2f);
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.28f, 0.06f);
            PresentRevealedTargets();
        }

        private void LaunchReconDrone()
        {
            _nextRecon = Time.time + ReconCooldown;
            _reconUntil = Time.time + ReconDuration;
            _nextReconSweep = Time.time;
            Vector2 player = _game.PlayerPosition;
            VisualFactory.RingPulse(player, new Color(0.30f, 1f, 0.64f), 1.55f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.24f, 0.10f);
        }

        private void ExecuteReconSweep()
        {
            _reconSweeps++;
            _revealUntil = Mathf.Max(_revealUntil, Time.time + 1.2f);
            Vector2 player = _game.PlayerPosition;
            float phase = (_reconSweeps % 16) * Mathf.PI * 0.125f;
            Vector2 drone = player + new Vector2(Mathf.Cos(phase), Mathf.Sin(phase)) * 2.4f;
            VisualFactory.RingPulse(drone, new Color(0.28f, 0.94f, 1f, 0.72f), 0.64f);
            PresentRevealedTargets();
        }

        private void PresentRevealedTargets()
        {
            EnemyElectronicWarfareDirector ew = EnemyElectronicWarfareDirector.Instance;
            if (ew != null && ew.HasLiveCommandVehicle)
                VisualFactory.RingPulse(ew.CommandVehicle.transform.position, new Color(1f, 0.34f, 0.18f, 0.76f), 0.92f);

            for (int i = 0; i < _relays.Count; i++)
            {
                RelayBinding relay = _relays[i];
                if (!relay.IsLive) continue;
                VisualFactory.RingPulse(relay.Enemy.transform.position, new Color(0.20f, 0.82f, 1f, 0.74f), 0.72f);
            }
        }

        private void OnRelayDied(Health dead)
        {
            if (dead == null) return;
            for (int i = _relays.Count - 1; i >= 0; i--)
            {
                RelayBinding relay = _relays[i];
                if (relay.Health != dead) continue;
                Vector2 pos = relay.Enemy != null ? (Vector2)relay.Enemy.transform.position : Vector2.zero;
                dead.Died -= OnRelayDied;
                if (relay.Node != null) Destroy(relay.Node);
                _relays.RemoveAt(i);
                _relaysDestroyed++;
                _relayDisruptionUntil = Mathf.Max(_relayDisruptionUntil, Time.time + RelayDisruptionDuration);
                _nextRelaySearch = Time.time + RelayReacquireDelay;
                VisualFactory.RingPulse(pos, new Color(0.22f, 1f, 0.68f), 1.55f);
                BattleAudio.PlayGlobal(SoundCue.Emp, 0.30f, -0.04f);
                break;
            }

            EnemyElectronicWarfareDirector ew = EnemyElectronicWarfareDirector.Instance;
            if (CountLiveRelays() == 0 && ew != null && ew.HasLiveCommandVehicle)
                TriggerFullNetworkBreak();
        }

        private void TriggerFullNetworkBreak()
        {
            if (NetworkBreakActive) return;
            _networkBreakUntil = Time.time + FullNetworkBreakDuration;
            _revealUntil = Mathf.Max(_revealUntil, _networkBreakUntil);
            _nextRelaySearch = Mathf.Max(_nextRelaySearch, _networkBreakUntil + RelayReacquireDelay);
            _fullNetworkBreaks++;
            VisualFactory.RingPulse(_game.PlayerPosition, new Color(0.18f, 1f, 0.58f), 2.8f);
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.40f, -0.08f);
        }

        private void SuppressTacticalNetwork()
        {
            AdaptiveFireControlDirector adaptive = AdaptiveFireControlDirector.Instance;
            if (adaptive != null && adaptive.enabled)
            {
                adaptive.enabled = false;
                _ownsAdaptiveSuppression = true;
            }
            EnemySquadTacticsDirector squad = EnemySquadTacticsDirector.Instance;
            if (squad != null && squad.enabled)
            {
                squad.enabled = false;
                _ownsSquadSuppression = true;
            }
            BossCommandTacticsDirector boss = BossCommandTacticsDirector.Instance;
            if (boss != null && boss.enabled)
            {
                boss.enabled = false;
                _ownsBossSuppression = true;
            }
        }

        private void RestoreOwnedSuppression(bool force)
        {
            if (!force)
            {
                TacticalCounterplayDirector counter = TacticalCounterplayDirector.Instance;
                bool jammed = counter != null && counter.NetworkJammed;
                bool smoked = counter != null && counter.PlayerInsideSmoke;
                EnemyElectronicWarfareDirector ew = EnemyElectronicWarfareDirector.Instance;
                bool superiority = ew != null && ew.TacticalSuperiorityActive;
                if (jammed || smoked || superiority || NetworkBreakActive || Time.time < _relayDisruptionUntil) return;
            }

            if (_ownsAdaptiveSuppression)
            {
                if (AdaptiveFireControlDirector.Instance != null) AdaptiveFireControlDirector.Instance.enabled = true;
                _ownsAdaptiveSuppression = false;
            }
            if (_ownsSquadSuppression)
            {
                if (EnemySquadTacticsDirector.Instance != null) EnemySquadTacticsDirector.Instance.enabled = true;
                _ownsSquadSuppression = false;
            }
            if (_ownsBossSuppression)
            {
                if (BossCommandTacticsDirector.Instance != null) BossCommandTacticsDirector.Instance.enabled = true;
                _ownsBossSuppression = false;
            }
        }

        private void PruneDeadRelays()
        {
            for (int i = _relays.Count - 1; i >= 0; i--)
            {
                RelayBinding relay = _relays[i];
                if (relay.IsLive) continue;
                if (relay.Health != null) relay.Health.Died -= OnRelayDied;
                if (relay.Node != null) Destroy(relay.Node);
                _relays.RemoveAt(i);
            }
        }

        private void ClearRelays(bool removeNodes)
        {
            for (int i = 0; i < _relays.Count; i++)
            {
                RelayBinding relay = _relays[i];
                if (relay.Health != null) relay.Health.Died -= OnRelayDied;
                if (removeNodes && relay.Node != null) Destroy(relay.Node);
            }
            _relays.Clear();
        }

        private int CountLiveRelays()
        {
            int count = 0;
            for (int i = 0; i < _relays.Count; i++) if (_relays[i].IsLive) count++;
            return count;
        }

        private static bool IsLive(EnemyTank enemy)
        {
            return enemy != null && enemy.Health != null && !enemy.Health.IsDead;
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _game.CurrentRound < MinimumRelayRound) return;
            float width = Mathf.Min(600f, Screen.width - 28f);
            Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 130f, width, 30f);
            string network = NetworkBreakActive ? $"NETWORK BROKEN {NetworkBreakRemaining:0.0}s" : $"EW RELAYS {LiveRelayCount}/{MaxRelayNodes}";
            string sigint = SigintReadyIn <= 0f ? "SIGINT [G] READY" : $"SIGINT {SigintReadyIn:0}s";
            string recon = ReconActive ? $"RECON {ReconRemaining:0.0}s" : ReconReadyIn <= 0f ? "RECON [H] READY" : $"RECON {ReconReadyIn:0}s";
            GUI.Box(rect, network + "     " + sigint + "     " + recon);
        }

        private sealed class RelayBinding
        {
            public readonly EnemyTank Enemy;
            public readonly Health Health;
            public readonly EnemyRelayNode Node;
            public RelayBinding(EnemyTank enemy, Health health, EnemyRelayNode node)
            {
                Enemy = enemy;
                Health = health;
                Node = node;
            }
            public bool IsLive => Enemy != null && Health != null && !Health.IsDead;
        }
    }

    public sealed class EnemyRelayNode : MonoBehaviour
    {
        public float LinkedAt { get; private set; }
        private void Awake() => LinkedAt = Time.time;
    }
}
