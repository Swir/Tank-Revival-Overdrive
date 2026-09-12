using UnityEngine;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;

namespace TankRevival
{
    public sealed class LateRoundStressHarness : MonoBehaviour
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private TankGame _game;
        private MethodInfo _startCampaign;
        private MethodInfo _beginRound;
        private MethodInfo _spawnEnemy;
        private FieldInfo _roundField;
        private bool _show = true;
        private bool _autoSoak;
        private float _smoothedFps = 120f;
        private float _soakMinFps = 999f;
        private float _soakMaxGcMb;
        private int _soakStartWarnings;
        private int _soakStartRepairs;
        private string _soakStatus = "MANUAL";
        private string _lastAction = "READY";
        private float _lastActionUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<LateRoundStressHarness>() != null) return;
            var go = new GameObject("LateRoundStressHarness_v4.8");
            DontDestroyOnLoad(go);
            go.AddComponent<LateRoundStressHarness>();
        }

        private void Update()
        {
            float fps = Time.unscaledDeltaTime > 0.0001f ? 1f / Time.unscaledDeltaTime : 120f;
            _smoothedFps = Mathf.Lerp(_smoothedFps, fps, 0.08f);
            if (_autoSoak)
            {
                _soakMinFps = Mathf.Min(_soakMinFps, fps);
                _soakMaxGcMb = Mathf.Max(_soakMaxGcMb, GC.GetTotalMemory(false) / (1024f * 1024f));
            }

            if (Input.GetKeyDown(KeyCode.F4)) _show = !_show;
            if (Input.GetKeyDown(KeyCode.F5) && !_autoSoak) StartCoroutine(AutoSoak());
            if (Input.GetKeyDown(KeyCode.F6)) JumpToRound(80);
            if (Input.GetKeyDown(KeyCode.F7)) JumpToRound(90);
            if (Input.GetKeyDown(KeyCode.F8)) JumpToRound(100);
            if (Input.GetKeyDown(KeyCode.F9)) SpawnPressureWave();
        }

        private bool ResolveGame()
        {
            if (_game != null) return true;
            _game = FindAnyObjectByType<TankGame>();
            if (_game == null) return false;
            Type type = typeof(TankGame);
            _startCampaign = type.GetMethod("StartCampaign", InstancePrivate);
            _beginRound = type.GetMethod("BeginRound", InstancePrivate);
            _spawnEnemy = type.GetMethod("SpawnEnemy", InstancePrivate);
            _roundField = type.GetField("_round", InstancePrivate);
            return _startCampaign != null && _beginRound != null && _spawnEnemy != null && _roundField != null;
        }

        private void JumpToRound(int round)
        {
            if (!ResolveGame()) { Action("TANKGAME NOT FOUND"); return; }
            try
            {
                if (!_game.IsPlaying) _startCampaign.Invoke(_game, null);
                ProjectilePool.ReleaseAllActive();
                int target = Mathf.Clamp(round, 1, 100);
                _roundField.SetValue(_game, target);
                _beginRound.Invoke(_game, new object[] { target });
                Action($"JUMP -> ROUND {target:000}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Action("JUMP FAILED");
            }
        }

        private void SpawnPressureWave()
        {
            if (!ResolveGame() || !_game.IsPlaying) { Action("START CAMPAIGN FIRST"); return; }
            EnemyKind[] pressure = { EnemyKind.Heavy, EnemyKind.Siege, EnemyKind.Sniper, EnemyKind.Elite };
            try
            {
                for (int i = 0; i < 24; i++)
                    _spawnEnemy.Invoke(_game, new object[] { pressure[i % pressure.Length] });
                Action("+24 STRESS ENEMIES");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Action("PRESSURE WAVE FAILED");
            }
        }

        private IEnumerator AutoSoak()
        {
            if (!ResolveGame())
            {
                Action("AUTO SOAK: TANKGAME NOT FOUND");
                yield break;
            }

            _autoSoak = true;
            _soakStatus = "RUNNING";
            _soakMinFps = 999f;
            _soakMaxGcMb = 0f;
            RuntimeStabilityDirector stability = RuntimeStabilityDirector.Instance;
            _soakStartWarnings = stability != null ? stability.Warnings : 0;
            _soakStartRepairs = stability != null ? stability.Repairs : 0;

            int[] rounds = { 80, 90, 100 };
            for (int r = 0; r < rounds.Length; r++)
            {
                JumpToRound(rounds[r]);
                yield return new WaitForSecondsRealtime(4f);
                SpawnPressureWave();
                float until = Time.unscaledTime + 12f;
                while (Time.unscaledTime < until)
                    yield return null;

                if (!ProjectilePool.ValidateIntegrity(out string reason))
                {
                    _soakStatus = "FAIL: " + reason;
                    _autoSoak = false;
                    Debug.LogError("[v4.8 Soak] " + _soakStatus);
                    yield break;
                }
            }

            stability = RuntimeStabilityDirector.Instance;
            int warningDelta = stability != null ? stability.Warnings - _soakStartWarnings : 0;
            int repairDelta = stability != null ? stability.Repairs - _soakStartRepairs : 0;
            _soakStatus = warningDelta == 0 && repairDelta == 0 ? "PASS" : $"CHECK W{warningDelta}/R{repairDelta}";
            _autoSoak = false;
            string summary = $"[v4.8 Soak] {_soakStatus} | minFPS {_soakMinFps:0.0} | maxGC {_soakMaxGcMb:0.0}MB | pool made {ProjectilePool.CreatedCount} reused {ProjectilePool.ReusedCount}";
            Debug.Log(summary);
            Action("AUTO SOAK " + _soakStatus);
        }

        private void Action(string text)
        {
            _lastAction = text;
            _lastActionUntil = Time.unscaledTime + 2.5f;
            Debug.Log("[v4.8 Stress] " + text);
        }

        private void OnGUI()
        {
            if (!_show) return;
            float mb = GC.GetTotalMemory(false) / (1024f * 1024f);
            RuntimeStabilityDirector stability = RuntimeStabilityDirector.Instance;
            string health = stability == null ? "N/A" : stability.Healthy ? "OK" : "CHECK";
            string line1 = $"v4.8 STRESS  FPS {_smoothedFps:0}  FRAME {(1000f / Mathf.Max(1f, _smoothedFps)):0.0}ms  GC {mb:0.0}MB  HEALTH {health}";
            string line2 = $"POOL active {ProjectilePool.ActiveCount} / idle {ProjectilePool.InactiveCount} / made {ProjectilePool.CreatedCount} / reused {ProjectilePool.ReusedCount} / faults {ProjectilePool.IntegrityFaultCount}";
            string line3 = $"REGISTRY {RuntimeBattleRegistry.RegisteredHealthCount}  ENEMY {CombatRoster.LivingEnemyCount}  FX {WarfarePerformanceGovernor.Tier}  SOAK {_soakStatus}";
            GUI.Box(new Rect(Screen.width - 520f, 10f, 510f, 112f), string.Empty);
            GUI.Label(new Rect(Screen.width - 508f, 18f, 490f, 22f), line1);
            GUI.Label(new Rect(Screen.width - 508f, 42f, 490f, 22f), line2);
            GUI.Label(new Rect(Screen.width - 508f, 66f, 490f, 22f), line3);
            GUI.Label(new Rect(Screen.width - 508f, 88f, 490f, 20f), "F4 HUD • F5 AUTO SOAK • F6 R80 • F7 R90 • F8 R100 • F9 +24");
            if (Time.unscaledTime < _lastActionUntil)
                GUI.Label(new Rect(Screen.width - 508f, 110f, 490f, 22f), _lastAction);
        }
    }
}
#endif
