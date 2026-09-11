using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Elite command layer attached to a real enemy tank. Commanders heal and shield
    /// nearby enemy armor, launch coordinated volleys and pay meaningful rewards on death.
    /// </summary>
    public sealed class WarCommander : MonoBehaviour
    {
        private TankGame _game;
        private EnemyFaction _faction;
        private int _round;
        private float _nextCommand;
        private float _nextVolley;
        private bool _rewardGranted;

        public Health Health { get; private set; }

        public void Initialize(TankGame game, EnemyFaction faction, int round)
        {
            _game = game;
            _faction = faction;
            _round = Mathf.Clamp(round, 1, 100);
            Health = GetComponent<Health>();

            if (Health != null)
            {
                int bonus = 4 + _round / 18;
                Health.SetMaximum(Health.Maximum + bonus, true);
                Health.Died += OnCommanderDied;
            }

            BuildCommandVisuals();
            _nextCommand = Time.time + 1.4f;
            _nextVolley = Time.time + 3.0f;
        }

        private void BuildCommandVisuals()
        {
            Color c = EnemyFactionDirector.FactionColor(_faction);
            VisualFactory.RingObject("CommanderOuterRing", transform, new Vector2(1.72f, 1.72f), new Color(c.r, c.g, c.b, 0.70f), Vector3.zero, 24);
            VisualFactory.RingObject("CommanderInnerRing", transform, new Vector2(1.38f, 1.38f), new Color(1f, 0.78f, 0.18f, 0.58f), Vector3.zero, 25);
            VisualFactory.RectRotated("CommanderChevronL", transform, new Vector2(0.34f, 0.08f), new Color(1f, 0.82f, 0.20f), new Vector3(-0.14f, -0.72f, 0f), -28f, 26);
            VisualFactory.RectRotated("CommanderChevronR", transform, new Vector2(0.34f, 0.08f), new Color(1f, 0.82f, 0.20f), new Vector3(0.14f, -0.72f, 0f), 28f, 26);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || Health == null || Health.IsDead) return;

            if (Time.time >= _nextCommand)
            {
                _nextCommand = Time.time + Mathf.Max(3.6f, 6.4f - _round * 0.018f);
                CommandPulse();
            }

            if (Time.time >= _nextVolley)
            {
                _nextVolley = Time.time + Mathf.Max(4.8f, 8.5f - _round * 0.025f);
                CommandVolley();
            }
        }

        private void CommandPulse()
        {
            Health[] units = FindObjectsByType<Health>(FindObjectsSortMode.None);
            int affected = 0;
            float radius = _round >= 70 ? 4.8f : 4.0f;

            for (int i = 0; i < units.Length; i++)
            {
                Health h = units[i];
                if (h == null || h == Health || h.IsDead || h.Team != Team.Enemy) continue;
                if (Vector2.Distance(transform.position, h.transform.position) > radius) continue;

                h.Heal(1);
                h.InvulnerableUntil = Mathf.Max(h.InvulnerableUntil, Time.time + 0.70f);
                affected++;

                if (affected <= 6)
                {
                    Color c = EnemyFactionDirector.FactionColor(_faction);
                    VisualFactory.RingPulse(h.transform.position, new Color(c.r, c.g, c.b, 0.62f), 0.46f);
                }
            }

            Color pulse = EnemyFactionDirector.FactionColor(_faction);
            VisualFactory.RingPulse(transform.position, new Color(pulse.r, pulse.g, pulse.b, 0.84f), 1.35f);
        }

        private void CommandVolley()
        {
            Vector2 origin = transform.position;
            bool eaglePriority = _round >= 45 || Random.value < 0.58f;
            Vector2 target = eaglePriority ? _game.BasePosition : _game.PlayerPosition;
            Vector2 direction = (target - origin).normalized;
            if (direction.sqrMagnitude < 0.01f) return;

            Color c = EnemyFactionDirector.FactionColor(_faction);
            Vector2 side = new Vector2(-direction.y, direction.x);
            int count = _round >= 80 ? 5 : _round >= 45 ? 4 : 3;
            int damage = _round >= 75 ? 2 : 1;

            for (int i = 0; i < count; i++)
            {
                float center = (count - 1) * 0.5f;
                float spread = (i - center) * 0.12f;
                Vector2 dir = (direction + side * spread).normalized;
                AmmoType ammo = CommanderAmmo();
                _game.SpawnProjectile(origin + dir * 0.82f, dir, Team.Enemy, damage, 9.4f + _round * 0.014f, c, ammo);
            }

            VisualFactory.MuzzleFlash(origin + direction * 0.82f, c, 1.15f);
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.28f, 0.05f);
        }

        private AmmoType CommanderAmmo()
        {
            switch (_faction)
            {
                case EnemyFaction.ScorchBrigade: return AmmoType.Incendiary;
                case EnemyFaction.StormCorps: return AmmoType.EMP;
                case EnemyFaction.OverdriveHost: return _round >= 90 ? AmmoType.Plasma : AmmoType.ArmorPiercing;
                default: return AmmoType.Basic;
            }
        }

        private void OnCommanderDied(Health dead)
        {
            if (_rewardGranted) return;
            _rewardGranted = true;

            int defeated = PlayerPrefs.GetInt("TankRevival.WarCommandersDefeated", 0) + 1;
            PlayerPrefs.SetInt("TankRevival.WarCommandersDefeated", defeated);
            PlayerPrefs.Save();

            _game?.RepairEagle(1);
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player != null)
            {
                AmmoType reward = _round >= 80 ? AmmoType.Plasma : _round >= 45 ? AmmoType.EMP : AmmoType.ArmorPiercing;
                player.AddAmmo(reward, _round >= 80 ? 3 : 2);
                if (player.Health != null)
                    player.Health.Heal(1);
            }

            Color c = EnemyFactionDirector.FactionColor(_faction);
            VisualFactory.Explosion(transform.position, c, 1.65f);
            VisualFactory.RingPulse(transform.position, new Color(0.30f, 1f, 0.55f, 0.85f), 1.55f);
        }
    }
}
