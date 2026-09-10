using UnityEngine;

namespace TankRevival
{
    public sealed class DefenseTurret : MonoBehaviour
    {
        private TankGame _game;
        private int _level;
        private int _index;
        private Transform _pivot;
        private Transform _barrel;
        private float _nextThink;
        private float _nextShot;
        private EnemyTank _target;

        public void Initialize(TankGame game, int level, int index)
        {
            _game = game;
            _level = Mathf.Clamp(level, 1, 3);
            _index = index;
            BuildVisuals();
            _nextThink = Time.time + Random.Range(0.05f, 0.18f);
            _nextShot = Time.time + Random.Range(0.25f, 0.65f);
        }

        private void BuildVisuals()
        {
            Color dark = new Color(0.055f, 0.075f, 0.095f);
            Color steel = new Color(0.20f, 0.32f, 0.42f);
            Color accent = _level >= 3 ? new Color(0.30f, 0.95f, 1f) : new Color(0.20f, 0.72f, 1f);

            VisualFactory.Disc("SentryShadow", transform, new Vector2(0.88f, 0.68f), new Color(0f, 0f, 0f, 0.34f), new Vector3(0.05f, -0.05f, 0f), 2);
            VisualFactory.Rect("SentryBase", transform, new Vector2(0.68f, 0.58f), dark, Vector3.zero, 4);
            VisualFactory.Rect("SentryArmor", transform, new Vector2(0.58f, 0.49f), steel, new Vector3(0f, 0.02f, 0f), 5);
            VisualFactory.Rect("SentryEdge", transform, new Vector2(0.50f, 0.055f), new Color(0.62f, 0.76f, 0.86f), new Vector3(0f, 0.20f, 0f), 6);

            for (int i = -1; i <= 1; i++)
                VisualFactory.Disc("SentryBolt" + i, transform, new Vector2(0.055f, 0.055f), new Color(0.82f, 0.88f, 0.94f), new Vector3(i * 0.18f, -0.18f, 0f), 7);

            var pivotGo = new GameObject("SentryPivot");
            pivotGo.transform.SetParent(transform, false);
            _pivot = pivotGo.transform;

            VisualFactory.Disc("TurretRing", _pivot, new Vector2(0.45f, 0.45f), dark, Vector3.zero, 8);
            VisualFactory.Disc("TurretHead", _pivot, new Vector2(0.35f, 0.35f), accent, Vector3.zero, 9);
            VisualFactory.Disc("OpticGlow", _pivot, new Vector2(0.12f, 0.12f), new Color(accent.r, accent.g, accent.b, 0.22f), new Vector3(0f, 0.09f, 0f), 10);
            VisualFactory.Disc("Optic", _pivot, new Vector2(0.065f, 0.065f), Color.white, new Vector3(0f, 0.09f, 0f), 11);

            var barrelGo = VisualFactory.Rect("SentryBarrel", _pivot, new Vector2(_level >= 3 ? 0.095f : 0.08f, 0.56f), Color.Lerp(accent, Color.black, 0.35f), new Vector3(0f, 0.38f, 0f), 10);
            _barrel = barrelGo.transform;
            VisualFactory.Rect("BarrelHighlight", _pivot, new Vector2(0.022f, 0.46f), new Color(1f, 1f, 1f, 0.28f), new Vector3(-0.024f, 0.38f, 0f), 11);

            if (_level >= 2)
            {
                VisualFactory.RingObject("SentryRangeAura", transform, new Vector2(0.90f, 0.90f), new Color(accent.r, accent.g, accent.b, 0.12f), Vector3.zero, 3)
                    .AddComponent<DoctrineAuraPulse>();
            }
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying) return;

            if (Time.time >= _nextThink)
            {
                _target = FindBestTarget();
                _nextThink = Time.time + Mathf.Lerp(0.22f, 0.10f, (_level - 1) / 2f);
            }

            if (_target == null || _target.Health == null || _target.Health.IsDead) return;

            Vector2 direction = ((Vector2)_target.transform.position - (Vector2)transform.position).normalized;
            if (direction.sqrMagnitude < 0.01f) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            _pivot.rotation = Quaternion.Euler(0f, 0f, angle);

            if (Time.time >= _nextShot)
            {
                Fire(direction);
                float delay = _level == 1 ? 1.15f : _level == 2 ? 0.88f : 0.68f;
                _nextShot = Time.time + delay + Random.Range(-0.06f, 0.08f);
            }
        }

        private EnemyTank FindBestTarget()
        {
            var enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            EnemyTank best = null;
            float bestScore = float.MaxValue;
            float range = 6.3f + _level * 1.0f;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;

                float distance = Vector2.Distance(transform.position, enemy.transform.position);
                if (distance > range) continue;

                float priority = distance;
                if (enemy.Kind == EnemyKind.Siege) priority -= 2.2f;
                if (enemy.Kind == EnemyKind.Elite) priority -= 1.5f;
                if (enemy.Kind == EnemyKind.Boss) priority -= 3.0f;
                if (enemy.Kind == EnemyKind.Supply) priority -= 0.8f;

                if (priority < bestScore)
                {
                    bestScore = priority;
                    best = enemy;
                }
            }

            return best;
        }

        private void Fire(Vector2 direction)
        {
            Vector2 muzzle = (Vector2)transform.position + direction * 0.70f;
            int damage = _level >= 3 ? 2 : 1;
            float speed = 11.8f + _level * 0.9f;
            Color c = _level >= 3 ? new Color(0.32f, 1f, 1f) : new Color(0.20f, 0.78f, 1f);
            AmmoType ammo = _level >= 3 ? AmmoType.ArmorPiercing : AmmoType.Basic;

            _game.SpawnProjectile(muzzle, direction, Team.Player, damage, speed, c, ammo);
            VisualFactory.MuzzleFlash(muzzle, c, 0.55f + _level * 0.08f);
            BattleAudio.PlayGlobal(_level >= 3 ? SoundCue.HeavyShot : SoundCue.PlayerShot, 0.11f, 0.08f);

            if (_barrel != null)
            {
                Vector3 p = _barrel.localPosition;
                p.y = 0.34f;
                _barrel.localPosition = p;
            }
        }

        private void LateUpdate()
        {
            if (_barrel == null) return;
            Vector3 p = _barrel.localPosition;
            p.y = Mathf.Lerp(p.y, 0.38f, 12f * Time.unscaledDeltaTime);
            _barrel.localPosition = p;
        }
    }
}
