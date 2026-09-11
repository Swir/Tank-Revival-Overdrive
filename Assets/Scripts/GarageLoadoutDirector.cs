using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum GaragePrimaryPackage
    {
        Standard = 0,
        VolleyFeed = 1,
        BreachCore = 2
    }

    public enum GarageReactorPackage
    {
        Standard = 0,
        CapacitorBank = 1,
        ThermalSink = 2
    }

    public enum GarageHullPackage
    {
        Standard = 0,
        ReactivePlating = 1,
        RepairLattice = 2
    }

    /// <summary>
    /// v3.1 WAR GARAGE LOADOUTS.
    /// Adds three persistent module slots to the existing garage economy. Unlocks spend the same
    /// lifetime Garage Marks used by the legacy cannon/loader/engine/armor tree, while selected
    /// modules augment the real Projectile, Health, ArmorSystem and v3.0 PlayerArsenal pipelines.
    /// </summary>
    public sealed class GarageLoadoutDirector : MonoBehaviour
    {
        private const string SpentKey = "TankRevival.Garage.LoadoutSpent";
        private const string PrimaryUnlockKey = "TankRevival.Garage.Loadout.PrimaryUnlock";
        private const string ReactorUnlockKey = "TankRevival.Garage.Loadout.ReactorUnlock";
        private const string HullUnlockKey = "TankRevival.Garage.Loadout.HullUnlock";
        private const string PrimarySelectedKey = "TankRevival.Garage.Loadout.Primary";
        private const string ReactorSelectedKey = "TankRevival.Garage.Loadout.Reactor";
        private const string HullSelectedKey = "TankRevival.Garage.Loadout.Hull";

        public static GarageLoadoutDirector Instance { get; private set; }
        public static int PersistentSpentMarks => Mathf.Max(0, PlayerPrefs.GetInt(SpentKey, 0));
        public static GaragePrimaryPackage SelectedPrimary => (GaragePrimaryPackage)Mathf.Clamp(PlayerPrefs.GetInt(PrimarySelectedKey, 0), 0, 2);
        public static GarageReactorPackage SelectedReactor => (GarageReactorPackage)Mathf.Clamp(PlayerPrefs.GetInt(ReactorSelectedKey, 0), 0, 2);
        public static GarageHullPackage SelectedHull => (GarageHullPackage)Mathf.Clamp(PlayerPrefs.GetInt(HullSelectedKey, 0), 0, 2);

        private TankGame _game;
        private PlayerTank _player;
        private Health _playerHealth;
        private ArmorSystem _armor;
        private readonly HashSet<Health> _hookedPlayers = new HashSet<Health>();

        private int _primaryUnlock;
        private int _reactorUnlock;
        private int _hullUnlock;
        private GaragePrimaryPackage _primary;
        private GarageReactorPackage _reactor;
        private GarageHullPackage _hull;
        private int _shotCounter;
        private int _observedRound = -1;
        private bool _suppressExtraShot;
        private bool _lastOverdrive;
        private bool _lastReactorLock;
        private float _reactiveReadyAt;
        private string _toast = string.Empty;
        private float _toastUntil;

        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _good;
        private GUIStyle _locked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<GarageLoadoutDirector>() != null) return;
            var go = new GameObject("GarageLoadoutDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<GarageLoadoutDirector>();
        }

        private void Awake()
        {
            Instance = this;
            LoadPersistentState();
            Projectile.ShotSpawned3D += OnProjectileSpawned;
        }

        private void OnDestroy()
        {
            Projectile.ShotSpawned3D -= OnProjectileSpawned;
            foreach (Health health in _hookedPlayers)
                if (health != null) health.Damaged -= OnPlayerDamaged;
            if (Instance == this) Instance = null;
        }

        private void LoadPersistentState()
        {
            _primaryUnlock = Mathf.Clamp(PlayerPrefs.GetInt(PrimaryUnlockKey, 0), 0, 2);
            _reactorUnlock = Mathf.Clamp(PlayerPrefs.GetInt(ReactorUnlockKey, 0), 0, 2);
            _hullUnlock = Mathf.Clamp(PlayerPrefs.GetInt(HullUnlockKey, 0), 0, 2);
            _primary = (GaragePrimaryPackage)Mathf.Clamp(PlayerPrefs.GetInt(PrimarySelectedKey, 0), 0, _primaryUnlock);
            _reactor = (GarageReactorPackage)Mathf.Clamp(PlayerPrefs.GetInt(ReactorSelectedKey, 0), 0, _reactorUnlock);
            _hull = (GarageHullPackage)Mathf.Clamp(PlayerPrefs.GetInt(HullSelectedKey, 0), 0, _hullUnlock);
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
                HandleGarageInput();
                ReleaseRuntimeReferences();
                return;
            }

            AcquirePlayer();
            if (_player == null || _player.Health == null) return;

            int round = _game.CurrentRound;
            if (round != _observedRound)
            {
                int previous = _observedRound;
                _observedRound = round;
                if (previous >= 0 && round > previous)
                    ApplyRoundService(round);
            }

            PlayerArsenalSystem arsenal = PlayerArsenalSystem.Instance;
            if (arsenal != null)
            {
                bool overdrive = arsenal.OverdriveActive;
                if (overdrive && !_lastOverdrive)
                    OnOverdriveIgnited();
                _lastOverdrive = overdrive;

                bool locked = arsenal.ReactorLocked;
                if (locked && !_lastReactorLock)
                    OnReactorLocked();
                _lastReactorLock = locked;
            }
        }

        private void HandleGarageInput()
        {
            bool buy = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (buy) TryUnlockPrimary(); else CyclePrimary();
            }
            if (Input.GetKeyDown(KeyCode.F10))
            {
                if (buy) TryUnlockReactor(); else CycleReactor();
            }
            if (Input.GetKeyDown(KeyCode.F11))
            {
                if (buy) TryUnlockHull(); else CycleHull();
            }
        }

        private static int LifetimeMarks()
        {
            int contracts = PlayerPrefs.GetInt("TankRevival.ContractsCompleted", 0);
            int legends = PlayerPrefs.GetInt("TankRevival.BossLegendsDefeated", 0);
            int sector = PlayerPrefs.GetInt("TankRevival.BestSector", 0);
            return Mathf.Max(0, contracts + legends * 3 + sector * 2);
        }

        private static int LegacySpentMarks()
        {
            return SpendFor(PlayerPrefs.GetInt("TankRevival.Garage.Cannon", 0))
                 + SpendFor(PlayerPrefs.GetInt("TankRevival.Garage.Loader", 0))
                 + SpendFor(PlayerPrefs.GetInt("TankRevival.Garage.Engine", 0))
                 + SpendFor(PlayerPrefs.GetInt("TankRevival.Garage.Armor", 0));
        }

        private static int SpendFor(int level)
        {
            if (level <= 0) return 0;
            if (level == 1) return 2;
            return 6;
        }

        private int AvailableMarks => Mathf.Max(0, LifetimeMarks() - LegacySpentMarks() - PersistentSpentMarks);

        private static int UnlockCost(int currentUnlock)
        {
            return currentUnlock <= 0 ? 3 : 5;
        }

        private bool SpendMarks(int cost, string label)
        {
            if (AvailableMarks < cost)
            {
                Announce($"{label} // NEED {cost} FREE GARAGE MARKS");
                return false;
            }

            PlayerPrefs.SetInt(SpentKey, PersistentSpentMarks + cost);
            PlayerPrefs.Save();
            return true;
        }

        private void TryUnlockPrimary()
        {
            if (_primaryUnlock >= 2) { Announce("PRIMARY HARDPOINT // ALL PACKAGES UNLOCKED"); return; }
            int cost = UnlockCost(_primaryUnlock);
            if (!SpendMarks(cost, "PRIMARY HARDPOINT")) return;
            _primaryUnlock++;
            PlayerPrefs.SetInt(PrimaryUnlockKey, _primaryUnlock);
            PlayerPrefs.Save();
            Announce($"PRIMARY PACKAGE UNLOCKED // {PrimaryName((GaragePrimaryPackage)_primaryUnlock)}");
        }

        private void TryUnlockReactor()
        {
            if (_reactorUnlock >= 2) { Announce("REACTOR BAY // ALL PACKAGES UNLOCKED"); return; }
            int cost = UnlockCost(_reactorUnlock);
            if (!SpendMarks(cost, "REACTOR BAY")) return;
            _reactorUnlock++;
            PlayerPrefs.SetInt(ReactorUnlockKey, _reactorUnlock);
            PlayerPrefs.Save();
            Announce($"REACTOR PACKAGE UNLOCKED // {ReactorName((GarageReactorPackage)_reactorUnlock)}");
        }

        private void TryUnlockHull()
        {
            if (_hullUnlock >= 2) { Announce("HULL SYSTEM // ALL PACKAGES UNLOCKED"); return; }
            int cost = UnlockCost(_hullUnlock);
            if (!SpendMarks(cost, "HULL SYSTEM")) return;
            _hullUnlock++;
            PlayerPrefs.SetInt(HullUnlockKey, _hullUnlock);
            PlayerPrefs.Save();
            Announce($"HULL PACKAGE UNLOCKED // {HullName((GarageHullPackage)_hullUnlock)}");
        }

        private void CyclePrimary()
        {
            _primary = (GaragePrimaryPackage)(((int)_primary + 1) % (_primaryUnlock + 1));
            PlayerPrefs.SetInt(PrimarySelectedKey, (int)_primary);
            PlayerPrefs.Save();
            Announce("PRIMARY // " + PrimaryName(_primary));
        }

        private void CycleReactor()
        {
            _reactor = (GarageReactorPackage)(((int)_reactor + 1) % (_reactorUnlock + 1));
            PlayerPrefs.SetInt(ReactorSelectedKey, (int)_reactor);
            PlayerPrefs.Save();
            Announce("REACTOR // " + ReactorName(_reactor));
        }

        private void CycleHull()
        {
            _hull = (GarageHullPackage)(((int)_hull + 1) % (_hullUnlock + 1));
            PlayerPrefs.SetInt(HullSelectedKey, (int)_hull);
            PlayerPrefs.Save();
            Announce("HULL // " + HullName(_hull));
        }

        private void AcquirePlayer()
        {
            if (_player != null) return;
            _player = FindAnyObjectByType<PlayerTank>();
            if (_player == null) return;

            _playerHealth = _player.Health;
            _armor = _player.GetComponent<ArmorSystem>();
            _shotCounter = 0;
            _lastOverdrive = false;
            _lastReactorLock = false;

            if (_playerHealth != null && !_hookedPlayers.Contains(_playerHealth))
            {
                _hookedPlayers.Add(_playerHealth);
                _playerHealth.Damaged += OnPlayerDamaged;
            }

            ApplySpawnLoadout();
            if (_player.GetComponent<GarageLoadout3DPresentation>() == null)
                _player.gameObject.AddComponent<GarageLoadout3DPresentation>();
        }

        private void ReleaseRuntimeReferences()
        {
            _player = null;
            _playerHealth = null;
            _armor = null;
            _observedRound = -1;
            _lastOverdrive = false;
            _lastReactorLock = false;
            _hookedPlayers.RemoveWhere(h => h == null);
        }

        private void ApplySpawnLoadout()
        {
            if (_player == null || _player.Health == null) return;

            if (_hull == GarageHullPackage.ReactivePlating)
                _player.Health.SetMaximum(_player.Health.Maximum + 1, true);
            else if (_hull == GarageHullPackage.RepairLattice)
                _player.Health.SetMaximum(_player.Health.Maximum + 1, false);

            if (_primary == GaragePrimaryPackage.VolleyFeed)
                _player.AddAmmo(AmmoType.Twin, 4);
            else if (_primary == GaragePrimaryPackage.BreachCore)
                _player.AddAmmo(AmmoType.ArmorPiercing, 5);

            if (_reactor == GarageReactorPackage.CapacitorBank)
                _player.AddAmmo(AmmoType.Plasma, 1);

            Announce($"LOADOUT DEPLOYED // {PrimaryName(_primary)} + {ReactorName(_reactor)} + {HullName(_hull)}");
        }

        private void ApplyRoundService(int round)
        {
            if (_player == null || _player.Health == null || _player.Health.IsDead) return;

            if (_primary == GaragePrimaryPackage.VolleyFeed)
                _player.AddAmmo(AmmoType.Twin, 1 + (round >= 50 ? 1 : 0));
            else if (_primary == GaragePrimaryPackage.BreachCore)
                _player.AddAmmo(AmmoType.ArmorPiercing, 2);

            if (_hull == GarageHullPackage.RepairLattice)
            {
                if (round % 3 == 0) _player.Health.Heal(1);
                if (_armor != null) _armor.RepairModules(12 + (round >= 60 ? 5 : 0));
            }
        }

        private void OnProjectileSpawned(Projectile projectile, Vector3 position, Vector2 direction, Team team, AmmoType ammo)
        {
            if (_suppressExtraShot || team != Team.Player || projectile == null || _player == null || _game == null) return;
            if (Vector2.Distance(position, _player.transform.position) > 1.65f) return;

            _shotCounter++;
            if (_primary == GaragePrimaryPackage.VolleyFeed && _shotCounter % 5 == 0)
            {
                SpawnEscortShot(projectile, position, direction, AmmoType.Twin, 0.11f, 0);
            }
            else if (_primary == GaragePrimaryPackage.BreachCore && _shotCounter % 7 == 0)
            {
                SpawnEscortShot(projectile, position, direction, AmmoType.ArmorPiercing, -0.12f, 1);
            }
        }

        private void SpawnEscortShot(Projectile source, Vector3 position, Vector2 direction, AmmoType ammo, float sideOffset, int bonusDamage)
        {
            Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            Vector2 side = new Vector2(-dir.y, dir.x);
            Color color = AmmoDatabase.Color(ammo);
            _suppressExtraShot = true;
            _game.SpawnProjectile((Vector2)position + dir * 0.08f + side * sideOffset, dir, Team.Player,
                Mathf.Max(1, source.Damage + bonusDamage), ammo == AmmoType.ArmorPiercing ? 14.5f : 12.2f, color, ammo);
            _suppressExtraShot = false;
            VisualFactory.MuzzleFlash(position, color, 0.62f);
        }

        private void OnOverdriveIgnited()
        {
            if (_player == null || _player.Health == null) return;
            if (_reactor == GarageReactorPackage.CapacitorBank)
            {
                _player.Health.InvulnerableUntil = Mathf.Max(_player.Health.InvulnerableUntil, Time.time + 1.45f);
                _player.AddAmmo(AmmoType.Plasma, 2);
                VisualFactory.RingPulse(_player.transform.position, new Color(0.30f, 0.84f, 1f), 1.45f);
                Announce("CAPACITOR BANK // OVERDRIVE SURGE BUFFERED");
            }
        }

        private void OnReactorLocked()
        {
            if (_player == null || _reactor != GarageReactorPackage.ThermalSink) return;

            if (_armor != null) _armor.RepairModules(8);
            Collider2D[] hits = Physics2D.OverlapCircleAll(_player.transform.position, 2.6f);
            int affected = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                Health health = hits[i].GetComponent<Health>();
                if (health == null || health.Team != Team.Enemy || health.IsDead) continue;
                CombatStatus status = health.GetComponent<CombatStatus>();
                if (status == null) status = health.gameObject.AddComponent<CombatStatus>();
                status.ApplyEmp(1.35f);
                affected++;
            }
            VisualFactory.RingPulse(_player.transform.position, AmmoDatabase.Color(AmmoType.EMP), 2.6f);
            Announce($"THERMAL SINK VENT // EMP SHOCK {affected} TARGETS");
        }

        private void OnPlayerDamaged(Health health, int amount)
        {
            if (health == null || amount <= 0 || _hull != GarageHullPackage.ReactivePlating) return;
            if (Time.time < _reactiveReadyAt) return;
            _reactiveReadyAt = Time.time + 7.5f;

            health.Heal(1);
            if (_armor == null) _armor = health.GetComponent<ArmorSystem>();
            if (_armor != null) _armor.RepairModules(7);
            VisualFactory.RingPulse(health.transform.position, new Color(1f, 0.65f, 0.12f), 1.05f);
            Announce("REACTIVE PLATING // IMPACT DISPERSED");
        }

        private void Announce(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 2.5f;
        }

        public static string PrimaryName(GaragePrimaryPackage package)
        {
            switch (package)
            {
                case GaragePrimaryPackage.VolleyFeed: return "VOLLEY FEED";
                case GaragePrimaryPackage.BreachCore: return "BREACH CORE";
                default: return "STANDARD FEED";
            }
        }

        public static string ReactorName(GarageReactorPackage package)
        {
            switch (package)
            {
                case GarageReactorPackage.CapacitorBank: return "CAPACITOR BANK";
                case GarageReactorPackage.ThermalSink: return "THERMAL SINK";
                default: return "STANDARD REACTOR";
            }
        }

        public static string HullName(GarageHullPackage package)
        {
            switch (package)
            {
                case GarageHullPackage.ReactivePlating: return "REACTIVE PLATING";
                case GarageHullPackage.RepairLattice: return "REPAIR LATTICE";
                default: return "STANDARD HULL";
            }
        }

        private static string PrimaryEffect(GaragePrimaryPackage package)
        {
            switch (package)
            {
                case GaragePrimaryPackage.VolleyFeed: return "Every 5th player shot adds a real Twin escort round; resupplies Twin each round.";
                case GaragePrimaryPackage.BreachCore: return "Every 7th player shot adds a high-speed AP escort round; AP resupply each round.";
                default: return "Baseline cannon feed. No additional heat or escort projectiles.";
            }
        }

        private static string ReactorEffect(GarageReactorPackage package)
        {
            switch (package)
            {
                case GarageReactorPackage.CapacitorBank: return "Overdrive ignition grants a short shield and two Plasma rounds.";
                case GarageReactorPackage.ThermalSink: return "Reactor lock vents an EMP shock and repairs damaged armor modules.";
                default: return "Baseline v3.0 Arsenal reactor behavior.";
            }
        }

        private static string HullEffect(GarageHullPackage package)
        {
            switch (package)
            {
                case GarageHullPackage.ReactivePlating: return "+1 max HP; periodically cancels one damage and repairs armor modules.";
                case GarageHullPackage.RepairLattice: return "+1 max HP capacity; repairs armor every round and hull every third round.";
                default: return "Baseline hull durability.";
            }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.34f, 0.90f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.90f, 0.95f, 1f) } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.62f, 0.72f, 0.82f) } };
            _good = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.40f, 1f, 0.62f) } };
            _locked = new GUIStyle(_small) { normal = { textColor = new Color(0.84f, 0.55f, 0.34f) } };
        }

        private void OnGUI()
        {
            if (_game == null) return;
            EnsureStyles();

            if (!_game.IsPlaying)
            {
                float x = Mathf.Max(12f, Screen.width - 520f);
                float y = 424f;
                GUI.color = new Color(0.018f, 0.030f, 0.050f, 0.95f);
                GUI.Box(new Rect(x, y, 500f, 228f), string.Empty);
                GUI.color = Color.white;

                GUI.Label(new Rect(x + 18f, y + 10f, 460f, 22f), "v3.1 LOADOUT BAY // SHARED GARAGE MARKS", _title);
                GUI.Label(new Rect(x + 18f, y + 35f, 460f, 18f), $"FREE MARKS {AvailableMarks} // MODULE SPENT {PersistentSpentMarks}", _body);
                GUI.Label(new Rect(x + 18f, y + 58f, 460f, 18f), $"F9 Primary: {PrimaryName(_primary)}  [{_primaryUnlock}/2 unlocked]", _good);
                GUI.Label(new Rect(x + 32f, y + 77f, 445f, 18f), PrimaryEffect(_primary), _small);
                GUI.Label(new Rect(x + 18f, y + 100f, 460f, 18f), $"F10 Reactor: {ReactorName(_reactor)}  [{_reactorUnlock}/2 unlocked]", _good);
                GUI.Label(new Rect(x + 32f, y + 119f, 445f, 18f), ReactorEffect(_reactor), _small);
                GUI.Label(new Rect(x + 18f, y + 142f, 460f, 18f), $"F11 Hull: {HullName(_hull)}  [{_hullUnlock}/2 unlocked]", _good);
                GUI.Label(new Rect(x + 32f, y + 161f, 445f, 18f), HullEffect(_hull), _small);
                GUI.Label(new Rect(x + 18f, y + 188f, 460f, 30f), "F9/F10/F11 cycle unlocked packages. Hold SHIFT + key to unlock next tier (3 then 5 marks).", _locked);
            }
            else if (_player != null)
            {
                float y = Mathf.Max(12f, Screen.height - 62f);
                GUI.color = new Color(0.015f, 0.025f, 0.040f, 0.88f);
                GUI.Box(new Rect(Screen.width * 0.5f - 310f, y, 620f, 42f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 298f, y + 5f, 596f, 18f), $"LOADOUT // {PrimaryName(_primary)} // {ReactorName(_reactor)} // {HullName(_hull)}", _body);
                GUI.Label(new Rect(Screen.width * 0.5f - 298f, y + 23f, 596f, 15f), "Persistent garage modules active on the real combat pipeline", _small);
            }

            if (Time.unscaledTime < _toastUntil)
            {
                GUI.color = new Color(0.02f, 0.07f, 0.09f, 0.96f);
                GUI.Box(new Rect(Screen.width * 0.5f - 300f, Screen.height * 0.29f, 600f, 38f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 286f, Screen.height * 0.29f + 8f, 572f, 22f), _toast, _good);
            }
        }
    }
}
