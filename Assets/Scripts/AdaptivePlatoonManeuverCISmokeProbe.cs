using UnityEngine;

namespace TankRevival
{
    public sealed class AdaptivePlatoonManeuverCISmokeProbe : MonoBehaviour
    {
        private float _started;
        private void Awake() { _started = Time.realtimeSinceStartup; DontDestroyOnLoad(gameObject); }
        private void Update()
        {
            if (Time.realtimeSinceStartup - _started < 1.5f) return;
            if (!AdaptivePlatoonManeuverDirector.ConfigurationValid)
                throw new System.InvalidOperationException("v11.6 maneuver configuration is outside bounded safety limits.");
            if (!PlatoonFireMissionCoordinator.ConfigurationValid)
                throw new System.InvalidOperationException("v11.6 requires the qualified platoon fire-mission coordinator.");
            if (AdaptivePlatoonManeuverDirector.MaxTrackedActors != PlatoonFireMissionCoordinator.MaxActors)
                throw new System.InvalidOperationException("v11.6 maneuver/platoon actor caps diverged.");
            Debug.Log("[CI] V11.6_MANEUVER_SMOKE_OK flank=" + AdaptivePlatoonManeuverDirector.HunterFlankBand + " siege=" + AdaptivePlatoonManeuverDirector.SiegeMinStandoff + "-" + AdaptivePlatoonManeuverDirector.SiegeMaxStandoff + " actors=" + AdaptivePlatoonManeuverDirector.MaxTrackedActors);
            enabled = false;
        }
    }
}
