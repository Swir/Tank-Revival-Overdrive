using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Adds lateral movement and cover-aware repositioning to mobile enemy classes.
    /// Runs after EnemyTank movement so it layers tactical steering on top of the base AI.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class FlankingCombatAgent : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _enemy;
        private Rigidbody2D _body;
        private float _nextPlan;
        private float _flankSign;
        private float _strength;
        private float _evadeUntil;

        public void Initialize(TankGame game, EnemyTank enemy, int round)
        {
            _game = game;
            _enemy = enemy;
            _body = GetComponent<Rigidbody2D>();
            _flankSign = Random.value < 0.5f ? -1f : 1f;
            _strength = Mathf.Lerp(0.38f, 0.82f, Mathf.InverseLerp(12f, 100f, round));
            if (enemy.Kind == EnemyKind.Elite) _strength *= 1.20f;
            if (enemy.Kind == EnemyKind.Sniper) _strength *= 0.78f;
            _nextPlan = Time.time + Random.Range(0.4f, 1.0f);
        }

        private void Update()
        {
            if (_game == null || _enemy == null || !_game.IsPlaying) return;
            if (_enemy.Health == null || _enemy.Health.IsDead) return;

            if (Time.time >= _nextPlan)
            {
                _nextPlan = Time.time + Random.Range(1.15f, 2.35f);
                if (Random.value < 0.58f) _flankSign = -_flankSign;

                Vector2 toPlayer = _game.PlayerPosition - (Vector2)transform.position;
                float distance = toPlayer.magnitude;
                if (distance < 5.2f || HasClearShot(toPlayer, distance))
                    _evadeUntil = Time.time + Random.Range(0.65f, 1.25f);
            }
        }

        private void FixedUpdate()
        {
            if (_game == null || _enemy == null || _body == null || !_game.IsPlaying) return;
            if (_enemy.Health == null || _enemy.Health.IsDead) return;

            Vector2 target = _enemy.Kind == EnemyKind.Sniper ? _game.PlayerPosition :
                Vector2.Lerp(_game.PlayerPosition, _game.BasePosition, _enemy.Kind == EnemyKind.Elite ? 0.20f : 0.08f);
            Vector2 toward = target - _body.position;
            if (toward.sqrMagnitude < 0.25f) return;

            toward.Normalize();
            Vector2 lateral = new Vector2(-toward.y, toward.x) * _flankSign;
            float dangerBoost = Time.time < _evadeUntil ? 1.55f : 1f;
            float classBoost = _enemy.Kind == EnemyKind.Fast ? 1.18f : _enemy.Kind == EnemyKind.Elite ? 1.08f : 0.82f;
            Vector2 nudge = lateral * (_strength * classBoost * dangerBoost * Time.fixedDeltaTime);

            // Avoid pushing the agent directly into solid cover.
            RaycastHit2D wall = Physics2D.Raycast(_body.position, lateral, 0.72f);
            if (wall.collider != null && wall.collider.gameObject != gameObject)
            {
                _flankSign = -_flankSign;
                lateral = -lateral;
                nudge = lateral * (_strength * 0.55f * Time.fixedDeltaTime);
            }

            _body.MovePosition(_body.position + nudge);
        }

        private bool HasClearShot(Vector2 direction, float distance)
        {
            if (distance <= 0.05f) return false;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction.normalized, distance);
            if (hit.collider == null) return true;
            return hit.collider.GetComponent<PlayerTank>() != null;
        }
    }
}
