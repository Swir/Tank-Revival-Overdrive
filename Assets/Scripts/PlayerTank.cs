using UnityEngine;

namespace TankRevival
{
    public sealed class PlayerTank : MonoBehaviour
    {
        public Health Health { get; private set; }
        public int ShotDamage { get; private set; } = 1;
        public float FireDelay { get; private set; } = 0.34f;
        public float MoveSpeed { get; private set; } = 4.8f;

        private TankGame _game;
        private Rigidbody2D _body;
        private Vector2 _move;
        private Vector2 _facing = Vector2.up;
        private float _nextShot;

        public void Initialize(TankGame game)
        {
            _game = game;

            VisualFactory.BuildTankSkin(transform, new Color(0.12f, 0.72f, 0.95f), new Color(0.82f, 0.96f, 1f));

            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.78f, 0.78f);

            _body = gameObject.AddComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;

            Health = gameObject.AddComponent<Health>();
            Health.Initialize(Team.Player, 3);
            Health.Died += _ => _game.OnPlayerDestroyed(transform.position);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying) return;

            float x = 0f;
            float y = 0f;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;

            if (Mathf.Abs(x) > 0.01f)
                _move = new Vector2(Mathf.Sign(x), 0f);
            else if (Mathf.Abs(y) > 0.01f)
                _move = new Vector2(0f, Mathf.Sign(y));
            else
                _move = Vector2.zero;

            if (_move.sqrMagnitude > 0.01f)
            {
                _facing = _move;
                ApplyFacingRotation();
            }

            if ((Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftControl)) && Time.time >= _nextShot)
            {
                Fire();
            }
        }

        private void FixedUpdate()
        {
            if (_game == null || !_game.IsPlaying || _body == null) return;
            _body.MovePosition(_body.position + _move * (MoveSpeed * Time.fixedDeltaTime));
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
            _nextShot = Time.time + FireDelay;
            Vector2 muzzle = (Vector2)transform.position + _facing * 0.72f;
            _game.SpawnProjectile(muzzle, _facing, Team.Player, ShotDamage, 10.5f, new Color(0.25f, 0.95f, 1f));
            _game.KickCamera(0.045f, 0.035f);
        }

        public void ApplyPowerUp(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Repair:
                    Health.Heal(2);
                    break;
                case PowerUpKind.RapidFire:
                    FireDelay = Mathf.Max(0.13f, FireDelay - 0.055f);
                    break;
                case PowerUpKind.PowerShot:
                    ShotDamage = Mathf.Min(4, ShotDamage + 1);
                    break;
                case PowerUpKind.Speed:
                    MoveSpeed = Mathf.Min(7.3f, MoveSpeed + 0.45f);
                    break;
                case PowerUpKind.Shield:
                    Health.InvulnerableUntil = Mathf.Max(Health.InvulnerableUntil, Time.time + 6f);
                    break;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var powerUp = other.GetComponent<PowerUp>();
            if (powerUp != null)
            {
                ApplyPowerUp(powerUp.Kind);
                _game.OnPowerUpCollected(powerUp.Kind);
                Destroy(powerUp.gameObject);
            }
        }
    }
}
