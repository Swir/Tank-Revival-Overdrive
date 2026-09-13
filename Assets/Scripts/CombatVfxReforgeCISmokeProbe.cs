using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class CombatVfxReforgeCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-combat-vfx-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("CombatVfxReforgeCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<CombatVfxReforgeCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                CombatVfxReforgeDirector director = FindAnyObjectByType<CombatVfxReforgeDirector>();
                if (director == null) { Fail("CombatVfxReforgeDirector missing"); return; }
                if (!CombatVfxReforgeDirector.ConfigurationValid) { Fail("Combat VFX configuration invalid"); return; }
                if (!CombatVfxReforgeDirector.UsesProjectileEventAuthority) { Fail("VFX must remain bound to Projectile events"); return; }
                if (CombatVfxReforgeDirector.AmmoSignatureCount != AmmoDatabase.AmmoTypeCount) { Fail("Ammo signature catalog incomplete"); return; }
                if (CombatVfxReforgeDirector.MaxTrackedTrails > 96) { Fail("Trail tracking budget exceeded"); return; }
                if (CombatVfxReforgeDirector.MaxLayerBurstsPerSecond > 32) { Fail("Layer burst budget exceeded"); return; }
                if (CombatVfxReforgeDirector.MinTrailInterval < 0.025f) { Fail("Trail cadence too aggressive"); return; }
                if (CombatVfxReforgeDirector.MaxShockwaveScale > 2f) { Fail("Shockwave presentation bound exceeded"); return; }
                if (FindAnyObjectByType<VehicleMotionWeaponAnimationDirector>() == null) { Fail("v6.7 vehicle motion service missing"); return; }
                if (FindAnyObjectByType<CinematicBattlefieldDirector>() == null) { Fail("v6.6 cinematic battlefield service missing"); return; }

                for (int i = 0; i < AmmoDatabase.AmmoTypeCount; i++)
                {
                    AmmoType ammo = (AmmoType)i;
                    Color c = AmmoDatabase.Color(ammo);
                    if (c.a <= 0f) { Fail("Invalid ammo signature color for " + ammo); return; }
                }

                var probe = new GameObject("COMBAT_VFX_HEALTH_AUTHORITY_PROBE");
                Health health = probe.AddComponent<Health>();
                health.Initialize(Team.Player, 12);
                if (!health.Damage(4, Team.Enemy) || health.Current != 8 || health.Maximum != 12)
                {
                    Destroy(probe);
                    Fail("Authoritative Health path changed");
                    return;
                }
                Destroy(probe);

                string report = "v6.8 combat VFX reforge smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "ammoSignatures=" + CombatVfxReforgeDirector.AmmoSignatureCount +
                    " trailBudget=" + CombatVfxReforgeDirector.MaxTrackedTrails +
                    " burstBudget=" + CombatVfxReforgeDirector.MaxLayerBurstsPerSecond +
                    " minTrail=" + CombatVfxReforgeDirector.MinTrailInterval +
                    " maxShockwave=" + CombatVfxReforgeDirector.MaxShockwaveScale + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "COMBAT_VFX_PASS.txt"), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "COMBAT_VFX_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v6.8 combat VFX smoke FAIL: " + reason);
            Application.Quit(68);
        }
    }
}
