using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.2 persistent command career. Converts existing campaign achievements into Command XP/Rank
    /// and grants modest combat-ready perks without replacing War Garage progression.
    /// </summary>
    public sealed class MetaProgressionDirector : MonoBehaviour
    {
        private static readonly int[] RankThresholds = { 0, 40, 90, 160, 250, 360, 500, 670, 870, 1100, 1400 };
        private static readonly string[] RankNames =
        {
            "CADET", "CREWMAN", "SERGEANT", "LIEUTENANT", "CAPTAIN",
            "MAJOR", "COLONEL", "BRIGADIER", "GENERAL", "MARSHAL", "EAGLE MARSHAL"
        };

        private TankGame _game;
        private PlayerTank _appliedPlayer;
        private int _rank;
        private int _xp;
        private float _nextRecalc;
        private string _banner = string.Empty;
        private float _bannerUntil;
        private GUIStyle _header, _body, _small, _bannerStyle;

        public int Rank => _rank;
        public int Experience => _xp;
        public static int CurrentRank => Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.CommandRank", 0), 0, RankThresholds.Length - 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<MetaProgressionDirector>() != null) return;
            var go = new GameObject("MetaProgressionDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<MetaProgressionDirector>();
        }

        private void Awake()
        {
            Recalculate(true);
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (Time.unscaledTime >= _nextRecalc)
            {
                _nextRecalc = Time.unscaledTime + 1.0f;
                Recalculate(false);
            }

            if (!_game.IsPlaying)
            {
                _appliedPlayer = null;
                return;
            }

            PlayerTank player = CombatRoster.Player;
            if (player != null && player != _appliedPlayer)
            {
                _appliedPlayer = player;
                ApplyRankPerks(player);
            }
        }

        private void Recalculate(bool silent)
        {
            int contracts = PlayerPrefs.GetInt("TankRevival.ContractsCompleted", 0);
            int legends = PlayerPrefs.GetInt("TankRevival.BossLegendsDefeated", 0);
            int directives = PlayerPrefs.GetInt("TankRevival.StrategicDirectives", 0);
            int elites = PlayerPrefs.GetInt("TankRevival.EliteEncountersDefeated", 0);
            int operations = PlayerPrefs.GetInt("TankRevival.OperationsCompleted", 0);
            int medals = PlayerPrefs.GetInt("TankRevival.OperationMedals", 0);
            int bestAct = PlayerPrefs.GetInt("TankRevival.BestAct", 0);
            int bestSector = PlayerPrefs.GetInt("TankRevival.BestSector", 0);

            _xp = Mathf.Max(0,
                contracts * 8 + legends * 24 + directives * 12 + elites * 20 +
                operations * 24 + medals * 6 + bestAct * 15 + bestSector * 6);

            int newRank = 0;
            for (int i = 1; i < RankThresholds.Length; i++)
                if (_xp >= RankThresholds[i]) newRank = i;

            int oldRank = Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.CommandRank", 0), 0, RankThresholds.Length - 1);
            _rank = newRank;
            PlayerPrefs.SetInt("TankRevival.CommandXP", _xp);
            PlayerPrefs.SetInt("TankRevival.CommandRank", _rank);

            if (_rank > oldRank && !silent)
            {
                _banner = $"COMMAND PROMOTION // {RankNames[_rank]} // RANK {_rank}";
                _bannerUntil = Time.unscaledTime + 4.0f;
                BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.78f, 0f);
            }

            if (_rank != oldRank || silent) PlayerPrefs.Save();
        }

        private void ApplyRankPerks(PlayerTank player)
        {
            if (player == null) return;

            int ap = 1 + _rank / 2;
            if (ap > 0) player.AddAmmo(AmmoType.ArmorPiercing, ap);
            if (_rank >= 3) player.AddAmmo(AmmoType.Explosive, 1 + _rank / 4);
            if (_rank >= 5) player.AddAmmo(AmmoType.EMP, 1 + (_rank - 5) / 2);
            if (_rank >= 8) player.AddAmmo(AmmoType.Plasma, 1 + (_rank - 8));

            if (player.Health != null)
            {
                if (_rank >= 2) player.Health.Heal(1);
                if (_rank >= 4)
                    player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 0.8f + _rank * 0.12f);
            }

            if (_rank >= 6 && _game != null && _game.CurrentRound > 1 && _game.CurrentRound % 20 == 1)
                _game.RepairEagle(1);
        }

        private int NextThreshold()
        {
            if (_rank >= RankThresholds.Length - 1) return RankThresholds[RankThresholds.Length - 1];
            return RankThresholds[_rank + 1];
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.38f, 0.94f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.64f, 0.74f, 0.82f) } };
            _bannerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.82f, 0.24f) } };
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (_game != null && !_game.IsPlaying)
            {
                int medals = PlayerPrefs.GetInt("TankRevival.OperationMedals", 0);
                int ops = PlayerPrefs.GetInt("TankRevival.OperationsCompleted", 0);
                float x = 14f;
                float y = Mathf.Max(176f, Screen.height - 178f);
                GUI.color = new Color(0.018f, 0.032f, 0.052f, 0.94f);
                GUI.Box(new Rect(x, y, 386f, 118f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 14f, y + 10f, 360f, 22f), $"COMMAND CAREER // {RankNames[_rank]} R{_rank}", _header);
                string progress = _rank >= RankThresholds.Length - 1 ? "MAXIMUM COMMAND RANK" : $"COMMAND XP {_xp}/{NextThreshold()}";
                GUI.Label(new Rect(x + 14f, y + 35f, 360f, 20f), progress, _body);
                GUI.Label(new Rect(x + 14f, y + 58f, 360f, 18f), $"OPERATIONS {ops}   //   MEDALS {medals}", _body);
                GUI.Label(new Rect(x + 14f, y + 81f, 360f, 26f), "Ranks grant deployment ammo, recovery and late-career Eagle support.", _small);
            }

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.02f, 0.04f, 0.065f, 0.95f);
                GUI.Box(new Rect(Screen.width * 0.5f - 360f, Screen.height * 0.24f, 720f, 54f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 350f, Screen.height * 0.24f + 8f, 700f, 38f), _banner, _bannerStyle);
            }
        }
    }
}
