using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Functional role layer for enemy tanks. It composes with EnemyTank movement/aiming
    /// and adds faction-specific attacks, support actions and visible role markers.
    /// </summary>
    public sealed class EnemyDoctrineAgent : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _tank;
        private Health _health;
        private EnemyFaction _faction;
        private EnemyBattleRole _role;
        private int _round;
        private float _nextAction;
        private float _nextPulse;

        public EnemyFaction Faction => _faction;
        public EnemyBattleRole Role => _role;

        public void Initialize(TankGame game, EnemyFaction faction, EnemyBattleRole role, int round)
        {
            _game = game;
            _tank = GetComponent<EnemyTank>();
            _health = GetComponent<Health>();
            _faction = faction;
            _role = role;
            _round = Mathf.Clamp(round, 1, 100);

            ApplyFactionDurability();
            BuildMarker();
            _nextAction = Time.time + Random.Range(1.8f, 4.2f);
            _nextPulse = Time.time + Random.Range(0.4f, 1.2f);
        }

        private void ApplyFactionDurability()
        {
            if (_health == null) return;
            int extra = 0;
            if (_faction == EnemyFaction.IronLegion) extra += 1;
            if (_faction == EnemyFaction.BlackGuard && _round >= 65) extra += 1;
            if (_faction == EnemyFaction.OverdriveHost) extra += 2;
            if (_role == EnemyBattleRole.Breacher) extra += 1;
            if (extra > 0)
                _health.SetMaximum(_health.Maximum + extra, true);
        }

        private void BuildMarker()
        {
            Color c = EnemyFactionDirector.FactionColor(_faction);
            float scale = _role == EnemyBattleRole.Breacher ? 1.28f : 1.08f;
            VisualFactory.RingObject("FactionRoleRing", transform, Vector2.one * scale, new Color(c.r, c.g, c.b, 0.44f), Vector3.zero, 18);

            Vector3 badgePos = new Vector3(0f, -0.57f, 0f);
            switch (_role)
            {
                case EnemyBattleRole.Breacher:
                    VisualFactory.Rect("BreacherBadge", transform, new Vector2(0.34f, 0.09f), c, badgePos, 19);
                    break;
                case EnemyBattleRole.Raider:
                    VisualFactory.RectRotated("RaiderBadgeA", transform, new Vector2(0.24f, 0.06f), c, badgePos + new Vector3(-0.07f, 0f, 0f), 28f, 19);
                    VisualFactory.RectRotated("RaiderBadgeB", transform, new Vector2(0.24f, 0.06f), c, badgePos + new Vector3(0.07f, 0f, 0f), -28f, 19);
                    break;
                case EnemyBattleRole.Suppressor:
                    VisualFactory.Disc("SuppressorBadge", transform, new Vector2(0.18f, 0.18f), c, badgePos, 19);
                    break;
                case EnemyBattleRole.Engineer:
                    VisualFactory.Rect("EngineerBadgeH", transform, new Vector2(0.28f, 0.07f), c, badgePos, 19);
                    VisualFactory.Rect("EngineerBadgeV", transform, new Vector2(0.07f, 0.28f), c, badgePos, 19);
                    break;
                default:
                    VisualFactory.Disc("VanguardBadge", transform, new Vector2(0.12f, 0.12f), c, badgePos, 19);
                    break;
            }
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || _health == null || _health.IsDead) return;

            if (_faction == EnemyFaction.BlackGuard && Time.time >= _nextPulse)
            {
                _nextPulse = Time.time + 7.5f;
                _health.InvulnerableUntil = Mathf.Max(_health.InvulnerableUntil, Time.time + 0.45f);
                Color c = EnemyFactionDirector.FactionColor(_faction);
                VisualFactory.RingPulse(transform.position, new Color(c.r, c.g, c.b, 0.70f), 0.72f);
            }

            if (Time.time < _nextAction) return;
            ExecuteRoleAction();
            _nextAction = Time.time + ActionCooldown();
        }

        private float ActionCooldown()
        {
            float baseCooldown;
            switch (_role)
            {
                case EnemyBattleRole.Breacher: baseCooldown = 7.0f; break;
                case EnemyBattleRole.Raider: baseCooldown = 5.6f; break;
                case EnemyBattleRole.Suppressor: baseCooldown = 7.4f; break;
                case EnemyBattleRole.Engineer: baseCooldown = 6.0f; break;
                default: baseCooldown = 8.0f; break;
            }
            if (_faction == EnemyFaction.OverdriveHost) baseCooldown *= 0.72f;
            return Random.Range(baseCooldown * 0.86f, baseCooldown * 1.14f);
        }

        private void ExecuteRoleAction()
        {
            switch (_role)
            {
                case EnemyBattleRole.Breacher:
                    BreachShot();
                    break;
                case EnemyBattleRole.Raider:
                    RaiderBurst();
                    break;
                case EnemyBattleRole.Suppressor:
                    SuppressionVolley();
                    break;
                case EnemyBattleRole.Engineer:
                    RepairFormation();
                    break;
                default:
                    VanguardPressure();
                    break;
            }
        }

        private void BreachShot()
        {
            Vector2 target = _game.BasePosition;
            Vector2 origin = transform.position;
            Vector2 delta = target - origin;
            if (delta.sqrMagnitude > 95f) return;

            Color c = FactionShellColor();
            VisualFactory.RingPulse(transform.position, c, 0.62f);
            int damage = _round >= 70 ? 3 : 2;
            float speed = 8.8f + _round * 0.012f;
            AmmoType ammo = _faction == EnemyFaction.ScorchBrigade ? AmmoType.Incendiary : AmmoType.Basic;
            _game.SpawnProjectile(origin + delta.normalized * 0.72f, delta.normalized, Team.Enemy, damage, speed, c, ammo);
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.18f, 0.07f);
        }

        private void RaiderBurst()
        {
            Vector2 target = _game.PlayerPosition;
            Vector2 origin = transform.position;
            Vector2 direction = (target - origin).normalized;
            if (direction.sqrMagnitude < 0.01f) return;

            Color c = FactionShellColor();
            Vector2 side = new Vector2(-direction.y, direction.x);
            AmmoType ammo = _faction == EnemyFaction.StormCorps ? AmmoType.EMP : AmmoType.Basic;
            int shots = _faction == EnemyFaction.OverdriveHost ? 3 : 2;
            for (int i = 0; i < shots; i++)
            {
                float spread = (i - (shots - 1) * 0.5f) * 0.12f;
                Vector2 dir = (direction + side * spread).normalized;
                _game.SpawnProjectile(origin + dir * 0.65f, dir, Team.Enemy, 1, 10.2f, c, ammo);
            }
            VisualFactory.MuzzleFlash(origin + direction * 0.65f, c, 0.72f);
        }

        private void SuppressionVolley()
        {
            Vector2 target = _round >= 55 && Random.value < 0.45f ? _game.BasePosition : _game.PlayerPosition;
            Vector2 origin = transform.position;
            Vector2 direction = (target - origin).normalized;
            if (direction.sqrMagnitude < 0.01f) return;

            Color c = FactionShellColor();
            Vector2 side = new Vector2(-direction.y, direction.x);
            for (int i = -1; i <= 1; i++)
            {
                Vector2 dir = (direction + side * (i * 0.16f)).normalized;
                _game.SpawnProjectile(origin + dir * 0.64f, dir, Team.Enemy, 1, 9.6f + _round * 0.01f, c, AmmoType.Basic);
            }
            VisualFactory.MuzzleFlash(origin + direction * 0.64f, c, 0.80f);
        }

        private void RepairFormation()
        {
            Health[] healthUnits = FindObjectsByType<Health>(FindObjectsSortMode.None);
            int repaired = 0;
            for (int i = 0; i < healthUnits.Length && repaired < 3; i++)
            {
                Health h = healthUnits[i];
                if (h == null || h == _health || h.IsDead || h.Team != Team.Enemy) continue;
                if (Vector2.Distance(transform.position, h.transform.position) > 3.0f) continue;
                if (h.Current >= h.Maximum) continue;
                h.Heal(1);
                repaired++;
                VisualFactory.RingPulse(h.transform.position, new Color(0.20f, 1f, 0.46f, 0.65f), 0.50f);
            }

            if (repaired == 0)
                VanguardPressure();
        }

        private void VanguardPressure()
        {
            Vector2 target = Vector2.Distance(transform.position, _game.BasePosition) < 6.2f ? _game.BasePosition : _game.PlayerPosition;
            Vector2 origin = transform.position;
            Vector2 direction = (target - origin).normalized;
            if (direction.sqrMagnitude < 0.01f) return;
            Color c = FactionShellColor();
            _game.SpawnProjectile(origin + direction * 0.62f, direction, Team.Enemy, 1, 8.8f, c, AmmoType.Basic);
        }

        private Color FactionShellColor()
        {
            Color c = EnemyFactionDirector.FactionColor(_faction);
            return Color.Lerp(c, Color.white, 0.12f);
        }
    }
}
