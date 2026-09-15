using UnityEngine;

namespace TankRevival
{
    public enum GunneryDoctrine { Standard, HunterKiller, CounterFire, EagleBreach }

    [DefaultExecutionOrder(22400)]
    public sealed class AdvancedGunneryDoctrineDirector : MonoBehaviour
    {
        public const float DoctrineRefreshSeconds = 1.0f;
        public const float CounterFireMemorySeconds = 5.5f;
        public const float MaxAimBiasDegrees = 3.0f;
        private static AdvancedGunneryDoctrineDirector _instance;
        private TankGame _game; private GunneryDoctrine _doctrine; private float _nextRefresh;
        public static AdvancedGunneryDoctrineDirector Instance => _instance;
        public GunneryDoctrine ActiveDoctrine => _doctrine;
        public static bool ConfigurationValid => DoctrineRefreshSeconds >= .75f && CounterFireMemorySeconds <= 6f && MaxAimBiasDegrees <= 3f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install(){if(FindAnyObjectByType<AdvancedGunneryDoctrineDirector>()==null){var go=new GameObject("AdvancedGunneryDoctrineDirector_v11_3");DontDestroyOnLoad(go);go.AddComponent<AdvancedGunneryDoctrineDirector>();}}
        private void Awake(){if(_instance!=null&&_instance!=this){Destroy(gameObject);return;}_instance=this;DontDestroyOnLoad(gameObject);}
        private void OnDestroy(){if(_instance==this)_instance=null;}
        private void Update(){if(_game==null)_game=FindAnyObjectByType<TankGame>();if(_game==null||!_game.IsPlaying)return;if(Time.time<_nextRefresh)return;_nextRefresh=Time.time+DoctrineRefreshSeconds;_doctrine=Resolve(_game.CurrentRound);}
        private static GunneryDoctrine Resolve(int round){var hc=AdaptiveEnemyHighCommandDirector.Instance;if(hc!=null){switch(hc.ActiveCounterDoctrine){case EnemyCounterDoctrine.ArmorTrap:return GunneryDoctrine.HunterKiller;case EnemyCounterDoctrine.SiegeBreach:return GunneryDoctrine.EagleBreach;case EnemyCounterDoctrine.DispersedLogistics:return GunneryDoctrine.CounterFire;}}return round>=70?GunneryDoctrine.CounterFire:round>=35?GunneryDoctrine.HunterKiller:GunneryDoctrine.Standard;}
        public static bool PreferPlayer(EnemyKind kind,int round){var d=_instance!=null?_instance._doctrine:Resolve(round);if(kind==EnemyKind.Siege)return d==GunneryDoctrine.CounterFire;if(kind==EnemyKind.Sniper||kind==EnemyKind.Elite)return true;if(kind==EnemyKind.Heavy)return d==GunneryDoctrine.HunterKiller||d==GunneryDoctrine.CounterFire;return false;}
        public static float SpreadMultiplier(EnemyKind kind,int round){var d=_instance!=null?_instance._doctrine:Resolve(round);if(d==GunneryDoctrine.HunterKiller&&(kind==EnemyKind.Sniper||kind==EnemyKind.Elite))return .82f;if(d==GunneryDoctrine.CounterFire&&(kind==EnemyKind.Siege||kind==EnemyKind.Heavy))return .88f;if(d==GunneryDoctrine.EagleBreach&&kind==EnemyKind.Siege)return .84f;return 1f;}
        public static float VolleyPhaseBias(EnemyKind kind,int round){if(round<45)return 0f;var d=_instance!=null?_instance._doctrine:Resolve(round);if(d==GunneryDoctrine.HunterKiller&&kind==EnemyKind.Sniper)return -.12f;if(d==GunneryDoctrine.CounterFire&&kind==EnemyKind.Heavy)return -.08f;return 0f;}
    }
}
