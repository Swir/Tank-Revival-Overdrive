using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v4.4 presentation layer. Adds caliber-aware camera trauma, player hit confirmation,
    /// damage pressure, kill confirmation and critical-state screen language without taking
    /// authority away from Projectile, Health, ArmorSystem or TankGame.
    /// </summary>
    [DefaultExecutionOrder(9000)]
    public sealed class CombatPresentationReforgeDirector : MonoBehaviour
    {
        private readonly HashSet<Health> _observed = new HashSet<Health>();
        private TankGame _game;
        private CameraTraumaLayer _cameraTrauma;
        private float _scanAt;
        private float _hitMarker;
        private float _killMarker;
        private float _damageFlash;
        private float _eagleFlash;
        private float _criticalPulse;
        private float _comboHold;
        private int _confirmedCombo;
        private GUIStyle _hitStyle;
        private GUIStyle _killStyle;
        private GUIStyle _criticalStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CombatPresentationReforgeDirector>() != null) return;
            var go = new GameObject("CombatPresentationReforgeDirector_v4_4");
            DontDestroyOnLoad(go);
            go.AddComponent<CombatPresentationReforgeDirector>();
        }

        private void OnEnable()
        {
            Projectile.ShotSpawned3D -= OnShot;
            Projectile.ShotSpawned3D += OnShot;
            Projectile.Impact3D -= OnImpact;
            Projectile.Impact3D += OnImpact;
            Projectile.DamageResolved -= OnDamageResolved;
            Projectile.DamageResolved += OnDamageResolved;
        }

        private void OnDisable()
        {
            Projectile.ShotSpawned3D -= OnShot;
            Projectile.Impact3D -= OnImpact;
            Projectile.DamageResolved -= OnDamageResolved;

            foreach (Health health in _observed)
            {
                if (health == null) continue;
                health.Damaged -= OnHealthDamaged;
                health.Died -= OnHealthDied;
            }
            _observed.Clear();
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            EnsureCameraLayer();

            float dt = Time.unscaledDeltaTime;
            _hitMarker = Mathf.MoveTowards(_hitMarker, 0f, dt * 4.8f);
            _killMarker = Mathf.MoveTowards(_killMarker, 0f, dt * 2.6f);
            _damageFlash = Mathf.MoveTowards(_damageFlash, 0f, dt * 2.9f);
            _eagleFlash = Mathf.MoveTowards(_eagleFlash, 0f, dt * 2.1f);

            if (_comboHold > 0f)
            {
                _comboHold -= dt;
                if (_comboHold <= 0f) _confirmedCombo = 0;
            }

            if (Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.25f;
                HookHealth();
                UpdateCriticalState();
            }
        }

        private void EnsureCameraLayer()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_cameraTrauma != null && _cameraTrauma.gameObject == cam.gameObject) return;
            _cameraTrauma = cam.GetComponent<CameraTraumaLayer>();
            if (_cameraTrauma == null) _cameraTrauma = cam.gameObject.AddComponent<CameraTraumaLayer>();
        }

        private void HookHealth()
        {
            Health[] all = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Health health = all[i];
                if (health == null || !_observed.Add(health)) continue;
                health.Damaged -= OnHealthDamaged;
                health.Damaged += OnHealthDamaged;
                health.Died -= OnHealthDied;
                health.Died += OnHealthDied;
            }
            _observed.RemoveWhere(h => h == null);
        }

        private void OnShot(Projectile projectile, Vector3 position, Vector2 direction, Team team, AmmoType ammo)
        {
            if (_cameraTrauma == null || team != Team.Player) return;
            float trauma = ShotTrauma(ammo);
            _cameraTrauma.AddImpulse(trauma, direction * -0.06f * trauma, trauma * 0.35f);
        }

        private void OnImpact(Projectile projectile, Vector3 position, Team team, AmmoType ammo, bool explosive, bool ricochet)
        {
            if (_cameraTrauma == null) return;
            Vector3 playerPos = CombatRoster.Player != null ? CombatRoster.Player.transform.position : Vector3.zero;
            float distance = Vector2.Distance(new Vector2(position.x, position.y), new Vector2(playerPos.x, playerPos.y));
            float proximity = Mathf.Clamp01(1f - distance / 9f);
            float force = explosive ? 0.54f : ricochet ? 0.16f : 0.12f;
            if (ammo == AmmoType.Plasma) force += 0.10f;
            if (ammo == AmmoType.ArmorPiercing) force += 0.05f;
            _cameraTrauma.AddImpulse(force * proximity, Vector2.zero, force * proximity * 1.2f);
        }

        private void OnDamageResolved(Projectile projectile, Health target, int damage, bool killed)
        {
            if (projectile == null || target == null || projectile.OwnerTeam != Team.Player || target.Team == Team.Player) return;

            _hitMarker = Mathf.Clamp01(0.52f + damage * 0.12f);
            if (killed)
            {
                _killMarker = 1f;
                _confirmedCombo++;
                _comboHold = 2.2f;
                _cameraTrauma?.AddImpulse(0.10f, Vector2.zero, 0.09f);
                EnhancedBattleAudioDirector.PlayConfirmation(true, Mathf.Clamp01(0.55f + _confirmedCombo * 0.04f));
            }
            else
            {
                EnhancedBattleAudioDirector.PlayConfirmation(false, 0.38f);
            }
        }

        private void OnHealthDamaged(Health health, int amount)
        {
            if (health == null || amount <= 0) return;
            if (health.GetComponent<PlayerTank>() != null)
            {
                float ratio = health.Max > 0 ? amount / (float)health.Max : 0.1f;
                _damageFlash = Mathf.Clamp01(_damageFlash + 0.36f + ratio * 1.8f);
                _cameraTrauma?.AddImpulse(0.34f + ratio * 1.8f, Random.insideUnitCircle * 0.08f, 0.55f);
                EnhancedBattleAudioDirector.PlayArmorStress(Mathf.Clamp01(0.45f + ratio * 2f));
                return;
            }

            if (health == CombatRoster.Eagle || health.gameObject.name.Contains("ORZELEK"))
            {
                _eagleFlash = Mathf.Clamp01(_eagleFlash + 0.42f);
                _cameraTrauma?.AddImpulse(0.24f, Vector2.zero, 0.32f);
            }
        }

        private void OnHealthDied(Health health)
        {
            if (health == null || _cameraTrauma == null) return;
            EnemyTank enemy = health.GetComponent<EnemyTank>();
            if (enemy != null)
            {
                float force = enemy.Kind == EnemyKind.Boss ? 0.92f :
                              enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege ? 0.58f : 0.24f;
                _cameraTrauma.AddImpulse(force, Random.insideUnitCircle * force * 0.035f, force * 1.15f);
            }
            else if (health.GetComponent<PlayerTank>() != null || health == CombatRoster.Eagle)
            {
                _cameraTrauma.AddImpulse(1f, Random.insideUnitCircle * 0.10f, 1.25f);
            }
        }

        private void UpdateCriticalState()
        {
            float pressure = 0f;
            PlayerTank player = CombatRoster.Player;
            if (player != null && player.Health != null && player.Health.Max > 0)
                pressure = Mathf.Max(pressure, 1f - player.Health.Current / (float)player.Health.Max);

            Health eagle = CombatRoster.Eagle;
            if (eagle != null && eagle.Max > 0)
                pressure = Mathf.Max(pressure, (1f - eagle.Current / (float)eagle.Max) * 0.92f);

            _criticalPulse = Mathf.InverseLerp(0.56f, 0.86f, pressure);
            EnhancedBattleAudioDirector.SetDangerPressure(_criticalPulse);
        }

        private static float ShotTrauma(AmmoType ammo)
        {
            switch (ammo)
            {
                case AmmoType.Explosive: return 0.25f;
                case AmmoType.ArmorPiercing: return 0.20f;
                case AmmoType.Plasma: return 0.18f;
                case AmmoType.EMP: return 0.12f;
                case AmmoType.Incendiary: return 0.14f;
                default: return 0.09f;
            }
        }

        private void EnsureStyles()
        {
            if (_hitStyle != null) return;
            _hitStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
            _killStyle = new GUIStyle(_hitStyle) { fontSize = 15 };
            _criticalStyle = new GUIStyle(_hitStyle) { fontSize = 14 };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            float w = Screen.width;
            float h = Screen.height;
            float cx = w * 0.5f;
            float cy = h * 0.5f;

            if (_damageFlash > 0.01f)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 0.035f, 0.015f, _damageFlash * 0.16f);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
                GUI.color = old;
            }

            if (_eagleFlash > 0.01f)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 0.40f, 0.02f, _eagleFlash * 0.075f);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
                GUI.color = old;
            }

            if (_criticalPulse > 0.01f)
            {
                float pulse = 0.55f + Mathf.Sin(Time.unscaledTime * 5.2f) * 0.45f;
                Color old = GUI.color;
                GUI.color = new Color(0.72f, 0.01f, 0.005f, _criticalPulse * pulse * 0.11f);
                float band = Mathf.Lerp(16f, 54f, _criticalPulse);
                GUI.DrawTexture(new Rect(0f, 0f, w, band), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, h - band, w, band), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, 0f, band, h), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(w - band, 0f, band, h), Texture2D.whiteTexture);
                GUI.color = old;
            }

            if (_hitMarker > 0.01f)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, _hitMarker);
                float s = 25f + (1f - _hitMarker) * 8f;
                _hitStyle.fontSize = Mathf.RoundToInt(s);
                GUI.Label(new Rect(cx - 32f, cy - 27f, 64f, 54f), "×", _hitStyle);
                GUI.color = old;
            }

            if (_killMarker > 0.01f)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 0.76f, 0.18f, _killMarker);
                string text = _confirmedCombo > 1 ? $"KILL CONFIRMED  x{_confirmedCombo}" : "KILL CONFIRMED";
                GUI.Label(new Rect(cx - 120f, cy + 28f, 240f, 30f), text, _killStyle);
                GUI.color = old;
            }

            if (_criticalPulse > 0.64f)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 0.32f, 0.16f, 0.62f + Mathf.Sin(Time.unscaledTime * 5.2f) * 0.18f);
                GUI.Label(new Rect(cx - 170f, h - 82f, 340f, 28f), "CRITICAL ARMOR STATE", _criticalStyle);
                GUI.color = old;
            }
        }
    }

    /// <summary>
    /// Non-authoritative camera layer. Removes its own previous-frame offset before applying
    /// new trauma so it can coexist with the campaign camera director instead of accumulating drift.
    /// </summary>
    public sealed class CameraTraumaLayer : MonoBehaviour
    {
        private float _trauma;
        private float _rotationTrauma;
        private Vector2 _kick;
        private Vector3 _lastOffset;
        private float _lastRotation;
        private float _seed;

        private void Awake()
        {
            _seed = Random.Range(10f, 500f);
        }

        public void AddImpulse(float trauma, Vector2 kick, float rotation)
        {
            _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, trauma));
            _rotationTrauma = Mathf.Clamp(_rotationTrauma + Mathf.Max(0f, rotation), 0f, 1.4f);
            _kick += Vector2.ClampMagnitude(kick, 0.16f);
        }

        private void LateUpdate()
        {
            transform.position -= _lastOffset;
            transform.rotation *= Quaternion.Euler(0f, 0f, -_lastRotation);

            float dt = Time.unscaledDeltaTime;
            _trauma = Mathf.MoveTowards(_trauma, 0f, dt * 1.75f);
            _rotationTrauma = Mathf.MoveTowards(_rotationTrauma, 0f, dt * 2.3f);
            _kick = Vector2.Lerp(_kick, Vector2.zero, 1f - Mathf.Exp(-dt * 13f));

            float squared = _trauma * _trauma;
            float nx = Mathf.PerlinNoise(_seed, Time.unscaledTime * 29f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(_seed + 17f, Time.unscaledTime * 31f) * 2f - 1f;
            _lastOffset = new Vector3(nx * squared * 0.065f + _kick.x, ny * squared * 0.055f + _kick.y, 0f);
            _lastRotation = (Mathf.PerlinNoise(_seed + 41f, Time.unscaledTime * 24f) * 2f - 1f) * _rotationTrauma * 0.55f;

            transform.position += _lastOffset;
            transform.rotation *= Quaternion.Euler(0f, 0f, _lastRotation);
        }

        private void OnDisable()
        {
            transform.position -= _lastOffset;
            transform.rotation *= Quaternion.Euler(0f, 0f, -_lastRotation);
            _lastOffset = Vector3.zero;
            _lastRotation = 0f;
        }
    }
}
