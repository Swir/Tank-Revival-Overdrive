using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Bounded visual damage-state feedback for armored units. Damaged machines emit
    /// module-colored sparks/smoke at a controlled cadence so critical state is visible
    /// in combat without adding permanent particle systems to every tank.
    /// </summary>
    public sealed class ArmorDamageVisualDirector : MonoBehaviour
    {
        private readonly Dictionary<int, float> _nextFx = new Dictionary<int, float>();
        private float _nextScan;
        private ArmorSystem[] _armor = new ArmorSystem[0];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ArmorDamageVisualDirector>() != null) return;
            var go = new GameObject("ArmorDamageVisualDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<ArmorDamageVisualDirector>();
        }

        private void Update()
        {
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 1.0f;
                _armor = FindObjectsByType<ArmorSystem>(FindObjectsSortMode.None);
            }

            int budget = 6;
            for (int i = 0; i < _armor.Length && budget > 0; i++)
            {
                ArmorSystem armor = _armor[i];
                if (armor == null || armor.AverageIntegrity >= 78) continue;
                int id = armor.GetInstanceID();
                if (_nextFx.TryGetValue(id, out float next) && Time.time < next) continue;

                budget--;
                float severity = Mathf.Clamp01((78f - armor.AverageIntegrity) / 58f);
                _nextFx[id] = Time.time + Mathf.Lerp(1.45f, 0.42f, severity) + Random.Range(0f, 0.25f);
                EmitDamageState(armor, severity);
            }

            if (_nextFx.Count > 256)
                _nextFx.Clear();
        }

        private static void EmitDamageState(ArmorSystem armor, float severity)
        {
            Vector3 basePos = armor.transform.position;
            Vector2 jitter = Random.insideUnitCircle * 0.32f;
            Vector3 pos = basePos + new Vector3(jitter.x, jitter.y, 0f);

            Color color;
            if (armor.AmmoRackIntegrity <= 35)
                color = new Color(1f, 0.08f, 0.18f);
            else if (armor.EngineIntegrity <= 35)
                color = new Color(1f, 0.30f, 0.05f);
            else if (armor.GunIntegrity <= 35)
                color = new Color(1f, 0.62f, 0.10f);
            else if (armor.TrackIntegrity <= 35)
                color = new Color(0.92f, 0.76f, 0.28f);
            else
                color = new Color(0.55f, 0.62f, 0.68f);

            VisualFactory.MicroBurst(pos, color, Mathf.Lerp(0.28f, 0.64f, severity));
            if (severity > 0.62f && Random.value < 0.42f)
                VisualFactory.RingPulse(basePos, new Color(color.r, color.g, color.b, 0.52f), 0.34f + severity * 0.28f);
        }
    }
}
