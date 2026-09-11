using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public sealed class OperationOverdriveDirector : MonoBehaviour
    {
        private enum ContractType
        {
            Elimination,
            PriorityTargets,
            ArmorDiscipline,
            TimeAttack,
            BossBreaker
        }

        private static readonly string[] SectorNames =
        {
            "BORDER WATCH", "IRON VALLEY", "ASHEN CROSSROADS", "STORM FRONT", "BLACK RIVER",
            "FROZEN SPEAR", "FORTRESS BELT", "NIGHT OFFENSIVE", "BURNING GATE", "OVERDRIVE ZERO"
        };

        private TankGame _game;
        private int _round;
        private int _sector = -1;
        private float _roundStarted;
        private float _scanAt;
        private int _roundKills;
        private int _priorityKills;
        private int _bossKills;
        private int _campaignKills;
        private int _contractsCompleted;
        private int _medals;
        private int _startingArmor;
        private ContractType _contract;
        private int _target;
        private string _contractTitle = string.Empty;
        private string _contractDetail = string.Empty;
        private string _banner = string.Empty;
        private float _bannerUntil;
        private string _debrief = string.Empty;
        private float _debriefUntil;
        private readonly HashSet<int> _hookedEnemies = new HashSet<int>();

        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _success;
        private GUIStyle _bannerStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<OperationOverdriveDirector>() != null) return;
            var go = new GameObject("OperationOverdriveDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<OperationOverdriveDirector>();
        }

        private void Awake()
        {
            _medals = PlayerPrefs.GetInt("TankRevival.OverdriveMedals", 0);
            _contractsCompleted = PlayerPrefs.GetInt("TankRevival.ContractsCompleted", 0);
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
                if (_round > 0) ResolvePreviousRound();
                BeginRound(current);
            }

            if (Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.35f;
                HookEnemies();
                UpdateContractText();
            }
        }

        private void BeginRound(int round)
        {
            _round = round;
            _roundStarted = Time.time;
            _roundKills = 0;
            _priorityKills = 0;
            _bossKills = 0;
            _hookedEnemies.Clear();

            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            _startingArmor = player != null && player.Health != null ? player.Health.Current : 3;

            int newSector = Mathf.Clamp((round - 1) / 10, 0, 9);
            if (newSector != _sector)
            {
                _sector = newSector;
                _banner = $"SECTOR {_sector + 1:00} // {SectorNames[_sector]}";
                _bannerUntil = Time.unscaledTime + 4.2f;
                int best = PlayerPrefs.GetInt("TankRevival.BestSector", 0);
                if (_sector + 1 > best)
                {
                    PlayerPrefs.SetInt("TankRevival.BestSector", _sector + 1);
                    PlayerPrefs.Save();
                }
            }

            SelectContract(round);
        }

        private void SelectContract(int round)
        {
            if (round % 10 == 0)
            {
                _contract = ContractType.BossBreaker;
                _target = 1;
                _contractTitle = "BOSS BREAKER";
                _contractDetail = "Destroy the sector boss";
                return;
            }

            switch (round % 4)
            {
                case 0:
                    _contract = ContractType.Elimination;
                    _target = Mathf.Clamp(6 + round / 8, 7, 16);
                    _contractTitle = "SHOCK ACTION";
                    _contractDetail = $"Destroy {_target} enemy vehicles";
                    break;
                case 1:
                    _contract = ContractType.PriorityTargets;
                    _target = Mathf.Clamp(2 + round / 25, 2, 5);
                    _contractTitle = "HEADHUNTER";
                    _contractDetail = $"Destroy {_target} Heavy / Siege / Elite targets";
                    break;
                case 2:
                    _contract = ContractType.ArmorDiscipline;
                    _target = Mathf.Max(1, _startingArmor - 1);
                    _contractTitle = "STEEL DISCIPLINE";
                    _contractDetail = $"Clear the round with at least {_target} armor";
                    break;
                default:
                    _contract = ContractType.TimeAttack;
                    _target = Mathf.Clamp(88 - round / 2, 42, 84);
                    _contractTitle = "BLITZ CLOCK";
                    _contractDetail = $"Clear the round within {_target} seconds";
                    break;
            }
        }

        private void HookEnemies()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            foreach (EnemyTank enemy in enemies)
            {
                if (enemy == null || enemy.Health == null) continue;
                int id = enemy.GetInstanceID();
                if (!_hookedEnemies.Add(id)) continue;
                EnemyTank captured = enemy;
                enemy.Health.Died += _ => RegisterKill(captured);
            }
        }

        private void RegisterKill(EnemyTank enemy)
        {
            if (enemy == null) return;
            _roundKills++;
            _campaignKills++;
            if (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Elite)
                _priorityKills++;
            if (enemy.Kind == EnemyKind.Boss)
                _bossKills++;
        }

        private void ResolvePreviousRound()
        {
            bool success = ContractSucceeded();
            float elapsed = Mathf.Max(0f, Time.time - _roundStarted);

            if (success)
            {
                _contractsCompleted++;
                _medals++;
                GrantReward();
                PlayerPrefs.SetInt("TankRevival.ContractsCompleted", _contractsCompleted);
                PlayerPrefs.SetInt("TankRevival.OverdriveMedals", _medals);
                PlayerPrefs.SetInt("TankRevival.CampaignKills", PlayerPrefs.GetInt("TankRevival.CampaignKills", 0) + _roundKills);
                PlayerPrefs.Save();
                _debrief = $"ROUND {_round:000} COMPLETE  //  CONTRACT SECURED  //  +1 MEDAL  //  {_roundKills} KILLS  //  {elapsed:0}s";
            }
            else
            {
                _debrief = $"ROUND {_round:000} COMPLETE  //  CONTRACT MISSED  //  {_roundKills} KILLS  //  {elapsed:0}s";
            }

            _debriefUntil = Time.unscaledTime + 3.4f;
        }

        private bool ContractSucceeded()
        {
            switch (_contract)
            {
                case ContractType.Elimination:
                    return _roundKills >= _target;
                case ContractType.PriorityTargets:
                    return _priorityKills >= _target;
                case ContractType.BossBreaker:
                    return _bossKills >= 1;
                case ContractType.TimeAttack:
                    return Time.time - _roundStarted <= _target;
                case ContractType.ArmorDiscipline:
                    PlayerTank player = FindAnyObjectByType<PlayerTank>();
                    return player != null && player.Health != null && player.Health.Current >= _target;
                default:
                    return false;
            }
        }

        private void GrantReward()
        {
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player == null) return;

            int cycle = _contractsCompleted % 5;
            if (cycle == 0)
            {
                player.ApplyPowerUp(PowerUpKind.PowerShot);
                AnnounceReward("COMMAND REWARD // CANNON POWER");
            }
            else if (cycle == 1)
            {
                player.AddAmmo(AmmoType.ArmorPiercing, 8 + _sector);
                AnnounceReward("COMMAND REWARD // AP RESUPPLY");
            }
            else if (cycle == 2)
            {
                player.ApplyPowerUp(PowerUpKind.Repair);
                AnnounceReward("COMMAND REWARD // FIELD REPAIR");
            }
            else if (cycle == 3)
            {
                AmmoType ammo = _round >= 50 ? AmmoType.Plasma : _round >= 25 ? AmmoType.EMP : AmmoType.Explosive;
                player.AddAmmo(ammo, 5 + _sector / 2);
                AnnounceReward("COMMAND REWARD // SPECIAL AMMUNITION");
            }
            else
            {
                player.ApplyPowerUp(PowerUpKind.RapidFire);
                AnnounceReward("COMMAND REWARD // AUTOLOADER");
            }
        }

        private void AnnounceReward(string text)
        {
            _banner = text;
            _bannerUntil = Time.unscaledTime + 2.5f;
        }

        private void UpdateContractText()
        {
            switch (_contract)
            {
                case ContractType.Elimination:
                    _contractDetail = $"Destroy {_target} enemy vehicles  [{Mathf.Min(_roundKills, _target)}/{_target}]";
                    break;
                case ContractType.PriorityTargets:
                    _contractDetail = $"Heavy / Siege / Elite targets  [{Mathf.Min(_priorityKills, _target)}/{_target}]";
                    break;
                case ContractType.BossBreaker:
                    _contractDetail = $"Destroy the sector boss  [{Mathf.Min(_bossKills, 1)}/1]";
                    break;
                case ContractType.TimeAttack:
                    _contractDetail = $"Clear within {_target}s  [{Mathf.Max(0, _target - Mathf.FloorToInt(Time.time - _roundStarted))}s]";
                    break;
                case ContractType.ArmorDiscipline:
                    PlayerTank player = FindAnyObjectByType<PlayerTank>();
                    int hp = player != null && player.Health != null ? player.Health.Current : 0;
                    _contractDetail = $"Finish with at least {_target} armor  [ARMOR {hp}]";
                    break;
            }
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.36f, 0.92f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(0.92f, 0.96f, 1f) } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.62f, 0.72f, 0.80f) } };
            _success = new GUIStyle(_body) { normal = { textColor = new Color(0.42f, 1f, 0.58f) } };
            _bannerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            float progress = Mathf.Clamp01(_round / 100f);
            GUI.color = new Color(0.025f, 0.040f, 0.060f, 0.93f);
            GUI.Box(new Rect(14f, 126f, 430f, 92f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(28f, 133f, 400f, 22f), $"OPERATION OVERDRIVE  //  SECTOR {_sector + 1:00} {SectorNames[Mathf.Clamp(_sector, 0, 9)]}", _header);
            GUI.Label(new Rect(28f, 157f, 400f, 20f), $"CONTRACT: {_contractTitle}", _body);
            GUI.Label(new Rect(28f, 178f, 400f, 18f), _contractDetail, _small);
            GUI.Label(new Rect(28f, 197f, 400f, 18f), $"MEDALS {_medals}   CONTRACTS {_contractsCompleted}   CAMPAIGN KILLS {_campaignKills}", _small);

            GUI.color = new Color(0.08f, 0.12f, 0.16f, 0.95f);
            GUI.Box(new Rect(14f, 222f, 430f, 14f), string.Empty);
            GUI.color = new Color(0.20f, 0.86f, 1f, 0.95f);
            GUI.Box(new Rect(16f, 224f, 426f * progress, 10f), string.Empty);
            GUI.color = Color.white;

            if (Time.unscaledTime < _debriefUntil)
            {
                GUI.color = new Color(0.02f, 0.05f, 0.07f, 0.96f);
                GUI.Box(new Rect(Screen.width * 0.5f - 370f, Screen.height - 115f, 740f, 48f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 355f, Screen.height - 102f, 710f, 24f), _debrief, _success);
            }

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.015f, 0.025f, 0.045f, 0.90f);
                GUI.Box(new Rect(Screen.width * 0.5f - 350f, Screen.height * 0.34f - 30f, 700f, 60f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 340f, Screen.height * 0.34f - 20f, 680f, 40f), _banner, _bannerStyle);
            }
        }
    }
}
