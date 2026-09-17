using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE qualification for v12.9 component degradation and finite emergency repair warfare.</summary>
    public sealed class ComponentDamageRepairCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V12_9_COMPONENT_DAMAGE_REPAIR_OK.txt";
        public const string FailMarker = "V12_9_COMPONENT_DAMAGE_REPAIR_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v129-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<ComponentDamageRepairCISmokeProbe>() != null) return;
                GameObject go = new GameObject("ComponentDamageRepairCISmokeProbe_v12_9");
                DontDestroyOnLoad(go);
                go.AddComponent<ComponentDamageRepairCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            GameObject fixture = null;
            try
            {
                if (!ComponentDamageWarfare.ConfigurationValid()) { Fail("component damage profile configuration invalid"); return; }
                if (!ArmorSystem.ConfigurationValid) { Fail("ArmorSystem v12.9 configuration invalid"); return; }
                if (!EmergencyRepairSystem.ConfigurationValid) { Fail("emergency repair configuration invalid"); return; }
                if (!ComponentDamageRepairDirector.ConfigurationValid) { Fail("component repair director configuration invalid"); return; }
                if (ComponentDamageRepairDirector.Instance == null) { Fail("component repair director was not installed at runtime"); return; }

                if (ArmorSystem.ConditionFor(71) != ModuleCondition.Operational ||
                    ArmorSystem.ConditionFor(70) != ModuleCondition.Damaged ||
                    ArmorSystem.ConditionFor(36) != ModuleCondition.Damaged ||
                    ArmorSystem.ConditionFor(35) != ModuleCondition.Critical ||
                    ArmorSystem.ConditionFor(13) != ModuleCondition.Critical ||
                    ArmorSystem.ConditionFor(12) != ModuleCondition.Disabled ||
                    ArmorSystem.ConditionFor(0) != ModuleCondition.Disabled)
                { Fail("module condition threshold boundaries are invalid"); return; }

                AmmoType[] ammo = {
                    AmmoType.Basic, AmmoType.Twin, AmmoType.ArmorPiercing, AmmoType.Explosive,
                    AmmoType.Plasma, AmmoType.EMP, AmmoType.Incendiary
                };
                ArmorZone[] zones = { ArmorZone.Front, ArmorZone.Side, ArmorZone.Rear };
                int matrixCases = 0;
                int severitySum = 0;
                for (int a = 0; a < ammo.Length; a++)
                {
                    for (int z = 0; z < zones.Length; z++)
                    {
                        TankModule module = ComponentDamageWarfare.PreferredModule(ammo[a], zones[z]);
                        int normal = ComponentDamageWarfare.ComputeDamage(2, ammo[a], zones[z], module, false, false);
                        int repeated = ComponentDamageWarfare.ComputeDamage(2, ammo[a], zones[z], module, false, false);
                        int severe = ComponentDamageWarfare.ComputeDamage(4, ammo[a], zones[z], module, true, true);
                        if (module == TankModule.None || normal != repeated || normal < ComponentDamageWarfare.MinModuleDamage ||
                            normal > ComponentDamageWarfare.MaxModuleDamage || severe < normal || severe > ComponentDamageWarfare.MaxModuleDamage)
                        { Fail("7x3 ammo/zone component matrix violated determinism or damage bounds"); return; }
                        matrixCases++;
                        severitySum += severe;
                    }
                }
                if (matrixCases != 21) { Fail("component matrix coverage mismatch"); return; }
                if (ComponentDamageWarfare.PreferredModule(AmmoType.Explosive, ArmorZone.Side) != TankModule.Tracks ||
                    ComponentDamageWarfare.PreferredModule(AmmoType.EMP, ArmorZone.Front) != TankModule.Gun ||
                    ComponentDamageWarfare.PreferredModule(AmmoType.Incendiary, ArmorZone.Rear) != TankModule.Engine)
                { Fail("representative ammunition-to-subsystem hierarchy regressed"); return; }

                fixture = new GameObject("v12_9_component_repair_fixture");
                Health health = fixture.AddComponent<Health>();
                health.Initialize(Team.Player, 200, 173);
                ArmorSystem armor = fixture.AddComponent<ArmorSystem>();
                armor.InitializePlayer();
                EmergencyRepairSystem repair = fixture.AddComponent<EmergencyRepairSystem>();
                repair.ConfigureForSmoke();

                int hpBefore = health.Current;
                float mobilityBefore = armor.MobilityMultiplier;
                float reloadBefore = armor.ReloadMultiplier;
                float weaponBefore = armor.WeaponFunctionMultiplier;

                armor.ApplyModuleDamageDeterministic(TankModule.Engine, 62, Vector2.zero, false);
                armor.ApplyModuleDamageDeterministic(TankModule.Gun, 62, Vector2.zero, false);
                if (!(armor.MobilityMultiplier < mobilityBefore && armor.ReloadMultiplier > reloadBefore && armor.WeaponFunctionMultiplier < weaponBefore))
                { Fail("component degradation is not connected to handling/reload/fire-control multipliers"); return; }
                if (health.Current != hpBefore) { Fail("module degradation modified Health"); return; }

                int engineBeforeRepair = armor.EngineIntegrity;
                if (!repair.CompleteImmediatelyForSmoke()) { Fail("first finite field repair failed"); return; }
                if (repair.ChargesRemaining != 1 || repair.CompletedRepairs != 1 || armor.EngineIntegrity <= engineBeforeRepair)
                { Fail("first field repair did not consume one charge and restore the priority module"); return; }
                if (health.Current != hpBefore) { Fail("field repair healed Health"); return; }

                armor.ApplyModuleDamageDeterministic(TankModule.Tracks, 62, Vector2.zero, false);
                int tracksBeforeRepair = armor.TrackIntegrity;
                if (!repair.CompleteImmediatelyForSmoke()) { Fail("second finite field repair failed"); return; }
                if (repair.ChargesRemaining != 0 || repair.CompletedRepairs != 2 || armor.TrackIntegrity <= tracksBeforeRepair)
                { Fail("second field repair did not exhaust the finite repair budget correctly"); return; }
                if (repair.CompleteImmediatelyForSmoke()) { Fail("third field repair succeeded with zero charges"); return; }
                if (health.Current != hpBefore) { Fail("finite repair sequence modified Health"); return; }

                float playerSpread = FireControlBallisticsDirector.SpreadDegrees(AmmoType.Basic, 0f, armor);
                float enemySpread = FireControlBallisticsDirector.EnemySpreadDegrees(EnemyKind.Basic, 0f, armor, false);
                if (playerSpread <= 1.0f || enemySpread <= 1.45f)
                { Fail("damaged gun state is not consumed by shared fire-control authority"); return; }

                string report =
                    "v12.9 component damage + emergency repair smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "matrixCases=" + matrixCases + " severitySum=" + severitySum + "\n" +
                    "thresholds=70/35/12 charges=" + repair.CompletedRepairs + "/" + EmergencyRepairSystem.MaxCharges + " remaining=" + repair.ChargesRemaining + "\n" +
                    "moduleStatus=" + armor.CompactStatus() + "\n" +
                    "mobility=" + armor.MobilityMultiplier.ToString("0.000") + " reload=" + armor.ReloadMultiplier.ToString("0.000") +
                    " weapon=" + armor.WeaponFunctionMultiplier.ToString("0.000") + " playerSpread=" + playerSpread.ToString("0.000") +
                    " enemySpread=" + enemySpread.ToString("0.000") + " hp=" + health.Current + "/" + health.Maximum + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), PassMarker), report);
                Debug.Log("[TankRevival] " + report.Replace("\n", " | "));
                Destroy(fixture);
                fixture = null;
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Fail(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (fixture != null) Destroy(fixture);
            }
        }

        private static void Fail(string reason)
        {
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), FailMarker), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v12.9 component damage/repair smoke FAIL: " + reason);
            Application.Quit(77);
        }
    }
}
