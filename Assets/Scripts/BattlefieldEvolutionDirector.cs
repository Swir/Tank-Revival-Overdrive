using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v1.4 BATTLEFIELD EVOLUTION
    /// Turns the ten campaign sectors into mechanically distinct battlefields.
    /// Conditions are deterministic per round, clearly telegraphed, and never deal
    /// environmental damage directly to the Orzelek core.
    /// </summary>
    public sealed class BattlefieldEvolutionDirector : MonoBehaviour
    {
        private enum BattleCondition
        {
            Clear,
            DustFront,
            ArtilleryWeather,
            ElectricalStorm,
            Whiteout,
            Firestorm,
            Blackout,
            CounterBattery
        }

        private static readonly string[] SectorNames =
        {
            "BORDER WATCH", "IRON VALLEY", "ASHEN CROSSROADS", "STORM FRONT", "BLACK RIVER",
            "FROZEN SPEAR", "FORTRESS BELT", "NIGHT OFFENSIVE", "BURNING GATE", "OVERDRIVE ZERO"
        };

        private static readonly Color[] SectorAccents =
        {
            new Color(0.30f, 0.84f, 1f), new Color(0.62f, 0.72f, 0.78f), new Color(0.90f, 0.54f, 0.22f),
            new Color(0.28f, 0.68f, 1f), new Color(0.18f, 0.72f, 0.82f), new Color(0.78f, 0.94f, 1f),
            new Color(0.76f, 0.74f, 0.62f), new Color(0.38f, 0.34f, 0.76f), new Color(1f, 0.36f, 0.08f),
            new Color(1f, 0.12f, 0.16f)
        };

        private TankGame _game;
        private int _round = -1;
        private int _sector = -1;
        private BattleCondition _condition;
        private Transform _decorRoot;
        private float _nextHazard;
        private float _bannerUntil;
        private string _banner = string.Empty;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _warning;
        private readonly List<GameObject> _decor = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<BattlefieldEvolutionDirector>() != null) return;
            var go = new GameObject("BattlefieldEvolutionDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<BattlefieldEvolutionDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying) return;

            int round = _game.CurrentRound;
            if (round != _round)
                BeginRound(round);

            if (_condition != BattleCondition.Clear && Time.time >= _nextHazard)
            {
                LaunchHazard();
                _nextHazard = Time.time + HazardCadence();
            }
        }

        private void BeginRound(int round)
        {
            _round = round;
            _sector = Mathf.Clamp((round - 1) / 10, 0, 9);
            _condition = SelectCondition(round, _sector);
            _nextHazard = Time.time + Mathf.Max(4.5f, HazardCadence() * 0.62f);
            RebuildSectorDecor();
            _banner = $"{SectorNames[_sector]} // {ConditionName(_condition)}";
            _bannerUntil = Time.unscaledTime + 3.3f;
        }

        private static BattleCondition SelectCondition(int round, int sector)
        {
            if (round <= 3) return BattleCondition.Clear;
            int seed = (round * 37 + sector * 19) % 100;
            if (sector >= 9 && seed < 32) return BattleCondition.CounterBattery;
            if (sector >= 8 && seed < 48) return BattleCondition.Firestorm;
            if (sector == 7 && seed < 58) return BattleCondition.Blackout;
            if (sector == 5 && seed < 60) return BattleCondition.Whiteout;
            if ((sector == 3 || sector == 4) && seed < 52) return BattleCondition.ElectricalStorm;
            if ((sector == 2 || sector == 6) && seed < 48) return BattleCondition.ArtilleryWeather;
            if (seed < 35) return BattleCondition.DustFront;
            if (seed < 58) return BattleCondition.ArtilleryWeather;
            return BattleCondition.Clear;
        }

        private float HazardCadence()
        {
            float campaignPressure = Mathf.Lerp(14f, 7.2f, (_round - 1f) / 99f);
            switch (_condition)
            {
                case BattleCondition.CounterBattery: return campaignPressure * 0.72f;
                case BattleCondition.Firestorm: return campaignPressure * 0.80f;
                case BattleCondition.ElectricalStorm: return campaignPressure * 0.90f;
                case BattleCondition.ArtilleryWeather: return campaignPressure;
                case BattleCondition.Blackout: return campaignPressure * 1.08f;
                default: return campaignPressure * 1.18f;
            }
        }

        private void LaunchHazard()
        {
            Vector2 point = PickSafeHazardPoint();
            float warning = Mathf.Clamp(1.55f - _round * 0.004f, 1.0f, 1.5f);
            float radius = _condition == BattleCondition.CounterBattery ? 1.55f : 1.25f;
            Color accent = SectorAccents[_sector];

            switch (_condition)
            {
                case BattleCondition.ElectricalStorm:
                    StartCoroutine(BattlefieldHazard.Telegraph(point, radius, warning, BattlefieldHazard.Kind.EmpStrike, accent, _round));
                    break;
                case BattleCondition.Firestorm:
                    StartCoroutine(BattlefieldHazard.Telegraph(point, radius + 0.20f, warning, BattlefieldHazard.Kind.FireBurst, accent, _round));
                    break;
                case BattleCondition.Whiteout:
                    StartCoroutine(BattlefieldHazard.Telegraph(point, radius, warning + 0.25f, BattlefieldHazard.Kind.FrostShock, accent, _round));
                    break;
                case BattleCondition.Blackout:
                    StartCoroutine(BattlefieldHazard.Telegraph(point, radius, warning, BattlefieldHazard.Kind.EmpStrike, new Color(0.42f, 0.38f, 1f), _round));
                    break;
                case BattleCondition.CounterBattery:
                    StartCoroutine(BattlefieldHazard.Telegraph(point, radius, warning, BattlefieldHazard.Kind.HeavyShell, new Color(1f, 0.16f, 0.08f), _round));
                    if (_round >= 85)
                    {
                        Vector2 second = PickSafeHazardPoint();
                        StartCoroutine(BattlefieldHazard.Telegraph(second, radius * 0.86f, warning + 0.25f, BattlefieldHazard.Kind.HeavyShell, accent, _round));
                    }
                    break;
                case BattleCondition.ArtilleryWeather:
                    StartCoroutine(BattlefieldHazard.Telegraph(point, radius, warning, BattlefieldHazard.Kind.HeavyShell, accent, _round));
                    break;
                case BattleCondition.DustFront:
                    StartCoroutine(BattlefieldHazard.Telegraph(point, radius * 0.90f, warning + 0.30f, BattlefieldHazard.Kind.Concussion, accent, _round));
                    break;
            }
        }

        private Vector2 PickSafeHazardPoint()
        {
            // Never spawn environmental damage directly on top of the Orzelek fortress.
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector2 p = new Vector2(Random.Range(-10.6f, 10.6f), Random.Range(-4.1f, 5.4f));
                if (Vector2.Distance(p, _game.BasePosition) > 2.65f)
                    return p;
            }
            return new Vector2(Random.Range(-8f, 8f), 1.5f);
        }

        private void RebuildSectorDecor()
        {
            if (_decorRoot != null) Destroy(_decorRoot.gameObject);
            _decor.Clear();
            var root = new GameObject("SectorIdentity_" + (_sector + 1).ToString("00"));
            DontDestroyOnLoad(root);
            _decorRoot = root.transform;

            Color accent = SectorAccents[_sector];
            var rng = new System.Random(_round * 991 + _sector * 31);
            int count = 18 + _sector * 2;
            for (int i = 0; i < count; i++)
            {
                float x = (float)(rng.NextDouble() * 22.0 - 11.0);
                float y = (float)(rng.NextDouble() * 10.8 - 5.0);
                if (Vector2.Distance(new Vector2(x, y), _game.BasePosition) < 1.8f) continue;

                GameObject item;
                if (_sector == 5)
                    item = VisualFactory.Disc("IceShard", _decorRoot, Vector2.one * (0.06f + (float)rng.NextDouble() * 0.10f), new Color(0.78f, 0.94f, 1f, 0.36f), new Vector3(x, y, 0f), -54);
                else if (_sector >= 8)
                    item = VisualFactory.RectRotated("BurningDebris", _decorRoot, new Vector2(0.05f, 0.20f), new Color(accent.r, accent.g, accent.b, 0.35f), new Vector3(x, y, 0f), (float)rng.NextDouble() * 180f, -54);
                else if (_sector == 7)
                    item = VisualFactory.Disc("NightBeacon", _decorRoot, Vector2.one * 0.13f, new Color(accent.r, accent.g, accent.b, 0.22f), new Vector3(x, y, 0f), -54);
                else
                    item = VisualFactory.RectRotated("SectorDebris", _decorRoot, new Vector2(0.05f + (float)rng.NextDouble() * 0.14f, 0.03f + (float)rng.NextDouble() * 0.08f), new Color(accent.r, accent.g, accent.b, 0.18f), new Vector3(x, y, 0f), (float)rng.NextDouble() * 180f, -54);
                _decor.Add(item);
            }
        }

        private static string ConditionName(BattleCondition condition)
        {
            switch (condition)
            {
                case BattleCondition.DustFront: return "DUST FRONT";
                case BattleCondition.ArtilleryWeather: return "ARTILLERY WEATHER";
                case BattleCondition.ElectricalStorm: return "ELECTRICAL STORM";
                case BattleCondition.Whiteout: return "WHITEOUT";
                case BattleCondition.Firestorm: return "FIRESTORM";
                case BattleCondition.Blackout: return "BLACKOUT";
                case BattleCondition.CounterBattery: return "COUNTER-BATTERY HELL";
                default: return "CLEAR CONTACT";
            }
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.52f, 0.92f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.82f, 0.88f, 0.92f) } };
            _warning = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            Color accent = SectorAccents[Mathf.Clamp(_sector, 0, 9)];

            GUI.color = new Color(0.02f, 0.03f, 0.045f, 0.90f);
            GUI.Box(new Rect(Screen.width - 336f, 14f, 320f, 66f), string.Empty);
            GUI.color = Color.white;
            _header.normal.textColor = accent;
            GUI.Label(new Rect(Screen.width - 322f, 20f, 294f, 21f), $"SECTOR {_sector + 1:00} // {SectorNames[Mathf.Clamp(_sector, 0, 9)]}", _header);
            GUI.Label(new Rect(Screen.width - 322f, 42f, 294f, 20f), $"BATTLE CONDITION: {ConditionName(_condition)}", _body);
            GUI.Label(new Rect(Screen.width - 322f, 59f, 294f, 16f), "Environmental strikes spare the Orzelek core directly.", _body);

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.015f, 0.025f, 0.04f, 0.88f);
                GUI.Box(new Rect(Screen.width * 0.5f - 330f, Screen.height * 0.25f - 28f, 660f, 56f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 320f, Screen.height * 0.25f - 18f, 640f, 38f), _banner, _warning);
            }
        }
    }
}
