using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v1.3 STEEL DOCTRINES
    /// Gives every War Garage chassis a distinct active combat ability on R.
    /// Abilities use existing projectile, armor and fortress systems so they remain integrated
    /// with the rest of the campaign instead of becoming a detached minigame layer.
    /// </summary>
    public sealed class SteelDoctrineDirector : MonoBehaviour
    {
        private enum Chassis
        {
            Assault = 0,
            Bastion = 1,
            Scout = 2,
            Hunter = 3
        }

        private static readonly string[] ChassisNames =
        {
            "ORZEL MK-I ASSAULT",
            "BASTION HEAVY",
            "WICHER SCOUT",
            "HUNTER TD"
        };

        private static readonly string[] AbilityNames =
        {
            "SHOCK BARRAGE",
            "EAGLE AEGIS",
            "WICHER OVERBOOST",
            "RAIL LANCE"
        };

        private TankGame _game;
        private PlayerTank _player;
        private float _readyAt;
        private string _banner = string.Empty;
        private float _bannerUntil;

        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _ready;
        private GUIStyle _cooldown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<SteelDoctrineDirector>() != null) return;
            var go = new GameObject("SteelDoctrineDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<SteelDoctrineDirector>();
        }

        private Chassis SelectedChassis => (Chassis)Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.Garage.Chassis", 0), 0, 3);

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
                return;
            }

            if (_player == null)
                _player = FindAnyObjectByType<PlayerTank>();

            if (_player == null || _player.Health == null || _player.Health.IsDead) return;

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (Time.time < _readyAt)
                {
                    Announce($"{AbilityNames[(int)SelectedChassis]} // RECHARGING {Mathf.CeilToInt(_readyAt - Time.time)}s");
                    return;
                }

                ActivateDoctrine(SelectedChassis);
            }
        }

        private void ActivateDoctrine(Chassis chassis)
        {
            switch (chassis)
            {
                case Chassis.Assault:
                    ShockBarrage();
                    _readyAt = Time.time + 15f;
                    break;
                case Chassis.Bastion:
                    EagleAegis();
                    _readyAt = Time.time + 22f;
                    break;
                case Chassis.Scout:
                    WicherOverboost();
                    _readyAt = Time.time + 11f;
                    break;
                case Chassis.Hunter:
                    RailLance();
                    _readyAt = Time.time + 18f;
                    break;
            }
        }

        private Vector2 AimDirection()
        {
            Vector2 fallback = Vector2.up;
            if (_player == null) return fallback;

            Camera camera = Camera.main;
            if (camera == null) return fallback;

            Vector3 mouse = camera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 delta = (Vector2)mouse - (Vector2)_player.transform.position;
            return delta.sqrMagnitude > 0.01f ? delta.normalized : fallback;
        }

        private void ShockBarrage()
        {
            Vector2 direction = AimDirection();
            Vector2 origin = _player.transform.position;
            int damage = Mathf.Clamp(_player.EffectiveShotDamage + 2, 3, 9);

            for (int i = -3; i <= 3; i++)
            {
                Vector2 shot = Rotate(direction, i * 5.5f);
                Vector2 muzzle = origin + shot * 0.88f;
                _game.SpawnProjectile(muzzle, shot, Team.Player, damage, 13.8f, new Color(0.32f, 0.92f, 1f), AmmoType.ArmorPiercing);
            }

            VisualFactory.RingPulse(origin, new Color(0.22f, 0.86f, 1f), 1.35f);
            VisualFactory.MuzzleFlash(origin + direction * 0.90f, new Color(0.42f, 0.94f, 1f), 1.55f);
            _game.KickCamera(0.18f, 0.11f);
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.78f, 0.02f);
            Announce("STEEL DOCTRINE // SHOCK BARRAGE");
        }

        private void EagleAegis()
        {
            _player.Health.InvulnerableUntil = Mathf.Max(_player.Health.InvulnerableUntil, Time.time + 4.2f);
            _player.Health.Heal(1);

            EagleFortressDirector fortress = EagleFortressDirector.Instance;
            if (fortress != null)
            {
                fortress.ActivateEmergencyShield(3.8f);
                fortress.RepairFortress(1);
            }

            VisualFactory.RingPulse(_player.transform.position, new Color(0.24f, 0.82f, 1f), 1.65f);
            VisualFactory.RingPulse(_game.BasePosition, new Color(0.32f, 0.96f, 1f), 2.25f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.72f, 0f);
            Announce("STEEL DOCTRINE // EAGLE AEGIS");
        }

        private void WicherOverboost()
        {
            Vector2 direction = AimDirection();
            Vector2 start = _player.transform.position;
            Vector2 destination = start + direction * 3.25f;
            destination.x = Mathf.Clamp(destination.x, -10.7f, 10.7f);
            destination.y = Mathf.Clamp(destination.y, -5.25f, 5.55f);

            VisualFactory.RingPulse(start, new Color(0.20f, 0.72f, 1f), 0.95f);
            _player.transform.position = destination;
            _player.Health.InvulnerableUntil = Mathf.Max(_player.Health.InvulnerableUntil, Time.time + 0.65f);
            VisualFactory.RingPulse(destination, new Color(0.30f, 1f, 0.78f), 1.15f);

            for (int i = 0; i < 10; i++)
            {
                float angle = i * 36f * Mathf.Deg2Rad;
                Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                _game.SpawnProjectile(destination + radial * 0.52f, radial, Team.Player, 1, 10.8f, new Color(0.30f, 0.92f, 1f), AmmoType.EMP);
            }

            _game.KickCamera(0.11f, 0.065f);
            BattleAudio.PlayGlobal(SoundCue.Plasma, 0.54f, 0.04f);
            Announce("STEEL DOCTRINE // WICHER OVERBOOST");
        }

        private void RailLance()
        {
            Vector2 direction = AimDirection();
            Vector2 origin = _player.transform.position;
            Vector2 muzzle = origin + direction * 0.92f;
            int damage = Mathf.Clamp(_player.EffectiveShotDamage + 5, 6, 12);

            _game.SpawnProjectile(muzzle, direction, Team.Player, damage, 19.5f, new Color(0.76f, 0.96f, 1f), AmmoType.Plasma);
            _game.SpawnProjectile(muzzle + Rotate(direction, 90f) * 0.10f, Rotate(direction, 1.6f), Team.Player, Mathf.Max(3, damage - 2), 18.2f, new Color(0.44f, 0.88f, 1f), AmmoType.ArmorPiercing);
            _game.SpawnProjectile(muzzle - Rotate(direction, 90f) * 0.10f, Rotate(direction, -1.6f), Team.Player, Mathf.Max(3, damage - 2), 18.2f, new Color(0.44f, 0.88f, 1f), AmmoType.ArmorPiercing);

            VisualFactory.MuzzleFlash(muzzle, Color.white, 1.75f);
            VisualFactory.RingPulse(origin, new Color(0.60f, 0.92f, 1f), 1.25f);
            _game.KickCamera(0.20f, 0.14f);
            BattleAudio.PlayGlobal(SoundCue.Plasma, 0.88f, 0f);
            Announce("STEEL DOCTRINE // RAIL LANCE");
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos).normalized;
        }

        private void Announce(string text)
        {
            _banner = text;
            _bannerUntil = Time.unscaledTime + 2.0f;
        }

        private float CooldownFor(Chassis chassis)
        {
            switch (chassis)
            {
                case Chassis.Bastion: return 22f;
                case Chassis.Scout: return 11f;
                case Chassis.Hunter: return 18f;
                default: return 15f;
            }
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.42f, 0.94f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.86f, 0.92f, 1f) } };
            _ready = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.38f, 1f, 0.56f) } };
            _cooldown = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.72f, 0.22f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            Chassis chassis = SelectedChassis;
            float remain = Mathf.Max(0f, _readyAt - Time.time);
            bool ready = remain <= 0.01f;
            float x = Screen.width * 0.5f - 205f;
            float y = 12f;

            GUI.color = new Color(0.018f, 0.032f, 0.052f, 0.93f);
            GUI.Box(new Rect(x, y, 410f, 62f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 7f, 386f, 19f), $"STEEL DOCTRINE // {ChassisNames[(int)chassis]}", _header);
            GUI.Label(new Rect(x + 12f, y + 29f, 270f, 18f), $"R  {AbilityNames[(int)chassis]}", _body);
            GUI.Label(new Rect(x + 282f, y + 29f, 112f, 18f), ready ? "READY" : $"{remain:0.0}s", ready ? _ready : _cooldown);
            GUI.Label(new Rect(x + 12f, y + 45f, 386f, 14f), $"Doctrine cooldown {CooldownFor(chassis):0}s", _body);

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.02f, 0.08f, 0.11f, 0.94f);
                GUI.Box(new Rect(Screen.width * 0.5f - 260f, Screen.height * 0.24f, 520f, 36f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 245f, Screen.height * 0.24f + 8f, 490f, 20f), _banner, _ready);
            }
        }
    }
}
