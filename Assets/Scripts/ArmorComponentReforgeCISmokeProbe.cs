using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class ArmorComponentReforgeCISmokeProbe : MonoBehaviour
    {
        private const string PassFile = "ARMOR_COMPONENT_REFORGE_PASS.txt";
        private const string FailFile = "ARMOR_COMPONENT_REFORGE_FAIL.txt";
        private float _deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArg("-armor-component-reforge-smoke")) return;
            GameObject go = new GameObject("ArmorComponentReforgeCISmokeProbe_v11_1");
            DontDestroyOnLoad(go);
            go.AddComponent<ArmorComponentReforgeCISmokeProbe>();
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
                ComponentDamageTacticsDirector componentTactics = ComponentDamageTacticsDirector.Instance;
                TacticalCombatReforgeDirector tactical = TacticalCombatReforgeDirector.Instance;
                TacticalNavigationDirector navigation = TacticalNavigationDirector.Instance;
                ArmoredWarfareDirector armored = ArmoredWarfareDirector.Instance;
                if (game == null || componentTactics == null || tactical == null || navigation == null || armored == null)
                {
                    if (Time.realtimeSinceStartup < _deadline) return;
                    Fail("required armor/tactical runtime directors not installed");
                    return;
                }

                ValidateConfiguration();
                ValidateModuleStates();
                ValidateAmmoAndFacingModel();
                ValidateTacticalBounds();

                File.WriteAllText(PassFile,
                    "v11.1 Armor Facings / Component Damage packaged smoke PASS\n" +
                    "version=" + Application.version + "\n" +
                    "thresholds=damaged:" + ArmorSystem.DamagedThreshold +
                    ",critical:" + ArmorSystem.CriticalThreshold +
                    ",disabled:" + ArmorSystem.DisabledThreshold + "\n" +
                    "tactics=damaged:" + ComponentDamageTacticsDirector.MaxManagedDamagedUnits +
                    ",escorts:" + ComponentDamageTacticsDirector.MaxProtectionEscorts +
                    ",rear-flankers:" + ComponentDamageTacticsDirector.MaxRearExploitFlankers + "\n" +
                    "integration=ArmorSystem + Health + Projectile + ArmorHunterAgent + TacticalNavigationAgent + v11.0 platoons\n");
                Debug.Log("[CI] v11.1 Armor Component Reforge smoke PASS");
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
            if (Application.version != "11.1.0-dev")
                throw new InvalidOperationException("unexpected Application.version: " + Application.version);
            if (!ArmorSystem.ConfigurationValid)
                throw new InvalidOperationException("ArmorSystem.ConfigurationValid=false");
            if (!ComponentDamageTacticsDirector.ConfigurationValid)
                throw new InvalidOperationException("ComponentDamageTacticsDirector.ConfigurationValid=false");
            if (!TacticalCombatReforgeDirector.ConfigurationValid || !TacticalNavigationDirector.ConfigurationValid)
                throw new InvalidOperationException("existing tactical configuration drifted");
        }

        private static void ValidateModuleStates()
        {
            if (ArmorSystem.ConditionFor(100) != ModuleCondition.Operational)
                throw new InvalidOperationException("100 integrity must be Operational");
            if (ArmorSystem.ConditionFor(ArmorSystem.DamagedThreshold) != ModuleCondition.Damaged)
                throw new InvalidOperationException("damaged threshold mapping invalid");
            if (ArmorSystem.ConditionFor(ArmorSystem.CriticalThreshold) != ModuleCondition.Critical)
                throw new InvalidOperationException("critical threshold mapping invalid");
            if (ArmorSystem.ConditionFor(ArmorSystem.DisabledThreshold) != ModuleCondition.Disabled)
                throw new InvalidOperationException("disabled threshold mapping invalid");
        }

        private static void ValidateAmmoAndFacingModel()
        {
            if (!(ArmorSystem.ZoneDamageFactor(ArmorZone.Rear) > ArmorSystem.ZoneDamageFactor(ArmorZone.Side) &&
                  ArmorSystem.ZoneDamageFactor(ArmorZone.Side) > ArmorSystem.ZoneDamageFactor(ArmorZone.Front)))
                throw new InvalidOperationException("rear/side/front damage ordering invalid");
            if (ArmorSystem.AmmoPenetrationFactor(AmmoType.Plasma) <= ArmorSystem.AmmoPenetrationFactor(AmmoType.ArmorPiercing))
                throw new InvalidOperationException("Plasma penetration must exceed AP");
            if (ArmorSystem.AmmoPenetrationFactor(AmmoType.ArmorPiercing) <= ArmorSystem.AmmoPenetrationFactor(AmmoType.Basic))
                throw new InvalidOperationException("AP penetration must exceed Basic");
            if (ArmorSystem.AmmoPenetrationFactor(AmmoType.EMP) >= ArmorSystem.AmmoPenetrationFactor(AmmoType.Basic))
                throw new InvalidOperationException("EMP should trade raw penetration for module pressure");
        }

        private static void ValidateTacticalBounds()
        {
            if (ComponentDamageTacticsDirector.MaxManagedDamagedUnits > TacticalCombatReforgeDirector.MaxManagedCombatants)
                throw new InvalidOperationException("component-damage tactical cap exceeds v11.0 platoon authority");
            if (ComponentDamageTacticsDirector.MaxProtectionEscorts > ComponentDamageTacticsDirector.MaxManagedDamagedUnits)
                throw new InvalidOperationException("escort cap exceeds managed damaged-unit cap");
            if (ComponentDamageTacticsDirector.DecisionCadence < 0.25f)
                throw new InvalidOperationException("component tactical cadence too aggressive");
            if (!ComponentDamageTacticsDirector.IsPriorityProtectionTarget(EnemyKind.Heavy, null) == false)
                throw new InvalidOperationException("null armor must not become protection target");
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
            Debug.LogError("[CI] v11.1 Armor Component Reforge smoke FAIL: " + message);
            Application.Quit(2);
            enabled = false;
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
