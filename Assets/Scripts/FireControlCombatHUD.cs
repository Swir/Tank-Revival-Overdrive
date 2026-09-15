using UnityEngine;

namespace TankRevival
{
    public sealed class FireControlCombatHUD : MonoBehaviour
    {
        private PlayerTank _player;
        private GUIStyle _label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<FireControlCombatHUD>() != null) return;
            var go = new GameObject("FireControlCombatHUD");
            DontDestroyOnLoad(go);
            go.AddComponent<FireControlCombatHUD>();
        }

        private void Update()
        {
            if (_player == null) _player = FindAnyObjectByType<PlayerTank>();
        }

        private void OnGUI()
        {
            if (_player == null) return;
            var body = _player.GetComponent<Rigidbody2D>();
            var armor = _player.GetComponent<ArmorSystem>();
            float movement = body != null ? Mathf.Clamp01(body.linearVelocity.magnitude / 5f) : 0f;
            float stabilization = FireControlBallisticsDirector.Stabilization(movement, armor);
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            string state = stabilization >= 0.90f ? "LOCKED" : stabilization >= 0.72f ? "STABILIZING" : "UNSTABLE";
            GUI.Label(new Rect(Screen.width - 238f, Screen.height - 64f, 220f, 26f), $"FCS {state}  {Mathf.RoundToInt(stabilization * 100f)}%", _label);
        }
    }
}
