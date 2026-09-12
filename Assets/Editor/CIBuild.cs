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
            BuildInternal(false);
        }

        public static void BuildDemoCandidate()
        {
            BuildInternal(true);
        }

        private static void BuildInternal(bool demoCandidate)
        {
            Debug.Log("[Tank Revival CI] Preparing " + (demoCandidate ? "DEMO CANDIDATE" : "development") + " Windows build...");
            Directory.CreateDirectory("Assets/Scenes");
            if (Directory.Exists(BuildFolder)) Directory.Delete(BuildFolder, true);
            Directory.CreateDirectory(BuildFolder);

            string version = ResolveVersion();
            if (demoCandidate && version.IndexOf("dev", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new Exception("Demo candidate VERSION must not contain '-dev': " + version);

            Debug.Log("[Tank Revival CI] Project version=" + version);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("TankGame");
            root.AddComponent<TankRevival.TankGame>();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new Exception("Could not save bootstrap scene: " + ScenePath);

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
                options = demoCandidate ? BuildOptions.CompressWithLz4HC : BuildOptions.CompressWithLz4HC | BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[Tank Revival CI] Build result={summary.result}, size={summary.totalSize}, time={summary.totalTime}");
            if (summary.result != BuildResult.Succeeded)
                throw new Exception("Tank Revival Windows build failed: " + summary.result);

            string channel = demoCandidate ? "PUBLIC DEMO CANDIDATE" : "DEVELOPMENT";
            string info =
                "TANK REVIVAL: ORZEL OVERDRIVE\n" +
                "Channel: " + channel + "\n" +
                "Build: " + version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Target: Windows x64\n" +
                "Controls: WASD/Arrows move, Mouse aim, LMB/Space/LeftCtrl fire, Q/E ammo, 1-7 ammo, P/Esc pause\n" +
                "Campaign: 100 rounds, Orzelek defense, Supply Tanks, special ammo, bosses and Field Command Center\n";
            File.WriteAllText(Path.Combine(BuildFolder, "BUILD_INFO.txt"), info);

            if (demoCandidate)
            {
                string sha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local-build";
                string manifest =
                    "Tank Revival: Orzel Overdrive\n" +
                    "Demo candidate: " + version + "\n" +
                    "Commit: " + sha + "\n" +
                    "Target: Windows x64\n" +
                    "Executable: TankRevivalOverdrive.exe\n" +
                    "Runtime data: TankRevivalOverdrive_Data\n";
                File.WriteAllText(Path.Combine(BuildFolder, "DEMO_MANIFEST.txt"), manifest);

                string readme =
                    "TANK REVIVAL: ORZEL OVERDRIVE — DEMO CANDIDATE\n\n" +
                    "1. Rozpakuj caly ZIP do osobnego folderu.\n" +
                    "2. Uruchom TankRevivalOverdrive.exe.\n" +
                    "3. Nie przenos samego EXE bez folderu TankRevivalOverdrive_Data.\n\n" +
                    "Sterowanie: WASD/strzalki ruch, mysz celowanie, LPM/Spacja/Lewy Ctrl ogien, Q/E lub 1-7 amunicja, ESC/P pauza.\n" +
                    "Cel: obron Orzelka przez 100 rund.\n";
                File.WriteAllText(Path.Combine(BuildFolder, "README_DEMO.txt"), readme);
            }

            Debug.Log("[Tank Revival CI] Windows executable created at: " + ExePath);
        }

        private static string ResolveVersion()
        {
            const string versionFile = "VERSION";
            if (!File.Exists(versionFile)) return "0.0.0-dev";
            string raw = File.ReadAllText(versionFile).Trim();
            if (raw.StartsWith("v", StringComparison.OrdinalIgnoreCase)) raw = raw.Substring(1);
            return string.IsNullOrWhiteSpace(raw) ? "0.0.0-dev" : raw;
        }
    }
}
