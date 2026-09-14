using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v9.0 integration bridge between the qualified v8.9 Forward Recovery Base and the live
    /// territory layer. It does not own recovery or lane state: it only applies a small bounded
    /// influence through the existing directors while those systems remain authoritative.
    /// </summary>
    [DefaultExecutionOrder(520)]
    public sealed class FrontlineRecoveryTerritoryBridge : MonoBehaviour
    {
        public const float RecoveryLaneInfluence = 5f;
        public const float InfluenceCadence = 1.25f;
        public const float FriendlyTerritorySupportExtension = 2.5f;
        public const float MaxTerritorySupportExtension = 2.5f;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo ControlField = typeof(DynamicFrontlineTerritoryDirector).GetField("_control", PrivateInstance);
        private static readonly FieldInfo RecoveryBaseField = typeof(ForwardRecoveryFrontlineDirector).GetField("_base", PrivateInstance);
        private static readonly FieldInfo RecoverySupportEndsAtField = typeof(ForwardRecoveryFrontlineDirector).GetField("_supportEndsAt", PrivateInstance);

        private float _nextInfluence;
        private int _lastExtendedBaseId;

        public static bool BridgeAvailable => ControlField != null && RecoveryBaseField != null && RecoverySupportEndsAtField != null;
        public static bool ConfigurationValid =>
            RecoveryLaneInfluence >= 2f && RecoveryLaneInfluence <= 7f &&
            InfluenceCadence >= 0.8f && InfluenceCadence <= 2f &&
            FriendlyTerritorySupportExtension > 0f &&
            FriendlyTerritorySupportExtension <= MaxTerritorySupportExtension &&
            MaxTerritorySupportExtension <= 3f && BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<FrontlineRecoveryTerritoryBridge>() != null) return;
            var go = new GameObject("FrontlineRecoveryTerritoryBridge_v9_0");
            DontDestroyOnLoad(go);
            go.AddComponent<FrontlineRecoveryTerritoryBridge>();
        }

        private void Update()
        {
            DynamicFrontlineTerritoryDirector frontline = DynamicFrontlineTerritoryDirector.Instance;
            ForwardRecoveryFrontlineDirector recovery = ForwardRecoveryFrontlineDirector.Instance;
            if (frontline == null || recovery == null || recovery.Phase != ForwardRecoveryPhase.Support || !BridgeAvailable) return;

            GameObject recoveryBase = RecoveryBaseField.GetValue(recovery) as GameObject;
            if (recoveryBase == null) return;

            if (Time.time >= _nextInfluence)
            {
                _nextInfluence = Time.time + InfluenceCadence;
                ApplyRecoveryInfluence(frontline, recoveryBase.transform.position);
            }

            if (frontline.FriendlyLanes >= 2)
                ExtendRecoveryAccessOnce(recovery, recoveryBase);
        }

        private static void ApplyRecoveryInfluence(DynamicFrontlineTerritoryDirector frontline, Vector2 basePosition)
        {
            float[] control = ControlField.GetValue(frontline) as float[];
            if (control == null || control.Length != DynamicFrontlineTerritoryDirector.LaneCount) return;

            int nearest = 0;
            float best = float.MaxValue;
            for (int lane = 0; lane < DynamicFrontlineTerritoryDirector.LaneCount; lane++)
            {
                float distance = Vector2.Distance(basePosition, DynamicFrontlineTerritoryDirector.LanePosition(lane));
                if (distance < best) { best = distance; nearest = lane; }
            }

            // One secured recovery base can reinforce one lane, never flip the entire front at once.
            control[nearest] = Mathf.Clamp(control[nearest] + RecoveryLaneInfluence, 0f, 100f);
        }

        private void ExtendRecoveryAccessOnce(ForwardRecoveryFrontlineDirector recovery, GameObject recoveryBase)
        {
            int id = recoveryBase.GetInstanceID();
            if (_lastExtendedBaseId == id) return;
            float supportEndsAt = Convert.ToSingle(RecoverySupportEndsAtField.GetValue(recovery));
            if (supportEndsAt <= Time.time) return;
            RecoverySupportEndsAtField.SetValue(recovery, supportEndsAt + FriendlyTerritorySupportExtension);
            _lastExtendedBaseId = id;
        }
    }
}
