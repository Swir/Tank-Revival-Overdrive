using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TankRevivalOverdrive.EditorTools
{
    public static class CIBuild
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

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

        public static void BuildWindows64()
        {
            string[] args = Environment.GetCommandLineArgs();
            string buildPath = GetArg(args, "-buildPath");
            if (string.IsNullOrWhiteSpace(buildPath))
                buildPath = Path.Combine("Builds", "Windows", "TankRevivalOverdrive.exe");

            buildPath = NormalizeExe(buildPath);

            string dir = Path.GetDirectoryName(buildPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            ValidateBootstrapScene();
            ValidateProductionAudioAssets();

            PlayerSettings.companyName = "SWIR";
            PlayerSettings.productName = "Tank Revival Overdrive";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScenePath },
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Console.WriteLine($"[CI] Building {options.target} -> {buildPath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"Build failed: {report.summary.result}, errors={report.summary.totalErrors}");

            Console.WriteLine($"[CI] Build OK: {report.summary.totalSize} bytes");
        }

        private static void ValidateBootstrapScene()
        {
            if (!File.Exists(BootstrapScenePath))
                throw new FileNotFoundException("Versioned bootstrap scene is missing.", BootstrapScenePath);

            string contents = File.ReadAllText(BootstrapScenePath);
            if (!contents.Contains("%YAML"))
                throw new InvalidOperationException("Bootstrap scene is not a serialized Unity scene.");

            string[] gameObjectLines = contents
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.TrimStart().StartsWith("--- !u!1 ", StringComparison.Ordinal))
                .ToArray();
            if (gameObjectLines.Length != 0)
                throw new InvalidOperationException("Bootstrap scene must stay empty; TankGame owns runtime bootstrap initialization.");

            Console.WriteLine($"[CI] Static bootstrap ready: {BootstrapScenePath}");
        }

        private static void ValidateProductionAudioAssets()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
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

                Console.WriteLine(
                    $"[CI] Production audio ready: path={path} guid={guid} clip={clip.name} samples={clip.samples} channels={clip.channels} frequency={clip.frequency}");
            }

            if (seenGuids.Count != RequiredProductionAudioAssets.Length)
                throw new InvalidOperationException("Production audio asset inventory is incomplete after import.");
        }

        private static string NormalizeExe(string path)
        {
            path = path.Replace('\\', '/');
            if (path.EndsWith("/TankRevivalOverdrive", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/TankRevivalOverdrive.exe", StringComparison.OrdinalIgnoreCase))
                return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? path : path + ".exe";

            if (string.IsNullOrEmpty(Path.GetExtension(path)))
                return Path.Combine(path, "TankRevivalOverdrive.exe");

            return path;
        }

        private static string GetArg(string[] args, string key)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
