using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Low-frequency production sentinel for long 100-round sessions. It does not own gameplay state;
    /// it validates the authoritative TankGame/registry/pool state and only performs conservative
    /// repairs when references have clearly gone stale.
    /// </summary>
    public sealed class RuntimeStabilityDirector : MonoBehaviour
    {
        private const float CheckInterval = 2f;
        private const float SoftLockWarningSeconds = 20f;

        private TankGame _game;
        private float _nextCheck;
        private float _lastCombatActivity;
        private int _registryFaultStreak;
        private int _checks;
        private int _repairs;
        private int _warnings;
        private string _lastIssue = "OK";

        public static RuntimeStabilityDirector Instance { get; private set; }
        public int Checks => _checks;
        public int Repairs => _repairs;
        public int Warnings => _warnings;
        public string LastIssue => _lastIssue;
        public bool Healthy => _lastIssue == "OK";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<RuntimeStabilityDirector>() != null) return;
            var go = new GameObject("RuntimeStabilityDirector_v4.8");
            DontDestroyOnLoad(go);
            go.AddComponent<RuntimeStabilityDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _lastCombatActivity = Time.unscaledTime;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + CheckInterval;
            CheckRuntime();
        }

        private void CheckRuntime()
        {
            _checks++;
            _lastIssue = "OK";

            if (_game == null)
                _game = FindAnyObjectByType<TankGame>();

            if (_game == null)
            {
                Warn("TankGame missing");
                return;
            }

            ProjectilePool.PruneDestroyedReferences();
            if (!ProjectilePool.ValidateIntegrity(out string poolReason))
            {
                Warn("Projectile pool: " + poolReason);
                ProjectilePool.ReleaseAllActive();
                ProjectilePool.PruneDestroyedReferences();
                _repairs++;
            }

            if (!_game.IsPlaying)
            {
                _registryFaultStreak = 0;
                if (ProjectilePool.ActiveCount > 0)
                {
                    ProjectilePool.ReleaseAllActive();
                    _repairs++;
                    _lastIssue = "Recovered orphan projectiles outside gameplay";
                }
                return;
            }

            if (_game.CurrentRound < 1 || _game.CurrentRound > 100)
            {
                Warn("Round out of campaign bounds: " + _game.CurrentRound);
                return;
            }

            bool missingPlayer = RuntimeBattleRegistry.Player == null;
            bool missingEagle = RuntimeBattleRegistry.Eagle == null;
            if (missingPlayer || missingEagle)
            {
                _registryFaultStreak++;
                _lastIssue = missingPlayer && missingEagle
                    ? "Registry missing player and Orzelek"
                    : missingPlayer ? "Registry missing player" : "Registry missing Orzelek";

                // A single frame can be a legitimate respawn/arena rebuild. Repair only after two checks.
                if (_registryFaultStreak >= 2)
                {
                    RuntimeBattleRegistry.ReconcileOnce();
                    _registryFaultStreak = 0;
                    _repairs++;
                }
            }
            else
            {
                _registryFaultStreak = 0;
            }

            int enemies = RuntimeBattleRegistry.RegisteredEnemyCount;
            if (enemies > 0 || ProjectilePool.ActiveCount > 0)
            {
                _lastCombatActivity = Time.unscaledTime;
            }
            else if (Time.unscaledTime - _lastCombatActivity > SoftLockWarningSeconds)
            {
                Warn("No registered combat activity for 20s during active campaign");
                _lastCombatActivity = Time.unscaledTime;
            }
        }

        private void Warn(string issue)
        {
            _lastIssue = issue;
            _warnings++;
            Debug.LogWarning("[v4.8 Stability] " + issue);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (!Input.GetKey(KeyCode.F2)) return;
            string status = Healthy ? "HEALTHY" : "CHECK";
            string text = $"v4.8 RUNTIME {status}  checks {_checks}  repairs {_repairs}  warnings {_warnings}\n" +
                          $"registry H:{RuntimeBattleRegistry.RegisteredHealthCount} E:{RuntimeBattleRegistry.RegisteredEnemyCount} rev:{RuntimeBattleRegistry.Revision}  " +
                          $"pool A:{ProjectilePool.ActiveCount} I:{ProjectilePool.InactiveCount} faults:{ProjectilePool.IntegrityFaultCount}\n" +
                          _lastIssue;
            GUI.Box(new Rect(10f, Screen.height - 82f, 760f, 72f), text);
        }
#endif
    }
}
