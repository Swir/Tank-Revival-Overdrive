using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.2 OPERATIONS. Every tenth-round offset launches a deterministic special operation
    /// by promoting threats already spawned by TankGame, then awards medals and integrated field rewards.
    /// </summary>
    public sealed class OperationChallengeDirector : MonoBehaviour
    {
        private enum OperationKind { None, Spearhead, EagleLock, ArmorHunt }

        private TankGame _game;
        private int _round;
        private OperationKind _kind;
        private int _target;
        private int _progress;
        private int _promoted;
        private int _startEagleHp;
        private bool _resolved;
        private float _bannerUntil;
        private string _banner = string.Empty;
        private readonly HashSet<int> _hooked = new HashSet<int>();
        private readonly HashSet<int> _operationTargets = new HashSet<int>();
        private GUIStyle _header, _body, _small, _medal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<OperationChallengeDirector>() != null) return;
            var go = new GameObject("OperationChallengeDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<OperationChallengeDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying) return;

            int current = _game.CurrentRound;
            if (current != _round)
            {
                if (_round > 0 && _kind != OperationKind.None && !_resolved) ResolveOperation();
                BeginRound(current);
            }

            if (_kind == OperationKind.None || _resolved) return;
            HookEnemies();
            if (_kind == OperationKind.Spearhead || _kind == OperationKind.EagleLock)
                PromoteOperationThreats();
        }

        private void BeginRound(int round)
        {
            _round = round;
            _progress = 0;
            _promoted = 0;
            _resolved = false;
            _hooked.Clear();
            _operationTargets.Clear();

            if (round < 8 || round > 98 || (round - 8) % 10 != 0)
            {
                _kind = OperationKind.None;
                return;
            }

            int index = (round - 8) / 10;
            _kind = (OperationKind)(1 + index % 3);
            _target = _kind == OperationKind.ArmorHunt ? Mathf.Clamp(2 + round / 25, 2, 6) : (_kind == OperationKind.Spearhead ? 2 : 1);
            Health eagle = CombatRoster.Eagle;
            _startEagleHp = eagle != null ? eagle.Current : 0;
            _banner = $"SPECIAL OPERATION // {OperationName()}";
            _bannerUntil = Time.unscaledTime + 4.5f;
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.55f, 0.02f);
        }

        private void HookEnemies()
        {
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                int id = enemy.GetInstanceID();
                if (!_hooked.Add(id)) continue;
                EnemyTank captured = enemy;
                enemy.Health.Died += _ => OnEnemyDestroyed(captured);
            }
        }

        private void PromoteOperationThreats()
        {
            if (_promoted >= _target) return;

            EnemyTank best = null;
            int bestScore = -1;
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (_operationTargets.Contains(enemy.GetInstanceID())) continue;
                if (enemy.Kind == EnemyKind.Boss || enemy.GetComponent<EliteEncounterAgent>() != null) continue;

                int score = enemy.Kind switch
                {
                    EnemyKind.Siege => 60,
                    EnemyKind.Elite => 55,
                    EnemyKind.Heavy => 45,
                    EnemyKind.Sniper => 35,
                    EnemyKind.Fast => 20,
                    _ => 10
                };
                if (score > bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }

            if (best == null) return;
            _operationTargets.Add(best.GetInstanceID());
            _promoted++;
            var agent = best.gameObject.AddComponent<OperationTargetAgent>();
            agent.Initialize(_game, best, _kind == OperationKind.EagleLock, Mathf.Clamp((_round - 1) / 20, 0, 4));
        }

        private void OnEnemyDestroyed(EnemyTank enemy)
        {
            if (_resolved || _kind == OperationKind.None || enemy == null) return;
            int id = enemy.GetInstanceID();

            if (_kind == OperationKind.ArmorHunt)
            {
                if (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Boss)
                    _progress++;
            }
            else if (_operationTargets.Contains(id))
            {
                _progress++;
            }
        }

        private void ResolveOperation()
        {
            _resolved = true;
            bool success = false;
            switch (_kind)
            {
                case OperationKind.Spearhead:
                case OperationKind.ArmorHunt:
                    success = _progress >= _target;
                    break;
                case OperationKind.EagleLock:
                    Health eagle = CombatRoster.Eagle;
                    success = _progress >= _target && eagle != null && !eagle.IsDead && eagle.Current >= _startEagleHp;
                    break;
            }

            if (!success)
            {
                _banner = "OPERATION FAILED // NO MEDAL AWARDED";
                _bannerUntil = Time.unscaledTime + 3f;
                return;
            }

            PlayerTank player = CombatRoster.Player;
            Health core = CombatRoster.Eagle;
            int medal = 1;
            if (core != null && core.Current >= Mathf.Max(4, _startEagleHp)) medal++;
            if (player != null && player.Health != null && player.Health.Current >= Mathf.Max(2, player.Health.Maximum - 1)) medal++;

            PlayerPrefs.SetInt("TankRevival.OperationsCompleted", PlayerPrefs.GetInt("TankRevival.OperationsCompleted", 0) + 1);
            PlayerPrefs.SetInt("TankRevival.OperationMedals", PlayerPrefs.GetInt("TankRevival.OperationMedals", 0) + medal);
            PlayerPrefs.Save();

            if (player != null)
            {
                player.Health?.Heal(1);
                player.AddAmmo(_round >= 70 ? AmmoType.Plasma : _round >= 40 ? AmmoType.EMP : AmmoType.ArmorPiercing, 3 + medal);
            }
            _game.RepairEagle(1);
            _banner = $"OPERATION COMPLETE // {MedalName(medal)} MEDAL // FIELD REWARD";
            _bannerUntil = Time.unscaledTime + 4f;
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.74f, 0f);
        }

        private string OperationName()
        {
            return _kind switch
            {
                OperationKind.Spearhead => "SPEARHEAD DECAPITATION",
                OperationKind.EagleLock => "EAGLE LOCKDOWN",
                OperationKind.ArmorHunt => "ARMORED PURGE",
                _ => string.Empty
            };
        }

        private string Objective()
        {
            return _kind switch
            {
                OperationKind.Spearhead => $"Destroy marked assault leaders [{Mathf.Min(_progress, _target)}/{_target}]",
                OperationKind.EagleLock => $"Destroy marked breaker and lose no Orzelek HP [{Mathf.Min(_progress, _target)}/{_target}]",
                OperationKind.ArmorHunt => $"Destroy heavy priority armor [{Mathf.Min(_progress, _target)}/{_target}]",
                _ => string.Empty
            };
        }

        private static string MedalName(int medal) => medal >= 3 ? "GOLD" : medal == 2 ? "SILVER" : "BRONZE";

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.72f, 0.18f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.66f, 0.74f, 0.80f) } };
            _medal = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.82f, 0.28f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            if (_kind != OperationKind.None && !_resolved)
            {
                float y = 316f;
                GUI.color = new Color(0.045f, 0.030f, 0.015f, 0.93f);
                GUI.Box(new Rect(14f, y, 455f, 72f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(28f, y + 7f, 425f, 20f), "SPECIAL OPERATION // " + OperationName(), _header);
                GUI.Label(new Rect(28f, y + 30f, 425f, 18f), Objective(), _body);
                GUI.Label(new Rect(28f, y + 50f, 425f, 16f), "Performance determines Bronze / Silver / Gold medal.", _small);
            }

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.95f);
                GUI.Box(new Rect(Screen.width * 0.5f - 380f, Screen.height * 0.31f, 760f, 54f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 370f, Screen.height * 0.31f + 8f, 740f, 38f), _banner, _medal);
            }
        }
    }

    public sealed class OperationTargetAgent : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _enemy;
        private Health _health;
        private bool _eagleBreaker;
        private int _tier;
        private float _nextAttack;
        private float _nextPulse;

        public void Initialize(TankGame game, EnemyTank enemy, bool eagleBreaker, int tier)
        {
            _game = game;
            _enemy = enemy;
            _health = enemy.Health;
            _eagleBreaker = eagleBreaker;
            _tier = Mathf.Clamp(tier, 0, 4);
            _health.SetMaximum(_health.Maximum + 3 + _tier * 2, true);
            _nextAttack = Time.time + 2.2f;
            _nextPulse = Time.time;
            transform.localScale *= 1.05f;
            VisualFactory.RingPulse(transform.position, new Color(1f, 0.72f, 0.08f), 1.4f);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || _health == null || _health.IsDead) return;

            if (Time.time >= _nextPulse)
            {
                _nextPulse = Time.time + 0.8f;
                VisualFactory.RingPulse(transform.position, new Color(1f, 0.66f, 0.08f, 0.58f), 0.62f);
            }

            if (Time.time < _nextAttack) return;
            _nextAttack = Time.time + Mathf.Max(1.7f, 3.2f - _tier * 0.25f);

            Vector2 origin = transform.position;
            Vector2 target = _eagleBreaker ? _game.BasePosition : _game.PlayerPosition;
            Vector2 direction = (target - origin).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.down;
            AmmoType ammo = _tier >= 4 ? AmmoType.Plasma : _tier >= 2 ? AmmoType.ArmorPiercing : AmmoType.Basic;
            int damage = _tier >= 3 ? 2 : 1;
            Color color = ammo == AmmoType.Plasma ? new Color(0.40f, 0.88f, 1f) : new Color(1f, 0.58f, 0.08f);
            _game.SpawnProjectile(origin + direction * 0.78f, direction, Team.Enemy, damage, 8.8f + _tier * 0.5f, color, ammo);
            if (_tier >= 2)
            {
                Vector2 side = new Vector2(-direction.y, direction.x);
                _game.SpawnProjectile(origin + direction * 0.72f + side * 0.18f, (direction + side * 0.08f).normalized, Team.Enemy, damage, 8.4f + _tier * 0.4f, color, ammo);
            }
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.22f, 0.05f);
        }
    }
}
