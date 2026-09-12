using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v4.0 endgame operation for rounds 91-100. It does not replace boss/encounter logic;
    /// instead it layers escalating operational states, kill-driven support, Eagle emergency
    /// servicing and completion rewards on top of the existing authoritative combat systems.
    /// </summary>
    public sealed class ZeroFrontFinaleDirector : MonoBehaviour
    {
        private TankGame _game;
        private int _round;
        private int _phase;
        private int _kills;
        private int _eliteKills;
        private int _supportCharges;
        private bool _finalRewardGranted;
        private float _scanAt;
        private float _bannerUntil;
        private string _banner = string.Empty;
        private readonly HashSet<int> _hooked = new HashSet<int>();

        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _bannerStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ZeroFrontFinaleDirector>() != null) return;
            var go = new GameObject("ZeroFrontFinaleDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<ZeroFrontFinaleDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying)
            {
                _round = 0;
                _phase = 0;
                _hooked.Clear();
                return;
            }

            int round = _game.CurrentRound;
            if (round < 91) return;

            if (round != _round)
            {
                _round = round;
                _hooked.Clear();
                OnFinaleRound(round);
            }

            if (Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.25f;
                HookEnemies();
                CheckEmergencySupport();
            }

            if (round >= 100)
                TryGrantFinalReward();
        }

        private void OnFinaleRound(int round)
        {
            int nextPhase = round <= 93 ? 1 : round <= 96 ? 2 : round <= 99 ? 3 : 4;
            if (nextPhase != _phase)
            {
                _phase = nextPhase;
                switch (_phase)
                {
                    case 1:
                        Announce("ZERO FRONT // PHASE I — BREACH THE OUTER RING", 4.2f);
                        GrantPhaseResupply(AmmoType.ArmorPiercing, 5, 2);
                        break;
                    case 2:
                        Announce("ZERO FRONT // PHASE II — FORTRESS INTERDICTION", 4.2f);
                        GrantPhaseResupply(AmmoType.EMP, 4, 2);
                        _game.RepairEagle(1);
                        break;
                    case 3:
                        Announce("ZERO FRONT // PHASE III — OVERDRIVE ASSAULT", 4.2f);
                        GrantPhaseResupply(AmmoType.Plasma, 4, 3);
                        break;
                    case 4:
                        Announce("ZERO FRONT // FINAL PHASE — DESTROY OVERDRIVE ZERO", 5f);
                        GrantPhaseResupply(AmmoType.Plasma, 6, 4);
                        ArmFinalDefense();
                        break;
                }
            }

            ApplyRoundEscalation(round);
        }

        private void GrantPhaseResupply(AmmoType ammo, int amount, int repair)
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null) return;
            player.AddAmmo(ammo, amount);
            player.AddAmmo(AmmoType.ArmorPiercing, 3);
            if (player.Health != null)
            {
                player.Health.Heal(repair);
                player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 2.2f);
            }
            BattleAudio.PlayGlobal(SoundCue.AmmoPickup, 0.62f, 0f);
        }

        private void ApplyRoundEscalation(int round)
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null) return;

            int local = round - 90;
            if (local % 2 == 0)
                player.AddAmmo(AmmoType.ArmorPiercing, 2);
            if (round >= 94 && local % 3 == 0)
                player.AddAmmo(AmmoType.EMP, 1);
            if (round >= 97 && local % 2 == 1)
                player.AddAmmo(AmmoType.Plasma, 1);

            if (round == 95 || round == 98)
            {
                WarEconomyDirector.AwardMissionBonds(6 + _phase * 2, "ZERO FRONT STAGING");
                if (player.Health != null) player.Health.Heal(1);
                _game.RepairEagle(1);
            }
        }

        private void ArmFinalDefense()
        {
            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;
            if (player != null && player.Health != null)
                player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 4f);
            if (eagle != null)
                eagle.InvulnerableUntil = Mathf.Max(eagle.InvulnerableUntil, Time.time + 3f);
            _game.RepairEagle(2);
            VisualFactory.RingPulse(eagle != null ? eagle.transform.position : Vector3.zero, new Color(1f, 0.78f, 0.18f), 2.2f);
        }

        private void HookEnemies()
        {
            foreach (EnemyTank enemy in CombatRoster.Enemies)
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
            if (enemy == null || _game == null || !_game.IsPlaying || _game.CurrentRound < 91) return;
            _kills++;
            if (enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss)
                _eliteKills++;

            int threshold = Mathf.Max(6, 11 - _phase);
            if (_kills > 0 && _kills % threshold == 0)
            {
                _supportCharges++;
                PlayerTank player = CombatRoster.Player;
                if (player != null)
                {
                    player.AddAmmo(_phase >= 3 ? AmmoType.Plasma : AmmoType.Explosive, 1 + _phase / 2);
                    if (player.Health != null && _supportCharges % 2 == 0)
                        player.Health.Heal(1);
                }
                Announce($"ZERO FRONT SUPPORT // COMBAT CHARGE {_supportCharges}", 2.2f);
            }

            if (_eliteKills > 0 && _eliteKills % 5 == 0)
                WarEconomyDirector.AwardMissionBonds(2 + _phase, "ZERO FRONT ELITE KILL");
        }

        private void CheckEmergencySupport()
        {
            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;
            if (player == null || player.Health == null || eagle == null) return;

            float playerRatio = player.Health.Max > 0 ? (float)player.Health.Current / player.Health.Max : 1f;
            float eagleRatio = eagle.Max > 0 ? (float)eagle.Current / eagle.Max : 1f;
            string key = $"TankRevival.ZeroFrontEmergency.{_round}";
            if (PlayerPrefs.GetInt(key, 0) != 0) return;

            if (playerRatio <= 0.28f || eagleRatio <= 0.30f)
            {
                PlayerPrefs.SetInt(key, 1);
                PlayerPrefs.Save();
                player.Health.Heal(2);
                player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 2.8f);
                _game.RepairEagle(1);
                player.AddAmmo(AmmoType.EMP, 1);
                player.AddAmmo(AmmoType.ArmorPiercing, 2);
                Announce("ZERO FRONT EMERGENCY COMMAND // FIELD RECOVERY", 3f);
                VisualFactory.RingPulse(player.transform.position, new Color(0.22f, 1f, 0.56f), 1.6f);
                BattleAudio.PlayGlobal(SoundCue.Pickup, 0.7f, -0.02f);
            }
        }

        private void TryGrantFinalReward()
        {
            if (_finalRewardGranted) return;
            Health eagle = CombatRoster.Eagle;
            PlayerTank player = CombatRoster.Player;
            if (eagle == null || eagle.IsDead || player == null || player.Health == null || player.Health.IsDead) return;

            bool bossAlive = false;
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy != null && enemy.Health != null && !enemy.Health.IsDead && enemy.Kind == EnemyKind.Boss)
                {
                    bossAlive = true;
                    break;
                }
            }
            if (bossAlive) return;

            _finalRewardGranted = true;
            WarEconomyDirector.AwardMissionBonds(40, "OPERATION OVERDRIVE COMPLETE");
            PlayerPrefs.SetInt("TankRevival.OperationOverdriveCompleted", 1);
            PlayerPrefs.SetInt("TankRevival.ZeroFrontBestKills", Mathf.Max(PlayerPrefs.GetInt("TankRevival.ZeroFrontBestKills", 0), _kills));
            PlayerPrefs.Save();
            Announce("OPERATION OVERDRIVE COMPLETE // ZERO FRONT DESTROYED", 8f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 1f, 0.04f);
            VisualFactory.RingPulse(player.transform.position, new Color(0.18f, 0.92f, 1f), 2.8f);
            VisualFactory.RingPulse(eagle.transform.position, new Color(1f, 0.82f, 0.20f), 3.1f);
        }

        private void Announce(string text, float duration)
        {
            _banner = text;
            _bannerUntil = Time.unscaledTime + duration;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.34f, 0.24f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.68f, 0.73f, 0.82f) } };
            _bannerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _game.CurrentRound < 91) return;
            EnsureStyles();

            float x = Mathf.Max(14f, Screen.width - 354f);
            float y = 104f;
            GUI.color = new Color(0.055f, 0.012f, 0.014f, 0.93f);
            GUI.Box(new Rect(x, y, 340f, 91f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 8f, 315f, 20f), "ZERO FRONT // FINAL OPERATION", _header);
            GUI.Label(new Rect(x + 12f, y + 31f, 315f, 18f), $"PHASE {_phase}/4   ROUND {_game.CurrentRound}/100", _body);
            GUI.Label(new Rect(x + 12f, y + 50f, 315f, 17f), $"KILLS {_kills}   ELITE {_eliteKills}   SUPPORT {_supportCharges}", _small);
            GUI.Label(new Rect(x + 12f, y + 68f, 315f, 17f), "Existing boss, fortress, frontline and terrain systems remain active.", _small);

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.04f, 0.008f, 0.012f, 0.95f);
                GUI.Box(new Rect(Screen.width * 0.5f - 410f, Screen.height * 0.31f - 34f, 820f, 68f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 400f, Screen.height * 0.31f - 22f, 800f, 44f), _banner, _bannerStyle);
            }
        }
    }
}
