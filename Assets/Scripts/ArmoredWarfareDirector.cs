using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Campaign layer for persistent battlefield salvage, field module repair and
    /// anti-armor threat deployment. v11.1 also exposes concise armor-zone/component
    /// state without replacing Health, Projectile or TankGame authority.
    /// </summary>
    public sealed class ArmoredWarfareDirector : MonoBehaviour
    {
        private TankGame _game;
        private PlayerTank _player;
        private ArmorSystem _armor;
        private readonly HashSet<int> _registeredEnemies = new HashSet<int>();
        private readonly HashSet<int> _hunterAgents = new HashSet<int>();
        private int _salvage;
        private int _observedRound = -1;
        private float _nextScan;
        private string _toast = string.Empty;
        private float _toastUntil;
        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _good;
        private GUIStyle _warning;
        private GUIStyle _critical;

        public static ArmoredWarfareDirector Instance { get; private set; }
        public int Salvage => _salvage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ArmoredWarfareDirector>() != null) return;
            var go = new GameObject("ArmoredWarfareDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<ArmoredWarfareDirector>();
        }

        private void Awake()
        {
            Instance = this;
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
                _player = null;
                _armor = null;
                _registeredEnemies.Clear();
                _hunterAgents.Clear();
                _observedRound = -1;
                return;
            }

            if (_observedRound != _game.CurrentRound)
            {
                _observedRound = _game.CurrentRound;
                int roundGrant = 1 + _observedRound / 20;
                _salvage = Mathf.Min(99, _salvage + roundGrant);
                Announce($"ARMORED WARFARE // +{roundGrant} FIELD SALVAGE");
            }

            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerTank>();
                if (_player != null) _armor = _player.GetComponent<ArmorSystem>();
            }

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.65f;
                RegisterEnemies();
            }

            if (Input.GetKeyDown(KeyCode.K))
                FieldRepair();
        }

        private void RegisterEnemies()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            foreach (EnemyTank enemy in enemies)
            {
                if (enemy == null || enemy.Health == null) continue;
                int id = enemy.GetInstanceID();
                if (_registeredEnemies.Add(id))
                {
                    EnemyKind kind = enemy.Kind;
                    enemy.Health.Died += _ => AwardSalvage(kind);
                }

                if (_game.CurrentRound >= 12 && IsHunterCandidate(enemy.Kind) && _hunterAgents.Add(id))
                {
                    var hunter = enemy.GetComponent<ArmorHunterAgent>();
                    if (hunter == null) hunter = enemy.gameObject.AddComponent<ArmorHunterAgent>();
                    hunter.Initialize(_game, enemy, _game.CurrentRound);
                }
            }
        }

        private static bool IsHunterCandidate(EnemyKind kind)
        {
            return kind == EnemyKind.Heavy || kind == EnemyKind.Sniper || kind == EnemyKind.Siege ||
                   kind == EnemyKind.Elite || kind == EnemyKind.Boss;
        }

        private void AwardSalvage(EnemyKind kind)
        {
            int amount = kind switch
            {
                EnemyKind.Boss => 6,
                EnemyKind.Elite => 3,
                EnemyKind.Siege => 3,
                EnemyKind.Heavy => 2,
                EnemyKind.Sniper => 2,
                _ => 1
            };
            _salvage = Mathf.Min(99, _salvage + amount);
        }

        private void FieldRepair()
        {
            if (_player == null || _armor == null)
            {
                Announce("FIELD REPAIR // TANK UNAVAILABLE");
                return;
            }

            int chassis = Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.Garage.Chassis", 0), 0, 3);
            int cost = chassis == 1 ? 3 : 4;
            if (_salvage < cost)
            {
                Announce($"FIELD REPAIR // NEED {cost} SALVAGE");
                return;
            }

            if (_armor.AverageIntegrity >= 98 && _player.Health != null && _player.Health.Current >= _player.Health.Maximum)
            {
                Announce("FIELD REPAIR // SYSTEMS NOMINAL");
                return;
            }

            _salvage -= cost;
            int repaired = _armor.RepairModules(chassis == 1 ? 32 : 25);
            if (_player.Health != null && _player.Health.Current < _player.Health.Maximum)
                _player.Health.Heal(1);
            Announce($"FIELD REPAIR COMPLETE // MODULE RECOVERY {repaired} // -{cost} SALVAGE");
        }

        private void Announce(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 2.1f;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.78f, 0.92f, 1f) }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.83f, 0.88f, 0.92f) }
            };
            _good = new GUIStyle(_body) { normal = { textColor = new Color(0.38f, 1f, 0.56f) } };
            _warning = new GUIStyle(_body) { normal = { textColor = new Color(1f, 0.72f, 0.18f) } };
            _critical = new GUIStyle(_body)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.24f, 0.14f) }
            };
        }

        private GUIStyle IntegrityStyle(int value)
        {
            return value <= ArmorSystem.CriticalThreshold ? _critical : value <= ArmorSystem.DamagedThreshold ? _warning : _good;
        }

        private static string ShortCondition(ModuleCondition condition)
        {
            return condition == ModuleCondition.Disabled ? "DISABLED" :
                   condition == ModuleCondition.Critical ? "CRIT" :
                   condition == ModuleCondition.Damaged ? "DMG" : "OK";
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _armor == null) return;
            EnsureStyles();

            float width = 350f;
            float x = Mathf.Max(10f, Screen.width - width - 14f);
            float y = 12f;
            GUI.color = new Color(0.018f, 0.030f, 0.050f, 0.93f);
            GUI.Box(new Rect(x, y, width, 174f), string.Empty);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 12f, y + 8f, width - 24f, 20f), "ARMORED WARFARE // DAMAGE CONTROL", _title);
            GUI.Label(new Rect(x + 12f, y + 31f, 160f, 18f), $"ENGINE {_armor.EngineIntegrity}% {ShortCondition(_armor.EngineCondition)}", IntegrityStyle(_armor.EngineIntegrity));
            GUI.Label(new Rect(x + 178f, y + 31f, 160f, 18f), $"TRACKS {_armor.TrackIntegrity}% {ShortCondition(_armor.TrackCondition)}", IntegrityStyle(_armor.TrackIntegrity));
            GUI.Label(new Rect(x + 12f, y + 53f, 160f, 18f), $"GUN    {_armor.GunIntegrity}% {ShortCondition(_armor.GunCondition)}", IntegrityStyle(_armor.GunIntegrity));
            GUI.Label(new Rect(x + 178f, y + 53f, 160f, 18f), $"AMMO   {_armor.AmmoRackIntegrity}% {ShortCondition(_armor.AmmoRackCondition)}", IntegrityStyle(_armor.AmmoRackIntegrity));
            GUI.Label(new Rect(x + 12f, y + 79f, width - 24f, 18f), $"SALVAGE {_salvage}   //   K FIELD REPAIR", _body);
            GUI.Label(new Rect(x + 12f, y + 101f, width - 24f, 18f), $"MOBILITY {Mathf.RoundToInt(_armor.MobilityMultiplier * 100f)}%   RELOAD {Mathf.RoundToInt(_armor.ReloadMultiplier * 100f)}%", _body);
            GUI.Label(new Rect(x + 12f, y + 123f, width - 24f, 18f), $"LAST ARMOR {_armor.LastZone.ToString().ToUpperInvariant()} // {AmmoDatabase.ShortName(_armor.LastAmmo)}" + (_armor.LastOvermatch ? " // OVERMATCH" : string.Empty), _body);
            if (_armor.LastCritical && _armor.LastDamagedModule != TankModule.None)
                GUI.Label(new Rect(x + 12f, y + 145f, width - 24f, 18f), $"LAST CRITICAL // {_armor.LastDamagedModule.ToString().ToUpperInvariant()}", _critical);
            else if (_armor.IsMobilityKilled || _armor.IsWeaponDisabled)
                GUI.Label(new Rect(x + 12f, y + 145f, width - 24f, 18f), _armor.IsMobilityKilled ? "MOBILITY KILL // FIELD REPAIR ADVISED" : "WEAPON SYSTEM CRITICAL", _critical);

            if (Time.unscaledTime < _toastUntil)
                GUI.Label(new Rect(0f, Screen.height - 92f, Screen.width, 24f), _toast, _warning);
        }
    }
}
