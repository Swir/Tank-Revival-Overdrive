using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class PlatoonFireMissionCISmokeProbe : MonoBehaviour
    {
        const string PassFile = "PLATOON_FIRE_MISSION_V114_PASS.txt";
        const string FailFile = "PLATOON_FIRE_MISSION_V114_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            foreach (var arg in Environment.GetCommandLineArgs())
                if (arg == "-platoon-fire-mission-v114-smoke")
                {
                    var go = new GameObject("PlatoonFireMissionCISmokeProbe_v11_4");
                    DontDestroyOnLoad(go);
                    go.AddComponent<PlatoonFireMissionCISmokeProbe>();
                    return;
                }
        }

        void Start()
        {
            try
            {
                if (!PlatoonFireMissionCoordinator.ConfigurationValid) throw new Exception("Coordinator configuration invalid");
                if (PlatoonFireMissionCoordinator.MaxActors > 24) throw new Exception("Actor reservation budget exceeded");
                if (PlatoonFireMissionCoordinator.MaxPrecisionCommitmentsPerTarget > 2) throw new Exception("Overkill cap exceeded");
                if (PlatoonFireMissionCoordinator.ReservationSeconds > 1.6f) throw new Exception("Reservation lifetime exceeded");
                if (!FireMissionNetworkDirector.IsFireMissionRound(54)) throw new Exception("Fire mission integration schedule missing");
                if (!AdvancedGunneryDoctrineDirector.ConfigurationValid) throw new Exception("v11.3 doctrine dependency invalid");
                if (!CounterFireThreatMemory.ConfigurationValid) throw new Exception("v11.3 counter-fire dependency invalid");
                PlatoonFireMissionCoordinator.Reset();
                if (PlatoonFireMissionCoordinator.ActiveReservations != 0) throw new Exception("Reservation reset failed");
                File.WriteAllText(PassFile, "v11.4 Platoon Fire Mission packaged smoke PASS");
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                File.WriteAllText(FailFile, ex.ToString());
                Application.Quit(2);
            }
        }
    }
}
