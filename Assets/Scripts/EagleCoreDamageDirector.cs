using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v1.6 visual damage-state layer for the destructible Orzelek core.
    /// Damage is readable directly on the battlefield: sparks, smoke and critical pulses scale with HP.
    /// </summary>
    public sealed class EagleCoreDamageDirector : MonoBehaviour
    {
        private TankGame _game;
        private Health _core;
        private float _nextFx;
        private int _lastHp = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EagleCoreDamageDirector>() != null) return;
            var go = new GameObject("EagleCoreDamageDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<EagleCoreDamageDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying)
            {
                _core = null;
                _lastHp = -1;
                return;
            }

            if (_core == null || _core.IsDead)
            {
                GameObject go = GameObject.Find("ORZELEK_DEFENSE_CORE");
                _core = go != null ? go.GetComponent<Health>() : null;
                _lastHp = _core != null ? _core.Current : -1;
                return;
            }

            if (_core.Current != _lastHp)
            {
                int previous = _lastHp;
                _lastHp = _core.Current;
                if (_core.Current < previous)
                    DamageBurst();
                else if (_core.Current > previous)
                    RepairBurst();
            }

            if (Time.time >= _nextFx)
                AmbientDamageFx();
        }

        private void DamageBurst()
        {
            Vector3 p = _core.transform.position;
            VisualFactory.RingPulse(p, new Color(1f, 0.12f, 0.04f), _core.Current <= 2 ? 1.8f : 1.1f);
            VisualFactory.MicroBurst(p + (Vector3)Random.insideUnitCircle * 0.35f, new Color(1f, 0.36f, 0.08f), 0.95f);
            BattleAudio.PlayGlobal(_core.Current <= 2 ? SoundCue.EagleAlarm : SoundCue.Ricochet, _core.Current <= 2 ? 0.72f : 0.30f, 0.05f);
        }

        private void RepairBurst()
        {
            Vector3 p = _core.transform.position;
            VisualFactory.RingPulse(p, new Color(0.26f, 1f, 0.52f), 1.2f);
            VisualFactory.MicroBurst(p, new Color(0.50f, 1f, 0.68f), 0.55f);
        }

        private void AmbientDamageFx()
        {
            if (_core == null || _core.IsDead) return;
            float health01 = _core.Maximum > 0 ? (float)_core.Current / _core.Maximum : 0f;
            if (health01 > 0.72f)
            {
                _nextFx = Time.time + 2.2f;
                return;
            }

            Vector3 p = _core.transform.position + (Vector3)(Random.insideUnitCircle * 0.42f);
            if (health01 <= 0.34f)
            {
                _nextFx = Time.time + Random.Range(0.18f, 0.38f);
                VisualFactory.ProjectileAfterglow(p, new Color(0.16f, 0.16f, 0.16f, 0.72f), 0.58f);
                VisualFactory.MicroBurst(p, new Color(1f, 0.24f, 0.04f), 0.42f);
                if (Random.value < 0.30f)
                    VisualFactory.RingPulse(_core.transform.position, new Color(1f, 0.08f, 0.03f, 0.35f), 0.65f);
            }
            else
            {
                _nextFx = Time.time + Random.Range(0.55f, 1.05f);
                VisualFactory.ProjectileAfterglow(p, new Color(0.20f, 0.20f, 0.20f, 0.55f), 0.38f);
                if (Random.value < 0.45f)
                    VisualFactory.MicroBurst(p, new Color(1f, 0.42f, 0.10f), 0.32f);
            }
        }
    }
}
