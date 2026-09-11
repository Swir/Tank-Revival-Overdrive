using UnityEngine;

namespace TankRevival
{
    public sealed class PlayerTank : MonoBehaviour
    {
        public Health Health { get; private set; }
        public int ShotDamage { get; private set; } = 1;
        public float FireDelay { get; private set; } = 0.34f;
        public float MoveSpeed { get; private set; } = 4.8f;
        public AmmoType ActiveAmmo { get; private set; } = AmmoType.Basic;

        public int EffectiveShotDamage => Mathf.Clamp(ShotDamage + _commanderCannonLevel, 1, 8);
        public float EffectiveFireDelay => Mathf.Max(0.075f, FireDelay * (1f - _commanderLoaderLevel * 0.085f));
        public float EffectiveMoveSpeed => Mathf.Min(9.2f, MoveSpeed + _commanderEngineLevel * 0.34f);
        public int CommanderArmorLevel => _commanderArmorLevel;

        private TankGame _game;
        private Rigidbody2D _body;
        private TankTurretRig _turret;
        private ArmorSystem _armor;
        private Vector2 _move;
        private Vector2 _facing = Vector2.up;
        private Vector2 _gunDirection = Vector2.up;
        private float _nextShot;
        private readonly int[] _ammo = new int[AmmoDatabase.AmmoTypeCount];

        private int _commanderCannonLevel;
        private int _commanderLoaderLevel;
        private int _commanderEngineLevel;
        private int _commanderArmorLevel;

        public void Initialize(TankGame game)
        {
            _game = game;
            ActiveAmmo = AmmoType.Basic;

            VisualFactory.BuildTankSkin(transform, new Color(0.10f, 0.58f, 0.86f), new Color(0.78f, 0.96f, 1f));
            gameObject.AddComponent<TrackDustEmitter>();

            _turret = gameObject.AddComponent<TankTurretRig>();
            _turret.Initialize();

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

            _armor = gameObject.AddComponent<ArmorSystem>();
            _armor.InitializePlayer();
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying)
            {
                BattleAudio.Instance?.SetEngineMoving(false, 0f);
                return;
            }

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

            UpdateTurretAim();

            float moduleMobility = _armor != null ? _armor.MobilityMultiplier : 1f;
            BattleAudio.Instance?.SetEngineMoving(_move.sqrMagnitude > 0.01f, (EffectiveMoveSpeed * moduleMobility) / 9.2f);
            HandleAmmoSelection();

            if ((Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftControl) || Input.GetMouseButton(0)) && Time.time >= _nextShot)
                Fire();
        }

        private void UpdateTurretAim()
        {
            Vector2 desired = _facing;
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
                Vector2 delta = (Vector2)mouse - (Vector2)transform.position;
                if (delta.sqrMagnitude > 0.10f)
                    desired = delta.normalized;
            }

            _gunDirection = desired.normalized;
            if (_turret != null)
                _turret.SetAimDirection(_gunDirection);
        }

        private void FixedUpdate()
        {
            if (_game == null || !_game.IsPlaying || _body == null) return;
            float moduleMobility = _armor != null ? _armor.MobilityMultiplier : 1f;
            _body.MovePosition(_body.position + _move * (EffectiveMoveSpeed * moduleMobility * Time.fixedDeltaTime));
        }

        private void OnDisable()
        {
            BattleAudio.Instance?.SetEngineMoving(false, 0f);
        }

        private void HandleAmmoSelection()
        {
            if (Input.GetKeyDown(KeyCode.Q)) CycleAmmo(-1);
            if (Input.GetKeyDown(KeyCode.E)) CycleAmmo(1);

            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectAmmo(AmmoType.Basic);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectAmmo(AmmoType.ArmorPiercing);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectAmmo(AmmoType.Explosive);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectAmmo(AmmoType.Incendiary);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SelectAmmo(AmmoType.EMP);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SelectAmmo(AmmoType.Twin);
            if (Input.GetKeyDown(KeyCode.Alpha7)) SelectAmmo(AmmoType.Plasma);
        }

        private void SelectAmmo(AmmoType type)
        {
            if (type == AmmoType.Basic || GetAmmoCount(type) > 0)
                ActiveAmmo = type;
        }

        private void CycleAmmo(int direction)
        {
            int current = (int)ActiveAmmo;
            for (int step = 1; step <= AmmoDatabase.AmmoTypeCount; step++)
            {
                int index = (current + direction * step) % AmmoDatabase.AmmoTypeCount;
                if (index < 0) index += AmmoDatabase.AmmoTypeCount;
                var candidate = (AmmoType)index;
                if (candidate == AmmoType.Basic || GetAmmoCount(candidate) > 0)
                {
                    ActiveAmmo = candidate;
                    return;
                }
            }
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
            AmmoType ammo = ActiveAmmo;
            int damage = EffectiveShotDamage + AmmoDatabase.BonusDamage(ammo);
            float speed = 10.5f * AmmoDatabase.SpeedMultiplier(ammo) * (1f + _commanderCannonLevel * 0.025f);
            Color color = AmmoDatabase.Color(ammo);
            Vector2 direction = _gunDirection.sqrMagnitude > 0.001f ? _gunDirection.normalized : _facing;
            Vector2 muzzle = (Vector2)transform.position + direction * 0.82f;
            Vector2 side = new Vector2(-direction.y, direction.x);
            float moduleReload = _armor != null ? _armor.ReloadMultiplier : 1f;

            _nextShot = Time.time + EffectiveFireDelay * moduleReload * (ammo == AmmoType.Twin ? 1.08f : 1f);

            if (ammo == AmmoType.Twin)
            {
                _game.SpawnProjectile(muzzle + side * 0.18f, direction, Team.Player, damage, speed, color, ammo);
                _game.SpawnProjectile(muzzle - side * 0.18f, direction, Team.Player, damage, speed, color, ammo);
            }
            else
            {
                _game.SpawnProjectile(muzzle, direction, Team.Player, damage, speed, color, ammo);
            }

            _turret?.KickRecoil(ammo == AmmoType.Plasma ? 1.75f : ammo == AmmoType.Explosive || ammo == AmmoType.ArmorPiercing ? 1.35f : 1f);
            VisualFactory.MuzzleFlash(muzzle, color, ammo == AmmoType.Plasma ? 1.35f : ammo == AmmoType.Explosive ? 1.15f : 0.90f);
            _game.KickCamera(ammo == AmmoType.Plasma ? 0.085f : 0.050f, ammo == AmmoType.Plasma ? 0.060f : 0.038f);

            if (ammo == AmmoType.Plasma)
                BattleAudio.PlayGlobal(SoundCue.Plasma, 0.78f);
            else if (ammo == AmmoType.Explosive || ammo == AmmoType.ArmorPiercing)
                BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.72f);
            else
                BattleAudio.PlayGlobal(SoundCue.PlayerShot, 0.66f);

            ConsumeAmmo(ammo);
        }

        private void ConsumeAmmo(AmmoType type)
        {
            if (type == AmmoType.Basic) return;
            int index = (int)type;
            _ammo[index] = Mathf.Max(0, _ammo[index] - 1);
            if (_ammo[index] <= 0)
                ActiveAmmo = AmmoType.Basic;
        }

        public void AddAmmo(AmmoType type, int amount)
        {
            if (type == AmmoType.Basic || amount <= 0) return;
            int index = (int)type;
            _ammo[index] = Mathf.Min(99, _ammo[index] + amount);
            ActiveAmmo = type;
        }

        public int GetAmmoCount(AmmoType type)
        {
            if (type == AmmoType.Basic) return -1;
            return _ammo[(int)type];
        }

        public int[] CopyAmmoInventory()
        {
            var copy = new int[_ammo.Length];
            for (int i = 0; i < _ammo.Length; i++) copy[i] = _ammo[i];
            return copy;
        }

        public void RestoreLoadout(int shotDamage, float fireDelay, float moveSpeed, int[] ammo, AmmoType activeAmmo)
        {
            ShotDamage = Mathf.Clamp(shotDamage, 1, 4);
            FireDelay = Mathf.Clamp(fireDelay, 0.13f, 0.34f);
            MoveSpeed = Mathf.Clamp(moveSpeed, 4.8f, 7.3f);

            if (ammo != null)
            {
                int count = Mathf.Min(ammo.Length, _ammo.Length);
                for (int i = 0; i < count; i++) _ammo[i] = Mathf.Clamp(ammo[i], 0, 99);
            }

            ActiveAmmo = activeAmmo == AmmoType.Basic || GetAmmoCount(activeAmmo) > 0 ? activeAmmo : AmmoType.Basic;
        }

        public void SetCommanderUpgrades(int cannonLevel, int loaderLevel, int engineLevel, int armorLevel)
        {
            cannonLevel = Mathf.Clamp(cannonLevel, 0, 3);
            loaderLevel = Mathf.Clamp(loaderLevel, 0, 4);
            engineLevel = Mathf.Clamp(engineLevel, 0, 4);
            armorLevel = Mathf.Clamp(armorLevel, 0, 4);

            int oldArmor = _commanderArmorLevel;
            _commanderCannonLevel = cannonLevel;
            _commanderLoaderLevel = loaderLevel;
            _commanderEngineLevel = engineLevel;
            _commanderArmorLevel = armorLevel;

            if (Health != null)
                Health.SetMaximum(3 + _commanderArmorLevel, _commanderArmorLevel > oldArmor);
        }

        public void ApplyPowerUp(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Repair:
                    Health.Heal(2);
                    _game.RepairEagle(1);
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
            var ammoPickup = other.GetComponent<AmmoPickup>();
            if (ammoPickup != null)
            {
                AmmoType kind = ammoPickup.Kind;
                int amount = ammoPickup.Amount;
                AddAmmo(kind, amount);
                _game.OnAmmoCollected(kind, amount);
                Destroy(ammoPickup.gameObject);
                return;
            }

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
