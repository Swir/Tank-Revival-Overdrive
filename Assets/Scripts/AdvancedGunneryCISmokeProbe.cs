using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class AdvancedGunneryCISmokeProbe : MonoBehaviour
    {
        const string PassFile = "ADVANCED_GUNNERY_V113_PASS.txt";
        const string FailFile = "ADVANCED_GUNNERY_V113_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            foreach (var arg in Environment.GetCommandLineArgs())
                if (arg == "-advanced-gunnery-v113-smoke")
                {
                    var go = new GameObject("AdvancedGunneryCISmokeProbe_v11_3");
                    DontDestroyOnLoad(go);
                    go.AddComponent<AdvancedGunneryCISmokeProbe>();
                    return;
                }
        }

        void Start()
        {
            try
            {
                if (!AdvancedGunneryDoctrineDirector.ConfigurationValid) throw new Exception("Doctrine configuration invalid");
                if (!CounterFireThreatMemory.ConfigurationValid) throw new Exception("Counter-fire memory configuration invalid");
                if (AdvancedGunneryDoctrineDirector.CounterFireMemorySeconds > 6f) throw new Exception("Memory budget exceeded");
                if (AdvancedGunneryDoctrineDirector.MaxAimBiasDegrees > 3f) throw new Exception("Aim bias budget exceeded");
                if (CounterFireThreatMemory.MaxTrackedThreats > 12) throw new Exception("Threat budget exceeded");
                if (CounterFireThreatMemory.RetaliationAccuracyBoost > .15f) throw new Exception("Accuracy budget exceeded");
                if (AdvancedGunneryDoctrineDirector.PreferPlayer(EnemyKind.Basic, 100)) throw new Exception("Basic doctrine eligibility invalid");
                if (!AdvancedGunneryDoctrineDirector.PreferPlayer(EnemyKind.Sniper, 35)) throw new Exception("Sniper doctrine identity missing");
                if (FireControlVolleyCoordinator.MaxHoldSeconds > FireControlBallisticsDirector.CoordinatedVolleyWindow + .001f) throw new Exception("Volley cap exceeded");
                File.WriteAllText(PassFile, "v11.3 Advanced Gunnery packaged smoke PASS");
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
