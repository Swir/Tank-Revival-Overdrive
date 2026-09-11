using System.Collections;
using UnityEngine;

namespace TankRevival
{
    public sealed class FinalOverdriveProtocol : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _boss;
        private Health _health;
        private bool _p75, _p50, _p25;
        private float _nextPressure;
        private GUIStyle _title, _body;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallWatcher()
        {
            if (FindAnyObjectByType<FinalBattleWatcher>() != null) return;
            var go = new GameObject("FinalBattleWatcher");
            DontDestroyOnLoad(go);
            go.AddComponent<FinalBattleWatcher>();
        }

        public void Initialize(TankGame game, EnemyTank boss)
        {
            _game = game;
            _boss = boss;
            _health = boss != null ? boss.Health : null;
            if (_health == null) return;
            int bonus = Mathf.Max(18, Mathf.RoundToInt(_health.Maximum * 0.45f));
            _health.SetMaximum(_health.Maximum + bonus, true);
            _nextPressure = Time.time + 5f;
            VisualFactory.RingPulse(transform.position, new Color(1f,0.08f,0.36f), 3.5f);
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 1f, 0f);
        }

        private void Update()
        {
            if (_game == null || _health == null || _health.IsDead || !_game.IsPlaying) return;
            float ratio = _health.Current / (float)Mathf.Max(1, _health.Maximum);

            if (!_p75 && ratio <= 0.75f) { _p75 = true; StartCoroutine(PhaseProtocol(1)); }
            if (!_p50 && ratio <= 0.50f) { _p50 = true; StartCoroutine(PhaseProtocol(2)); }
            if (!_p25 && ratio <= 0.25f) { _p25 = true; StartCoroutine(PhaseProtocol(3)); }

            if (Time.time >= _nextPressure)
            {
                _nextPressure = Time.time + (ratio <= 0.25f ? 3.2f : ratio <= 0.5f ? 4.2f : 5.3f);
                StartCoroutine(PressureSalvo(ratio <= 0.25f ? 7 : 4));
            }
        }

        private IEnumerator PhaseProtocol(int phase)
        {
            _health.InvulnerableUntil = Mathf.Max(_health.InvulnerableUntil, Time.time + 1.0f);
            Color c = phase == 1 ? new Color(1f,0.45f,0.08f) : phase == 2 ? new Color(0.72f,0.16f,1f) : new Color(1f,0.04f,0.18f);
            VisualFactory.RingPulse(transform.position, c, 3.0f + phase * 0.5f);
            _game.KickCamera(0.35f, 0.18f + phase * 0.04f);
            yield return new WaitForSeconds(0.7f);

            int rays = 10 + phase * 4;
            for (int i = 0; i < rays; i++)
            {
                float a = (360f / rays) * i + phase * 11f;
                Vector2 d = new Vector2(Mathf.Cos(a*Mathf.Deg2Rad), Mathf.Sin(a*Mathf.Deg2Rad));
                _game.SpawnProjectile((Vector2)transform.position + d*0.9f, d, Team.Enemy, phase >= 3 ? 2 : 1, 9.5f + phase, c, phase >= 2 ? AmmoType.ArmorPiercing : AmmoType.Basic);
            }

            if (phase >= 2) _game.RepairEagle(0); // explicit no free core damage/heal side effect
            yield return new WaitForSeconds(0.3f);
            StartCoroutine(TargetedBurst(_game.PlayerPosition, 3 + phase));
            StartCoroutine(TargetedBurst(_game.BasePosition + Vector2.up * 0.7f, 2 + phase));
        }

        private IEnumerator PressureSalvo(int shots)
        {
            Vector2 target = Random.value < 0.55f ? _game.BasePosition + Vector2.up*0.6f : _game.PlayerPosition;
            VisualFactory.RingPulse(target, new Color(1f,0.16f,0.08f), 1.4f);
            yield return new WaitForSeconds(0.48f);
            yield return TargetedBurst(target, shots);
        }

        private IEnumerator TargetedBurst(Vector2 target, int shots)
        {
            Vector2 origin = transform.position;
            Vector2 baseDir = (target-origin).normalized;
            for (int i=0;i<shots;i++)
            {
                float offset = (i-(shots-1)*0.5f)*0.065f;
                Vector2 side = new Vector2(-baseDir.y, baseDir.x);
                Vector2 dir = (baseDir + side*offset).normalized;
                _game.SpawnProjectile(origin + dir*1.05f, dir, Team.Enemy, 2, 11.8f, new Color(1f,0.12f,0.06f), AmmoType.ArmorPiercing);
                yield return new WaitForSeconds(0.07f);
            }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=22,fontStyle=FontStyle.Bold,normal={textColor=new Color(1f,0.18f,0.12f)}};
            _body = new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=12,normal={textColor=Color.white}};
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _health == null || _health.IsDead) return;
            EnsureStyles();
            GUI.color = new Color(0.09f,0.01f,0.02f,0.92f);
            GUI.Box(new Rect(Screen.width*0.5f-270f, 12f, 540f, 58f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width*0.5f-260f,17f,520f,27f), "FINAL PROTOCOL // OVERDRIVE PRIME", _title);
            GUI.Label(new Rect(Screen.width*0.5f-260f,43f,520f,20f), $"CORE ARMOR {_health.Current}/{_health.Maximum} // DESTROY PRIME AND SAVE ORZELEK", _body);
        }
    }

    public sealed class FinalBattleWatcher : MonoBehaviour
    {
        private TankGame _game;
        private float _scanAt;
        private int _attachedId;

        private void Update()
        {
            if (_game == null) { _game = FindAnyObjectByType<TankGame>(); return; }
            if (!_game.IsPlaying || _game.CurrentRound != 100) { _attachedId = 0; return; }
            if (Time.unscaledTime < _scanAt) return;
            _scanAt = Time.unscaledTime + 0.3f;
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            foreach (EnemyTank enemy in enemies)
            {
                if (enemy == null || enemy.Kind != EnemyKind.Boss || enemy.Health == null) continue;
                int id = enemy.GetInstanceID();
                if (_attachedId == id) return;
                _attachedId = id;
                var protocol = enemy.GetComponent<FinalOverdriveProtocol>();
                if (protocol == null) protocol = enemy.gameObject.AddComponent<FinalOverdriveProtocol>();
                protocol.Initialize(_game, enemy);
                return;
            }
        }
    }
}
