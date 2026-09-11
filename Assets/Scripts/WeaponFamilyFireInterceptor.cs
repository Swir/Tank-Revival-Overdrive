using System.Collections;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Routes the player's unlimited Basic trigger through the selected v3.2 primary family.
    /// Special ammo remains untouched. Replacement shells still use TankGame.SpawnProjectile.
    /// </summary>
    public sealed class WeaponFamilyFireInterceptor : MonoBehaviour
    {
        private TankGame _game;
        private PlayerTank _player;
        private bool _replacementInProgress;
        private float _nextFamilyShot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<WeaponFamilyFireInterceptor>() != null) return;
            var go = new GameObject("WeaponFamilyFireInterceptor");
            DontDestroyOnLoad(go);
            go.AddComponent<WeaponFamilyFireInterceptor>();
        }

        private void Awake()
        {
            Projectile.ShotSpawned3D += OnShotSpawned;
        }

        private void OnDestroy()
        {
            Projectile.ShotSpawned3D -= OnShotSpawned;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) _player = null;
            else if (_player == null) _player = FindAnyObjectByType<PlayerTank>();
        }

        private void OnShotSpawned(Projectile projectile, Vector3 position, Vector2 direction, Team team, AmmoType ammo)
        {
            if (_replacementInProgress || projectile == null || team != Team.Player || ammo != AmmoType.Basic) return;
            if (_game == null || !_game.IsPlaying) return;
            if (_player == null) _player = FindAnyObjectByType<PlayerTank>();
            if (_player == null || Vector2.Distance(position, _player.transform.position) > 1.6f) return;

            WeaponFamilyDirector director = WeaponFamilyDirector.Instance;
            if (director == null) return;

            // Remove the generic Basic shell before physics can advance it. The replacement shot(s)
            // are spawned synchronously through the normal projectile pipeline below.
            projectile.gameObject.SetActive(false);
            Destroy(projectile.gameObject);

            float now = Time.time;
            if (now < _nextFamilyShot) return;

            Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
            float originalSpeed = body != null ? Mathf.Max(6f, body.linearVelocity.magnitude) : 10.5f;
            Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float baseDelay = _player.EffectiveFireDelay;
            float familyDelay = baseDelay * director.ReloadMultiplier(AmmoType.Basic);
            PrimaryWeaponFamily family = director.Family;

            if (family == PrimaryWeaponFamily.Autocannon)
                _nextFamilyShot = now + Mathf.Max(0.11f, familyDelay);
            else if (family == PrimaryWeaponFamily.PlasmaRepeater)
                _nextFamilyShot = now + Mathf.Max(0.16f, familyDelay);
            else
                _nextFamilyShot = now + Mathf.Max(baseDelay, familyDelay);

            FireReplacement(position, dir, side, projectile.Damage, originalSpeed);

            // Fast weapon families use real delayed follow-up projectiles instead of pretending
            // the player's legacy 0.34 s trigger magically became faster.
            if (family == PrimaryWeaponFamily.Autocannon)
                StartCoroutine(FollowUpBurst(position, dir, side, projectile.Damage, originalSpeed, 0.105f));
            else if (family == PrimaryWeaponFamily.PlasmaRepeater && director.MasteryLevel >= 2)
                StartCoroutine(FollowUpBurst(position, dir, side, projectile.Damage, originalSpeed, 0.145f));
        }

        private IEnumerator FollowUpBurst(Vector2 muzzle, Vector2 direction, Vector2 side, int damage, float speed, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_game == null || !_game.IsPlaying || _player == null) yield break;
            WeaponFamilyDirector director = WeaponFamilyDirector.Instance;
            if (director == null || director.Family == PrimaryWeaponFamily.HeavyCannon || director.Family == PrimaryWeaponFamily.Railgun) yield break;
            FireReplacement(muzzle, direction, side, damage, speed);
        }

        private void FireReplacement(Vector2 muzzle, Vector2 direction, Vector2 side, int damage, float speed)
        {
            WeaponFamilyDirector director = WeaponFamilyDirector.Instance;
            if (director == null || _game == null || _player == null) return;
            _replacementInProgress = true;
            director.TryFirePrimary(_game, _player, AmmoType.Basic, muzzle, direction, side, damage, speed, Color.white);
            _replacementInProgress = false;
        }
    }
}
