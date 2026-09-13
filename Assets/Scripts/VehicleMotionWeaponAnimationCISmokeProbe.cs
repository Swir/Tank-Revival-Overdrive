using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class VehicleMotionWeaponAnimationCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-vehicle-motion-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("VehicleMotionWeaponAnimationCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<VehicleMotionWeaponAnimationCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                VehicleMotionWeaponAnimationDirector director = FindAnyObjectByType<VehicleMotionWeaponAnimationDirector>();
                if (director == null) { Fail("VehicleMotionWeaponAnimationDirector missing"); return; }
                if (!VehicleMotionWeaponAnimationDirector.ConfigurationValid) { Fail("Vehicle motion configuration invalid"); return; }
                if (!VehicleMotionWeaponAnimationDirector.ShotObservationUsesProjectileAuthority) { Fail("Shot observation must remain tied to Projectile authority"); return; }
                if (VehicleMotionWeaponAnimationDirector.MaxHullLeanDegrees > 5f) { Fail("Hull lean comfort bound exceeded"); return; }
                if (VehicleMotionWeaponAnimationDirector.MaxSuspensionTravel > 0.06f) { Fail("Suspension presentation bound exceeded"); return; }
                if (VehicleMotionWeaponAnimationDirector.MaxBarrelRecoil > 0.24f) { Fail("Weapon recoil presentation bound exceeded"); return; }
                if (VehicleMotionWeaponAnimationDirector.ProjectileObservationInterval < 0.03f) { Fail("Projectile observation cadence is too aggressive"); return; }
                if (VehicleMotionWeaponAnimationDirector.MaxTrackedProjectileIds > 256) { Fail("Projectile presentation tracking budget too high"); return; }
                if (FindAnyObjectByType<CinematicBattlefieldDirector>() == null) { Fail("v6.6 cinematic battlefield service missing"); return; }
                if (FindAnyObjectByType<BattlefieldPresentationOverdriveDirector>() == null) { Fail("v6.5 presentation service missing"); return; }

                var probe = new GameObject("VEHICLE_MOTION_HEALTH_AUTHORITY_PROBE");
                Health health = probe.AddComponent<Health>();
                health.Initialize(Team.Player, 10);
                if (!health.Damage(3, Team.Enemy) || health.Current != 7 || health.Maximum != 10) { Destroy(probe); Fail("Authoritative Health path changed"); return; }
                Destroy(probe);

                string report = "v6.7 vehicle motion / weapon animation smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "lean=" + VehicleMotionWeaponAnimationDirector.MaxHullLeanDegrees +
                    " suspension=" + VehicleMotionWeaponAnimationDirector.MaxSuspensionTravel +
                    " recoil=" + VehicleMotionWeaponAnimationDirector.MaxBarrelRecoil +
                    " projectileScan=" + VehicleMotionWeaponAnimationDirector.ProjectileObservationInterval +
                    " projectileBudget=" + VehicleMotionWeaponAnimationDirector.MaxTrackedProjectileIds + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "VEHICLE_MOTION_PASS.txt"), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "VEHICLE_MOTION_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v6.7 vehicle motion smoke FAIL: " + reason);
            Application.Quit(67);
        }
    }
}
