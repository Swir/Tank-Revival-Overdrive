using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum EncounterArchetype
    {
        FrontlineAssault,
        ArmoredColumn,
        Wolfpack,
        SniperNet,
        SiegePush,
        ArtilleryScreen,
        SupplyInterdiction,
        EliteHunt,
        LastStand,
        BossGauntlet
    }

    public readonly struct RoundEncounterProfile
    {
        public readonly int Round;
        public readonly int Sector;
        public readonly int SectorRound;
        public readonly EncounterArchetype Archetype;
        public readonly string Codename;
        public readonly string Objective;
        public readonly float HealthMultiplier;
        public readonly float FireSupportMultiplier;
        public readonly float StrikeCadence;
        public readonly bool ChampionEnabled;
        public readonly bool StrategicStrikes;
        public readonly bool BossRound;

        public RoundEncounterProfile(
            int round,
            int sector,
            int sectorRound,
            EncounterArchetype archetype,
            string codename,
            string objective,
            float healthMultiplier,
            float fireSupportMultiplier,
            float strikeCadence,
            bool championEnabled,
            bool strategicStrikes,
            bool bossRound)
        {
            Round = round;
            Sector = sector;
            SectorRound = sectorRound;
            Archetype = archetype;
            Codename = codename;
            Objective = objective;
            HealthMultiplier = healthMultiplier;
            FireSupportMultiplier = fireSupportMultiplier;
            StrikeCadence = strikeCadence;
            ChampionEnabled = championEnabled;
            StrategicStrikes = strategicStrikes;
            BossRound = bossRound;
        }
    }

    /// <summary>
    /// v2.8 campaign pacing layer. It does not replace TankGame spawning or EnemyTank AI.
    /// Instead it turns the existing 100 rounds into authored encounter archetypes by modifying
    /// real spawned enemies, adding telegraphed support attacks and assigning high-value targets.
    /// </summary>
    [DefaultExecutionOrder(220)]
    public sealed class CampaignEncounterDirector : MonoBehaviour
    {
        public static CampaignEncounterDirector Instance { get; private set; }

        private TankGame _game;
        private int _activeRound = -1;
        private RoundEncounterProfile _profile;
        private float _nextScan;
        private float _nextStrike;
        private float _briefUntil;
        private string _eventMessage = string.Empty;
        private float _eventUntil;
        private int _configuredThisRound;
        private int _championInstanceId;
        private bool _championResolved;
        private int _strategicStrikesFired;
        private readonly HashSet<int> _configured = new HashSet<int>();
        private readonly List<EnemyTank> _buffer = new List<EnemyTank>(32);

        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _objective;
        private GUIStyle _event;

        public RoundEncounterProfile ActiveProfile => _profile;
        public bool HasActiveProfile => _activeRound > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CampaignEncounterDirector>() != null) return;
            var go = new GameObject("CampaignEncounterDirector_v2_8");
            DontDestroyOnLoad(go);
            go.AddComponent<CampaignEncounterDirector>();
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
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                if (_game == null) return;
            }

            if (!_game.IsPlaying)
            {
                _activeRound = -1;
                _configured.Clear();
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round != _activeRound)
                BeginEncounter(round);

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.34f;
                ConfigureNewEnemies();
            }

            if (_profile.StrategicStrikes && Time.time >= _nextStrike)
            {
                float progress = (_profile.Round - 1f) / 99f;
                float jitter = Random.Range(0.88f, 1.18f);
                _nextStrike = Time.time + Mathf.Max(4.6f, _profile.StrikeCadence * Mathf.Lerp(1f, 0.80f, progress)) * jitter;
                if (_strategicStrikesFired < StrikeBudget(_profile))
                {
                    _strategicStrikesFired++;
                    StartCoroutine(StrategicStrike());
                }
            }
        }

        private void BeginEncounter(int round)
        {
            _activeRound = round;
            _profile = Resolve(round);
            _configured.Clear();
            _configuredThisRound = 0;
            _championInstanceId = 0;
            _championResolved = false;
            _strategicStrikesFired = 0;
            _nextScan = Time.time + 0.18f;
            _nextStrike = Time.time + Mathf.Max(3.8f, _profile.StrikeCadence * 0.72f);
            _briefUntil = Time.unscaledTime + (_profile.BossRound ? 4.8f : 3.4f);
            _eventMessage = string.Empty;
            _eventUntil = 0f;
        }

        private void ConfigureNewEnemies()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            _buffer.Clear();

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                _buffer.Add(enemy);
                int id = enemy.GetInstanceID();
                if (_configured.Contains(id)) continue;

                _configured.Add(id);
                _configuredThisRound++;

                bool champion = ShouldPromoteChampion(enemy);
                var modifier = enemy.GetComponent<EncounterCombatant>();
                if (modifier == null) modifier = enemy.gameObject.AddComponent<EncounterCombatant>();
                modifier.Configure(this, _game, _profile, champion);

                if (champion)
                {
                    _championInstanceId = id;
                    Broadcast("HIGH-VALUE TARGET DEPLOYED");
                }
            }

            _configured.RemoveWhere(id => !ContainsInstance(id));
        }

        private bool ContainsInstance(int id)
        {
            for (int i = 0; i < _buffer.Count; i++)
                if (_buffer[i] != null && _buffer[i].GetInstanceID() == id) return true;
            return false;
        }

        private bool ShouldPromoteChampion(EnemyTank enemy)
        {
            if (!_profile.ChampionEnabled || _championInstanceId != 0 || _championResolved) return false;
            if (enemy.Kind == EnemyKind.Supply) return false;
            if (_profile.BossRound) return enemy.Kind == EnemyKind.Boss;

            switch (_profile.Archetype)
            {
                case EncounterArchetype.EliteHunt:
                    return enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Heavy || _configuredThisRound >= 4;
                case EncounterArchetype.ArmoredColumn:
                    return enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || _configuredThisRound >= 5;
                case EncounterArchetype.SniperNet:
                    return enemy.Kind == EnemyKind.Sniper || _configuredThisRound >= 5;
                case EncounterArchetype.SiegePush:
                    return enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Heavy || _configuredThisRound >= 5;
                default:
                    return _configuredThisRound >= 6;
            }
        }

        public void OnChampionDestroyed(EncounterCombatant champion)
        {
            if (_championResolved) return;
            _championResolved = true;
            _championInstanceId = 0;
            Broadcast("TARGET DESTROYED // COUNTERATTACK WINDOW");

            if (_game != null && _game.IsPlaying)
                _game.RepairEagle(_profile.BossRound ? 1 : (_profile.Round % 20 == 0 ? 1 : 0));

            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                CombatStatus status = enemy.GetComponent<CombatStatus>();
                if (status != null) status.ApplyEmp(_profile.BossRound ? 0.55f : 0.90f);
            }
        }

        public void Broadcast(string message)
        {
            _eventMessage = message;
            _eventUntil = Time.unscaledTime + 2.2f;
        }

        private IEnumerator StrategicStrike()
        {
            if (_game == null || !_game.IsPlaying) yield break;

            bool pressureEagle = _profile.Archetype == EncounterArchetype.SiegePush ||
                                 _profile.Archetype == EncounterArchetype.BossGauntlet ||
                                 (_profile.Archetype == EncounterArchetype.ArtilleryScreen && Random.value < 0.42f);

            Vector2 anchor = pressureEagle ? _game.BasePosition : _game.PlayerPosition;
            float spread = pressureEagle ? 1.35f : 1.85f;
            Vector2 target = anchor + Random.insideUnitCircle * spread;
            target.x = Mathf.Clamp(target.x, -10.7f, 10.7f);
            target.y = Mathf.Clamp(target.y, -5.65f, 5.25f);

            float radius = _profile.BossRound ? 1.20f : _profile.Round >= 60 ? 1.05f : 0.88f;
            Color warning = _profile.BossRound ? new Color(1f, 0.08f, 0.04f) : new Color(1f, 0.36f, 0.08f);
            VisualFactory.RingPulse(target, warning, radius * 1.28f);
            yield return new WaitForSeconds(Mathf.Lerp(1.05f, 0.68f, (_profile.Round - 1f) / 99f));

            if (_game == null || !_game.IsPlaying) yield break;
            VisualFactory.Explosion(target, warning, radius * 1.20f);
            DamagePlayersInRadius(target, radius, _profile.BossRound && _profile.Round >= 70 ? 2 : 1);

            if (_profile.Round >= 45 && Random.value < 0.34f)
            {
                yield return new WaitForSeconds(0.18f);
                Vector2 follow = target + Random.insideUnitCircle * 1.4f;
                VisualFactory.RingPulse(follow, warning, radius * 0.92f);
                yield return new WaitForSeconds(0.62f);
                VisualFactory.Explosion(follow, warning, radius);
                DamagePlayersInRadius(follow, radius * 0.86f, 1);
            }
        }

        private static void DamagePlayersInRadius(Vector2 center, float radius, int damage)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;
                Health health = hit.GetComponent<Health>();
                if (health != null && !health.IsDead && health.Team == Team.Player)
                    health.Damage(Mathf.Max(1, damage), Team.Enemy);
            }
        }

        private static int StrikeBudget(RoundEncounterProfile profile)
        {
            int baseBudget = profile.BossRound ? 7 : profile.Archetype == EncounterArchetype.ArtilleryScreen ? 5 : 3;
            if (profile.Round >= 70) baseBudget++;
            if (profile.Round >= 90) baseBudget++;
            return baseBudget;
        }

        public static RoundEncounterProfile Resolve(int round)
        {
            round = Mathf.Clamp(round, 1, 100);
            int sector = Mathf.Clamp((round - 1) / 10, 0, 9);
            int local = ((round - 1) % 10) + 1;
            float pressure = (round - 1f) / 99f;

            EncounterArchetype archetype;
            string code;
            string objective;
            bool champion = false;
            bool strikes = false;
            float cadence = Mathf.Lerp(12.5f, 7.0f, pressure);

            if (local == 10)
            {
                archetype = EncounterArchetype.BossGauntlet;
                code = BossCodename(sector);
                objective = "BREAK THE COMMAND TANK // PROTECT ORZELEK";
                champion = true;
                strikes = round >= 20;
                cadence = Mathf.Lerp(10.0f, 5.8f, pressure);
            }
            else if (local == 5)
            {
                archetype = EncounterArchetype.EliteHunt;
                code = "HUNTER KILLZONE";
                objective = "DESTROY THE HIGH-VALUE TARGET";
                champion = true;
            }
            else
            {
                switch ((local + sector * 3) % 8)
                {
                    case 0:
                        archetype = EncounterArchetype.ArmoredColumn;
                        code = "STEEL COLUMN";
                        objective = "BREAK THE ARMORED SPEARHEAD";
                        champion = round >= 22;
                        break;
                    case 1:
                        archetype = EncounterArchetype.Wolfpack;
                        code = "WOLFPACK RUN";
                        objective = "STOP FAST FLANKERS BEFORE THEY SPLIT THE LINE";
                        break;
                    case 2:
                        archetype = EncounterArchetype.SniperNet;
                        code = "LONG SIGHT";
                        objective = "DISMANTLE THE FIRE-SUPPORT NETWORK";
                        champion = round >= 31;
                        break;
                    case 3:
                        archetype = EncounterArchetype.SiegePush;
                        code = "IRON HAMMER";
                        objective = "INTERCEPT SIEGE UNITS BEFORE ORZELEK";
                        champion = round >= 42;
                        strikes = round >= 35;
                        break;
                    case 4:
                        archetype = EncounterArchetype.ArtilleryScreen;
                        code = "FALLING THUNDER";
                        objective = "FIGHT THROUGH TELEGRAPHED ARTILLERY";
                        strikes = round >= 18;
                        cadence *= 0.86f;
                        break;
                    case 5:
                        archetype = EncounterArchetype.SupplyInterdiction;
                        code = "BROKEN CONVOY";
                        objective = "HUNT SUPPLY CARRIERS FOR AMMUNITION";
                        break;
                    case 6:
                        archetype = EncounterArchetype.LastStand;
                        code = "NO STEP BACK";
                        objective = "SURVIVE THE HARDENED ASSAULT";
                        champion = round >= 55;
                        break;
                    default:
                        archetype = EncounterArchetype.FrontlineAssault;
                        code = "FRONTLINE PRESSURE";
                        objective = "HOLD THE LINE AND PRESERVE ORZELEK";
                        break;
                }
            }

            float health = 1f;
            float support = 1f;
            switch (archetype)
            {
                case EncounterArchetype.ArmoredColumn: health = 1.18f; support = 0.92f; break;
                case EncounterArchetype.Wolfpack: health = 0.96f; support = 1.18f; break;
                case EncounterArchetype.SniperNet: health = 1.02f; support = 1.24f; break;
                case EncounterArchetype.SiegePush: health = 1.14f; support = 1.18f; break;
                case EncounterArchetype.ArtilleryScreen: health = 1.00f; support = 1.12f; break;
                case EncounterArchetype.SupplyInterdiction: health = 1.04f; support = 0.96f; break;
                case EncounterArchetype.EliteHunt: health = 1.10f; support = 1.08f; break;
                case EncounterArchetype.LastStand: health = 1.20f; support = 1.10f; break;
                case EncounterArchetype.BossGauntlet: health = 1.16f; support = 1.24f; break;
            }

            health *= Mathf.Lerp(1f, 1.06f, pressure);
            return new RoundEncounterProfile(round, sector, local, archetype, code, objective, health, support, cadence, champion, strikes, local == 10);
        }

        private static string BossCodename(int sector)
        {
            string[] names =
            {
                "GATEKEEPER",
                "IRON WARDEN",
                "ASH COMMANDER",
                "STORM MARSHAL",
                "RIVER TYRANT",
                "FROST COLOSSUS",
                "FORTRESS BREAKER",
                "NIGHT EXECUTIONER",
                "BURNING CROWN",
                "OVERDRIVE ZERO"
            };
            return names[Mathf.Clamp(sector, 0, names.Length - 1)];
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.62f, 0.16f) }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                normal = { textColor = new Color(0.78f, 0.88f, 0.96f) }
            };
            _objective = new GUIStyle(_body)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.42f, 0.92f, 1f) }
            };
            _event = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.74f, 0.20f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _activeRound <= 0) return;
            EnsureStyles();

            if (Time.unscaledTime <= _briefUntil)
            {
                float width = Mathf.Min(390f, Screen.width - 28f);
                Rect panel = new Rect(14f, 84f, width, 72f);
                Color old = GUI.color;
                GUI.color = new Color(0.025f, 0.04f, 0.06f, 0.82f);
                GUI.Box(panel, string.Empty);
                GUI.color = old;
                GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 22f), $"R{_profile.Round:000} // {_profile.Codename}", _header);
                GUI.Label(new Rect(panel.x + 12f, panel.y + 31f, panel.width - 24f, 17f), _profile.Archetype.ToString().ToUpperInvariant(), _body);
                GUI.Label(new Rect(panel.x + 12f, panel.y + 49f, panel.width - 24f, 17f), _profile.Objective, _objective);
            }

            if (!string.IsNullOrEmpty(_eventMessage) && Time.unscaledTime <= _eventUntil)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 210f, 66f, 420f, 28f), _eventMessage, _event);
            }
        }
    }
}
