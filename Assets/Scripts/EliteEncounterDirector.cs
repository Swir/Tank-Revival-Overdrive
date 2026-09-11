using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.1 elite encounter layer. Converts one existing heavy threat into a named mini-boss
    /// on rounds 15/35/55/75/95 instead of spawning detached content outside TankGame.
    /// </summary>
    public sealed class EliteEncounterDirector : MonoBehaviour
    {
        private static readonly int[] EncounterRounds = { 15, 35, 55, 75, 95 };
        private static readonly string[] EncounterNames =
        {
            "IRON JACKAL",
            "ASH EXECUTIONER",
            "FROST HAMMER",
            "FORTRESS BREAKER",
            "BLACK EAGLE HUNTER"
        };

        private TankGame _game;
        private int _round;
        private bool _armed;
        private bool _deployed;
        private float _scanAt;
        private float _bannerUntil;
        private string _banner = string.Empty;
        private GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EliteEncounterDirector>() != null) return;
            var go = new GameObject("EliteEncounterDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<EliteEncounterDirector>();
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
                _deployed = false;
                _armed = EncounterIndex(round) >= 0;
                if (_armed)
                {
                    _banner = $"ELITE ENCOUNTER // {EncounterNames[EncounterIndex(round)]}";
                    _bannerUntil = Time.unscaledTime + 4f;
                }
            }

            if (_armed && !_deployed && Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.25f;
                TryPromoteThreat();
            }
        }

        private static int EncounterIndex(int round)
        {
            for (int i = 0; i < EncounterRounds.Length; i++)
                if (EncounterRounds[i] == round) return i;
            return -1;
        }

        private void TryPromoteThreat()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            EnemyTank best = null;
            int bestScore = -1;

            foreach (EnemyTank enemy in enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (enemy.GetComponent<EliteEncounterAgent>() != null) continue;

                int score = enemy.Kind switch
                {
                    EnemyKind.Boss => -1,
                    EnemyKind.Elite => 50,
                    EnemyKind.Siege => 45,
                    EnemyKind.Heavy => 35,
                    EnemyKind.Sniper => 25,
                    _ => 10
                };

                if (score > bestScore)
                {
                    best = enemy;
                    bestScore = score;
                }
            }

            if (best == null) return;
            int index = EncounterIndex(_round);
            if (index < 0) return;

            var agent = best.gameObject.AddComponent<EliteEncounterAgent>();
            agent.Initialize(_game, best, EncounterNames[index], index);
            _deployed = true;
            _armed = false;
            _banner = $"MINI-BOSS DEPLOYED // {EncounterNames[index]}";
            _bannerUntil = Time.unscaledTime + 3.5f;
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.64f, 0f);
        }

        private void EnsureStyle()
        {
            if (_style != null) return;
            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.50f, 0.16f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || Time.unscaledTime >= _bannerUntil) return;
            EnsureStyle();
            GUI.color = new Color(0.04f, 0.015f, 0.01f, 0.90f);
            GUI.Box(new Rect(Screen.width * 0.5f - 340f, Screen.height * 0.35f, 680f, 54f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * 0.5f - 330f, Screen.height * 0.35f + 9f, 660f, 36f), _banner, _style);
        }
    }

    public sealed class EliteEncounterAgent : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _enemy;
        private Health _health;
        private string _title;
        private int _tier;
        private int _phase;
        private float _nextSpecial;
        private float _nextPulse;

        public string Title => _title;
        public int Phase => _phase;

        public void Initialize(TankGame game, EnemyTank enemy, string title, int tier)
        {
            _game = game;
            _enemy = enemy;
            _health = enemy.Health;
            _title = title;
            _tier = Mathf.Clamp(tier, 0, 4);

            int bonus = 5 + _tier * 3;
            _health.SetMaximum(_health.Maximum + bonus, true);
            _health.Died += OnDefeated;
            _nextSpecial = Time.time + 2.3f;
            _nextPulse = Time.time;

            transform.localScale *= 1.08f + _tier * 0.015f;
            VisualFactory.RingPulse(transform.position, new Color(1f, 0.28f, 0.05f), 1.8f);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || _health == null || _health.IsDead) return;

            float ratio = _health.Maximum > 0 ? (float)_health.Current / _health.Maximum : 0f;
            int nextPhase = ratio <= 0.30f ? 3 : ratio <= 0.58f ? 2 : ratio <= 0.82f ? 1 : 0;
            if (nextPhase > _phase)
            {
                _phase = nextPhase;
                _health.InvulnerableUntil = Mathf.Max(_health.InvulnerableUntil, Time.time + 0.65f);
                VisualFactory.RingPulse(transform.position, new Color(1f, 0.12f, 0.03f), 1.5f + _phase * 0.18f);
                BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.48f, 0.04f);
            }

            if (Time.time >= _nextSpecial)
            {
                FireSpecial();
                _nextSpecial = Time.time + Mathf.Max(1.35f, 3.1f - _tier * 0.22f - _phase * 0.30f);
            }

            if (Time.time >= _nextPulse)
            {
                _nextPulse = Time.time + 0.75f;
                VisualFactory.RingPulse(transform.position, new Color(1f, 0.42f, 0.08f, 0.62f), 0.68f);
            }
        }

        private void FireSpecial()
        {
            Vector2 origin = transform.position;
            bool attackEagle = _tier >= 2 && (_phase >= 2 || Random.value < 0.45f);
            Vector2 target = attackEagle ? _game.BasePosition : _game.PlayerPosition;
            Vector2 direction = (target - origin).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.down;

            int damage = _tier >= 3 ? 2 : 1;
            float speed = 8.8f + _tier * 0.55f;
            AmmoType ammo = _tier >= 4 && _phase >= 2 ? AmmoType.Plasma : _tier >= 2 ? AmmoType.ArmorPiercing : AmmoType.Basic;
            Color color = ammo == AmmoType.Plasma ? new Color(0.45f, 0.88f, 1f) : new Color(1f, 0.25f, 0.06f);
            Vector2 side = new Vector2(-direction.y, direction.x);

            VisualFactory.RingPulse(target, new Color(1f, 0.18f, 0.04f), 0.9f);
            _game.SpawnProjectile(origin + direction * 0.8f, direction, Team.Enemy, damage, speed, color, ammo);

            if (_phase >= 1)
                _game.SpawnProjectile(origin + direction * 0.75f + side * 0.20f, (direction + side * 0.10f).normalized, Team.Enemy, damage, speed * 0.96f, color, ammo);
            if (_phase >= 3)
                _game.SpawnProjectile(origin + direction * 0.75f - side * 0.20f, (direction - side * 0.10f).normalized, Team.Enemy, damage, speed * 0.96f, color, ammo);
        }

        private void OnDefeated(Health _)
        {
            if (_game == null) return;
            _game.RepairEagle(1);
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player != null)
            {
                player.AddAmmo(AmmoType.ArmorPiercing, 3 + _tier);
                if (_tier >= 2) player.AddAmmo(AmmoType.EMP, 1 + _tier / 2);
                if (_tier >= 4) player.AddAmmo(AmmoType.Plasma, 2);
                if (player.Health != null) player.Health.Heal(1);
            }
            PlayerPrefs.SetInt("TankRevival.EliteEncountersDefeated", PlayerPrefs.GetInt("TankRevival.EliteEncountersDefeated", 0) + 1);
            PlayerPrefs.Save();
        }
    }
}
