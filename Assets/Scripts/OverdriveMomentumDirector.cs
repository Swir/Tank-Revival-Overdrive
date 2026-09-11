using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public sealed class OverdriveMomentumDirector : MonoBehaviour
    {
        private TankGame _game;
        private readonly HashSet<int> _hooked = new HashSet<int>();
        private float _scanAt;
        private float _momentum;
        private float _lastKillAt = -99f;
        private float _overdriveUntil;
        private float _cooldownUntil;
        private int _chain;
        private string _message = string.Empty;
        private float _messageUntil;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _center;

        public bool Active => Time.time < _overdriveUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<OverdriveMomentumDirector>() != null) return;
            var go = new GameObject("OverdriveMomentumDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<OverdriveMomentumDirector>();
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
                _momentum = Mathf.MoveTowards(_momentum, 0f, 24f * Time.unscaledDeltaTime);
                _chain = 0;
                return;
            }

            if (Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.32f;
                HookEnemies();
            }

            if (!Active && Time.time - _lastKillAt > 5.5f)
            {
                _momentum = Mathf.MoveTowards(_momentum, 0f, 5.5f * Time.deltaTime);
                if (Time.time - _lastKillAt > 8f) _chain = 0;
            }

            if (Active)
            {
                PlayerTank player = FindAnyObjectByType<PlayerTank>();
                if (player != null && player.Health != null)
                    player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 0.20f);
            }
            else if (_overdriveUntil > 0f && Time.time >= _overdriveUntil)
            {
                _overdriveUntil = 0f;
                _cooldownUntil = Time.time + 10f;
                _momentum = 0f;
                _chain = 0;
                ShowMessage("OVERDRIVE COMPLETE // SYSTEMS COOLING", 2.0f);
            }
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

            if (_hooked.Count > 512)
                _hooked.Clear();
        }

        private void OnEnemyDestroyed(EnemyTank enemy)
        {
            if (enemy == null || _game == null || !_game.IsPlaying) return;

            float now = Time.time;
            if (now - _lastKillAt <= 4.2f) _chain++;
            else _chain = 1;
            _lastKillAt = now;

            float gain = enemy.Kind switch
            {
                EnemyKind.Boss => 48f,
                EnemyKind.Elite => 24f,
                EnemyKind.Siege => 19f,
                EnemyKind.Heavy => 16f,
                EnemyKind.Sniper => 14f,
                EnemyKind.Supply => 13f,
                EnemyKind.Fast => 11f,
                _ => 9f
            };

            gain += Mathf.Min(12f, Mathf.Max(0, _chain - 1) * 1.5f);
            if (Active)
            {
                _overdriveUntil = Mathf.Min(_overdriveUntil + gain * 0.018f, Time.time + 12f);
                return;
            }

            _momentum = Mathf.Min(100f, _momentum + gain);
            if (_chain == 4) ShowMessage("COMBAT CHAIN x4 // MOMENTUM RISING", 1.2f);
            else if (_chain == 8) ShowMessage("COMBAT CHAIN x8 // OVERDRIVE NEAR", 1.4f);

            if (_momentum >= 100f && Time.time >= _cooldownUntil)
                ActivateOverdrive();
        }

        private void ActivateOverdrive()
        {
            _momentum = 100f;
            _overdriveUntil = Time.time + 8.5f;
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player != null)
            {
                if (player.Health != null)
                {
                    player.Health.Heal(1);
                    player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, _overdriveUntil);
                }

                int round = _game != null ? _game.CurrentRound : 1;
                AmmoType ammo = round >= 50 ? AmmoType.Plasma : round >= 25 ? AmmoType.EMP : round >= 8 ? AmmoType.Explosive : AmmoType.ArmorPiercing;
                player.AddAmmo(ammo, round >= 70 ? 8 : 5);
            }

            _game?.RepairEagle(1);
            if (player != null)
                VisualFactory.RingPulse(player.transform.position, new Color(0.15f, 0.90f, 1f), 1.8f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.90f, 0f);
            ShowMessage("OVERDRIVE ENGAGED // SHIELD + REPAIR + SPECIAL AMMO", 2.4f);
        }

        private void ShowMessage(string text, float duration)
        {
            _message = text;
            _messageUntil = Time.unscaledTime + duration;
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.28f, 0.92f, 1f) }
            };
            _small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.72f, 0.82f, 0.88f) }
            };
            _center = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            float width = Mathf.Min(420f, Screen.width * 0.34f);
            float x = Screen.width * 0.5f - width * 0.5f;
            float y = 10f;
            GUI.color = new Color(0.02f, 0.035f, 0.055f, 0.92f);
            GUI.Box(new Rect(x, y, width, 48f), string.Empty);
            GUI.color = Color.white;

            string state = Active ? $"OVERDRIVE {Mathf.Max(0f, _overdriveUntil - Time.time):0.0}s" : "COMBAT MOMENTUM";
            GUI.Label(new Rect(x + 12f, y + 5f, width - 24f, 20f), state, _label);
            GUI.Label(new Rect(x + width - 118f, y + 5f, 106f, 20f), $"CHAIN x{_chain}", _small);

            float ratio = Active ? 1f : Mathf.Clamp01(_momentum / 100f);
            GUI.color = new Color(0.07f, 0.11f, 0.16f, 0.96f);
            GUI.Box(new Rect(x + 12f, y + 29f, width - 24f, 10f), string.Empty);
            GUI.color = Active ? new Color(0.30f, 1f, 0.92f, 1f) : new Color(0.12f, 0.72f, 1f, 1f);
            GUI.Box(new Rect(x + 14f, y + 31f, (width - 28f) * ratio, 6f), string.Empty);
            GUI.color = Color.white;

            if (Time.unscaledTime < _messageUntil)
            {
                GUI.color = new Color(0.015f, 0.035f, 0.055f, 0.94f);
                GUI.Box(new Rect(Screen.width * 0.5f - 330f, Screen.height * 0.24f, 660f, 42f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 320f, Screen.height * 0.24f + 7f, 640f, 28f), _message, _center);
            }
        }
    }
}
