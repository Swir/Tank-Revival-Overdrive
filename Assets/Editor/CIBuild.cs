using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TankRevival.Editor
{
    public static class CIBuild
    {
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string BuildFolder = "build/StandaloneWindows64";
        private const string ExePath = BuildFolder + "/TankRevivalOverdrive.exe";

        public static void BuildWindows()
        {
            Debug.Log("[Tank Revival CI] Preparing Windows build...");
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(BuildFolder);

            string version = ResolveVersion();
            Debug.Log("[Tank Revival CI] Project version=" + version);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("TankGame");
            root.AddComponent<TankRevival.TankGame>();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new Exception("Could not save bootstrap scene: " + ScenePath);
            }

            PlayerSettings.companyName = "SWIR Games";
            PlayerSettings.productName = "Tank Revival Overdrive";
            PlayerSettings.bundleVersion = version;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;

#pragma warning disable CS0618
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
#pragma warning restore CS0618

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ExePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[Tank Revival CI] Build result={summary.result}, size={summary.totalSize}, time={summary.totalTime}");
            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception("Tank Revival Windows build failed: " + summary.result);
            }

            string info =
                "TANK REVIVAL: ORZEL OVERDRIVE\n" +
                "Build: " + version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Target: Windows x64\n" +
                "Controls: WASD/Arrows move, Space/LeftCtrl fire, Q/E ammo, 1-7 ammo, P/Esc pause\n" +
                "Campaign: 100 rounds, Orzelek defense, Supply Tanks, special ammo, bosses and Field Command Center\n";
            File.WriteAllText(Path.Combine(BuildFolder, "BUILD_INFO.txt"), info);

            Debug.Log("[Tank Revival CI] Windows executable created at: " + ExePath);
        }

        private static string ResolveVersion()
        {
            const string versionFile = "VERSION";
            if (!File.Exists(versionFile)) return "0.0.0-dev";

            string raw = File.ReadAllText(versionFile).Trim();
            if (raw.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(1);
            return string.IsNullOrWhiteSpace(raw) ? "0.0.0-dev" : raw;
        }
    }
}
