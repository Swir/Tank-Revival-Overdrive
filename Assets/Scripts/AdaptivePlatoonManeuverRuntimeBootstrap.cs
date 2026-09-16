using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Keeps the v11.6 packaged qualification probe attached to the real runtime
    /// without coupling qualification plumbing to TankGame's public combat API.
    /// This deliberately avoids touching projectile, pickup, HUD or tank contracts.
    /// </summary>
    internal static class AdaptivePlatoonManeuverRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachProbe()
        {
            TankGame game = Object.FindAnyObjectByType<TankGame>();
            if (game == null)
                return;

            if (game.GetComponent<AdaptivePlatoonManeuverCISmokeProbe>() == null)
                game.gameObject.AddComponent<AdaptivePlatoonManeuverCISmokeProbe>();
        }
    }
}
