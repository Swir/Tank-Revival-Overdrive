using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v5.2 authored audio overlay. Uses real imported WAV resources for selected high-value cues,
    /// while keeping the existing BattleAudio/EnhancedBattleAudioDirector as fallback and ambience.
    /// The overlay is budget-aware and never changes gameplay state.
    /// </summary>
    [DefaultExecutionOrder(9150)]
    public sealed class ProductionBattleAudioDirector : MonoBehaviour
    {
        public static ProductionBattleAudioDirector Instance { get; private set; }

        private AudioClip _heavyCannon;
        private AudioClip _bossAlarm;
        private AudioSource[] _voices;
        private int _voiceCursor;
        private float _nextBossAlertAllowed;

        public bool HeavyCannonLoaded => _heavyCannon != null;
        public bool BossAlarmLoaded => _bossAlarm != null;
        public int LoadedAuthoredClipCount => (_heavyCannon != null ? 1 : 0) + (_bossAlarm != null ? 1 : 0);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ProductionBattleAudioDirector>() != null) return;
            GameObject go = new GameObject("ProductionBattleAudioDirector_v5_2");
            DontDestroyOnLoad(go);
            go.AddComponent<ProductionBattleAudioDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _heavyCannon = Resources.Load<AudioClip>("TankRevivalProduction/Audio/HeavyCannon");
            _bossAlarm = Resources.Load<AudioClip>("TankRevivalProduction/Audio/BossAlarm");

            _voices = new AudioSource[6];
            for (int i = 0; i < _voices.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.volume = 1f;
                _voices[i] = source;
            }
        }

        private void OnEnable()
        {
            Projectile.ShotSpawned3D -= OnShot;
            Projectile.ShotSpawned3D += OnShot;
            Projectile.DamageResolved -= OnDamageResolved;
            Projectile.DamageResolved += OnDamageResolved;
        }

        private void OnDisable()
        {
            Projectile.ShotSpawned3D -= OnShot;
            Projectile.DamageResolved -= OnDamageResolved;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnShot(Projectile projectile, Vector3 position, Vector2 direction, Team team, AmmoType ammo)
        {
            if (_heavyCannon == null) return;
            if (ammo != AmmoType.Explosive && ammo != AmmoType.ArmorPiercing) return;

            WarfarePerformanceGovernor.BudgetTier tier = WarfarePerformanceGovernor.Tier;
            if (tier == WarfarePerformanceGovernor.BudgetTier.Survival && team != Team.Player) return;

            float volume = team == Team.Player ? 0.38f : 0.20f;
            if (tier == WarfarePerformanceGovernor.BudgetTier.Balanced) volume *= 0.82f;
            if (tier == WarfarePerformanceGovernor.BudgetTier.Survival) volume *= 0.65f;
            PlayAt(_heavyCannon, position, volume, 0.965f, 1.035f);
        }

        private void OnDamageResolved(Projectile projectile, Health target, int amount, bool killed)
        {
            if (!killed || target == null || _bossAlarm == null) return;
            EnemyTank enemy = target.GetComponent<EnemyTank>();
            if (enemy == null || enemy.Kind != EnemyKind.Boss) return;
            if (Time.unscaledTime < _nextBossAlertAllowed) return;
            _nextBossAlertAllowed = Time.unscaledTime + 2.5f;
            PlayAt(_bossAlarm, target.transform.position, 0.52f, 0.98f, 1.02f);
        }

        private void PlayAt(AudioClip clip, Vector3 worldPosition, float volume, float minPitch, float maxPitch)
        {
            if (clip == null || _voices == null || _voices.Length == 0) return;
            AudioSource voice = _voices[_voiceCursor++ % _voices.Length];
            if (voice == null) return;

            float attenuation = 1f;
            float pan = 0f;
            PlayerTank player = CombatRoster.Player;
            if (player != null)
            {
                Vector3 delta = worldPosition - player.transform.position;
                attenuation = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(delta.magnitude / 16f));
                pan = Mathf.Clamp(delta.x / 9f, -0.65f, 0.65f);
            }

            voice.panStereo = pan;
            voice.pitch = Random.Range(minPitch, maxPitch);
            voice.PlayOneShot(clip, Mathf.Clamp01(volume * attenuation));
        }
    }
}
