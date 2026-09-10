using UnityEngine;

namespace TankRevival
{
    public sealed class BossWeaponController : MonoBehaviour
    {
        private TankGame _game;
        private CombatStatus _status;
        private int _round;
        private int _tier;
        private float _nextSpecial;
        private float _pendingBurstAt = -1f;
        private int _pendingBurstCount;

        public void Initialize(int round)
        {
            _round = Mathf.Clamp(round, 10, 100);
            _tier = Mathf.Clamp(_round / 10, 1, 10);
            _nextSpecial = Time.time + Mathf.Lerp(4.2f, 2.15f, (_tier - 1f) / 9f) + Random.Range(0.25f, 0.9f);
        }

        private void Start()
        {
            _game = FindAnyObjectByType<TankGame>();
            _status = GetComponent<CombatStatus>();
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying) return;
            _status ??= GetComponent<CombatStatus>();
            if (_status != null && _status.IsEmpDisabled) return;

            if (_pendingBurstCount > 0 && Time.time >= _pendingBurstAt)
            {
                FireFollowupBurst();
                _pendingBurstCount--;
                _pendingBurstAt = Time.time + Mathf.Lerp(0.26f, 0.14f, (_tier - 1f) / 9f);
            }

            if (Time.time < _nextSpecial) return;

            FireSpecialPattern();
            float cadence = Mathf.Lerp(4.4f, 2.05f, (_tier - 1f) / 9f);
            _nextSpecial = Time.time + cadence + Random.Range(-0.20f, 0.32f);
        }

        private void FireSpecialPattern()
        {
            Vector2 forward = ((Vector2)transform.up).normalized;
            Vector2 side = new Vector2(-forward.y, forward.x);
            Vector2 muzzle = (Vector2)transform.position + forward * 1.06f;
            int damage = 2 + (_tier >= 5 ? 1 : 0) + (_tier >= 9 ? 1 : 0);
            float speed = 9.0f + _tier * 0.40f;
            Color color = TierColor();

            switch (_tier)
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

            VisualFactory.MuzzleFlash(muzzle, color, 1.35f + _tier * 0.035f);
            VisualFactory.RingPulse(transform.position, color, 0.85f + _tier * 0.035f);
            BattleAudio.PlayGlobal(_tier >= 8 ? SoundCue.Plasma : SoundCue.HeavyShot, _tier >= 8 ? 0.50f : 0.34f, 0.035f);
            _game.KickCamera(0.10f + _tier * 0.012f, 0.065f + _tier * 0.006f);
        }

        private void FireFollowupBurst()
        {
            if (_game == null) return;
            Vector2 forward = ((Vector2)transform.up).normalized;
            Vector2 muzzle = (Vector2)transform.position + forward * 1.06f;
            Color color = TierColor();
            int damage = 2 + (_tier >= 7 ? 1 : 0);
            float speed = 9.5f + _tier * 0.42f;

            if (_tier >= 10)
            {
                float offset = _pendingBurstCount * 11f;
                FireRadial(transform.position, 8, damage, speed, color, offset);
            }
            else if (_tier >= 8)
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

        private Color TierColor()
        {
            float t = (_tier - 1f) / 9f;
            Color hot = Color.Lerp(new Color(1f, 0.42f, 0.05f), new Color(1f, 0.04f, 0.22f), t);
            if (_tier >= 8)
                hot = Color.Lerp(hot, new Color(0.58f, 0.22f, 1f), (_tier - 7f) / 3f);
            return hot;
        }
    }
}
