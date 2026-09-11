using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Per-enemy encounter modifier used by CampaignEncounterDirector. It augments existing tanks
    /// without replacing EnemyTank movement, firing, armor or health ownership.
    /// </summary>
    public sealed class EncounterCombatant : MonoBehaviour
    {
        private CampaignEncounterDirector _director;
        private TankGame _game;
        private RoundEncounterProfile _profile;
        private EnemyTank _enemy;
        private Health _health;
        private bool _champion;
        private bool _configured;
        private float _nextSupportShot;
        private float _nextPulse;
        private int _bossPhase = 3;
        private int _baseMaximum;

        public bool IsChampion => _champion;

        public void Configure(CampaignEncounterDirector director, TankGame game, RoundEncounterProfile profile, bool champion)
        {
            if (_configured) return;
            _configured = true;
            _director = director;
            _game = game;
            _profile = profile;
            _champion = champion;
            _enemy = GetComponent<EnemyTank>();
            _health = GetComponent<Health>();
            if (_enemy == null || _health == null) return;

            _baseMaximum = Mathf.Max(1, _health.Maximum);
            float multiplier = ResolveHealthMultiplier();
            if (_champion) multiplier *= _profile.BossRound ? 1.22f : 1.55f;
            int enhancedMaximum = Mathf.Max(_baseMaximum, Mathf.RoundToInt(_baseMaximum * multiplier));
            _health.SetMaximum(enhancedMaximum, true);

            if (_champion)
            {
                _health.InvulnerableUntil = Mathf.Max(_health.InvulnerableUntil, Time.time + 0.70f);
                VisualFactory.RingPulse(transform.position, new Color(1f, 0.68f, 0.08f), 1.35f);
            }

            _health.Died -= OnDied;
            _health.Died += OnDied;
            _health.Damaged -= OnDamaged;
            _health.Damaged += OnDamaged;

            _nextSupportShot = Time.time + Random.Range(2.4f, 4.5f);
            _nextPulse = Time.time + Random.Range(2.0f, 3.6f);
        }

        private float ResolveHealthMultiplier()
        {
            float value = _profile.HealthMultiplier;
            if (_enemy == null) return value;

            switch (_profile.Archetype)
            {
                case EncounterArchetype.ArmoredColumn:
                    if (_enemy.Kind == EnemyKind.Heavy || _enemy.Kind == EnemyKind.Siege) value *= 1.18f;
                    break;
                case EncounterArchetype.SniperNet:
                    if (_enemy.Kind == EnemyKind.Sniper) value *= 1.10f;
                    break;
                case EncounterArchetype.SiegePush:
                    if (_enemy.Kind == EnemyKind.Siege) value *= 1.16f;
                    break;
                case EncounterArchetype.SupplyInterdiction:
                    if (_enemy.Kind == EnemyKind.Supply) value *= 1.22f;
                    break;
                case EncounterArchetype.LastStand:
                    value *= 1.08f;
                    break;
            }
            return value;
        }

        private void Update()
        {
            if (!_configured || _game == null || !_game.IsPlaying || _enemy == null || _health == null || _health.IsDead)
                return;

            if (Time.time >= _nextSupportShot && CanUseSupportFire())
            {
                FireSupportShot();
                float cadence = BaseSupportCadence() / Mathf.Max(0.65f, _profile.FireSupportMultiplier);
                _nextSupportShot = Time.time + Random.Range(cadence * 0.82f, cadence * 1.18f);
            }

            if (_champion && Time.time >= _nextPulse)
            {
                _nextPulse = Time.time + (_profile.BossRound ? 2.6f : 3.8f);
                ChampionPulse();
            }

            if (_profile.BossRound && _enemy.Kind == EnemyKind.Boss)
                UpdateBossPhases();
        }

        private bool CanUseSupportFire()
        {
            if (_enemy.Kind == EnemyKind.Supply) return false;
            switch (_profile.Archetype)
            {
                case EncounterArchetype.SniperNet:
                    return _enemy.Kind == EnemyKind.Sniper || _champion;
                case EncounterArchetype.SiegePush:
                    return _enemy.Kind == EnemyKind.Siege || _enemy.Kind == EnemyKind.Heavy || _champion;
                case EncounterArchetype.ArtilleryScreen:
                    return _enemy.Kind == EnemyKind.Heavy || _enemy.Kind == EnemyKind.Siege || _enemy.Kind == EnemyKind.Elite;
                case EncounterArchetype.Wolfpack:
                    return _enemy.Kind == EnemyKind.Fast && Random.value < 0.48f;
                case EncounterArchetype.BossGauntlet:
                    return _enemy.Kind == EnemyKind.Boss || _enemy.Kind == EnemyKind.Elite || _enemy.Kind == EnemyKind.Siege;
                default:
                    return _champion && Random.value < 0.45f;
            }
        }

        private float BaseSupportCadence()
        {
            if (_enemy.Kind == EnemyKind.Boss) return 3.0f;
            if (_enemy.Kind == EnemyKind.Sniper) return 4.6f;
            if (_enemy.Kind == EnemyKind.Siege) return 4.2f;
            if (_enemy.Kind == EnemyKind.Elite) return 4.4f;
            return 5.2f;
        }

        private void FireSupportShot()
        {
            if (_game == null) return;
            bool attackEagle = _enemy.Kind == EnemyKind.Siege ||
                               (_profile.Archetype == EncounterArchetype.SiegePush && Random.value < 0.72f) ||
                               (_profile.BossRound && Random.value < 0.38f);
            Vector2 target = attackEagle ? _game.BasePosition : _game.PlayerPosition;
            Vector2 origin = transform.position;
            Vector2 delta = target - origin;
            if (delta.sqrMagnitude < 0.15f) return;

            Vector2 direction = delta.normalized;
            float speed = _enemy.Kind == EnemyKind.Sniper ? 13.8f : _enemy.Kind == EnemyKind.Boss ? 11.8f : 9.4f;
            speed += _profile.Round * 0.012f;
            int damage = _enemy.Kind == EnemyKind.Boss && _profile.Round >= 70 ? 2 : 1;
            Color color = _champion ? new Color(1f, 0.66f, 0.10f) : new Color(1f, 0.26f, 0.08f);

            _game.SpawnProjectile(origin + direction * 0.72f, direction, Team.Enemy, damage, speed, color, AmmoType.Basic);
            VisualFactory.MuzzleFlash(origin + direction * 0.70f, color, _champion ? 1.15f : 0.82f);
        }

        private void ChampionPulse()
        {
            if (_health == null || _health.IsDead) return;
            Color pulse = _profile.BossRound ? new Color(1f, 0.10f, 0.04f) : new Color(1f, 0.68f, 0.08f);
            VisualFactory.RingPulse(transform.position, pulse, _profile.BossRound ? 1.65f : 1.18f);

            EnemyTank[] allies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            int assisted = 0;
            for (int i = 0; i < allies.Length && assisted < 4; i++)
            {
                EnemyTank ally = allies[i];
                if (ally == null || ally == _enemy || ally.Health == null || ally.Health.IsDead) continue;
                if (Vector2.Distance(transform.position, ally.transform.position) > 2.6f) continue;
                if (ally.Health.Current >= ally.Health.Maximum) continue;
                ally.Health.Heal(1);
                assisted++;
            }
        }

        private void UpdateBossPhases()
        {
            if (_health.Maximum <= 0) return;
            float ratio = _health.Current / (float)_health.Maximum;
            int desired = ratio <= 0.30f ? 0 : ratio <= 0.55f ? 1 : ratio <= 0.78f ? 2 : 3;
            if (desired >= _bossPhase) return;
            _bossPhase = desired;

            Color phaseColor = desired == 2 ? new Color(1f, 0.55f, 0.08f) : desired == 1 ? new Color(1f, 0.22f, 0.05f) : new Color(1f, 0.06f, 0.03f);
            VisualFactory.Explosion(transform.position, phaseColor, 1.35f + (2 - desired) * 0.26f);
            VisualFactory.RingPulse(transform.position, phaseColor, 1.8f + (2 - desired) * 0.25f);
            _director?.Broadcast(desired == 2 ? "BOSS PHASE II // WEAPONS HOT" : desired == 1 ? "BOSS PHASE III // BREAKTHROUGH" : "FINAL BOSS PHASE // OVERDRIVE");

            _health.InvulnerableUntil = Mathf.Max(_health.InvulnerableUntil, Time.time + 0.42f);
            if (desired <= 1)
            {
                EnemyTank[] allies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
                for (int i = 0; i < allies.Length; i++)
                {
                    EnemyTank ally = allies[i];
                    if (ally == null || ally == _enemy || ally.Health == null || ally.Health.IsDead) continue;
                    CombatStatus status = ally.GetComponent<CombatStatus>();
                    if (status != null && Random.value < 0.42f) status.ApplyEmp(0.18f);
                }
            }
        }

        private void OnDamaged(Health health, int amount)
        {
            if (!_champion || health == null || health.IsDead) return;
            if (Random.value < 0.22f)
                VisualFactory.MicroBurst(transform.position, new Color(1f, 0.54f, 0.10f), 0.48f);
        }

        private void OnDied(Health health)
        {
            if (_champion)
                _director?.OnChampionDestroyed(this);
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Damaged -= OnDamaged;
            }
        }
    }
}
