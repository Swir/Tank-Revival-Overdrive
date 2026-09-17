using UnityEngine;

namespace TankRevival
{
    public sealed class BossWeaponController : MonoBehaviour
    {
        private TankGame _game;
        private CombatStatus _status;
        private Health _health;
        private ArmorSystem _armor;
        private int _round;
        private int _tier;
        private int _phase = 1;
        private int _phaseCount = 3;
        private int _lastPatternTier = 1;
        private float _nextSpecial;
        private float _pendingBurstAt = -1f;
        private int _pendingBurstCount;

        public int CurrentPhase { get { return _phase; } }
        public int PhaseCount { get { return _phaseCount; } }
        public string PhaseLabel { get { return BossPhaseWarfareV130.PhaseLabel(_phase, _phaseCount); } }

        public void Initialize(int round)
        {
            _round = Mathf.Clamp(round, 10, 100);
            _tier = Mathf.Clamp(_round / 10, 1, 10);
            _phaseCount = BossPhaseWarfareV130.PhaseCountForRound(_round);
            _phase = 1;
            _lastPatternTier = _tier;
            _nextSpecial = Time.time + Mathf.Lerp(4.2f, 2.15f, (_tier - 1f) / 9f) + Random.Range(0.25f, 0.9f);
        }

        private void Start()
        {
            _game = FindAnyObjectByType<TankGame>();
            _status = GetComponent<CombatStatus>();
            _health = GetComponent<Health>();
            _armor = GetComponent<ArmorSystem>();
            RefreshPhase(true);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying) return;
            _status ??= GetComponent<CombatStatus>();
            _health ??= GetComponent<Health>();
            _armor ??= GetComponent<ArmorSystem>();
            RefreshPhase(false);

            if (_status != null && _status.IsEmpDisabled) return;
            if (_armor != null && _armor.IsWeaponDisabled) return;

            int effectiveTier = Mathf.Clamp(_tier + BossPhaseWarfareV130.PatternTierBonus(_phase), 1, 10);
            if (_pendingBurstCount > 0 && Time.time >= _pendingBurstAt)
            {
                FireFollowupBurst();
                _pendingBurstCount--;
                _pendingBurstAt = Time.time + Mathf.Lerp(0.26f, 0.14f, (effectiveTier - 1f) / 9f);
            }

            if (Time.time < _nextSpecial) return;

            FireSpecialPattern(effectiveTier);
            float cadence = Mathf.Lerp(4.4f, 2.05f, (_tier - 1f) / 9f);
            cadence *= BossPhaseWarfareV130.CadenceScale(_phase);
            if (_armor != null) cadence *= Mathf.Clamp(_armor.ReloadMultiplier, 1f, 1.85f);
            _nextSpecial = Time.time + Mathf.Max(0.85f, cadence) + Random.Range(-0.20f, 0.32f);
        }

        private void RefreshPhase(bool forceReport)
        {
            float hpRatio = 1f;
            if (_health != null && _health.Maximum > 0) hpRatio = _health.Current / (float)_health.Maximum;
            bool mobilityCritical = _armor != null && _armor.IsMobilityCritical;
            bool weaponDisabled = _armor != null && _armor.IsWeaponDisabled;
            int resolved = BossPhaseWarfareV130.ResolvePhase(_round, hpRatio, mobilityCritical, weaponDisabled);
            if (!forceReport && resolved == _phase) return;

            int previous = _phase;
            _phase = resolved;
            EncounterWarfareDirector.Instance?.ReportBossPhase(_round, _phase, _phaseCount);
            if (!forceReport && _phase > previous)
            {
                Color color = TierColor(Mathf.Clamp(_tier + _phase - 1, 1, 10));
                VisualFactory.RingPulse(transform.position, color, 1.05f + _phase * 0.12f);
                BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.50f + _phase * 0.05f, -0.03f + _phase * 0.01f);
                _nextSpecial = Mathf.Min(_nextSpecial, Time.time + Mathf.Lerp(1.20f, 0.62f, (_phase - 1f) / 3f));
            }
        }

        private void FireSpecialPattern(int effectiveTier)
        {
            _lastPatternTier = effectiveTier;
            Vector2 forward = ((Vector2)transform.up).normalized;
            Vector2 side = new Vector2(-forward.y, forward.x);
            Vector2 muzzle = (Vector2)transform.position + forward * 1.06f;
            int damage = 2 + (effectiveTier >= 5 ? 1 : 0) + (effectiveTier >= 9 ? 1 : 0);
            float speed = 9.0f + effectiveTier * 0.40f;
            Color color = TierColor(effectiveTier);

            switch (effectiveTier)
            {
                case 1:
                    Fire(muzzle + side * 0.24f, forward, damage, speed, color);
                    Fire(muzzle - side * 0.24f, forward, damage, speed, color);
                    break;

                case 2:
                    FireFan(muzzle, forward, damage, speed, color, 3, 13f);
                    break;

                case 3:
                    FireFan(muzzle, forward, damage, speed, color, 3, 20f);
                    _pendingBurstCount = 1;
                    _pendingBurstAt = Time.time + 0.32f;
                    break;

                case 4:
                    Fire(muzzle, forward, damage, speed, color);
                    Fire(muzzle, side, damage, speed * 0.92f, color);
                    Fire(muzzle, -side, damage, speed * 0.92f, color);
                    Fire(muzzle, -forward, Mathf.Max(1, damage - 1), speed * 0.82f, color);
                    break;

                case 5:
                    FireFan(muzzle, forward, damage, speed, color, 5, 12f);
                    break;

                case 6:
                    FireRadial(transform.position, 6, damage, speed * 0.92f, color, 30f);
                    break;

                case 7:
                    FireFan(muzzle, forward, damage, speed, color, 5, 16f);
                    _pendingBurstCount = 2;
                    _pendingBurstAt = Time.time + 0.24f;
                    break;

                case 8:
                    FireRadial(transform.position, 8, damage, speed, color, 22.5f);
                    _pendingBurstCount = 1;
                    _pendingBurstAt = Time.time + 0.22f;
                    break;

                case 9:
                    FireRadial(transform.position, 10, damage, speed, color, 18f);
                    FireFan(muzzle, forward, damage, speed * 1.12f, color, 3, 9f);
                    _pendingBurstCount = 1;
                    _pendingBurstAt = Time.time + 0.18f;
                    break;

                default:
                    FireRadial(transform.position, 12, damage, speed, color, 15f);
                    FireFan(muzzle, forward, damage + 1, speed * 1.18f, color, 5, 10f);
                    _pendingBurstCount = 3;
                    _pendingBurstAt = Time.time + 0.16f;
                    break;
            }

            if (_phase >= 3 && _pendingBurstCount == 0)
            {
                _pendingBurstCount = _phase >= 4 ? 2 : 1;
                _pendingBurstAt = Time.time + (_phase >= 4 ? 0.19f : 0.27f);
            }
            else if (_phase >= 4 && _pendingBurstCount > 0)
            {
                _pendingBurstCount = Mathf.Min(3, _pendingBurstCount + 1);
            }

            VisualFactory.MuzzleFlash(muzzle, color, 1.35f + effectiveTier * 0.035f);
            VisualFactory.RingPulse(transform.position, color, 0.85f + _phase * 0.08f + effectiveTier * 0.02f);
            BattleAudio.PlayGlobal(effectiveTier >= 8 ? SoundCue.Plasma : SoundCue.HeavyShot, effectiveTier >= 8 ? 0.50f : 0.34f, 0.035f);
            _game.KickCamera(0.10f + effectiveTier * 0.012f, 0.065f + _phase * 0.012f);
        }

        private void FireFollowupBurst()
        {
            if (_game == null) return;
            Vector2 forward = ((Vector2)transform.up).normalized;
            Vector2 muzzle = (Vector2)transform.position + forward * 1.06f;
            Color color = TierColor(_lastPatternTier);
            int damage = 2 + (_lastPatternTier >= 7 ? 1 : 0);
            float speed = 9.5f + _lastPatternTier * 0.42f;

            if (_lastPatternTier >= 10)
            {
                float offset = _pendingBurstCount * 11f;
                FireRadial(transform.position, 8, damage, speed, color, offset);
            }
            else if (_lastPatternTier >= 8)
            {
                FireFan(muzzle, forward, damage, speed, color, 3, 18f);
            }
            else
            {
                FireFan(muzzle, forward, damage, speed, color, 3, 10f);
            }

            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.22f, 0.08f);
            VisualFactory.MuzzleFlash(muzzle, color, 0.92f);
        }

        private void FireFan(Vector2 origin, Vector2 forward, int damage, float speed, Color color, int count, float stepDegrees)
        {
            if (count <= 1)
            {
                Fire(origin, forward, damage, speed, color);
                return;
            }

            float middle = (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float angle = (i - middle) * stepDegrees;
                Fire(origin, Rotate(forward, angle), damage, speed, color);
            }
        }

        private void FireRadial(Vector2 origin, int count, int damage, float speed, Color color, float angleOffset)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = angleOffset + i * (360f / count);
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Fire(origin + direction * 0.65f, direction, damage, speed, color);
            }
        }

        private void Fire(Vector2 origin, Vector2 direction, int damage, float speed, Color color)
        {
            _game.SpawnProjectile(origin, direction.normalized, Team.Enemy, damage, speed, color, AmmoType.Basic);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c).normalized;
        }

        private Color TierColor(int effectiveTier)
        {
            float t = (effectiveTier - 1f) / 9f;
            Color hot = Color.Lerp(new Color(1f, 0.42f, 0.05f), new Color(1f, 0.04f, 0.22f), t);
            if (effectiveTier >= 8)
                hot = Color.Lerp(hot, new Color(0.58f, 0.22f, 1f), (effectiveTier - 7f) / 3f);
            return hot;
        }
    }
}
