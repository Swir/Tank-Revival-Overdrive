using System.Collections;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Adds health-driven boss phases on top of the existing BossWeaponController.
    /// Every phase changes cadence and unlocks a distinct telegraphed attack pattern.
    /// </summary>
    public sealed class MultiPhaseBossController : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _boss;
        private Health _health;
        private int _round;
        private int _phase = 1;
        private float _nextSpecial;
        private bool _transitioning;

        public void Initialize(TankGame game, EnemyTank boss, int round)
        {
            _game = game;
            _boss = boss;
            _health = boss.Health;
            _round = round;
            _nextSpecial = Time.time + 3.2f;
        }

        private void Update()
        {
            if (_game == null || _boss == null || _health == null || !_game.IsPlaying || _health.IsDead) return;

            float ratio = _health.Maximum > 0 ? _health.Current / (float)_health.Maximum : 0f;
            int targetPhase = ratio > 0.72f ? 1 : ratio > 0.44f ? 2 : ratio > 0.20f ? 3 : 4;
            if (targetPhase > _phase && !_transitioning)
            {
                _phase = targetPhase;
                StartCoroutine(PhaseTransition());
            }

            if (!_transitioning && Time.time >= _nextSpecial)
            {
                float cooldown = Mathf.Lerp(7.0f, 3.7f, Mathf.InverseLerp(10f, 100f, _round));
                cooldown *= Mathf.Lerp(1f, 0.68f, (_phase - 1) / 3f);
                _nextSpecial = Time.time + cooldown;
                StartCoroutine(FireSpecialPattern());
            }
        }

        private IEnumerator PhaseTransition()
        {
            _transitioning = true;
            Color phaseColor = _phase == 2 ? new Color(1f, 0.55f, 0.08f) : _phase == 3 ? new Color(1f, 0.18f, 0.06f) : new Color(0.95f, 0.04f, 0.18f);
            VisualFactory.RingPulse(transform.position, phaseColor, 2.2f + _phase * 0.25f);
            VisualFactory.RingPulse(transform.position, Color.white, 1.25f + _phase * 0.18f);
            _game.KickCamera(0.28f, 0.16f + _phase * 0.025f);
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.55f, 0f);

            // Short readable transition window instead of an instant surprise attack.
            _health.InvulnerableUntil = Mathf.Max(_health.InvulnerableUntil, Time.time + 0.65f);
            yield return new WaitForSeconds(0.75f);
            _transitioning = false;
            _nextSpecial = Time.time + 0.65f;
        }

        private IEnumerator FireSpecialPattern()
        {
            if (_phase == 1)
            {
                yield return StartCoroutine(TargetedFan());
                yield break;
            }

            if (_phase == 2)
            {
                yield return StartCoroutine(RadialBurst(10, 8.8f));
                yield break;
            }

            if (_phase == 3)
            {
                yield return StartCoroutine(CrossfireSequence());
                yield break;
            }

            yield return StartCoroutine(FinalDesperation());
        }

        private IEnumerator TargetedFan()
        {
            Vector2 target = _game.PlayerPosition;
            Vector2 dir = (target - (Vector2)transform.position).normalized;
            Vector2 side = new Vector2(-dir.y, dir.x);
            Vector2 muzzle = (Vector2)transform.position + dir * 1.05f;

            VisualFactory.RingPulse(target, new Color(1f, 0.28f, 0.05f), 0.85f);
            yield return new WaitForSeconds(0.42f);

            for (int i = -2; i <= 2; i++)
            {
                Vector2 shot = (dir + side * (i * 0.12f)).normalized;
                _game.SpawnProjectile(muzzle, shot, Team.Enemy, 2, 10.6f, new Color(1f, 0.30f, 0.06f), AmmoType.Basic);
            }
            VisualFactory.MuzzleFlash(muzzle, new Color(1f, 0.36f, 0.08f), 1.25f);
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.34f, 0.03f);
        }

        private IEnumerator RadialBurst(int count, float speed)
        {
            VisualFactory.RingPulse(transform.position, new Color(1f, 0.48f, 0.06f), 1.55f);
            yield return new WaitForSeconds(0.48f);

            float offset = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float a = (offset + i * (360f / count)) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                _game.SpawnProjectile((Vector2)transform.position + dir * 1.0f, dir, Team.Enemy, 1 + _round / 80, speed, new Color(1f, 0.18f, 0.04f), AmmoType.Basic);
            }
            _game.KickCamera(0.14f, 0.09f);
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.38f, 0.02f);
        }

        private IEnumerator CrossfireSequence()
        {
            for (int wave = 0; wave < 3; wave++)
            {
                Vector2 target = wave % 2 == 0 ? _game.PlayerPosition : _game.BasePosition + Vector2.up * 1.0f;
                Vector2 dir = (target - (Vector2)transform.position).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x);
                VisualFactory.RingPulse(target, new Color(1f, 0.10f, 0.04f), 0.72f);
                yield return new WaitForSeconds(0.28f);

                for (int i = -1; i <= 1; i++)
                {
                    Vector2 shot = (dir + side * (i * 0.18f)).normalized;
                    _game.SpawnProjectile((Vector2)transform.position + shot, shot, Team.Enemy, 2, 11.0f, new Color(1f, 0.12f, 0.03f), AmmoType.Basic);
                }
                yield return new WaitForSeconds(0.24f);
            }
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.42f, 0.03f);
        }

        private IEnumerator FinalDesperation()
        {
            VisualFactory.RingPulse(transform.position, new Color(1f, 0.02f, 0.12f), 2.7f);
            _game.KickCamera(0.22f, 0.17f);
            yield return new WaitForSeconds(0.40f);

            yield return StartCoroutine(RadialBurst(_round >= 80 ? 18 : 14, 10.2f));
            yield return new WaitForSeconds(0.20f);
            yield return StartCoroutine(TargetedFan());
            if (_round >= 60)
            {
                yield return new WaitForSeconds(0.18f);
                yield return StartCoroutine(RadialBurst(12, 11.4f));
            }
        }
    }
}
