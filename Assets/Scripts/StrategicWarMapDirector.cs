using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v4.1 strategic layer over the v4.0 sector campaign.
    /// Turns sector performance into persistent momentum, next-sector advantages/penalties,
    /// checkpoint resupply and a full ten-sector war map without replacing authoritative combat.
    /// </summary>
    [DefaultExecutionOrder(6000)]
    public sealed class StrategicWarMapDirector : MonoBehaviour
    {
        private enum SectorState { Locked, Active, Secured, Dominant, Contested }

        private static readonly string[] Names =
        {
            "BORDER FIRE", "STEEL CORRIDOR", "FROZEN PASS", "ASH FIELDS", "CRIMSON LINE",
            "HYDRA MARSH", "REAPER VALLEY", "NIGHT FORTRESS", "BURNING CROWN", "ZERO FRONT"
        };

        private static readonly string[] Threats =
        {
            "Mechanized screen", "Hardened armor", "Frozen mobility", "Artillery pressure", "Fortified belt",
            "Encirclement", "Long-range killers", "Blackout fortress", "Elite defense", "Overdrive Zero"
        };

        private TankGame _game;
        private int _round;
        private int _sector = -1;
        private int _momentum;
        private bool _mapOpen;
        private bool _checkpointApplied;
        private float _scanAt;
        private float _bannerUntil;
        private string _banner = string.Empty;
        private readonly HashSet<int> _reinforced = new HashSet<int>();

        private GUIStyle _title;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _good;
        private GUIStyle _warn;
        private GUIStyle _bad;
        private GUIStyle _center;

        public static int Momentum => Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.StrategicMomentum", 50), 0, 100);
        public static int CurrentSectorIndex
        {
            get
            {
                TankGame game = FindAnyObjectByType<TankGame>();
                int round = game != null ? Mathf.Clamp(game.CurrentRound, 1, 100) : 1;
                return Mathf.Clamp((round - 1) / 10, 0, 9);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<StrategicWarMapDirector>() != null) return;
            var go = new GameObject("StrategicWarMapDirector_v4_1");
            DontDestroyOnLoad(go);
            go.AddComponent<StrategicWarMapDirector>();
        }

        private void Awake()
        {
            _momentum = Momentum;
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (Input.GetKeyDown(KeyCode.M))
                _mapOpen = !_mapOpen;
            if (_mapOpen && Input.GetKeyDown(KeyCode.Escape))
                _mapOpen = false;

            if (!_game.IsPlaying)
            {
                _round = 0;
                _sector = -1;
                _checkpointApplied = false;
                _reinforced.Clear();
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            int sector = Mathf.Clamp((round - 1) / 10, 0, 9);
            if (round != _round)
            {
                int previousSector = _sector;
                _round = round;
                _sector = sector;
                _reinforced.Clear();

                if (previousSector >= 0 && sector != previousSector)
                {
                    ResolvePreviousSector(previousSector);
                    _checkpointApplied = false;
                }

                if (sector != previousSector)
                    ApplySectorEntryConsequences(sector);
            }

            if (!_checkpointApplied && ((_round - 1) % 10) == 0)
                ApplyCheckpointResupply(_sector);

            if (Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.35f;
                ApplyEnemyPressure();
            }
        }

        private void ResolvePreviousSector(int sector)
        {
            int kills = PlayerPrefs.GetInt($"TankRevival.SectorKills.{sector}", 0);
            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;

            float playerRatio = player != null && player.Health != null && player.Health.Max > 0
                ? (float)player.Health.Current / player.Health.Max : 0.5f;
            float eagleRatio = eagle != null && eagle.Max > 0 ? (float)eagle.Current / eagle.Max : 0.5f;

            int expectedKills = 16 + sector * 3;
            int killScore = Mathf.Clamp(Mathf.RoundToInt((float)kills / Mathf.Max(1, expectedKills) * 55f), 0, 55);
            int survivalScore = Mathf.RoundToInt(playerRatio * 20f + eagleRatio * 25f);
            int score = Mathf.Clamp(killScore + survivalScore, 0, 100);
            int grade = score >= 88 ? 5 : score >= 72 ? 4 : score >= 55 ? 3 : score >= 38 ? 2 : 1;

            PlayerPrefs.SetInt($"TankRevival.SectorScore.{sector}", score);
            PlayerPrefs.SetInt($"TankRevival.SectorGrade.{sector}", grade);

            int delta = grade == 5 ? 14 : grade == 4 ? 9 : grade == 3 ? 3 : grade == 2 ? -7 : -14;
            _momentum = Mathf.Clamp(_momentum + delta, 0, 100);
            PlayerPrefs.SetInt("TankRevival.StrategicMomentum", _momentum);
            PlayerPrefs.SetInt("TankRevival.LastResolvedStrategicSector", sector);
            PlayerPrefs.Save();

            string gradeName = GradeName(grade);
            _banner = $"SECTOR {sector + 1} {gradeName} // SCORE {score} // MOMENTUM {_momentum}%";
            _bannerUntil = Time.unscaledTime + 4.4f;
        }

        private void ApplySectorEntryConsequences(int sector)
        {
            if (sector <= 0)
            {
                _banner = "STRATEGIC WAR MAP ONLINE // M TO OPEN";
                _bannerUntil = Time.unscaledTime + 3.2f;
                return;
            }

            int previousGrade = PlayerPrefs.GetInt($"TankRevival.SectorGrade.{sector - 1}", 3);
            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;

            if (previousGrade >= 5)
            {
                WarEconomyDirector.AwardMissionBonds(8 + sector, "STRATEGIC DOMINANCE");
                if (player != null)
                {
                    player.AddAmmo(AmmoType.ArmorPiercing, 4);
                    player.AddAmmo(AmmoType.Explosive, 3);
                    player.AddAmmo(AmmoType.Plasma, sector >= 5 ? 2 : 1);
                    if (player.Health != null) player.Health.Heal(2);
                }
                _game.RepairEagle(2);
                _banner = "DOMINANCE CARRYOVER // ALLIED LOGISTICS SUPERIORITY";
            }
            else if (previousGrade == 4)
            {
                WarEconomyDirector.AwardMissionBonds(5 + sector / 2, "SECURED SUPPLY LINE");
                if (player != null)
                {
                    player.AddAmmo(AmmoType.ArmorPiercing, 3);
                    player.AddAmmo(AmmoType.EMP, 1);
                }
                _game.RepairEagle(1);
                _banner = "SECURED FRONT // SUPPLY LINE INTACT";
            }
            else if (previousGrade == 3)
            {
                if (player != null) player.AddAmmo(AmmoType.ArmorPiercing, 2);
                _banner = "CONTESTED FRONT // STANDARD DEPLOYMENT";
            }
            else
            {
                if (player != null && player.Health != null)
                    player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 1.5f);
                if (eagle != null)
                    eagle.InvulnerableUntil = Mathf.Max(eagle.InvulnerableUntil, Time.time + 1.2f);
                _banner = previousGrade == 2
                    ? "ENEMY COUNTEROFFENSIVE // REINFORCED ARMOR EXPECTED"
                    : "FRONT COLLAPSE // HEAVY ENEMY REINFORCEMENTS INBOUND";
            }

            PlayerPrefs.SetInt($"TankRevival.StrategicEntryApplied.{sector}", 1);
            PlayerPrefs.Save();
            _bannerUntil = Time.unscaledTime + 4.4f;
        }

        private void ApplyCheckpointResupply(int sector)
        {
            if (_checkpointApplied || sector < 0) return;
            _checkpointApplied = true;

            PlayerTank player = CombatRoster.Player;
            if (player == null) return;

            int priorGrade = sector > 0 ? PlayerPrefs.GetInt($"TankRevival.SectorGrade.{sector - 1}", 3) : 3;
            int strength = Mathf.Clamp(priorGrade + (_momentum >= 70 ? 1 : 0) - (_momentum <= 30 ? 1 : 0), 1, 6);

            player.AddAmmo(AmmoType.ArmorPiercing, 2 + strength);
            player.AddAmmo(AmmoType.Explosive, 1 + strength / 2);
            if (strength >= 3) player.AddAmmo(AmmoType.EMP, 1);
            if (strength >= 5) player.AddAmmo(AmmoType.Plasma, 1);
            if (player.Health != null) player.Health.Heal(strength >= 4 ? 2 : 1);
            _game.RepairEagle(strength >= 5 ? 2 : 1);

            ArmorSystem armor = player.GetComponent<ArmorSystem>();
            if (armor != null) armor.RepairModules(12 + strength * 5);

            PlayerPrefs.SetInt("TankRevival.LastCheckpointSector", sector);
            PlayerPrefs.SetInt("TankRevival.LastCheckpointRound", _round);
            PlayerPrefs.SetInt("TankRevival.LastCheckpointMomentum", _momentum);
            PlayerPrefs.SetInt("TankRevival.LastCheckpointBonds", WarEconomyDirector.CurrentBonds);
            PlayerPrefs.Save();

            _banner = $"FIELD CHECKPOINT // RESUPPLY TIER {strength} // MOMENTUM {_momentum}%";
            _bannerUntil = Time.unscaledTime + 3.4f;
        }

        private void ApplyEnemyPressure()
        {
            if (_sector <= 0 || _momentum >= 45) return;
            int previousGrade = PlayerPrefs.GetInt($"TankRevival.SectorGrade.{_sector - 1}", 3);
            if (previousGrade >= 3) return;

            int bonus = previousGrade <= 1 ? 2 : 1;
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                int id = enemy.GetInstanceID();
                if (!_reinforced.Add(id)) continue;

                int extra = bonus;
                if (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege) extra += bonus;
                if (enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Boss) extra += 1 + bonus;
                enemy.Health.SetMaximum(enemy.Health.Max + extra, true);
            }
        }

        private SectorState StateFor(int index)
        {
            if (index == _sector && _game != null && _game.IsPlaying) return SectorState.Active;
            int grade = PlayerPrefs.GetInt($"TankRevival.SectorGrade.{index}", 0);
            if (grade >= 5) return SectorState.Dominant;
            if (grade >= 3) return SectorState.Secured;
            if (grade > 0) return SectorState.Contested;
            return SectorState.Locked;
        }

        private static string GradeName(int grade)
        {
            switch (grade)
            {
                case 5: return "DOMINANT";
                case 4: return "DECISIVE";
                case 3: return "SECURED";
                case 2: return "CONTESTED";
                default: return "CRITICAL";
            }
        }

        private static string RouteName(int sector)
        {
            int route = PlayerPrefs.GetInt($"TankRevival.SectorRoute.{sector}", 0);
            if (route == 1) return "SPEARHEAD";
            if (route == 2) return "BULWARK";
            if (route == 3) return "RECON";
            return "UNDECIDED";
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(0.42f, 0.94f, 1f);
            _header = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(0.92f, 0.96f, 1f);
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _body.normal.textColor = new Color(0.82f, 0.90f, 0.96f);
            _small = new GUIStyle(_body) { fontSize = 10 };
            _small.normal.textColor = new Color(0.60f, 0.70f, 0.78f);
            _good = new GUIStyle(_body) { fontStyle = FontStyle.Bold };
            _good.normal.textColor = new Color(0.38f, 1f, 0.58f);
            _warn = new GUIStyle(_body) { fontStyle = FontStyle.Bold };
            _warn.normal.textColor = new Color(1f, 0.76f, 0.24f);
            _bad = new GUIStyle(_body) { fontStyle = FontStyle.Bold };
            _bad.normal.textColor = new Color(1f, 0.34f, 0.28f);
            _center = new GUIStyle(_header) { alignment = TextAnchor.MiddleCenter };
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (_game != null && _game.IsPlaying && !_mapOpen)
            {
                GUI.Label(new Rect(Screen.width - 250f, 30f, 235f, 18f), $"M // WAR MAP   MOMENTUM {_momentum}%", _small);
            }

            if (Time.unscaledTime < _bannerUntil && !_mapOpen)
            {
                float w = Mathf.Min(820f, Screen.width - 40f);
                GUI.color = new Color(0.008f, 0.022f, 0.040f, 0.95f);
                GUI.Box(new Rect((Screen.width - w) * 0.5f, 82f, w, 50f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect((Screen.width - w) * 0.5f + 12f, 94f, w - 24f, 28f), _banner, _center);
            }

            if (!_mapOpen) return;

            GUI.depth = -300;
            GUI.color = new Color(0.004f, 0.010f, 0.020f, 0.985f);
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), string.Empty);
            GUI.color = Color.white;

            float margin = 30f;
            GUI.Label(new Rect(margin, 20f, Screen.width - margin * 2f, 34f), "OPERATION OVERDRIVE // STRATEGIC WAR MAP", _title);
            GUI.Label(new Rect(margin, 56f, Screen.width - margin * 2f, 20f), "Persistent sector outcomes now change the next battlefield. ESC/M closes map.", _small);

            int completed = PlayerPrefs.GetInt("TankRevival.SectorsCompleted", 0);
            int checkpoint = PlayerPrefs.GetInt("TankRevival.LastCheckpointSector", 0);
            int flawless = PlayerPrefs.GetInt("TankRevival.FlawlessSectors", 0);
            GUI.Label(new Rect(margin, 88f, 700f, 24f), $"STRATEGIC MOMENTUM {_momentum}%   //   SECURED {completed}/10   //   FLAWLESS {flawless}   //   LAST CHECKPOINT S{checkpoint + 1}", _header);
            GUI.Label(new Rect(margin, 114f, 700f, 20f), _momentum >= 70 ? "Initiative: ALLIED ADVANTAGE — stronger checkpoint logistics." : _momentum <= 30 ? "Initiative: ENEMY ADVANTAGE — failed sectors reinforce hostile armor." : "Initiative: CONTESTED — neither side controls operational tempo.", _momentum >= 70 ? _good : _momentum <= 30 ? _bad : _warn);

            float top = 154f;
            float gap = 10f;
            float cardW = (Screen.width - margin * 2f - gap * 4f) / 5f;
            float cardH = Mathf.Min(196f, (Screen.height - top - 70f - gap) / 2f);

            for (int i = 0; i < 10; i++)
            {
                int row = i / 5;
                int col = i % 5;
                Rect r = new Rect(margin + col * (cardW + gap), top + row * (cardH + gap), cardW, cardH);
                DrawSectorCard(i, r);
            }

            GUI.Label(new Rect(margin, Screen.height - 42f, Screen.width - margin * 2f, 22f), "DOMINANT sectors feed the next offensive. CONTESTED/CRITICAL sectors create enemy reinforcement pressure. Checkpoints persist campaign intelligence and resupply state.", _small);
        }

        private void DrawSectorCard(int index, Rect r)
        {
            SectorState state = StateFor(index);
            GUI.color = state == SectorState.Active ? new Color(0.04f, 0.15f, 0.20f, 0.97f)
                : state == SectorState.Dominant ? new Color(0.03f, 0.13f, 0.08f, 0.94f)
                : state == SectorState.Contested ? new Color(0.15f, 0.055f, 0.035f, 0.94f)
                : new Color(0.025f, 0.040f, 0.060f, 0.94f);
            GUI.Box(r, string.Empty);
            GUI.color = Color.white;

            int grade = PlayerPrefs.GetInt($"TankRevival.SectorGrade.{index}", 0);
            int score = PlayerPrefs.GetInt($"TankRevival.SectorScore.{index}", 0);
            int kills = PlayerPrefs.GetInt($"TankRevival.SectorKills.{index}", 0);
            string status = state == SectorState.Active ? "ACTIVE FRONT" : grade > 0 ? GradeName(grade) : "LOCKED";
            GUIStyle statusStyle = state == SectorState.Contested ? _bad : state == SectorState.Dominant ? _good : state == SectorState.Active ? _warn : _body;

            GUI.Label(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, 22f), $"S{index + 1:00} // {Names[index]}", _header);
            GUI.Label(new Rect(r.x + 10f, r.y + 34f, r.width - 20f, 20f), status, statusStyle);
            GUI.Label(new Rect(r.x + 10f, r.y + 58f, r.width - 20f, 18f), $"Threat: {Threats[index]}", _small);
            GUI.Label(new Rect(r.x + 10f, r.y + 80f, r.width - 20f, 18f), $"Route: {RouteName(index)}", _small);
            GUI.Label(new Rect(r.x + 10f, r.y + 102f, r.width - 20f, 18f), grade > 0 ? $"Score {score}/100   Grade {grade}/5" : "Score --   Grade --", _body);
            GUI.Label(new Rect(r.x + 10f, r.y + 124f, r.width - 20f, 18f), $"Best eliminations: {kills}", _small);

            if (index == _sector && _game != null && _game.IsPlaying)
            {
                int local = ((_round - 1) % 10) + 1;
                GUI.Label(new Rect(r.x + 10f, r.y + r.height - 31f, r.width - 20f, 20f), $"ROUND {local}/10 // {_round}/100", _good);
            }
        }
    }
}
