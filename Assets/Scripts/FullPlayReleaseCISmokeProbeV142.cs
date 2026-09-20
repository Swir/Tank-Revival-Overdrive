using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(21000)]
    public sealed class FullPlayReleaseCISmokeProbeV142 : MonoBehaviour
    {
        private const float TimeoutSeconds = 24f;
        private float _startedAt;
        private float _stageAt;
        private int _stage;
        private bool _visual;
        private TankGame _game;
        private DemoExperienceDirector _shell;
        private FieldInfo _stateField;
        private MethodInfo _startCampaign;
        private MethodInfo _togglePause;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            bool runtime = HasArgument("-v142-full-play-smoke");
            bool visual = HasArgument("-v142-full-play-visual-smoke");
            if (!runtime && !visual) return;
            var go = new GameObject("FullPlayReleaseCISmokeProbe_v14.2");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<FullPlayReleaseCISmokeProbeV142>();
            probe._visual = visual;
        }

        private void Awake()
        {
            _startedAt = Time.realtimeSinceStartup;
            _stageAt = _startedAt;
        }

        private void Resolve()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                if (_game != null)
                {
                    Type type = typeof(TankGame);
                    _stateField = type.GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
                    _startCampaign = type.GetMethod("StartCampaign", BindingFlags.Instance | BindingFlags.NonPublic);
                    _togglePause = type.GetMethod("TogglePause", BindingFlags.Instance | BindingFlags.NonPublic);
                }
            }
            if (_shell == null) _shell = FindAnyObjectByType<DemoExperienceDirector>();
        }

        private string StateName()
        {
            if (_game == null || _stateField == null) return string.Empty;
            object value = _stateField.GetValue(_game);
            return value != null ? value.ToString() : string.Empty;
        }

        private void Update()
        {
            Resolve();
            if (Time.realtimeSinceStartup - _startedAt > TimeoutSeconds)
            {
                Fail("timeout stage=" + _stage + " state=" + StateName());
                return;
            }

            if (_stage == 0)
            {
                if (_game == null || _shell == null || !FullPlayReleaseGuardV142.Installed || _startCampaign == null || _togglePause == null) return;
                if (!DemoPlayerSettings.Fullscreen)
                {
                    Fail("fullscreen preference is not enabled on a clean release launch");
                    return;
                }
                _startCampaign.Invoke(_game, null);
                Advance();
                return;
            }

            if (_stage == 1)
            {
                if (StateName() != "Playing") return;
                if (_shell.enabled || !FullPlayReleaseGuardV142.GameplayShellSuppressed)
                {
                    Fail("legacy development shell remained visible during gameplay");
                    return;
                }
                if (_visual)
                {
                    if (Time.realtimeSinceStartup - _stageAt < 2.0f) return;
                    if (!Screen.fullScreen && Screen.fullScreenMode == FullScreenMode.Windowed)
                    {
                        Fail("visual release probe did not reach fullscreen mode");
                        return;
                    }
                    ScreenCapture.CaptureScreenshot("V14_2_FULL_PLAY_SCREENSHOT.png");
                    Advance();
                    return;
                }
                _togglePause.Invoke(_game, null);
                Advance();
                return;
            }

            if (_visual && _stage == 2)
            {
                if (Time.realtimeSinceStartup - _stageAt < 1.5f) return;
                if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "V14_2_FULL_PLAY_SCREENSHOT.png"))) return;
                Pass("visual fullscreen gameplay screenshot captured with legacy shell suppressed");
                return;
            }

            if (!_visual && _stage == 2)
            {
                if (StateName() != "Paused") return;
                if (!_shell.enabled)
                {
                    Fail("pause/settings shell did not return when gameplay paused");
                    return;
                }
                _togglePause.Invoke(_game, null);
                Advance();
                return;
            }

            if (!_visual && _stage == 3 && StateName() == "Playing")
            {
                if (_shell.enabled)
                {
                    Fail("legacy development shell returned after resume");
                    return;
                }
                Pass("menu->play->pause->resume flow; fullscreen default; clean gameplay shell policy");
            }
        }

        private void Advance()
        {
            _stage++;
            _stageAt = Time.realtimeSinceStartup;
        }

        private void Pass(string details)
        {
            string name = _visual ? "V14_2_FULL_PLAY_VISUAL_OK.txt" : "V14_2_FULL_PLAY_OK.txt";
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), name), "PASS\n" + details + "\n");
            Application.Quit(0);
        }

        private void Fail(string details)
        {
            string name = _visual ? "V14_2_FULL_PLAY_VISUAL_FAIL.txt" : "V14_2_FULL_PLAY_FAIL.txt";
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), name), "FAIL\n" + details + "\n");
            Application.Quit(42);
        }

        private static bool HasArgument(string expected)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
