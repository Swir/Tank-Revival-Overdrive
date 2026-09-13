using UnityEngine;

namespace TankRevival
{
    public sealed class DestructionAuthorityMigration : MonoBehaviour
    {
        public static bool LegacyAuthoritySuppressed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<DestructionAuthorityMigration>() != null) return;
            var go = new GameObject("DestructionAuthorityMigration");
            DontDestroyOnLoad(go);
            go.AddComponent<DestructionAuthorityMigration>();
        }

        private void Update()
        {
            if (LegacyAuthoritySuppressed)
            {
                enabled = false;
                return;
            }

            WarzoneDestructionDirector legacy = FindAnyObjectByType<WarzoneDestructionDirector>();
            DestructionReforgeDirector current = FindAnyObjectByType<DestructionReforgeDirector>();
            if (legacy == null || current == null) return;

            legacy.enabled = false;
            DestructionWitness[] witnesses = FindObjectsByType<DestructionWitness>(FindObjectsSortMode.None);
            for (int i = 0; i < witnesses.Length; i++)
            {
                if (witnesses[i] != null) Destroy(witnesses[i]);
            }

            LegacyAuthoritySuppressed = true;
            Debug.Log("[TankRevival] v6.9 destruction authority migrated: legacy death-wreck observer disabled.");
            enabled = false;
        }
    }
}
