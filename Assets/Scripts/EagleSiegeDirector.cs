using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v1.3 EAGLE SIEGE
    /// Turns the enemy objective into an explicit battlefield system. Selected Heavy, Siege,
    /// Elite and Boss units become Eagle Hunters and periodically execute readable long-range
    /// attacks against Orzelek. The shells are real projectiles and can be stopped by destroying
    /// the attacker or intercepted by the fortress modules.
    /// </summary>
    public sealed class EagleSiegeDirector : MonoBehaviour
    {
        private TankGame _game;
        private int _round = -1;
        private float _nextScan;
        private readonly HashSet<int> _known = new HashSet<int>();
        private string _doctrine = "PROBING ATTACK";
        private int _threatCount;
        private string _banner = string.Empty;
        private float _bannerUntil;

        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _danger;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EagleSiegeDirector>() != null) return;
            var go = new GameObject("EagleSiegeDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<EagleSiegeDirector>();
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
                _round = -1;
                _known.Clear();
                _threatCount = 0;
                return;
            }

            int round = _game.CurrentRound;
            if (round != _round)
                BeginRound(round);

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.55f;
                UpgradeAssaultUnits();
            }
        }

        private void BeginRound(int round)
        {
            _round = round;
            _known.Clear();
            _threatCount = 0;
            _nextScan = Time.time + 0.35f;

            if (round >= 80)
                _doctrine = "FINAL EAGLE HUNT";
            else if (round >= 55)
                _doctrine = "SIEGE NETWORK";
            else if (round >= 25)
                _doctrine = "BREACH COLUMN";
            else
                _doctrine = "PROBING ATTACK";

            _banner = $"ENEMY DOCTRINE // {_doctrine} // ORZELEK IS THE PRIMARY OBJECTIVE";
            _bannerUntil = Time.unscaledTime + 3.0f;
        }

        private void UpgradeAssaultUnits()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            int active = 0;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;

                EagleAssaultAgent existing = enemy.GetComponent<EagleAssaultAgent>();
                if (existing != null)
                {
                    active++;
                    continue;
                }

                int id = enemy.GetInstanceID();
                if (!_known.Add(id)) continue;
                if (!ShouldBecomeHunter(enemy, id)) continue;

                float pressure = _round >= 80 ? 1.35f : _round >= 55 ? 1.18f : _round >= 25 ? 1.05f : 0.92f;
                var agent = enemy.gameObject.AddComponent<EagleAssaultAgent>();
                agent.Initialize(_game, enemy, _round, pressure);
                active++;
                VisualFactory.RingPulse(enemy.transform.position, new Color(1f, 0.18f, 0.04f), 0.72f);
            }

            _threatCount = active;
        }

        private bool ShouldBecomeHunter(EnemyTank enemy, int id)
        {
            if (enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss) return true;

            int roll = (id & 0x7fffffff) % 100;
            if (enemy.Kind == EnemyKind.Heavy)
                return _round >= 21 && roll < Mathf.Clamp(18 + _round / 3, 18, 50);
            if (enemy.Kind == EnemyKind.Elite)
                return _round >= 55 && roll < Mathf.Clamp(28 + _round / 3, 28, 62);
            if (enemy.Kind == EnemyKind.Fast)
                return _round >= 80 && roll < 18;
            return false;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.48f, 0.20f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.90f, 0.92f, 0.96f) } };
            _danger = new GUIStyle(_header) { normal = { textColor = new Color(1f, 0.18f, 0.10f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            float width = 510f;
            float x = Screen.width * 0.5f - width * 0.5f;
            float y = Screen.height - 92f;
            GUI.color = new Color(0.045f, 0.018f, 0.018f, 0.92f);
            GUI.Box(new Rect(x, y, width, 36f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f, y + 4f, width - 16f, 16f), $"EAGLE THREAT // {_doctrine} // HUNTERS {_threatCount}", _threatCount >= 3 ? _danger : _header);
            GUI.Label(new Rect(x + 8f, y + 19f, width - 16f, 14f), "Kill marked assault armor before it opens a breach on Orzelek.", _body);

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.12f, 0.018f, 0.012f, 0.94f);
                GUI.Box(new Rect(Screen.width * 0.5f - 350f, Screen.height * 0.30f, 700f, 42f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 335f, Screen.height * 0.30f + 11f, 670f, 20f), _banner, _danger);
            }
        }
    }

    public sealed class EagleAssaultAgent : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _enemy;
        private int _round;
        private float _pressure;
        private float _nextSiegeShot;
        private bool _firing;

        public void Initialize(TankGame game, EnemyTank enemy, int round, float pressure)
        {
            _game = game;
            _enemy = enemy;
            _round = Mathf.Clamp(round, 1, 100);
            _pressure = Mathf.Clamp(pressure, 0.8f, 1.5f);
            _nextSiegeShot = Time.time + Random.Range(3.4f, 6.4f);
        }

        private void Update()
        {
            if (_game == null || _enemy == null || _enemy.Health == null || _enemy.Health.IsDead || !_game.IsPlaying) return;
            if (_firing || Time.time < _nextSiegeShot) return;

            float distance = Vector2.Distance(transform.position, _game.BasePosition);
            if (distance > 15f) return;

            StartCoroutine(FireSiegeShot());
        }

        private IEnumerator FireSiegeShot()
        {
            _firing = true;
            Vector2 target = _game.BasePosition + Random.insideUnitCircle * (_enemy.Kind == EnemyKind.Boss ? 0.42f : 0.28f);
            Color warning = _enemy.Kind == EnemyKind.Boss ? new Color(1f, 0.02f, 0.04f) : new Color(1f, 0.20f, 0.04f);
            VisualFactory.RingPulse(target, warning, _enemy.Kind == EnemyKind.Boss ? 1.55f : 1.05f);
            VisualFactory.RingPulse(transform.position, warning, 0.65f);

            yield return new WaitForSeconds(_enemy.Kind == EnemyKind.Boss ? 0.45f : 0.68f);

            if (_game == null || !_game.IsPlaying || _enemy == null || _enemy.Health == null || _enemy.Health.IsDead)
            {
                _firing = false;
                yield break;
            }

            Vector2 direction = (target - (Vector2)transform.position).normalized;
            Vector2 muzzle = (Vector2)transform.position + direction * (_enemy.Kind == EnemyKind.Boss ? 1.05f : 0.82f);
            bool heavyShell = (_enemy.Kind == EnemyKind.Siege || _enemy.Kind == EnemyKind.Boss) && _round >= 45;
            AmmoType ammo = heavyShell ? AmmoType.Explosive : AmmoType.Basic;
            int damage = _enemy.Kind == EnemyKind.Boss ? 2 + _round / 70 : _enemy.Kind == EnemyKind.Siege ? 2 : 1;
            float speed = _enemy.Kind == EnemyKind.Boss ? 11.5f : 9.6f + _round * 0.010f;
            Color shell = heavyShell ? new Color(1f, 0.24f, 0.04f) : new Color(1f, 0.40f, 0.08f);

            _game.SpawnProjectile(muzzle, direction, Team.Enemy, damage, speed, shell, ammo);
            VisualFactory.MuzzleFlash(muzzle, shell, heavyShell ? 1.25f : 0.85f);
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, _enemy.Kind == EnemyKind.Boss ? 0.36f : 0.20f, 0.04f);

            if (_enemy.Kind == EnemyKind.Boss && _round >= 60)
            {
                yield return new WaitForSeconds(0.16f);
                Vector2 secondTarget = _game.BasePosition + Random.insideUnitCircle * 0.52f;
                Vector2 secondDirection = (secondTarget - (Vector2)transform.position).normalized;
                _game.SpawnProjectile((Vector2)transform.position + secondDirection * 1.05f, secondDirection, Team.Enemy, damage, speed, shell, ammo);
            }

            float baseCooldown = _enemy.Kind == EnemyKind.Boss ? 5.2f : _enemy.Kind == EnemyKind.Siege ? 7.2f : _enemy.Kind == EnemyKind.Elite ? 8.4f : 9.6f;
            _nextSiegeShot = Time.time + baseCooldown / _pressure + Random.Range(0.5f, 2.2f);
            _firing = false;
        }
    }
}
