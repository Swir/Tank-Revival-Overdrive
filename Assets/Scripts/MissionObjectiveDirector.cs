using UnityEngine;

namespace TankRevival
{
    public enum MissionObjectiveType
    {
        HoldRelay,
        ArmorBreak,
        CounterBattery,
        SupplyInterdiction,
        EagleShield,
        CommanderHunt
    }

    /// <summary>
    /// v3.6 mission layer. Every non-boss round receives a deterministic battlefield directive
    /// that is completed through existing combat, movement and Eagle-defense systems.
    /// Objectives never replace TankGame's authoritative round-clear rules; they add tactical
    /// pressure, optional rewards and short-lived battlefield advantages.
    /// </summary>
    [DefaultExecutionOrder(3300)]
    public sealed class MissionObjectiveDirector : MonoBehaviour
    {
        private TankGame _game;
        private int _round;
        private MissionObjectiveType _type;
        private bool _active;
        private bool _completed;
        private bool _failed;
        private int _progress;
        private int _target;
        private float _holdProgress;
        private float _startedAt;
        private int _eagleStartHp;
        private int _lastPriorityCount;
        private int _lastSupplyCount;
        private EnemyTank _markedTarget;
        private GameObject _relay;
        private string _status = string.Empty;
        private string _rewardText = string.Empty;
        private GUIStyle _title, _body, _good, _bad;

        public static MissionObjectiveDirector Instance { get; private set; }
        public static string CurrentStatus => Instance != null ? Instance._status : "NO ACTIVE DIRECTIVE";
        public static bool CurrentCompleted => Instance != null && Instance._completed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<MissionObjectiveDirector>() != null) return;
            var go = new GameObject("MissionObjectiveDirector_v3_6");
            DontDestroyOnLoad(go);
            go.AddComponent<MissionObjectiveDirector>();
        }

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying)
            {
                ResetMission();
                return;
            }

            if (_round != _game.CurrentRound)
                BeginMission(_game.CurrentRound);

            if (!_active || _completed || _failed) return;

            switch (_type)
            {
                case MissionObjectiveType.HoldRelay: UpdateHoldRelay(); break;
                case MissionObjectiveType.ArmorBreak: UpdatePriorityKills(false); break;
                case MissionObjectiveType.CounterBattery: UpdatePriorityKills(true); break;
                case MissionObjectiveType.SupplyInterdiction: UpdateSupplyInterdiction(); break;
                case MissionObjectiveType.EagleShield: UpdateEagleShield(); break;
                case MissionObjectiveType.CommanderHunt: UpdateCommanderHunt(); break;
            }
        }

        private void BeginMission(int round)
        {
            ClearRuntimeObjects();
            _round = round;
            _startedAt = Time.time;
            _completed = false;
            _failed = false;
            _progress = 0;
            _holdProgress = 0f;
            _markedTarget = null;
            _rewardText = string.Empty;

            if (round % 10 == 0)
            {
                _active = false;
                _status = "BOSS OPERATION // LEGEND ENGAGEMENT";
                return;
            }

            _active = true;
            _type = SelectType(round);
            Health eagle = CombatRoster.Eagle;
            _eagleStartHp = eagle != null ? eagle.Current : 6;
            _lastPriorityCount = PriorityCount(_type == MissionObjectiveType.CounterBattery);
            _lastSupplyCount = CombatRoster.Count(EnemyKind.Supply);

            switch (_type)
            {
                case MissionObjectiveType.HoldRelay:
                    _target = Mathf.Clamp(8 + round / 18, 8, 13);
                    SpawnRelay(round);
                    _status = $"HOLD RELAY // secure uplink {_target}s";
                    break;
                case MissionObjectiveType.ArmorBreak:
                    _target = Mathf.Clamp(2 + round / 24, 2, 5);
                    _status = $"ARMOR BREAK // destroy {_target} Heavy/Elite/Siege units";
                    break;
                case MissionObjectiveType.CounterBattery:
                    _target = Mathf.Clamp(2 + round / 30, 2, 4);
                    _status = $"COUNTER-BATTERY // neutralize {_target} Sniper/Siege units";
                    break;
                case MissionObjectiveType.SupplyInterdiction:
                    _target = 1;
                    _status = "SUPPLY INTERDICTION // destroy a supply carrier";
                    break;
                case MissionObjectiveType.EagleShield:
                    _target = Mathf.Clamp(20 + round / 4, 20, 42);
                    _status = $"EAGLE SHIELD // keep Orzełek undamaged for {_target}s";
                    break;
                default:
                    _target = 1;
                    _status = "COMMANDER HUNT // eliminate marked priority armor";
                    break;
            }
        }

        private static MissionObjectiveType SelectType(int round)
        {
            int sector = (round - 1) / 10;
            int slot = (round + sector * 3) % 6;
            return (MissionObjectiveType)slot;
        }

        private void UpdateHoldRelay()
        {
            if (_relay == null) SpawnRelay(_round);
            PlayerTank player = CombatRoster.Player;
            if (player == null || _relay == null) return;

            float distance = Vector2.Distance(player.transform.position, _relay.transform.position);
            if (distance <= 1.65f)
            {
                _holdProgress += Time.deltaTime;
                if (Time.frameCount % 35 == 0)
                    VisualFactory.RingPulse(_relay.transform.position, new Color(0.18f, 0.92f, 1f), 0.55f);
            }
            else
            {
                _holdProgress = Mathf.Max(0f, _holdProgress - Time.deltaTime * 0.25f);
            }

            _progress = Mathf.FloorToInt(_holdProgress);
            _status = $"HOLD RELAY // {_holdProgress:0.0}/{_target}s // range {distance:0.0}m";
            if (_holdProgress >= _target) Complete("RELAY SECURED", 5, true);
        }

        private void UpdatePriorityKills(bool artilleryOnly)
        {
            int current = PriorityCount(artilleryOnly);
            if (current < _lastPriorityCount)
                _progress += _lastPriorityCount - current;
            _lastPriorityCount = current;

            string label = artilleryOnly ? "COUNTER-BATTERY" : "ARMOR BREAK";
            _status = $"{label} // {_progress}/{_target}";
            if (_progress >= _target) Complete(label + " COMPLETE", artilleryOnly ? 6 : 5, artilleryOnly);
        }

        private int PriorityCount(bool artilleryOnly)
        {
            if (artilleryOnly)
                return CombatRoster.Count(EnemyKind.Sniper) + CombatRoster.Count(EnemyKind.Siege);
            return CombatRoster.Count(EnemyKind.Heavy) + CombatRoster.Count(EnemyKind.Siege) + CombatRoster.Count(EnemyKind.Elite);
        }

        private void UpdateSupplyInterdiction()
        {
            int current = CombatRoster.Count(EnemyKind.Supply);
            if (_lastSupplyCount > 0 && current < _lastSupplyCount)
                _progress += _lastSupplyCount - current;
            _lastSupplyCount = current;

            _status = current > 0 ? "SUPPLY INTERDICTION // carrier on battlefield" : "SUPPLY INTERDICTION // awaiting carrier";
            if (_progress >= 1) Complete("SUPPLY LINE BROKEN", 6, false);

            // Avoid unwinnable optional directives on rounds where RNG never produces a carrier.
            if (Time.time - _startedAt > 32f && current == 0 && _progress == 0)
            {
                _type = MissionObjectiveType.ArmorBreak;
                _target = 2;
                _lastPriorityCount = PriorityCount(false);
                _status = "DIRECTIVE SHIFT // destroy 2 priority armor units";
            }
        }

        private void UpdateEagleShield()
        {
            Health eagle = CombatRoster.Eagle;
            if (eagle == null || eagle.IsDead) { Fail("ORZEŁEK LOST"); return; }
            if (eagle.Current < _eagleStartHp) { Fail("EAGLE SHIELD BROKEN"); return; }

            int elapsed = Mathf.FloorToInt(Time.time - _startedAt);
            _progress = elapsed;
            _status = $"EAGLE SHIELD // {Mathf.Min(elapsed, _target)}/{_target}s without damage";
            if (elapsed >= _target) Complete("EAGLE SHIELD HELD", 7, true);
        }

        private void UpdateCommanderHunt()
        {
            if (_markedTarget == null)
            {
                EnemyTank candidate = PickPriorityTarget();
                if (candidate != null)
                {
                    _markedTarget = candidate;
                    if (candidate.GetComponent<MissionTargetMarker>() == null)
                        candidate.gameObject.AddComponent<MissionTargetMarker>();
                    _status = $"COMMANDER HUNT // marked {candidate.Kind}";
                }
                else if (Time.time - _startedAt > 24f)
                {
                    _type = MissionObjectiveType.HoldRelay;
                    _target = 9;
                    SpawnRelay(_round);
                    _status = "DIRECTIVE SHIFT // secure emergency relay";
                }
                return;
            }

            if (_markedTarget.Health == null || _markedTarget.Health.IsDead)
                Complete("COMMAND TARGET DESTROYED", 8, true);
            else
                _status = $"COMMANDER HUNT // {_markedTarget.Kind} HP {_markedTarget.Health.Current}/{_markedTarget.Health.Maximum}";
        }

        private EnemyTank PickPriorityTarget()
        {
            EnemyTank best = null;
            int score = -1;
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Kind == EnemyKind.Supply) continue;
                int value = enemy.Kind switch
                {
                    EnemyKind.Elite => 5,
                    EnemyKind.Siege => 4,
                    EnemyKind.Heavy => 3,
                    EnemyKind.Sniper => 2,
                    EnemyKind.Fast => 1,
                    _ => 0
                };
                if (value > score) { score = value; best = enemy; }
            }
            return best;
        }

        private void Complete(string label, int baseBonds, bool supportPulse)
        {
            if (_completed || _failed) return;
            _completed = true;
            int reward = baseBonds + _round / 25;
            WarEconomyDirector.AwardMissionBonds(reward, label);
            _rewardText = $"{label} // +{reward} WAR BONDS";

            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;
            if (supportPulse)
            {
                if (player != null && player.Health != null)
                {
                    player.Health.Heal(1);
                    player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 1.2f);
                    player.AddAmmo(AmmoType.ArmorPiercing, 2);
                    VisualFactory.RingPulse(player.transform.position, new Color(0.20f, 1f, 0.58f), 1.05f);
                }
                if (eagle != null && !eagle.IsDead)
                    eagle.InvulnerableUntil = Mathf.Max(eagle.InvulnerableUntil, Time.time + 1.5f);
            }

            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.48f, 0.03f);
            if (_relay != null) Destroy(_relay);
        }

        private void Fail(string reason)
        {
            if (_completed || _failed) return;
            _failed = true;
            _rewardText = reason + " // BONUS LOST";
            if (_relay != null) Destroy(_relay);
        }

        private void SpawnRelay(int round)
        {
            if (_relay != null) Destroy(_relay);
            var go = new GameObject("MISSION_RELAY_v3_6");
            float side = ((round / 2) % 2 == 0) ? -1f : 1f;
            go.transform.position = new Vector3(side * (4.2f + (round % 3) * 0.7f), -0.5f + (round % 4) * 0.65f, 0f);
            go.AddComponent<MissionRelayPresentation>();
            _relay = go;
        }

        private void ResetMission()
        {
            if (_round == 0) return;
            _round = 0;
            _active = false;
            _status = "NO ACTIVE DIRECTIVE";
            ClearRuntimeObjects();
        }

        private void ClearRuntimeObjects()
        {
            if (_relay != null) Destroy(_relay);
            _relay = null;
            _markedTarget = null;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(0.30f, 0.90f, 1f);
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            _body.normal.textColor = new Color(0.86f, 0.92f, 0.98f);
            _good = new GUIStyle(_body) { fontStyle = FontStyle.Bold };
            _good.normal.textColor = new Color(0.34f, 1f, 0.56f);
            _bad = new GUIStyle(_body) { fontStyle = FontStyle.Bold };
            _bad.normal.textColor = new Color(1f, 0.40f, 0.24f);
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || !_active) return;
            EnsureStyles();
            float w = 510f;
            float x = Screen.width * 0.5f - w * 0.5f;
            GUI.color = new Color(0.015f, 0.032f, 0.050f, 0.94f);
            GUI.Box(new Rect(x, 10f, w, 62f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 14f, 16f, w - 28f, 20f), $"MISSION DIRECTIVE // ROUND {_round:000}", _title);
            GUI.Label(new Rect(x + 14f, 39f, w - 28f, 20f), _completed || _failed ? _rewardText : _status, _completed ? _good : _failed ? _bad : _body);
        }
    }

    public sealed class MissionTargetMarker : MonoBehaviour
    {
        private void Update()
        {
            if (Time.frameCount % 35 == 0)
                VisualFactory.RingPulse(transform.position, new Color(1f, 0.28f, 0.10f), 0.72f);
        }
    }

    public sealed class MissionRelayPresentation : MonoBehaviour
    {
        private float _phase;

        private void Awake()
        {
            VisualFactory.Disc("RelayZone", transform, new Vector2(3.25f, 3.25f), new Color(0.08f, 0.62f, 0.90f, 0.12f), Vector3.zero, -3);
            VisualFactory.Disc("RelayCore", transform, new Vector2(0.65f, 0.65f), new Color(0.18f, 0.92f, 1f, 0.85f), Vector3.zero, 7);
            VisualFactory.Rect("RelayMast", transform, new Vector2(0.16f, 1.30f), new Color(0.62f, 0.86f, 0.94f), new Vector3(0f, 0.52f, 0f), 6);
        }

        private void Update()
        {
            _phase += Time.deltaTime * 2.4f;
            transform.localScale = Vector3.one * (1f + Mathf.Sin(_phase) * 0.035f);
        }
    }
}
