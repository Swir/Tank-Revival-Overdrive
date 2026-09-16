using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public static class PlatoonAssaultCISmokeProbe
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            if (!HasArg("-platoon-assault-v115-smoke")) return;
            try
            {
                if (!PlatoonFireMissionCoordinator.ConfigurationValid) throw new InvalidOperationException("invalid coordinator bounds");
                if (PlatoonFireMissionCoordinator.MaxActors != 24) throw new InvalidOperationException("actor cap drift");
                if (PlatoonFireMissionCoordinator.MaxPrecisionCommitmentsPerTarget != 2) throw new InvalidOperationException("overkill cap drift");
                if (PlatoonFireMissionCoordinator.ReservationSeconds > 1.6f) throw new InvalidOperationException("handoff reservation too long");
                if (PlatoonFireMissionCoordinator.AssaultPhaseSeconds < 6f) throw new InvalidOperationException("assault phase unstable");
                if (Enum.GetValues(typeof(PlatoonFireMissionCoordinator.PlatoonRole)).Length != 4) throw new InvalidOperationException("role contract drift");
                File.WriteAllText("PLATOON_ASSAULT_V115_PASS.txt", "roles+handoffs+assaults=ok");
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                File.WriteAllText("PLATOON_ASSAULT_V115_FAIL.txt", ex.ToString());
                Application.Quit(17);
            }
        }

        private static bool HasArg(string value)
        {
            foreach (string arg in Environment.GetCommandLineArgs()) if (string.Equals(arg, value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
