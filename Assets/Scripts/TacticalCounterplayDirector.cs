using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(442)]
    public sealed class TacticalCounterplayDirector : MonoBehaviour
    {
        public const float SmokeRadius = 2.6f;
        public const float SmokeDuration = 5.5f;
        public const float SmokeCooldown = 15.0f;
        public const float EcmDuration = 4.2f;
        public const float EcmCooldown = 24.0f;
        public const float EmpJamDuration = 3.5f;
        public const int MaxSmokeZones = 2;

        private sealed class SmokeZone
        {
            public Vector2 Center;
            public float ExpiresAt;
            public float NextPulse;
        }

        private static TacticalCounterplayDirector _instance;
        private readonly List<SmokeZone> _smoke = new List<SmokeZone>(MaxSmokeZones);
        private TankGame _game;
        private float _nextSmoke;
        private float _nextEcm;
        private float _jamUntil;
        private bool _adaptiveSuppressed;
        private bool _squadSuppressed;
        private bool _bossSuppressed;
        private int _empInterrupts;

        public static TacticalCounterplayDirector Instance => _instance;
        public static bool ConfigurationValid =>
            SmokeRadius >= 2.0f && SmokeRadius <= 3.2f &&
            SmokeDuration >= 4.0f && SmokeDuration <= 7.0f &&
            SmokeCooldown >= 12.0f && SmokeCooldown <= 20.0f &&
            EcmDuration >= 3.0f && EcmDuration <= 5.5f &&
            EcmCooldown >= 18.0f && EcmCooldown <= 32.0f &&
            EmpJamDuration >= 2.4f && EmpJamDuration <= 4.5f &&
            MaxSmokeZones >= 1 && MaxSmokeZones <= 3;

        public bool PlayerInsideSmoke => _game != null && IsPointInSmoke(_game.PlayerPosition);
        public bool NetworkJammed => Time.time < _jamUntil;
        public int ActiveSmokeZones => _smoke.Count;
        public int EmpInterrupts => _empInterrupts;
        public float SmokeReadyIn => Mathf.Max(0f, _nextSmoke - Time.time);
        public float EcmReadyIn => Mathf.Max(0f, _nextEcm - Time.time);
        public float JamRemaining => Mathf.Max(0f, _jamUntil - Time.time);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TacticalCounterplayDirector>() != null) return;
            var go = new GameObject("TacticalCounterplayDirector_v7_6");
            DontDestroyOnLoad(go);
            go.AddComponent<TacticalCounterplayDirector>();
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

        private void OnEnable()
        {
            Projectile.DamageResolved += OnProjectileDamageResolved;
        }

        private void OnDisable()
        {
            Projectile.DamageResolved -= OnProjectileDamageResolved;
            RestoreSuppressedDirectors();
        }

        private void OnDestroy()
        {
            Projectile.DamageResolved -= OnProjectileDamageResolved;
            RestoreSuppressedDirectors();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            ExpireSmoke();

            if (_game == null || !_game.IsPlaying)
            {
                RestoreSuppressedDirectors();
                return;
            }

            if (Input.GetKeyDown(KeyCode.R) && Time.time >= _nextSmoke)
                DeploySmoke(_game.PlayerPosition);

            if (Input.GetKeyDown(KeyCode.C) && Time.time >= _nextEcm)
                TriggerEcm();

            bool smokeBlocksPrediction = IsPointInSmoke(_game.PlayerPosition);
            bool jammed = Time.time < _jamUntil;
            ApplyCounterplayState(smokeBlocksPrediction, jammed);
            UpdateSmokePresentation();
        }

        private void DeploySmoke(Vector2 center)
        {
            if (_smoke.Count >= MaxSmokeZones) _smoke.RemoveAt(0);
            _smoke.Add(new SmokeZone
            {
                Center = center,
                ExpiresAt = Time.time + SmokeDuration,
                NextPulse = 0f
            });
            _nextSmoke = Time.time + SmokeCooldown;
            VisualFactory.RingPulse(center, new Color(0.62f, 0.72f, 0.76f), SmokeRadius);
            VisualFactory.MicroBurst(center, new Color(0.64f, 0.72f, 0.76f), 1.35f);
            BattleAudio.PlayGlobal(SoundCue.PowerUp, 0.28f, 0.04f);
        }

        private void TriggerEcm()
        {
            _nextEcm = Time.time + EcmCooldown;
            _jamUntil = Mathf.Max(_jamUntil, Time.time + EcmDuration);
            Vector2 center = _game != null ? _game.PlayerPosition : Vector2.zero;
            VisualFactory.RingPulse(center, new Color(0.18f, 0.92f, 1f), 2.8f);
            VisualFactory.RingPulse(center, new Color(0.42f, 0.38f, 1f), 1.8f);
            BattleAudio.PlayGlobal(SoundCue.PowerUp, 0.34f, 0.03f);
        }

        private void OnProjectileDamageResolved(Projectile projectile, Health target, int damage, bool killed)
        {
            if (projectile == null || target == null) return;
            if (projectile.OwnerTeam != Team.Player || projectile.Ammo != AmmoType.EMP) return;
            if (target.Team != Team.Enemy || damage <= 0) return;

            _empInterrupts++;
            _jamUntil = Mathf.Max(_jamUntil, Time.time + EmpJamDuration);
            Vector2 center = target.transform.position;
            VisualFactory.RingPulse(center, AmmoDatabase.Color(AmmoType.EMP), 1.55f);
            if (_game != null)
                VisualFactory.RingPulse(_game.PlayerPosition, new Color(0.12f, 0.78f, 1f), 0.72f);
        }

        private void ApplyCounterplayState(bool smokeBlocksPrediction, bool jammed)
        {
            AdaptiveFireControlDirector adaptive = AdaptiveFireControlDirector.Instance;
            bool blockAdaptive = smokeBlocksPrediction || jammed;
            if (adaptive != null)
            {
                if (blockAdaptive && adaptive.enabled)
                {
                    adaptive.enabled = false;
                    _adaptiveSuppressed = true;
                }
                else if (!blockAdaptive && _adaptiveSuppressed)
                {
                    adaptive.enabled = true;
                    _adaptiveSuppressed = false;
                }
            }

            EnemySquadTacticsDirector squad = EnemySquadTacticsDirector.Instance;
            if (squad != null)
            {
                if (jammed && squad.enabled)
                {
                    squad.enabled = false;
                    _squadSuppressed = true;
                }
                else if (!jammed && _squadSuppressed)
                {
                    squad.enabled = true;
                    _squadSuppressed = false;
                }
            }

            BossCommandTacticsDirector boss = BossCommandTacticsDirector.Instance;
            if (boss != null)
            {
                if (jammed && boss.enabled)
                {
                    boss.enabled = false;
                    _bossSuppressed = true;
                }
                else if (!jammed && _bossSuppressed)
                {
                    boss.enabled = true;
                    _bossSuppressed = false;
                }
            }
        }

        private void RestoreSuppressedDirectors()
        {
            if (_adaptiveSuppressed && AdaptiveFireControlDirector.Instance != null)
                AdaptiveFireControlDirector.Instance.enabled = true;
            if (_squadSuppressed && EnemySquadTacticsDirector.Instance != null)
                EnemySquadTacticsDirector.Instance.enabled = true;
            if (_bossSuppressed && BossCommandTacticsDirector.Instance != null)
                BossCommandTacticsDirector.Instance.enabled = true;
            _adaptiveSuppressed = false;
            _squadSuppressed = false;
            _bossSuppressed = false;
        }

        private void ExpireSmoke()
        {
            for (int i = _smoke.Count - 1; i >= 0; i--)
                if (_smoke[i] == null || Time.time >= _smoke[i].ExpiresAt) _smoke.RemoveAt(i);
        }

        private bool IsPointInSmoke(Vector2 point)
        {
            float radiusSq = SmokeRadius * SmokeRadius;
            for (int i = 0; i < _smoke.Count; i++)
            {
                SmokeZone zone = _smoke[i];
                if (zone != null && Time.time < zone.ExpiresAt && (point - zone.Center).sqrMagnitude <= radiusSq)
                    return true;
            }
            return false;
        }

        private void UpdateSmokePresentation()
        {
            for (int i = 0; i < _smoke.Count; i++)
            {
                SmokeZone zone = _smoke[i];
                if (zone == null || Time.time < zone.NextPulse) continue;
                zone.NextPulse = Time.time + 0.42f;
                float remaining = Mathf.Clamp01((zone.ExpiresAt - Time.time) / SmokeDuration);
                Color smokeColor = new Color(0.55f, 0.62f, 0.66f, Mathf.Lerp(0.18f, 0.52f, remaining));
                if (MassBattleFxBudget.TryConsumeMicroFx(false))
                    VisualFactory.MicroBurst(zone.Center + Random.insideUnitCircle * (SmokeRadius * 0.52f), smokeColor, 0.72f);
            }
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            float w = Mathf.Min(470f, Screen.width - 28f);
            Rect rect = new Rect((Screen.width - w) * 0.5f, Screen.height - 56f, w, 36f);
            string smoke = SmokeReadyIn <= 0f ? "SMOKE [R] READY" : $"SMOKE {SmokeReadyIn:0}s";
            string ecm = EcmReadyIn <= 0f ? "ECM [C] READY" : $"ECM {EcmReadyIn:0}s";
            string state = NetworkJammed ? $"  •  JAM {JamRemaining:0.0}s" : PlayerInsideSmoke ? "  •  CONCEALED" : string.Empty;
            GUI.Box(rect, smoke + "     " + ecm + state);
        }
    }
}
