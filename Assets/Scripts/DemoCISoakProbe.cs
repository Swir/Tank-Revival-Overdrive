using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Release-only automated late-round qualification probe. It is dormant for normal players and
    /// runs only when the packaged standalone is launched with -demo-ci-soak. The probe drives the
    /// authoritative TankGame through rounds 80/90/100, injects heavy pressure, validates projectile
    /// pool integrity and runtime-stability telemetry, then emits an explicit PASS/FAIL marker for CI.
    /// The player side is made temporarily invulnerable only inside this opt-in CI path so the probe
    /// measures runtime integrity rather than the survivability of an unattended tank.
    /// </summary>
    [DefaultExecutionOrder(20010)]
    public sealed class DemoCISoakProbe : MonoBehaviour
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float ResolveTimeoutSeconds = 12f;
        private const float TotalTimeoutSeconds = 70f;
        private const int PressureUnitsPerStage = 16;

        private TankGame _game;
        private MethodInfo _startCampaign;
        private MethodInfo _beginRound;
        private MethodInfo _spawnEnemy;
        private FieldInfo _roundField;
        private float _startedAt;
        private float _minFps = 999f;
        private float _maxGcMb;
        private int _startWarnings;
        private int _startRepairs;
        private int _startPoolFaults;
        private bool _completed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-demo-ci-soak")) return;
            if (FindAnyObjectByType<DemoCISoakProbe>() != null) return;
            var go = new GameObject("DemoCISoakProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<DemoCISoakProbe>();
        }

        private void Awake()
        {
            _startedAt = Time.realtimeSinceStartup;
            StartCoroutine(RunQualification());
        }

        private void Update()
        {
            if (_completed) return;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.0001f) _minFps = Mathf.Min(_minFps, 1f / dt);
            _maxGcMb = Mathf.Max(_maxGcMb, GC.GetTotalMemory(false) / (1024f * 1024f));

            if (Time.realtimeSinceStartup - _startedAt > TotalTimeoutSeconds)
                Fail("global timeout");
        }

        private IEnumerator RunQualification()
        {
            float resolveUntil = Time.realtimeSinceStartup + ResolveTimeoutSeconds;
            while (!ResolveGame() && Time.realtimeSinceStartup < resolveUntil)
                yield return null;

            if (!ResolveGame())
            {
                Fail("TankGame/reflection contract unavailable");
                yield break;
            }

            RuntimeStabilityDirector stability = null;
            float stabilityUntil = Time.realtimeSinceStartup + ResolveTimeoutSeconds;
            while (stability == null && Time.realtimeSinceStartup < stabilityUntil)
            {
                stability = RuntimeStabilityDirector.Instance ?? FindAnyObjectByType<RuntimeStabilityDirector>();
                if (stability == null) yield return null;
            }

            if (stability == null)
            {
                Fail("RuntimeStabilityDirector unavailable");
                yield break;
            }

            _startWarnings = stability.Warnings;
            _startRepairs = stability.Repairs;
            _startPoolFaults = ProjectilePool.IntegrityFaultCount;

            int[] rounds = { 80, 90, 100 };
            for (int i = 0; i < rounds.Length; i++)
            {
                if (!StartStage(rounds[i])) yield break;
                yield return new WaitForSecondsRealtime(1.5f);
                ProtectPlayerSide();

                if (!InjectPressure()) yield break;

                float stageUntil = Time.realtimeSinceStartup + 5f;
                while (Time.realtimeSinceStartup < stageUntil)
                {
                    ProtectPlayerSide();
                    if (!_game.IsPlaying)
                    {
                        Fail("campaign stopped during round " + rounds[i]);
                        yield break;
                    }
                    yield return null;
                }

                if (!ProjectilePool.ValidateIntegrity(out string reason))
                {
                    Fail("pool integrity round " + rounds[i] + ": " + reason);
                    yield break;
                }

                if (!stability.Healthy)
                {
                    Fail("runtime stability unhealthy at round " + rounds[i]);
                    yield break;
                }
            }

            int warningDelta = stability.Warnings - _startWarnings;
            int repairDelta = stability.Repairs - _startRepairs;
            int poolFaultDelta = ProjectilePool.IntegrityFaultCount - _startPoolFaults;
            if (warningDelta != 0 || repairDelta != 0 || poolFaultDelta != 0)
            {
                Fail($"stability deltas warnings={warningDelta} repairs={repairDelta} poolFaults={poolFaultDelta}");
                yield break;
            }

            Pass($"rounds=80,90,100 pressure={PressureUnitsPerStage}x3 minFPS={_minFps:0.0} maxGC={_maxGcMb:0.0}MB created={ProjectilePool.CreatedCount} reused={ProjectilePool.ReusedCount}");
        }

        private bool ResolveGame()
        {
            if (_game != null && _startCampaign != null && _beginRound != null && _spawnEnemy != null && _roundField != null)
                return true;

            _game = FindAnyObjectByType<TankGame>();
            if (_game == null) return false;

            Type type = typeof(TankGame);
            _startCampaign = type.GetMethod("StartCampaign", InstancePrivate);
            _beginRound = type.GetMethod("BeginRound", InstancePrivate);
            _spawnEnemy = type.GetMethod("SpawnEnemy", InstancePrivate);
            _roundField = type.GetField("_round", InstancePrivate);
            return _startCampaign != null && _beginRound != null && _spawnEnemy != null && _roundField != null;
        }

        private bool StartStage(int round)
        {
            try
            {
                if (!_game.IsPlaying) _startCampaign.Invoke(_game, null);
                ProjectilePool.ReleaseAllActive();
                _roundField.SetValue(_game, round);
                _beginRound.Invoke(_game, new object[] { round });
                ProtectPlayerSide();
                Debug.Log("[DemoCISoakProbe] stage round=" + round);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Fail("stage start failed round " + round + ": " + ex.GetType().Name);
                return false;
            }
        }

        private static void ProtectPlayerSide()
        {
            Health[] units = FindObjectsByType<Health>(FindObjectsSortMode.None);
            float protectedUntil = Time.time + TotalTimeoutSeconds + 10f;
            for (int i = 0; i < units.Length; i++)
            {
                Health health = units[i];
                if (health != null && !health.IsDead && health.Team == Team.Player)
                    health.InvulnerableUntil = protectedUntil;
            }
        }

        private bool InjectPressure()
        {
            EnemyKind[] pressure = { EnemyKind.Heavy, EnemyKind.Siege, EnemyKind.Sniper, EnemyKind.Elite };
            try
            {
                for (int i = 0; i < PressureUnitsPerStage; i++)
                    _spawnEnemy.Invoke(_game, new object[] { pressure[i % pressure.Length] });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Fail("pressure injection failed: " + ex.GetType().Name);
                return false;
            }
        }

        private void Pass(string details)
        {
            if (_completed) return;
            _completed = true;
            WriteMarker(true, details);
            Application.Quit(0);
        }

        private void Fail(string details)
        {
            if (_completed) return;
            _completed = true;
            WriteMarker(false, details);
            Application.Quit(24);
        }

        private static bool HasArgument(string expected)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void WriteMarker(bool pass, string details)
        {
            string file = pass ? "DEMO_SOAK_PASS.txt" : "DEMO_SOAK_FAIL.txt";
            string path = Path.Combine(Directory.GetCurrentDirectory(), file);
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "Late-round soak: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[DemoCISoakProbe] " + text.Replace("\n", " | "));
        }
    }
}
