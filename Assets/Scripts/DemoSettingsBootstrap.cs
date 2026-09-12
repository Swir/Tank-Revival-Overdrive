using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// TankGame still owns legacy boot defaults. Re-apply the persisted player display policy in Start,
    /// after every Awake has run, so saved demo settings win deterministically on Windows launch.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class DemoSettingsBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<DemoSettingsBootstrap>() != null) return;
            var go = new GameObject("DemoSettingsBootstrap_v4.9");
            DontDestroyOnLoad(go);
            go.AddComponent<DemoSettingsBootstrap>();
        }

        private void Start()
        {
            DemoPlayerSettings.LoadAndApply();
        }
    }
}
