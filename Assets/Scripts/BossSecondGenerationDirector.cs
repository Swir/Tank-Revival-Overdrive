using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum BossDoctrine
    {
        Fortress = 0,
        Predator = 1,
        Tempest = 2,
        Annihilator = 3
    }

    /// <summary>
    /// v5.7 BOSS LEGENDS: SECOND GENERATION
    /// Adds phase-aware command doctrine and ArmorSystem-reactive counterplay on top of the
    /// existing BossLegendDirector. It never replaces EnemyTank, Health, Projectile or weak-point
    /// authority; all offensive actions are routed through TankGame.SpawnProjectile and all module
    /// state is read from the existing ArmorSystem.
    /// </summary>
    [DefaultExecutionOrder(275)]
    public sealed class BossSecondGenerationDirector : MonoBehaviour
    {
        public const int DoctrineCount = 4;
        public const int RetaliationThreshold = 65;
        public const int CriticalRetaliationThreshold = 30;
        public const float CoreVulnerabilitySeconds = 1.15f;

        private EnemyTank _boss;
        private BossLegendDirector _legend;
        private ArmorSystem _armor;
        private CombatStatus _status;
        private TankGame _game;
        private Health _health;
        private int _round;
        private BossDoctrine _doctrine;
        private float _nextCommand;
        private float _retaliationUntil;
        private float _vulnerableUntil;
        private string _retaliationLabel = string.Empty;
        private int _reactionMask;
        private int _criticalReactionMask;
        private int _lastPhase;
        private bool _initialized;
        private GUIStyle _doctrineStyle;
        private GUIStyle _stateStyle;

        public BossDoctrine Doctrine => _doctrine;
        public string DoctrineName => DoctrineDisplayName(_doctrine);
        public bool InRetaliationWindow => Time.time < _retaliationUntil;
        public bool CoreVulnerable => Time.time < _vulnerableUntil;
        public int ReactionCount => CountBits(_reactionMask) + CountBits(_criticalReactionMask);

        public void Initialize(EnemyTank boss, int round)
        {
            if (boss == null || boss.Kind != EnemyKind.Boss) return;
            _boss = boss;
            _health = boss.Health;
            _legend = boss.GetComponent<BossLegendDirector>();
            _armor = boss.GetComponent<ArmorSystem>();
            _status = boss.GetComponent<CombatStatus>();
            _game = FindAnyObjectByType<TankGame>();
            _round = Mathf.Clamp(round, 10, 100);
            _doctrine = DoctrineForRound(_round);
            _lastPhase = _legend != null ? _legend.Phase : 1;
            _nextCommand = Time.time + 4.0f;
            _initialized = _health != null && _armor != null;

            if (_initialized)
            {
                CampaignEncounterDirector.Instance?.Broadcast("BOSS DOCTRINE // " + DoctrineName);
                VisualFactory.RingPulse(transform.position, DoctrineColor(_doctrine), 1.55f);
            }
        }

        private void Update()
        {
            if (!_initialized || _health == null || _health.IsDead) return;
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) return;
            if (_legend == null) _legend = GetComponent<BossLegendDirector>();
            if (_armor == null) _armor = GetComponent<ArmorSystem>();
            if (_status == null) _status = GetComponent<CombatStatus>();
            if (_armor == null) return;

            ObserveModuleDamage();

            int phase = _legend != null ? Mathf.Clamp(_legend.Phase, 1, 4) : 1;
            if (phase != _lastPhase)
            {
                _lastPhase = phase;
                _nextCommand = Mathf.Max(_nextCommand, Time.time + 1.15f);
                CampaignEncounterDirector.Instance?.Broadcast(DoctrineName + " // COMMAND PHASE " + phase);
            }

            if (_status != null && _status.IsEmpDisabled) return;
            if (Time.time < _nextCommand || InRetaliationWindow) return;

            _nextCommand = Time.time + CommandCadence(_doctrine, _round, phase);
            StartCoroutine(ExecuteDoctrineCommand(phase));
        }

        private void ObserveModuleDamage()
        {
            ObserveModule(TankModule.Engine, _armor.EngineIntegrity, 0);
            ObserveModule(TankModule.Tracks, _armor.TrackIntegrity, 1);
            ObserveModule(TankModule.Gun, _armor.GunIntegrity, 2);
            ObserveModule(TankModule.AmmoRack, _armor.AmmoRackIntegrity, 3);
        }

        private void ObserveModule(TankModule module, int integrity, int bit)
        {
            int flag = 1 << bit;
            if (integrity <= RetaliationThreshold && (_reactionMask & flag) == 0)
            {
                _reactionMask |= flag;
                StartCoroutine(ExecuteModuleRetaliation(module, false));
            }
            if (integrity <= CriticalRetaliationThreshold && (_criticalReactionMask & flag) == 0)
            {
                _criticalReactionMask |= flag;
                StartCoroutine(ExecuteModuleRetaliation(module, true));
            }
        }

        private IEnumerator ExecuteModuleRetaliation(TankModule module, bool critical)
        {
            float now = Time.time;
            _retaliationUntil = Mathf.Max(_retaliationUntil, now + (critical ? 2.1f : 1.55f));
            _retaliationLabel = RetaliationName(module, critical);
            Color c = ModuleColor(module);
            VisualFactory.RingPulse(transform.position, c, critical ? 2.0f : 1.45f);
            VisualFactory.MicroBurst(transform.position, c, critical ? 1.7f : 1.2f);
            BattleAudio.PlayGlobal(critical ? SoundCue.ExplosionLarge : SoundCue.HeavyShot, critical ? 0.55f : 0.40f, 0.04f);
            CampaignEncounterDirector.Instance?.Broadcast("MODULE REACTION // " + _retaliationLabel);

            yield return new WaitForSeconds(critical ? 0.72f : 0.52f);
            if (_health == null || _health.IsDead || _game == null || !_game.IsPlaying) yield break;

            int phase = _legend != null ? Mathf.Clamp(_legend.Phase, 1, 4) : 1;
            int damage = Mathf.Clamp(1 + _round / 40 + (critical ? 1 : 0), 1, 5);
            float speed = 9.0f + _round * 0.025f;

            switch (module)
            {
                case TankModule.Engine:
                    FireRadial(critical ? 10 : 6, damage, speed * 0.88f, AmmoType.Incendiary, critical ? 14f : 0f);
                    break;
                case TankModule.Tracks:
                    FireRadial(critical ? 12 : 8, damage, speed * 0.78f, AmmoType.Explosive, critical ? 22f : 8f);
                    break;
                case TankModule.Gun:
                    FireAimedBurst(_game.PlayerPosition, critical ? 5 : 3, damage, speed * 1.18f, AmmoType.ArmorPiercing, critical ? 7f : 10f);
                    break;
                case TankModule.AmmoRack:
                    FireRadial(critical ? 14 : 8, damage, speed, critical ? AmmoType.Plasma : AmmoType.EMP, phase * 13f);
                    _vulnerableUntil = Time.time + CoreVulnerabilitySeconds + (critical ? 0.40f : 0f);
                    if (_status != null) _status.ApplyEmp(CoreVulnerabilitySeconds);
                    CampaignEncounterDirector.Instance?.Broadcast("CORE VENT // COUNTERATTACK INTERRUPTED");
                    break;
            }
        }

        private IEnumerator ExecuteDoctrineCommand(int phase)
        {
            Color c = DoctrineColor(_doctrine);
            string label = CommandName(_doctrine, phase);
            CampaignEncounterDirector.Instance?.Broadcast(DoctrineName + " // " + label);
            VisualFactory.RingPulse(transform.position, c, 1.15f + phase * 0.10f);
            yield return new WaitForSeconds(TelegraphSeconds(_doctrine, phase));
            if (_health == null || _health.IsDead || _game == null || !_game.IsPlaying) yield break;
            if (_status != null && _status.IsEmpDisabled) yield break;

            int damage = Mathf.Clamp(1 + _round / 45 + (phase >= 4 ? 1 : 0), 1, 5);
            float speed = 8.8f + _round * 0.030f + phase * 0.20f;

            switch (_doctrine)
            {
                case BossDoctrine.Fortress:
                    FireAimedBurst(_game.BasePosition, 2 + phase, damage, speed * 0.88f, AmmoType.Explosive, 8f);
                    if (phase >= 3) FireRadial(4 + phase, damage, speed * 0.76f, AmmoType.Basic, phase * 17f);
                    break;
                case BossDoctrine.Predator:
                    FireAimedBurst(_game.PlayerPosition, 3 + phase, damage, speed * 1.18f, AmmoType.ArmorPiercing, 5.5f);
                    break;
                case BossDoctrine.Tempest:
                    FireRadial(6 + phase * 2, damage, speed, phase >= 3 ? AmmoType.Plasma : AmmoType.EMP, Time.time * 21f);
                    break;
                case BossDoctrine.Annihilator:
                    FireAimedBurst(_game.PlayerPosition, 2 + phase, damage, speed * 1.15f, AmmoType.Plasma, 6f);
                    FireAimedBurst(_game.BasePosition, 1 + phase, damage, speed, AmmoType.ArmorPiercing, 10f);
                    if (phase >= 4) FireRadial(10, damage, speed * 0.90f, AmmoType.Explosive, 9f);
                    break;
            }

            _game.KickCamera(0.075f + phase * 0.014f, 0.055f);
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.38f, 0.05f);
        }

        private void FireAimedBurst(Vector2 target, int count, int damage, float speed, AmmoType ammo, float spreadStep)
        {
            Vector2 origin = (Vector2)transform.position;
            Vector2 forward = (target - origin).sqrMagnitude > 0.01f ? (target - origin).normalized : (Vector2)transform.up;
            Vector2 muzzle = origin + forward * 1.10f;
            float middle = (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Vector2 d = Rotate(forward, (i - middle) * spreadStep);
                _game.SpawnProjectile(muzzle, d, Team.Enemy, damage, speed, AmmoDatabase.Color(ammo), ammo);
            }
            VisualFactory.MuzzleFlash(muzzle, AmmoDatabase.Color(ammo), 1.3f);
        }

        private void FireRadial(int count, int damage, float speed, AmmoType ammo, float offset)
        {
            Vector2 origin = transform.position;
            Color c = AmmoDatabase.Color(ammo);
            for (int i = 0; i < count; i++)
            {
                float a = offset + i * 360f / Mathf.Max(1, count);
                Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                _game.SpawnProjectile(origin + d * 0.72f, d, Team.Enemy, damage, speed, c, ammo);
            }
            VisualFactory.MuzzleFlash(origin, c, 1.15f);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float cs = Mathf.Cos(r);
            float sn = Mathf.Sin(r);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs).normalized;
        }

        public static BossDoctrine DoctrineForRound(int round)
        {
            int tier = Mathf.Clamp(Mathf.Max(10, round) / 10, 1, 10);
            return (BossDoctrine)((tier - 1) % DoctrineCount);
        }

        public static float CommandCadence(BossDoctrine doctrine, int round, int phase)
        {
            float progress = Mathf.Clamp01((Mathf.Clamp(round, 10, 100) - 10f) / 90f);
            float baseCadence = doctrine == BossDoctrine.Fortress ? 6.6f : doctrine == BossDoctrine.Predator ? 5.4f : doctrine == BossDoctrine.Tempest ? 6.0f : 5.8f;
            return Mathf.Clamp(baseCadence - progress * 1.25f - (Mathf.Clamp(phase, 1, 4) - 1) * 0.35f, 3.25f, 6.8f);
        }

        public static float TelegraphSeconds(BossDoctrine doctrine, int phase)
        {
            float baseline = doctrine == BossDoctrine.Predator ? 0.78f : doctrine == BossDoctrine.Annihilator ? 0.92f : 1.05f;
            return Mathf.Clamp(baseline - (Mathf.Clamp(phase, 1, 4) - 1) * 0.06f, 0.58f, 1.10f);
        }

        public static AmmoType DoctrineAmmo(BossDoctrine doctrine, int phase)
        {
            switch (doctrine)
            {
                case BossDoctrine.Fortress: return AmmoType.Explosive;
                case BossDoctrine.Predator: return AmmoType.ArmorPiercing;
                case BossDoctrine.Tempest: return phase >= 3 ? AmmoType.Plasma : AmmoType.EMP;
                default: return AmmoType.Plasma;
            }
        }

        public static AmmoType ReactionAmmo(TankModule module, bool critical)
        {
            if (module == TankModule.Engine) return AmmoType.Incendiary;
            if (module == TankModule.Tracks) return AmmoType.Explosive;
            if (module == TankModule.Gun) return AmmoType.ArmorPiercing;
            if (module == TankModule.AmmoRack) return critical ? AmmoType.Plasma : AmmoType.EMP;
            return AmmoType.Basic;
        }

        public static string DoctrineDisplayName(BossDoctrine doctrine)
        {
            switch (doctrine)
            {
                case BossDoctrine.Fortress: return "FORTRESS PROTOCOL";
                case BossDoctrine.Predator: return "PREDATOR PROTOCOL";
                case BossDoctrine.Tempest: return "TEMPEST PROTOCOL";
                default: return "ANNIHILATOR PROTOCOL";
            }
        }

        private static string CommandName(BossDoctrine doctrine, int phase)
        {
            switch (doctrine)
            {
                case BossDoctrine.Fortress: return phase >= 3 ? "SIEGE WALL" : "BASE BREAKER";
                case BossDoctrine.Predator: return phase >= 3 ? "KILL CORRIDOR" : "HUNTER SALVO";
                case BossDoctrine.Tempest: return phase >= 3 ? "PLASMA TEMPEST" : "EMP CYCLONE";
                default: return phase >= 4 ? "TOTAL ERASURE" : "DUAL EXECUTION";
            }
        }

        private static string RetaliationName(TankModule module, bool critical)
        {
            string prefix = critical ? "CRITICAL " : string.Empty;
            switch (module)
            {
                case TankModule.Engine: return prefix + "ENGINE RAGE";
                case TankModule.Tracks: return prefix + "SIEGE ANCHOR";
                case TankModule.Gun: return prefix + "COUNTER BATTERY";
                case TankModule.AmmoRack: return prefix + "CORE VENT";
                default: return prefix + "RETALIATION";
            }
        }

        private static Color DoctrineColor(BossDoctrine doctrine)
        {
            switch (doctrine)
            {
                case BossDoctrine.Fortress: return new Color(1f, 0.48f, 0.08f);
                case BossDoctrine.Predator: return new Color(1f, 0.12f, 0.20f);
                case BossDoctrine.Tempest: return new Color(0.20f, 0.82f, 1f);
                default: return new Color(0.86f, 0.16f, 1f);
            }
        }

        private static Color ModuleColor(TankModule module)
        {
            if (module == TankModule.Engine) return new Color(1f, 0.34f, 0.05f);
            if (module == TankModule.Tracks) return new Color(1f, 0.72f, 0.10f);
            if (module == TankModule.Gun) return new Color(1f, 0.08f, 0.08f);
            return new Color(0.78f, 0.16f, 1f);
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0) { count += value & 1; value >>= 1; }
            return count;
        }

        private void EnsureStyles()
        {
            if (_doctrineStyle != null) return;
            _doctrineStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
            _stateStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10, fontStyle = FontStyle.Bold };
        }

        private void OnGUI()
        {
            if (!_initialized || _health == null || _health.IsDead || _game == null || !_game.IsPlaying) return;
            EnsureStyles();
            float width = Mathf.Min(620f, Screen.width - 40f);
            float x = (Screen.width - width) * 0.5f;
            _doctrineStyle.normal.textColor = DoctrineColor(_doctrine);
            _stateStyle.normal.textColor = CoreVulnerable ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.78f, 0.24f);
            GUI.Label(new Rect(x, 92f, width, 18f), "DOCTRINE: " + DoctrineName, _doctrineStyle);
            string state = CoreVulnerable ? "CORE VULNERABLE // COMMAND SYSTEM INTERRUPTED" : InRetaliationWindow ? "RETALIATION: " + _retaliationLabel : "MODULE REACTIONS: " + ReactionCount + "/8";
            GUI.Label(new Rect(x, 109f, width, 18f), state, _stateStyle);
        }
    }

    public sealed class BossSecondGenerationBootstrap : MonoBehaviour
    {
        private readonly HashSet<int> _configured = new HashSet<int>();
        private TankGame _game;
        private int _lastRevision = -1;
        private float _nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<BossSecondGenerationBootstrap>() != null) return;
            var go = new GameObject("BossSecondGenerationBootstrap_v5_7");
            DontDestroyOnLoad(go);
            go.AddComponent<BossSecondGenerationBootstrap>();
        }

        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.24f;
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) { _configured.Clear(); _lastRevision = -1; return; }

            int revision = RuntimeBattleRegistry.Revision;
            if (revision == _lastRevision && _configured.Count > 0) return;
            _lastRevision = revision;

            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Kind != EnemyKind.Boss || enemy.Health == null || enemy.Health.IsDead) continue;
                int id = enemy.GetInstanceID();
                if (_configured.Contains(id)) continue;
                BossLegendDirector legend = enemy.GetComponent<BossLegendDirector>();
                if (legend == null) continue;
                var secondGen = enemy.GetComponent<BossSecondGenerationDirector>();
                if (secondGen == null) secondGen = enemy.gameObject.AddComponent<BossSecondGenerationDirector>();
                secondGen.Initialize(enemy, Mathf.Clamp(_game.CurrentRound, 10, 100));
                _configured.Add(id);
            }
            _configured.RemoveWhere(id => !Exists(enemies, id));
        }

        private static bool Exists(EnemyTank[] enemies, int id)
        {
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] != null && enemies[i].GetInstanceID() == id) return true;
            return false;
        }
    }
}
