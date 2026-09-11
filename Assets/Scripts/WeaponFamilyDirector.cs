using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum PrimaryWeaponFamily
    {
        Autocannon = 0,
        HeavyCannon = 1,
        Railgun = 2,
        PlasmaRepeater = 3
    }

    /// <summary>
    /// v3.2 WEAPON FAMILIES & COMBAT MASTERY.
    /// Replaces the generic unlimited Basic shell with four persistent primary weapon families.
    /// All shots still travel through TankGame.SpawnProjectile and Projectile, so armor, ricochets,
    /// status effects, 3D combat presentation and destruction remain authoritative.
    /// </summary>
    public sealed class WeaponFamilyDirector : MonoBehaviour
    {
        private const string SelectedKey = "TankRevival.WeaponFamily.Selected";
        private const string XpPrefix = "TankRevival.WeaponFamily.XP.";
        private static readonly int[] LevelThresholds = { 0, 20, 55, 110, 190, 300 };

        public static WeaponFamilyDirector Instance { get; private set; }
        public static PrimaryWeaponFamily Selected => (PrimaryWeaponFamily)Mathf.Clamp(PlayerPrefs.GetInt(SelectedKey, 0), 0, 3);

        private TankGame _game;
        private PlayerTank _player;
        private PrimaryWeaponFamily _family;
        private readonly Dictionary<int, PrimaryWeaponFamily> _familyProjectiles = new Dictionary<int, PrimaryWeaponFamily>();
        private bool _registerSpawn;
        private PrimaryWeaponFamily _registerFamily;
        private int _shotSequence;
        private string _toast = string.Empty;
        private float _toastUntil;
        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _accent;

        public PrimaryWeaponFamily Family => _family;
        public int MasteryXp => GetXp(_family);
        public int MasteryLevel => LevelForXp(MasteryXp);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<WeaponFamilyDirector>() != null) return;
            var go = new GameObject("WeaponFamilyDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<WeaponFamilyDirector>();
        }

        private void Awake()
        {
            Instance = this;
            _family = Selected;
            Projectile.ShotSpawned3D += OnShotSpawned;
            Projectile.DamageResolved += OnDamageResolved;
        }

        private void OnDestroy()
        {
            Projectile.ShotSpawned3D -= OnShotSpawned;
            Projectile.DamageResolved -= OnDamageResolved;
            if (Instance == this) Instance = null;
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
                _player = null;
                if (Input.GetKeyDown(KeyCode.F12))
                    CycleFamily(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
                return;
            }

            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerTank>();
                _family = Selected;
                if (_player != null && _player.GetComponent<WeaponFamily3DPresentation>() == null)
                    _player.gameObject.AddComponent<WeaponFamily3DPresentation>();
            }
        }

        private void CycleFamily(int direction)
        {
            int next = ((int)_family + direction) % 4;
            if (next < 0) next += 4;
            _family = (PrimaryWeaponFamily)next;
            PlayerPrefs.SetInt(SelectedKey, next);
            PlayerPrefs.Save();
            Announce("PRIMARY FAMILY // " + FamilyName(_family));
        }

        public float ReloadMultiplier(AmmoType ammo)
        {
            if (ammo != AmmoType.Basic) return 1f;
            float value;
            switch (_family)
            {
                case PrimaryWeaponFamily.Autocannon: value = 0.58f; break;
                case PrimaryWeaponFamily.HeavyCannon: value = 1.58f; break;
                case PrimaryWeaponFamily.Railgun: value = 1.28f; break;
                case PrimaryWeaponFamily.PlasmaRepeater: value = 0.82f; break;
                default: value = 1f; break;
            }

            if (HasChassisSynergy()) value *= 0.94f;
            if (_family == PrimaryWeaponFamily.Autocannon && MasteryLevel >= 3) value *= 0.92f;
            if (_family == PrimaryWeaponFamily.PlasmaRepeater && MasteryLevel >= 4) value *= 0.92f;
            return value;
        }

        public float HeatMultiplier(AmmoType ammo)
        {
            if (ammo == AmmoType.Basic) return 1f;
            if (ammo == AmmoType.Plasma && _family == PrimaryWeaponFamily.PlasmaRepeater)
                return Mathf.Max(0.52f, 0.78f - MasteryLevel * 0.045f);
            if (ammo == AmmoType.Explosive && _family == PrimaryWeaponFamily.HeavyCannon)
                return Mathf.Max(0.76f, 0.96f - MasteryLevel * 0.035f);
            if (ammo == AmmoType.ArmorPiercing && _family == PrimaryWeaponFamily.Railgun)
                return Mathf.Max(0.70f, 0.90f - MasteryLevel * 0.035f);
            return 1f;
        }

        public bool TryFirePrimary(TankGame game, PlayerTank player, AmmoType selectedAmmo, Vector2 muzzle, Vector2 direction, Vector2 side, int baseDamage, float baseSpeed, Color requestedColor)
        {
            if (game == null || player == null || selectedAmmo != AmmoType.Basic) return false;
            _family = Selected;
            _shotSequence++;
            int level = MasteryLevel;
            bool synergy = HasChassisSynergy();

            _registerSpawn = true;
            _registerFamily = _family;
            switch (_family)
            {
                case PrimaryWeaponFamily.Autocannon:
                {
                    int damage = Mathf.Max(1, baseDamage - (level >= 4 ? 0 : 1) + (synergy && level >= 2 ? 1 : 0));
                    float speed = baseSpeed * 1.22f;
                    Spawn(game, muzzle, direction, damage, speed, new Color(1f, 0.82f, 0.30f), AmmoType.Basic);
                    int cadence = GarageLoadoutDirector.SelectedPrimary == GaragePrimaryPackage.VolleyFeed ? 3 : level >= 2 ? 4 : 5;
                    if (_shotSequence % cadence == 0)
                    {
                        Vector2 escort = Quaternion.Euler(0f, 0f, _shotSequence % 2 == 0 ? 3.2f : -3.2f) * direction;
                        Spawn(game, muzzle + side * 0.08f, escort, Mathf.Max(1, damage), speed * 0.96f, new Color(1f, 0.64f, 0.16f), AmmoType.Basic);
                    }
                    if (level >= 5 && _shotSequence % 8 == 0)
                        Spawn(game, muzzle - side * 0.10f, direction, damage + 1, speed, new Color(1f, 0.92f, 0.46f), AmmoType.Basic);
                    break;
                }
                case PrimaryWeaponFamily.HeavyCannon:
                {
                    int damage = baseDamage + 2 + level / 2 + (synergy ? 1 : 0);
                    Spawn(game, muzzle, direction, damage, baseSpeed * 0.78f, new Color(1f, 0.40f, 0.055f), AmmoType.Explosive);
                    if (level >= 5 && _shotSequence % 3 == 0)
                        Spawn(game, muzzle - direction * 0.08f, direction, Mathf.Max(2, damage - 2), baseSpeed * 1.10f, new Color(1f, 0.72f, 0.22f), AmmoType.ArmorPiercing);
                    break;
                }
                case PrimaryWeaponFamily.Railgun:
                {
                    int damage = baseDamage + 2 + (level >= 2 ? 1 : 0) + (GarageLoadoutDirector.SelectedPrimary == GaragePrimaryPackage.BreachCore ? 1 : 0);
                    if (synergy && level >= 3) damage++;
                    Spawn(game, muzzle, direction, damage, baseSpeed * (level >= 4 ? 1.78f : 1.58f), new Color(0.62f, 0.92f, 1f), AmmoType.ArmorPiercing);
                    if (level >= 5 && _shotSequence % 4 == 0)
                        Spawn(game, muzzle + direction * 0.06f, direction, Mathf.Max(2, damage - 2), baseSpeed * 1.88f, new Color(0.84f, 0.98f, 1f), AmmoType.ArmorPiercing);
                    break;
                }
                case PrimaryWeaponFamily.PlasmaRepeater:
                {
                    int damage = baseDamage + 1 + (level >= 3 ? 1 : 0) + (synergy && level >= 2 ? 1 : 0);
                    Spawn(game, muzzle, direction, damage, baseSpeed * 1.18f, new Color(0.28f, 0.92f, 1f), AmmoType.Plasma);
                    if (level >= 5 && _shotSequence % 5 == 0)
                    {
                        Vector2 fork = Quaternion.Euler(0f, 0f, _shotSequence % 2 == 0 ? 5f : -5f) * direction;
                        Spawn(game, muzzle, fork, Mathf.Max(2, damage - 1), baseSpeed * 1.14f, new Color(0.46f, 1f, 0.92f), AmmoType.Plasma);
                    }
                    break;
                }
            }
            _registerSpawn = false;
            return true;
        }

        private void Spawn(TankGame game, Vector2 position, Vector2 direction, int damage, float speed, Color color, AmmoType ammo)
        {
            game.SpawnProjectile(position, direction.normalized, Team.Player, Mathf.Max(1, damage), Mathf.Max(4f, speed), color, ammo);
        }

        private void OnShotSpawned(Projectile projectile, Vector3 position, Vector2 direction, Team team, AmmoType ammo)
        {
            if (!_registerSpawn || projectile == null || team != Team.Player) return;
            _familyProjectiles[projectile.GetInstanceID()] = _registerFamily;
            if (_familyProjectiles.Count > 192)
            {
                var stale = new List<int>(_familyProjectiles.Keys);
                for (int i = 0; i < stale.Count / 2; i++) _familyProjectiles.Remove(stale[i]);
            }
        }

        private void OnDamageResolved(Projectile projectile, Health target, int damage, bool killed)
        {
            if (projectile == null || target == null || target.Team != Team.Enemy) return;
            if (!_familyProjectiles.TryGetValue(projectile.GetInstanceID(), out PrimaryWeaponFamily family)) return;

            int gain = 1 + Mathf.Clamp(damage / 3, 0, 2);
            if (killed)
            {
                gain += 5;
                EnemyTank enemy = target.GetComponent<EnemyTank>();
                if (enemy != null)
                {
                    if (enemy.Kind == EnemyKind.Boss) gain += 16;
                    else if (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege) gain += 5;
                    else if (enemy.Kind == EnemyKind.Elite) gain += 3;
                }
            }
            AddXp(family, gain);
        }

        private static int GetXp(PrimaryWeaponFamily family) => Mathf.Max(0, PlayerPrefs.GetInt(XpPrefix + (int)family, 0));

        private void AddXp(PrimaryWeaponFamily family, int amount)
        {
            int before = GetXp(family);
            int oldLevel = LevelForXp(before);
            int after = Mathf.Min(9999, before + Mathf.Max(1, amount));
            PlayerPrefs.SetInt(XpPrefix + (int)family, after);
            PlayerPrefs.Save();
            int newLevel = LevelForXp(after);
            if (family == _family && newLevel > oldLevel)
            {
                Announce($"{FamilyName(family)} MASTERY {newLevel} // COMBAT UPGRADE ONLINE");
                if (_player != null) VisualFactory.RingPulse(_player.transform.position, FamilyColor(family), 1.65f);
            }
        }

        public static int LevelForXp(int xp)
        {
            int level = 0;
            for (int i = 1; i < LevelThresholds.Length; i++)
                if (xp >= LevelThresholds[i]) level = i;
            return level;
        }

        public static int NextThreshold(int level)
        {
            int next = Mathf.Clamp(level + 1, 1, LevelThresholds.Length - 1);
            return LevelThresholds[next];
        }

        private bool HasChassisSynergy()
        {
            PlayerArsenalSystem arsenal = PlayerArsenalSystem.Instance;
            if (arsenal == null) return false;
            return (_family == PrimaryWeaponFamily.Autocannon && arsenal.Chassis == PlayerChassisSpecialization.Assault)
                || (_family == PrimaryWeaponFamily.HeavyCannon && arsenal.Chassis == PlayerChassisSpecialization.Bastion)
                || (_family == PrimaryWeaponFamily.PlasmaRepeater && arsenal.Chassis == PlayerChassisSpecialization.Scout)
                || (_family == PrimaryWeaponFamily.Railgun && arsenal.Chassis == PlayerChassisSpecialization.Hunter);
        }

        public static string FamilyName(PrimaryWeaponFamily family)
        {
            switch (family)
            {
                case PrimaryWeaponFamily.Autocannon: return "VULCAN AUTOCANNON";
                case PrimaryWeaponFamily.HeavyCannon: return "SIEGE HEAVY CANNON";
                case PrimaryWeaponFamily.Railgun: return "LANCER RAILGUN";
                case PrimaryWeaponFamily.PlasmaRepeater: return "ARC PLASMA REPEATER";
                default: return family.ToString().ToUpperInvariant();
            }
        }

        public static Color FamilyColor(PrimaryWeaponFamily family)
        {
            switch (family)
            {
                case PrimaryWeaponFamily.Autocannon: return new Color(1f, 0.72f, 0.18f);
                case PrimaryWeaponFamily.HeavyCannon: return new Color(1f, 0.28f, 0.06f);
                case PrimaryWeaponFamily.Railgun: return new Color(0.58f, 0.90f, 1f);
                case PrimaryWeaponFamily.PlasmaRepeater: return new Color(0.18f, 1f, 0.84f);
                default: return Color.white;
            }
        }

        private static string FamilyRole(PrimaryWeaponFamily family)
        {
            switch (family)
            {
                case PrimaryWeaponFamily.Autocannon: return "HIGH ROF // ESCORT BURSTS";
                case PrimaryWeaponFamily.HeavyCannon: return "HE SPLASH // BREAKTHROUGH";
                case PrimaryWeaponFamily.Railgun: return "AP PENETRATION // VELOCITY";
                case PrimaryWeaponFamily.PlasmaRepeater: return "MULTI-PEN // HEAT PRESSURE";
                default: return string.Empty;
            }
        }

        private void Announce(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 2.8f;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _title.normal.textColor = new Color(0.84f, 0.92f, 1f);
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleLeft };
            _body.normal.textColor = new Color(0.82f, 0.86f, 0.91f);
            _small = new GUIStyle(_body) { fontSize = 9 };
            _accent = new GUIStyle(_title) { fontSize = 11 };
        }

        private void OnGUI()
        {
            if (_game == null) return;
            EnsureStyles();
            _family = Selected;
            _accent.normal.textColor = FamilyColor(_family);

            if (!_game.IsPlaying)
            {
                Rect panel = new Rect(18f, Screen.height - 122f, 365f, 94f);
                GUI.color = new Color(0.025f, 0.035f, 0.052f, 0.92f); GUI.Box(panel, string.Empty); GUI.color = Color.white;
                GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, 330f, 18f), "PRIMARY WEAPON FAMILY // F12 CYCLE // SHIFT+F12 BACK", _title);
                GUI.Label(new Rect(panel.x + 12f, panel.y + 29f, 330f, 18f), FamilyName(_family), _accent);
                GUI.Label(new Rect(panel.x + 12f, panel.y + 49f, 330f, 16f), FamilyRole(_family), _body);
                GUI.Label(new Rect(panel.x + 12f, panel.y + 66f, 330f, 16f), $"MASTERY {MasteryLevel}/5 // XP {MasteryXp}", _small);
            }
            else if (_player != null)
            {
                Rect panel = new Rect(16f, Screen.height - 204f, 300f, 68f);
                GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.82f); GUI.Box(panel, string.Empty); GUI.color = Color.white;
                GUI.Label(new Rect(panel.x + 10f, panel.y + 7f, 275f, 17f), FamilyName(_family), _accent);
                GUI.Label(new Rect(panel.x + 10f, panel.y + 26f, 275f, 16f), $"MASTERY {MasteryLevel}/5  •  {FamilyRole(_family)}", _small);
                int next = MasteryLevel >= 5 ? MasteryXp : NextThreshold(MasteryLevel);
                float ratio = MasteryLevel >= 5 ? 1f : Mathf.InverseLerp(LevelThresholds[MasteryLevel], next, MasteryXp);
                GUI.color = new Color(0.05f, 0.07f, 0.10f, 0.95f); GUI.Box(new Rect(panel.x + 10f, panel.y + 49f, 274f, 7f), string.Empty);
                GUI.color = FamilyColor(_family); GUI.Box(new Rect(panel.x + 11f, panel.y + 50f, 272f * ratio, 5f), string.Empty); GUI.color = Color.white;
            }

            if (Time.unscaledTime < _toastUntil && !string.IsNullOrEmpty(_toast))
            {
                float width = 510f;
                Rect toast = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.20f, width, 30f);
                GUI.color = new Color(0.015f, 0.025f, 0.04f, 0.90f); GUI.Box(toast, string.Empty); GUI.color = Color.white;
                GUIStyle centered = new GUIStyle(_accent) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(toast, _toast, centered);
            }
        }
    }
}
