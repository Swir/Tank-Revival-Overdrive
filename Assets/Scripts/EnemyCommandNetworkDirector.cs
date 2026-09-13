using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum CommandRole
    {
        Suppressor,
        Escort,
        FortressBreaker
    }

    /// <summary>
    /// v5.9 tactical overlay. It never replaces EnemyTank movement/fire authority; instead it
    /// selects already-living enemies from RuntimeBattleRegistry and gives a small command cell
    /// an explicit, telegraphed mission. The breaker can be killed or AEGIS-countered before its
    /// siege strike lands, while suppressors create player pressure and escorts harden the cell.
    /// </summary>
    public sealed class EnemyCommandNetworkDirector : MonoBehaviour
    {
        public const float TelegraphSeconds = 5.5f;
        public const float OperationCooldown = 17f;
        public const float SuppressionCadence = 2.35f;
        public const float EscortPulseCadence = 4.5f;
        public const int MaxAssignedUnits = 4;
        public const int SiegeVolleyCount = 3;
        public const int EarliestRound = 18;

        private TankGame _game;
        private Health _eagle;
        private readonly List<RoleAssignment> _cell = new List<RoleAssignment>(MaxAssignedUnits);
        private float _nextOperation;
        private float _strikeAt;
        private float _nextSuppress;
        private float _nextEscortPulse;
        private int _lastRound = -1;
        private int _operationsStarted;
        private int _operationsCountered;
        private string _operation = "STANDBY";
        private string _outcome = string.Empty;
        private float _outcomeUntil;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _warning;

        public static int RoleCount => Enum.GetValues(typeof(CommandRole)).Length;
        public static bool ConfigurationValid => TelegraphSeconds >= 4f && TelegraphSeconds <= 8f && OperationCooldown >= 12f && OperationCooldown <= 25f && SuppressionCadence >= 1.5f && MaxAssignedUnits >= 3 && MaxAssignedUnits <= 5 && SiegeVolleyCount >= 2 && SiegeVolleyCount <= 4;
        public static bool ShieldCountersStrike(float invulnerableUntil, float now) => invulnerableUntil > now + 0.15f;
        public int ActiveAssignments => _cell.Count;
        public int OperationsStarted => _operationsStarted;
        public int OperationsCountered => _operationsCountered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EnemyCommandNetworkDirector>() != null) return;
            var go = new GameObject("EnemyCommandNetworkDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<EnemyCommandNetworkDirector>();
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            _eagle = CombatRoster.Eagle;
            if (_game == null || !_game.IsPlaying || _eagle == null || _eagle.IsDead)
            {
                ClearCell();
                return;
            }

            int round = _game.CurrentRound;
            if (round != _lastRound)
            {
                _lastRound = round;
                ClearCell();
                _nextOperation = Time.time + Mathf.Lerp(8f, 4.5f, Mathf.Clamp01((round - EarliestRound) / 82f));
            }

            PruneDeadAssignments();
            if (_cell.Count > 0)
            {
                RunActiveOperation();
                return;
            }

            if (round >= EarliestRound && Time.time >= _nextOperation && RuntimeBattleRegistry.RegisteredEnemyCount >= 3)
                TryStartOperation(round);
        }

        private void TryStartOperation(int round)
        {
            EnemyTank[] snapshot = RuntimeBattleRegistry.EnemySnapshot;
            EnemyTank breaker = Pick(snapshot, EnemyKind.Siege, EnemyKind.Heavy, EnemyKind.Elite);
            if (breaker == null) { _nextOperation = Time.time + 4f; return; }

            EnemyTank suppressor = PickDifferent(snapshot, breaker, EnemyKind.Sniper, EnemyKind.Elite, EnemyKind.Fast, EnemyKind.Basic);
            EnemyTank escortA = PickDifferent(snapshot, breaker, suppressor, EnemyKind.Heavy, EnemyKind.Basic, EnemyKind.Fast);
            EnemyTank escortB = PickDifferent(snapshot, breaker, suppressor, escortA, EnemyKind.Elite, EnemyKind.Heavy, EnemyKind.Basic);

            Assign(breaker, CommandRole.FortressBreaker);
            if (suppressor != null) Assign(suppressor, CommandRole.Suppressor);
            if (escortA != null) Assign(escortA, CommandRole.Escort);
            if (escortB != null && _cell.Count < MaxAssignedUnits) Assign(escortB, CommandRole.Escort);

            if (_cell.Count < 3)
            {
                ClearCell();
                _nextOperation = Time.time + 5f;
                return;
            }

            _operationsStarted++;
            _operation = round >= 70 ? "CITADEL BREAK" : round >= 42 ? "IRON SPEAR" : "EAGLE BREACH";
            _strikeAt = Time.time + TelegraphSeconds;
            _nextSuppress = Time.time + 0.75f;
            _nextEscortPulse = Time.time + 1.25f;
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.34f, 0.02f);

            RoleAssignment activeBreaker = FindRole(CommandRole.FortressBreaker);
            if (activeBreaker != null && activeBreaker.Enemy != null)
                VisualFactory.RingPulse(activeBreaker.Enemy.transform.position, new Color(1f, 0.20f, 0.07f), 1.65f);
        }

        private void RunActiveOperation()
        {
            RoleAssignment breaker = FindRole(CommandRole.FortressBreaker);
            if (breaker == null || breaker.Enemy == null || breaker.Enemy.Health == null || breaker.Enemy.Health.IsDead)
            {
                Resolve("BREAKER DESTROYED // SIEGE CELL COLLAPSED", true);
                return;
            }

            if (Time.time >= _nextSuppress)
            {
                FireSuppression();
                _nextSuppress = Time.time + SuppressionCadence;
            }

            if (Time.time >= _nextEscortPulse)
            {
                PulseEscorts();
                _nextEscortPulse = Time.time + EscortPulseCadence;
            }

            if (Time.time < _strikeAt) return;

            if (ShieldCountersStrike(_eagle.InvulnerableUntil, Time.time))
            {
                VisualFactory.RingPulse(_eagle.transform.position, new Color(0.20f, 0.86f, 1f), 2.1f);
                Resolve("AEGIS INTERCEPT // FORTRESS STRIKE DISRUPTED", true);
                return;
            }

            FireSiegeVolley(breaker.Enemy);
            Resolve("FORTRESS IMPACT // COMMAND CELL WITHDRAWING", false);
        }

        private void FireSuppression()
        {
            RoleAssignment suppressor = FindRole(CommandRole.Suppressor);
            if (suppressor == null || suppressor.Enemy == null || suppressor.Enemy.Health == null || suppressor.Enemy.Health.IsDead) return;
            Vector2 from = suppressor.Enemy.transform.position;
            Vector2 target = _game.PlayerPosition;
            Vector2 direction = (target - from).normalized;
            if (direction.sqrMagnitude < 0.1f) return;
            _game.SpawnProjectile(from + direction * 0.72f, direction, Team.Enemy, 1, 10.8f, new Color(1f, 0.68f, 0.10f), AmmoType.Basic);
            VisualFactory.MuzzleFlash(from + direction * 0.72f, new Color(1f, 0.68f, 0.10f), 0.72f);
        }

        private void PulseEscorts()
        {
            for (int i = 0; i < _cell.Count; i++)
            {
                RoleAssignment assignment = _cell[i];
                if (assignment.Role != CommandRole.Escort || assignment.Enemy == null || assignment.Enemy.Health == null || assignment.Enemy.Health.IsDead) continue;
                Health h = assignment.Enemy.Health;
                if (!assignment.CapacityGranted)
                {
                    h.SetMaximum(Mathf.Min(h.Maximum + 1, Mathf.CeilToInt(h.Maximum * 1.20f)), true);
                    assignment.CapacityGranted = true;
                }
                VisualFactory.RingPulse(assignment.Enemy.transform.position, new Color(0.92f, 0.48f, 0.10f), 0.72f);
            }
        }

        private void FireSiegeVolley(EnemyTank breaker)
        {
            Vector2 from = breaker.transform.position;
            Vector2 target = _game.BasePosition;
            Vector2 direction = (target - from).normalized;
            if (direction.sqrMagnitude < 0.1f) direction = Vector2.down;
            Vector2 side = new Vector2(-direction.y, direction.x);

            for (int i = 0; i < SiegeVolleyCount; i++)
            {
                float spread = (i - (SiegeVolleyCount - 1) * 0.5f) * 0.12f;
                Vector2 shot = (direction + side * spread).normalized;
                _game.SpawnProjectile(from + shot * 0.84f, shot, Team.Enemy, 2, 8.7f, new Color(1f, 0.16f, 0.035f), AmmoType.Explosive);
            }
            VisualFactory.MuzzleFlash(from + direction * 0.84f, new Color(1f, 0.16f, 0.035f), 1.35f);
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.46f, 0.03f);
        }

        private void Resolve(string text, bool countered)
        {
            if (countered) _operationsCountered++;
            _outcome = text;
            _outcomeUntil = Time.unscaledTime + 3.2f;
            ClearCell();
            _nextOperation = Time.time + OperationCooldown;
        }

        private void Assign(EnemyTank enemy, CommandRole role)
        {
            if (enemy == null || _cell.Count >= MaxAssignedUnits) return;
            var assignment = new RoleAssignment(enemy, role);
            _cell.Add(assignment);
            var marker = enemy.gameObject.GetComponent<CommandRoleMarker>();
            if (marker == null) marker = enemy.gameObject.AddComponent<CommandRoleMarker>();
            marker.SetRole(role);
        }

        private void ClearCell()
        {
            for (int i = 0; i < _cell.Count; i++)
            {
                if (_cell[i].Enemy == null) continue;
                CommandRoleMarker marker = _cell[i].Enemy.GetComponent<CommandRoleMarker>();
                if (marker != null) Destroy(marker);
            }
            _cell.Clear();
            _operation = "STANDBY";
            _strikeAt = 0f;
        }

        private void PruneDeadAssignments()
        {
            for (int i = _cell.Count - 1; i >= 0; i--)
            {
                RoleAssignment a = _cell[i];
                if (a.Enemy == null || a.Enemy.Health == null || a.Enemy.Health.IsDead) _cell.RemoveAt(i);
            }
        }

        private RoleAssignment FindRole(CommandRole role)
        {
            for (int i = 0; i < _cell.Count; i++) if (_cell[i].Role == role) return _cell[i];
            return null;
        }

        private static EnemyTank Pick(EnemyTank[] enemies, params EnemyKind[] preference)
        {
            for (int p = 0; p < preference.Length; p++)
                for (int i = 0; i < enemies.Length; i++)
                    if (Valid(enemies[i]) && enemies[i].Kind == preference[p]) return enemies[i];
            return null;
        }

        private static EnemyTank PickDifferent(EnemyTank[] enemies, EnemyTank excludeA, params EnemyKind[] preference)
        {
            return PickDifferent(enemies, excludeA, null, null, preference);
        }

        private static EnemyTank PickDifferent(EnemyTank[] enemies, EnemyTank excludeA, EnemyTank excludeB, params EnemyKind[] preference)
        {
            return PickDifferent(enemies, excludeA, excludeB, null, preference);
        }

        private static EnemyTank PickDifferent(EnemyTank[] enemies, EnemyTank excludeA, EnemyTank excludeB, EnemyTank excludeC, params EnemyKind[] preference)
        {
            for (int p = 0; p < preference.Length; p++)
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank e = enemies[i];
                    if (!Valid(e) || e == excludeA || e == excludeB || e == excludeC || e.Kind != preference[p]) continue;
                    return e;
                }
            return null;
        }

        private static bool Valid(EnemyTank e) => e != null && e.Health != null && !e.Health.IsDead && e.Kind != EnemyKind.Boss && e.Kind != EnemyKind.Supply;

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            float width = 390f;
            Rect box = new Rect(Screen.width - width - 18f, 128f, width, _cell.Count > 0 ? 112f : 72f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 12f, box.y + 8f, width - 24f, 24f), "ENEMY COMMAND NETWORK // " + _operation, _header);

            if (_cell.Count > 0)
            {
                float countdown = Mathf.Max(0f, _strikeAt - Time.time);
                GUI.Label(new Rect(box.x + 12f, box.y + 34f, width - 24f, 22f), "ROLES: " + RoleSummary() + "   CELL " + _cell.Count + "/" + MaxAssignedUnits, _body);
                GUI.Label(new Rect(box.x + 12f, box.y + 58f, width - 24f, 24f), "FORTRESS STRIKE IN " + countdown.ToString("0.0") + "s // KILL BREAKER OR DEPLOY AEGIS", _warning);
            }
            else if (Time.unscaledTime < _outcomeUntil)
            {
                GUI.Label(new Rect(box.x + 12f, box.y + 37f, width - 24f, 24f), _outcome, _body);
            }
            else
            {
                GUI.Label(new Rect(box.x + 12f, box.y + 37f, width - 24f, 24f), "No coordinated siege cell detected.", _body);
            }
        }

        private string RoleSummary()
        {
            int s = 0, e = 0, b = 0;
            for (int i = 0; i < _cell.Count; i++)
            {
                if (_cell[i].Role == CommandRole.Suppressor) s++;
                else if (_cell[i].Role == CommandRole.Escort) e++;
                else if (_cell[i].Role == CommandRole.FortressBreaker) b++;
            }
            return "SUP " + s + " / ESC " + e + " / BRK " + b;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(1f, 0.48f, 0.16f);
            _body = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _body.normal.textColor = new Color(0.80f, 0.90f, 1f);
            _warning = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _warning.normal.textColor = new Color(1f, 0.28f, 0.12f);
        }

        private sealed class RoleAssignment
        {
            public readonly EnemyTank Enemy;
            public readonly CommandRole Role;
            public bool CapacityGranted;
            public RoleAssignment(EnemyTank enemy, CommandRole role) { Enemy = enemy; Role = role; }
        }
    }

    public sealed class CommandRoleMarker : MonoBehaviour
    {
        private CommandRole _role;
        private float _nextPulse;
        public CommandRole Role => _role;
        public void SetRole(CommandRole role) { _role = role; _nextPulse = Time.time; }
        private void Update()
        {
            if (Time.time < _nextPulse) return;
            _nextPulse = Time.time + 1.8f;
            Color c = _role == CommandRole.FortressBreaker ? new Color(1f, 0.16f, 0.04f) : _role == CommandRole.Suppressor ? new Color(1f, 0.68f, 0.08f) : new Color(0.72f, 0.22f, 1f);
            VisualFactory.RingPulse(transform.position, c, _role == CommandRole.FortressBreaker ? 1.25f : 0.78f);
        }
    }
}
