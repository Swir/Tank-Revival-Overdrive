using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE qualification for v12.1 operational sustainment contracts.</summary>
    public sealed class OperationalSustainmentCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V12_1_SUSTAINMENT_SMOKE_OK.txt";
        public const string FailMarker = "V12_1_SUSTAINMENT_SMOKE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v121-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<OperationalSustainmentCISmokeProbe>() != null) return;
                GameObject go = new GameObject("OperationalSustainmentCISmokeProbe_v12_1");
                DontDestroyOnLoad(go);
                go.AddComponent<OperationalSustainmentCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!OperationalSustainmentDirector.ConfigurationValid) { Fail("Operational sustainment configuration invalid"); return; }
                if (!OperationalSustainmentDirector.BridgeAvailable) { Fail("Existing reserve/siege/engineering bridge unavailable"); return; }
                if (!CombinedArmsMobileFrontDirector.ConfigurationValid) { Fail("v12.0 mobile-front dependency invalid"); return; }
                if (!LogisticsNetworkDirector.ConfigurationValid) { Fail("Existing logistics-network dependency invalid"); return; }
                if (!SupplyRouteWarfareDirector.ConfigurationValid) { Fail("Existing supply-route dependency invalid"); return; }
                if (OperationalSustainmentDirector.Instance == null) { Fail("Operational sustainment runtime director not installed"); return; }
                if (CombinedArmsMobileFrontDirector.Instance == null) { Fail("Mobile-front runtime director not installed"); return; }
                if (StrategicReserveAttritionDirector.Instance == null) { Fail("Strategic reserve authority not installed"); return; }
                if (OperationalSustainmentDirector.MaxColumns != 1 || OperationalSustainmentDirector.MaxEscorts > 4)
                { Fail("Column/escort cap exceeds bounded contract"); return; }

                int eligible = OperationalSustainmentDirector.EligibleColumnCount();
                if (eligible < 3 || eligible > CombinedArmsMobileFrontDirector.EligibleOperationCount())
                { Fail("Operational sustainment schedule density invalid: " + eligible); return; }

                for (int round = 1; round <= 100; round++)
                {
                    if (!OperationalSustainmentDirector.CanLaunchForRound(round)) continue;
                    if (!CombinedArmsMobileFrontDirector.IsCandidateRound(round))
                    { Fail("Sustainment escaped mobile-front schedule at round " + round); return; }
                    if (ConvoyWarfareDirector.HasMissionForRound(round) || LogisticsNetworkDirector.HasLogisticsForRound(round))
                    { Fail("Sustainment overlaps existing convoy/logistics authority at round " + round); return; }
                }

                int fuel60 = OperationalSustainmentDirector.FuelForRound(60);
                int fuel99 = OperationalSustainmentDirector.FuelForRound(99);
                int ammo99 = OperationalSustainmentDirector.AmmoForRound(99);
                int repair99 = OperationalSustainmentDirector.RepairForRound(99);
                int hp99 = OperationalSustainmentDirector.HealthForRound(99);
                if (fuel60 < OperationalSustainmentDirector.FuelMin || fuel99 > OperationalSustainmentDirector.FuelMax || fuel99 < fuel60)
                { Fail("Finite fuel manifest scaling invalid"); return; }
                if (ammo99 > OperationalSustainmentDirector.AmmoMax || repair99 > OperationalSustainmentDirector.RepairMax || hp99 > OperationalSustainmentDirector.ColumnHealthMax)
                { Fail("Finite ammo/repair/health bounds invalid"); return; }

                Vector2 friendlyEarly = OperationalSustainmentDirector.SupportAnchor(1, 0.20f, false);
                Vector2 friendlyLate = OperationalSustainmentDirector.SupportAnchor(1, 0.80f, false);
                Vector2 enemyEarly = OperationalSustainmentDirector.SupportAnchor(1, 0.20f, true);
                Vector2 enemyLate = OperationalSustainmentDirector.SupportAnchor(1, 0.80f, true);
                if (friendlyLate.y <= friendlyEarly.y || enemyLate.y >= enemyEarly.y)
                { Fail("Mobile-front sustainment routing does not follow front direction"); return; }
                if (Mathf.Abs(friendlyLate.x) > CombinedArmsMobileFrontDirector.ArenaXLimit || Mathf.Abs(enemyLate.x) > CombinedArmsMobileFrontDirector.ArenaXLimit)
                { Fail("Sustainment routing escaped arena bounds"); return; }

                Vector2 friendlyStart = OperationalSustainmentDirector.ColumnStart(0, false);
                Vector2 enemyStart = OperationalSustainmentDirector.ColumnStart(2, true);
                if (friendlyStart.y >= -4f || enemyStart.y <= 4f)
                { Fail("Operational columns do not start behind their fronts"); return; }

                float stretched = OperationalSustainmentDirector.StretchedDelay(5f, OperationalSustainmentDirector.EnemyDeficitMultiplier);
                if (stretched <= 5f || stretched > 5f + OperationalSustainmentDirector.MaxTimerStretchSeconds + 0.01f)
                { Fail("Bounded sustainment cadence degradation invalid"); return; }
                if (Mathf.Abs(OperationalSustainmentDirector.StretchedDelay(5f, 1f) - 5f) > 0.01f)
                { Fail("Healthy sustainment unexpectedly changes cadence"); return; }

                if (!OperationalSustainmentDirector.IsEscortKind(EnemyKind.Heavy) ||
                    !OperationalSustainmentDirector.IsEscortKind(EnemyKind.Elite) ||
                    !OperationalSustainmentDirector.IsEscortKind(EnemyKind.Fast) ||
                    OperationalSustainmentDirector.IsEscortKind(EnemyKind.Sniper) ||
                    OperationalSustainmentDirector.IsEscortKind(EnemyKind.Boss))
                { Fail("Operational escort class filter invalid"); return; }

                // Physical column contract uses canonical Health and kinematic Rigidbody2D rather than a second damage authority.
                GameObject authority = new GameObject("V121_SUSTAINMENT_AUTHORITY_PROBE");
                authority.AddComponent<BoxCollider2D>();
                Rigidbody2D body = authority.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                Health health = authority.AddComponent<Health>();
                int hp = OperationalSustainmentDirector.HealthForRound(88);
                health.Initialize(Team.Enemy, hp);
                if (!health.Damage(2, Team.Player) || health.Current != hp - 2 || body.bodyType != RigidbodyType2D.Kinematic)
                { Destroy(authority); Fail("Physical logistics Health/collision authority invalid"); return; }
                Destroy(authority);

                string report =
                    "v12.1 operational sustainment smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "eligibleColumns=" + eligible + " columnCap=" + OperationalSustainmentDirector.MaxColumns + " escortCap=" + OperationalSustainmentDirector.MaxEscorts + "\n" +
                    "fuel60=" + fuel60 + " fuel99=" + fuel99 + " ammo99=" + ammo99 + " repair99=" + repair99 + " hp99=" + hp99 + "\n" +
                    "deficitMultiplier=" + OperationalSustainmentDirector.EnemyDeficitMultiplier.ToString("0.00") + " stretched5s=" + stretched.ToString("0.00") + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), PassMarker), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), FailMarker), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v12.1 operational sustainment smoke FAIL: " + reason);
            Application.Quit(71);
        }
    }
}
