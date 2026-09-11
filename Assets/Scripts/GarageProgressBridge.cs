using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Keeps the v1.1 legend counter compatible with the v1.2 garage economy.
    /// This can be removed after the legacy key is migrated in a later save-format pass.
    /// </summary>
    public sealed class GarageProgressBridge : MonoBehaviour
    {
        private int _lastValue = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<GarageProgressBridge>() != null) return;
            var go = new GameObject("GarageProgressBridge");
            DontDestroyOnLoad(go);
            go.AddComponent<GarageProgressBridge>();
        }

        private void Update()
        {
            int value = PlayerPrefs.GetInt("TankRevival.LegendsDefeated", 0);
            if (value == _lastValue) return;
            _lastValue = value;
            PlayerPrefs.SetInt("TankRevival.BossLegendsDefeated", value);
            PlayerPrefs.Save();
        }
    }
}
