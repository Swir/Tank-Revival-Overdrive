using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum FortressDoctrine
    {
        Bastion,
        HunterGrid,
        Recovery
    }

    /// <summary>
    /// v2.9 ORZEL FORTRESS COMMAND.
    /// Converts the existing Eagle Fortress + field engineering systems into an active defensive
    /// command layer. The player selects a doctrine per sector, spends Command Charges on upgrades
    /// and emergency actions, and receives bonuses that feed the existing sentry/relay/engineering
    /// systems instead of replacing them.
    /// </summary>
    public sealed class EagleFortressCommandDirector : MonoBehaviour
    {
        public static EagleFortressCommandDirector Instance { get; private set; }

        public FortressDoctrine Doctrine { get; private set; } = FortressDoctrine.Bastion;
        public int DoctrineLevel { get; private set; } = 1;
        public int CommandCharges => _charges;
        public int Sector => _sector;

        public int FortressModuleBonus => Doctrine == FortressDoctrine.Bastion ? DoctrineLevel : Mathf.Max(0, DoctrineLevel - 2);
        public int SentryDamageBonus => Doctrine == FortressDoctrine.HunterGrid ? Mathf.Max(0, DoctrineLevel - 1) : 0;
        public float SentryFireRateMultiplier => Doctrine == FortressDoctrine.HunterGrid ? Mathf.Lerp(0.92f, 0.68f, (DoctrineLevel - 1f) / 3f) : 1f;
        public float SentryRangeBonus => Doctrine == FortressDoctrine.HunterGrid ? 0.55f * DoctrineLevel : 0f;
        public int RepairBonus => Doctrine == FortressDoctrine.Recovery ? Mathf.Max(0, DoctrineLevel - 1) : 0;
        public float RepairIntervalMultiplier => Doctrine == FortressDoctrine.Recovery ? Mathf.Lerp(0.92f, 0.62f, (DoctrineLevel - 1f) / 3f) : 1f;
        public int EngineeringPartsBonus => Doctrine == FortressDoctrine.Recovery ? DoctrineLevel : Doctrine == FortressDoctrine.Bastion ? Mathf.Max(0, DoctrineLevel - 2) : 0;

        private TankGame _game;
        private Health _eagle;
        private int _round = -1;
        private int _sector = -1;
        private int _charges;
        private bool _doctrineLocked;
        private int _lastEagleHealth = -1;
        private bool _sectorPerfect = true;
        private float _nextCommandAt;
        private string _toast = string.Empty;
        private float _toastUntil;
        private readonly List<GameObject> _fieldNodes = new List<GameObject>();

        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _accent;
        private GUIStyle _warn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EagleFortressCommandDirector>() != null) return;
            var go = new GameObject("EagleFortressCommandDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<EagleFortressCommandDirector>();
        }

        private void Awake()
        {
            Instance = this;
            _charges = Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.Fortress.Charges", 2), 0, 12);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying) return;
            AcquireEagle();
            HandleRoundTransition();
            if (Time.time < _nextCommandAt) return;

            if (!_doctrineLocked)
            {
                if (Input.GetKeyDown(KeyCode.F1)) SelectDoctrine(FortressDoctrine.Bastion);
                if (Input.GetKeyDown(KeyCode.F2)) SelectDoctrine(FortressDoctrine.HunterGrid);
                if (Input.GetKeyDown(KeyCode.F3)) SelectDoctrine(FortressDoctrine.Recovery);
            }

            if (Input.GetKeyDown(KeyCode.F4)) UpgradeDoctrine();
            if (Input.GetKeyDown(KeyCode.F5)) ActivateAegis();
            if (Input.GetKeyDown(KeyCode.F6)) ActivateCounterBattery();
            if (Input.GetKeyDown(KeyCode.F7)) ActivateEngineerSurge();
        }

        private void AcquireEagle()
        {
            if (_eagle != null && !_eagle.IsDead) return;
            GameObject eagleObject = GameObject.Find("ORZELEK_DEFENSE_CORE");
            if (eagleObject == null) return;
            _eagle = eagleObject.GetComponent<Health>();
            if (_eagle == null) return;
            _lastEagleHealth = _eagle.Current;
            _eagle.Damaged -= OnEagleDamaged;
            _eagle.Damaged += OnEagleDamaged;
        }

        private void HandleRoundTransition()
        {
            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (_round == round) return;

            int nextSector = Mathf.Clamp((round - 1) / 10 + 1, 1, 10);
            bool sectorChanged = nextSector != _sector;
            if (sectorChanged)
            {
                if (_sector > 0 && _sectorPerfect)
                {
                    AddCharges(2);
                    Announce("PERFECT SECTOR DEFENSE // +2 COMMAND CHARGES");
                }

                _sector = nextSector;
                DoctrineLevel = 1;
                _doctrineLocked = false;
                _sectorPerfect = true;
                CleanupFieldNodes();
                AddCharges(2 + (_sector >= 6 ? 1 : 0));
                Announce($"SECTOR {_sector:00} FORTRESS BRIEFING // F1 BASTION  F2 HUNTER  F3 RECOVERY");
            }
            else
            {
                AddCharges(round % 5 == 0 ? 2 : 1);
            }

            _round = round;
            if (_eagle != null) _lastEagleHealth = _eagle.Current;
        }

        private void SelectDoctrine(FortressDoctrine doctrine)
        {
            Doctrine = doctrine;
            DoctrineLevel = 1;
            _doctrineLocked = true;
            _nextCommandAt = Time.time + 0.25f;
            string name = doctrine == FortressDoctrine.Bastion ? "BASTION" : doctrine == FortressDoctrine.HunterGrid ? "HUNTER GRID" : "RECOVERY CORPS";
            Announce($"FORTRESS DOCTRINE LOCKED // {name}");
            VisualFactory.RingPulse(_game.BasePosition, DoctrineColor(), 2.35f);
            EagleDefenseEngineeringDirector.Instance?.GrantParts(EngineeringPartsBonus);
        }

        private void UpgradeDoctrine()
        {
            if (!_doctrineLocked)
            {
                Announce("SELECT DOCTRINE FIRST // F1 F2 F3");
                return;
            }
            if (DoctrineLevel >= 4)
            {
                Announce("FORTRESS DOCTRINE // MAX LEVEL 4");
                return;
            }
            int cost = DoctrineLevel + 1;
            if (!SpendCharges(cost, "DOCTRINE UPGRADE")) return;
            DoctrineLevel++;
            _nextCommandAt = Time.time + 0.45f;
            EagleFortressDirector.Instance?.RepairFortress(1 + FortressModuleBonus);
            EagleDefenseEngineeringDirector.Instance?.GrantParts(EngineeringPartsBonus);
            VisualFactory.RingPulse(_game.BasePosition, DoctrineColor(), 2.6f);
            Announce($"{Doctrine.ToString().ToUpperInvariant()} // LEVEL {DoctrineLevel} ONLINE");
        }

        private void ActivateAegis()
        {
            int cost = Doctrine == FortressDoctrine.Bastion ? 2 : 3;
            if (!SpendCharges(cost, "AEGIS")) return;
            _nextCommandAt = Time.time + 1.2f;
            float duration = 2.4f + (Doctrine == FortressDoctrine.Bastion ? DoctrineLevel * 0.85f : 0f);
            EagleFortressDirector.Instance?.ActivateEmergencyShield(duration);
            ReinforceFriendlyDefenses(Doctrine == FortressDoctrine.Bastion ? 2 : 1);
            Announce($"AEGIS COMMAND // CORE SHIELD {duration:0.0}s");
        }

        private void ActivateCounterBattery()
        {
            int cost = Doctrine == FortressDoctrine.HunterGrid ? 2 : 3;
            if (!SpendCharges(cost, "COUNTER-BATTERY")) return;
            _nextCommandAt = Time.time + 1.1f;

            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            int affected = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                float distance = Vector2.Distance(enemy.transform.position, _game.BasePosition);
                if (distance > 7.2f + SentryRangeBonus) continue;
                CombatStatus status = enemy.GetComponent<CombatStatus>();
                if (status == null) status = enemy.gameObject.AddComponent<CombatStatus>();
                status.ApplyEmp(1.25f + 0.25f * DoctrineLevel);
                if (Doctrine == FortressDoctrine.HunterGrid && (enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Heavy))
                    enemy.Health.Damage(1 + (DoctrineLevel >= 4 ? 1 : 0), Team.Player);
                affected++;
            }

            VisualFactory.RingPulse(_game.BasePosition, new Color(0.35f, 0.92f, 1f), 7.0f);
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.66f, 0.02f);
            Announce($"COUNTER-BATTERY EMP // {affected} HOSTILES DISRUPTED");
        }

        private void ActivateEngineerSurge()
        {
            int cost = Doctrine == FortressDoctrine.Recovery ? 1 : 2;
            if (!SpendCharges(cost, "ENGINEER SURGE")) return;
            _nextCommandAt = Time.time + 1.0f;
            int repair = Doctrine == FortressDoctrine.Recovery ? 2 + RepairBonus : 1;
            _game.RepairEagle(1);
            EagleFortressDirector.Instance?.RepairFortress(repair);
            EagleDefenseEngineeringDirector.Instance?.GrantParts(2 + EngineeringPartsBonus);
            DeployRecoveryNode();
            VisualFactory.RingPulse(_game.BasePosition, new Color(0.38f, 1f, 0.55f), 2.3f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.45f, 0.02f);
            Announce("ENGINEER SURGE // CORE + MODULES + FIELD PARTS");
        }

        private void DeployRecoveryNode()
        {
            if (Doctrine != FortressDoctrine.Recovery || _fieldNodes.Count >= DoctrineLevel) return;
            var go = new GameObject("FORTRESS_RECOVERY_NODE");
            float side = _fieldNodes.Count % 2 == 0 ? -1f : 1f;
            go.transform.position = _game.BasePosition + new Vector2(side * (2.0f + _fieldNodes.Count * 0.25f), -0.75f);
            _fieldNodes.Add(go);
            VisualFactory.Disc("RecoveryPlate", go.transform, new Vector2(0.60f, 0.60f), new Color(0.10f, 0.30f, 0.18f), Vector3.zero, 9);
            VisualFactory.RingObject("RecoveryRing", go.transform, new Vector2(0.72f, 0.72f), new Color(0.35f, 1f, 0.55f), Vector3.zero, 10);
            var relay = go.AddComponent<FortressRecoveryNode>();
            relay.Initialize(_game, DoctrineLevel);
        }

        private void ReinforceFriendlyDefenses(int amount)
        {
            if (amount <= 0) return;
            Health[] all = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Health h = all[i];
                if (h == null || h.IsDead || h.Team != Team.Player) continue;
                if (h == _eagle) continue;
                h.Heal(amount);
            }
        }

        private void OnEagleDamaged(Health eagle, int amount)
        {
            if (eagle == null || amount <= 0) return;
            _sectorPerfect = false;
            _lastEagleHealth = eagle.Current;
            if (eagle.Current <= Mathf.Max(1, eagle.Maximum / 3) && _charges > 0)
                Announce("CORE CRITICAL // F5 AEGIS  F7 ENGINEER SURGE");
        }

        private bool SpendCharges(int amount, string label)
        {
            if (_charges < amount)
            {
                Announce($"{label} // NEED {amount} COMMAND CHARGES");
                return false;
            }
            _charges -= amount;
            PersistCharges();
            return true;
        }

        private void AddCharges(int amount)
        {
            if (amount <= 0) return;
            _charges = Mathf.Clamp(_charges + amount, 0, 12);
            PersistCharges();
        }

        private void PersistCharges()
        {
            PlayerPrefs.SetInt("TankRevival.Fortress.Charges", _charges);
        }

        private void CleanupFieldNodes()
        {
            for (int i = 0; i < _fieldNodes.Count; i++)
                if (_fieldNodes[i] != null) Destroy(_fieldNodes[i]);
            _fieldNodes.Clear();
        }

        private Color DoctrineColor()
        {
            if (Doctrine == FortressDoctrine.HunterGrid) return new Color(0.28f, 0.92f, 1f);
            if (Doctrine == FortressDoctrine.Recovery) return new Color(0.34f, 1f, 0.54f);
            return new Color(0.32f, 0.68f, 1f);
        }

        private void Announce(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 2.6f;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.48f, 0.90f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.86f, 0.92f, 0.98f) } };
            _accent = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.44f, 1f, 0.68f) } };
            _warn = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.65f, 0.18f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            float width = 530f;
            float x = 14f;
            float y = Screen.height - 270f;
            GUI.color = new Color(0.012f, 0.028f, 0.044f, 0.94f);
            GUI.Box(new Rect(x, y, width, 72f), string.Empty);
            GUI.color = Color.white;

            string doctrine = _doctrineLocked ? Doctrine.ToString().ToUpperInvariant() + " L" + DoctrineLevel : "SELECT: F1 BASTION / F2 HUNTER / F3 RECOVERY";
            GUI.Label(new Rect(x + 12f, y + 7f, width - 24f, 18f), $"ORZEL FORTRESS COMMAND // SECTOR {_sector:00} // CHARGES {_charges}", _title);
            GUI.Label(new Rect(x + 12f, y + 27f, width - 24f, 17f), doctrine, _doctrineLocked ? _accent : _warn);
            GUI.Label(new Rect(x + 12f, y + 44f, width - 24f, 17f), "F4 Upgrade   F5 Aegis   F6 Counter-Battery   F7 Engineer Surge", _body);
            if (Time.unscaledTime < _toastUntil)
                GUI.Label(new Rect(x + 12f, y + 57f, width - 24f, 17f), _toast, _warn);
        }
    }

    public sealed class FortressRecoveryNode : MonoBehaviour
    {
        private TankGame _game;
        private int _level;
        private int _charges;
        private float _nextPulse;

        public void Initialize(TankGame game, int level)
        {
            _game = game;
            _level = Mathf.Clamp(level, 1, 4);
            _charges = 1 + _level;
            _nextPulse = Time.time + 7f;
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || _charges <= 0 || Time.time < _nextPulse) return;
            _nextPulse = Time.time + Mathf.Max(5f, 9f - _level * 0.75f);
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player == null || player.Health == null || player.Health.IsDead) return;
            if (Vector2.Distance(player.transform.position, transform.position) > 3.4f) return;
            if (player.Health.Current >= player.Health.Maximum) return;
            _charges--;
            player.Health.Heal(1);
            VisualFactory.RingPulse(transform.position, new Color(0.35f, 1f, 0.55f), 0.85f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.18f, 0.04f);
        }
    }
}
