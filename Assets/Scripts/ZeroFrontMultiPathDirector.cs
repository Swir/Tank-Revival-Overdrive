using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v4.3 endgame specialization layer. Converts the campaign approach produced by v4.2
    /// into three materially different Zero Front doctrines for rounds 91-100 without
    /// replacing the authoritative round, spawn, health, economy or projectile systems.
    /// </summary>
    [DefaultExecutionOrder(6200)]
    public sealed class ZeroFrontMultiPathDirector : MonoBehaviour
    {
        private enum Doctrine
        {
            IronShield = 0,
            Breakthrough = 1,
            GhostSpear = 2
        }

        private enum ObjectiveKind
        {
            HoldTheLine = 0,
            BreakArmor = 1,
            PriorityHunt = 2
        }

        private TankGame _game;
        private Doctrine _doctrine;
        private int _round;
        private int _runId;
        private bool _wasPlaying;
        private bool _objectiveComplete;
        private bool _objectiveCompromised;
        private int _objectiveProgress;
        private int _objectiveTarget;
        private int _roundKills;
        private int _roundPriorityKills;
        private int _completedObjectives;
        private int _doctrineScore;
        private float _scanAt;
        private float _bannerUntil;
        private string _banner = string.Empty;
        private readonly HashSet<int> _hooked = new HashSet<int>();
        private readonly HashSet<int> _reinforced = new HashSet<int>();

        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _bannerStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ZeroFrontMultiPathDirector>() != null) return;
            var go = new GameObject("ZeroFrontMultiPathDirector_v4_3");
            DontDestroyOnLoad(go);
            go.AddComponent<ZeroFrontMultiPathDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            bool playing = _game.IsPlaying;
            if (playing && !_wasPlaying)
                BeginRun();
            else if (!playing && _wasPlaying)
                EndRun();
            _wasPlaying = playing;

            if (!playing || _game.CurrentRound < 91) return;

            if (_game.CurrentRound != _round)
            {
                FinishPreviousObjective();
                BeginFinaleRound(_game.CurrentRound);
            }

            if (Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.25f;
                HookEnemies();
                ApplyDoctrinePressure();
                TickDoctrineState();
            }
        }

        private void BeginRun()
        {
            _round = 0;
            _runId = PlayerPrefs.GetInt("TankRevival.CampaignEventRunId", 0);
            _doctrine = ResolveDoctrine(CampaignEventDirector.CurrentApproach);
            _completedObjectives = 0;
            _doctrineScore = 0;
            _hooked.Clear();
            _reinforced.Clear();
        }

        private void EndRun()
        {
            _round = 0;
            _hooked.Clear();
            _reinforced.Clear();
        }

        private Doctrine ResolveDoctrine(string approach)
        {
            if (approach == "BREAKTHROUGH") return Doctrine.Breakthrough;
            if (approach == "GHOST SPEAR") return Doctrine.GhostSpear;
            return Doctrine.IronShield;
        }

        private void BeginFinaleRound(int round)
        {
            _round = round;
            _roundKills = 0;
            _roundPriorityKills = 0;
            _objectiveProgress = 0;
            _objectiveComplete = false;
            _objectiveCompromised = false;
            _hooked.Clear();
            _reinforced.Clear();

            if (round <= 99)
            {
                _objectiveTarget = ObjectiveTargetFor(round);
                Announce($"ZERO FRONT // {DoctrineName()} // {ObjectiveTitle()} {_objectiveTarget}", 4.2f);
            }
            else
            {
                _objectiveTarget = 0;
                GrantFinalEntryPackage();
                Announce($"ZERO FRONT // {DoctrineName()} // FINAL ASSAULT", 5.2f);
            }

            GrantRoundDoctrineSupport(round);
        }

        private ObjectiveKind CurrentObjective()
        {
            switch (_doctrine)
            {
                case Doctrine.Breakthrough: return ObjectiveKind.BreakArmor;
                case Doctrine.GhostSpear: return ObjectiveKind.PriorityHunt;
                default: return ObjectiveKind.HoldTheLine;
            }
        }

        private int ObjectiveTargetFor(int round)
        {
            int local = Mathf.Clamp(round - 90, 1, 9);
            switch (CurrentObjective())
            {
                case ObjectiveKind.BreakArmor:
                    return 2 + local / 3;
                case ObjectiveKind.PriorityHunt:
                    return 2 + local / 4;
                default:
                    return 6 + local / 2;
            }
        }

        private void HookEnemies()
        {
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null) continue;
                int id = enemy.GetInstanceID();
                if (!_hooked.Add(id)) continue;
                EnemyTank captured = enemy;
                enemy.Health.Died += _ => OnEnemyDestroyed(captured);
            }
        }

        private void OnEnemyDestroyed(EnemyTank enemy)
        {
            if (enemy == null || _game == null || !_game.IsPlaying || _round < 91) return;

            _roundKills++;
            bool armored = enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss;
            bool priority = enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Sniper || enemy.Kind == EnemyKind.Supply || enemy.Kind == EnemyKind.Boss;
            if (armored || priority) _roundPriorityKills++;

            switch (CurrentObjective())
            {
                case ObjectiveKind.BreakArmor:
                    if (armored) _objectiveProgress++;
                    else if (_roundKills % 5 == 0) _objectiveProgress++;
                    break;
                case ObjectiveKind.PriorityHunt:
                    if (priority) _objectiveProgress++;
                    else if (_roundKills % 6 == 0) _objectiveProgress++;
                    break;
                default:
                    _objectiveProgress++;
                    break;
            }

            ApplyKillSupport(enemy, armored, priority);
            if (!_objectiveComplete && _round <= 99 && _objectiveProgress >= _objectiveTarget)
                CompleteObjective();
        }

        private void ApplyKillSupport(EnemyTank enemy, bool armored, bool priority)
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null) return;

            if (_doctrine == Doctrine.IronShield)
            {
                if (_roundKills > 0 && _roundKills % 6 == 0)
                {
                    _game.RepairEagle(1);
                    if (player.Health != null && _roundKills % 12 == 0) player.Health.Heal(1);
                }
            }
            else if (_doctrine == Doctrine.Breakthrough)
            {
                if (armored)
                {
                    _doctrineScore++;
                    if (_doctrineScore % 3 == 0)
                    {
                        player.AddAmmo(AmmoType.Explosive, 1);
                        player.AddAmmo(AmmoType.ArmorPiercing, 1);
                    }
                }
            }
            else
            {
                if (priority)
                {
                    _doctrineScore++;
                    if (_doctrineScore % 2 == 0) player.AddAmmo(AmmoType.EMP, 1);
                    if (_doctrineScore % 4 == 0)
                        WarEconomyDirector.AwardMissionBonds(2, "GHOST SPEAR PRIORITY KILL");
                }
            }
        }

        private void ApplyDoctrinePressure()
        {
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                int id = enemy.GetInstanceID();
                if (!_reinforced.Add(id)) continue;

                float factor = 1f;
                if (_doctrine == Doctrine.IronShield && (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege))
                    factor = 1.08f;
                else if (_doctrine == Doctrine.Breakthrough && (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss))
                    factor = 1.14f;
                else if (_doctrine == Doctrine.GhostSpear && (enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Sniper || enemy.Kind == EnemyKind.Supply))
                    factor = 1.12f;

                if (factor <= 1f) continue;
                int oldMax = enemy.Health.Max;
                int newMax = Mathf.Max(oldMax + 1, Mathf.CeilToInt(oldMax * factor));
                int delta = newMax - oldMax;
                enemy.Health.SetMaximum(newMax);
                if (delta > 0) enemy.Health.Heal(delta);
            }
        }

        private void TickDoctrineState()
        {
            if (_round > 99 || _objectiveComplete) return;

            if (_doctrine == Doctrine.IronShield)
            {
                Health eagle = CombatRoster.Eagle;
                if (eagle == null || eagle.Max <= 0) return;
                float ratio = (float)eagle.Current / eagle.Max;
                float floor = _round >= 97 ? 0.48f : 0.55f;
                if (ratio < floor)
                    _objectiveCompromised = true;
            }
        }

        private void CompleteObjective()
        {
            _objectiveComplete = true;
            if (_objectiveCompromised && _doctrine == Doctrine.IronShield)
            {
                Announce("IRON SHIELD // LINE HELD BUT ORZELEK INTEGRITY BONUS LOST", 3.2f);
                return;
            }

            _completedObjectives++;
            PlayerTank player = CombatRoster.Player;
            int reward = 4 + Mathf.Max(0, (_round - 91) / 2);
            WarEconomyDirector.AwardMissionBonds(reward, $"{DoctrineName()} OBJECTIVE");

            if (player != null)
            {
                if (_doctrine == Doctrine.IronShield)
                {
                    if (player.Health != null) player.Health.Heal(1);
                    _game.RepairEagle(1);
                    player.AddAmmo(AmmoType.EMP, 1);
                }
                else if (_doctrine == Doctrine.Breakthrough)
                {
                    player.AddAmmo(AmmoType.Explosive, 2);
                    player.AddAmmo(AmmoType.Plasma, _round >= 96 ? 1 : 0);
                }
                else
                {
                    player.AddAmmo(AmmoType.EMP, 2);
                    player.AddAmmo(AmmoType.ArmorPiercing, 2);
                }
            }

            Announce($"{DoctrineName()} // OBJECTIVE COMPLETE // +{reward} WAR BONDS", 3.2f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.62f, 0.04f);
        }

        private void FinishPreviousObjective()
        {
            if (_round < 91 || _round > 99 || _objectiveComplete) return;
            PlayerPrefs.SetInt($"TankRevival.ZeroFrontMissed.{_runId}.{_round}", 1);
            PlayerPrefs.Save();
        }

        private void GrantRoundDoctrineSupport(int round)
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null) return;

            if (_doctrine == Doctrine.IronShield)
            {
                if (round == 91 || round == 94 || round == 97)
                {
                    _game.RepairEagle(2);
                    if (player.Health != null) player.Health.Heal(1);
                    ArmorSystem armor = player.GetComponent<ArmorSystem>();
                    if (armor != null) armor.RepairModules(18 + (round - 90));
                }
            }
            else if (_doctrine == Doctrine.Breakthrough)
            {
                player.AddAmmo(AmmoType.ArmorPiercing, 1);
                if (round >= 94) player.AddAmmo(AmmoType.Explosive, 1);
                if (round >= 97 && round % 2 == 1) player.AddAmmo(AmmoType.Plasma, 1);
            }
            else
            {
                player.AddAmmo(AmmoType.EMP, 1);
                if (round == 93 || round == 96 || round == 99)
                    WarEconomyDirector.AwardMissionBonds(3, "GHOST SPEAR INTEL CACHE");
            }
        }

        private void GrantFinalEntryPackage()
        {
            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;
            if (player == null) return;

            if (_doctrine == Doctrine.IronShield)
            {
                _game.RepairEagle(4);
                if (player.Health != null)
                {
                    player.Health.Heal(3);
                    player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 4f);
                }
                if (eagle != null)
                    eagle.InvulnerableUntil = Mathf.Max(eagle.InvulnerableUntil, Time.time + 5f);
                player.AddAmmo(AmmoType.EMP, 3);
            }
            else if (_doctrine == Doctrine.Breakthrough)
            {
                player.AddAmmo(AmmoType.ArmorPiercing, 6);
                player.AddAmmo(AmmoType.Explosive, 5);
                player.AddAmmo(AmmoType.Plasma, 4);
                if (player.Health != null)
                    player.Health.InvulnerableUntil = Mathf.Max(player.Health.InvulnerableUntil, Time.time + 2.5f);
            }
            else
            {
                player.AddAmmo(AmmoType.EMP, 6);
                player.AddAmmo(AmmoType.ArmorPiercing, 6);
                player.AddAmmo(AmmoType.Plasma, 2);
                WarEconomyDirector.AwardMissionBonds(8, "GHOST SPEAR FINAL INTEL");
            }

            if (_completedObjectives >= 7)
            {
                WarEconomyDirector.AwardMissionBonds(15, "ZERO FRONT DOCTRINE MASTERY");
                PlayerPrefs.SetInt($"TankRevival.ZeroFrontDoctrineMastery.{_runId}", 1);
                PlayerPrefs.SetInt("TankRevival.ZeroFrontDoctrineMasteryBest", Mathf.Max(PlayerPrefs.GetInt("TankRevival.ZeroFrontDoctrineMasteryBest", 0), _completedObjectives));
                PlayerPrefs.Save();
                Announce($"{DoctrineName()} // DOCTRINE MASTERY // FINAL RESERVE AUTHORIZED", 4.2f);
            }
        }

        private string DoctrineName()
        {
            switch (_doctrine)
            {
                case Doctrine.Breakthrough: return "BREAKTHROUGH";
                case Doctrine.GhostSpear: return "GHOST SPEAR";
                default: return "IRON SHIELD";
            }
        }

        private string ObjectiveTitle()
        {
            switch (CurrentObjective())
            {
                case ObjectiveKind.BreakArmor: return "BREACH ARMORED TARGETS";
                case ObjectiveKind.PriorityHunt: return "ELIMINATE PRIORITY TARGETS";
                default: return "HOLD ORZELEK LINE // KILLS";
            }
        }

        private void Announce(string text, float duration)
        {
            _banner = text;
            _bannerUntil = Time.unscaledTime + duration;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.96f, 0.78f, 0.20f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.70f, 0.76f, 0.86f) } };
            _bannerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _game.CurrentRound < 91) return;
            EnsureStyles();

            float x = 14f;
            float y = Mathf.Max(110f, Screen.height - 202f);
            GUI.color = new Color(0.035f, 0.045f, 0.065f, 0.94f);
            GUI.Box(new Rect(x, y, 380f, 116f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 8f, 352f, 20f), $"ZERO FRONT DOCTRINE // {DoctrineName()}", _title);

            if (_round <= 99)
            {
                string state = _objectiveComplete ? "COMPLETE" : _objectiveCompromised ? "COMPROMISED" : "ACTIVE";
                GUI.Label(new Rect(x + 12f, y + 32f, 352f, 18f), $"{ObjectiveTitle()}  {_objectiveProgress}/{_objectiveTarget}", _body);
                GUI.Label(new Rect(x + 12f, y + 51f, 352f, 17f), $"STATUS {state}   ROUND KILLS {_roundKills}   PRIORITY {_roundPriorityKills}", _small);
            }
            else
            {
                GUI.Label(new Rect(x + 12f, y + 32f, 352f, 18f), "FINAL ASSAULT // DESTROY OVERDRIVE ZERO", _body);
                GUI.Label(new Rect(x + 12f, y + 51f, 352f, 17f), $"DOCTRINE OBJECTIVES {_completedObjectives}/9", _small);
            }

            GUI.Label(new Rect(x + 12f, y + 72f, 352f, 17f), "Campaign choices now alter objectives, support and enemy pressure.", _small);
            GUI.Label(new Rect(x + 12f, y + 89f, 352f, 17f), $"MASTERY {_completedObjectives}/9   RUN {_runId}", _small);

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.025f, 0.03f, 0.05f, 0.96f);
                GUI.Box(new Rect(Screen.width * 0.5f - 390f, Screen.height * 0.38f - 32f, 780f, 64f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 380f, Screen.height * 0.38f - 22f, 760f, 44f), _banner, _bannerStyle);
            }
        }
    }
}
