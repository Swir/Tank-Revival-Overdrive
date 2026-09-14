using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class AdaptiveEnemyHighCommandCISmokeProbe : MonoBehaviour
    {
        private const string PassFile = "HIGH_COMMAND_PASS.txt";
        private const string FailFile = "HIGH_COMMAND_FAIL.txt";
        private float _deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArg("-adaptive-high-command-smoke")) return;
            var go = new GameObject("AdaptiveEnemyHighCommandCISmokeProbe_v10_3");
            DontDestroyOnLoad(go);
            go.AddComponent<AdaptiveEnemyHighCommandCISmokeProbe>();
        }

        private void Awake()
        {
            SafeDelete(PassFile);
            SafeDelete(FailFile);
            _deadline = Time.realtimeSinceStartup + 14f;
        }

        private void Update()
        {
            try
            {
                TankGame game = FindAnyObjectByType<TankGame>();
                AdaptiveEnemyHighCommandDirector highCommand = AdaptiveEnemyHighCommandDirector.Instance;
                TheaterConsequenceEngineDirector consequence = TheaterConsequenceEngineDirector.Instance;
                TacticalNavigationDirector navigation = TacticalNavigationDirector.Instance;
                if (game == null || highCommand == null || consequence == null || navigation == null)
                {
                    if (Time.realtimeSinceStartup < _deadline) return;
                    Fail("runtime directors not installed");
                    return;
                }

                ValidateConfiguration();
                ValidateDoctrineMapping();
                ValidateRoundWindows();
                ValidateIntegration();

                File.WriteAllText(PassFile,
                    "v10.3 adaptive enemy high command packaged smoke PASS\n" +
                    "version=" + Application.version + "\n" +
                    "caps=retask:" + AdaptiveEnemyHighCommandDirector.MaxRetaskedCombatants +
                    ",shells:" + AdaptiveEnemyHighCommandDirector.MaxCounterFireShells + "\n" +
                    "window=offsets:" + AdaptiveEnemyHighCommandDirector.PlanStartOffset + "-" + AdaptiveEnemyHighCommandDirector.PlanEndOffset + "\n" +
                    "integration=v10.2 consequence history + tactical navigation + existing TankGame/Health authority\n");
                Debug.Log("[CI] v10.3 adaptive enemy high command smoke PASS");
                Application.Quit(0);
                enabled = false;
            }
            catch (Exception ex)
            {
                Fail(ex.ToString());
            }
        }

        private static void ValidateConfiguration()
        {
            if (Application.version != "10.3.0-dev")
                throw new InvalidOperationException("unexpected Application.version: " + Application.version);
            if (!AdaptiveEnemyHighCommandDirector.ConfigurationValid)
                throw new InvalidOperationException("AdaptiveEnemyHighCommandDirector.ConfigurationValid=false");
            if (!TacticalNavigationDirector.ConfigurationValid)
                throw new InvalidOperationException("TacticalNavigationDirector.ConfigurationValid=false");
            if (AdaptiveEnemyHighCommandDirector.MaxRetaskedCombatants > 4)
                throw new InvalidOperationException("retask cap exceeds four combatants");
            if (AdaptiveEnemyHighCommandDirector.MaxCounterFireShells > 2)
                throw new InvalidOperationException("counter-fire cap exceeds two shells");
        }

        private static void ValidateDoctrineMapping()
        {
            EnemyCounterDoctrine armor = AdaptiveEnemyHighCommandDirector.ResolveCounterDoctrine(
                TheaterSectorDoctrine.Breakthrough, TheaterSectorDoctrine.Breakthrough, TheaterSectorDoctrine.PreparedDefense);
            if (armor != EnemyCounterDoctrine.ArmorTrap)
                throw new InvalidOperationException("Breakthrough history must resolve ARMOR TRAP");

            EnemyCounterDoctrine logistics = AdaptiveEnemyHighCommandDirector.ResolveCounterDoctrine(
                TheaterSectorDoctrine.SupplyStarved, TheaterSectorDoctrine.SupplyStarved, TheaterSectorDoctrine.Breakthrough);
            if (logistics != EnemyCounterDoctrine.DispersedLogistics)
                throw new InvalidOperationException("SupplyStarved history must resolve DISPERSED LOGISTICS");

            EnemyCounterDoctrine siege = AdaptiveEnemyHighCommandDirector.ResolveCounterDoctrine(
                TheaterSectorDoctrine.PreparedDefense, TheaterSectorDoctrine.PreparedDefense, TheaterSectorDoctrine.SupplyStarved);
            if (siege != EnemyCounterDoctrine.SiegeBreach)
                throw new InvalidOperationException("PreparedDefense history must resolve SIEGE BREACH");

            if (AdaptiveEnemyHighCommandDirector.ResolveCounterDoctrine(TheaterSectorDoctrine.None, TheaterSectorDoctrine.None, TheaterSectorDoctrine.None) != EnemyCounterDoctrine.None)
                throw new InvalidOperationException("empty doctrine history must stay None");

            if (AdaptiveEnemyHighCommandDirector.CounterShellCap(EnemyCounterDoctrine.ArmorTrap) != 2 ||
                AdaptiveEnemyHighCommandDirector.CounterShellCap(EnemyCounterDoctrine.DispersedLogistics) != 1 ||
                AdaptiveEnemyHighCommandDirector.CounterShellCap(EnemyCounterDoctrine.SiegeBreach) != 2 ||
                AdaptiveEnemyHighCommandDirector.CounterShellCap(EnemyCounterDoctrine.None) != 0)
                throw new InvalidOperationException("counter-shell caps drifted");
        }

        private static void ValidateRoundWindows()
        {
            for (int round = 1; round <= 100; round++)
            {
                int expectedSector = Mathf.Clamp((round - 1) / 10, 0, 9);
                if (AdaptiveEnemyHighCommandDirector.SectorForRound(round) != expectedSector)
                    throw new InvalidOperationException("sector mapping mismatch at round " + round);

                int offset = (round - 1) % 10;
                bool expected = round % 10 != 0 && offset >= 4 && offset <= 7;
                if (AdaptiveEnemyHighCommandDirector.IsPlanActiveForRound(round) != expected)
                    throw new InvalidOperationException("war-plan window mismatch at round " + round);
            }

            for (int sector = 0; sector < 10; sector++)
            {
                int first = sector * 10 + 1;
                for (int offset = 0; offset < 4; offset++)
                    if (AdaptiveEnemyHighCommandDirector.IsPlanActiveForRound(first + offset))
                        throw new InvalidOperationException("v10.3 must not overlap v10.2 opening consequence window");
                if (AdaptiveEnemyHighCommandDirector.IsPlanActiveForRound(first + 8))
                    throw new InvalidOperationException("sector pre-boss round must remain outside high-command window");
                if (AdaptiveEnemyHighCommandDirector.IsPlanActiveForRound(first + 9))
                    throw new InvalidOperationException("boss/end-sector round must remain outside high-command window");
            }
        }

        private static void ValidateIntegration()
        {
            if (TheaterConsequenceEngineDirector.SectorCount != 10)
                throw new InvalidOperationException("v10.2 sector count drifted");
            if (EnemySquadTacticsDirector.RoleCount != 6)
                throw new InvalidOperationException("tactical role contract drifted");

            EnemyCounterDoctrine mixed = AdaptiveEnemyHighCommandDirector.ResolveCounterDoctrine(
                TheaterSectorDoctrine.PreparedDefense, TheaterSectorDoctrine.Breakthrough, TheaterSectorDoctrine.Breakthrough);
            if (mixed == EnemyCounterDoctrine.None)
                throw new InvalidOperationException("weighted doctrine history unexpectedly resolved None");
        }

        private static bool HasArg(string arg)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], arg, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void Fail(string message)
        {
            try { File.WriteAllText(FailFile, message); } catch { }
            Debug.LogError("[CI] v10.3 adaptive enemy high command smoke FAIL: " + message);
            Application.Quit(2);
            enabled = false;
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
