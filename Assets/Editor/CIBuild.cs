using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TankRevival.Editor
{
    public static class CIBuild
    {
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string BuildFolder = "build/StandaloneWindows64";
        private const string ExePath = BuildFolder + "/TankRevivalOverdrive.exe";

        private static readonly string[] RequiredProductionAudioAssets =
        {
            "Assets/Resources/TankRevivalProduction/Audio/HeavyCannon.wav",
            "Assets/Resources/TankRevivalProduction/Audio/BossAlarm.wav"
        };

        private static readonly Dictionary<string, string> RequiredProductionAudioGuids =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Assets/Resources/TankRevivalProduction/Audio/HeavyCannon.wav", "4e496dac21264c68a0c77e2597b08a72" },
                { "Assets/Resources/TankRevivalProduction/Audio/BossAlarm.wav", "a9423fa26ea64656a77b6a6552d46332" }
            };

        public static void BuildWindows()
        {
            BuildInternal(false);
        }

        public static void BuildDemoCandidate()
        {
            BuildInternal(true);
        }

        private static void BuildInternal(bool releaseCandidate)
        {
            Debug.Log("[Tank Revival CI] Preparing " + (releaseCandidate ? "FULL-PLAY RELEASE CANDIDATE" : "development") + " Windows build...");
            if (Directory.Exists(BuildFolder)) Directory.Delete(BuildFolder, true);
            Directory.CreateDirectory(BuildFolder);

            string version = ResolveVersion();
            if (releaseCandidate && version.IndexOf("dev", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new Exception("Release candidate VERSION must not contain '-dev': " + version);

            Debug.Log("[Tank Revival CI] Project version=" + version);
            ValidateStaticBootstrapScene();
            ValidateProductionAudioAssets();

            PlayerSettings.companyName = "SWIR Games";
            PlayerSettings.productName = "Tank Revival Overdrive";
            PlayerSettings.bundleVersion = version;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = releaseCandidate ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
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
                options = releaseCandidate ? BuildOptions.CompressWithLz4HC : BuildOptions.CompressWithLz4HC | BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[Tank Revival CI] Build result={summary.result}, size={summary.totalSize}, time={summary.totalTime}");
            if (summary.result != BuildResult.Succeeded)
                throw new Exception("Tank Revival Windows build failed: " + summary.result);

            string channel = releaseCandidate ? version.ToUpperInvariant() + " FULL-PLAY RELEASE CANDIDATE" : "DEVELOPMENT";
            string info =
                "TANK REVIVAL: ORZEL OVERDRIVE\n" +
                "Channel: " + channel + "\n" +
                "Build: " + version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Target: Windows x64\n" +
                "Display default: borderless fullscreen at the current monitor resolution\n" +
                "Controls: WASD/Arrows or gamepad left stick move, Mouse/facing aim, LMB/Space/LeftCtrl or gamepad A fire, Q/E or LB/RB ammo, 1-7 ammo, R smoke, C ECM, V decoy, G SIGINT, H recon, P/Esc or Start pause\n" +
                "Campaign: 100 rounds, Orzelek defense, objectives, convoys, bosses, EW command network, smoke screening and Mobile HQ operations\n";
            File.WriteAllText(Path.Combine(BuildFolder, "BUILD_INFO.txt"), info);

            if (releaseCandidate)
            {
                string sha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local-build";
                string manifest =
                    "Tank Revival: Orzel Overdrive\n" +
                    "Full-play release candidate: " + version + "\n" +
                    "Commit: " + sha + "\n" +
                    "Target: Windows x64\n" +
                    "Executable: TankRevivalOverdrive.exe\n" +
                    "Runtime data: TankRevivalOverdrive_Data\n" +
                    "Default display: fullscreen\n" +
                    "Qualification: full-play flow + v14.3/v14.2/v14.1/v14.0/v13.9/v13.8 + rounds 80/90/100\n";
                File.WriteAllText(Path.Combine(BuildFolder, "DEMO_MANIFEST.txt"), manifest);

                string readme =
                    "TANK REVIVAL: ORZEL OVERDRIVE — " + version.ToUpperInvariant() + " FULL-PLAY RELEASE CANDIDATE\n\n" +
                    "1. Rozpakuj caly ZIP do osobnego folderu.\n" +
                    "2. Uruchom TankRevivalOverdrive.exe.\n" +
                    "3. Gra startuje domyslnie na pelnym ekranie; tryb ekranu i rozdzielczosc zmienisz w Ustawieniach.\n" +
                    "4. Nie przenos samego EXE bez folderu TankRevivalOverdrive_Data.\n\n" +
                    "Klawiatura: WASD/strzalki ruch, mysz celowanie, LPM/Spacja/Lewy Ctrl ogien, Q/E lub 1-7 amunicja, ESC/P pauza.\n" +
                    "Gamepad: lewy stick ruch, A ogien w kierunku jazdy/celowania, LB/RB amunicja, Start pauza.\n" +
                    "Kontry taktyczne: R dym, C ECM, V wabik, G SIGINT, H dron rozpoznawczy.\n" +
                    "Cel: obron Orzelka przez 100 rund, wykorzystuj smoke/break-contact counterplay, niszcz siec dowodzenia i przetrwaj operacje Mobile HQ.\n";
                File.WriteAllText(Path.Combine(BuildFolder, "README_DEMO.txt"), readme);
            }

            Debug.Log("[Tank Revival CI] Windows executable created at: " + ExePath);
        }

        private static void ValidateStaticBootstrapScene()
        {
            if (!File.Exists(ScenePath))
                throw new Exception("Versioned bootstrap scene is missing: " + ScenePath);

            string guid = AssetDatabase.AssetPathToGUID(ScenePath);
            if (string.IsNullOrWhiteSpace(guid))
                throw new Exception("Bootstrap scene has no stable Unity asset GUID: " + ScenePath);

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
                throw new Exception("Bootstrap scene could not be imported as a SceneAsset: " + ScenePath);

            Debug.Log("[Tank Revival CI] Using immutable bootstrap scene " + ScenePath + " guid=" + guid);
        }

        private static void ValidateProductionAudioAssets()
        {
            var seenGuids = new HashSet<string>(StringComparer.Ordinal);

            foreach (string path in RequiredProductionAudioAssets)
            {
                if (!File.Exists(path))
                    throw new FileNotFoundException("Required production audio asset is missing.", path);

                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                    throw new InvalidOperationException($"Production audio importer unavailable: {path}");

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                    throw new InvalidOperationException($"Production audio asset did not import as AudioClip: {path}");
                if (clip.samples <= 0 || clip.channels <= 0 || clip.frequency <= 0)
                    throw new InvalidOperationException(
                        $"Production audio asset has invalid decoded metadata: {path} samples={clip.samples} channels={clip.channels} frequency={clip.frequency}");

                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrWhiteSpace(guid) || !seenGuids.Add(guid))
                    throw new InvalidOperationException($"Production audio GUID is missing or duplicated: {path} ({guid})");
                if (!RequiredProductionAudioGuids.TryGetValue(path, out string expectedGuid) ||
                    !string.Equals(guid, expectedGuid, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Production audio GUID changed: {path} ({guid} != {expectedGuid})");

                Debug.Log(
                    $"[Tank Revival CI] Production audio ready: path={path} guid={guid} clip={clip.name} samples={clip.samples} channels={clip.channels} frequency={clip.frequency}");
            }

            if (seenGuids.Count != RequiredProductionAudioAssets.Length)
                throw new InvalidOperationException("Production audio asset inventory is incomplete after import.");
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
