using UnityEngine;

namespace TankRevival
{
    public enum FriendlySupportRole
    {
        Guardian,
        Medic,
        Hunter
    }

    /// <summary>
    /// Lightweight allied vehicle used by v3.7 battlefield operations. It deliberately reuses
    /// TankGame.SpawnProjectile and Health so friendly fire, damage and projectile presentation
    /// stay inside the game's existing combat authority.
    /// </summary>
    public sealed class FriendlySupportUnit : MonoBehaviour
    {
        private TankGame _game;
        private Rigidbody2D _body;
        private Health _health;
        private FriendlySupportRole _role;
        private float _nextShot;
        private float _nextAbility;
        private float _spawnedAt;
        private Vector2 _anchor;

        public FriendlySupportRole Role => _role;
        public Health Health => _health;
        public bool IsAlive => _health != null && !_health.IsDead;

        public void Initialize(TankGame game, FriendlySupportRole role, Vector2 position)
        {
            _game = game;
            _role = role;
            _anchor = position;
            _spawnedAt = Time.time;
            transform.position = position;

            var hitbox = gameObject.AddComponent<BoxCollider2D>();
            hitbox.size = new Vector2(0.86f, 1.02f);

            _body = gameObject.AddComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.constraints = RigidbodyConstraints2D.FreezeRotation;

            int hp = role == FriendlySupportRole.Guardian ? 10 : role == FriendlySupportRole.Medic ? 7 : 8;
            _health = gameObject.AddComponent<Health>();
            _health.Initialize(Team.Player, hp);
            _health.Died += _ =>
            {
                VisualFactory.Explosion(transform.position, new Color(0.20f, 0.72f, 1f), 0.78f);
                BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.48f, 0.06f);
            };

            BuildVisuals();
        }

        private void BuildVisuals()
        {
            Color body = _role switch
            {
                FriendlySupportRole.Guardian => new Color(0.12f, 0.46f, 0.78f),
                FriendlySupportRole.Medic => new Color(0.18f, 0.72f, 0.48f),
                _ => new Color(0.55f, 0.38f, 0.90f)
            };

            VisualFactory.Rect("SupportHull", transform, new Vector2(0.78f, 0.96f), body, Vector3.zero, 18);
            VisualFactory.Rect("SupportTrackL", transform, new Vector2(0.16f, 1.02f), body * 0.42f, new Vector3(-0.43f, 0f, 0f), 17);
            VisualFactory.Rect("SupportTrackR", transform, new Vector2(0.16f, 1.02f), body * 0.42f, new Vector3(0.43f, 0f, 0f), 17);
            VisualFactory.Disc("SupportTurret", transform, new Vector2(0.52f, 0.52f), Color.Lerp(body, Color.white, 0.14f), new Vector3(0f, 0.08f, 0f), 20);
            VisualFactory.Rect("SupportBarrel", transform, new Vector2(0.12f, 0.58f), Color.Lerp(body, Color.white, 0.28f), new Vector3(0f, 0.46f, 0f), 19);
            VisualFactory.RingObject("SupportIFF", transform, new Vector2(0.94f, 0.94f), new Color(body.r, body.g, body.b, 0.62f), Vector3.zero, 16);
        }

        private void FixedUpdate()
        {
            if (_game == null || !_game.IsPlaying || !IsAlive || _body == null) return;

            PlayerTank player = CombatRoster.Player;
            EnemyTank target = PickTarget();
            Vector2 desired = _anchor;

            if (player != null)
            {
                Vector2 playerPos = player.transform.position;
                Vector2 offset = _role switch
                {
                    FriendlySupportRole.Guardian => new Vector2(-1.65f, 0.65f),
                    FriendlySupportRole.Medic => new Vector2(1.65f, -0.35f),
                    _ => new Vector2(0f, 1.8f)
                };
                desired = playerPos + offset;
            }

            if (target != null && _role == FriendlySupportRole.Hunter)
            {
                Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
                if (toTarget.magnitude > 5.2f) desired = target.transform.position;
            }

            Vector2 delta = desired - (Vector2)transform.position;
            float speed = _role == FriendlySupportRole.Hunter ? 4.1f : 3.35f;
            _body.linearVelocity = delta.magnitude > 0.45f ? delta.normalized * speed : Vector2.zero;

            if (_body.linearVelocity.sqrMagnitude > 0.04f)
                transform.up = Vector2.Lerp(transform.up, _body.linearVelocity.normalized, 0.18f);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || !IsAlive) return;

            EnemyTank target = PickTarget();
            if (target != null && Time.time >= _nextShot)
                FireAt(target);

            if (_role == FriendlySupportRole.Medic && Time.time >= _nextAbility)
            {
                _nextAbility = Time.time + 7.5f;
                PlayerTank player = CombatRoster.Player;
                if (player != null && player.Health != null && !player.Health.IsDead && Vector2.Distance(transform.position, player.transform.position) <= 3.2f)
                {
                    player.Health.Heal(1);
                    VisualFactory.RingPulse(player.transform.position, new Color(0.22f, 1f, 0.58f), 0.72f);
                }
                Health eagle = CombatRoster.Eagle;
                if (eagle != null && !eagle.IsDead && Vector2.Distance(transform.position, eagle.transform.position) <= 4.2f)
                    eagle.Heal(1);
            }
            else if (_role == FriendlySupportRole.Guardian && Time.time >= _nextAbility)
            {
                _nextAbility = Time.time + 6.8f;
                Health eagle = CombatRoster.Eagle;
                if (eagle != null && !eagle.IsDead && Vector2.Distance(transform.position, eagle.transform.position) <= 4.8f)
                {
                    eagle.InvulnerableUntil = Mathf.Max(eagle.InvulnerableUntil, Time.time + 0.75f);
                    VisualFactory.RingPulse(eagle.transform.position, new Color(0.22f, 0.70f, 1f), 0.9f);
                }
            }
        }

        private EnemyTank PickTarget()
        {
            EnemyTank best = null;
            float bestScore = float.MaxValue;
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                float distance = Vector2.Distance(transform.position, enemy.transform.position);
                if (distance > 8.5f) continue;
                float bias = enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Sniper ? -1.8f : 0f;
                float score = distance + bias;
                if (score < bestScore) { bestScore = score; best = enemy; }
            }
            return best;
        }

        private void FireAt(EnemyTank target)
        {
            Vector2 direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            if (direction.sqrMagnitude < 0.001f) return;
            transform.up = direction;

            int damage = _role == FriendlySupportRole.Hunter ? 2 : 1;
            float speed = _role == FriendlySupportRole.Hunter ? 13.5f : 10.8f;
            AmmoType ammo = _role == FriendlySupportRole.Hunter ? AmmoType.ArmorPiercing : AmmoType.Basic;
            Color color = _role == FriendlySupportRole.Medic ? new Color(0.25f, 1f, 0.58f) : new Color(0.20f, 0.72f, 1f);
            float delay = _role == FriendlySupportRole.Guardian ? 0.72f : _role == FriendlySupportRole.Medic ? 1.05f : 0.86f;
            _nextShot = Time.time + delay;
            _game.SpawnProjectile((Vector2)transform.position + direction * 0.62f, direction, Team.Player, damage, speed, color, ammo);
        }
    }
}
