using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public sealed class FireControlBallisticsCISmokeProbe : MonoBehaviour
    {
        private const string PassFile = "FIRE_CONTROL_BALLISTICS_PASS.txt";
        private const string FailFile = "FIRE_CONTROL_BALLISTICS_FAIL.txt";
        private float _deadline;
        private bool _campaignStartRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArg("-fire-control-ballistics-smoke")) return;
            GameObject go = new GameObject("FireControlBallisticsCISmokeProbe_v11_2");
            DontDestroyOnLoad(go);
            go.AddComponent<FireControlBallisticsCISmokeProbe>();
        }

        private void Awake()
        {
            SafeDelete(PassFile);
            SafeDelete(FailFile);
            _deadline = Time.realtimeSinceStartup + 24f;
        }

        private void Update()
        {
            try
            {
                TankGame game = FindAnyObjectByType<TankGame>();
                if (game == null)
                {
                    if (Time.realtimeSinceStartup < _deadline) return;
                    Fail("TankGame not available");
                    return;
                }

                PlayerTank player = FindAnyObjectByType<PlayerTank>();
                if (player == null && !_campaignStartRequested)
                {
                    _campaignStartRequested = true;
                    MethodInfo startCampaign = typeof(TankGame).GetMethod("StartCampaign", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (startCampaign == null)
                        throw new MissingMethodException("TankGame.StartCampaign smoke bootstrap unavailable");
                    startCampaign.Invoke(game, null);
                    return;
                }

                if (player == null)
                {
                    if (Time.realtimeSinceStartup < _deadline) return;
                    Fail("PlayerTank not available after campaign bootstrap");
                    return;
                }

                ValidateConfiguration();
                ValidateBallistics();
                ValidatePlayerIntegration(player);

                File.WriteAllText(PassFile,
                    "v11.2 Fire Control / Stabilization / Ballistics packaged smoke PASS\n" +
                    "version=" + Application.version + "\n" +
                    "bounds=minAccuracy:" + FireControlBallisticsDirector.MinAccuracy +
                    ",maxSpread:" + FireControlBallisticsDirector.MaxSpreadDegrees +
                    ",maxLead:" + FireControlBallisticsDirector.MaxLeadSeconds +
                    ",volleyWindow:" + FireControlBallisticsDirector.CoordinatedVolleyWindow + "\n" +
                    "integration=PlayerTank + ArmorSystem + TankGame + Projectile + Health\n" +
                    "bootstrap=packaged smoke starts a real campaign when launched from menu\n");
                Debug.Log("[CI] v11.2 Fire Control Ballistics smoke PASS");
                Application.Quit(0);
                enabled = false;
            }
            catch (Exception ex)
            {
                Fail(ex.ToString());
            }
        }

        private static void ValidateConfiguration()
        {
            if (Application.version != "11.2.0-dev")
                throw new InvalidOperationException("unexpected Application.version: " + Application.version);
            if (!FireControlBallisticsDirector.ConfigurationValid)
                throw new InvalidOperationException("FireControlBallisticsDirector.ConfigurationValid=false");
        }

        private static void ValidateBallistics()
        {
            float stationaryAp = FireControlBallisticsDirector.SpreadDegrees(AmmoType.ArmorPiercing, 0f, null);
            float movingAp = FireControlBallisticsDirector.SpreadDegrees(AmmoType.ArmorPiercing, 1f, null);
            float stationaryHe = FireControlBallisticsDirector.SpreadDegrees(AmmoType.Explosive, 0f, null);
            if (!(movingAp > stationaryAp))
                throw new InvalidOperationException("movement must increase AP dispersion");
            if (!(stationaryHe > stationaryAp))
                throw new InvalidOperationException("HE must be less precise than AP");

            Vector2 lead = FireControlBallisticsDirector.Lead(Vector2.zero, new Vector2(10f, 0f), Vector2.up * 3f, 10f);
            if (!(lead.y > 0f && lead.y <= 3f * FireControlBallisticsDirector.MaxLeadSeconds + 0.01f))
                throw new InvalidOperationException("predictive lead bounds invalid");

            float sniper = FireControlBallisticsDirector.EnemySpreadDegrees(EnemyKind.Sniper, 0f, null, false);
            float basic = FireControlBallisticsDirector.EnemySpreadDegrees(EnemyKind.Basic, 0f, null, false);
            if (!(sniper < basic))
                throw new InvalidOperationException("Sniper precision identity invalid");
        }

        private static void ValidatePlayerIntegration(PlayerTank player)
        {
            if (player.Health == null)
                throw new InvalidOperationException("PlayerTank Health authority unavailable");
            if (player.GetComponent<ArmorSystem>() == null)
                throw new InvalidOperationException("PlayerTank ArmorSystem integration unavailable");

            float stabilization = player.FireControlStabilization;
            if (stabilization < FireControlBallisticsDirector.MinAccuracy || stabilization > 1.001f)
                throw new InvalidOperationException("player stabilization outside safety bounds: " + stabilization);
        }

        private static bool HasArg(string arg)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], arg, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void Fail(string message)
        {
            try { File.WriteAllText(FailFile, message); } catch { }
            Debug.LogError("[CI] v11.2 Fire Control Ballistics smoke FAIL: " + message);
            Application.Quit(2);
            enabled = false;
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
