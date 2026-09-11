using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v3.5 compatibility guard for the legacy pre-deployment shortcuts.
    /// v3.1 GarageLoadout and v3.4 CommanderCareer both historically consumed F9-F11 in the
    /// same frame. Preserve the old garage behavior on plain/Shift F9-F11, reserve Ctrl+F9-F11
    /// for Commander perks, and keep Command Center clicks as the preferred interaction path.
    /// </summary>
    [DefaultExecutionOrder(12000)]
    public sealed class CommandCenterInputGuard : MonoBehaviour
    {
        private int _vanguard;
        private int _engineer;
        private int _logistics;
        private int _primary;
        private int _reactor;
        private int _hull;
        private bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CommandCenterInputGuard>() != null) return;
            var go = new GameObject("CommandCenterInputGuard_v3_5");
            DontDestroyOnLoad(go);
            go.AddComponent<CommandCenterInputGuard>();
        }

        private void Update()
        {
            TankGame game = FindAnyObjectByType<TankGame>();
            if (game != null && game.IsPlaying)
            {
                Snapshot();
                return;
            }

            if (!_initialized)
            {
                Snapshot();
                _initialized = true;
                return;
            }

            bool f9 = Input.GetKeyDown(KeyCode.F9);
            bool f10 = Input.GetKeyDown(KeyCode.F10);
            bool f11 = Input.GetKeyDown(KeyCode.F11);
            if (!f9 && !f10 && !f11)
            {
                Snapshot();
                return;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool centerOpen = CommandCenterDirector.IsOpen;

            if (centerOpen)
            {
                RestoreCommander();
                RestoreGarageSelection();
                return;
            }

            if (ctrl)
            {
                // Commander shortcut explicitly requested. Undo only the unintended garage cycle.
                RestoreGarageSelection();
            }
            else
            {
                // Plain and Shift shortcuts belong to GarageLoadout. Undo the unintended perk buy.
                RestoreCommander();
            }

            PlayerPrefs.Save();
            Snapshot();
        }

        private void Snapshot()
        {
            _vanguard = PlayerPrefs.GetInt("TankRevival.CommanderPerks.Vanguard", 0);
            _engineer = PlayerPrefs.GetInt("TankRevival.CommanderPerks.Engineer", 0);
            _logistics = PlayerPrefs.GetInt("TankRevival.CommanderPerks.Logistics", 0);
            _primary = PlayerPrefs.GetInt("TankRevival.Garage.Loadout.Primary", 0);
            _reactor = PlayerPrefs.GetInt("TankRevival.Garage.Loadout.Reactor", 0);
            _hull = PlayerPrefs.GetInt("TankRevival.Garage.Loadout.Hull", 0);
        }

        private void RestoreCommander()
        {
            PlayerPrefs.SetInt("TankRevival.CommanderPerks.Vanguard", _vanguard);
            PlayerPrefs.SetInt("TankRevival.CommanderPerks.Engineer", _engineer);
            PlayerPrefs.SetInt("TankRevival.CommanderPerks.Logistics", _logistics);
        }

        private void RestoreGarageSelection()
        {
            PlayerPrefs.SetInt("TankRevival.Garage.Loadout.Primary", _primary);
            PlayerPrefs.SetInt("TankRevival.Garage.Loadout.Reactor", _reactor);
            PlayerPrefs.SetInt("TankRevival.Garage.Loadout.Hull", _hull);
        }
    }
}
