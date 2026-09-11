using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public sealed class StrategicDirectiveDirector : MonoBehaviour
    {
        private enum Directive { None, EagleGuard, PriorityPurge, ArmorHold }

        private TankGame _game;
        private int _round;
        private Directive _directive;
        private int _target;
        private int _progress;
        private int _startEagleHp;
        private float _scanAt;
        private readonly HashSet<int> _hooked = new HashSet<int>();
        private GUIStyle _header, _body, _small, _success;
        private string _result = string.Empty;
        private float _resultUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<StrategicDirectiveDirector>() != null) return;
            var go = new GameObject("StrategicDirectiveDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<StrategicDirectiveDirector>();
        }

        private void Update()
        {
            if (_game == null) { _game = FindAnyObjectByType<TankGame>(); return; }
            if (!_game.IsPlaying) return;

            int current = _game.CurrentRound;
            if (current != _round)
            {
                if (_round > 0 && _directive != Directive.None) Resolve();
                Begin(current);
            }

            if (_directive != Directive.None && Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.35f;
                HookEnemies();
            }
        }

        private void Begin(int round)
        {
            _round = round;
            _progress = 0;
            _hooked.Clear();
            _directive = round % 5 == 0 && round % 10 != 0 ? (Directive)(1 + (round / 5) % 3) : Directive.None;
            _target = _directive == Directive.PriorityPurge ? Mathf.Clamp(2 + round / 30, 2, 5) : 1;
            Health eagle = FindEagle();
            _startEagleHp = eagle != null ? eagle.Current : 0;
        }

        private Health FindEagle()
        {
            Health[] all = FindObjectsByType<Health>(FindObjectsSortMode.None);
            foreach (Health h in all)
                if (h != null && h.name == "ORZELEK_DEFENSE_CORE") return h;
            return null;
        }

        private void HookEnemies()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            foreach (EnemyTank enemy in enemies)
            {
                if (enemy == null || enemy.Health == null) continue;
                if (!_hooked.Add(enemy.GetInstanceID())) continue;
                EnemyTank captured = enemy;
                enemy.Health.Died += _ => OnKill(captured);
            }
        }

        private void OnKill(EnemyTank enemy)
        {
            if (_directive != Directive.PriorityPurge || enemy == null) return;
            if (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Boss)
                _progress++;
        }

        private void Resolve()
        {
            bool success = false;
            switch (_directive)
            {
                case Directive.EagleGuard:
                    Health eagle = FindEagle();
                    success = eagle != null && !eagle.IsDead && eagle.Current >= _startEagleHp;
                    break;
                case Directive.PriorityPurge:
                    success = _progress >= _target;
                    break;
                case Directive.ArmorHold:
                    PlayerTank p = FindAnyObjectByType<PlayerTank>();
                    success = p != null && p.Health != null && p.Health.Current >= Mathf.Max(2, p.Health.Maximum / 2);
                    break;
            }

            if (!success)
            {
                _result = "STRATEGIC DIRECTIVE FAILED";
                _resultUntil = Time.unscaledTime + 2.2f;
                return;
            }

            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player != null)
            {
                player.AddAmmo(_round >= 60 ? AmmoType.Plasma : _round >= 30 ? AmmoType.EMP : AmmoType.ArmorPiercing, 4 + _round / 20);
                player.Health.Heal(1);
            }
            _game.RepairEagle(1);
            PlayerPrefs.SetInt("TankRevival.StrategicDirectives", PlayerPrefs.GetInt("TankRevival.StrategicDirectives", 0) + 1);
            PlayerPrefs.Save();
            _result = "STRATEGIC DIRECTIVE SECURED // FIELD REWARD DELIVERED";
            _resultUntil = Time.unscaledTime + 3.0f;
        }

        private string Title()
        {
            return _directive switch
            {
                Directive.EagleGuard => "EAGLE GUARD",
                Directive.PriorityPurge => "PRIORITY PURGE",
                Directive.ArmorHold => "STEEL PRESERVATION",
                _ => string.Empty
            };
        }

        private string Detail()
        {
            return _directive switch
            {
                Directive.EagleGuard => "Clear without losing Orzelek HP",
                Directive.PriorityPurge => $"Destroy priority armor [{Mathf.Min(_progress,_target)}/{_target}]",
                Directive.ArmorHold => "Finish with at least 50% tank armor",
                _ => string.Empty
            };
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f,0.78f,0.24f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.68f,0.75f,0.82f) } };
            _success = new GUIStyle(_body) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.42f,1f,0.58f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            if (_directive != Directive.None)
            {
                float y = 244f;
                GUI.color = new Color(0.04f,0.035f,0.02f,0.92f);
                GUI.Box(new Rect(14f,y,430f,67f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(28f,y+7f,400f,20f), "STRATEGIC DIRECTIVE // " + Title(), _header);
                GUI.Label(new Rect(28f,y+29f,400f,18f), Detail(), _body);
                GUI.Label(new Rect(28f,y+48f,400f,16f), "Optional objective: repair + special ammo + Orzelek recovery", _small);
            }
            if (Time.unscaledTime < _resultUntil)
            {
                GUI.color = new Color(0.02f,0.06f,0.04f,0.94f);
                GUI.Box(new Rect(Screen.width*0.5f-330f, Screen.height-165f, 660f, 38f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width*0.5f-320f, Screen.height-157f, 640f, 22f), _result, _success);
            }
        }
    }
}
