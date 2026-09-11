using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v1.2 WAR GARAGE
    /// Persistent pre-campaign chassis selection and upgrade tree driven by campaign achievements.
    /// Uses the existing PlayerTank commander-upgrade hooks, so garage choices affect real combat
    /// without replacing the proven movement, armor, ammo or progression systems.
    /// </summary>
    public sealed class WarGarageDirector : MonoBehaviour
    {
        private enum Chassis
        {
            Assault = 0,
            Bastion = 1,
            Scout = 2,
            Hunter = 3
        }

        private static readonly string[] Names = { "ORZEL MK-I ASSAULT", "BASTION HEAVY", "WICHER SCOUT", "HUNTER TD" };
        private static readonly string[] Roles =
        {
            "Balanced assault / cannon + loader",
            "Heavy survival / maximum armor",
            "Fast maneuver / engine + loader",
            "Tank destroyer / maximum cannon"
        };

        private TankGame _game;
        private PlayerTank _lastPlayer;
        private int _observedRound = -1;
        private bool _roundAmmoGranted;
        private Chassis _selected;

        private int _cannon;
        private int _loader;
        private int _engine;
        private int _armor;

        private string _toast = string.Empty;
        private float _toastUntil;

        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _selectedStyle;
        private GUIStyle _lockedStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<WarGarageDirector>() != null) return;
            var go = new GameObject("WarGarageDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<WarGarageDirector>();
        }

        private void Awake()
        {
            _selected = (Chassis)Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.Garage.Chassis", 0), 0, 3);
            _cannon = Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.Garage.Cannon", 0), 0, 2);
            _loader = Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.Garage.Loader", 0), 0, 2);
            _engine = Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.Garage.Engine", 0), 0, 2);
            _armor = Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.Garage.Armor", 0), 0, 2);
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
                HandleGarageInput();
                _lastPlayer = null;
                _observedRound = -1;
                _roundAmmoGranted = false;
                return;
            }

            int round = _game.CurrentRound;
            if (round != _observedRound)
            {
                _observedRound = round;
                _roundAmmoGranted = false;
            }

            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player != null && player != _lastPlayer)
            {
                _lastPlayer = player;
                ApplyGarageBuild(player);
            }
        }

        private int LifetimeMarks()
        {
            int contracts = PlayerPrefs.GetInt("TankRevival.ContractsCompleted", 0);
            int legends = PlayerPrefs.GetInt("TankRevival.BossLegendsDefeated", 0);
            int sector = PlayerPrefs.GetInt("TankRevival.BestSector", 0);
            return Mathf.Max(0, contracts + legends * 3 + sector * 2);
        }

        private int UpgradeCost(int level)
        {
            return level <= 0 ? 2 : 4;
        }

        private int SpentMarks()
        {
            return SpendFor(_cannon) + SpendFor(_loader) + SpendFor(_engine) + SpendFor(_armor);
        }

        private static int SpendFor(int level)
        {
            if (level <= 0) return 0;
            if (level == 1) return 2;
            return 6;
        }

        private int AvailableMarks => Mathf.Max(0, LifetimeMarks() - SpentMarks());

        private bool Unlocked(Chassis chassis)
        {
            int marks = LifetimeMarks();
            switch (chassis)
            {
                case Chassis.Assault: return true;
                case Chassis.Bastion: return marks >= 4;
                case Chassis.Scout: return marks >= 8;
                case Chassis.Hunter: return marks >= 14;
                default: return false;
            }
        }

        private void HandleGarageInput()
        {
            if (Input.GetKeyDown(KeyCode.F1)) SelectChassis(Chassis.Assault);
            if (Input.GetKeyDown(KeyCode.F2)) SelectChassis(Chassis.Bastion);
            if (Input.GetKeyDown(KeyCode.F3)) SelectChassis(Chassis.Scout);
            if (Input.GetKeyDown(KeyCode.F4)) SelectChassis(Chassis.Hunter);

            if (Input.GetKeyDown(KeyCode.F5)) BuyUpgrade("CANNON", ref _cannon, "TankRevival.Garage.Cannon");
            if (Input.GetKeyDown(KeyCode.F6)) BuyUpgrade("AUTOLOADER", ref _loader, "TankRevival.Garage.Loader");
            if (Input.GetKeyDown(KeyCode.F7)) BuyUpgrade("ENGINE", ref _engine, "TankRevival.Garage.Engine");
            if (Input.GetKeyDown(KeyCode.F8)) BuyUpgrade("ARMOR", ref _armor, "TankRevival.Garage.Armor");
        }

        private void SelectChassis(Chassis chassis)
        {
            if (!Unlocked(chassis))
            {
                Announce("CHASSIS LOCKED // EARN MORE GARAGE MARKS");
                return;
            }

            _selected = chassis;
            PlayerPrefs.SetInt("TankRevival.Garage.Chassis", (int)_selected);
            PlayerPrefs.Save();
            Announce($"ACTIVE CHASSIS // {Names[(int)_selected]}");
        }

        private void BuyUpgrade(string label, ref int level, string key)
        {
            if (level >= 2)
            {
                Announce(label + " // MAX LEVEL");
                return;
            }

            int cost = UpgradeCost(level);
            if (AvailableMarks < cost)
            {
                Announce($"{label} // NEED {cost} MARKS");
                return;
            }

            level++;
            PlayerPrefs.SetInt(key, level);
            PlayerPrefs.Save();
            Announce($"{label} UPGRADED // LEVEL {level}");
        }

        private void ApplyGarageBuild(PlayerTank player)
        {
            int cannon = _cannon;
            int loader = _loader;
            int engine = _engine;
            int armor = _armor;

            switch (_selected)
            {
                case Chassis.Assault:
                    cannon += 1;
                    loader += 1;
                    break;
                case Chassis.Bastion:
                    armor += 2;
                    cannon += 1;
                    break;
                case Chassis.Scout:
                    engine += 2;
                    loader += 1;
                    break;
                case Chassis.Hunter:
                    cannon += 2;
                    engine += 1;
                    break;
            }

            player.SetCommanderUpgrades(cannon, loader, engine, armor);

            if (!_roundAmmoGranted)
            {
                _roundAmmoGranted = true;
                switch (_selected)
                {
                    case Chassis.Assault:
                        player.AddAmmo(AmmoType.ArmorPiercing, 3);
                        break;
                    case Chassis.Bastion:
                        player.AddAmmo(AmmoType.Explosive, 2);
                        break;
                    case Chassis.Scout:
                        player.AddAmmo(AmmoType.EMP, 2);
                        break;
                    case Chassis.Hunter:
                        player.AddAmmo(AmmoType.ArmorPiercing, 5);
                        break;
                }
            }
        }

        private void Announce(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 2.2f;
        }

        private string UnlockText(Chassis chassis)
        {
            if (Unlocked(chassis)) return "READY";
            int need = chassis == Chassis.Bastion ? 4 : chassis == Chassis.Scout ? 8 : 14;
            return $"LOCKED @ {need} MARKS";
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 0.92f, 1f) }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.90f, 0.95f, 1f) }
            };
            _small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.62f, 0.72f, 0.80f) }
            };
            _selectedStyle = new GUIStyle(_body)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.40f, 1f, 0.58f) }
            };
            _lockedStyle = new GUIStyle(_body)
            {
                normal = { textColor = new Color(0.58f, 0.42f, 0.42f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || _game.IsPlaying) return;
            EnsureStyles();

            float x = Mathf.Max(12f, Screen.width - 520f);
            float y = 18f;
            GUI.color = new Color(0.018f, 0.030f, 0.050f, 0.95f);
            GUI.Box(new Rect(x, y, 500f, 360f), string.Empty);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 18f, y + 12f, 460f, 28f), "WAR GARAGE // PERSISTENT ARMORY", _title);
            GUI.Label(new Rect(x + 18f, y + 42f, 460f, 20f), $"GARAGE MARKS: {AvailableMarks} AVAILABLE / {LifetimeMarks()} EARNED", _body);
            GUI.Label(new Rect(x + 18f, y + 63f, 460f, 18f), "Earn marks from contracts, sector progress and defeated Boss Legends.", _small);

            float rowY = y + 92f;
            for (int i = 0; i < 4; i++)
            {
                Chassis chassis = (Chassis)i;
                bool unlocked = Unlocked(chassis);
                bool selected = _selected == chassis;
                GUIStyle style = selected ? _selectedStyle : unlocked ? _body : _lockedStyle;
                string prefix = selected ? ">" : " ";
                GUI.Label(new Rect(x + 18f, rowY + i * 39f, 460f, 19f), $"{prefix} F{i + 1}  {Names[i]}  //  {UnlockText(chassis)}", style);
                GUI.Label(new Rect(x + 42f, rowY + 18f + i * 39f, 430f, 17f), Roles[i], _small);
            }

            float upgradeY = y + 258f;
            GUI.Label(new Rect(x + 18f, upgradeY, 460f, 18f), "PERMANENT UPGRADE TREE", _body);
            GUI.Label(new Rect(x + 18f, upgradeY + 23f, 460f, 18f), $"F5 Cannon {_cannon}/2   F6 Loader {_loader}/2   F7 Engine {_engine}/2   F8 Armor {_armor}/2", _small);
            GUI.Label(new Rect(x + 18f, upgradeY + 43f, 460f, 18f), "Next upgrade costs 2 marks at L0, then 4 marks at L1.", _small);
            GUI.Label(new Rect(x + 18f, upgradeY + 66f, 460f, 18f), $"ACTIVE BUILD: {Names[(int)_selected]}   //   ENTER or SPACE starts campaign", _selectedStyle);

            if (Time.unscaledTime < _toastUntil)
            {
                GUI.color = new Color(0.02f, 0.08f, 0.10f, 0.94f);
                GUI.Box(new Rect(x, y + 370f, 500f, 34f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 14f, y + 378f, 470f, 20f), _toast, _selectedStyle);
            }
        }
    }
}
