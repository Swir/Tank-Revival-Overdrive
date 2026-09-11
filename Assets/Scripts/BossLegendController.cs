using System.Collections;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v1.1 Boss Legends layer. Every tenth-round boss receives a unique identity,
    /// readable signature attacks, campaign-scaled armor pressure and a guaranteed
    /// legendary reward. It intentionally layers on top of MultiPhaseBossController
    /// instead of replacing the proven boss core.
    /// </summary>
    public sealed class BossLegendController : MonoBehaviour
    {
        private enum LegendPattern
        {
            IronWolf,
            StormViper,
            SiegeKing,
            BlackComet,
            RedBaron,
            FrostMammoth,
            ThunderPriest,
            NightReaper,
            BurningTitan,
            OverdrivePrime
        }

        private static readonly string[] LegendNames =
        {
            "IRON WOLF",
            "STORM VIPER",
            "SIEGE KING",
            "BLACK COMET",
            "RED BARON",
            "FROST MAMMOTH",
            "THUNDER PRIEST",
            "NIGHT REAPER",
            "BURNING TITAN",
            "OVERDRIVE PRIME"
        };

        private TankGame _game;
        private EnemyTank _boss;
        private Health _health;
        private LegendPattern _pattern;
        private int _round;
        private int _legendIndex;
        private int _phase = 1;
        private float _nextSignature;
        private float _nextAura;
        private bool _casting;
        private bool _rewardGranted;

        private GUIStyle _title;
        private GUIStyle _phaseStyle;
        private GUIStyle _small;

        public string LegendName => LegendNames[Mathf.Clamp(_legendIndex, 0, LegendNames.Length - 1)];

        public void Initialize(TankGame game, EnemyTank boss, int round)
        {
            _game = game;
            _boss = boss;
            _health = boss != null ? boss.Health : null;
            _round = Mathf.Clamp(round, 10, 100);
            _legendIndex = Mathf.Clamp((_round / 10) - 1, 0, 9);
            _pattern = (LegendPattern)_legendIndex;
            _nextSignature = Time.time + 4.5f;
            _nextAura = Time.time + 0.2f;

            if (_health != null)
            {
                int bonus = 2 + _legendIndex * 2;
                _health.SetMaximum(_health.Maximum + bonus, true);
                _health.Died += OnLegendDefeated;
            }

            transform.localScale *= 1f + _legendIndex * 0.012f;
            VisualFactory.RingPulse(transform.position, LegendColor(), 2.2f);
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.80f, 0f);
        }

        private void Update()
        {
            if (_game == null || _boss == null || _health == null || _health.IsDead || !_game.IsPlaying) return;

            float ratio = _health.Maximum > 0 ? _health.Current / (float)_health.Maximum : 0f;
            _phase = ratio > 0.72f ? 1 : ratio > 0.44f ? 2 : ratio > 0.20f ? 3 : 4;

            if (Time.time >= _nextAura)
            {
                _nextAura = Time.time + Mathf.Lerp(1.6f, 0.75f, (_phase - 1) / 3f);
                VisualFactory.RingPulse(transform.position, LegendColor(), 0.85f + _phase * 0.10f);
            }

            if (!_casting && Time.time >= _nextSignature)
            {
                float baseCooldown = Mathf.Lerp(8.8f, 5.4f, _legendIndex / 9f);
                float phaseMultiplier = Mathf.Lerp(1f, 0.66f, (_phase - 1) / 3f);
                _nextSignature = Time.time + baseCooldown * phaseMultiplier;
                StartCoroutine(SignatureAttack());
            }
        }

        private IEnumerator SignatureAttack()
        {
            _casting = true;

            switch (_pattern)
            {
                case LegendPattern.IronWolf:
                    yield return StartCoroutine(IronWolfCharge());
                    break;
                case LegendPattern.StormViper:
                    yield return StartCoroutine(StormViperFan());
                    break;
                case LegendPattern.SiegeKing:
                    yield return StartCoroutine(SiegeKingBombardment());
                    break;
                case LegendPattern.BlackComet:
                    yield return StartCoroutine(BlackCometCross());
                    break;
                case LegendPattern.RedBaron:
                    yield return StartCoroutine(RedBaronSalvo());
                    break;
                case LegendPattern.FrostMammoth:
                    yield return StartCoroutine(FrostMammothRing());
                    break;
                case LegendPattern.ThunderPriest:
                    yield return StartCoroutine(ThunderPriestLances());
                    break;
                case LegendPattern.NightReaper:
                    yield return StartCoroutine(NightReaperSweep());
                    break;
                case LegendPattern.BurningTitan:
                    yield return StartCoroutine(BurningTitanInferno());
                    break;
                default:
                    yield return StartCoroutine(OverdrivePrimeSequence());
                    break;
            }

            _casting = false;
        }

        private IEnumerator IronWolfCharge()
        {
            Vector2 target = _game.PlayerPosition;
            Warn(target, 1.15f);
            yield return new WaitForSeconds(0.55f);
            yield return StartCoroutine(FanAt(target, 5 + _phase, 0.11f, 10.2f, Damage(1)));
            if (_phase >= 3)
                yield return StartCoroutine(Radial(8 + _phase * 2, 8.4f, 1));
        }

        private IEnumerator StormViperFan()
        {
            for (int wave = 0; wave < 2 + (_phase >= 3 ? 1 : 0); wave++)
            {
                Vector2 target = _game.PlayerPosition + Random.insideUnitCircle * 0.7f;
                Warn(target, 0.90f);
                yield return new WaitForSeconds(0.26f);
                yield return StartCoroutine(FanAt(target, 3 + _phase, 0.15f, 12.0f, Damage(1)));
                yield return new WaitForSeconds(0.18f);
            }
        }

        private IEnumerator SiegeKingBombardment()
        {
            int impacts = 3 + _phase;
            for (int i = 0; i < impacts; i++)
            {
                Vector2 focus = i % 2 == 0 ? _game.BasePosition + Vector2.up * 1.0f : _game.PlayerPosition;
                Vector2 pos = focus + Random.insideUnitCircle * 1.8f;
                StartCoroutine(DelayedBlast(pos, 0.65f + i * 0.12f, 1.15f, Damage(1)));
            }
            yield return new WaitForSeconds(1.25f);
        }

        private IEnumerator BlackCometCross()
        {
            Warn(transform.position, 1.6f);
            yield return new WaitForSeconds(0.46f);
            int rays = 8 + _phase * 2;
            yield return StartCoroutine(Radial(rays, 11.8f, Damage(1)));
            if (_phase >= 3)
            {
                yield return new WaitForSeconds(0.22f);
                yield return StartCoroutine(Radial(rays, 9.6f, 1, 360f / rays * 0.5f));
            }
        }

        private IEnumerator RedBaronSalvo()
        {
            Vector2 player = _game.PlayerPosition;
            for (int wave = 0; wave < 3; wave++)
            {
                Vector2 target = player + new Vector2((wave - 1) * 1.4f, wave % 2 == 0 ? 0.5f : -0.5f);
                Warn(target, 0.75f);
                yield return new WaitForSeconds(0.22f);
                yield return StartCoroutine(FanAt(target, 4 + _phase, 0.12f, 12.6f, Damage(1)));
            }
        }

        private IEnumerator FrostMammothRing()
        {
            _health.InvulnerableUntil = Mathf.Max(_health.InvulnerableUntil, Time.time + 0.75f);
            VisualFactory.RingPulse(transform.position, new Color(0.40f, 0.86f, 1f), 2.6f);
            yield return new WaitForSeconds(0.62f);
            yield return StartCoroutine(Radial(12 + _phase * 2, 7.6f, Damage(1)));
            if (_phase == 4)
            {
                yield return new WaitForSeconds(0.25f);
                yield return StartCoroutine(FanAt(_game.PlayerPosition, 9, 0.10f, 10.4f, 2));
            }
        }

        private IEnumerator ThunderPriestLances()
        {
            int strikes = 2 + _phase;
            for (int i = 0; i < strikes; i++)
            {
                Vector2 pos = _game.PlayerPosition + Random.insideUnitCircle * 1.3f;
                StartCoroutine(DelayedBlast(pos, 0.52f + i * 0.10f, 0.92f, Damage(1)));
            }
            yield return new WaitForSeconds(0.90f);
            yield return StartCoroutine(FanAt(_game.PlayerPosition, 3 + _phase, 0.18f, 13.2f, Damage(1)));
        }

        private IEnumerator NightReaperSweep()
        {
            Vector2 target = _game.PlayerPosition;
            Vector2 dir = (target - (Vector2)transform.position).normalized;
            Vector2 side = new Vector2(-dir.y, dir.x);
            for (int i = -3 - _phase; i <= 3 + _phase; i++)
            {
                Vector2 shot = (dir + side * i * 0.085f).normalized;
                SpawnShell(shot, 12.8f, Damage(1), new Color(0.76f, 0.18f, 1f));
                yield return new WaitForSeconds(0.045f);
            }
            _game.KickCamera(0.16f, 0.10f);
        }

        private IEnumerator BurningTitanInferno()
        {
            Warn(transform.position, 2.5f);
            yield return new WaitForSeconds(0.52f);
            yield return StartCoroutine(Radial(14 + _phase * 2, 9.8f, Damage(1)));
            for (int i = 0; i < 2 + _phase; i++)
            {
                Vector2 pos = _game.PlayerPosition + Random.insideUnitCircle * 2.0f;
                StartCoroutine(DelayedBlast(pos, 0.35f + i * 0.12f, 1.05f, 2));
            }
            yield return new WaitForSeconds(0.85f);
        }

        private IEnumerator OverdrivePrimeSequence()
        {
            _health.InvulnerableUntil = Mathf.Max(_health.InvulnerableUntil, Time.time + 0.65f);
            VisualFactory.RingPulse(transform.position, new Color(1f, 0.12f, 0.42f), 3.0f);
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.75f, 0f);
            yield return new WaitForSeconds(0.55f);
            yield return StartCoroutine(Radial(16 + _phase * 2, 10.8f, Damage(1)));
            yield return new WaitForSeconds(0.18f);
            yield return StartCoroutine(FanAt(_game.PlayerPosition, 7 + _phase, 0.095f, 13.5f, Damage(1)));
            if (_phase >= 3)
            {
                for (int i = 0; i < 3 + _phase; i++)
                {
                    Vector2 pos = _game.PlayerPosition + Random.insideUnitCircle * 2.3f;
                    StartCoroutine(DelayedBlast(pos, 0.38f + i * 0.09f, 1.0f, 2));
                }
                yield return new WaitForSeconds(0.92f);
            }
        }

        private IEnumerator FanAt(Vector2 target, int count, float spread, float speed, int damage)
        {
            Vector2 dir = (target - (Vector2)transform.position).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float center = (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Vector2 shot = (dir + side * ((i - center) * spread)).normalized;
                SpawnShell(shot, speed, damage, LegendColor());
            }
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.42f, 0.02f);
            yield return null;
        }

        private IEnumerator Radial(int count, float speed, int damage, float angleOffset = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (angleOffset + i * (360f / count)) * Mathf.Deg2Rad;
                SpawnShell(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), speed, damage, LegendColor());
            }
            _game.KickCamera(0.14f, 0.11f);
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.46f, 0.02f);
            yield return null;
        }

        private IEnumerator DelayedBlast(Vector2 position, float delay, float radius, int damage)
        {
            Warn(position, radius);
            yield return new WaitForSeconds(delay);
            if (_game == null || !_game.IsPlaying) yield break;

            Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius);
            foreach (Collider2D hit in hits)
            {
                if (hit == null) continue;
                Health hp = hit.GetComponent<Health>();
                if (hp == null || hp.IsDead || hp.Team == Team.Enemy) continue;
                hp.Damage(damage, Team.Enemy);
            }

            VisualFactory.Explosion(position, LegendColor(), radius * 1.35f);
            _game.KickCamera(0.14f, 0.10f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.34f, 0.04f);
        }

        private void SpawnShell(Vector2 direction, float speed, int damage, Color color)
        {
            Vector2 dir = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.down;
            Vector2 muzzle = (Vector2)transform.position + dir * 1.15f;
            _game.SpawnProjectile(muzzle, dir, Team.Enemy, damage, speed, color, AmmoType.Basic);
            VisualFactory.MuzzleFlash(muzzle, color, 0.95f);
        }

        private void Warn(Vector2 position, float size)
        {
            VisualFactory.RingPulse(position, LegendColor(), size);
        }

        private int Damage(int baseDamage)
        {
            return baseDamage + (_round >= 70 ? 1 : 0) + (_phase == 4 && _round >= 90 ? 1 : 0);
        }

        private Color LegendColor()
        {
            Color[] colors =
            {
                new Color(1f, 0.48f, 0.08f), new Color(0.30f, 0.92f, 1f), new Color(0.88f, 0.32f, 0.08f),
                new Color(0.54f, 0.22f, 1f), new Color(1f, 0.12f, 0.20f), new Color(0.48f, 0.88f, 1f),
                new Color(0.92f, 0.86f, 0.22f), new Color(0.72f, 0.18f, 0.92f), new Color(1f, 0.22f, 0.04f),
                new Color(1f, 0.08f, 0.42f)
            };
            return colors[Mathf.Clamp(_legendIndex, 0, colors.Length - 1)];
        }

        private void OnLegendDefeated(Health _)
        {
            if (_rewardGranted) return;
            _rewardGranted = true;

            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player != null)
            {
                AmmoType ammo = _legendIndex >= 8 ? AmmoType.Plasma : _legendIndex >= 5 ? AmmoType.EMP : _legendIndex >= 2 ? AmmoType.Explosive : AmmoType.ArmorPiercing;
                player.AddAmmo(ammo, 5 + _legendIndex);
                if (_legendIndex == 4 || _legendIndex == 9)
                    player.ApplyPowerUp(PowerUpKind.PowerShot);
                else if (_legendIndex == 6)
                    player.ApplyPowerUp(PowerUpKind.RapidFire);
                else
                    player.ApplyPowerUp(PowerUpKind.Repair);
            }

            _game?.RepairEagle(_legendIndex >= 7 ? 2 : 1);
            PlayerPrefs.SetInt("TankRevival.LegendsDefeated", PlayerPrefs.GetInt("TankRevival.LegendsDefeated", 0) + 1);
            PlayerPrefs.SetInt("TankRevival.LastLegendRound", _round);
            PlayerPrefs.Save();

            VisualFactory.RingPulse(transform.position, Color.white, 3.0f);
            VisualFactory.RingPulse(transform.position, LegendColor(), 2.5f);
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _phaseStyle = new GUIStyle(_title) { fontSize = 12 };
            _small = new GUIStyle(_title) { fontSize = 10, fontStyle = FontStyle.Normal };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _health == null || _health.IsDead) return;
            EnsureStyles();

            float width = Mathf.Min(620f, Screen.width - 80f);
            float x = (Screen.width - width) * 0.5f;
            float ratio = _health.Maximum > 0 ? Mathf.Clamp01(_health.Current / (float)_health.Maximum) : 0f;

            GUI.color = new Color(0.03f, 0.01f, 0.02f, 0.92f);
            GUI.Box(new Rect(x, 16f, width, 62f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 10f, 19f, width - 20f, 23f), $"LEGEND // {LegendName}", _title);

            GUI.color = new Color(0.12f, 0.04f, 0.05f, 0.96f);
            GUI.Box(new Rect(x + 18f, 45f, width - 36f, 12f), string.Empty);
            GUI.color = LegendColor();
            GUI.Box(new Rect(x + 20f, 47f, (width - 40f) * ratio, 8f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 16f, 58f, width - 32f, 18f), $"PHASE {_phase}/4   ARMOR {_health.Current}/{_health.Maximum}   ROUND {_round:000}", _small);
        }
    }
}
