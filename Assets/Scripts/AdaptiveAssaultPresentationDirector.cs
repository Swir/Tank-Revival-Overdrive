using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v11.7 presentation bridge for the verified v11.6 maneuver authority. It reads EnemyTank,
    /// CounterFireThreatMemory and AdaptivePlatoonManeuverDirector state and renders bounded cues only.
    /// It never moves actors, fires projectiles, mutates Health/ArmorSystem or changes target selection.
    /// </summary>
    [DefaultExecutionOrder(-8420)]
    public sealed class AdaptiveAssaultPresentationDirector : MonoBehaviour
    {
        public const float ScanIntervalSeconds = 0.28f;
        public const float CueHoldSeconds = 0.52f;
        public const int MaxManeuverCues = 18;

        private readonly List<PlatoonManeuverCue3D> _cues = new List<PlatoonManeuverCue3D>(MaxManeuverCues);
        private TankGame _game;
        private float _nextScan;
        private int _activeHunters;
        private int _activeSupport;
        private int _activeBreacher;
        private int _activeCommanders;
        private int _counterFireCues;

        public static AdaptiveAssaultPresentationDirector Instance { get; private set; }
        public int ActiveCueCount => CountLiveCues();
        public int ActiveHunters => _activeHunters;
        public int ActiveSupport => _activeSupport;
        public int ActiveBreacher => _activeBreacher;
        public int ActiveCommanders => _activeCommanders;
        public int CounterFireCues => _counterFireCues;

        public static bool ConfigurationValid =>
            ScanIntervalSeconds >= 0.20f && ScanIntervalSeconds <= 0.45f &&
            CueHoldSeconds > ScanIntervalSeconds && CueHoldSeconds <= 0.80f &&
            MaxManeuverCues >= 10 && MaxManeuverCues <= AdaptivePlatoonManeuverDirector.MaxTrackedActors;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<AdaptiveAssaultPresentationDirector>() != null) return;
            var go = new GameObject("AdaptiveAssaultPresentationDirector_v11_7");
            DontDestroyOnLoad(go);
            go.AddComponent<AdaptiveAssaultPresentationDirector>();
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

            if (!_game.IsPlaying)
            {
                ExpireAll();
                ResetCounters();
                return;
            }

            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanIntervalSeconds;
            RefreshManeuverCues();
        }

        private void RefreshManeuverCues()
        {
            ResetCounters();
            PruneDestroyedCues();

            int round = _game.CurrentRound;
            AdaptivePlatoonManeuverDirector.ManeuverPresentationSnapshot snapshot =
                AdaptivePlatoonManeuverDirector.ReadPresentationSnapshot(round);
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead ||
                    !AdaptivePlatoonManeuverDirector.SupportsPresentation(enemy.Kind))
                    continue;

                PlatoonFireMissionCoordinator.PlatoonRole role = PlatoonFireMissionCoordinator.RoleFor(enemy, enemy.Kind);
                bool counterFire = CounterFireThreatMemory.ShouldRetaliate(enemy, round);
                AdaptivePlatoonManeuverDirector.ManeuverPresentationState state =
                    AdaptivePlatoonManeuverDirector.PresentationStateFor(enemy, enemy.Kind, counterFire);

                TrackRole(role);
                if (state == AdaptivePlatoonManeuverDirector.ManeuverPresentationState.CounterFireDisplacement)
                    _counterFireCues++;

                PlatoonManeuverCue3D cue = enemy.GetComponent<PlatoonManeuverCue3D>();
                if (cue == null)
                {
                    if (CountLiveCues() >= MaxManeuverCues) continue;
                    cue = enemy.gameObject.AddComponent<PlatoonManeuverCue3D>();
                    cue.Initialize(enemy);
                    _cues.Add(cue);
                }

                bool priority = role == PlatoonFireMissionCoordinator.PlatoonRole.Commander ||
                                state == AdaptivePlatoonManeuverDirector.ManeuverPresentationState.Reorganizing ||
                                state == AdaptivePlatoonManeuverDirector.ManeuverPresentationState.CounterFireDisplacement;
                bool budgetVisible = MassBattleFxBudget.TryConsumeTacticalCue(priority);
                cue.RefreshCue(role, state, snapshot, budgetVisible);
            }
        }

        private void TrackRole(PlatoonFireMissionCoordinator.PlatoonRole role)
        {
            switch (role)
            {
                case PlatoonFireMissionCoordinator.PlatoonRole.Commander: _activeCommanders++; break;
                case PlatoonFireMissionCoordinator.PlatoonRole.Hunter: _activeHunters++; break;
                case PlatoonFireMissionCoordinator.PlatoonRole.FireSupport: _activeSupport++; break;
                case PlatoonFireMissionCoordinator.PlatoonRole.Breacher: _activeBreacher++; break;
            }
        }

        private int CountLiveCues()
        {
            int count = 0;
            for (int i = 0; i < _cues.Count; i++) if (_cues[i] != null) count++;
            return count;
        }

        private void PruneDestroyedCues()
        {
            for (int i = _cues.Count - 1; i >= 0; i--)
                if (_cues[i] == null) _cues.RemoveAt(i);
        }

        private void ExpireAll()
        {
            for (int i = 0; i < _cues.Count; i++)
                if (_cues[i] != null) _cues[i].ExpireNow();
        }

        private void ResetCounters()
        {
            _activeHunters = 0;
            _activeSupport = 0;
            _activeBreacher = 0;
            _activeCommanders = 0;
            _counterFireCues = 0;
        }

        public static string OperationLabel(int round)
        {
            AdaptivePlatoonManeuverDirector.ManeuverPresentationSnapshot snapshot =
                AdaptivePlatoonManeuverDirector.ReadPresentationSnapshot(round);
            if (snapshot.Reorganizing) return "REORGANIZING";
            if (Instance != null && Instance.CounterFireCues > 0) return "COUNTER-FIRE REPOSITION";
            return (snapshot.PhaseIndex & 1) == 0 ? "ENVELOPMENT LEFT/RIGHT" : "ASSAULT CROSSOVER";
        }
    }

    /// <summary>
    /// Presentation-only child of an existing EnemyTank. Geometry is created once and then toggled;
    /// the owning tank remains authoritative for movement and combat.
    /// </summary>
    public sealed class PlatoonManeuverCue3D : MonoBehaviour
    {
        private EnemyTank _actor;
        private Transform _root;
        private GameObject _forward;
        private GameObject _leftWing;
        private GameObject _rightWing;
        private GameObject _standoff;
        private GameObject _displace;
        private readonly GameObject[] _recovery = new GameObject[4];
        private float _visibleUntil;
        private float _phase;
        private AdaptivePlatoonManeuverDirector.ManeuverPresentationState _state;
        private PlatoonFireMissionCoordinator.PlatoonRole _role;

        public void Initialize(EnemyTank actor)
        {
            _actor = actor;
            var root = new GameObject("PlatoonManeuverCue3D");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, 0.34f);
            root.transform.localRotation = Quaternion.identity;
            _root = root.transform;

            PlatoonFireMissionCoordinator.PlatoonRole role = PlatoonFireMissionCoordinator.RoleFor(actor, actor.Kind);
            Color color = RoleColor(role);
            float diameter = role == PlatoonFireMissionCoordinator.PlatoonRole.Commander ? 1.80f :
                             role == PlatoonFireMissionCoordinator.PlatoonRole.FireSupport ? 1.66f : 1.52f;
            Runtime3DFactory.Cylinder("ManeuverRoleRing3D", _root, Vector3.zero, diameter, 0.025f, color, 0.01f, 0.84f);

            _forward = Runtime3DFactory.Box("ManeuverAdvance3D", _root,
                new Vector3(0f, 0.92f, 0f), new Vector3(0.16f, 0.48f, 0.035f), color, 0.01f, 0.90f);
            _leftWing = Runtime3DFactory.Box("ManeuverFlankLeft3D", _root,
                new Vector3(-0.78f, 0.28f, 0f), new Vector3(0.42f, 0.12f, 0.035f), color, 0.01f, 0.90f);
            _rightWing = Runtime3DFactory.Box("ManeuverFlankRight3D", _root,
                new Vector3(0.78f, 0.28f, 0f), new Vector3(0.42f, 0.12f, 0.035f), color, 0.01f, 0.90f);
            _standoff = Runtime3DFactory.Box("ManeuverStandoff3D", _root,
                new Vector3(0f, -0.86f, 0f), new Vector3(0.82f, 0.10f, 0.035f), color, 0.01f, 0.90f);
            _displace = Runtime3DFactory.Box("ManeuverCounterFireDisplace3D", _root,
                new Vector3(0f, -1.02f, 0f), new Vector3(1.10f, 0.11f, 0.035f), new Color(1f, 0.18f, 0.05f, 0.88f), 0.01f, 0.94f);

            for (int i = 0; i < _recovery.Length; i++)
            {
                var holder = new GameObject("ManeuverRecoveryAnchor3D_" + i);
                holder.transform.SetParent(_root, false);
                holder.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                _recovery[i] = Runtime3DFactory.Box("ManeuverRecoveryBar3D", holder.transform,
                    new Vector3(0f, 1.02f, 0f), new Vector3(0.14f, 0.40f, 0.04f),
                    new Color(1f, 0.78f, 0.16f, 0.90f), 0.01f, 0.95f);
            }

            SetPattern(AdaptivePlatoonManeuverDirector.ManeuverPresentationState.Advance, 0);
            _root.gameObject.SetActive(false);
        }

        public void RefreshCue(
            PlatoonFireMissionCoordinator.PlatoonRole role,
            AdaptivePlatoonManeuverDirector.ManeuverPresentationState state,
            AdaptivePlatoonManeuverDirector.ManeuverPresentationSnapshot snapshot,
            bool budgetVisible)
        {
            _role = role;
            _state = state;
            if (!budgetVisible) return;
            _visibleUntil = Time.unscaledTime + AdaptiveAssaultPresentationDirector.CueHoldSeconds;
            SetPattern(state, snapshot.PhaseIndex);
            if (_root != null) _root.gameObject.SetActive(true);
        }

        public void ExpireNow()
        {
            _visibleUntil = -1f;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_root == null || _actor == null || _actor.Health == null || _actor.Health.IsDead)
            {
                if (_root != null) _root.gameObject.SetActive(false);
                return;
            }

            bool visible = Time.unscaledTime <= _visibleUntil;
            if (_root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
            if (!visible) return;

            // Keep local identity so forward/flank/standoff glyphs inherit the actual tank facing.
            // This is presentation-only; the parent EnemyTank remains the sole transform authority.
            _phase += Time.unscaledDeltaTime * (_state == AdaptivePlatoonManeuverDirector.ManeuverPresentationState.Reorganizing ? 6.4f : 3.4f);
            float amplitude = _role == PlatoonFireMissionCoordinator.PlatoonRole.Commander ? 0.085f : 0.055f;
            float pulse = 1f + Mathf.Sin(_phase) * amplitude;
            _root.localScale = Vector3.one * pulse;
        }

        private void SetPattern(AdaptivePlatoonManeuverDirector.ManeuverPresentationState state, int phaseIndex)
        {
            if (_forward == null) return;
            _forward.SetActive(state == AdaptivePlatoonManeuverDirector.ManeuverPresentationState.Advance);
            bool flank = state == AdaptivePlatoonManeuverDirector.ManeuverPresentationState.Envelopment;
            _leftWing.SetActive(flank && (phaseIndex & 1) == 0);
            _rightWing.SetActive(flank && (phaseIndex & 1) != 0);
            _standoff.SetActive(state == AdaptivePlatoonManeuverDirector.ManeuverPresentationState.Standoff ||
                                state == AdaptivePlatoonManeuverDirector.ManeuverPresentationState.CounterFireDisplacement);
            _displace.SetActive(state == AdaptivePlatoonManeuverDirector.ManeuverPresentationState.CounterFireDisplacement);
            bool reorg = state == AdaptivePlatoonManeuverDirector.ManeuverPresentationState.Reorganizing;
            for (int i = 0; i < _recovery.Length; i++) if (_recovery[i] != null) _recovery[i].SetActive(reorg);
        }

        private static Color RoleColor(PlatoonFireMissionCoordinator.PlatoonRole role)
        {
            switch (role)
            {
                case PlatoonFireMissionCoordinator.PlatoonRole.Commander: return new Color(0.20f, 0.90f, 1f, 0.78f);
                case PlatoonFireMissionCoordinator.PlatoonRole.Hunter: return new Color(0.72f, 0.30f, 1f, 0.74f);
                case PlatoonFireMissionCoordinator.PlatoonRole.FireSupport: return new Color(1f, 0.52f, 0.08f, 0.78f);
                default: return new Color(1f, 0.18f, 0.08f, 0.72f);
            }
        }
    }
}
