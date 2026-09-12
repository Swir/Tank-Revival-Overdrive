using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum CareerAchievement
    {
        FirstBlood = 0,
        TankHunter100 = 1,
        TankHunter500 = 2,
        BossBreaker = 3,
        BossHunter10 = 4,
        HoldTheLine25 = 5,
        IronDefense50 = 6,
        EagleLegend75 = 7,
        CenturyDefense100 = 8,
        Veteran10Runs = 9
    }

    [Serializable]
    public sealed class CareerRecordData
    {
        public int schemaVersion = 1;
        public int lifetimeKills;
        public int lifetimeBossKills;
        public int runsObserved;
        public int highestRoundObserved = 1;
        public int round25Visits;
        public int round50Visits;
        public int round75Visits;
        public int round100Visits;
        public int achievementsUnlocked;
        public string unlockedCsv = string.Empty;
        public string lastWriteUtc = string.Empty;
    }

    [DefaultExecutionOrder(420)]
    public sealed class CareerRecordsDirector : MonoBehaviour
    {
        private const string CareerKey = "TankRevival.V54.CareerJournal";
        private const int CatalogSize = 10;

        private readonly HashSet<int> _trackedEnemies = new HashSet<int>();
        private readonly HashSet<CareerAchievement> _unlocked = new HashSet<CareerAchievement>();
        private TankGame _game;
        private CareerRecordData _data;
        private bool _wasPlaying;
        private int _lastRound;
        private bool _dirty;
        private float _nextSave;
        private bool _showOverlay;
        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _accent;
        private GUIStyle _dim;

        public static CareerRecordsDirector Instance { get; private set; }
        public static int AchievementCatalogSize => CatalogSize;
        public CareerRecordData Data => _data;
        public int UnlockedCount => _unlocked.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CareerRecordsDirector>() != null) return;
            GameObject go = new GameObject("CareerRecordsDirector_v5_4");
            DontDestroyOnLoad(go);
            go.AddComponent<CareerRecordsDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
            _nextSave = Time.unscaledTime + 8f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_game == null)
                _game = FindAnyObjectByType<TankGame>();

            if (Input.GetKeyDown(KeyCode.F2))
                _showOverlay = !_showOverlay;

            if (_game == null)
            {
                FlushIfDue();
                return;
            }

            bool playing = _game.IsPlaying;
            if (playing && !_wasPlaying)
            {
                _data.runsObserved++;
                EvaluateAchievement(CareerAchievement.Veteran10Runs, _data.runsObserved >= 10);
                MarkDirty();
            }

            if (playing)
            {
                int round = Mathf.Max(1, _game.CurrentRound);
                if (round != _lastRound)
                    ObserveRound(round);
                TrackLiveEnemies();
            }
            else if (_wasPlaying)
            {
                _trackedEnemies.Clear();
                SaveNow();
            }

            _wasPlaying = playing;
            FlushIfDue();
        }

        private void ObserveRound(int round)
        {
            _lastRound = round;
            _data.highestRoundObserved = Mathf.Max(_data.highestRoundObserved, round);
            if (round == 25) _data.round25Visits++;
            if (round == 50) _data.round50Visits++;
            if (round == 75) _data.round75Visits++;
            if (round == 100) _data.round100Visits++;

            EvaluateAchievement(CareerAchievement.HoldTheLine25, round >= 25);
            EvaluateAchievement(CareerAchievement.IronDefense50, round >= 50);
            EvaluateAchievement(CareerAchievement.EagleLegend75, round >= 75);
            EvaluateAchievement(CareerAchievement.CenturyDefense100, round >= 100);
            MarkDirty();
        }

        private void TrackLiveEnemies()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null) continue;
                int id = enemy.GetInstanceID();
                if (!_trackedEnemies.Add(id)) continue;
                EnemyKind kind = enemy.Kind;
                enemy.Health.Died += _ => RecordKill(kind);
            }
        }

        private void RecordKill(EnemyKind kind)
        {
            _data.lifetimeKills++;
            if (kind == EnemyKind.Boss)
                _data.lifetimeBossKills++;

            EvaluateAchievement(CareerAchievement.FirstBlood, _data.lifetimeKills >= 1);
            EvaluateAchievement(CareerAchievement.TankHunter100, _data.lifetimeKills >= 100);
            EvaluateAchievement(CareerAchievement.TankHunter500, _data.lifetimeKills >= 500);
            EvaluateAchievement(CareerAchievement.BossBreaker, _data.lifetimeBossKills >= 1);
            EvaluateAchievement(CareerAchievement.BossHunter10, _data.lifetimeBossKills >= 10);
            MarkDirty();
        }

        private void EvaluateAchievement(CareerAchievement achievement, bool condition)
        {
            if (!condition || _unlocked.Contains(achievement)) return;
            _unlocked.Add(achievement);
            _data.achievementsUnlocked = _unlocked.Count;
            _data.unlockedCsv = SerializeUnlocked();
            _dirty = true;
            Debug.Log("[TankRevival] Career achievement unlocked: " + AchievementName(achievement));
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void FlushIfDue()
        {
            if (_dirty && Time.unscaledTime >= _nextSave)
                SaveNow();
        }

        public void SaveNow()
        {
            if (_data == null || !_dirty) return;
            _data.achievementsUnlocked = _unlocked.Count;
            _data.unlockedCsv = SerializeUnlocked();
            _data.lastWriteUtc = DateTime.UtcNow.ToString("O");
            PlayerPrefs.SetString(CareerKey, JsonUtility.ToJson(_data, false));
            PlayerPrefs.Save();
            _dirty = false;
            _nextSave = Time.unscaledTime + 8f;
        }

        private void Load()
        {
            _data = null;
            try
            {
                string json = PlayerPrefs.GetString(CareerKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                    _data = JsonUtility.FromJson<CareerRecordData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TankRevival] Career journal load failed safely: " + ex.Message);
            }

            if (_data == null || _data.schemaVersion < 1)
                _data = new CareerRecordData();

            _data.highestRoundObserved = Mathf.Max(1, _data.highestRoundObserved);
            ParseUnlocked(_data.unlockedCsv);
            ReevaluateFromRecords();
        }

        private void ReevaluateFromRecords()
        {
            EvaluateAchievement(CareerAchievement.FirstBlood, _data.lifetimeKills >= 1);
            EvaluateAchievement(CareerAchievement.TankHunter100, _data.lifetimeKills >= 100);
            EvaluateAchievement(CareerAchievement.TankHunter500, _data.lifetimeKills >= 500);
            EvaluateAchievement(CareerAchievement.BossBreaker, _data.lifetimeBossKills >= 1);
            EvaluateAchievement(CareerAchievement.BossHunter10, _data.lifetimeBossKills >= 10);
            EvaluateAchievement(CareerAchievement.HoldTheLine25, _data.highestRoundObserved >= 25);
            EvaluateAchievement(CareerAchievement.IronDefense50, _data.highestRoundObserved >= 50);
            EvaluateAchievement(CareerAchievement.EagleLegend75, _data.highestRoundObserved >= 75);
            EvaluateAchievement(CareerAchievement.CenturyDefense100, _data.highestRoundObserved >= 100);
            EvaluateAchievement(CareerAchievement.Veteran10Runs, _data.runsObserved >= 10);
        }

        private string SerializeUnlocked()
        {
            List<int> values = new List<int>(_unlocked.Count);
            foreach (CareerAchievement achievement in _unlocked)
                values.Add((int)achievement);
            values.Sort();
            return string.Join(",", values);
        }

        private void ParseUnlocked(string csv)
        {
            _unlocked.Clear();
            if (string.IsNullOrWhiteSpace(csv)) return;
            string[] parts = csv.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                int value;
                if (int.TryParse(parts[i], out value) && value >= 0 && value < CatalogSize)
                    _unlocked.Add((CareerAchievement)value);
            }
            _data.achievementsUnlocked = _unlocked.Count;
        }

        public bool IsUnlocked(CareerAchievement achievement)
        {
            return _unlocked.Contains(achievement);
        }

        public static bool ValidateCatalog(out string reason)
        {
            Array values = Enum.GetValues(typeof(CareerAchievement));
            if (values.Length != CatalogSize)
            {
                reason = "achievement catalog size mismatch";
                return false;
            }
            HashSet<string> names = new HashSet<string>();
            for (int i = 0; i < values.Length; i++)
            {
                CareerAchievement achievement = (CareerAchievement)values.GetValue(i);
                string name = AchievementName(achievement);
                if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
                {
                    reason = "achievement name catalog invalid";
                    return false;
                }
            }
            reason = "achievements=" + CatalogSize;
            return true;
        }

        public static bool RunPersistenceSelfTest(out string reason)
        {
            const string key = "TankRevival.V54.CareerSelfTest";
            try
            {
                CareerRecordData sample = new CareerRecordData
                {
                    lifetimeKills = 123,
                    lifetimeBossKills = 4,
                    runsObserved = 7,
                    highestRoundObserved = 55,
                    achievementsUnlocked = 3,
                    unlockedCsv = "0,3,5"
                };
                string json = JsonUtility.ToJson(sample, false);
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
                CareerRecordData loaded = JsonUtility.FromJson<CareerRecordData>(PlayerPrefs.GetString(key, string.Empty));
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                if (loaded == null || loaded.lifetimeKills != 123 || loaded.highestRoundObserved != 55 || loaded.unlockedCsv != "0,3,5")
                {
                    reason = "career persistence round-trip mismatch";
                    return false;
                }
                reason = "career persistence round-trip ok";
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static string AchievementName(CareerAchievement achievement)
        {
            switch (achievement)
            {
                case CareerAchievement.FirstBlood: return "FIRST BLOOD";
                case CareerAchievement.TankHunter100: return "TANK HUNTER // 100";
                case CareerAchievement.TankHunter500: return "ARMORED REAPER // 500";
                case CareerAchievement.BossBreaker: return "BOSS BREAKER";
                case CareerAchievement.BossHunter10: return "LEGEND HUNTER // 10";
                case CareerAchievement.HoldTheLine25: return "HOLD THE LINE // 25";
                case CareerAchievement.IronDefense50: return "IRON DEFENSE // 50";
                case CareerAchievement.EagleLegend75: return "EAGLE LEGEND // 75";
                case CareerAchievement.CenturyDefense100: return "CENTURY DEFENSE // 100";
                case CareerAchievement.Veteran10Runs: return "VETERAN // 10 RUNS";
                default: return achievement.ToString().ToUpperInvariant();
            }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.25f, 0.92f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
            _accent = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.78f, 0.20f) } };
            _dim = new GUIStyle(_body) { normal = { textColor = new Color(0.55f, 0.62f, 0.68f) } };
        }

        private void OnGUI()
        {
            if (!_showOverlay || _data == null) return;
            EnsureStyles();
            float w = Mathf.Min(690f, Screen.width - 40f);
            float h = Mathf.Min(620f, Screen.height - 40f);
            Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.color = new Color(0.025f, 0.035f, 0.050f, 0.97f);
            GUI.Box(panel, string.Empty);
            GUI.color = Color.white;

            float x = panel.x + 24f;
            float y = panel.y + 18f;
            GUI.Label(new Rect(x, y, w - 48f, 34f), "CAREER RECORD // F2 CLOSE", _title);
            y += 44f;
            int profileFurthest = PlayerProfileDirector.Instance != null ? PlayerProfileDirector.Instance.FurthestRound : _data.highestRoundObserved;
            GUI.Label(new Rect(x, y, w - 48f, 26f), $"Kills {_data.lifetimeKills:N0}   Bosses {_data.lifetimeBossKills:N0}   Runs {_data.runsObserved:N0}   Furthest {Mathf.Max(profileFurthest, _data.highestRoundObserved):000}", _body);
            y += 36f;
            GUI.Label(new Rect(x, y, w - 48f, 26f), $"ACHIEVEMENTS {_unlocked.Count}/{CatalogSize}", _accent);
            y += 32f;

            Array values = Enum.GetValues(typeof(CareerAchievement));
            for (int i = 0; i < values.Length; i++)
            {
                CareerAchievement achievement = (CareerAchievement)values.GetValue(i);
                bool unlocked = _unlocked.Contains(achievement);
                GUI.Label(new Rect(x, y, w - 48f, 24f), (unlocked ? "[UNLOCKED]  " : "[LOCKED]    ") + AchievementName(achievement), unlocked ? _accent : _dim);
                y += 26f;
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) SaveNow();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus) SaveNow();
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }
    }
}
