using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE qualification for v12.7 combat feedback, damage language and adaptive FX budgets.</summary>
    public sealed class CinematicCombatFeedbackCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V12_7_CINEMATIC_COMBAT_SMOKE_OK.txt";
        public const string FailMarker = "V12_7_CINEMATIC_COMBAT_SMOKE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v127-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<CinematicCombatFeedbackCISmokeProbe>() != null) return;
                GameObject go = new GameObject("CinematicCombatFeedbackCISmokeProbe_v12_7");
                DontDestroyOnLoad(go);
                go.AddComponent<CinematicCombatFeedbackCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!CinematicCombatFeedbackDirector.ConfigurationValid) { Fail("v12.7 combat feedback configuration invalid"); return; }
                CinematicCombatFeedbackDirector director = CinematicCombatFeedbackDirector.Instance;
                if (director == null) { Fail("v12.7 runtime combat feedback director not installed"); return; }

                CombatFeedbackBudget light = CinematicCombatFeedbackDirector.ComputeBudget(20, 0.18f, 2);
                CombatFeedbackBudget medium = CinematicCombatFeedbackDirector.ComputeBudget(65, 0.54f, 6);
                CombatFeedbackBudget dense = CinematicCombatFeedbackDirector.ComputeBudget(99, 0.91f, 11);
                if (!(light.MaxActiveCues > medium.MaxActiveCues && medium.MaxActiveCues > dense.MaxActiveCues) ||
                    !(light.MaxParticlesPerCue > medium.MaxParticlesPerCue && medium.MaxParticlesPerCue > dense.MaxParticlesPerCue) ||
                    !(light.RingSegments > medium.RingSegments && medium.RingSegments > dense.RingSegments) ||
                    !(light.MinImpactInterval < medium.MinImpactInterval && medium.MinImpactInterval < dense.MinImpactInterval) ||
                    !(light.AudioCooldown < medium.AudioCooldown && medium.AudioCooldown < dense.AudioCooldown))
                { Fail("Adaptive combat-FX budget is not monotonically cheaper under late-wave pressure"); return; }
                if (light.MaxActiveCues > CinematicCombatFeedbackDirector.PoolCapacity ||
                    light.MaxParticlesPerCue > CinematicCombatFeedbackDirector.MaxParticlesPerCue ||
                    dense.MaxActiveCues < 6 || dense.RingSegments < CinematicCombatFeedbackDirector.MinRingSegments)
                { Fail("Combat-FX budget violates pool cap or readability floor"); return; }

                if (CinematicCombatFeedbackDirector.ResolveDamageState(1.00f) != DamageVisualState.Healthy ||
                    CinematicCombatFeedbackDirector.ResolveDamageState(0.66f) != DamageVisualState.Damaged ||
                    CinematicCombatFeedbackDirector.ResolveDamageState(0.40f) != DamageVisualState.Critical ||
                    CinematicCombatFeedbackDirector.ResolveDamageState(0.20f) != DamageVisualState.Burning ||
                    CinematicCombatFeedbackDirector.ResolveDamageState(0.05f) != DamageVisualState.Burning)
                { Fail("Damage-state language thresholds invalid"); return; }

                AmmoType[] ammo = {
                    AmmoType.Basic, AmmoType.Twin, AmmoType.ArmorPiercing, AmmoType.Explosive,
                    AmmoType.Plasma, AmmoType.EMP, AmmoType.Incendiary
                };
                ImpactMaterialKind[] materials = {
                    ImpactMaterialKind.Organic, ImpactMaterialKind.Brick, ImpactMaterialKind.Steel,
                    ImpactMaterialKind.Terrain, ImpactMaterialKind.Unknown
                };
                int styleCases = 0;
                int prioritySum = 0;
                for (int a = 0; a < ammo.Length; a++)
                {
                    for (int m = 0; m < materials.Length; m++)
                    {
                        CombatImpactStyle style = CinematicCombatFeedbackDirector.ResolveImpactStyle(ammo[a], materials[m]);
                        if (style.Radius <= 0f || style.Lifetime <= 0f || style.Particles <= 0 ||
                            style.Particles > CinematicCombatFeedbackDirector.MaxParticlesPerCue || style.Priority < 1 || style.Priority > 7)
                        { Fail("Impact style matrix produced invalid bounded style"); return; }
                        styleCases++;
                        prioritySum += style.Priority;
                    }
                }
                if (styleCases != 35) { Fail("Impact style matrix coverage mismatch"); return; }

                CombatImpactStyle basicOrganic = CinematicCombatFeedbackDirector.ResolveImpactStyle(AmmoType.Basic, ImpactMaterialKind.Organic);
                CombatImpactStyle apSteel = CinematicCombatFeedbackDirector.ResolveImpactStyle(AmmoType.ArmorPiercing, ImpactMaterialKind.Steel);
                CombatImpactStyle heBrick = CinematicCombatFeedbackDirector.ResolveImpactStyle(AmmoType.Explosive, ImpactMaterialKind.Brick, true, false);
                CombatImpactStyle plasmaSteel = CinematicCombatFeedbackDirector.ResolveImpactStyle(AmmoType.Plasma, ImpactMaterialKind.Steel);
                CombatImpactStyle empTerrain = CinematicCombatFeedbackDirector.ResolveImpactStyle(AmmoType.EMP, ImpactMaterialKind.Terrain);
                CombatImpactStyle ricochetSteel = CinematicCombatFeedbackDirector.ResolveImpactStyle(AmmoType.Basic, ImpactMaterialKind.Steel, false, true);
                if (!(apSteel.Priority > basicOrganic.Priority && heBrick.Radius > apSteel.Radius &&
                    plasmaSteel.Particles > basicOrganic.Particles && empTerrain.Radius > basicOrganic.Radius &&
                    ricochetSteel.AudioCue == SoundCue.Ricochet && heBrick.AudioCue == SoundCue.ExplosionSmall))
                { Fail("Ammo/material hierarchy failed representative ordering"); return; }

                float damagedCadence = CinematicCombatFeedbackDirector.DamagePulseCadence(0.55f, 0f);
                float criticalCadence = CinematicCombatFeedbackDirector.DamagePulseCadence(0.30f, 0f);
                float burningCadence = CinematicCombatFeedbackDirector.DamagePulseCadence(0.10f, 0f);
                float denseBurningCadence = CinematicCombatFeedbackDirector.DamagePulseCadence(0.10f, 1f);
                if (!(burningCadence < criticalCadence && criticalCadence < damagedCadence && denseBurningCadence > burningCadence))
                { Fail("Persistent damage cadence is not severity/pressure aware"); return; }

                bool pulseAccepted = CinematicCombatFeedbackDirector.RequestDamagePulse(Vector3.zero, 0.15f);
                if (!pulseAccepted || director.ActiveCueCount < 1 || director.ActiveCueCount > CinematicCombatFeedbackDirector.PoolCapacity)
                { Fail("Fixed combat cue pool did not accept bounded burning-state pulse"); return; }

                string report =
                    "v12.7 cinematic combat feedback smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "styleCases=" + styleCases + " prioritySum=" + prioritySum +
                    " pool=" + director.ActiveCueCount + "/" + CinematicCombatFeedbackDirector.PoolCapacity + "\n" +
                    "budgetLight=" + light.MaxActiveCues + "/" + light.MaxParticlesPerCue + "/" + light.RingSegments +
                    " budgetMedium=" + medium.MaxActiveCues + "/" + medium.MaxParticlesPerCue + "/" + medium.RingSegments +
                    " budgetDense=" + dense.MaxActiveCues + "/" + dense.MaxParticlesPerCue + "/" + dense.RingSegments + "\n" +
                    "damageCadence=" + burningCadence.ToString("0.000") + "<" + criticalCadence.ToString("0.000") + "<" + damagedCadence.ToString("0.000") +
                    " denseBurning=" + denseBurningCadence.ToString("0.000") + "\n" +
                    "representativePriority basicOrganic=" + basicOrganic.Priority + " apSteel=" + apSteel.Priority +
                    " heBrick=" + heBrick.Priority + " plasmaSteel=" + plasmaSteel.Priority + " empTerrain=" + empTerrain.Priority + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), PassMarker), report);
                Debug.Log("[TankRevival] " + report.Replace("\n", " | "));
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Fail(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Fail(string reason)
        {
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), FailMarker), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v12.7 cinematic combat feedback smoke FAIL: " + reason);
            Application.Quit(77);
        }
    }
}
