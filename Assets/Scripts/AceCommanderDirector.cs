using UnityEngine;

namespace TankRevival
{
    public enum AceArchetype { PrecisionHunter, Breaker, BlitzRaider, SiegeMarshal }

    /// <summary>
    /// v5.6: promotes rare existing enemies into named high-value threats. EnemyTank, Health,
    /// Projectile and TankGame remain authoritative; this layer adds bounded endurance, telegraphed
    /// special pressure and War Bond bounties without introducing a parallel combat system.
    /// </summary>
    public sealed class AceCommanderDirector : MonoBehaviour
    {
        private sealed class ActiveAce
        {
            public EnemyTank Enemy;
            public Health Health;
            public AceArchetype Archetype;
            public string Callsign;
            public int Round;
            public int Bounty;
            public float Deadline;
            public float NextSpecial;
            public bool RewardExpired;
        }

        private static readonly string[] HunterNames = { "LYNX", "VIPER", "SPECTRE", "FALCON" };
        private static readonly string[] BreakerNames = { "ANVIL", "RAM", "HAMMER", "BRUTUS" };
        private static readonly string[] BlitzNames = { "WRAITH", "DASH", "RAPTOR", "COMET" };
        private static readonly string[] SiegeNames = { "BASTION", "MORTAR", "TITAN", "IRON CROWN" };

        public static AceCommanderDirector Instance { get; private set; }
        public static int ArchetypeCount => 4;
        public bool HasActiveAce => _ace != null && IsAlive(_ace.Health);
        public string ActiveCallsign => HasActiveAce ? _ace.Callsign : string.Empty;
        public int ActiveBounty => HasActiveAce ? _ace.Bounty : 0;
        public float ActiveTimeRemaining => HasActiveAce ? Mathf.Max(0f, _ace.Deadline - Time.time) : 0f;
        public int AcesPromoted { get; private set; }
        public int BountiesClaimed { get; private set; }
        public int BountiesExpired { get; private set; }

        private TankGame _game;
        private ActiveAce _ace;
        private int _lastRound;
        private int _promotionRound = -1;
        private float _nextScan;
        private string _banner = string.Empty;
        private float _bannerUntil;
        private GUIStyle _title, _body, _timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<AceCommanderDirector>() != null) return;
            var go = new GameObject("AceCommanderDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<AceCommanderDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
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
                ClearAce();
                _lastRound = 0;
                _promotionRound = -1;
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (_lastRound != round)
            {
                _lastRound = round;
                if (_ace != null && !IsAlive(_ace.Health)) ClearAce();
            }

            if (_ace != null)
            {
                if (!IsAlive(_ace.Health)) { ClearAce(); return; }
                if (!_ace.RewardExpired && Time.time >= _ace.Deadline)
                {
                    _ace.RewardExpired = true;
                    BountiesExpired++;
                    _banner = $"ACE BOUNTY EXPIRED // {_ace.Callsign} STILL ACTIVE";
                    _bannerUntil = Time.unscaledTime + 3f;
                }
                if (Time.time >= _ace.NextSpecial) FireAceSpecial(_ace);
                return;
            }

            if (!IsAceRound(round) || _promotionRound == round || Time.time < _nextScan) return;
            _nextScan = Time.time + 0.65f;
            EnemyTank candidate = SelectCandidate(round);
            if (candidate != null) Promote(candidate, ResolveArchetype(candidate.Kind, round), round);
        }

        public static bool IsAceRound(int round)
        {
            round = Mathf.Clamp(round, 1, 100);
            return round >= 12 && (round == 12 || round % 7 == 0 || round % 10 == 5);
        }

        public static float HealthMultiplier(AceArchetype archetype, int round)
        {
            float progression = Mathf.Lerp(1f, 1.16f, Mathf.Clamp01((round - 1f) / 99f));
            float scale = archetype == AceArchetype.SiegeMarshal ? 1.42f
                : archetype == AceArchetype.Breaker ? 1.36f
                : archetype == AceArchetype.BlitzRaider ? 1.18f : 1.24f;
            return Mathf.Clamp(scale * progression, 1.15f, 1.64f);
        }

        public static float SpecialCadence(AceArchetype archetype, int round)
        {
            float baseCadence = archetype == AceArchetype.BlitzRaider ? 4.2f
                : archetype == AceArchetype.PrecisionHunter ? 5.8f
                : archetype == AceArchetype.Breaker ? 6.3f : 7.0f;
            float late = Mathf.Lerp(1f, 0.82f, Mathf.Clamp01((round - 1f) / 99f));
            return Mathf.Clamp(baseCadence * late, 3.4f, 7.4f);
        }

        public static int BountyFor(AceArchetype archetype, int round)
        {
            int value = 8 + Mathf.Clamp(round, 1, 100) / 12;
            if (archetype == AceArchetype.SiegeMarshal) value += 5;
            else if (archetype == AceArchetype.Breaker) value += 3;
            else if (archetype == AceArchetype.PrecisionHunter) value += 2;
            return Mathf.Clamp(value, 8, 24);
        }

        public static float BountyWindow(int round) => Mathf.Lerp(31f, 22f, Mathf.Clamp01((round - 1f) / 99f));

        public static int ApplyHealthMutationForValidation(Health health, AceArchetype archetype, int round)
        {
            if (health == null || health.IsDead) return 0;
            int before = Mathf.Max(1, health.Maximum);
            int target = Mathf.Max(before + 1, Mathf.CeilToInt(before * HealthMultiplier(archetype, round)));
            health.SetMaximum(target, true);
            return health.Maximum;
        }

        public static AmmoType SpecialAmmo(AceArchetype archetype)
        {
            if (archetype == AceArchetype.Breaker) return AmmoType.ArmorPiercing;
            if (archetype == AceArchetype.BlitzRaider) return AmmoType.EMP;
            if (archetype == AceArchetype.SiegeMarshal) return AmmoType.Explosive;
            return AmmoType.Basic;
        }

        public static string ArchetypeLabel(AceArchetype archetype)
        {
            if (archetype == AceArchetype.Breaker) return "BREAKER";
            if (archetype == AceArchetype.BlitzRaider) return "BLITZ RAIDER";
            if (archetype == AceArchetype.SiegeMarshal) return "SIEGE MARSHAL";
            return "PRECISION HUNTER";
        }

        private EnemyTank SelectCandidate(int round)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            EnemyTank best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!Eligible(enemy)) continue;
                float score = CandidateWeight(enemy.Kind) + Mathf.Abs((enemy.GetInstanceID() * 397) ^ (round * 7919)) % 1000 * 0.001f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = enemy;
            }
            return best;
        }

        private static bool Eligible(EnemyTank enemy)
        {
            if (enemy == null || enemy.Health == null || enemy.Health.IsDead || !enemy.gameObject.activeInHierarchy) return false;
            if (enemy.Kind == EnemyKind.Supply || enemy.Kind == EnemyKind.Boss) return false;
            return enemy.GetComponent<AceCommanderMarker>() == null;
        }

        private static float CandidateWeight(EnemyKind kind)
        {
            if (kind == EnemyKind.Elite) return 8f;
            if (kind == EnemyKind.Siege) return 7f;
            if (kind == EnemyKind.Heavy) return 6f;
            if (kind == EnemyKind.Sniper) return 5f;
            if (kind == EnemyKind.Fast) return 4f;
            return 2f;
        }

        private static AceArchetype ResolveArchetype(EnemyKind kind, int round)
        {
            if (kind == EnemyKind.Sniper) return AceArchetype.PrecisionHunter;
            if (kind == EnemyKind.Siege) return AceArchetype.SiegeMarshal;
            if (kind == EnemyKind.Fast) return AceArchetype.BlitzRaider;
            if (kind == EnemyKind.Heavy || kind == EnemyKind.Elite) return AceArchetype.Breaker;
            return (AceArchetype)(Mathf.Abs(round * 17 + (int)kind * 5) % ArchetypeCount);
        }

        private void Promote(EnemyTank enemy, AceArchetype archetype, int round)
        {
            Health health = enemy.Health;
            if (health == null || health.IsDead) return;
            ApplyHealthMutationForValidation(health, archetype, round);
            var marker = enemy.gameObject.AddComponent<AceCommanderMarker>();
            string callsign = Callsign(archetype, round);
            marker.Configure(archetype, callsign);

            _ace = new ActiveAce
            {
                Enemy = enemy, Health = health, Archetype = archetype, Callsign = callsign, Round = round,
                Bounty = BountyFor(archetype, round), Deadline = Time.time + BountyWindow(round),
                NextSpecial = Time.time + Mathf.Max(2.2f, SpecialCadence(archetype, round) * 0.55f)
            };
            _promotionRound = round;
            AcesPromoted++;
            health.Died += OnAceDied;
            Color c = AceColor(archetype);
            VisualFactory.RingPulse(enemy.transform.position, c, 1.65f);
            VisualFactory.RingPulse(enemy.transform.position, c, 2.35f);
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.42f, -0.03f);
            _banner = $"ACE COMMANDER // {callsign} // BOUNTY {_ace.Bounty} BONDS";
            _bannerUntil = Time.unscaledTime + 4f;
        }

        private void OnAceDied(Health health)
        {
            if (_ace == null || health != _ace.Health) return;
            if (!_ace.RewardExpired && Time.time <= _ace.Deadline)
            {
                int reward = _ace.Bounty;
                BountiesClaimed++;
                WarEconomyDirector.AwardMissionBonds(reward, $"ACE {_ace.Callsign}");
                _banner = $"ACE DESTROYED // {_ace.Callsign} // +{reward} WAR BONDS";
                _bannerUntil = Time.unscaledTime + 4f;
                BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.58f, 0.04f);
            }
            else
            {
                _banner = $"ACE DESTROYED // {_ace.Callsign}";
                _bannerUntil = Time.unscaledTime + 2.8f;
            }
            ClearAce();
        }

        private void FireAceSpecial(ActiveAce ace)
        {
            if (_game == null || ace == null || ace.Enemy == null || !IsAlive(ace.Health)) return;
            ace.NextSpecial = Time.time + SpecialCadence(ace.Archetype, ace.Round) * Random.Range(0.88f, 1.14f);
            Vector2 origin = ace.Enemy.transform.position;
            Vector2 target = ace.Archetype == AceArchetype.SiegeMarshal || ace.Archetype == AceArchetype.Breaker ? _game.BasePosition : _game.PlayerPosition;
            Vector2 delta = target - origin;
            if (delta.sqrMagnitude < 1f || delta.sqrMagnitude > 196f) return;
            Vector2 dir = delta.normalized;
            Color color = AceColor(ace.Archetype);
            int shots = ace.Archetype == AceArchetype.BlitzRaider ? 3 : ace.Archetype == AceArchetype.PrecisionHunter ? 1 : 2;
            int damage = ace.Archetype == AceArchetype.SiegeMarshal ? 2 : 1;
            float speed = ace.Archetype == AceArchetype.PrecisionHunter ? 13.8f : ace.Archetype == AceArchetype.BlitzRaider ? 10.4f : 9.2f;
            for (int i = 0; i < shots; i++)
            {
                float spread = (i - (shots - 1) * 0.5f) * (ace.Archetype == AceArchetype.BlitzRaider ? 0.10f : 0.055f);
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 shotDir = (dir + side * spread).normalized;
                Vector2 muzzle = origin + shotDir * 0.88f;
                _game.SpawnProjectile(muzzle, shotDir, Team.Enemy, damage, speed, color, SpecialAmmo(ace.Archetype));
                VisualFactory.MuzzleFlash(muzzle, color, ace.Archetype == AceArchetype.SiegeMarshal ? 1.05f : 0.72f);
            }
            BattleAudio.PlayGlobal(ace.Archetype == AceArchetype.SiegeMarshal || ace.Archetype == AceArchetype.Breaker ? SoundCue.HeavyShot : SoundCue.EnemyShot, 0.26f, 0.06f);
        }

        private void ClearAce()
        {
            if (_ace != null && _ace.Health != null) _ace.Health.Died -= OnAceDied;
            _ace = null;
        }

        private static bool IsAlive(Health health) => health != null && !health.IsDead && health.gameObject.activeInHierarchy;

        private static string Callsign(AceArchetype archetype, int round)
        {
            string[] names = archetype == AceArchetype.Breaker ? BreakerNames : archetype == AceArchetype.BlitzRaider ? BlitzNames : archetype == AceArchetype.SiegeMarshal ? SiegeNames : HunterNames;
            return names[Mathf.Abs(round * 31 + (int)archetype * 11) % names.Length];
        }

        public static Color AceColor(AceArchetype archetype)
        {
            if (archetype == AceArchetype.Breaker) return new Color(1f, 0.24f, 0.08f);
            if (archetype == AceArchetype.BlitzRaider) return new Color(0.24f, 0.92f, 1f);
            if (archetype == AceArchetype.SiegeMarshal) return new Color(1f, 0.64f, 0.10f);
            return new Color(0.92f, 0.30f, 1f);
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.56f, 0.16f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.92f, 0.94f, 0.98f) } };
            _timer = new GUIStyle(_title) { alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(1f, 0.24f, 0.12f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            if (HasActiveAce)
            {
                float x = Mathf.Max(14f, Screen.width - 344f), y = 88f;
                GUI.color = new Color(0.035f, 0.018f, 0.022f, 0.92f);
                GUI.Box(new Rect(x, y, 330f, 82f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 12f, y + 8f, 220f, 20f), $"ACE // {_ace.Callsign}", _title);
                GUI.Label(new Rect(x + 12f, y + 31f, 260f, 18f), ArchetypeLabel(_ace.Archetype), _body);
                GUI.Label(new Rect(x + 12f, y + 52f, 260f, 18f), $"BOUNTY {_ace.Bounty} WAR BONDS", _body);
                GUI.Label(new Rect(x + 236f, y + 9f, 80f, 20f), _ace.RewardExpired ? "EXPIRED" : $"{ActiveTimeRemaining:0.0}s", _timer);
            }
            if (Time.unscaledTime < _bannerUntil && !string.IsNullOrEmpty(_banner))
            {
                var style = new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
                GUI.Label(new Rect(Screen.width * 0.5f - 300f, 52f, 600f, 28f), _banner, style);
            }
        }
    }

    public sealed class AceCommanderMarker : MonoBehaviour
    {
        public AceArchetype Archetype { get; private set; }
        public string Callsign { get; private set; }
        private float _nextPulse;

        public void Configure(AceArchetype archetype, string callsign)
        {
            Archetype = archetype;
            Callsign = callsign;
            _nextPulse = Time.time + 0.5f;
        }

        private void Update()
        {
            if (Time.time < _nextPulse) return;
            _nextPulse = Time.time + 2.4f;
            VisualFactory.RingPulse(transform.position, AceCommanderDirector.AceColor(Archetype), 1.12f);
        }
    }
}
