using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Gives late-game heavy units a coordinated support-fire role. Nearby allies
    /// periodically receive a synchronized extra shot toward the current objective.
    /// </summary>
    public sealed class FormationPressureAura : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _leader;
        private float _nextVolley;
        private int _round;

        public void Initialize(TankGame game, EnemyTank leader, int round)
        {
            _game = game;
            _leader = leader;
            _round = round;
            _nextVolley = Time.time + Random.Range(3.0f, 5.0f);
        }

        private void Update()
        {
            if (_game == null || _leader == null || !_game.IsPlaying) return;
            if (_leader.Health == null || _leader.Health.IsDead) return;
            if (Time.time < _nextVolley) return;

            _nextVolley = Time.time + Mathf.Lerp(7.0f, 4.2f, Mathf.InverseLerp(55f, 100f, _round));
            FireFormationVolley();
        }

        private void FireFormationVolley()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            int fired = 0;
            int cap = _round >= 85 ? 4 : 3;
            Vector2 objective = _leader.Kind == EnemyKind.Siege ? _game.BasePosition : _game.PlayerPosition;

            foreach (EnemyTank ally in enemies)
            {
                if (ally == null || ally == _leader || ally.Health == null || ally.Health.IsDead) continue;
                if (((Vector2)ally.transform.position - (Vector2)transform.position).sqrMagnitude > 16f) continue;

                Vector2 dir = objective - (Vector2)ally.transform.position;
                if (dir.sqrMagnitude < 0.15f) continue;
                dir.Normalize();
                Vector2 muzzle = (Vector2)ally.transform.position + dir * 0.72f;
                _game.SpawnProjectile(muzzle, dir, Team.Enemy, 1, 9.4f + _round * 0.015f, new Color(1f, 0.28f, 0.06f), AmmoType.Basic);
                VisualFactory.MuzzleFlash(muzzle, new Color(1f, 0.30f, 0.08f), 0.65f);
                fired++;
                if (fired >= cap) break;
            }

            if (fired > 0)
                BattleAudio.PlayGlobal(SoundCue.EnemyShot, 0.18f, 0.04f);
        }
    }
}
