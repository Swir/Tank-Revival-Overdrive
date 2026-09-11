using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Extra anti-armor behavior for advanced enemy classes. Hunters telegraph precision
    /// attacks, inspect the player's current module state and choose ammunition that
    /// pressures damaged systems instead of blindly adding raw fire rate.
    /// </summary>
    public sealed class ArmorHunterAgent : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _enemy;
        private int _round;
        private float _nextAttack;
        private float _fireAt;
        private bool _charging;
        private AmmoType _plannedAmmo;
        private int _plannedDamage;
        private float _plannedSpeed;

        public void Initialize(TankGame game, EnemyTank enemy, int round)
        {
            _game = game;
            _enemy = enemy;
            _round = Mathf.Clamp(round, 1, 100);
            _nextAttack = Time.time + Random.Range(3.0f, 5.5f);
        }

        private void Update()
        {
            if (_game == null || _enemy == null || !_game.IsPlaying) return;
            if (_enemy.Health == null || _enemy.Health.IsDead) return;

            if (_charging)
            {
                if (Time.time >= _fireAt)
                {
                    _charging = false;
                    FirePrecisionRound();
                    _nextAttack = Time.time + AttackCooldown();
                }
                return;
            }

            if (Time.time < _nextAttack) return;
            if (!EligibleThisCycle())
            {
                _nextAttack = Time.time + 1.4f;
                return;
            }

            PrepareAttack();
        }

        private bool EligibleThisCycle()
        {
            float chance = _enemy.Kind switch
            {
                EnemyKind.Boss => 1f,
                EnemyKind.Elite => 0.82f,
                EnemyKind.Sniper => 0.78f,
                EnemyKind.Siege => 0.68f,
                EnemyKind.Heavy => 0.54f,
                _ => 0.25f
            };
            chance += Mathf.Clamp01((_round - 20f) / 100f) * 0.15f;
            return Random.value <= chance;
        }

        private void PrepareAttack()
        {
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player == null)
            {
                _nextAttack = Time.time + 1.0f;
                return;
            }

            ArmorSystem playerArmor = player.GetComponent<ArmorSystem>();
            bool mobilityWeak = playerArmor != null && playerArmor.IsMobilityCritical;
            bool weaponWeak = playerArmor != null && playerArmor.IsWeaponCritical;

            if (_enemy.Kind == EnemyKind.Siege)
                _plannedAmmo = AmmoType.Explosive;
            else if (_enemy.Kind == EnemyKind.Elite && mobilityWeak)
                _plannedAmmo = AmmoType.EMP;
            else if (_enemy.Kind == EnemyKind.Boss && _round >= 70 && weaponWeak)
                _plannedAmmo = AmmoType.Plasma;
            else
                _plannedAmmo = AmmoType.ArmorPiercing;

            _plannedDamage = _enemy.Kind == EnemyKind.Boss ? 3 : _round >= 60 ? 2 : 1;
            if (_plannedAmmo == AmmoType.Plasma) _plannedDamage += 1;
            _plannedSpeed = 10.5f + _round * 0.025f;
            if (_plannedAmmo == AmmoType.ArmorPiercing) _plannedSpeed *= 1.18f;

            _charging = true;
            float telegraph = _enemy.Kind == EnemyKind.Sniper ? 0.72f : _enemy.Kind == EnemyKind.Boss ? 0.85f : 1.05f;
            _fireAt = Time.time + telegraph;

            Color c = AmmoDatabase.Color(_plannedAmmo);
            VisualFactory.RingPulse(transform.position, c, _enemy.Kind == EnemyKind.Boss ? 1.18f : 0.82f);
            VisualFactory.MicroBurst(transform.position, c, 0.36f);
        }

        private void FirePrecisionRound()
        {
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player == null) return;

            Vector2 origin = transform.position;
            Vector2 target = player.transform.position;
            Vector2 direction = target - origin;
            if (direction.sqrMagnitude < 0.01f) return;
            direction.Normalize();

            float muzzleDistance = _enemy.Kind == EnemyKind.Boss ? 1.0f : 0.78f;
            Vector2 muzzle = origin + direction * muzzleDistance;
            Color c = AmmoDatabase.Color(_plannedAmmo);
            _game.SpawnProjectile(muzzle, direction, Team.Enemy, _plannedDamage, _plannedSpeed, c, _plannedAmmo);
            VisualFactory.MuzzleFlash(muzzle, c, _enemy.Kind == EnemyKind.Boss ? 1.12f : 0.78f);

            if (_enemy.Kind == EnemyKind.Boss && _round >= 80)
            {
                Vector2 side = new Vector2(-direction.y, direction.x);
                _game.SpawnProjectile(muzzle + side * 0.20f, (direction + side * 0.09f).normalized, Team.Enemy,
                    Mathf.Max(1, _plannedDamage - 1), _plannedSpeed * 0.96f, c, _plannedAmmo);
            }

            BattleAudio.PlayGlobal(_plannedAmmo == AmmoType.Plasma ? SoundCue.Plasma : SoundCue.HeavyShot,
                _enemy.Kind == EnemyKind.Boss ? 0.44f : 0.22f, 0.06f);
        }

        private float AttackCooldown()
        {
            float baseTime = _enemy.Kind switch
            {
                EnemyKind.Boss => 3.4f,
                EnemyKind.Elite => 4.1f,
                EnemyKind.Sniper => 4.6f,
                EnemyKind.Siege => 5.0f,
                _ => 5.7f
            };
            return Mathf.Max(2.5f, baseTime - _round * 0.012f) + Random.Range(0.0f, 1.2f);
        }
    }
}
