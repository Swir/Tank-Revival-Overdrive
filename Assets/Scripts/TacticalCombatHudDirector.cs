using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Tactical combat HUD. v11.7 extends the bottom command strip with live platoon maneuver
    /// state from AdaptivePlatoonManeuverDirector without taking gameplay authority.
    /// </summary>
    public sealed class TacticalCombatHudDirector : MonoBehaviour
    {
        public const float PanelHeight = 96f;
        private TankGame _game;
        private PlayerTank _player;
        private float _nextPlayerScan;
        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _good;
        private GUIStyle _warning;
        private GUIStyle _critical;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TacticalCombatHudDirector>() != null) return;
            var go = new GameObject("TacticalCombatHudDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<TacticalCombatHudDirector>();
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
                return;
            }

            if (_player == null || Time.time >= _nextPlayerScan)
            {
                _nextPlayerScan = Time.time + 0.60f;
                _player = FindAnyObjectByType<PlayerTank>();
            }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.38f, 0.90f, 1f) }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.80f, 0.87f, 0.93f) }
            };
            _good = new GUIStyle(_body)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.32f, 1f, 0.52f) }
            };
            _warning = new GUIStyle(_body)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.66f, 0.14f) }
            };
            _critical = new GUIStyle(_body)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.14f, 0.08f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            float width = Mathf.Min(690f, Screen.width - 36f);
            float height = PanelHeight;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 154f;

            GUI.color = new Color(0.015f, 0.024f, 0.038f, 0.93f);
            GUI.Box(new Rect(x, y, width, height), string.Empty);
            GUI.color = Color.white;

            EnemyFaction faction = EnemyFactionDirector.Instance != null
                ? EnemyFactionDirector.Instance.CurrentFaction
                : EnemyFactionDirector.FactionForRound(_game.CurrentRound);
            string factionName = EnemyFactionDirector.FactionName(faction);
            TacticalThreatDirector threat = TacticalThreatDirector.Instance;
            int threatPercent = threat != null ? threat.ThreatPercent : 0;
            string threatText = threat != null ? threat.ThreatLabel : "SCANNING";

            GUI.Label(new Rect(x + 12f, y + 5f, width - 24f, 18f),
                $"TACTICAL LINK // R{_game.CurrentRound:000}  •  {factionName}  •  EAGLE THREAT {threatPercent}% {threatText}",
                threatPercent >= 70 ? _critical : threatPercent >= 45 ? _warning : _title);

            DrawAssaultOperationStrip(x, y, width);

            if (_player == null || _player.Health == null)
            {
                GUI.Label(new Rect(x + 12f, y + 49f, width - 24f, 18f), "PLAYER VEHICLE OFFLINE // RESPAWN LINK ACTIVE", _critical);
                return;
            }

            ArmorSystem armor = _player.GetComponent<ArmorSystem>();
            if (armor != null)
            {
                GUIStyle armorStyle = armor.AverageIntegrity <= 35 ? _critical : armor.AverageIntegrity <= 65 ? _warning : _good;
                GUI.Label(new Rect(x + 12f, y + 48f, width * 0.62f, 18f),
                    $"DAMAGE CONTROL  ENG {armor.EngineIntegrity}%   TRK {armor.TrackIntegrity}%   GUN {armor.GunIntegrity}%   AMMO {armor.AmmoRackIntegrity}%",
                    armorStyle);

                string readiness = armor.IsMobilityCritical && armor.IsWeaponCritical ? "VEHICLE CRITICAL" :
                                   armor.IsMobilityCritical ? "MOBILITY CRITICAL" :
                                   armor.IsWeaponCritical ? "WEAPON SYSTEM CRITICAL" :
                                   armor.AverageIntegrity < 75 ? "FIELD REPAIR ADVISED [K]" : "COMBAT READY";
                GUI.Label(new Rect(x + 12f, y + 69f, width * 0.55f, 18f), readiness,
                    armor.AverageIntegrity <= 35 ? _critical : armor.AverageIntegrity < 75 ? _warning : _good);
            }

            AmmoType active = _player.ActiveAmmo;
            int count = _player.GetAmmoCount(active);
            string ammo = active == AmmoType.Basic ? "∞" : count.ToString();
            string priority = threat != null && threat.PriorityTarget != null
                ? threat.PriorityTarget.Kind.ToString().ToUpperInvariant()
                : "NONE";

            GUI.Label(new Rect(x + width * 0.58f, y + 48f, width * 0.40f, 18f),
                $"ACTIVE {AmmoDatabase.DisplayName(active)} [{ammo}]", _body);
            GUI.Label(new Rect(x + width * 0.58f, y + 69f, width * 0.40f, 18f),
                "PRIORITY TARGET // " + priority,
                priority == "NONE" ? _good : threatPercent >= 70 ? _critical : _warning);
        }

        private void DrawAssaultOperationStrip(float x, float y, float width)
        {
            AdaptivePlatoonManeuverDirector.ManeuverPresentationSnapshot snapshot =
                AdaptivePlatoonManeuverDirector.ReadPresentationSnapshot(_game.CurrentRound);
            AdaptiveAssaultPresentationDirector presentation = AdaptiveAssaultPresentationDirector.Instance;
            string operation = AdaptiveAssaultPresentationDirector.OperationLabel(_game.CurrentRound);
            int hunters = presentation != null ? presentation.ActiveHunters : 0;
            int support = presentation != null ? presentation.ActiveSupport : 0;
            int breachers = presentation != null ? presentation.ActiveBreacher : 0;
            int counter = presentation != null ? presentation.CounterFireCues : 0;

            GUIStyle style = snapshot.Reorganizing ? _warning : counter > 0 ? _critical : _body;
            string doctrine = snapshot.Doctrine.ToString().ToUpperInvariant();
            GUI.Label(new Rect(x + 12f, y + 26f, width - 24f, 18f),
                $"ENEMY MANEUVER // {operation}  •  HUNTER {hunters}  BREACH {breachers}  SUPPORT {support}  •  {doctrine}", style);
        }
    }
}
