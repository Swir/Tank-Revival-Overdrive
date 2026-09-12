using UnityEngine;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
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
        private float _smoothedFps = 120f;
        private string _lastAction = "READY";
        private float _lastActionUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<LateRoundStressHarness>() != null) return;
            var go = new GameObject("LateRoundStressHarness_v4.7");
            DontDestroyOnLoad(go);
            go.AddComponent<LateRoundStressHarness>();
        }

        private void Update()
        {
            float fps = Time.unscaledDeltaTime > 0.0001f ? 1f / Time.unscaledDeltaTime : 120f;
            _smoothedFps = Mathf.Lerp(_smoothedFps, fps, 0.08f);
            if (Input.GetKeyDown(KeyCode.F4)) _show = !_show;
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

        private void Action(string text)
        {
            _lastAction = text;
            _lastActionUntil = Time.unscaledTime + 2.5f;
            Debug.Log("[v4.7 Stress] " + text);
        }

        private void OnGUI()
        {
            if (!_show) return;
            float mb = GC.GetTotalMemory(false) / (1024f * 1024f);
            string line1 = $"v4.7 STRESS  FPS {_smoothedFps:0}  FRAME {(1000f / Mathf.Max(1f, _smoothedFps)):0.0}ms  GC {mb:0.0}MB";
            string line2 = $"POOL active {ProjectilePool.ActiveCount} / idle {ProjectilePool.InactiveCount} / made {ProjectilePool.CreatedCount} / reused {ProjectilePool.ReusedCount}";
            string line3 = $"ROUTED {ProjectilePool.RoutedLegacySpawns}  REGISTRY {RuntimeBattleRegistry.RegisteredHealthCount}  ENEMY {CombatRoster.LivingEnemyCount}  FX {WarfarePerformanceGovernor.Tier}";
            GUI.Box(new Rect(Screen.width - 480f, 10f, 470f, 106f), string.Empty);
            GUI.Label(new Rect(Screen.width - 468f, 18f, 450f, 22f), line1);
            GUI.Label(new Rect(Screen.width - 468f, 42f, 450f, 22f), line2);
            GUI.Label(new Rect(Screen.width - 468f, 66f, 450f, 22f), line3);
            GUI.Label(new Rect(Screen.width - 468f, 88f, 450f, 20f), "F4 HUD • F6 R80 • F7 R90 • F8 R100 • F9 +24 pressure");
            if (Time.unscaledTime < _lastActionUntil)
                GUI.Label(new Rect(Screen.width - 468f, 112f, 450f, 22f), _lastAction);
        }
    }
}
#endif
