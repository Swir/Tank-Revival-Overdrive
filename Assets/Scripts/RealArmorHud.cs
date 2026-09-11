using UnityEngine;

namespace TankRevival
{
    public sealed class RealArmorHud : MonoBehaviour
    {
        private TankGame _game;
        private PlayerTank _player;
        private ArmorSystem _armor;
        private float _nextScan;
        private GUIStyle _label;
        private GUIStyle _title;
        private GUIStyle _hint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindAnyObjectByType<RealArmorHud>() != null) return;
            new GameObject("RealArmorHUD").AddComponent<RealArmorHud>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.35f;

            if (_game == null)
                _game = FindAnyObjectByType<TankGame>();

            PlayerTank current = FindAnyObjectByType<PlayerTank>();
            if (current != _player)
            {
                _player = current;
                _armor = _player != null ? _player.GetComponent<ArmorSystem>() : null;
            }
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _title = new GUIStyle(_label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.35f, 0.90f, 1f) }
            };
            _hint = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.68f, 0.76f, 0.82f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _player == null || _armor == null) return;
            EnsureStyles();

            float width = 286f;
            Rect panel = new Rect(Screen.width - width - 14f, 12f, width, 108f);
            GUI.Box(panel, string.Empty);

            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, width - 24f, 20f), "REAL ARMOR // MODULE STATUS", _title);

            int mobility = Mathf.RoundToInt(_armor.MobilityMultiplier * 100f);
            int weapon = Mathf.RoundToInt((1f / Mathf.Max(1f, _armor.ReloadMultiplier)) * 100f);
            string zone = _armor.LastZone.ToString().ToUpperInvariant();

            GUI.Label(new Rect(panel.x + 12f, panel.y + 32f, width - 24f, 20f), $"ENGINE  {mobility}%     GUN  {weapon}%", _label);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 54f, width - 24f, 20f), $"LAST IMPACT  {zone}{(_armor.LastCritical ? "  // CRITICAL" : string.Empty)}", _label);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 77f, width - 24f, 20f), "Mouse aim + LMB fire  •  hull moves independently", _hint);
        }
    }
}
