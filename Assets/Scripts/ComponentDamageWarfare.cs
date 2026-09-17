using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v12.9 deterministic ammunition-to-subsystem language. Health remains the only vehicle-life authority;
    /// this profile only decides which existing ArmorSystem module is stressed and by how much.
    /// </summary>
    public readonly struct ComponentDamageProfile
    {
        public readonly float Engine;
        public readonly float Tracks;
        public readonly float Gun;
        public readonly float AmmoRack;
        public readonly float BaseScale;

        public ComponentDamageProfile(float engine, float tracks, float gun, float ammoRack, float baseScale)
        {
            Engine = engine;
            Tracks = tracks;
            Gun = gun;
            AmmoRack = ammoRack;
            BaseScale = baseScale;
        }

        public float Weight(TankModule module)
        {
            switch (module)
            {
                case TankModule.Engine: return Engine;
                case TankModule.Tracks: return Tracks;
                case TankModule.Gun: return Gun;
                case TankModule.AmmoRack: return AmmoRack;
                default: return 0f;
            }
        }
    }

    public static class ComponentDamageWarfare
    {
        public const int MinModuleDamage = 10;
        public const int MaxModuleDamage = 62;

        public static ComponentDamageProfile Profile(AmmoType ammo)
        {
            switch (ammo)
            {
                case AmmoType.Twin: return new ComponentDamageProfile(0.88f, 1.08f, 0.88f, 0.78f, 0.92f);
                case AmmoType.ArmorPiercing: return new ComponentDamageProfile(1.12f, 0.88f, 1.18f, 1.34f, 1.18f);
                case AmmoType.Explosive: return new ComponentDamageProfile(1.04f, 1.48f, 1.02f, 0.72f, 1.08f);
                case AmmoType.Plasma: return new ComponentDamageProfile(1.26f, 0.94f, 1.34f, 1.26f, 1.30f);
                case AmmoType.EMP: return new ComponentDamageProfile(1.55f, 0.86f, 1.52f, 1.08f, 1.12f);
                case AmmoType.Incendiary: return new ComponentDamageProfile(1.42f, 0.94f, 0.88f, 1.38f, 1.04f);
                default: return new ComponentDamageProfile(1.00f, 1.00f, 1.00f, 1.00f, 1.00f);
            }
        }

        public static TankModule PreferredModule(AmmoType ammo, ArmorZone zone)
        {
            ComponentDamageProfile p = Profile(ammo);
            float engine = p.Engine * (zone == ArmorZone.Rear ? 1.42f : zone == ArmorZone.Side ? 1.08f : 0.86f);
            float tracks = p.Tracks * (zone == ArmorZone.Side ? 1.38f : zone == ArmorZone.Front ? 1.08f : 0.96f);
            float gun = p.Gun * (zone == ArmorZone.Front ? 1.30f : zone == ArmorZone.Side ? 1.05f : 0.88f);
            float rack = p.AmmoRack * (zone == ArmorZone.Rear ? 1.30f : zone == ArmorZone.Side ? 1.18f : 0.92f);

            TankModule best = TankModule.Engine;
            float score = engine;
            if (tracks > score) { score = tracks; best = TankModule.Tracks; }
            if (gun > score) { score = gun; best = TankModule.Gun; }
            if (rack > score) best = TankModule.AmmoRack;
            return best;
        }

        public static int ComputeDamage(int rawDamage, AmmoType ammo, ArmorZone zone, TankModule module, bool critical, bool overmatch)
        {
            ComponentDamageProfile p = Profile(ammo);
            float zoneScale = zone == ArmorZone.Rear ? 1.18f : zone == ArmorZone.Side ? 1.08f : 0.96f;
            float severity = (12f + Mathf.Max(1, rawDamage) * 7f) * p.BaseScale * p.Weight(module) * zoneScale;
            if (critical) severity += 8f;
            if (overmatch) severity += 9f;
            return Mathf.Clamp(Mathf.RoundToInt(severity), MinModuleDamage, MaxModuleDamage);
        }

        public static bool ConfigurationValid()
        {
            AmmoType[] ammo = { AmmoType.Basic, AmmoType.Twin, AmmoType.ArmorPiercing, AmmoType.Explosive, AmmoType.Plasma, AmmoType.EMP, AmmoType.Incendiary };
            for (int i = 0; i < ammo.Length; i++)
            {
                ComponentDamageProfile p = Profile(ammo[i]);
                if (p.BaseScale <= 0f || p.Engine <= 0f || p.Tracks <= 0f || p.Gun <= 0f || p.AmmoRack <= 0f) return false;
                foreach (ArmorZone zone in System.Enum.GetValues(typeof(ArmorZone)))
                {
                    TankModule module = PreferredModule(ammo[i], zone);
                    int damage = ComputeDamage(2, ammo[i], zone, module, true, true);
                    if (module == TankModule.None || damage < MinModuleDamage || damage > MaxModuleDamage) return false;
                }
            }
            return true;
        }
    }
}
