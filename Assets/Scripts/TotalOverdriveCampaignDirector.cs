using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.0 TOTAL OVERDRIVE campaign spine.
    /// Groups 100 rounds into five acts and turns sustained combat performance into a playable surge system.
    /// </summary>
    public sealed class TotalOverdriveCampaignDirector : MonoBehaviour
    {
        private static readonly string[] ActNames =
        {
            "ACT I // BORDER FIRE",
            "ACT II // IRON STORM",
            "ACT III // FROZEN BREAKTHROUGH",
            "ACT IV // FORTRESS COLLAPSE",
            "ACT V // TOTAL OVERDRIVE"
        };

        private TankGame _game;
        private int _round;
        private int _act = -1;
        private int _charge;
        private int _surges;
        private float _scanAt;
        private float _bannerUntil;
        private string _banner = string.Empty;
        private readonly HashSet<int> _hooked = new HashSet<int>();

        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _bannerStyle;

        public int Charge => _charge;
        public int ActIndex => Mathf.Clamp(_act, 0, 4);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TotalOverdriveCampaignDirector>() != null) return;
            var go = new GameObject("TotalOverdriveCampaignDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<TotalOverdriveCampaignDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying) return;

            int round = _game.CurrentRound;
            if (round != _round)
            {
                _round = round;
                _hooked.Clear();
                EnterActIfNeeded(round);
            }

            if (Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.28f;
                HookEnemies();
            }
        }

        private void EnterActIfNeeded(int round)
        {
            int nextAct = Mathf.Clamp((round - 1) / 20, 0, 4);
            if (nextAct == _act) return;
            _act = nextAct;
            _banner = ActNames[_act];
            _bannerUntil = Time.unscaledTime + 4.5f;

            int bestAct = PlayerPrefs.GetInt("TankRevival.BestAct", 0);
            if (_act + 1 > bestAct)
            {
                PlayerPrefs.SetInt("TankRevival.BestAct", _act + 1);
                PlayerPrefs.Save();
            }

            if (round > 1)
                GrantActResupply();
        }

        private void GrantActResupply()
        {
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player == null) return;

            player.ApplyPowerUp(PowerUpKind.Repair);
            player.AddAmmo(AmmoType.ArmorPiercing, 5 + _act * 2);
            if (_act >= 2) player.AddAmmo(AmmoType.EMP, 2 + _act);
            if (_act >= 4) player.AddAmmo(AmmoType.Plasma, 3);
            _game.RepairEagle(1);
            Announce("ACT RESUPPLY // ORZELEK + ARMOR + SPECIAL AMMO");
        }

        private void HookEnemies()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            foreach (EnemyTank enemy in enemies)
            {
                if (enemy == null || enemy.Health == null) continue;
                int id = enemy.GetInstanceID();
                if (!_hooked.Add(id)) continue;
                EnemyTank captured = enemy;
                enemy.Health.Died += _ => OnEnemyDestroyed(captured);
            }
        }

        private void OnEnemyDestroyed(EnemyTank enemy)
        {
            if (enemy == null || _game == null || !_game.IsPlaying) return;
            int gain = enemy.Kind switch
            {
                EnemyKind.Boss => 34,
                EnemyKind.Elite => 15,
                EnemyKind.Siege => 13,
                EnemyKind.Heavy => 10,
                EnemyKind.Sniper => 9,
                EnemyKind.Supply => 8,
                _ => 5
            };

            _charge = Mathf.Min(100, _charge + gain);
            if (_charge >= 100)
                TriggerCombatSurge();
        }

        private void TriggerCombatSurge()
        {
            _charge = 0;
            _surges++;
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player == null || player.Health == null) return;

            player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 3.5f);
            player.AddAmmo(_round >= 70 ? AmmoType.Plasma : _round >= 35 ? AmmoType.EMP : AmmoType.ArmorPiercing, 3 + _act);
            if (_surges % 2 == 0) _game.RepairEagle(1);
            VisualFactory.RingPulse(player.transform.position, new Color(0.18f, 0.92f, 1f), 2.1f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.78f, 0f);
            Announce(_surges % 2 == 0 ? "OVERDRIVE SURGE // SHIELD + AMMO + ORZELEK REPAIR" : "OVERDRIVE SURGE // SHIELD + SPECIAL AMMO");
        }

        private void Announce(string text)
        {
            _banner = text;
            _bannerUntil = Time.unscaledTime + 2.8f;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.34f, 0.94f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.63f, 0.74f, 0.82f) } };
            _bannerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 25, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            float x = Mathf.Max(14f, Screen.width - 368f);
            GUI.color = new Color(0.018f, 0.032f, 0.052f, 0.93f);
            GUI.Box(new Rect(x, 14f, 354f, 82f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 13f, 20f, 330f, 20f), ActNames[ActIndex], _header);
            GUI.Label(new Rect(x + 13f, 42f, 330f, 18f), $"OVERDRIVE CHARGE {_charge}%   //   SURGES {_surges}", _body);
            GUI.Label(new Rect(x + 13f, 61f, 330f, 17f), "Combat kills fill charge. 100% triggers a combat surge.", _small);
            GUI.color = new Color(0.08f, 0.13f, 0.18f, 0.95f);
            GUI.Box(new Rect(x + 13f, 80f, 328f, 8f), string.Empty);
            GUI.color = new Color(0.16f, 0.88f, 1f, 0.98f);
            GUI.Box(new Rect(x + 13f, 80f, 3.28f * _charge, 8f), string.Empty);
            GUI.color = Color.white;

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.012f, 0.025f, 0.045f, 0.94f);
                GUI.Box(new Rect(Screen.width * 0.5f - 390f, Screen.height * 0.29f - 34f, 780f, 68f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 380f, Screen.height * 0.29f - 22f, 760f, 44f), _banner, _bannerStyle);
            }
        }
    }
}
