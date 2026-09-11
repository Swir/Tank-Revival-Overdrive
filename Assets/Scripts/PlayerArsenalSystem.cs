using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum PlayerChassisSpecialization
    {
        Assault = 0,
        Bastion = 1,
        Scout = 2,
        Hunter = 3
    }

    /// <summary>
    /// v3.0 ARMORED ARSENAL.
    /// Turns the four persistent War Garage chassis into genuinely different combat machines.
    /// The system deliberately uses the existing TankGame projectile pipeline, Health, ArmorSystem,
    /// CombatStatus and PlayerPrefs garage selection instead of creating a parallel combat model.
    /// </summary>
    public sealed class PlayerArsenalSystem : MonoBehaviour
    {
        public static PlayerArsenalSystem Instance { get; private set; }

        public PlayerChassisSpecialization Chassis => _chassis;
        public float Energy => _energy;
        public float Heat => _heat;
        public float EnergyRatio => _energy / MaxEnergy;
        public float HeatRatio => _heat / MaxHeat;
        public bool AbilityActive => Time.time < _abilityUntil;
        public bool OverdriveActive => Time.time < _overdriveUntil;
        public bool ReactorLocked => Time.time < _reactorLockedUntil;
        public string SecondaryName => SecondaryLabel(_chassis);
        public string AbilityName => AbilityLabel(_chassis);

        private const float MaxEnergy = 100f;
        private const float MaxHeat = 100f;

        private TankGame _game;
        private PlayerTank _player;
        private TankTurretRig _turret;
        private PlayerChassisSpecialization _chassis;
        private readonly HashSet<Health> _hookedEnemies = new HashSet<Health>();

        private float _energy = 28f;
        private float _heat;
        private float _nextScan;
        private float _nextSecondary;
        private float _nextAbility;
        private float _abilityUntil;
        private float _overdriveUntil;
        private float _reactorLockedUntil;
        private float _nextPassiveEnergy;
        private int _observedRound = -1;
        private bool _suppressShotHeat;
        private string _toast = string.Empty;
        private float _toastUntil;

        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _hot;
        private GUIStyle _ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<PlayerArsenalSystem>() != null) return;
            var go = new GameObject("PlayerArsenalSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerArsenalSystem>();
        }

        private void Awake()
        {
            Instance = this;
            _chassis = (PlayerChassisSpecialization)Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.Garage.Chassis", 0), 0, 3);
            Projectile.ShotSpawned3D += OnProjectileSpawned;
        }

        private void OnDestroy()
        {
            Projectile.ShotSpawned3D -= OnProjectileSpawned;
            foreach (Health health in _hookedEnemies)
                if (health != null) health.Died -= OnEnemyDied;
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
                _turret = null;
                _observedRound = -1;
                _hookedEnemies.RemoveWhere(h => h == null);
                return;
            }

            AcquirePlayer();
            if (_player == null) return;

            int round = _game.CurrentRound;
            if (round != _observedRound)
            {
                _observedRound = round;
                _chassis = (PlayerChassisSpecialization)Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.Garage.Chassis", 0), 0, 3);
                _energy = Mathf.Min(MaxEnergy, _energy + 10f + Mathf.Min(10f, round * 0.10f));
                _heat = Mathf.Max(0f, _heat - 30f);
                Announce($"ARSENAL ONLINE // {ChassisLabel(_chassis)}");
            }

            float cooling = CoolingRate(_chassis);
            if (OverdriveActive) cooling *= 1.8f;
            _heat = Mathf.Max(0f, _heat - cooling * Time.deltaTime);

            if (Time.time >= _nextPassiveEnergy)
            {
                _nextPassiveEnergy = Time.time + 1f;
                _energy = Mathf.Min(MaxEnergy, _energy + (OverdriveActive ? 0f : 0.75f));
            }

            if (_heat >= MaxHeat && !ReactorLocked)
            {
                _reactorLockedUntil = Time.time + 2.15f;
                _heat = 94f;
                Announce("REACTOR OVERHEAT // WEAPONS COOLING");
                VisualFactory.RingPulse(_player.transform.position, new Color(1f, 0.20f, 0.05f), 1.25f);
            }

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.45f;
                HookEnemies();
            }

            if (!ReactorLocked && Input.GetKeyDown(KeyCode.R))
                FireSecondary();

            if (Input.GetKeyDown(KeyCode.LeftShift))
                ActivateChassisAbility();

            if (Input.GetKeyDown(KeyCode.G))
                ActivateOverdrive();
        }

        private void AcquirePlayer()
        {
            if (_player != null) return;
            _player = FindAnyObjectByType<PlayerTank>();
            if (_player == null) return;

            _turret = _player.GetComponent<TankTurretRig>();
            _chassis = (PlayerChassisSpecialization)Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.Garage.Chassis", 0), 0, 3);
            if (_player.GetComponent<PlayerArsenal3DPresentation>() == null)
                _player.gameObject.AddComponent<PlayerArsenal3DPresentation>();
        }

        private void HookEnemies()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || _hookedEnemies.Contains(enemy.Health)) continue;
                _hookedEnemies.Add(enemy.Health);
                enemy.Health.Died += OnEnemyDied;
            }
            _hookedEnemies.RemoveWhere(h => h == null);
        }

        private void OnEnemyDied(Health health)
        {
            if (health == null) return;
            EnemyTank enemy = health.GetComponent<EnemyTank>();
            float gain = 10f;
            if (enemy != null)
            {
                if (enemy.Kind == EnemyKind.Boss) gain = 34f;
                else if (enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Heavy) gain = 16f;
                else if (enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Sniper) gain = 13f;
                else if (enemy.Kind == EnemyKind.Fast) gain = 9f;
            }

            _energy = Mathf.Min(MaxEnergy, _energy + gain);
            _heat = Mathf.Max(0f, _heat - 4f);
        }

        private void OnProjectileSpawned(Projectile projectile, Vector3 position, Vector2 direction, Team team, AmmoType ammo)
        {
            if (_suppressShotHeat || team != Team.Player || _player == null) return;
            if (Vector2.Distance(position, _player.transform.position) > 1.55f) return;

            float heat = ammo == AmmoType.Plasma ? 10f : ammo == AmmoType.Explosive || ammo == AmmoType.ArmorPiercing ? 6.5f : 4.2f;
            if (AbilityActive && _chassis == PlayerChassisSpecialization.Assault) heat *= 0.70f;
            if (OverdriveActive) heat *= 0.48f;
            _heat = Mathf.Min(MaxHeat + 8f, _heat + heat);
        }

        private Vector2 AimDirection()
        {
            if (_turret == null && _player != null) _turret = _player.GetComponent<TankTurretRig>();
            Vector2 direction = _turret != null ? _turret.AimDirection : (Vector2)_player.transform.up;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        }

        private Vector2 Muzzle(Vector2 direction, float distance = 0.88f)
        {
            return (Vector2)_player.transform.position + direction * distance;
        }

        private void FireSecondary()
        {
            if (_player == null || _game == null || Time.time < _nextSecondary) return;

            float cost = SecondaryEnergyCost(_chassis);
            if (_energy < cost)
            {
                Announce($"{SecondaryName} // NEED {Mathf.CeilToInt(cost)} ENERGY");
                return;
            }

            if (_heat >= 82f)
            {
                Announce("SECONDARY BLOCKED // REACTOR TOO HOT");
                return;
            }

            _energy -= cost;
            _heat = Mathf.Min(MaxHeat, _heat + SecondaryHeat(_chassis));
            _nextSecondary = Time.time + SecondaryCooldown(_chassis);
            Vector2 dir = AimDirection();
            int baseDamage = Mathf.Max(2, _player.EffectiveShotDamage);

            _suppressShotHeat = true;
            switch (_chassis)
            {
                case PlayerChassisSpecialization.Assault:
                    FireAssaultSalvo(dir, baseDamage);
                    break;
                case PlayerChassisSpecialization.Bastion:
                    FireBastionSiegeShell(dir, baseDamage);
                    break;
                case PlayerChassisSpecialization.Scout:
                    FireScoutEmpBurst();
                    break;
                case PlayerChassisSpecialization.Hunter:
                    FireHunterRailLance(dir, baseDamage);
                    break;
            }
            _suppressShotHeat = false;

            _turret?.KickRecoil(_chassis == PlayerChassisSpecialization.Hunter || _chassis == PlayerChassisSpecialization.Bastion ? 2.1f : 1.35f);
            _game.KickCamera(0.075f, 0.055f);
            Announce($"SECONDARY // {SecondaryName}");
        }

        private void FireAssaultSalvo(Vector2 dir, int damage)
        {
            Color color = AmmoDatabase.Color(AmmoType.Explosive);
            float[] angles = { -8f, 0f, 8f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector2 shot = Quaternion.Euler(0f, 0f, angles[i]) * dir;
                _game.SpawnProjectile(Muzzle(dir), shot, Team.Player, Mathf.Max(2, damage), 10.8f, color, AmmoType.Explosive);
            }
            VisualFactory.MuzzleFlash(Muzzle(dir), color, 1.35f);
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.78f);
        }

        private void FireBastionSiegeShell(Vector2 dir, int damage)
        {
            Color color = new Color(1f, 0.46f, 0.08f);
            _game.SpawnProjectile(Muzzle(dir, 0.95f), dir, Team.Player, damage + 4, 8.2f, color, AmmoType.Explosive);
            VisualFactory.MuzzleFlash(Muzzle(dir), color, 1.65f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.70f);
        }

        private void FireScoutEmpBurst()
        {
            Vector2 center = _player.transform.position;
            const float radius = 3.8f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
            int affected = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                Health health = hits[i].GetComponent<Health>();
                if (health == null || health.Team != Team.Enemy || health.IsDead) continue;
                CombatStatus status = health.GetComponent<CombatStatus>();
                if (status == null) status = health.gameObject.AddComponent<CombatStatus>();
                status.ApplyEmp(3.2f);
                health.Damage(1, Team.Player);
                affected++;
            }
            VisualFactory.RingPulse(center, AmmoDatabase.Color(AmmoType.EMP), radius);
            if (affected > 0) _energy = Mathf.Min(MaxEnergy, _energy + Mathf.Min(10f, affected * 1.5f));
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.72f);
        }

        private void FireHunterRailLance(Vector2 dir, int damage)
        {
            Color color = new Color(0.76f, 0.94f, 1f);
            _game.SpawnProjectile(Muzzle(dir, 0.96f), dir, Team.Player, damage + 5, 18.5f, color, AmmoType.ArmorPiercing);
            VisualFactory.MuzzleFlash(Muzzle(dir), color, 1.55f);
            BattleAudio.PlayGlobal(SoundCue.Plasma, 0.76f);
        }

        private void ActivateChassisAbility()
        {
            if (_player == null || Time.time < _nextAbility) return;
            const float cost = 35f;
            if (_energy < cost)
            {
                Announce($"{AbilityName} // NEED 35 ENERGY");
                return;
            }

            _energy -= cost;
            _nextAbility = Time.time + 13f;
            _abilityUntil = Time.time + AbilityDuration(_chassis);

            switch (_chassis)
            {
                case PlayerChassisSpecialization.Assault:
                    _player.AddAmmo(AmmoType.Twin, 6);
                    _player.AddAmmo(AmmoType.Explosive, 4);
                    _heat = Mathf.Max(0f, _heat - 28f);
                    break;
                case PlayerChassisSpecialization.Bastion:
                    _player.Health.Heal(2);
                    _player.Health.InvulnerableUntil = Mathf.Max(_player.Health.InvulnerableUntil, Time.time + 3.4f);
                    break;
                case PlayerChassisSpecialization.Scout:
                    _player.Health.InvulnerableUntil = Mathf.Max(_player.Health.InvulnerableUntil, Time.time + 1.4f);
                    PulseScoutJammer(4.8f, 1.8f);
                    _heat = Mathf.Max(0f, _heat - 42f);
                    break;
                case PlayerChassisSpecialization.Hunter:
                    _player.AddAmmo(AmmoType.ArmorPiercing, 8);
                    _player.AddAmmo(AmmoType.Plasma, 2);
                    _heat = Mathf.Max(0f, _heat - 24f);
                    break;
            }

            VisualFactory.RingPulse(_player.transform.position, ChassisColor(_chassis), 1.45f);
            Announce($"ABILITY ACTIVE // {AbilityName}");
        }

        private void ActivateOverdrive()
        {
            if (_player == null || OverdriveActive) return;
            if (_energy < MaxEnergy - 0.1f)
            {
                Announce($"OVERDRIVE CHARGE // {Mathf.FloorToInt(_energy)}%");
                return;
            }
            if (_heat > 72f)
            {
                Announce("OVERDRIVE BLOCKED // COOL REACTOR BELOW 72%");
                return;
            }

            _energy = 0f;
            _heat = Mathf.Max(0f, _heat - 48f);
            _overdriveUntil = Time.time + 9f;
            _abilityUntil = Mathf.Max(_abilityUntil, Time.time + 9f);
            _reactorLockedUntil = 0f;

            switch (_chassis)
            {
                case PlayerChassisSpecialization.Assault:
                    _player.AddAmmo(AmmoType.Twin, 12);
                    _player.AddAmmo(AmmoType.Explosive, 8);
                    RadialBarrage(8, 3, AmmoType.Explosive, 11.2f);
                    break;
                case PlayerChassisSpecialization.Bastion:
                    _player.Health.Heal(4);
                    _player.Health.InvulnerableUntil = Mathf.Max(_player.Health.InvulnerableUntil, Time.time + 5.0f);
                    RadialBarrage(6, 4, AmmoType.Explosive, 9.0f);
                    break;
                case PlayerChassisSpecialization.Scout:
                    _player.Health.InvulnerableUntil = Mathf.Max(_player.Health.InvulnerableUntil, Time.time + 2.8f);
                    PulseScoutJammer(6.2f, 5.0f);
                    _player.AddAmmo(AmmoType.EMP, 10);
                    break;
                case PlayerChassisSpecialization.Hunter:
                    _player.AddAmmo(AmmoType.ArmorPiercing, 16);
                    _player.AddAmmo(AmmoType.Plasma, 5);
                    HunterExecutionVolley();
                    break;
            }

            VisualFactory.Explosion(_player.transform.position, ChassisColor(_chassis), 1.35f);
            VisualFactory.RingPulse(_player.transform.position, Color.white, 2.2f);
            _game.KickCamera(0.14f, 0.10f);
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.60f);
            Announce($"OVERDRIVE // {ChassisLabel(_chassis)} MAXIMUM OUTPUT");
        }

        private void RadialBarrage(int count, int damage, AmmoType ammo, float speed)
        {
            _suppressShotHeat = true;
            Color color = AmmoDatabase.Color(ammo);
            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count);
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                _game.SpawnProjectile((Vector2)_player.transform.position + dir * 0.72f, dir, Team.Player, damage, speed, color, ammo);
            }
            _suppressShotHeat = false;
        }

        private void PulseScoutJammer(float radius, float seconds)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(_player.transform.position, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Health health = hits[i].GetComponent<Health>();
                if (health == null || health.Team != Team.Enemy || health.IsDead) continue;
                CombatStatus status = health.GetComponent<CombatStatus>();
                if (status == null) status = health.gameObject.AddComponent<CombatStatus>();
                status.ApplyEmp(seconds);
            }
            VisualFactory.RingPulse(_player.transform.position, AmmoDatabase.Color(AmmoType.EMP), radius);
        }

        private void HunterExecutionVolley()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            int fired = 0;
            _suppressShotHeat = true;
            for (int i = 0; i < enemies.Length && fired < 5; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                Vector2 dir = ((Vector2)enemy.transform.position - (Vector2)_player.transform.position).normalized;
                if (dir.sqrMagnitude < 0.01f) continue;
                _game.SpawnProjectile((Vector2)_player.transform.position + dir * 0.90f, dir, Team.Player,
                    Mathf.Max(5, _player.EffectiveShotDamage + 4), 19.5f, new Color(0.75f, 0.95f, 1f), AmmoType.ArmorPiercing);
                fired++;
            }
            _suppressShotHeat = false;
        }

        private static float SecondaryEnergyCost(PlayerChassisSpecialization chassis)
        {
            return chassis switch
            {
                PlayerChassisSpecialization.Assault => 24f,
                PlayerChassisSpecialization.Bastion => 28f,
                PlayerChassisSpecialization.Scout => 22f,
                PlayerChassisSpecialization.Hunter => 30f,
                _ => 25f
            };
        }

        private static float SecondaryHeat(PlayerChassisSpecialization chassis)
        {
            return chassis switch
            {
                PlayerChassisSpecialization.Assault => 22f,
                PlayerChassisSpecialization.Bastion => 30f,
                PlayerChassisSpecialization.Scout => 14f,
                PlayerChassisSpecialization.Hunter => 34f,
                _ => 20f
            };
        }

        private static float SecondaryCooldown(PlayerChassisSpecialization chassis)
        {
            return chassis switch
            {
                PlayerChassisSpecialization.Assault => 3.2f,
                PlayerChassisSpecialization.Bastion => 4.4f,
                PlayerChassisSpecialization.Scout => 4.8f,
                PlayerChassisSpecialization.Hunter => 4.9f,
                _ => 4f
            };
        }

        private static float CoolingRate(PlayerChassisSpecialization chassis)
        {
            return chassis switch
            {
                PlayerChassisSpecialization.Assault => 8.8f,
                PlayerChassisSpecialization.Bastion => 7.3f,
                PlayerChassisSpecialization.Scout => 12.2f,
                PlayerChassisSpecialization.Hunter => 6.7f,
                _ => 8f
            };
        }

        private static float AbilityDuration(PlayerChassisSpecialization chassis)
        {
            return chassis switch
            {
                PlayerChassisSpecialization.Assault => 6.5f,
                PlayerChassisSpecialization.Bastion => 6.0f,
                PlayerChassisSpecialization.Scout => 5.5f,
                PlayerChassisSpecialization.Hunter => 7.0f,
                _ => 6f
            };
        }

        public static string ChassisLabel(PlayerChassisSpecialization chassis)
        {
            return chassis switch
            {
                PlayerChassisSpecialization.Assault => "ORZEŁ MK-I ASSAULT",
                PlayerChassisSpecialization.Bastion => "BASTION HEAVY",
                PlayerChassisSpecialization.Scout => "WICHER SCOUT",
                PlayerChassisSpecialization.Hunter => "HUNTER TD",
                _ => "UNKNOWN"
            };
        }

        private static string SecondaryLabel(PlayerChassisSpecialization chassis)
        {
            return chassis switch
            {
                PlayerChassisSpecialization.Assault => "TRIPLE HE SALVO",
                PlayerChassisSpecialization.Bastion => "SIEGE BREAKER",
                PlayerChassisSpecialization.Scout => "EMP BURST",
                PlayerChassisSpecialization.Hunter => "RAIL LANCE",
                _ => "SECONDARY"
            };
        }

        private static string AbilityLabel(PlayerChassisSpecialization chassis)
        {
            return chassis switch
            {
                PlayerChassisSpecialization.Assault => "BARRAGE DRIVE",
                PlayerChassisSpecialization.Bastion => "IRON CITADEL",
                PlayerChassisSpecialization.Scout => "GHOST OVERBOOST",
                PlayerChassisSpecialization.Hunter => "PREDATOR LOCK",
                _ => "ABILITY"
            };
        }

        public static Color ChassisColor(PlayerChassisSpecialization chassis)
        {
            return chassis switch
            {
                PlayerChassisSpecialization.Assault => new Color(0.22f, 0.82f, 1f),
                PlayerChassisSpecialization.Bastion => new Color(0.38f, 0.72f, 1f),
                PlayerChassisSpecialization.Scout => new Color(0.18f, 1f, 0.72f),
                PlayerChassisSpecialization.Hunter => new Color(0.78f, 0.62f, 1f),
                _ => Color.white
            };
        }

        private void Announce(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 2.2f;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.56f, 0.92f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.88f, 0.94f, 1f) } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = new Color(0.62f, 0.72f, 0.82f) } };
            _hot = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.32f, 0.10f) } };
            _ready = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.42f, 1f, 0.58f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _player == null) return;
            EnsureStyles();

            float x = 16f;
            float y = Screen.height - 176f;
            GUI.color = new Color(0.012f, 0.024f, 0.042f, 0.90f);
            GUI.Box(new Rect(x, y, 330f, 158f), string.Empty);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 12f, y + 8f, 305f, 20f), $"ARSENAL // {ChassisLabel(_chassis)}", _title);
            DrawBar(new Rect(x + 12f, y + 35f, 220f, 10f), EnergyRatio, new Color(0.20f, 0.82f, 1f));
            GUI.Label(new Rect(x + 239f, y + 29f, 80f, 18f), $"ENERGY {_energy:0}", _body);
            DrawBar(new Rect(x + 12f, y + 55f, 220f, 10f), HeatRatio, new Color(1f, 0.30f, 0.08f));
            GUI.Label(new Rect(x + 239f, y + 49f, 80f, 18f), $"HEAT {_heat:0}", ReactorLocked ? _hot : _body);

            GUI.Label(new Rect(x + 12f, y + 76f, 305f, 18f), $"R  {SecondaryName}", Time.time >= _nextSecondary && !ReactorLocked ? _ready : _body);
            GUI.Label(new Rect(x + 12f, y + 94f, 305f, 18f), $"SHIFT  {AbilityName}", Time.time >= _nextAbility ? _ready : _body);
            GUI.Label(new Rect(x + 12f, y + 112f, 305f, 18f), "G  OVERDRIVE // requires 100 energy + cool reactor", _energy >= 99.9f && _heat <= 72f ? _ready : _small);

            string state = OverdriveActive ? "MAXIMUM OUTPUT" : AbilityActive ? "ABILITY ACTIVE" : ReactorLocked ? "REACTOR LOCK" : "COMBAT READY";
            GUI.Label(new Rect(x + 12f, y + 133f, 305f, 18f), state, ReactorLocked ? _hot : OverdriveActive ? _ready : _small);

            if (Time.unscaledTime < _toastUntil)
            {
                float width = Mathf.Min(650f, Screen.width - 40f);
                GUI.color = new Color(0.01f, 0.04f, 0.065f, 0.94f);
                GUI.Box(new Rect((Screen.width - width) * 0.5f, 92f, width, 34f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect((Screen.width - width) * 0.5f + 12f, 100f, width - 24f, 20f), _toast, _title);
            }
        }

        private static void DrawBar(Rect rect, float ratio, Color color)
        {
            ratio = Mathf.Clamp01(ratio);
            Color previous = GUI.color;
            GUI.color = new Color(0.035f, 0.05f, 0.07f, 0.96f);
            GUI.Box(rect, string.Empty);
            GUI.color = color;
            GUI.Box(new Rect(rect.x + 1f, rect.y + 1f, (rect.width - 2f) * ratio, rect.height - 2f), string.Empty);
            GUI.color = previous;
        }
    }
}
