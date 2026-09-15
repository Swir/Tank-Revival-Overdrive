using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22410)]
    public sealed class CounterFireThreatMemory : MonoBehaviour
    {
        public const float MemorySeconds = 5.5f;
        public const float RetaliationAccuracyBoost = 0.12f;
        public const int MaxTrackedThreats = 12;

        private struct Threat { public int id; public EnemyKind kind; public Vector2 position; public float hitTime; public int hitCount; }
        private static CounterFireThreatMemory _instance;
        private readonly Dictionary<int, Threat> _threats = new Dictionary<int, Threat>(MaxTrackedThreats);
        private float _nextPrune;

        public static CounterFireThreatMemory Instance => _instance;
        public static bool ConfigurationValid => MemorySeconds <= AdvancedGunneryDoctrineDirector.CounterFireMemorySeconds && RetaliationAccuracyBoost <= .15f && MaxTrackedThreats <= 12;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CounterFireThreatMemory>() != null) return;
            var go = new GameObject("CounterFireThreatMemory_v11_3");
            DontDestroyOnLoad(go);
            go.AddComponent<CounterFireThreatMemory>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this; DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }
        private void Update()
        {
            if (Time.time < _nextPrune) return;
            _nextPrune = Time.time + .75f;
            if (_threats.Count == 0) return;
            var expired = ListPool<int>.Get();
            foreach (var kv in _threats) if (Time.time - kv.Value.hitTime > MemorySeconds) expired.Add(kv.Key);
            for (int i = 0; i < expired.Count; i++) _threats.Remove(expired[i]);
            ListPool<int>.Release(expired);
        }

        public static void RecordPlayerHit(EnemyTank tank)
        {
            if (tank == null || tank.Kind == EnemyKind.Basic || tank.Kind == EnemyKind.Fast || tank.Kind == EnemyKind.Supply || tank.Kind == EnemyKind.Boss) return;
            if (_instance == null) return;
            int id = tank.GetInstanceID();
            Threat t;
            if (!_instance._threats.TryGetValue(id, out t))
            {
                if (_instance._threats.Count >= MaxTrackedThreats) _instance.DropOldest();
                t = new Threat { id = id, kind = tank.Kind, hitCount = 0 };
            }
            t.kind = tank.Kind; t.position = tank.transform.position; t.hitTime = Time.time; t.hitCount = Mathf.Min(3, t.hitCount + 1);
            _instance._threats[id] = t;
        }

        public static bool HasFreshThreat(EnemyTank tank)
        {
            if (_instance == null || tank == null) return false;
            Threat t; return _instance._threats.TryGetValue(tank.GetInstanceID(), out t) && Time.time - t.hitTime <= MemorySeconds;
        }

        public static float AccuracyMultiplier(EnemyTank tank)
        {
            if (!HasFreshThreat(tank)) return 1f;
            Threat t = _instance._threats[tank.GetInstanceID()];
            float boost = Mathf.Min(RetaliationAccuracyBoost, .04f * t.hitCount);
            return 1f - boost;
        }

        public static bool ShouldRetaliate(EnemyTank tank, int round)
        {
            if (!HasFreshThreat(tank)) return false;
            if (round < 35) return false;
            return tank.Kind == EnemyKind.Heavy || tank.Kind == EnemyKind.Siege || tank.Kind == EnemyKind.Sniper || tank.Kind == EnemyKind.Elite;
        }

        private void DropOldest()
        {
            int oldest = 0; float time = float.MaxValue;
            foreach (var kv in _threats) if (kv.Value.hitTime < time) { time = kv.Value.hitTime; oldest = kv.Key; }
            if (oldest != 0) _threats.Remove(oldest);
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new Stack<List<T>>(2);
            public static List<T> Get() => Pool.Count > 0 ? Pool.Pop() : new List<T>(MaxTrackedThreats);
            public static void Release(List<T> list) { list.Clear(); if (Pool.Count < 2) Pool.Push(list); }
        }
    }
}
