using UnityEngine;

namespace TankRevival
{
    public enum AmmoType
    {
        Basic = 0,
        ArmorPiercing = 1,
        Explosive = 2,
        Incendiary = 3,
        EMP = 4,
        Twin = 5,
        Plasma = 6
    }

    public static class AmmoDatabase
    {
        public const int AmmoTypeCount = 7;

        public static string DisplayName(AmmoType type)
        {
            return type switch
            {
                AmmoType.ArmorPiercing => "AP PIERCING",
                AmmoType.Explosive => "HE EXPLOSIVE",
                AmmoType.Incendiary => "INCENDIARY",
                AmmoType.EMP => "EMP SHOCK",
                AmmoType.Twin => "TWIN SHOT",
                AmmoType.Plasma => "PLASMA",
                _ => "STANDARD"
            };
        }

        public static string ShortName(AmmoType type)
        {
            return type switch
            {
                AmmoType.ArmorPiercing => "AP",
                AmmoType.Explosive => "HE",
                AmmoType.Incendiary => "FIRE",
                AmmoType.EMP => "EMP",
                AmmoType.Twin => "TWIN",
                AmmoType.Plasma => "PLASMA",
                _ => "STD"
            };
        }

        public static Color Color(AmmoType type)
        {
            return type switch
            {
                AmmoType.ArmorPiercing => new Color(0.84f, 0.90f, 1f),
                AmmoType.Explosive => new Color(1f, 0.34f, 0.08f),
                AmmoType.Incendiary => new Color(1f, 0.70f, 0.08f),
                AmmoType.EMP => new Color(0.18f, 0.92f, 1f),
                AmmoType.Twin => new Color(0.92f, 0.32f, 1f),
                AmmoType.Plasma => new Color(0.30f, 1f, 0.58f),
                _ => new Color(0.82f, 0.95f, 1f)
            };
        }

        public static int PickupAmount(AmmoType type, int round)
        {
            int bonus = round >= 60 ? 2 : round >= 30 ? 1 : 0;
            return type switch
            {
                AmmoType.ArmorPiercing => 9 + bonus,
                AmmoType.Explosive => 7 + bonus,
                AmmoType.Incendiary => 7 + bonus,
                AmmoType.EMP => 6 + bonus,
                AmmoType.Twin => 8 + bonus,
                AmmoType.Plasma => 4 + Mathf.Min(2, bonus),
                _ => 0
            };
        }

        public static int UnlockRound(AmmoType type)
        {
            return type switch
            {
                AmmoType.ArmorPiercing => 3,
                AmmoType.Explosive => 8,
                AmmoType.Incendiary => 15,
                AmmoType.EMP => 25,
                AmmoType.Twin => 35,
                AmmoType.Plasma => 50,
                _ => 1
            };
        }

        public static float SpeedMultiplier(AmmoType type)
        {
            return type switch
            {
                AmmoType.ArmorPiercing => 1.18f,
                AmmoType.Explosive => 0.88f,
                AmmoType.Incendiary => 0.95f,
                AmmoType.EMP => 0.92f,
                AmmoType.Twin => 1.02f,
                AmmoType.Plasma => 1.26f,
                _ => 1f
            };
        }

        public static int BonusDamage(AmmoType type)
        {
            return type switch
            {
                AmmoType.ArmorPiercing => 1,
                AmmoType.Explosive => 1,
                AmmoType.Incendiary => 0,
                AmmoType.EMP => 0,
                AmmoType.Twin => 0,
                AmmoType.Plasma => 3,
                _ => 0
            };
        }
    }
}
