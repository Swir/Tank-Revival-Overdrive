using System;
using UnityEngine;

namespace TankRevival
{
    public enum TheaterSectorDoctrine
    {
        None = 0,
        Breakthrough = 1,
        SupplyStarved = 2,
        PreparedDefense = 3
    }

    [DefaultExecutionOrder(590)]
    public sealed class TheaterConsequenceEngineDirector : MonoBehaviour
    {
        public const int SectorCount = 10;
        public const int InitiativeMin = -3;
        public const int InitiativeMax = 3;
        public const int ConsequenceRounds = 4;
        public const int MaxBreakthroughShellsPerRound = 2;
        public const int InterdictionDamage = 2;
        public const int FortifyPlayerHeal = 1;
        public const int FortifyEagleHeal = 1;
        public const int NoTargetBondRelief = 1;

        private static TheaterConsequenceEngineDirector _instance;

        private readonly TheaterSectorDoctrine[] _sectorDoctrine = new TheaterSectorDoctrine[SectorCount];
        private readonly TheaterOrderKind[] _observedOrders = new TheaterOrderKind[SectorCount];
        private readonly SectorWarState[] _sourceWarState = new SectorWarState[SectorCount];
        private readonly int[] _initiativeAtEntry = new int[SectorCount];

        private TankGame _game;
        private int _round = -1;
        private int _initiative;
        private int _lastResolvedTargetSector = -1;
        private bool _effectAppliedThisRound;
        private float _effectAt;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _header;
        private GUIStyle _body;

        public static TheaterConsequenceEngineDirector Instance => _instance;
        public int TheaterInitiative => _initiative;
        public TheaterSectorDoctrine ActiveDoctrine => _round > 0 ? GetDoctrineForSector(WarStateCampaignMemoryDirector.SectorForRound(_round)) : TheaterSectorDoctrine.None;

        public static bool ConfigurationValid =>
            SectorCount == WarStateCampaignMemoryDirector.SectorCount &&
            InitiativeMin == -InitiativeMax && InitiativeMax == 3 &&
            ConsequenceRounds >= 3 && ConsequenceRounds <= 4 &&
            MaxBreakthroughShellsPerRound >= 1 && MaxBreakthroughShellsPerRound <= 2 &&
            InterdictionDamage >= 1 && InterdictionDamage <= 2 &&
            FortifyPlayerHeal == 1 && FortifyEagleHeal == 1 &&
            NoTargetBondRelief >= 0 && NoTargetBondRelief <= 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TheaterConsequenceEngineDirector>() != null) return;
            GameObject go = new GameObject("TheaterConsequenceEngineDirector_v10_2");
            DontDestroyOnLoad(go);
            go.AddComponent<TheaterConsequenceEngineDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetRun();
                return;
            }

            int current = Mathf.Clamp(_game.CurrentRound, 1, 100);
            ObserveCurrentOrder(current);

            if (current != _round)
            {
                _round = current;
                _effectAppliedThisRound = false;
                OnRoundChanged(current);
            }

            ObserveCurrentOrder(current);

            TheaterSectorDoctrine doctrine = ActiveDoctrine;
            if (!_effectAppliedThisRound && doctrine != TheaterSectorDoctrine.None && IsConsequenceActiveForRound(current))
            {
                if (_effectAt <= 0f) _effectAt = Time.time + 1.0f;
                if (Time.time >= _effectAt)
                {
                    _effectAppliedThisRound = true;
                    _effectAt = 0f;
                    ApplyDoctrineEffect(doctrine, current);
                }
            }
        }

        private void OnRoundChanged(int round)
        {
            _effectAt = 0f;
            int sector = WarStateCampaignMemoryDirector.SectorForRound(round);
            int sectorRound = WarStateCampaignMemoryDirector.SectorRound(round);

            if (sectorRound == 1 && sector > 0 && _lastResolvedTargetSector != sector)
                ResolveSectorTransition(sector - 1, sector);

            TheaterSectorDoctrine doctrine = GetDoctrineForSector(sector);
            if (doctrine != TheaterSectorDoctrine.None)
            {
                _status = "SECTOR " + (sector + 1) + " DOCTRINE // " + DoctrineLabel(doctrine) + " // INITIATIVE " + FormatSigned(_initiative);
                _statusUntil = Time.time + 4.0f;
            }
        }

        private void ObserveCurrentOrder(int round)
        {
            TheaterOrderDirector orders = TheaterOrderDirector.Instance;
            if (orders == null || orders.ActiveOrder == TheaterOrderKind.None) return;
            int sector = WarStateCampaignMemoryDirector.SectorForRound(round);
            if (sector < 0 || sector >= SectorCount) return;
            _observedOrders[sector] = orders.ActiveOrder;
        }

        private void ResolveSectorTransition(int sourceSector, int targetSector)
        {
            if (sourceSector < 0 || sourceSector >= SectorCount || targetSector < 0 || targetSector >= SectorCount) return;

            WarStateCampaignMemoryDirector memory = WarStateCampaignMemoryDirector.Instance;
            SectorWarState warState = memory != null ? memory.GetSectorResult(sourceSector) : SectorWarState.Unresolved;
            TheaterOrderKind observed = _observedOrders[sourceSector];
            int commandMomentum = CombinedArmsCampaignCommandDirector.Instance != null
                ? CombinedArmsCampaignCommandDirector.Instance.CommandMomentum
                : 0;

            TheaterSectorDoctrine doctrine = ResolveDoctrine(observed, warState, commandMomentum);
            _sectorDoctrine[targetSector] = doctrine;
            _sourceWarState[targetSector] = warState;
            _initiative = NextInitiative(_initiative, warState, commandMomentum);
            _initiativeAtEntry[targetSector] = _initiative;
            _lastResolvedTargetSector = targetSector;

            ApplySectorEntryConsequence(doctrine, targetSector);
            _status = "WAR BRANCH LOCKED // S" + (sourceSector + 1) + " → S" + (targetSector + 1) + " // " + DoctrineLabel(doctrine) + " // INITIATIVE " + FormatSigned(_initiative);
            _statusUntil = Time.time + 5.0f;
            BattleAudio.PlayGlobal(warState == SectorWarState.Defeat ? SoundCue.BossAlarm : SoundCue.RoundClear, 0.28f, warState == SectorWarState.Defeat ? -0.08f : 0f);
        }

        public TheaterSectorDoctrine GetDoctrineForSector(int sector)
        {
            if (sector < 0 || sector >= SectorCount) return TheaterSectorDoctrine.None;
            return _sectorDoctrine[sector];
        }

        public TheaterOrderKind GetObservedOrderForSector(int sector)
        {
            if (sector < 0 || sector >= SectorCount) return TheaterOrderKind.None;
            return _observedOrders[sector];
        }

        public SectorWarState GetSourceWarStateForSector(int sector)
        {
            if (sector < 0 || sector >= SectorCount) return SectorWarState.Unresolved;
            return _sourceWarState[sector];
        }

        public int GetInitiativeAtSectorEntry(int sector)
        {
            if (sector < 0 || sector >= SectorCount) return 0;
            return _initiativeAtEntry[sector];
        }

        public static TheaterSectorDoctrine ResolveDoctrine(TheaterOrderKind order, SectorWarState warState, int commandMomentum)
        {
            switch (order)
            {
                case TheaterOrderKind.Assault:
                    return TheaterSectorDoctrine.Breakthrough;
                case TheaterOrderKind.Interdiction:
                    return TheaterSectorDoctrine.SupplyStarved;
                case TheaterOrderKind.Fortify:
                    return TheaterSectorDoctrine.PreparedDefense;
            }

            if (warState == SectorWarState.Victory)
                return commandMomentum < -1 ? TheaterSectorDoctrine.SupplyStarved : TheaterSectorDoctrine.Breakthrough;
            if (warState == SectorWarState.Defeat)
                return TheaterSectorDoctrine.PreparedDefense;
            return commandMomentum > 1 ? TheaterSectorDoctrine.Breakthrough :
                   commandMomentum < -1 ? TheaterSectorDoctrine.PreparedDefense :
                   TheaterSectorDoctrine.SupplyStarved;
        }

        public static int NextInitiative(int current, SectorWarState warState, int commandMomentum)
        {
            int delta = warState == SectorWarState.Victory ? 1 : warState == SectorWarState.Defeat ? -1 : 0;
            if (delta == 0)
                delta = commandMomentum >= 2 ? 1 : commandMomentum <= -2 ? -1 : 0;
            return Mathf.Clamp(current + delta, InitiativeMin, InitiativeMax);
        }

        public static bool IsConsequenceActiveForRound(int round)
        {
            if (round < 1 || round > 100 || round % 10 == 0) return false;
            return WarStateCampaignMemoryDirector.SectorRound(round) <= ConsequenceRounds;
        }

        public static int BreakthroughShellCount(int round, int initiative)
        {
            int shells = 1;
            if (round >= 70 && initiative >= 2) shells++;
            return Mathf.Clamp(shells, 1, MaxBreakthroughShellsPerRound);
        }

        public static int ExpectedSectorForRound(int round)
        {
            return WarStateCampaignMemoryDirector.SectorForRound(Mathf.Clamp(round, 1, 100));
        }

        private void ApplySectorEntryConsequence(TheaterSectorDoctrine doctrine, int targetSector)
        {
            if (doctrine != TheaterSectorDoctrine.PreparedDefense) return;

            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;
            if (player != null && player.Health != null && !player.Health.IsDead)
                player.Health.Heal(FortifyPlayerHeal);
            if (eagle != null && !eagle.IsDead)
                eagle.Heal(FortifyEagleHeal);
            WarEconomyDirector.AwardMissionBonds(1, "PREPARED SECTOR DEFENSE");

            if (player != null) VisualFactory.RingPulse(player.transform.position, new Color(0.24f, 1f, 0.48f), 1.0f);
            if (eagle != null) VisualFactory.RingPulse(eagle.transform.position, new Color(0.24f, 1f, 0.48f), 1.2f);
        }

        private void ApplyDoctrineEffect(TheaterSectorDoctrine doctrine, int round)
        {
            switch (doctrine)
            {
                case TheaterSectorDoctrine.Breakthrough:
                    ApplyBreakthrough(round);
                    break;
                case TheaterSectorDoctrine.SupplyStarved:
                    ApplySupplyStarvation(round);
                    break;
                case TheaterSectorDoctrine.PreparedDefense:
                    ApplyPreparedDefense(round);
                    break;
            }
        }

        private void ApplyBreakthrough(int round)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int shells = BreakthroughShellCount(round, _initiative);
            int fired = 0;

            for (int pass = 0; pass < 3 && fired < shells; pass++)
            {
                for (int i = 0; i < enemies.Length && fired < shells; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                    bool priority = pass == 0 ? enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Heavy :
                                    pass == 1 ? enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Sniper : true;
                    if (!priority) continue;

                    Vector2 target = enemy.transform.position;
                    Vector2 origin = target + new Vector2(fired == 0 ? -0.34f : 0.34f, 5.2f);
                    _game.SpawnProjectile(origin, Vector2.down, Team.Player, 2, 11.2f, new Color(1f, 0.46f, 0.12f), AmmoType.Explosive);
                    VisualFactory.RingPulse(target, new Color(1f, 0.46f, 0.12f), 0.74f);
                    fired++;
                }
            }

            _status = "BREAKTHROUGH DOCTRINE // " + fired + " HE SUPPORT SHELL" + (fired == 1 ? string.Empty : "S");
            _statusUntil = Time.time + 2.6f;
        }

        private void ApplySupplyStarvation(int round)
        {
            Health target = FindInfrastructureTarget();
            if (target != null && !target.IsDead)
            {
                Vector2 targetPos = target.transform.position;
                Vector2 origin = targetPos + new Vector2(-3.8f, 3.0f);
                _game.SpawnProjectile(origin, (targetPos - origin).normalized, Team.Player, InterdictionDamage, 12.4f, new Color(0.18f, 0.82f, 1f), AmmoType.ArmorPiercing);
                VisualFactory.RingPulse(targetPos, new Color(0.18f, 0.82f, 1f), 0.82f);
                _status = "SUPPLY-STARVED SECTOR // AP STRIKE ON " + target.name;
            }
            else
            {
                if (NoTargetBondRelief > 0 && WarStateCampaignMemoryDirector.SectorRound(round) == 1)
                    WarEconomyDirector.AwardMissionBonds(NoTargetBondRelief, "ENEMY SUPPLY DENIAL");
                _status = "SUPPLY-STARVED SECTOR // ENEMY INFRASTRUCTURE ABSENT";
            }
            _statusUntil = Time.time + 2.6f;
        }

        private void ApplyPreparedDefense(int round)
        {
            int sectorRound = WarStateCampaignMemoryDirector.SectorRound(round);
            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;

            if (sectorRound == 2 && player != null && player.Health != null && !player.Health.IsDead)
            {
                player.Health.Heal(FortifyPlayerHeal);
                VisualFactory.RingPulse(player.transform.position, new Color(0.24f, 1f, 0.48f), 0.85f);
            }
            else if (sectorRound == 4 && eagle != null && !eagle.IsDead)
            {
                eagle.Heal(FortifyEagleHeal);
                VisualFactory.RingPulse(eagle.transform.position, new Color(0.24f, 1f, 0.48f), 1.0f);
            }

            _status = "PREPARED DEFENSE // SECTOR SUSTAIN // INITIATIVE " + FormatSigned(_initiative);
            _statusUntil = Time.time + 2.4f;
        }

        private static Health FindInfrastructureTarget()
        {
            Health[] health = RuntimeBattleRegistry.HealthSnapshot;
            Health logistics = null;
            for (int i = 0; i < health.Length; i++)
            {
                Health h = health[i];
                if (h == null || h.IsDead || h.Team != Team.Enemy) continue;
                string n = h.gameObject.name;
                if (n == "ENEMY_THEATER_COMMAND_RELAY_V100") return h;
                if (n.StartsWith("ENEMY_LOGISTICS_", StringComparison.Ordinal)) logistics = h;
            }
            return logistics;
        }

        private void ResetRun()
        {
            for (int i = 0; i < SectorCount; i++)
            {
                _sectorDoctrine[i] = TheaterSectorDoctrine.None;
                _observedOrders[i] = TheaterOrderKind.None;
                _sourceWarState[i] = SectorWarState.Unresolved;
                _initiativeAtEntry[i] = 0;
            }
            _round = -1;
            _initiative = 0;
            _lastResolvedTargetSector = -1;
            _effectAppliedThisRound = false;
            _effectAt = 0f;
            _status = string.Empty;
            _statusUntil = 0f;
        }

        private static string DoctrineLabel(TheaterSectorDoctrine doctrine)
        {
            switch (doctrine)
            {
                case TheaterSectorDoctrine.Breakthrough: return "BREAKTHROUGH";
                case TheaterSectorDoctrine.SupplyStarved: return "SUPPLY STARVED";
                case TheaterSectorDoctrine.PreparedDefense: return "PREPARED DEFENSE";
                default: return "UNRESOLVED";
            }
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? "+" + value : value.ToString();
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.72f, 0.25f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || Time.time > _statusUntil) return;
            EnsureStyles();
            int sector = WarStateCampaignMemoryDirector.SectorForRound(Mathf.Max(1, _round));
            Rect box = new Rect(14f, Screen.height - 158f, 590f, 42f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 10f, box.y + 4f, 570f, 18f), "THEATER CONSEQUENCE ENGINE // v10.2 // SECTOR " + (sector + 1), _header);
            GUI.Label(new Rect(box.x + 10f, box.y + 22f, 570f, 16f), _status, _body);
        }
    }
}
