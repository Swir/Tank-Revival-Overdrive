using System.Collections;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Shared telegraphed hazard execution for Battlefield Evolution.
    /// Hazards can hit both mobile armies, but explicitly exclude the Orzelek core.
    /// </summary>
    public static class BattlefieldHazard
    {
        public enum Kind
        {
            HeavyShell,
            EmpStrike,
            FireBurst,
            FrostShock,
            Concussion
        }

        public static IEnumerator Telegraph(Vector2 point, float radius, float warningSeconds, Kind kind, Color accent, int round)
        {
            var root = new GameObject("BattlefieldHazard_" + kind);
            root.transform.position = point;

            GameObject outer = VisualFactory.RingObject("WarningOuter", root.transform, Vector2.one * radius * 2f, new Color(accent.r, accent.g, accent.b, 0.72f), Vector3.zero, 92);
            GameObject inner = VisualFactory.Disc("WarningFill", root.transform, Vector2.one * radius * 1.72f, new Color(accent.r, accent.g, accent.b, 0.09f), Vector3.zero, 91);
            GameObject crossA = VisualFactory.Rect("WarningCrossA", root.transform, new Vector2(radius * 1.38f, 0.035f), new Color(accent.r, accent.g, accent.b, 0.72f), Vector3.zero, 93);
            GameObject crossB = VisualFactory.Rect("WarningCrossB", root.transform, new Vector2(0.035f, radius * 1.38f), new Color(accent.r, accent.g, accent.b, 0.72f), Vector3.zero, 93);

            SpriteRenderer outerSr = outer.GetComponent<SpriteRenderer>();
            SpriteRenderer innerSr = inner.GetComponent<SpriteRenderer>();
            SpriteRenderer crossASr = crossA.GetComponent<SpriteRenderer>();
            SpriteRenderer crossBSr = crossB.GetComponent<SpriteRenderer>();

            float started = Time.time;
            while (Time.time - started < warningSeconds)
            {
                float u = Mathf.Clamp01((Time.time - started) / Mathf.Max(0.1f, warningSeconds));
                float pulse = 0.70f + Mathf.Sin(Time.time * (8f + u * 9f)) * 0.18f;
                root.transform.localScale = Vector3.one * Mathf.Lerp(0.84f, 1.04f, u);
                SetAlpha(outerSr, 0.45f + pulse * 0.30f);
                SetAlpha(innerSr, 0.05f + u * 0.13f);
                SetAlpha(crossASr, 0.38f + pulse * 0.35f);
                SetAlpha(crossBSr, 0.38f + pulse * 0.35f);
                yield return null;
            }

            Resolve(point, radius, kind, accent, round);
            Object.Destroy(root);
        }

        private static void Resolve(Vector2 point, float radius, Kind kind, Color accent, int round)
        {
            float fxScale = Mathf.Max(0.9f, radius * 0.92f);
            switch (kind)
            {
                case Kind.HeavyShell:
                    VisualFactory.Explosion(point, new Color(1f, 0.24f, 0.06f), fxScale * 1.25f);
                    VisualFactory.RingPulse(point, new Color(1f, 0.40f, 0.08f), radius * 0.80f);
                    BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.70f, 0.04f);
                    DamageArea(point, radius, round >= 70 ? 2 : 1, false, false, false);
                    break;

                case Kind.EmpStrike:
                    VisualFactory.RingPulse(point, new Color(0.26f, 0.82f, 1f), radius * 1.05f);
                    VisualFactory.RingPulse(point, new Color(0.48f, 0.38f, 1f), radius * 0.72f);
                    BattleAudio.PlayGlobal(SoundCue.Emp, 0.66f, 0.03f);
                    DamageArea(point, radius, 0, true, false, false);
                    break;

                case Kind.FireBurst:
                    VisualFactory.Explosion(point, new Color(1f, 0.18f, 0.03f), fxScale * 1.15f);
                    VisualFactory.RingPulse(point, new Color(1f, 0.36f, 0.04f), radius * 0.92f);
                    BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.68f, 0.05f);
                    DamageArea(point, radius, 1, false, true, false);
                    break;

                case Kind.FrostShock:
                    VisualFactory.RingPulse(point, new Color(0.72f, 0.94f, 1f), radius * 1.05f);
                    VisualFactory.RingPulse(point, new Color(0.34f, 0.72f, 1f), radius * 0.70f);
                    BattleAudio.PlayGlobal(SoundCue.Emp, 0.48f, 0.04f);
                    DamageArea(point, radius, 0, false, false, true);
                    break;

                default:
                    VisualFactory.Explosion(point, accent, fxScale * 0.78f);
                    VisualFactory.RingPulse(point, accent, radius * 0.86f);
                    BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.46f, 0.08f);
                    DamageArea(point, radius, 1, false, false, false);
                    break;
            }
        }

        private static void DamageArea(Vector2 point, float radius, int damage, bool emp, bool burn, bool frost)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(point, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;
                Health health = hit.GetComponent<Health>();
                if (health == null || health.IsDead || IsOrzelekCore(health)) continue;

                if (damage > 0)
                    health.Damage(damage, Team.Neutral);

                if (health.IsDead) continue;
                if (emp || burn || frost)
                {
                    CombatStatus status = health.GetComponent<CombatStatus>();
                    if (status == null) status = health.gameObject.AddComponent<CombatStatus>();
                    if (emp)
                        status.ApplyEmp(2.3f);
                    else if (burn)
                        status.ApplyBurn(Team.Neutral, 3.0f, 1, 0.85f);
                    else if (frost)
                        status.ApplyEmp(1.15f);
                }
            }
        }

        private static bool IsOrzelekCore(Health health)
        {
            if (health == null) return false;
            string objectName = health.gameObject.name.ToUpperInvariant();
            return objectName.Contains("ORZELEK") || objectName.Contains("EAGLE_CORE") || objectName.Contains("DEFENSE_CORE");
        }

        private static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Color c = renderer.color;
            c.a = Mathf.Clamp01(alpha);
            renderer.color = c;
        }
    }
}
