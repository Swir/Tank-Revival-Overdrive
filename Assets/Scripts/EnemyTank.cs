using UnityEngine;

namespace TankRevival
{
    public enum EnemyKind
    {
        Basic,
        Fast,
        Heavy,
        Sniper,
        Boss
    }

    public sealed class EnemyTank : MonoBehaviour
    {
        public EnemyKind Kind { get; private set; }
        public Health Health { get; private set; }

        private TankGame _game;
        private Rigidbody2D _body;
        private Vector2 _facing = Vector2.down;
        private float _speed;
        private float _shotDelay;
        private float _projectileSpeed;
        private int _shotDamage;
        private float _nextShot;
        private float _nextThink;
        private float _aggression;
        private int _round;
        private float _aimBias;

        public void Initialize(TankGame game, EnemyKind kind, int round)
        {
            _game = game;
            Kind = kind;
            _round = Mathf.Clamp(round, 1, 100);

            Color body;
            Color accent;
            int hp;

            switch (kind)
            {
                default:
                case EnemyKind.Basic:
                    body = new Color(0.92f, 0.30f, 0.16f);
                    accent = new Color(1f, 0.70f, 0.24f);
                    hp = 1 + _round / 35;
                    _speed = 2.2f + _round * 0.010f;
                    _shotDelay = Mathf.Max(0.75f, 2.25f - _round * 0.010f);
                    _projectileSpeed = 7.2f + _round * 0.015f;
                    _shotDamage = 1;
                    break;

                case EnemyKind.Fast:
                    body = new Color(0.96f, 0.72f, 0.16f);
                    accent = new Color(1f, 0.94f, 0.50f);
                    hp = 1 + _round / 45;
                    _speed = 3.4f + _round * 0.012f;
                    _shotDelay = Mathf.Max(0.62f, 1.75f - _round * 0.008f);
                    _projectileSpeed = 8.6f;
                    _shotDamage = 1;
                    break;

                case EnemyKind.Heavy:
                    body = new Color(0.52f, 0.20f, 0.68f);
                    accent = new Color(0.90f, 0.55f, 1f);
                    hp = 3 + _round / 20;
                    _speed = 1.65f + _round * 0.006f;
                    _shotDelay = Mathf.Max(0.90f, 2.35f - _round * 0.008f);
                    _projectileSpeed = 7.8f;
                    _shotDamage = 1 + _round / 60;
                    transform.localScale = Vector3.one * 1.10f;
                    break;

                case EnemyKind.Sniper:
                    body = new Color(0.18f, 0.82f, 0.46f);
                    accent = new Color(0.62f, 1f, 0.76f);
                    hp = 2 + _round / 40;
                    _speed = 1.85f;
                    _shotDelay = Mathf.Max(1.05f, 2.80f - _round * 0.007f);
                    _projectileSpeed = 12.5f;
                    _shotDamage = 2;
                    break;

                case EnemyKind.Boss:
                    body = new Color(0.78f, 0.08f, 0.12f);
                    accent = new Color(1f, 0.76f, 0.10f);
                    hp = 10 + _round / 2;
                    _speed = 1.65f + _round * 0.004f;
                    _shotDelay = Mathf.Max(0.36f, 1.15f - _round * 0.004f);
                    _projectileSpeed = 10.2f;
                    _shotDamage = 2 + _round / 50;
                    transform.localScale = Vector3.one * 1.55f;
                    break;
            }

            // Every single round is stronger than the previous one, even on rounds
            // where enemy count/HP happens to stay on the same integer step.
            float progress = (_round - 1f) / 99f;
            float roundPressure = 1f + (_round - 1) * 0.0045f;
            _speed *= Mathf.Lerp(1f, 1.18f, progress);
            _projectileSpeed *= Mathf.Lerp(1f, 1.20f, progress);
            _shotDelay = Mathf.Max(0.30f, _shotDelay / roundPressure);
            _aggression = Mathf.Clamp01(0.38f + _round * 0.0055f + progress * 0.10f);
            _aimBias = Mathf.Lerp(0.06f, 0.33f, progress);

            VisualFactory.BuildTankSkin(transform, body, accent);

            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.78f, 0.78f);

            _body = gameObject.AddComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;

            Health = gameObject.AddComponent<Health>();
            Health.Initialize(Team.Enemy, hp);
            Health.Died += _ => _game.OnEnemyDestroyed(this, transform.position, Kind);

            ChooseDirection(true);
            _nextThink = Time.time + Random.Range(0.35f, 1.0f);
            _nextShot = Time.time + Random.Range(0.55f, _shotDelay + 0.75f);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying) return;

            if (Time.time >= _nextThink)
            {
                ChooseDirection(false);
                float thinkRate = Mathf.Lerp(1.10f, 0.30f, _aggression);
                _nextThink = Time.time + Random.Range(thinkRate * 0.62f, thinkRate * 1.30f);
            }

            if (Time.time >= _nextShot)
            {
                Fire();
                _nextShot = Time.time + Random.Range(_shotDelay * 0.82f, _shotDelay * 1.22f);
            }
        }

        private void FixedUpdate()
        {
            if (_game == null || !_game.IsPlaying || _body == null) return;
            _body.MovePosition(_body.position + _facing * (_speed * Time.fixedDeltaTime));
        }

        private void ChooseDirection(bool forceRandom)
        {
            Vector2 desired;

            if (!forceRandom && Random.value < _aggression)
            {
                Vector2 target = Random.value < Mathf.Lerp(0.63f, 0.78f, _aimBias) ? _game.PlayerPosition : _game.BasePosition;
                Vector2 delta = target - (Vector2)transform.position;
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    desired = new Vector2(Mathf.Sign(delta.x), 0f);
                else
                    desired = new Vector2(0f, Mathf.Sign(delta.y));

                float feintChance = Mathf.Lerp(0.22f, 0.08f, _aggression);
                if (Random.value < feintChance)
                    desired = Perpendicular(desired);
            }
            else
            {
                int d = Random.Range(0, 4);
                desired = d == 0 ? Vector2.up : d == 1 ? Vector2.right : d == 2 ? Vector2.down : Vector2.left;
            }

            _facing = desired;
            ApplyFacingRotation();
        }

        private static Vector2 Perpendicular(Vector2 v)
        {
            return Random.value < 0.5f ? new Vector2(-v.y, v.x) : new Vector2(v.y, -v.x);
        }

        private void ApplyFacingRotation()
        {
            float angle = 0f;
            if (_facing == Vector2.right) angle = -90f;
            else if (_facing == Vector2.down) angle = 180f;
            else if (_facing == Vector2.left) angle = 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Fire()
        {
            Vector2 muzzle = (Vector2)transform.position + _facing * (Kind == EnemyKind.Boss ? 1.02f : 0.72f);
            _game.SpawnProjectile(muzzle, _facing, Team.Enemy, _shotDamage, _projectileSpeed, new Color(1f, 0.32f, 0.10f));

            if (Kind == EnemyKind.Boss && _round >= 50 && Random.value < Mathf.Lerp(0.38f, 0.62f, (_round - 50f) / 50f))
            {
                Vector2 side = Perpendicular(_facing);
                _game.SpawnProjectile(muzzle + side * 0.22f, (_facing + side * 0.18f).normalized, Team.Enemy, _shotDamage, _projectileSpeed, new Color(1f, 0.18f, 0.08f));
            }

            if (Kind == EnemyKind.Boss && _round >= 80 && Random.value < 0.30f)
            {
                Vector2 side = Perpendicular(_facing);
                _game.SpawnProjectile(muzzle - side * 0.22f, (_facing - side * 0.18f).normalized, Team.Enemy, _shotDamage, _projectileSpeed, new Color(1f, 0.12f, 0.05f));
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_game != null && _game.IsPlaying)
            {
                ChooseDirection(true);
                _nextThink = Time.time + Random.Range(0.10f, 0.30f);
            }
        }
    }
}
