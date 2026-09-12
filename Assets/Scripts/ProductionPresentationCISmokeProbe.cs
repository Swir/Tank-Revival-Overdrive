using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v5.2 standalone smoke probe. Runs only with -production-presentation-smoke.
    /// Verifies authored resources and every EnemyKind signature package inside the real Windows player.
    /// </summary>
    [DefaultExecutionOrder(21000)]
    public sealed class ProductionPresentationCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 12f;
        private float _startedAt;
        private bool _validated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-production-presentation-smoke")) return;
            GameObject go = new GameObject("ProductionPresentationCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<ProductionPresentationCISmokeProbe>();
        }

        private void Awake()
        {
            _startedAt = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (_validated) return;

            ProductionPresentationDirector presentation = ProductionPresentationDirector.Instance;
            ProductionBattleAudioDirector audio = ProductionBattleAudioDirector.Instance;
            if (presentation == null || audio == null)
            {
                if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
                    Finish(false, "presentation/audio director timeout");
                return;
            }

            _validated = true;
            if (audio.LoadedAuthoredClipCount < 2)
            {
                Finish(false, "authored audio missing; loaded=" + audio.LoadedAuthoredClipCount);
                return;
            }

            Array values = Enum.GetValues(typeof(EnemyKind));
            int verifiedKinds = 0;
            for (int i = 0; i < values.Length; i++)
            {
                EnemyKind kind = (EnemyKind)values.GetValue(i);
                GameObject dummy = new GameObject("CI_Signature_" + kind);
                try
                {
                    EnemyClassSignature signature = dummy.AddComponent<EnemyClassSignature>();
                    signature.Configure(kind, WarfarePerformanceGovernor.BudgetTier.Full);
                    if (!signature.IsConfigured || dummy.transform.childCount == 0)
                    {
                        Finish(false, "signature failed for " + kind);
                        Destroy(dummy);
                        return;
                    }

                    Collider[] colliders = dummy.GetComponentsInChildren<Collider>(true);
                    if (colliders != null && colliders.Length > 0)
                    {
                        Finish(false, "presentation collider leaked for " + kind + "; count=" + colliders.Length);
                        Destroy(dummy);
                        return;
                    }

                    signature.ApplyBudget(WarfarePerformanceGovernor.BudgetTier.Survival);
                    verifiedKinds++;
                }
                finally
                {
                    Destroy(dummy);
                }
            }

            Finish(true, "authoredAudio=" + audio.LoadedAuthoredClipCount + " enemyKinds=" + verifiedKinds + " budget=" + WarfarePerformanceGovernor.Tier);
        }

        private static bool HasArgument(string expected)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void Finish(bool pass, string details)
        {
            string file = pass ? "PRODUCTION_PRESENTATION_PASS.txt" : "PRODUCTION_PRESENTATION_FAIL.txt";
            string path = Path.Combine(Directory.GetCurrentDirectory(), file);
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "v5.2 production presentation smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[ProductionPresentationCISmokeProbe] " + text.Replace("\n", " | "));
            Application.Quit(pass ? 0 : 31);
        }
    }
}
