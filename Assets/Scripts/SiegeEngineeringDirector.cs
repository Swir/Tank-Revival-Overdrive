using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v1.6 SIEGE ENGINEERING — enemy breach infrastructure.
    /// Promotes existing faction-role units into sappers, siege batteries and support relays.
    /// These systems explicitly pressure fortress modules and Orzelek instead of acting as generic DPS.
    /// </summary>
    public sealed class SiegeEngineeringDirector : MonoBehaviour
    {
        private TankGame _game;
        private int _round = -1;
        private float _nextScan;
        private string _order = "NO SIEGE ORDER";
        private GUIStyle _header;
        private GUIStyle _body;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<SiegeEngineeringDirector>() != null) return;
            var go = new GameObject("SiegeEngineeringDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<SiegeEngineeringDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }
            if (!_game.IsPlaying) return;

            if (_round != _game.CurrentRound)
            {
                _round = _game.CurrentRound;
                _order = ResolveOrder(_round);
                _nextScan = Time.time + 0.8f;
            }

            if (Time.time < _nextScan) return;
            _nextScan = Time.time + 1.1f;
            AttachEngineeringPackages();
        }

        private static string ResolveOrder(int round)
        {
            if (round < 16) return "BREACH RECON";
            if (round < 31) return "SAPPER ADVANCE";
            if (round < 51) return "DEMOLITION CORRIDOR";
            if (round < 71) return "MOBILE SIEGE LINE";
            if (round < 91) return "FORTRESS ANNIHILATION";
            return "TOTAL EAGLE SIEGE";
        }

        private void AttachEngineeringPackages()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            int sapperBudget = Mathf.Clamp(1 + _round / 28, 1, 4);
            int batteryBudget = _round >= 28 ? Mathf.Clamp(1 + (_round - 28) / 34, 1, 3) : 0;
            int relayBudget = _round >= 38 ? Mathf.Clamp(1 + (_round - 38) / 40, 1, 2) : 0;

            int sappers = CountComponents<SiegeSapper>();
            int batteries = CountComponents<MobileSiegeBattery>();
            int relays = CountComponents<EnemySiegeRelay>();

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                EnemyDoctrineAgent doctrine = enemy.GetComponent<EnemyDoctrineAgent>();

                if (sappers < sapperBudget && enemy.GetComponent<SiegeSapper>() == null && IsSapperCandidate(enemy, doctrine))
                {
                    var sapper = enemy.gameObject.AddComponent<SiegeSapper>();
                    sapper.Initialize(_game, _round);
                    sappers++;
                    continue;
                }

                if (batteries < batteryBudget && enemy.GetComponent<MobileSiegeBattery>() == null && IsBatteryCandidate(enemy, doctrine))
                {
                    var battery = enemy.gameObject.AddComponent<MobileSiegeBattery>();
                    battery.Initialize(_game, _round);
                    batteries++;
                    continue;
                }

                if (relays < relayBudget && enemy.GetComponent<EnemySiegeRelay>() == null && IsRelayCandidate(enemy, doctrine))
                {
                    var relay = enemy.gameObject.AddComponent<EnemySiegeRelay>();
                    relay.Initialize(_game, _round);
                    relays++;
                }
            }
        }

        private bool IsSapperCandidate(EnemyTank enemy, EnemyDoctrineAgent doctrine)
        {
            if (_round < 16) return enemy.Kind == EnemyKind.Siege;
            if (doctrine != null && doctrine.Role == EnemyBattleRole.Breacher) return true;
            return enemy.Kind == EnemyKind.Fast && _round >= 55;
        }

        private bool IsBatteryCandidate(EnemyTank enemy, EnemyDoctrineAgent doctrine)
        {
            if (enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss) return true;
            return _round >= 62 && enemy.Kind == EnemyKind.Heavy;
        }

        private bool IsRelayCandidate(EnemyTank enemy, EnemyDoctrineAgent doctrine)
        {
            if (doctrine != null && doctrine.Role == EnemyBattleRole.Engineer) return true;
            return _round >= 70 && enemy.Kind == EnemyKind.Elite;
        }

        private static int CountComponents<T>() where T : Component
        {
            return FindObjectsByType<T>(FindObjectsSortMode.None).Length;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.35f, 0.16f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.94f, 0.76f, 0.68f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _round < 16) return;
            EnsureStyles();
            float w = 320f;
            float x = Mathf.Max(12f, Screen.width - w - 14f);
            float y = 126f;
            GUI.color = new Color(0.08f, 0.018f, 0.012f, 0.90f);
            GUI.Box(new Rect(x, y, w, 54f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 7f, w - 24f, 18f), "ENEMY SIEGE ENGINEERING", _header);
            GUI.Label(new Rect(x + 12f, y + 27f, w - 24f, 18f), _order, _body);
        }
    }

    public sealed class SiegeSapper : MonoBehaviour
    {
        private TankGame _game;
        private Health _self;
        private int _round;
        private float _nextCharge;
        private bool _armed;

        public void Initialize(TankGame game, int round)
        {
            _game = game;
            _round = Mathf.Clamp(round, 1, 100);
            _self = GetComponent<Health>();
            _nextCharge = Time.time + Random.Range(3f, 5f);
            VisualFactory.RingObject("SapperRing", transform, Vector2.one * 1.15f, new Color(1f, 0.42f, 0.08f, 0.55f), Vector3.zero, 20);
            VisualFactory.Rect("SapperCharge", transform, new Vector2(0.42f, 0.16f), new Color(1f, 0.18f, 0.04f), new Vector3(0f, -0.54f, 0f), 21);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || _self == null || _self.IsDead || Time.time < _nextCharge) return;
            GameObject target = FindFortificationTarget();
            if (target == null) return;
            float distance = Vector2.Distance(transform.position, target.transform.position);
            if (distance > 2.35f) return;

            _nextCharge = Time.time + Mathf.Max(4.8f, 8.2f - _round * 0.025f);
            DetonateCharge(target);
        }

        private GameObject FindFortificationTarget()
        {
            GameObject[] all = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            GameObject best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null) continue;
                string n = go.name;
                bool fortification = n.Contains("FORTRESS_") || n.Contains("PLAYER_BARRICADE") || n.Contains("PLAYER_ANTI_SIEGE");
                if (!fortification) continue;
                Health h = go.GetComponent<Health>();
                if (h == null || h.IsDead || h.Team != Team.Player) continue;
                float d = Vector2.Distance(transform.position, go.transform.position);
                if (d < bestDistance) { bestDistance = d; best = go; }
            }
            return best;
        }

        private void DetonateCharge(GameObject target)
        {
            Health h = target.GetComponent<Health>();
            if (h == null || h.IsDead) return;
            int damage = _round >= 70 ? 4 : _round >= 40 ? 3 : 2;
            Vector2 blast = target.transform.position;
            VisualFactory.RingPulse(blast, new Color(1f, 0.10f, 0.03f), 1.35f);
            VisualFactory.Explosion(blast, new Color(1f, 0.32f, 0.06f), 1.25f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.48f, 0.06f);
            h.Damage(damage, Team.Enemy);
            _armed = true;
            if (_armed && _round >= 82 && Random.value < 0.25f)
                _self.Damage(Mathf.Max(1, _self.Current), Team.Player);
        }
    }

    public sealed class MobileSiegeBattery : MonoBehaviour
    {
        private TankGame _game;
        private Health _self;
        private int _round;
        private float _nextBarrage;

        public void Initialize(TankGame game, int round)
        {
            _game = game;
            _round = Mathf.Clamp(round, 1, 100);
            _self = GetComponent<Health>();
            _nextBarrage = Time.time + Random.Range(5.5f, 8.5f);
            VisualFactory.Rect("SiegeBatteryBadge", transform, new Vector2(0.62f, 0.12f), new Color(1f, 0.16f, 0.04f), new Vector3(0f, -0.70f, 0f), 22);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || _self == null || _self.IsDead || Time.time < _nextBarrage) return;
            _nextBarrage = Time.time + Mathf.Max(5.0f, 9.0f - _round * 0.035f) + Random.Range(-0.7f, 0.7f);
            FireBarrage();
        }

        private void FireBarrage()
        {
            Vector2 origin = transform.position;
            Vector2 target = _game.BasePosition;
            Vector2 direction = (target - origin).normalized;
            if (direction.sqrMagnitude < 0.01f) return;
            Color c = new Color(1f, 0.14f, 0.035f);
            int shells = _round >= 78 ? 3 : _round >= 48 ? 2 : 1;
            Vector2 side = new Vector2(-direction.y, direction.x);
            VisualFactory.RingPulse(transform.position, c, 0.95f);
            for (int i = 0; i < shells; i++)
            {
                float spread = (i - (shells - 1) * 0.5f) * 0.10f;
                Vector2 dir = (direction + side * spread).normalized;
                _game.SpawnProjectile(origin + dir * 0.80f, dir, Team.Enemy, _round >= 65 ? 3 : 2, 7.8f + _round * 0.018f, c, AmmoType.Explosive);
            }
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.25f, 0.08f);
        }
    }

    public sealed class EnemySiegeRelay : MonoBehaviour
    {
        private TankGame _game;
        private Health _self;
        private int _round;
        private float _nextPulse;

        public void Initialize(TankGame game, int round)
        {
            _game = game;
            _round = Mathf.Clamp(round, 1, 100);
            _self = GetComponent<Health>();
            _nextPulse = Time.time + 3.5f;
            VisualFactory.RingObject("SiegeRelayRing", transform, Vector2.one * 1.28f, new Color(0.88f, 0.18f, 1f, 0.42f), Vector3.zero, 20);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || _self == null || _self.IsDead || Time.time < _nextPulse) return;
            _nextPulse = Time.time + Mathf.Max(4.2f, 7.2f - _round * 0.018f);
            PulseSupport();
        }

        private void PulseSupport()
        {
            Health[] units = FindObjectsByType<Health>(FindObjectsSortMode.None);
            int supported = 0;
            for (int i = 0; i < units.Length && supported < 5; i++)
            {
                Health h = units[i];
                if (h == null || h == _self || h.IsDead || h.Team != Team.Enemy) continue;
                if (Vector2.Distance(transform.position, h.transform.position) > 4.2f) continue;
                if (h.Current < h.Maximum) h.Heal(1);
                if (_round >= 68)
                    h.InvulnerableUntil = Mathf.Max(h.InvulnerableUntil, Time.time + 0.40f);
                VisualFactory.RingPulse(h.transform.position, new Color(0.82f, 0.22f, 1f, 0.46f), 0.46f);
                supported++;
            }
            VisualFactory.RingPulse(transform.position, new Color(0.90f, 0.22f, 1f), 1.05f);
        }
    }
}
