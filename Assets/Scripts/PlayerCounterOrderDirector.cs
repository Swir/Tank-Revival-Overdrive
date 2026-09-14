using UnityEngine;

namespace TankRevival
{
    public enum PlayerCounterOrder
    {
        None = 0,
        Block = 1,
        Counterattack = 2,
        DeepStrike = 3
    }

    [DefaultExecutionOrder(552)]
    public sealed class PlayerCounterOrderDirector : MonoBehaviour
    {
        public const float DecisionWindow = 8f;
        public const float ResponseCadence = 4.25f;
        public const int MaxResponseBeats = 2;
        public const int MaxSupportShellsPerBeat = 2;
        public const int MaxBlockRepairPerOperation = 3;
        public const int MaxOrderRewardBonds = 6;
        public const float AxisTargetRadius = 7.5f;

        private static PlayerCounterOrderDirector _instance;
        private TankGame _game;
        private int _round = -1;
        private int _operationSector = -1;
        private bool _decisionOpen;
        private bool _orderResolved;
        private float _decisionUntil;
        private float _nextResponseBeat;
        private int _beatsRemaining;
        private int _blockRepairSpent;
        private PlayerCounterOrder _activeOrder;
        private DeceptionAxis _knownMainAxis;
        private DeceptionAxis _knownFeintAxis;
        private string _banner = string.Empty;
        private float _bannerUntil;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        public static PlayerCounterOrderDirector Instance => _instance;
        public PlayerCounterOrder ActiveOrder => _activeOrder;
        public bool DecisionOpen => _decisionOpen;
        public float DecisionRemaining => Mathf.Max(0f, _decisionUntil - Time.unscaledTime);
        public int BeatsRemaining => _beatsRemaining;
        public DeceptionAxis KnownMainAxis => _knownMainAxis;
        public DeceptionAxis KnownFeintAxis => _knownFeintAxis;

        public static bool ConfigurationValid =>
            DecisionWindow >= 6f && DecisionWindow <= 12f &&
            ResponseCadence >= 3.5f && ResponseCadence <= 6f &&
            MaxResponseBeats >= 1 && MaxResponseBeats <= 2 &&
            MaxSupportShellsPerBeat >= 1 && MaxSupportShellsPerBeat <= 2 &&
            MaxBlockRepairPerOperation >= 2 && MaxBlockRepairPerOperation <= 4 &&
            MaxOrderRewardBonds >= 4 && MaxOrderRewardBonds <= 8 &&
            AxisTargetRadius >= 5f && AxisTargetRadius <= 9f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<PlayerCounterOrderDirector>() != null) return;
            GameObject go = new GameObject("PlayerCounterOrderDirector_v10_5");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerCounterOrderDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetRun();
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round != _round) BeginRound(round);

            HighCommandDeceptionWarDirector deception = HighCommandDeceptionWarDirector.Instance;
            if (deception == null || !IsDecisionRound(round)) return;

            int sector = HighCommandDeceptionWarDirector.SectorForRound(round);
            if (sector != _operationSector)
            {
                _operationSector = sector;
                _decisionOpen = false;
                _orderResolved = false;
                _activeOrder = PlayerCounterOrder.None;
                _beatsRemaining = 0;
                _blockRepairSpent = 0;
            }

            if (!_orderResolved && !_decisionOpen && deception.IntelligenceConfirmed)
                OpenDecision(deception);

            if (_decisionOpen)
                ProcessDecisionInput();

            if (_orderResolved && _beatsRemaining > 0 && Time.time >= _nextResponseBeat)
            {
                _nextResponseBeat = Time.time + ResponseCadence;
                ExecuteResponseBeat(round);
            }
        }

        private void BeginRound(int round)
        {
            _round = round;
            if (!IsDecisionRound(round))
            {
                _decisionOpen = false;
                _beatsRemaining = 0;
            }
        }

        private void OpenDecision(HighCommandDeceptionWarDirector deception)
        {
            _knownMainAxis = deception.MainAxis;
            _knownFeintAxis = deception.FeintAxis;
            _decisionOpen = true;
            _decisionUntil = Time.unscaledTime + DecisionWindow;
            _banner = "INTEL CONFIRMED // ISSUE COUNTER-ORDER";
            _bannerUntil = Time.unscaledTime + DecisionWindow;
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.30f, 0.10f);
            VisualFactory.RingPulse(DynamicFrontlineTerritoryDirector.LanePosition((int)_knownMainAxis), new Color(1f, 0.12f, 0.12f), 1.55f);
            VisualFactory.RingPulse(DynamicFrontlineTerritoryDirector.LanePosition((int)_knownFeintAxis), new Color(0.58f, 0.36f, 1f), 1.15f);
        }

        private void ProcessDecisionInput()
        {
            if (Input.GetKeyDown(KeyCode.Z))
                ResolveOrder(PlayerCounterOrder.Block);
            else if (Input.GetKeyDown(KeyCode.X))
                ResolveOrder(PlayerCounterOrder.Counterattack);
            else if (Input.GetKeyDown(KeyCode.C))
                ResolveOrder(PlayerCounterOrder.DeepStrike);
            else if (Time.unscaledTime >= _decisionUntil)
                ResolveOrder(ResolveDefaultOrder(CurrentEagleRatio()));
        }

        private void ResolveOrder(PlayerCounterOrder order)
        {
            if (!_decisionOpen || _orderResolved || order == PlayerCounterOrder.None) return;
            _decisionOpen = false;
            _orderResolved = true;
            _activeOrder = order;
            _beatsRemaining = MaxResponseBeats;
            _nextResponseBeat = Time.time + 0.15f;

            switch (order)
            {
                case PlayerCounterOrder.Block:
                    _banner = "COUNTER-ORDER: BLOCK // MAIN AXIS HARDENED";
                    ApplyBlockRecovery(2);
                    break;
                case PlayerCounterOrder.Counterattack:
                    _banner = "COUNTER-ORDER: COUNTERATTACK // FEINT TRAP ARMED";
                    break;
                case PlayerCounterOrder.DeepStrike:
                    _banner = "COUNTER-ORDER: DEEP STRIKE // HIGH-VALUE INTERDICTION";
                    break;
            }
            _bannerUntil = Time.unscaledTime + 4f;
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.34f, 0.03f);
        }

        private void ExecuteResponseBeat(int round)
        {
            if (_beatsRemaining <= 0) return;
            int fired = 0;
            switch (_activeOrder)
            {
                case PlayerCounterOrder.Block:
                    fired = FireAxisSupport(_knownMainAxis, AmmoType.ArmorPiercing, round >= 75 ? 2 : 1, MaxSupportShellsPerBeat);
                    ApplyBlockRecovery(1);
                    break;
                case PlayerCounterOrder.Counterattack:
                    fired = FireAxisSupport(_knownFeintAxis, AmmoType.Explosive, 1, MaxSupportShellsPerBeat);
                    break;
                case PlayerCounterOrder.DeepStrike:
                    fired = FireDeepStrike(round);
                    break;
            }

            _beatsRemaining--;
            if (_beatsRemaining <= 0)
            {
                int reward = ResolveCompletionReward(_activeOrder, fired > 0);
                if (reward > 0) WarEconomyDirector.AwardMissionBonds(reward, "COUNTER-ORDER EXECUTED");
                _banner = "COUNTER-ORDER COMPLETE // " + _activeOrder.ToString().ToUpperInvariant();
                _bannerUntil = Time.unscaledTime + 3f;
            }
        }

        private int FireAxisSupport(DeceptionAxis axis, AmmoType ammo, int damage, int shellCap)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return 0;
            Vector2 axisPos = DynamicFrontlineTerritoryDirector.LanePosition((int)axis);
            int fired = 0;

            for (int pass = 0; pass < 2 && fired < shellCap; pass++)
            {
                EnemyTank best = null;
                float bestScore = float.MaxValue;
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (!IsLiveEnemy(enemy)) continue;
                    bool highValue = enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Sniper;
                    if (pass == 0 && !highValue) continue;
                    float distance = Vector2.Distance(enemy.transform.position, axisPos);
                    if (distance > AxisTargetRadius || distance >= bestScore) continue;
                    bestScore = distance;
                    best = enemy;
                }
                if (best == null) continue;
                SpawnFriendlySupport(best.transform.position, ammo, damage);
                fired++;
            }
            return fired;
        }

        private int FireDeepStrike(int round)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return 0;
            int fired = 0;
            for (int pass = 0; pass < 5 && fired < MaxSupportShellsPerBeat; pass++)
            {
                EnemyTank target = null;
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (!IsLiveEnemy(enemy)) continue;
                    if (!MatchesDeepStrikePriority(enemy.Kind, pass)) continue;
                    target = enemy;
                    break;
                }
                if (target == null) continue;
                int damage = round >= 80 && pass <= 1 ? 2 : 1;
                SpawnFriendlySupport(target.transform.position, AmmoType.ArmorPiercing, damage);
                fired++;
            }
            return fired;
        }

        private void SpawnFriendlySupport(Vector2 target, AmmoType ammo, int damage)
        {
            Vector2 origin = _game.PlayerPosition;
            Vector2 delta = target - origin;
            if (delta.sqrMagnitude < 0.25f) return;
            float speed = ammo == AmmoType.Explosive ? 10f : 12f;
            _game.SpawnProjectile(origin + delta.normalized * 0.65f, delta.normalized, Team.Player, Mathf.Clamp(damage, 1, 2), speed, AmmoDatabase.Color(ammo), ammo);
            VisualFactory.RingPulse(target, ammo == AmmoType.Explosive ? new Color(1f, 0.58f, 0.14f) : new Color(0.30f, 0.82f, 1f), 0.72f);
        }

        private void ApplyBlockRecovery(int requested)
        {
            int remaining = Mathf.Max(0, MaxBlockRepairPerOperation - _blockRepairSpent);
            int amount = Mathf.Min(requested, remaining);
            if (amount <= 0) return;

            Health eagle = CombatRoster.Eagle;
            PlayerTank player = CombatRoster.Player;
            int applied = 0;
            if (eagle != null && !eagle.IsDead && eagle.Current < eagle.Maximum)
            {
                int heal = Mathf.Min(amount, 2);
                eagle.Heal(heal);
                applied += heal;
            }
            if (player != null && player.Health != null && !player.Health.IsDead && player.Health.Current < player.Health.Maximum && applied < amount)
            {
                player.Health.Heal(amount - applied);
                applied = amount;
            }
            _blockRepairSpent += applied;
        }

        private float CurrentEagleRatio()
        {
            Health eagle = CombatRoster.Eagle;
            if (eagle == null || eagle.Maximum <= 0) return 1f;
            return Mathf.Clamp01(eagle.Current / (float)eagle.Maximum);
        }

        private static bool IsLiveEnemy(EnemyTank enemy)
        {
            return enemy != null && enemy.Health != null && !enemy.Health.IsDead && enemy.Kind != EnemyKind.Boss && enemy.Kind != EnemyKind.Supply;
        }

        private static bool MatchesDeepStrikePriority(EnemyKind kind, int pass)
        {
            if (pass == 0) return kind == EnemyKind.Siege;
            if (pass == 1) return kind == EnemyKind.Elite;
            if (pass == 2) return kind == EnemyKind.Sniper;
            if (pass == 3) return kind == EnemyKind.Heavy;
            return kind != EnemyKind.Basic;
        }

        private void ResetRun()
        {
            _round = -1;
            _operationSector = -1;
            _decisionOpen = false;
            _orderResolved = false;
            _activeOrder = PlayerCounterOrder.None;
            _beatsRemaining = 0;
            _blockRepairSpent = 0;
            _banner = string.Empty;
        }

        public static bool IsDecisionRound(int round)
        {
            return HighCommandDeceptionWarDirector.ResolvePhase(round) == DeceptionWarPhase.MainEffort;
        }

        public static PlayerCounterOrder ResolveDefaultOrder(float eagleHealthRatio)
        {
            if (eagleHealthRatio < 0.48f) return PlayerCounterOrder.Block;
            if (eagleHealthRatio > 0.78f) return PlayerCounterOrder.Counterattack;
            return PlayerCounterOrder.DeepStrike;
        }

        public static int ResolveCompletionReward(PlayerCounterOrder order, bool engagedTarget)
        {
            if (!engagedTarget) return 0;
            switch (order)
            {
                case PlayerCounterOrder.Block: return 4;
                case PlayerCounterOrder.Counterattack: return 6;
                case PlayerCounterOrder.DeepStrike: return 5;
                default: return 0;
            }
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            if (!_decisionOpen && Time.unscaledTime >= _bannerUntil) return;

            EnsureStyles();
            float width = Mathf.Min(570f, Screen.width - 32f);
            Rect box = new Rect((Screen.width - width) * 0.5f, 18f, width, _decisionOpen ? 104f : 58f);
            GUI.Box(box, GUIContent.none);
            GUILayout.BeginArea(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, box.height - 16f));
            GUILayout.Label(_decisionOpen ? "OPERATIONAL INTELLIGENCE // COUNTER-ORDER" : _banner, _titleStyle);
            if (_decisionOpen)
            {
                GUILayout.Label("MAIN " + _knownMainAxis.ToString().ToUpperInvariant() + "  //  FEINT " + _knownFeintAxis.ToString().ToUpperInvariant() + "  //  " + DecisionRemaining.ToString("0.0") + "s", _bodyStyle);
                GUILayout.Label("Z BLOCK   X COUNTERATTACK   C DEEP STRIKE", _bodyStyle);
            }
            else
            {
                GUILayout.Label("Response beats remaining: " + _beatsRemaining, _bodyStyle);
            }
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 15, alignment = TextAnchor.MiddleCenter };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
        }
    }
}
