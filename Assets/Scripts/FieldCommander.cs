using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum CommandUpgrade
    {
        Cannon,
        Autoloader,
        Engine,
        Armor,
        Sentry,
        Logistics
    }

    public sealed class FieldCommander : MonoBehaviour
    {
        public static FieldCommander Instance { get; private set; }
        public static bool CommandCenterOpen => Instance != null && Instance._shopOpen;

        private TankGame _game;
        private bool _campaignActive;
        private bool _sawCampaignEnd;
        private bool _shopOpen;
        private int _shopAfterRound;
        private int _lastRound;
        private int _warBonds;
        private int _logisticsGrantedRound = -1;
        private float _nextEnemyScan;

        private int _cannonLevel;
        private int _loaderLevel;
        private int _engineLevel;
        private int _armorLevel;
        private int _sentryLevel;
        private int _logisticsLevel;

        private PlayerTank _lastAppliedPlayer;
        private PlayerTank _shopDisabledPlayer;
        private WaveDoctrineProfile _doctrine;
        private readonly HashSet<int> _hookedEnemies = new HashSet<int>();
        private readonly List<GameObject> _sentries = new List<GameObject>();

        private GUIStyle _titleStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _accentStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCommanderExists()
        {
            if (FindAnyObjectByType<FieldCommander>() != null) return;
            var go = new GameObject("FieldCommander");
            go.AddComponent<FieldCommander>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (_game == null)
                _game = FindAnyObjectByType<TankGame>();
            if (_game == null) return;

            if (!_game.IsPlaying)
            {
                if (_campaignActive && Time.timeScale > 0.01f)
                    _sawCampaignEnd = true;
                return;
            }

            if (!_campaignActive || (_sawCampaignEnd && _game.CurrentRound == 1))
                ResetCampaign();

            _sawCampaignEnd = false;

            if (_game.CurrentRound != _lastRound)
                OnRoundStarted(_game.CurrentRound);

            ApplyUpgradesToCurrentPlayer();

            if (Time.unscaledTime >= _nextEnemyScan)
            {
                HookNewEnemies();
                _nextEnemyScan = Time.unscaledTime + 0.22f;
            }

            if (_shopOpen)
                HandleShopInput();
        }

        private void ResetCampaign()
        {
            CloseShop(false);
            DestroySentries();
            _campaignActive = true;
            _sawCampaignEnd = false;
            _lastRound = 0;
            _warBonds = 5;
            _logisticsGrantedRound = -1;
            _cannonLevel = 0;
            _loaderLevel = 0;
            _engineLevel = 0;
            _armorLevel = 0;
            _sentryLevel = 0;
            _logisticsLevel = 0;
            _lastAppliedPlayer = null;
            _hookedEnemies.Clear();
            _doctrine = WaveDoctrineDirector.Create(1);
        }

        private void OnRoundStarted(int round)
        {
            if (_lastRound > 0 && round > _lastRound)
                AwardRoundClear(_lastRound);

            _lastRound = round;
            _doctrine = WaveDoctrineDirector.Create(round);
            _lastAppliedPlayer = null;
            _logisticsGrantedRound = -1;
            DestroySentries();
            RebuildSentries();

            if (round > 1 && (round - 1) % 5 == 0)
                OpenShop(round - 1);
        }

        private void AwardRoundClear(int clearedRound)
        {
            int bonus = 3 + clearedRound / 3;
            if (clearedRound % 10 == 0) bonus += 12;
            _warBonds += bonus;
        }

        private void HookNewEnemies()
        {
            var enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null) continue;
                int id = enemy.GetInstanceID();
                if (!_hookedEnemies.Add(id)) continue;

                EnemyTank captured = enemy;
                enemy.Health.Died += _ => AwardEnemySalvage(captured);

                if (enemy.GetComponent<DoctrineAugment>() == null)
                {
                    var augment = enemy.gameObject.AddComponent<DoctrineAugment>();
                    augment.Initialize(_game, enemy, _doctrine);
                }
            }
        }

        private void AwardEnemySalvage(EnemyTank enemy)
        {
            if (enemy == null) return;

            int baseBonds = enemy.Kind switch
            {
                EnemyKind.Fast => 2,
                EnemyKind.Heavy => 3,
                EnemyKind.Sniper => 3,
                EnemyKind.Siege => 4,
                EnemyKind.Elite => 6,
                EnemyKind.Supply => 5,
                EnemyKind.Boss => 18 + Mathf.Max(1, _lastRound / 10),
                _ => 1
            };

            float logistics = 1f + _logisticsLevel * 0.12f;
            _warBonds += Mathf.Max(1, Mathf.RoundToInt(baseBonds * _doctrine.SalvageMultiplier * logistics));
        }

        private void ApplyUpgradesToCurrentPlayer()
        {
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player == null) return;

            if (_lastAppliedPlayer != player)
            {
                _lastAppliedPlayer = player;
                player.SetCommanderUpgrades(_cannonLevel, _loaderLevel, _engineLevel, _armorLevel);
            }
            else
            {
                player.SetCommanderUpgrades(_cannonLevel, _loaderLevel, _engineLevel, _armorLevel);
            }

            if (_shopOpen && player.enabled)
            {
                _shopDisabledPlayer = player;
                player.enabled = false;
                BattleAudio.Instance?.SetEngineMoving(false, 0f);
            }

            if (!_shopOpen && _logisticsLevel > 0 && _logisticsGrantedRound != _lastRound)
            {
                GrantLogisticsPackage(player);
                _logisticsGrantedRound = _lastRound;
            }
        }

        private void GrantLogisticsPackage(PlayerTank player)
        {
            if (player == null || _logisticsLevel <= 0) return;

            player.AddAmmo(AmmoType.ArmorPiercing, 2 + _logisticsLevel);
            if (_logisticsLevel >= 2)
                player.AddAmmo(AmmoType.Explosive, 1 + _logisticsLevel / 2);
            if (_logisticsLevel >= 3)
                player.AddAmmo(AmmoType.EMP, 1);
            if (_logisticsLevel >= 4)
                player.AddAmmo(AmmoType.Plasma, 1);
        }

        private void OpenShop(int clearedRound)
        {
            if (_shopOpen) return;
            _shopOpen = true;
            _shopAfterRound = clearedRound;
            Time.timeScale = 0f;
            BattleAudio.Instance?.SetEngineMoving(false, 0f);

            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player != null)
            {
                _shopDisabledPlayer = player;
                player.enabled = false;
            }
        }

        private void CloseShop(bool resume)
        {
            if (_shopDisabledPlayer != null)
                _shopDisabledPlayer.enabled = true;
            _shopDisabledPlayer = null;
            _shopOpen = false;
            if (resume) Time.timeScale = 1f;
        }

        private void HandleShopInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) TryBuy(CommandUpgrade.Cannon);
            if (Input.GetKeyDown(KeyCode.Alpha2)) TryBuy(CommandUpgrade.Autoloader);
            if (Input.GetKeyDown(KeyCode.Alpha3)) TryBuy(CommandUpgrade.Engine);
            if (Input.GetKeyDown(KeyCode.Alpha4)) TryBuy(CommandUpgrade.Armor);
            if (Input.GetKeyDown(KeyCode.Alpha5)) TryBuy(CommandUpgrade.Sentry);
            if (Input.GetKeyDown(KeyCode.Alpha6)) TryBuy(CommandUpgrade.Logistics);
            if (Input.GetKeyDown(KeyCode.Alpha7)) RepairStronghold();

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                CloseShop(true);
        }

        private int GetLevel(CommandUpgrade upgrade)
        {
            return upgrade switch
            {
                CommandUpgrade.Cannon => _cannonLevel,
                CommandUpgrade.Autoloader => _loaderLevel,
                CommandUpgrade.Engine => _engineLevel,
                CommandUpgrade.Armor => _armorLevel,
                CommandUpgrade.Sentry => _sentryLevel,
                _ => _logisticsLevel
            };
        }

        private int GetMaxLevel(CommandUpgrade upgrade)
        {
            return upgrade switch
            {
                CommandUpgrade.Cannon => 3,
                CommandUpgrade.Sentry => 3,
                _ => 4
            };
        }

        private int GetCost(CommandUpgrade upgrade)
        {
            int level = GetLevel(upgrade);
            int baseCost = upgrade switch
            {
                CommandUpgrade.Cannon => 18,
                CommandUpgrade.Autoloader => 14,
                CommandUpgrade.Engine => 11,
                CommandUpgrade.Armor => 16,
                CommandUpgrade.Sentry => 20,
                _ => 13
            };
            int step = upgrade switch
            {
                CommandUpgrade.Cannon => 15,
                CommandUpgrade.Sentry => 18,
                CommandUpgrade.Armor => 12,
                _ => 9
            };
            return baseCost + level * step;
        }

        private void TryBuy(CommandUpgrade upgrade)
        {
            int level = GetLevel(upgrade);
            if (level >= GetMaxLevel(upgrade)) return;

            int cost = GetCost(upgrade);
            if (_warBonds < cost)
            {
                BattleAudio.PlayGlobal(SoundCue.EnemyShot, 0.08f, 0f);
                return;
            }

            _warBonds -= cost;
            switch (upgrade)
            {
                case CommandUpgrade.Cannon: _cannonLevel++; break;
                case CommandUpgrade.Autoloader: _loaderLevel++; break;
                case CommandUpgrade.Engine: _engineLevel++; break;
                case CommandUpgrade.Armor: _armorLevel++; break;
                case CommandUpgrade.Sentry: _sentryLevel++; break;
                case CommandUpgrade.Logistics: _logisticsLevel++; break;
            }

            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.70f, 0f);
            _lastAppliedPlayer = null;
            ApplyUpgradesToCurrentPlayer();
            RebuildSentries();
        }

        private void RepairStronghold()
        {
            const int repairCost = 9;
            if (_warBonds < repairCost) return;
            _warBonds -= repairCost;
            _game.RepairEagle(2);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.65f, 0f);
        }

        private void DestroySentries()
        {
            for (int i = 0; i < _sentries.Count; i++)
            {
                if (_sentries[i] != null)
                    Destroy(_sentries[i]);
            }
            _sentries.Clear();
        }

        private void RebuildSentries()
        {
            DestroySentries();
            if (_sentryLevel <= 0 || _game == null || !_campaignActive) return;

            Vector2 basePos = _game.BasePosition;
            int count = _sentryLevel >= 3 ? 3 : _sentryLevel;
            Vector2[] offsets =
            {
                new Vector2(-2.05f, 0.55f),
                new Vector2(2.05f, 0.55f),
                new Vector2(0f, 1.65f)
            };

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("EagleSentry_L" + _sentryLevel + "_" + i);
                go.transform.position = basePos + offsets[i];
                var turret = go.AddComponent<DefenseTurret>();
                turret.Initialize(_game, _sentryLevel, i);
                _sentries.Add(go);
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.28f, 0.92f, 1f) }
            };
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _rowStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 15,
                normal = { textColor = new Color(0.82f, 0.90f, 0.96f) }
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                normal = { textColor = new Color(0.62f, 0.72f, 0.80f) }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            _accentStyle = new GUIStyle(_headerStyle)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(1f, 0.78f, 0.18f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_campaignActive) return;
            EnsureStyles();

            DrawDoctrineHud();
            if (_shopOpen)
                DrawCommandCenter();
        }

        private void DrawDoctrineHud()
        {
            if (!_game.IsPlaying && !_shopOpen) return;

            float width = 310f;
            Rect panel = new Rect(Screen.width - width - 14f, 12f, width, 86f);
            GUI.color = new Color(0.035f, 0.050f, 0.072f, 0.94f);
            GUI.Box(panel, string.Empty);
            GUI.color = Color.white;

            Color old = GUI.contentColor;
            GUI.contentColor = _doctrine.Color;
            GUI.Label(new Rect(panel.x + 14f, panel.y + 8f, panel.width - 28f, 24f), _doctrine.Name, _headerStyle);
            GUI.contentColor = old;
            GUI.Label(new Rect(panel.x + 14f, panel.y + 33f, panel.width - 28f, 22f), _doctrine.Description, _smallStyle);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 58f, panel.width - 28f, 20f), "WAR BONDS  " + _warBonds, _accentStyle);
        }

        private void DrawCommandCenter()
        {
            float width = Mathf.Min(900f, Screen.width - 50f);
            float height = Mathf.Min(620f, Screen.height - 40f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.color = new Color(0.025f, 0.038f, 0.060f, 0.985f);
            GUI.Box(panel, string.Empty);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x, panel.y + 18f, panel.width, 44f), "FIELD COMMAND CENTER", _titleStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 67f, panel.width - 56f, 26f), $"ROUND {_shopAfterRound:000} SECURED    •    WAR BONDS {_warBonds}", _headerStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 94f, panel.width - 56f, 24f), "Buy permanent campaign upgrades. Keys 1–7 work here. ENTER / SPACE deploys to the next assault.", _smallStyle);

            float y = panel.y + 132f;
            DrawUpgradeRow(panel, ref y, 1, CommandUpgrade.Cannon, "CANNON CALIBRATION", "+1 effective shell damage and higher muzzle velocity");
            DrawUpgradeRow(panel, ref y, 2, CommandUpgrade.Autoloader, "AUTOLOADER", "faster reload cycle; stacks with field rapid-fire upgrades");
            DrawUpgradeRow(panel, ref y, 3, CommandUpgrade.Engine, "ENGINE TUNING", "higher effective movement speed and response");
            DrawUpgradeRow(panel, ref y, 4, CommandUpgrade.Armor, "COMPOSITE ARMOR", "+1 maximum player armor per level");
            DrawUpgradeRow(panel, ref y, 5, CommandUpgrade.Sentry, "EAGLE SENTRY NETWORK", "deploy 1–3 autonomous defense cannons around Orzełek");
            DrawUpgradeRow(panel, ref y, 6, CommandUpgrade.Logistics, "LOGISTICS & SALVAGE", "free special ammo each round + higher War Bond recovery");

            int repairCost = 9;
            Rect repairButton = new Rect(panel.x + 28f, y + 6f, 150f, 34f);
            if (GUI.Button(repairButton, $"7  REPAIR [{repairCost}]", _buttonStyle))
                RepairStronghold();
            GUI.Label(new Rect(panel.x + 192f, y + 6f, panel.width - 220f, 34f), "Field engineers restore +2 HP to the Orzełek stronghold", _rowStyle);

            GUI.Label(new Rect(panel.x + 28f, panel.yMax - 54f, panel.width - 56f, 30f), "ENTER / SPACE  →  DEPLOY", _headerStyle);
        }

        private void DrawUpgradeRow(Rect panel, ref float y, int hotkey, CommandUpgrade upgrade, string title, string description)
        {
            int level = GetLevel(upgrade);
            int max = GetMaxLevel(upgrade);
            int cost = GetCost(upgrade);
            bool capped = level >= max;
            string costText = capped ? "MAX" : cost.ToString();

            Rect button = new Rect(panel.x + 28f, y, 150f, 38f);
            if (GUI.Button(button, $"{hotkey}  {costText}", _buttonStyle) && !capped)
                TryBuy(upgrade);

            GUI.Label(new Rect(panel.x + 194f, y - 2f, panel.width - 222f, 23f), $"{title}    LV {level}/{max}", _headerStyle);
            GUI.Label(new Rect(panel.x + 194f, y + 19f, panel.width - 222f, 22f), description, _smallStyle);
            y += 57f;
        }
    }
}
