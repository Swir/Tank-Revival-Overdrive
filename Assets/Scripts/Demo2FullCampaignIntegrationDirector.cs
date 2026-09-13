using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(451)]
    public sealed class Demo2FullCampaignIntegrationDirector : MonoBehaviour
    {
        public const int AdvancedControlsIntroRound = 12;
        public const int EwIntroRound = 20;
        public const int NetworkHuntIntroRound = 28;
        public const int MobileHqIntroRound = 36;
        public const float HintDuration = 5.5f;

        private static Demo2FullCampaignIntegrationDirector _instance;
        private TankGame _game;
        private int _lastRound;
        private string _hint;
        private float _hintUntil;
        private GUIStyle _hintStyle;
        private GUIStyle _keyStyle;

        public static Demo2FullCampaignIntegrationDirector Instance => _instance;
        public static bool ConfigurationValid =>
            AdvancedControlsIntroRound >= 10 && AdvancedControlsIntroRound < EwIntroRound &&
            EwIntroRound < NetworkHuntIntroRound && NetworkHuntIntroRound < MobileHqIntroRound &&
            MobileHqIntroRound == MobileHQWarfareDirector.MinimumOperationRound &&
            HintDuration >= 4f && HintDuration <= 7f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<Demo2FullCampaignIntegrationDirector>() != null) return;
            var go = new GameObject("Demo2FullCampaignIntegrationDirector_v8_0");
            DontDestroyOnLoad(go);
            go.AddComponent<Demo2FullCampaignIntegrationDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                _lastRound = 0;
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round == _lastRound) return;
            _lastRound = round;

            if (round == AdvancedControlsIntroRound)
                Show("TAKTYCZNE KONTRY  •  R: DYM   C: ECM   V: WABIK");
            else if (round == EwIntroRound)
                Show("WALKA ELEKTRONICZNA  •  EMP i ECM przerywają koordynację wroga");
            else if (round == NetworkHuntIntroRound)
                Show("POLOWANIE NA SIEĆ  •  G: SIGINT   H: DRON  •  niszcz przekaźniki EW");
            else if (round == MobileHqIntroRound)
                Show("MOBILE HQ  •  rozbij eskortę i sieć, potem centrum dowodzenia");
        }

        private void Show(string text)
        {
            _hint = text;
            _hintUntil = Time.unscaledTime + HintDuration;
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || Time.unscaledTime >= _hintUntil || string.IsNullOrEmpty(_hint)) return;
            EnsureStyles();
            float width = Mathf.Min(Screen.width - 32f, 720f);
            Rect box = new Rect((Screen.width - width) * 0.5f, Screen.height - 112f, width, 58f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 16f, box.y + 9f, box.width - 32f, 40f), _hint, _hintStyle);
        }

        private void EnsureStyles()
        {
            if (_hintStyle != null) return;
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _hintStyle.normal.textColor = new Color(0.92f, 0.96f, 1f);
            _keyStyle = new GUIStyle(_hintStyle);
        }
    }
}
