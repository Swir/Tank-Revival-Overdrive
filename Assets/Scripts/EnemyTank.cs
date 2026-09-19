using UnityEngine;

namespace TankRevival
{
    public enum EnemyKind { Basic, Fast, Heavy, Sniper, Siege, Elite, Supply, Boss }

    public sealed class EnemyTank : MonoBehaviour
    {
        public EnemyKind Kind { get; private set; }
        public Health Health { get; private set; }
        public AmmoType SupplyAmmo { get; private set; } = AmmoType.Basic;
        public bool DropsAmmo => Kind == EnemyKind.Supply;
        public ComponentCasualtyTactic CurrentCasualtyTactic { get; private set; } = ComponentCasualtyTactic.FightThrough;
        public int CasualtyTransitions { get; private set; }

        private TankGame _game;
        private Rigidbody2D _body;
        private CombatStatus _status;
        private TankTurretRig _turret;
        private ArmorSystem _armor;
        private Vector2 _facing = Vector2.down, _gunDirection = Vector2.down;
        private float _speed, _shotDelay, _projectileSpeed, _nextShot, _nextThink, _aggression, _aimBias, _baseHuntBias;
        private float _nextCasualtyEvaluation;
        private int _shotDamage, _round;
        private bool _aimingAtPlayer;

        public void Initialize(TankGame game, EnemyKind kind, int round, AmmoType supplyAmmo = AmmoType.Basic)
        {
            _game = game; Kind = kind; SupplyAmmo = supplyAmmo; _round = Mathf.Clamp(round, 1, 100);
            Color body, accent; int hp;
            switch (kind)
            {
                default:
                case EnemyKind.Basic: body = new Color(.72f, .18f, .10f); accent = new Color(1f, .56f, .20f); hp = 1 + _round / 35; _speed = 2.20f + _round * .010f; _shotDelay = Mathf.Max(.75f, 2.25f - _round * .010f); _projectileSpeed = 7.2f + _round * .015f; _shotDamage = 1; _baseHuntBias = .30f; break;
                case EnemyKind.Fast: body = new Color(.88f, .52f, .08f); accent = new Color(1f, .94f, .48f); hp = 1 + _round / 45; _speed = 3.40f + _round * .012f; _shotDelay = Mathf.Max(.62f, 1.75f - _round * .008f); _projectileSpeed = 8.6f; _shotDamage = 1; _baseHuntBias = .22f; transform.localScale = Vector3.one * .94f; break;
                case EnemyKind.Heavy: body = new Color(.38f, .12f, .54f); accent = new Color(.88f, .50f, 1f); hp = 3 + _round / 20; _speed = 1.65f + _round * .006f; _shotDelay = Mathf.Max(.90f, 2.35f - _round * .008f); _projectileSpeed = 7.8f; _shotDamage = 1 + _round / 60; _baseHuntBias = .38f; transform.localScale = Vector3.one * 1.12f; break;
                case EnemyKind.Sniper: body = new Color(.08f, .48f, .28f); accent = new Color(.58f, 1f, .74f); hp = 2 + _round / 40; _speed = 1.82f + _round * .003f; _shotDelay = Mathf.Max(1f, 2.80f - _round * .008f); _projectileSpeed = 12.8f + _round * .012f; _shotDamage = 2; _baseHuntBias = .26f; break;
                case EnemyKind.Siege: body = new Color(.22f, .25f, .31f); accent = new Color(1f, .36f, .12f); hp = 4 + _round / 18; _speed = 1.45f + _round * .004f; _shotDelay = Mathf.Max(.78f, 2.15f - _round * .008f); _projectileSpeed = 8.5f; _shotDamage = 2 + _round / 55; _baseHuntBias = .80f; transform.localScale = Vector3.one * 1.18f; break;
                case EnemyKind.Elite: body = new Color(.12f, .22f, .62f); accent = new Color(.30f, .90f, 1f); hp = 5 + _round / 16; _speed = 2.45f + _round * .007f; _shotDelay = Mathf.Max(.48f, 1.45f - _round * .006f); _projectileSpeed = 10.4f; _shotDamage = 2 + _round / 70; _baseHuntBias = .45f; transform.localScale = Vector3.one * 1.08f; break;
                case EnemyKind.Supply: body = Color.Lerp(AmmoDatabase.Color(supplyAmmo), Color.black, .42f); accent = AmmoDatabase.Color(supplyAmmo); hp = 2 + _round / 28; _speed = 2.65f + _round * .006f; _shotDelay = Mathf.Max(.88f, 2.05f - _round * .006f); _projectileSpeed = 8.4f; _shotDamage = 1; _baseHuntBias = .18f; transform.localScale = Vector3.one * 1.04f; break;
                case EnemyKind.Boss: body = new Color(.58f, .035f, .055f); accent = new Color(1f, .70f, .08f); hp = 10 + _round / 2; _speed = 1.65f + _round * .004f; _shotDelay = Mathf.Max(.34f, 1.15f - _round * .004f); _projectileSpeed = 10.2f + _round * .012f; _shotDamage = 2 + _round / 50; _baseHuntBias = .55f; transform.localScale = Vector3.one * (1.48f + _round * .0012f); break;
            }

            float progress = (_round - 1f) / 99f, pressure = 1f + (_round - 1) * .0045f;
            _speed *= Mathf.Lerp(1f, 1.18f, progress);
            _projectileSpeed *= Mathf.Lerp(1f, 1.20f, progress);
            _shotDelay = Mathf.Max(.28f, _shotDelay / pressure);
            _aggression = Mathf.Clamp01(.38f + _round * .0055f + progress * .10f);
            _aimBias = Mathf.Lerp(.06f, .33f, progress);

            VisualFactory.BuildTankSkin(transform, body, accent);
            _turret = gameObject.AddComponent<TankTurretRig>(); _turret.Initialize();
            if (Kind == EnemyKind.Heavy || Kind == EnemyKind.Siege || Kind == EnemyKind.Supply || Kind == EnemyKind.Boss) gameObject.AddComponent<TrackDustEmitter>();
            if (Kind == EnemyKind.Supply) VisualFactory.BuildSupplyMarker(transform, SupplyAmmo); else if (Kind == EnemyKind.Boss) VisualFactory.BuildBossArmor(transform, _round);
            var col = gameObject.AddComponent<BoxCollider2D>(); col.size = new Vector2(.78f, .78f);
            _body = gameObject.AddComponent<Rigidbody2D>(); _body.gravityScale = 0f; _body.freezeRotation = true; _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous; _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Health = gameObject.AddComponent<Health>(); Health.Initialize(Team.Enemy, hp);
            _status = gameObject.AddComponent<CombatStatus>();
            _armor = gameObject.AddComponent<ArmorSystem>(); _armor.InitializeEnemy(Kind, _round);
            if (Kind == EnemyKind.Boss) { var w = gameObject.AddComponent<BossWeaponController>(); w.Initialize(_round); }
            Health.Died += _ => _game.OnEnemyDestroyed(this, transform.position, Kind);
            Health.Damaged += OnDamaged;
            BattlefieldCohesionDirector.EnsureInstalled().Register(this, Kind, _round);
            BattlefieldSuppressionMoraleDirector.EnsureInstalled().Register(this, Kind);
            CurrentCasualtyTactic = ComponentCasualtyTactic.FightThrough;
            _nextCasualtyEvaluation = Time.time;
            ChooseDirection(true);
            UpdateTurretAim();
            _nextThink = Time.time + Random.Range(.30f, .85f);
            _nextShot = Time.time + Random.Range(.50f, _shotDelay + .70f);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || (_status != null && _status.IsEmpDisabled)) return;
            RefreshCasualtyTactic(false);
            UpdateTurretAim();
            if (Time.time >= _nextThink)
            {
                ChooseDirection(false);
                float r = Mathf.Lerp(1.10f, .28f, _aggression);
                _nextThink = Time.time + Random.Range(r * .60f, r * 1.26f);
            }
            if (Time.time >= _nextShot)
            {
                if (!ComponentCasualtyTactics.CanFire(CurrentCasualtyTactic, _armor))
                {
                    _nextShot = Time.time + .24f;
                    return;
                }
                float phase = AdvancedGunneryDoctrineDirector.VolleyPhaseBias(Kind, _round);
                float hold = FireControlVolleyCoordinator.HoldForWindow(Time.time + phase, GetInstanceID(), Kind, _round);
                if (hold > 0f) { _nextShot = Time.time + hold; return; }
                Fire();
                float reload = _armor != null ? _armor.ReloadMultiplier : 1f;
                _nextShot = Time.time + Random.Range(_shotDelay * .82f, _shotDelay * 1.18f) * reload
                    * AdaptiveEnemyCommandDirector.ReloadScale(Kind) * BattlefieldCohesionDirector.ReloadScale(this)
                    * BattlefieldWeatherDirector.EnemyReloadScale(Kind) * BattlefieldSuppressionMoraleDirector.ReloadScale(this);
            }
        }

        private bool ResolvePlatoonTarget(bool localPreference)
        {
            bool counter = CounterFireThreatMemory.ShouldRetaliate(this, _round);
            bool doctrine = localPreference || AdvancedGunneryDoctrineDirector.PreferPlayer(Kind, _round) || AdaptiveEnemyCommandDirector.PreferPlayer(Kind) || BattlefieldCohesionDirector.PreferPlayer(this) || counter;
            return PlatoonFireMissionCoordinator.PreferPlayer(this, Kind, _round, doctrine, counter);
        }

        private void UpdateTurretAim()
        {
            bool doctrinePlayer = ResolvePlatoonTarget(false);
            Vector2 target;
            if (Kind == EnemyKind.Siege && !doctrinePlayer) { target = _game.BasePosition; _aimingAtPlayer = false; }
            else if (Kind == EnemyKind.Sniper || Kind == EnemyKind.Elite || doctrinePlayer) { target = _game.PlayerPosition; _aimingAtPlayer = true; }
            else { _aimingAtPlayer = Random.value >= _baseHuntBias; target = _aimingAtPlayer ? _game.PlayerPosition : _game.BasePosition; }
            Vector2 d = target - (Vector2)transform.position;
            _gunDirection = d.sqrMagnitude > .05f ? d.normalized : _facing;
            _turret?.SetAimDirection(_gunDirection);
        }

        private void FixedUpdate()
        {
            if (_game == null || !_game.IsPlaying || _body == null || (_status != null && _status.IsEmpDisabled)) return;
            float casualtyScale = ComponentCasualtyTactics.SpeedScale(CurrentCasualtyTactic);
            if (casualtyScale <= 0f) return;
            float m = _armor != null ? _armor.MobilityMultiplier : 1f;
            float weatherMobility = BattlefieldWeatherDirector.MobilityScale(Team.Enemy, transform.position);
            _body.MovePosition(_body.position + _facing * (_speed * m * casualtyScale
                * AdaptiveEnemyCommandDirector.MovementScale(Kind) * BattlefieldCohesionDirector.MovementScale(this)
                * BattlefieldSuppressionMoraleDirector.MovementScale(this) * weatherMobility * Time.fixedDeltaTime));
        }

        private void ChooseDirection(bool random)
        {
            Vector2 desired;
            if (!random && Random.value < _aggression)
            {
                bool doctrinePlayer = ResolvePlatoonTarget(false);
                bool counter = CounterFireThreatMemory.ShouldRetaliate(this, _round);
                if (Kind == EnemyKind.Heavy || Kind == EnemyKind.Sniper || Kind == EnemyKind.Siege || Kind == EnemyKind.Elite)
                {
                    desired = AdaptivePlatoonManeuverDirector.DesiredDirection(this, Kind, transform.position, _game.PlayerPosition, _game.BasePosition, doctrinePlayer, counter, _round);
                }
                else
                {
                    bool baseHunt = !doctrinePlayer && Random.value < Mathf.Clamp01(_baseHuntBias + _aimBias * .18f);
                    Vector2 d = (baseHunt ? _game.BasePosition : _game.PlayerPosition) - (Vector2)transform.position;
                    desired = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? new Vector2(Mathf.Sign(d.x), 0) : new Vector2(0, Mathf.Sign(d.y));
                    float f = Mathf.Lerp(.22f, .07f, _aggression);
                    if (Kind == EnemyKind.Sniper) f += .08f;
                    if (Random.value < f) desired = Perpendicular(desired);
                }
            }
            else
            {
                int d = Random.Range(0, 4);
                desired = d == 0 ? Vector2.up : d == 1 ? Vector2.right : d == 2 ? Vector2.down : Vector2.left;
            }

            desired = ComponentCasualtyTactics.AdjustDirection(CurrentCasualtyTactic,
                transform.position, _game.PlayerPosition, desired, GetInstanceID());
            desired = BattlefieldCohesionDirector.AdjustDirection(this, transform.position, desired);
            desired = BattlefieldSuppressionMoraleDirector.AdjustDirection(this, transform.position, _game.PlayerPosition, desired);
            desired = TacticalTerrainDirector.AdjustDirection(this, transform.position, _game.PlayerPosition, desired);
            if (desired.sqrMagnitude < .001f) return;
            _facing = desired;
            ApplyFacingRotation();
        }

        private void RefreshCasualtyTactic(bool force)
        {
            if (_armor == null || _game == null) return;
            if (!force && Time.time < _nextCasualtyEvaluation) return;
            _nextCasualtyEvaluation = Time.time + ComponentCasualtyTactics.EvaluationInterval;
            EmergencyRepairSystem repair = GetComponent<EmergencyRepairSystem>();
            bool hasRepair = repair != null && repair.ChargesRemaining > 0;
            float distance = Vector2.Distance(transform.position, _game.PlayerPosition);
            ComponentCasualtyTactic next = ComponentCasualtyTactics.Resolve(Kind, _armor, distance, hasRepair);
            if (next == CurrentCasualtyTactic) return;
            CurrentCasualtyTactic = next;
            CasualtyTransitions++;
            _nextThink = Mathf.Min(_nextThink, Time.time + .08f);
        }

        private static Vector2 Perpendicular(Vector2 v) => Random.value < .5f ? new Vector2(-v.y, v.x) : new Vector2(v.y, -v.x);

        private void ApplyFacingRotation()
        {
            float a = 0;
            if (_facing == Vector2.right) a = -90;
            else if (_facing == Vector2.down) a = 180;
            else if (_facing == Vector2.left) a = 90;
            transform.rotation = Quaternion.Euler(0, 0, a);
        }

        private void Fire()
        {
            Vector2 raw = _gunDirection.sqrMagnitude > .001f ? _gunDirection.normalized : _facing;
            if (_aimingAtPlayer)
            {
                PlayerTank p = FindAnyObjectByType<PlayerTank>();
                if (p != null)
                {
                    Rigidbody2D pb = p.GetComponent<Rigidbody2D>();
                    Vector2 v = pb != null ? pb.linearVelocity : Vector2.zero;
                    Vector2 lead = FireControlBallisticsDirector.Lead(transform.position, p.transform.position, v, _projectileSpeed);
                    Vector2 d = lead - (Vector2)transform.position;
                    if (d.sqrMagnitude > .01f) raw = d.normalized;
                }
            }
            float movement = _body != null ? Mathf.Clamp01(_body.linearVelocity.magnitude / Mathf.Max(.1f, _speed)) : 0;
            bool coordinated = FireControlVolleyCoordinator.IsEligible(Kind, _round);
            float spread = FireControlBallisticsDirector.EnemySpreadDegrees(Kind, movement, _armor, coordinated)
                * AdvancedGunneryDoctrineDirector.SpreadMultiplier(Kind, _round)
                * CounterFireThreatMemory.AccuracyMultiplier(this)
                * AdaptiveEnemyCommandDirector.SpreadScale(Kind)
                * BattlefieldCohesionDirector.SpreadScale(this)
                * BattlefieldWeatherDirector.EnemySpreadScale(Kind)
                * BattlefieldSuppressionMoraleDirector.SpreadScale(this);
            Vector2 dir = FireControlBallisticsDirector.ApplySpread(raw, spread);
            float md = Kind == EnemyKind.Boss ? 1.03f : Kind == EnemyKind.Siege ? .86f : .76f;
            Vector2 muzzle = (Vector2)transform.position + dir * md;
            Color c = Kind == EnemyKind.Elite ? new Color(.25f, .86f, 1f) : new Color(1f, .30f, .08f);
            _game.SpawnProjectile(muzzle, dir, Team.Enemy, _shotDamage, _projectileSpeed, c, AmmoType.Basic);
            _turret?.KickRecoil(Kind == EnemyKind.Boss ? 1.8f : Kind == EnemyKind.Siege || Kind == EnemyKind.Heavy ? 1.35f : .85f);
            VisualFactory.MuzzleFlash(muzzle, c, Kind == EnemyKind.Boss || Kind == EnemyKind.Siege ? 1.05f : .65f);
            if (Kind == EnemyKind.Boss || Kind == EnemyKind.Siege) BattleAudio.PlayGlobal(SoundCue.HeavyShot, Kind == EnemyKind.Boss ? .26f : .18f, .06f); else BattleAudio.PlayGlobal(SoundCue.EnemyShot, .11f, .08f);
            Vector2 side = new Vector2(-dir.y, dir.x);
            if (Kind == EnemyKind.Elite && _round >= 65 && Random.value < .34f) _game.SpawnProjectile(muzzle + side * .20f, (dir + side * .12f).normalized, Team.Enemy, _shotDamage, _projectileSpeed, c, AmmoType.Basic);
            if (Kind == EnemyKind.Boss && _round >= 50 && Random.value < Mathf.Lerp(.38f, .62f, (_round - 50f) / 50f)) _game.SpawnProjectile(muzzle + side * .24f, (dir + side * .18f).normalized, Team.Enemy, _shotDamage, _projectileSpeed, new Color(1f, .16f, .05f), AmmoType.Basic);
            if (Kind == EnemyKind.Boss && _round >= 80 && Random.value < .32f) _game.SpawnProjectile(muzzle - side * .24f, (dir - side * .18f).normalized, Team.Enemy, _shotDamage, _projectileSpeed, new Color(1f, .10f, .03f), AmmoType.Basic);
        }

        private void OnDamaged(Health health, int amount)
        {
            if (amount <= 0) return;
            CounterFireThreatMemory.RecordPlayerHit(this);
            RefreshCasualtyTactic(true);
        }

        private void OnDestroy()
        {
            BattlefieldCohesionDirector.Instance?.Unregister(this);
            BattlefieldSuppressionMoraleDirector.Instance?.Unregister(this);
            AdaptivePlatoonManeuverDirector.NotifyLoss(Kind);
            PlatoonFireMissionCoordinator.Release(this);
        }

        private void OnCollisionEnter2D(Collision2D c)
        {
            if (_game == null || !_game.IsPlaying) return;
            ChooseDirection(true);
            _nextThink = Time.time + Random.Range(.10f, .28f);
        }
    }
}
