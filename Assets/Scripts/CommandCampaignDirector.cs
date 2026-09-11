using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.1 COMMAND CAMPAIGN
    /// Presents a meaningful doctrine choice at the start of Acts II-V.
    /// Doctrine effects are intentionally compact and reuse existing combat systems.
    /// </summary>
    public sealed class CommandCampaignDirector : MonoBehaviour
    {
        private enum Doctrine
        {
            None,
            Fortress,
            Hunter
        }

        private TankGame _game;
        private int _round;
        private int _lastAct = -1;
        private Doctrine _activeDoctrine;
        private bool _choiceOpen;
        private float _choiceUntil;
        private float _bannerUntil;
        private string _banner = string.Empty;
        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _choice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CommandCampaignDirector>() != null) return;
            var go = new GameObject("CommandCampaignDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<CommandCampaignDirector>();
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
                _choiceOpen = false;
                return;
            }

            int round = _game.CurrentRound;
            if (round != _round)
            {
                _round = round;
                OnRoundStarted(round);
            }

            if (_choiceOpen)
            {
                if (Input.GetKeyDown(KeyCode.Alpha8)) SelectDoctrine(Doctrine.Fortress);
                else if (Input.GetKeyDown(KeyCode.Alpha9)) SelectDoctrine(Doctrine.Hunter);
                else if (Time.unscaledTime >= _choiceUntil) SelectDoctrine(Doctrine.Fortress);
            }
        }

        private void OnRoundStarted(int round)
        {
            int act = Mathf.Clamp((round - 1) / 20, 0, 4);
            if (act != _lastAct)
            {
                _lastAct = act;
                if (act > 0)
                    OpenDoctrineChoice(act);
            }

            ApplyRoundDoctrine(round);
        }

        private void OpenDoctrineChoice(int act)
        {
            _choiceOpen = true;
            _choiceUntil = Time.unscaledTime + 9f;
            _banner = $"COMMAND DECISION // ACT {act + 1}";
            _bannerUntil = _choiceUntil;
        }

        private void SelectDoctrine(Doctrine doctrine)
        {
            _activeDoctrine = doctrine;
            _choiceOpen = false;
            PlayerPrefs.SetInt("TankRevival.CommandDoctrine", (int)doctrine);
            PlayerPrefs.Save();

            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (doctrine == Doctrine.Fortress)
            {
                _game.RepairEagle(2);
                if (player != null && player.Health != null)
                {
                    player.Health.Heal(2);
                    player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 2.5f);
                }
                _banner = "FORTRESS DOCTRINE // ORZELEK REINFORCED";
            }
            else
            {
                if (player != null)
                {
                    player.AddAmmo(AmmoType.ArmorPiercing, 6);
                    player.AddAmmo(AmmoType.EMP, 3);
                    player.AddAmmo(AmmoType.Plasma, _round >= 61 ? 2 : 0);
                    if (player.Health != null)
                        player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 2f);
                }
                _banner = "HUNTER DOCTRINE // SPECIAL AMMO RESERVE";
            }
            _bannerUntil = Time.unscaledTime + 3.2f;
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.72f, 0f);
        }

        private void ApplyRoundDoctrine(int round)
        {
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player == null) return;

            if (_activeDoctrine == Doctrine.Fortress)
            {
                if (round % 4 == 0) _game.RepairEagle(1);
                if (player.Health != null && round % 5 == 0) player.Health.Heal(1);
            }
            else if (_activeDoctrine == Doctrine.Hunter)
            {
                if (round % 3 == 0) player.AddAmmo(AmmoType.ArmorPiercing, 2);
                if (round >= 60 && round % 5 == 0) player.AddAmmo(AmmoType.EMP, 1);
                if (round >= 80 && round % 8 == 0) player.AddAmmo(AmmoType.Plasma, 1);
            }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.42f, 0.94f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, normal = { textColor = Color.white } };
            _choice = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.82f, 0.26f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            if (_choiceOpen)
            {
                float w = Mathf.Min(760f, Screen.width - 40f);
                float x = (Screen.width - w) * 0.5f;
                float y = Screen.height * 0.22f;
                GUI.color = new Color(0.015f, 0.026f, 0.045f, 0.96f);
                GUI.Box(new Rect(x, y, w, 146f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 12f, y + 12f, w - 24f, 30f), "COMMAND CAMPAIGN // CHOOSE ACT DOCTRINE", _title);
                GUI.Label(new Rect(x + 16f, y + 49f, w - 32f, 24f), "[8] FORTRESS — Orzelek repair, armor sustain, periodic defensive recovery", _choice);
                GUI.Label(new Rect(x + 16f, y + 78f, w - 32f, 24f), "[9] HUNTER — AP/EMP/Plasma reserves and offensive round resupply", _choice);
                GUI.Label(new Rect(x + 16f, y + 111f, w - 32f, 20f), "No input: Fortress doctrine is selected automatically.", _body);
            }
            else if (Time.unscaledTime < _bannerUntil)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.20f, Screen.width, 36f), _banner, _title);
            }

            if (_activeDoctrine != Doctrine.None)
            {
                string label = _activeDoctrine == Doctrine.Fortress ? "FORTRESS" : "HUNTER";
                GUI.color = new Color(0.02f, 0.04f, 0.06f, 0.90f);
                GUI.Box(new Rect(14f, 126f, 230f, 34f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(24f, 133f, 210f, 20f), "ACT DOCTRINE // " + label, _body);
            }
        }
    }
}
