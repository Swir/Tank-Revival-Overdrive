using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Runtime tactical layer for v0.6+. It upgrades existing enemies without replacing
    /// the proven EnemyTank core, so later milestone systems remain composable.
    /// </summary>
    public sealed class TacticalWarDirector : MonoBehaviour
    {
        private TankGame _game;
        private float _nextScan;
        private float _nextAirstrike;
        private readonly HashSet<int> _knownEnemies = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDirector()
        {
            if (FindAnyObjectByType<TacticalWarDirector>() != null) return;
            var go = new GameObject("TacticalWarDirector");
            go.AddComponent<TacticalWarDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying) return;

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.55f;
                UpgradeEnemyTactics();
            }

            int round = _game.CurrentRound;
            if (round >= 30 && Time.time >= _nextAirstrike)
            {
                float interval = Mathf.Lerp(18f, 8.5f, Mathf.InverseLerp(30f, 100f, round));
                _nextAirstrike = Time.time + interval;
                StartCoroutine(LaunchTacticalStrike(round));
            }
        }

        private void UpgradeEnemyTactics()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            int round = _game.CurrentRound;

            foreach (EnemyTank enemy in enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                int id = enemy.GetInstanceID();
                if (_knownEnemies.Contains(id)) continue;
                _knownEnemies.Add(id);

                bool flanker = enemy.Kind == EnemyKind.Fast || enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Sniper;
                if (flanker && round >= 12 && enemy.GetComponent<FlankingCombatAgent>() == null)
                {
                    var agent = enemy.gameObject.AddComponent<FlankingCombatAgent>();
                    agent.Initialize(_game, enemy, round);
                }

                if (enemy.Kind == EnemyKind.Boss)
                {
                    if (enemy.GetComponent<MultiPhaseBossController>() == null)
                    {
                        var boss = enemy.gameObject.AddComponent<MultiPhaseBossController>();
                        boss.Initialize(_game, enemy, round);
                    }

                    if (enemy.GetComponent<BossLegendController>() == null)
                    {
                        var legend = enemy.gameObject.AddComponent<BossLegendController>();
                        legend.Initialize(_game, enemy, round);
                    }
                }

                if (round >= 55 && (enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Heavy) && enemy.GetComponent<FormationPressureAura>() == null)
                {
                    var aura = enemy.gameObject.AddComponent<FormationPressureAura>();
                    aura.Initialize(_game, enemy, round);
                }
            }
        }

        private IEnumerator LaunchTacticalStrike(int round)
        {
            Vector2 player = _game.PlayerPosition;
            Vector2 basePos = _game.BasePosition;
            float progress = Mathf.InverseLerp(30f, 100f, round);
            int markers = round >= 80 ? 4 : round >= 55 ? 3 : 2;

            for (int i = 0; i < markers; i++)
            {
                Vector2 focus = i % 3 == 2 ? basePos + Vector2.up * 1.3f : player;
                Vector2 offset = Random.insideUnitCircle * Mathf.Lerp(2.2f, 3.4f, progress);
                Vector2 pos = focus + offset;
                pos.x = Mathf.Clamp(pos.x, -10.8f, 10.8f);
                pos.y = Mathf.Clamp(pos.y, -5.2f, 5.5f);
                StartCoroutine(ResolveStrike(pos, round, 0.95f + i * 0.16f));
            }

            yield break;
        }

        private IEnumerator ResolveStrike(Vector2 position, int round, float delay)
        {
            Color warning = new Color(1f, 0.16f, 0.05f, 0.92f);
            VisualFactory.RingPulse(position, warning, 1.25f);
            VisualFactory.Disc("StrikeMarker", transform, new Vector2(0.20f, 0.20f), warning, position, 40);

            yield return new WaitForSeconds(delay);
            if (_game == null || !_game.IsPlaying) yield break;

            float radius = round >= 75 ? 1.45f : 1.20f;
            int damage = round >= 70 ? 2 : 1;
            Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius);
            foreach (Collider2D hit in hits)
            {
                if (hit == null) continue;
                Health hp = hit.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;

                // Tactical strikes can hurt both armies, but never directly delete the Eagle core.
                if (hit.gameObject.name.Contains("ORZELEK_DEFENSE_CORE")) continue;
                hp.Damage(damage, Team.Neutral);
            }

            VisualFactory.Explosion(position, new Color(1f, 0.22f, 0.04f), radius * 1.35f);
            VisualFactory.RingPulse(position, new Color(1f, 0.54f, 0.08f), radius * 1.55f);
            _game.KickCamera(0.16f, round >= 75 ? 0.14f : 0.10f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.36f, 0.04f);
        }
    }
}
