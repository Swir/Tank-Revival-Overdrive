using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v4.0 strategic campaign spine. Converts the linear 100-round run into ten named sectors.
    /// Each sector opens with a route decision whose bonuses are applied through existing player,
    /// Eagle, ammo and War Economy systems. Sector completion is persistent and boss rounds remain
    /// authoritative through the existing encounter/boss directors.
    /// </summary>
    public sealed class OverdriveSectorCampaignDirector : MonoBehaviour
    {
        public enum SectorRoute
        {
            None = 0,
            Spearhead = 1,
            Bulwark = 2,
            Recon = 3
        }

        private static readonly string[] SectorNames =
        {
            "01 // BORDER FIRE",
            "02 // STEEL CORRIDOR",
            "03 // FROZEN PASS",
            "04 // ASH FIELDS",
            "05 // CRIMSON LINE",
            "06 // HYDRA MARSH",
            "07 // REAPER VALLEY",
            "08 // NIGHT FORTRESS",
            "09 // BURNING CROWN",
            "10 // ZERO FRONT"
        };

        private static readonly string[] SectorOperations =
        {
            "Break the frontier and establish armored momentum.",
            "Push through hardened mechanized resistance.",
            "Hold mobility through hostile frozen terrain.",
            "Survive artillery pressure and burning ground.",
            "Crack the enemy's fortified central belt.",
            "Keep the supply chain alive under encirclement.",
            "Hunt elite armor through long-range kill zones.",
            "Seize the fortress approaches under blackout pressure.",
            "Break the final conventional defense network.",
            "Destroy the Zero Front and complete Operation Overdrive."
        };

        private TankGame _game;
        private int _round;
        private int _sector = -1;
        private int _lastResolvedSector = -1;
        private SectorRoute _route;
        private bool _choiceOpen;
        private float _choiceUntil;
        private float _bannerUntil;
        private string _banner = string.Empty;
        private int _sectorKills;
        private int _sectorHeavyKills;
        private int _sectorSpecialKills;
        private int _sectorStartBonds;
        private readonly HashSet<int> _hooked = new HashSet<int>();
        private float _scanAt;

        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _choice;
        private GUIStyle _bannerStyle;

        public static int CurrentSector => Mathf.Clamp((FindAnyObjectByType<TankGame>()?.CurrentRound ?? 1 - 1) / 10, 0, 9);
        public SectorRoute ActiveRoute => _route;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<OverdriveSectorCampaignDirector>() != null) return;
            var go = new GameObject("OverdriveSectorCampaignDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<OverdriveSectorCampaignDirector>();
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
                _round = 0;
                _hooked.Clear();
                _choiceOpen = false;
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round != _round)
            {
                int previousSector = _sector;
                _round = round;
                _sector = Mathf.Clamp((round - 1) / 10, 0, 9);
                _hooked.Clear();

                if (previousSector >= 0 && _sector != previousSector)
                    ResolveSector(previousSector);

                if (_sector != previousSector)
                    EnterSector(_sector);

                ApplyRoundRouteBonus(round);
            }

            if (_choiceOpen)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) SelectRoute(SectorRoute.Spearhead);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectRoute(SectorRoute.Bulwark);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectRoute(SectorRoute.Recon);
                else if (Time.unscaledTime >= _choiceUntil) SelectRoute(DefaultRouteForSector(_sector));
            }

            if (Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.3f;
                HookEnemies();
            }
        }

        private void EnterSector(int sector)
        {
            _sectorKills = 0;
            _sectorHeavyKills = 0;
            _sectorSpecialKills = 0;
            _sectorStartBonds = WarEconomyDirector.CurrentBonds;

            string key = $"TankRevival.SectorRoute.{sector}";
            int saved = PlayerPrefs.GetInt(key, 0);
            if (saved > 0)
            {
                _route = (SectorRoute)Mathf.Clamp(saved, 1, 3);
                Announce($"{SectorNames[sector]} // ROUTE {_route.ToString().ToUpperInvariant()}", 4.2f);
            }
            else
            {
                _route = SectorRoute.None;
                _choiceOpen = true;
                _choiceUntil = Time.unscaledTime + 10f;
                Announce($"STRATEGIC ROUTE // {SectorNames[sector]}", 10f);
            }

            int bestSector = PlayerPrefs.GetInt("TankRevival.BestSector", 0);
            if (sector + 1 > bestSector)
            {
                PlayerPrefs.SetInt("TankRevival.BestSector", sector + 1);
                PlayerPrefs.Save();
            }
        }

        private SectorRoute DefaultRouteForSector(int sector)
        {
            if (sector >= 8) return SectorRoute.Bulwark;
            if (sector == 6 || sector == 7) return SectorRoute.Recon;
            return SectorRoute.Spearhead;
        }

        private void SelectRoute(SectorRoute route)
        {
            _route = route;
            _choiceOpen = false;
            PlayerPrefs.SetInt($"TankRevival.SectorRoute.{_sector}", (int)route);
            PlayerPrefs.Save();

            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;

            switch (route)
            {
                case SectorRoute.Spearhead:
                    if (player != null)
                    {
                        player.AddAmmo(AmmoType.ArmorPiercing, 5 + _sector);
                        player.AddAmmo(AmmoType.Explosive, 3 + _sector / 2);
                        if (_sector >= 5) player.AddAmmo(AmmoType.Plasma, 2);
                        if (player.Health != null)
                            player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 2.2f);
                    }
                    Announce("SPEARHEAD ROUTE // OFFENSIVE ORDNANCE + ASSAULT TEMPO", 3.6f);
                    break;

                case SectorRoute.Bulwark:
                    if (player != null && player.Health != null)
                    {
                        player.Health.Heal(2 + _sector / 3);
                        player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 3f);
                        ArmorSystem armor = player.GetComponent<ArmorSystem>();
                        if (armor != null) armor.RepairModules(25 + _sector * 3);
                    }
                    if (eagle != null)
                        eagle.InvulnerableUntil = Mathf.Max(eagle.InvulnerableUntil, Time.time + 2f);
                    _game.RepairEagle(2);
                    Announce("BULWARK ROUTE // ORZELEK + ARMOR SUSTAIN", 3.6f);
                    break;

                case SectorRoute.Recon:
                    if (player != null)
                    {
                        player.AddAmmo(AmmoType.EMP, 2 + _sector / 2);
                        player.AddAmmo(AmmoType.ArmorPiercing, 3);
                    }
                    WarEconomyDirector.AwardMissionBonds(4 + _sector, "SECTOR RECON DEPLOYMENT");
                    Announce("RECON ROUTE // INTEL BONDS + EMP RESERVE", 3.6f);
                    break;
            }

            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.7f, 0f);
        }

        private void ApplyRoundRouteBonus(int round)
        {
            if (_route == SectorRoute.None) return;
            int localRound = ((round - 1) % 10) + 1;
            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;

            if (_route == SectorRoute.Spearhead)
            {
                if (player != null && localRound % 3 == 0)
                    player.AddAmmo(AmmoType.ArmorPiercing, 2 + _sector / 4);
                if (player != null && _sector >= 6 && localRound % 5 == 0)
                    player.AddAmmo(AmmoType.Plasma, 1);
            }
            else if (_route == SectorRoute.Bulwark)
            {
                if (localRound % 3 == 0) _game.RepairEagle(1);
                if (player != null && player.Health != null && localRound % 4 == 0)
                    player.Health.Heal(1);
                if (eagle != null && localRound == 10)
                    eagle.InvulnerableUntil = Mathf.Max(eagle.InvulnerableUntil, Time.time + 2.2f);
            }
            else if (_route == SectorRoute.Recon)
            {
                if (localRound == 4 || localRound == 8)
                    WarEconomyDirector.AwardMissionBonds(2 + _sector / 2, "RECON INTELLIGENCE");
                if (player != null && localRound % 5 == 0)
                    player.AddAmmo(AmmoType.EMP, 1);
            }
        }

        private void HookEnemies()
        {
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null) continue;
                int id = enemy.GetInstanceID();
                if (!_hooked.Add(id)) continue;
                EnemyTank captured = enemy;
                enemy.Health.Died += _ => OnEnemyDestroyed(captured);
            }
        }

        private void OnEnemyDestroyed(EnemyTank enemy)
        {
            if (enemy == null || _game == null || !_game.IsPlaying) return;
            _sectorKills++;
            if (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss)
                _sectorHeavyKills++;
            if (enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Sniper || enemy.Kind == EnemyKind.Supply || enemy.Kind == EnemyKind.Boss)
                _sectorSpecialKills++;

            if (_route == SectorRoute.Spearhead && _sectorKills % 12 == 0)
            {
                PlayerTank player = CombatRoster.Player;
                if (player != null)
                {
                    player.AddAmmo(AmmoType.Explosive, 1);
                    if (_sector >= 5) player.AddAmmo(AmmoType.Plasma, 1);
                }
            }

            if (_route == SectorRoute.Recon && _sectorSpecialKills > 0 && _sectorSpecialKills % 6 == 0)
                WarEconomyDirector.AwardMissionBonds(2, "PRIORITY TARGET INTEL");
        }

        private void ResolveSector(int sector)
        {
            if (sector < 0 || sector == _lastResolvedSector) return;
            _lastResolvedSector = sector;

            Health eagle = CombatRoster.Eagle;
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead || eagle == null || eagle.IsDead) return;

            int routeBonus = _route == SectorRoute.Recon ? 3 : _route == SectorRoute.Spearhead ? 2 : 1;
            int performance = Mathf.Clamp(_sectorHeavyKills / 4 + _sectorSpecialKills / 5, 0, 6);
            int reward = 6 + sector * 2 + routeBonus + performance;
            WarEconomyDirector.AwardMissionBonds(reward, $"SECTOR {sector + 1} SECURED");

            int completed = PlayerPrefs.GetInt("TankRevival.SectorsCompleted", 0);
            if (sector + 1 > completed)
                PlayerPrefs.SetInt("TankRevival.SectorsCompleted", sector + 1);

            int flawless = PlayerPrefs.GetInt("TankRevival.FlawlessSectors", 0);
            if (eagle.Current >= eagle.Max && player.Health.Current >= Mathf.Max(1, player.Health.Max - 1))
                PlayerPrefs.SetInt("TankRevival.FlawlessSectors", flawless + 1);

            PlayerPrefs.SetInt($"TankRevival.SectorKills.{sector}", Mathf.Max(PlayerPrefs.GetInt($"TankRevival.SectorKills.{sector}", 0), _sectorKills));
            PlayerPrefs.Save();

            int earned = Mathf.Max(0, WarEconomyDirector.CurrentBonds - _sectorStartBonds);
            Announce($"SECTOR SECURED // +{reward} BONDS // {earned} NET LOGISTICS", 4.5f);
        }

        private void Announce(string text, float duration)
        {
            _banner = text;
            _bannerUntil = Time.unscaledTime + duration;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.30f, 0.92f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.62f, 0.74f, 0.84f) } };
            _choice = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.84f, 0.28f) } };
            _bannerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _sector < 0) return;
            EnsureStyles();

            float x = 14f;
            float y = Screen.height - 128f;
            GUI.color = new Color(0.012f, 0.026f, 0.045f, 0.92f);
            GUI.Box(new Rect(x, y, 390f, 112f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 8f, 365f, 22f), SectorNames[_sector], _title);
            GUI.Label(new Rect(x + 12f, y + 31f, 365f, 20f), SectorOperations[_sector], _small);
            GUI.Label(new Rect(x + 12f, y + 53f, 365f, 20f), $"ROUTE: {(_route == SectorRoute.None ? "AWAITING ORDER" : _route.ToString().ToUpperInvariant())}", _body);
            GUI.Label(new Rect(x + 12f, y + 73f, 365f, 18f), $"SECTOR KILLS {_sectorKills}   HEAVY {_sectorHeavyKills}   SPECIAL {_sectorSpecialKills}", _small);
            GUI.Label(new Rect(x + 12f, y + 91f, 365f, 18f), $"WAR BONDS {WarEconomyDirector.CurrentBonds}   //   ROUND {_round}/100", _small);

            if (_choiceOpen)
            {
                float w = Mathf.Min(860f, Screen.width - 40f);
                float cx = (Screen.width - w) * 0.5f;
                float cy = Screen.height * 0.18f;
                GUI.color = new Color(0.008f, 0.018f, 0.034f, 0.97f);
                GUI.Box(new Rect(cx, cy, w, 190f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx + 20f, cy + 12f, w - 40f, 30f), $"SECTOR {_sector + 1} COMMAND DECISION", _bannerStyle);
                GUI.Label(new Rect(cx + 20f, cy + 52f, w - 40f, 26f), "[1] SPEARHEAD — AP/Explosive/Plasma sustain, kill-chain ordnance", _choice);
                GUI.Label(new Rect(cx + 20f, cy + 84f, w - 40f, 26f), "[2] BULWARK — Orzelek repairs, hull sustain, boss-entry protection", _choice);
                GUI.Label(new Rect(cx + 20f, cy + 116f, w - 40f, 26f), "[3] RECON — War Bond intelligence payouts, EMP reserve, priority-target bonuses", _choice);
                GUI.Label(new Rect(cx + 20f, cy + 153f, w - 40f, 22f), "No input: command selects the recommended route for this sector.", _small);
            }
            else if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.012f, 0.025f, 0.045f, 0.94f);
                GUI.Box(new Rect(Screen.width * 0.5f - 390f, Screen.height * 0.25f - 32f, 780f, 64f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 380f, Screen.height * 0.25f - 20f, 760f, 40f), _banner, _bannerStyle);
            }
        }
    }
}
