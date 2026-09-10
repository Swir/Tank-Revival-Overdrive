using UnityEngine;

namespace TankRevival
{
    public enum EnemyKind
    {
        Basic,
        Fast,
        Heavy,
        Sniper,
        Siege,
        Elite,
        Supply,
        Boss
    }

    public sealed class EnemyTank : MonoBehaviour
    {
        public EnemyKind Kind { get; private set; }
        public Health Health { get; private set; }
        public AmmoType SupplyAmmo { get; private set; } = AmmoType.Basic;
        public bool DropsAmmo => Kind == EnemyKind.Supply;

        private TankGame _game;
        private Rigidbody2D _body;
        private CombatStatus _status;
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
        private float _baseHuntBias;

        public void Initialize(TankGame game, EnemyKind kind, int round, AmmoType supplyAmmo = AmmoType.Basic)
        {
            _game = game;
            Kind = kind;
            SupplyAmmo = supplyAmmo;
            _round = Mathf.Clamp(round, 1, 100);

            Color body;
            Color accent;
            int hp;

            switch (kind)
            {
                default:
                case EnemyKind.Basic:
                    body = new Color(0.72f, 0.18f, 0.10f);
                    accent = new Color(1f, 0.56f, 0.20f);
                    hp = 1 + _round / 35;
                    _speed = 2.20f + _round * 0.010f;
                    _shotDelay = Mathf.Max(0.75f, 2.25f - _round * 0.010f);
                    _projectileSpeed = 7.2f + _round * 0.015f;
                    _shotDamage = 1;
                    _baseHuntBias = 0.30f;
                    break;

                case EnemyKind.Fast:
                    body = new Color(0.88f, 0.52f, 0.08f);
                    accent = new Color(1f, 0.94f, 0.48f);
                    hp = 1 + _round / 45;
                    _speed = 3.40f + _round * 0.012f;
                    _shotDelay = Mathf.Max(0.62f, 1.75f - _round * 0.008f);
                    _projectileSpeed = 8.6f;
                    _shotDamage = 1;
                    _baseHuntBias = 0.22f;
                    transform.localScale = Vector3.one * 0.94f;
                    break;

                case EnemyKind.Heavy:
                    body = new Color(0.38f, 0.12f, 0.54f);
                    accent = new Color(0.88f, 0.50f, 1f);
                    hp = 3 + _round / 20;
                    _speed = 1.65f + _round * 0.006f;
                    _shotDelay = Mathf.Max(0.90f, 2.35f - _round * 0.008f);
                    _projectileSpeed = 7.8f;
                    _shotDamage = 1 + _round / 60;
                    _baseHuntBias = 0.38f;
                    transform.localScale = Vector3.one * 1.12f;
                    break;

                case EnemyKind.Sniper:
                    body = new Color(0.08f, 0.48f, 0.28f);
                    accent = new Color(0.58f, 1f, 0.74f);
                    hp = 2 + _round / 40;
                    _speed = 1.82f + _round * 0.003f;
                    _shotDelay = Mathf.Max(1.00f, 2.80f - _round * 0.008f);
                    _projectileSpeed = 12.8f + _round * 0.012f;
                    _shotDamage = 2;
                    _baseHuntBias = 0.26f;
                    break;

                case EnemyKind.Siege:
                    body = new Color(0.22f, 0.25f, 0.31f);
                    accent = new Color(1f, 0.36f, 0.12f);
                    hp = 4 + _round / 18;
                    _speed = 1.45f + _round * 0.004f;
                    _shotDelay = Mathf.Max(0.78f, 2.15f - _round * 0.008f);
                    _projectileSpeed = 8.5f;
                    _shotDamage = 2 + _round / 55;
                    _baseHuntBias = 0.80f;
                    transform.localScale = Vector3.one * 1.18f;
                    break;

                case EnemyKind.Elite:
                    body = new Color(0.12f, 0.22f, 0.62f);
                    accent = new Color(0.30f, 0.90f, 1f);
                    hp = 5 + _round / 16;
                    _speed = 2.45f + _round * 0.007f;
                    _shotDelay = Mathf.Max(0.48f, 1.45f - _round * 0.006f);
                    _projectileSpeed = 10.4f;
                    _shotDamage = 2 + _round / 70;
                    _baseHuntBias = 0.45f;
                    transform.localScale = Vector3.one * 1.08f;
                    break;

                case EnemyKind.Supply:
                    body = Color.Lerp(AmmoDatabase.Color(supplyAmmo), Color.black, 0.42f);
                    accent = AmmoDatabase.Color(supplyAmmo);
                    hp = 2 + _round / 28;
                    _speed = 2.65f + _round * 0.006f;
                    _shotDelay = Mathf.Max(0.88f, 2.05f - _round * 0.006f);
                    _projectileSpeed = 8.4f;
                    _shotDamage = 1;
                    _baseHuntBias = 0.18f;
                    transform.localScale = Vector3.one * 1.04f;
                    break;

                case EnemyKind.Boss:
                    body = new Color(0.58f, 0.035f, 0.055f);
                    accent = new Color(1f, 0.70f, 0.08f);
                    hp = 10 + _round / 2;
                    _speed = 1.65f + _round * 0.004f;
                    _shotDelay = Mathf.Max(0.34f, 1.15f - _round * 0.004f);
                    _projectileSpeed = 10.2f + _round * 0.012f;
                    _shotDamage = 2 + _round / 50;
                    _baseHuntBias = 0.55f;
                    transform.localScale = Vector3.one * (1.48f + _round * 0.0012f);
                    break;
            }

            float progress = (_round - 1f) / 99f;
            float roundPressure = 1f + (_round - 1) * 0.0045f;
            _speed *= Mathf.Lerp(1f, 1.18f, progress);
            _projectileSpeed *= Mathf.Lerp(1f, 1.20f, progress);
            _shotDelay = Mathf.Max(0.28f, _shotDelay / roundPressure);
            _aggression = Mathf.Clamp01(0.38f + _round * 0.0055f + progress * 0.10f);
            _aimBias = Mathf.Lerp(0.06f, 0.33f, progress);

            VisualFactory.BuildTankSkin(transform, body, accent);
            gameObject.AddComponent<TrackDustEmitter>();
            if (Kind == EnemyKind.Supply)
                VisualFactory.BuildSupplyMarker(transform, SupplyAmmo);
            else if (Kind == EnemyKind.Boss)
                VisualFactory.BuildBossArmor(transform, _round);

            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.78f, 0.78f);

            _body = gameObject.AddComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;

            Health = gameObject.AddComponent<Health>();
            Health.Initialize(Team.Enemy, hp);
            _status = gameObject.AddComponent<CombatStatus>();
            Health.Died += _ => _game.OnEnemyDestroyed(this, transform.position, Kind);

            ChooseDirection(true);
            _nextThink = Time.time + Random.Range(0.30f, 0.85f);
            _nextShot = Time.time + Random.Range(0.50f, _shotDelay + 0.70f);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying) return;
            if (_status != null && _status.IsEmpDisabled) return;

            if (Time.time >= _nextThink)
            {
                ChooseDirection(false);
                float thinkRate = Mathf.Lerp(1.10f, 0.28f, _aggression);
                _nextThink = Time.time + Random.Range(thinkRate * 0.60f, thinkRate * 1.26f);
            }

            if (Time.time >= _nextShot)
            {
                Fire();
                _nextShot = Time.time + Random.Range(_shotDelay * 0.82f, _shotDelay * 1.18f);
            }
        }

        private void FixedUpdate()
        {
            if (_game == null || !_game.IsPlaying || _body == null) return;
            if (_status != null && _status.IsEmpDisabled) return;
            _body.MovePosition(_body.position + _facing * (_speed * Time.fixedDeltaTime));
        }

        private void ChooseDirection(bool forceRandom)
        {
            Vector2 desired;

            if (!forceRandom && Random.value < _aggression)
            {
                bool huntBase = Random.value < Mathf.Clamp01(_baseHuntBias + _aimBias * 0.18f);
                Vector2 target = huntBase ? _game.BasePosition : _game.PlayerPosition;
                Vector2 delta = target - (Vector2)transform.position;

                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    desired = new Vector2(Mathf.Sign(delta.x), 0f);
                else
                    desired = new Vector2(0f, Mathf.Sign(delta.y));

                float feintChance = Mathf.Lerp(0.22f, 0.07f, _aggression);
                if (Kind == EnemyKind.Sniper) feintChance += 0.08f;
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
            float muzzleDistance = Kind == EnemyKind.Boss ? 1.03f : Kind == EnemyKind.Siege ? 0.86f : 0.76f;
            Vector2 muzzle = (Vector2)transform.position + _facing * muzzleDistance;
            Color shellColor = Kind == EnemyKind.Elite ? new Color(0.25f, 0.86f, 1f) : new Color(1f, 0.30f, 0.08f);

            _game.SpawnProjectile(muzzle, _facing, Team.Enemy, _shotDamage, _projectileSpeed, shellColor, AmmoType.Basic);
            VisualFactory.MuzzleFlash(muzzle, shellColor, Kind == EnemyKind.Boss || Kind == EnemyKind.Siege ? 1.05f : 0.65f);

            if (Kind == EnemyKind.Boss || Kind == EnemyKind.Siege)
                BattleAudio.PlayGlobal(SoundCue.HeavyShot, Kind == EnemyKind.Boss ? 0.26f : 0.18f, 0.06f);
            else
                BattleAudio.PlayGlobal(SoundCue.EnemyShot, 0.11f, 0.08f);

            if (Kind == EnemyKind.Elite && _round >= 65 && Random.value < 0.34f)
            {
                Vector2 side = Perpendicular(_facing);
                _game.SpawnProjectile(muzzle + side * 0.20f, (_facing + side * 0.12f).normalized, Team.Enemy, _shotDamage, _projectileSpeed, shellColor, AmmoType.Basic);
            }

            if (Kind == EnemyKind.Boss && _round >= 50 && Random.value < Mathf.Lerp(0.38f, 0.62f, (_round - 50f) / 50f))
            {
                Vector2 side = Perpendicular(_facing);
                _game.SpawnProjectile(muzzle + side * 0.24f, (_facing + side * 0.18f).normalized, Team.Enemy, _shotDamage, _projectileSpeed, new Color(1f, 0.16f, 0.05f), AmmoType.Basic);
            }

            if (Kind == EnemyKind.Boss && _round >= 80 && Random.value < 0.32f)
            {
                Vector2 side = Perpendicular(_facing);
                _game.SpawnProjectile(muzzle - side * 0.24f, (_facing - side * 0.18f).normalized, Team.Enemy, _shotDamage, _projectileSpeed, new Color(1f, 0.10f, 0.03f), AmmoType.Basic);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_game != null && _game.IsPlaying)
            {
                ChooseDirection(true);
                _nextThink = Time.time + Random.Range(0.10f, 0.28f);
            }
        }
    }
}
