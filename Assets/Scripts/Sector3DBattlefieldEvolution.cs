using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.6 3D BATTLEFIELD EVOLUTION.
    /// Gives each ten-round campaign sector a distinct runtime 3D silhouette while the existing
    /// BattlefieldEvolutionDirector remains authoritative for hazards and mechanical conditions.
    /// Everything generated here is presentation-only and intentionally carries no colliders.
    /// </summary>
    [DefaultExecutionOrder(10020)]
    public sealed class Sector3DBattlefieldEvolution : MonoBehaviour
    {
        private static readonly string[] Names =
        {
            "BORDER WATCH", "IRON VALLEY", "ASHEN CROSSROADS", "STORM FRONT", "BLACK RIVER",
            "FROZEN SPEAR", "FORTRESS BELT", "NIGHT OFFENSIVE", "BURNING GATE", "OVERDRIVE ZERO"
        };

        private static readonly Color[] Accent =
        {
            new Color(0.30f, 0.84f, 1f), new Color(0.62f, 0.72f, 0.78f), new Color(0.90f, 0.54f, 0.22f),
            new Color(0.28f, 0.68f, 1f), new Color(0.18f, 0.72f, 0.82f), new Color(0.78f, 0.94f, 1f),
            new Color(0.76f, 0.74f, 0.62f), new Color(0.38f, 0.34f, 0.76f), new Color(1f, 0.36f, 0.08f),
            new Color(1f, 0.12f, 0.16f)
        };

        private TankGame _game;
        private Transform _root;
        private readonly List<Transform> _animated = new List<Transform>(40);
        private readonly List<float> _phases = new List<float>(40);
        private int _round = -1;
        private int _sector = -1;
        private float _bannerUntil;
        private GUIStyle _badge;
        private GUIStyle _small;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<Sector3DBattlefieldEvolution>() != null) return;
            var go = new GameObject("Sector3DBattlefieldEvolution");
            DontDestroyOnLoad(go);
            go.AddComponent<Sector3DBattlefieldEvolution>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying) return;
            if (_game.CurrentRound != _round)
                Rebuild(_game.CurrentRound);

            AnimateSector();
        }

        private void Rebuild(int round)
        {
            _round = round;
            _sector = Mathf.Clamp((round - 1) / 10, 0, 9);
            if (_root != null) Destroy(_root.gameObject);
            _animated.Clear();
            _phases.Clear();

            var root = new GameObject("Sector3DIdentity_" + (_sector + 1).ToString("00"));
            DontDestroyOnLoad(root);
            _root = root.transform;
            _bannerUntil = Time.unscaledTime + 3.6f;

            int seed = round * 1319 + _sector * 719 + 2606;
            var rng = new System.Random(seed);
            Color accent = Accent[_sector];

            BuildPerimeter(rng, accent);
            BuildSectorLandmarks(rng, accent);
            BuildForegroundDebris(rng, accent);
        }

        private void BuildPerimeter(System.Random rng, Color accent)
        {
            Color structure = Color.Lerp(accent, new Color(0.13f, 0.15f, 0.18f), 0.72f);
            Color dark = Color.Lerp(structure, Color.black, 0.48f);

            // Outside playable collision bounds: strong depth cues without changing gameplay paths.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 7; i++)
                {
                    float x = -10.8f + i * 3.6f + Rand(rng, -0.35f, 0.35f);
                    float y = side * (6.45f + Rand(rng, 0.10f, 0.48f));
                    float h = Rand(rng, 0.65f, 1.55f) + _sector * 0.035f;
                    Runtime3DFactory.Box("OuterRevetment3D", _root, new Vector3(x, y, 0.05f - h * 0.5f), new Vector3(Rand(rng, 0.35f, 0.80f), Rand(rng, 0.28f, 0.55f), h), structure, 0.42f, 0.22f);
                    if ((i + _sector) % 2 == 0)
                        Runtime3DFactory.Box("OuterCap3D", _root, new Vector3(x, y, -h - 0.02f), new Vector3(0.24f, 0.24f, 0.10f), accent, 0.58f, 0.48f);
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 12.15f;
                for (int i = 0; i < 5; i++)
                {
                    float y = -4.8f + i * 2.45f + Rand(rng, -0.22f, 0.22f);
                    Runtime3DFactory.Box("SideButtress3D", _root, new Vector3(x, y, -0.38f), new Vector3(0.55f, 0.48f, 1.05f), dark, 0.48f, 0.25f);
                }
            }
        }

        private void BuildSectorLandmarks(System.Random rng, Color accent)
        {
            switch (_sector)
            {
                case 0: BuildBorderWatch(rng, accent); break;
                case 1: BuildIronValley(rng, accent); break;
                case 2: BuildAshenCrossroads(rng, accent); break;
                case 3: BuildStormFront(rng, accent); break;
                case 4: BuildBlackRiver(rng, accent); break;
                case 5: BuildFrozenSpear(rng, accent); break;
                case 6: BuildFortressBelt(rng, accent); break;
                case 7: BuildNightOffensive(rng, accent); break;
                case 8: BuildBurningGate(rng, accent); break;
                default: BuildOverdriveZero(rng, accent); break;
            }
        }

        private void BuildBorderWatch(System.Random rng, Color accent)
        {
            for (int i = 0; i < 6; i++)
            {
                float x = -9.8f + i * 3.9f;
                BuildWatchTower(new Vector3(x, 6.05f, 0f), accent, 0.85f + Rand(rng, 0f, 0.25f));
            }
            for (int i = 0; i < 10; i++)
                Runtime3DFactory.Box("BorderMarker3D", _root, new Vector3(-10.4f + i * 2.3f, -6.0f, -0.20f), new Vector3(0.10f, 0.12f, 0.80f), accent, 0.25f, 0.30f);
        }

        private void BuildIronValley(System.Random rng, Color accent)
        {
            Color steel = new Color(0.28f, 0.32f, 0.36f);
            for (int i = 0; i < 7; i++)
            {
                float x = -10.5f + i * 3.5f;
                float h = Rand(rng, 0.9f, 1.8f);
                Runtime3DFactory.Box("IronStack3D", _root, new Vector3(x, 6.25f, -0.3f - h * 0.5f), new Vector3(0.55f, 0.78f, h), steel, 0.78f, 0.34f);
                Runtime3DFactory.Cylinder("SmelterCap3D", _root, new Vector3(x, 6.25f, -1.18f - h), 0.38f, 0.16f, accent, 0.56f, 0.42f);
            }
            for (int i = 0; i < 8; i++)
                Runtime3DFactory.Box("RailTie3D", _root, new Vector3(-8.5f + i * 2.45f, -5.95f, 0.12f), new Vector3(1.35f, 0.12f, 0.10f), Color.Lerp(steel, Color.black, 0.3f), 0.62f, 0.22f);
        }

        private void BuildAshenCrossroads(System.Random rng, Color accent)
        {
            Color concrete = new Color(0.24f, 0.23f, 0.21f);
            for (int i = 0; i < 9; i++)
            {
                float x = -10.2f + i * 2.55f;
                float lean = Rand(rng, -16f, 16f);
                GameObject slab = Runtime3DFactory.Box("RuinedSlab3D", _root, new Vector3(x, 6.0f + Rand(rng, -0.25f, 0.25f), -0.45f), new Vector3(Rand(rng, 0.45f, 1.0f), 0.25f, Rand(rng, 0.8f, 1.65f)), concrete, 0.18f, 0.20f);
                slab.transform.localRotation = Quaternion.Euler(0f, 0f, lean);
            }
            BuildArch(new Vector3(-7.8f, -5.9f, 0f), accent, 1.0f);
            BuildArch(new Vector3(0f, -5.9f, 0f), accent, 1.2f);
            BuildArch(new Vector3(7.8f, -5.9f, 0f), accent, 0.92f);
        }

        private void BuildStormFront(System.Random rng, Color accent)
        {
            for (int i = 0; i < 8; i++)
            {
                float x = -10.4f + i * 3.0f;
                Transform mast = BuildLightningMast(new Vector3(x, 6.2f, 0f), accent, Rand(rng, 0.85f, 1.25f));
                _animated.Add(mast); _phases.Add(Rand(rng, 0f, 9f));
            }
        }

        private void BuildBlackRiver(System.Random rng, Color accent)
        {
            Color bridge = new Color(0.24f, 0.30f, 0.33f);
            for (int i = 0; i < 6; i++)
            {
                float x = -9.5f + i * 3.8f;
                Runtime3DFactory.Box("BridgePier3D", _root, new Vector3(x, 6.1f, -0.55f), new Vector3(0.46f, 0.62f, 1.35f), bridge, 0.55f, 0.24f);
                Runtime3DFactory.Box("RiverLamp3D", _root, new Vector3(x, 5.88f, -1.35f), new Vector3(0.15f, 0.15f, 0.12f), accent, 0.20f, 0.86f);
            }
            for (int i = 0; i < 9; i++)
            {
                GameObject water = Runtime3DFactory.Box("RiverGlint3D", _root, new Vector3(-10.0f + i * 2.5f, 6.75f, 0.18f), new Vector3(1.35f, 0.12f, 0.025f), new Color(0.04f, 0.34f, 0.52f), 0.02f, 0.90f);
                _animated.Add(water.transform); _phases.Add(i * 0.7f);
            }
        }

        private void BuildFrozenSpear(System.Random rng, Color accent)
        {
            Color ice = new Color(0.66f, 0.88f, 1f);
            for (int i = 0; i < 15; i++)
            {
                float x = Rand(rng, -11f, 11f);
                float y = (i % 2 == 0 ? 1f : -1f) * Rand(rng, 5.8f, 6.55f);
                float h = Rand(rng, 0.45f, 1.35f);
                GameObject shard = Runtime3DFactory.Box("IceSpear3D", _root, new Vector3(x, y, -0.3f - h * 0.5f), new Vector3(Rand(rng, 0.10f, 0.22f), Rand(rng, 0.10f, 0.24f), h), Color.Lerp(ice, accent, 0.35f), 0.05f, 0.82f);
                shard.transform.localRotation = Quaternion.Euler(Rand(rng, -8f, 8f), Rand(rng, -8f, 8f), Rand(rng, -14f, 14f));
            }
        }

        private void BuildFortressBelt(System.Random rng, Color accent)
        {
            Color armor = new Color(0.32f, 0.33f, 0.30f);
            for (int i = 0; i < 7; i++)
            {
                float x = -10.3f + i * 3.45f;
                Runtime3DFactory.Box("Bunker3D", _root, new Vector3(x, 6.18f, -0.38f), new Vector3(1.35f, 0.90f, 0.85f), armor, 0.65f, 0.30f);
                Runtime3DFactory.Box("BunkerSlot3D", _root, new Vector3(x, 5.72f, -0.55f), new Vector3(0.58f, 0.06f, 0.13f), accent, 0.12f, 0.78f);
            }
            for (int i = 0; i < 5; i++)
                BuildArch(new Vector3(-9f + i * 4.5f, -5.95f, 0f), armor, 0.72f);
        }

        private void BuildNightOffensive(System.Random rng, Color accent)
        {
            Color black = new Color(0.055f, 0.060f, 0.085f);
            for (int i = 0; i < 10; i++)
            {
                float x = -10.5f + i * 2.3f;
                Runtime3DFactory.Box("NightPylon3D", _root, new Vector3(x, 6.25f, -0.62f), new Vector3(0.18f, 0.18f, 1.45f), black, 0.55f, 0.26f);
                GameObject lamp = Runtime3DFactory.Cylinder("NightSignal3D", _root, new Vector3(x, 6.25f, -1.42f), 0.22f, 0.07f, accent, 0.05f, 0.95f);
                _animated.Add(lamp.transform); _phases.Add(i * 0.91f);
            }
        }

        private void BuildBurningGate(System.Random rng, Color accent)
        {
            BuildArch(new Vector3(-7.6f, 6.05f, 0f), new Color(0.30f, 0.13f, 0.07f), 1.35f);
            BuildArch(new Vector3(0f, 6.05f, 0f), new Color(0.30f, 0.13f, 0.07f), 1.65f);
            BuildArch(new Vector3(7.6f, 6.05f, 0f), new Color(0.30f, 0.13f, 0.07f), 1.35f);
            for (int i = 0; i < 14; i++)
            {
                float x = Rand(rng, -10.7f, 10.7f);
                float y = (i % 2 == 0 ? 1f : -1f) * Rand(rng, 5.75f, 6.45f);
                GameObject ember = Runtime3DFactory.Cylinder("GateEmber3D", _root, new Vector3(x, y, -0.45f), Rand(rng, 0.08f, 0.18f), 0.05f, accent, 0.02f, 0.95f);
                _animated.Add(ember.transform); _phases.Add(Rand(rng, 0f, 10f));
            }
        }

        private void BuildOverdriveZero(System.Random rng, Color accent)
        {
            Color obsidian = new Color(0.075f, 0.045f, 0.060f);
            for (int i = 0; i < 10; i++)
            {
                float x = -10.8f + i * 2.4f;
                float h = 1.15f + (i % 3) * 0.35f;
                Runtime3DFactory.Box("OverdriveMonolith3D", _root, new Vector3(x, 6.25f, -0.25f - h * 0.5f), new Vector3(0.42f, 0.42f, h), obsidian, 0.42f, 0.42f);
                GameObject core = Runtime3DFactory.Box("OverdriveCore3D", _root, new Vector3(x, 6.03f, -0.58f - h), new Vector3(0.16f, 0.06f, 0.26f), accent, 0.05f, 0.98f);
                _animated.Add(core.transform); _phases.Add(i * 0.63f);
            }

            for (int i = 0; i < 4; i++)
                BuildLightningMast(new Vector3(-7.5f + i * 5f, -6.10f, 0f), accent, 1.25f);
        }

        private void BuildForegroundDebris(System.Random rng, Color accent)
        {
            int count = 12 + _sector * 2;
            for (int i = 0; i < count; i++)
            {
                float x = Rand(rng, -11.2f, 11.2f);
                float y = (i % 2 == 0 ? -1f : 1f) * Rand(rng, 5.35f, 6.15f);
                float z = Rand(rng, 0.08f, 0.24f);
                GameObject bit = Runtime3DFactory.Box("SectorDebris3D", _root, new Vector3(x, y, z), new Vector3(Rand(rng, 0.08f, 0.30f), Rand(rng, 0.10f, 0.34f), Rand(rng, 0.05f, 0.18f)), Color.Lerp(accent, new Color(0.20f, 0.19f, 0.18f), 0.74f), 0.32f, 0.20f);
                bit.transform.localRotation = Quaternion.Euler(Rand(rng, -20f, 20f), Rand(rng, -20f, 20f), Rand(rng, 0f, 180f));
            }
        }

        private void BuildWatchTower(Vector3 position, Color accent, float scale)
        {
            Runtime3DFactory.Box("WatchLegL3D", _root, position + new Vector3(-0.22f, 0f, -0.58f * scale), new Vector3(0.10f, 0.10f, 1.20f * scale), new Color(0.24f, 0.27f, 0.28f), 0.52f, 0.25f);
            Runtime3DFactory.Box("WatchLegR3D", _root, position + new Vector3(0.22f, 0f, -0.58f * scale), new Vector3(0.10f, 0.10f, 1.20f * scale), new Color(0.24f, 0.27f, 0.28f), 0.52f, 0.25f);
            Runtime3DFactory.Box("WatchCab3D", _root, position + new Vector3(0f, 0f, -1.18f * scale), new Vector3(0.72f, 0.58f, 0.42f), Color.Lerp(accent, Color.black, 0.55f), 0.48f, 0.32f);
            Runtime3DFactory.Box("WatchGlass3D", _root, position + new Vector3(0f, -0.30f, -1.18f * scale), new Vector3(0.42f, 0.035f, 0.14f), accent, 0.02f, 0.92f);
        }

        private Transform BuildLightningMast(Vector3 position, Color accent, float scale)
        {
            var root = new GameObject("LightningMast3D");
            root.transform.SetParent(_root, false);
            root.transform.localPosition = position;
            Runtime3DFactory.Box("Mast3D", root.transform, new Vector3(0f, 0f, -0.75f * scale), new Vector3(0.11f, 0.11f, 1.55f * scale), new Color(0.27f, 0.31f, 0.36f), 0.66f, 0.30f);
            Runtime3DFactory.Cylinder("Coil3D", root.transform, new Vector3(0f, 0f, -1.52f * scale), 0.34f, 0.10f, accent, 0.22f, 0.90f);
            return root.transform;
        }

        private void BuildArch(Vector3 position, Color material, float scale)
        {
            Runtime3DFactory.Box("ArchL3D", _root, position + new Vector3(-0.62f * scale, 0f, -0.62f * scale), new Vector3(0.28f * scale, 0.32f, 1.25f * scale), material, 0.46f, 0.25f);
            Runtime3DFactory.Box("ArchR3D", _root, position + new Vector3(0.62f * scale, 0f, -0.62f * scale), new Vector3(0.28f * scale, 0.32f, 1.25f * scale), material, 0.46f, 0.25f);
            Runtime3DFactory.Box("ArchTop3D", _root, position + new Vector3(0f, 0f, -1.24f * scale), new Vector3(1.52f * scale, 0.34f, 0.28f * scale), material, 0.46f, 0.25f);
        }

        private void AnimateSector()
        {
            float time = Time.unscaledTime;
            for (int i = _animated.Count - 1; i >= 0; i--)
            {
                Transform t = _animated[i];
                if (t == null) { _animated.RemoveAt(i); _phases.RemoveAt(i); continue; }
                float phase = _phases[i];
                float pulse = 0.90f + 0.10f * Mathf.Sin(time * (2.2f + _sector * 0.12f) + phase);
                t.localScale = Vector3.one * pulse;
                if (_sector == 4)
                    t.localPosition += new Vector3(Mathf.Sin(time * 0.8f + phase) * 0.0008f, 0f, 0f);
            }
        }

        private static float Rand(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        private void EnsureStyles()
        {
            if (_badge != null) return;
            _badge = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
            _small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10, normal = { textColor = new Color(0.80f, 0.86f, 0.92f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _sector < 0 || Time.unscaledTime >= _bannerUntil) return;
            EnsureStyles();
            Color accent = Accent[_sector];
            _badge.normal.textColor = accent;
            float y = Screen.height * 0.25f + 34f;
            GUI.color = new Color(0.01f, 0.015f, 0.025f, 0.80f);
            GUI.Box(new Rect(Screen.width * 0.5f - 260f, y, 520f, 48f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * 0.5f - 250f, y + 3f, 500f, 24f), "3D THEATER // " + Names[_sector], _badge);
            GUI.Label(new Rect(Screen.width * 0.5f - 250f, y + 25f, 500f, 16f), "Sector architecture rebuilt for this theater", _small);
        }
    }
}
