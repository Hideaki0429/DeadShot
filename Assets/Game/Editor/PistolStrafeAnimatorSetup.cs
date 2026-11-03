#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
internal static class PistolStrafeAnimatorSetup
{
    private const string ControllerPath = "Assets/unity-chan!/Unity-chan! Model/Art/Animations/Animators/UnityChanLocomotions.controller";
    private const string LeftClipPath = "Assets/Game/Anim/PistolAnim/Run/Pistol_left.anim";
    private const string RightClipPath = "Assets/Game/Anim/PistolAnim/Run/Pistol_right.anim";
    private const string OldMaskPath = "Assets/Game/Anim/PistolAnim/PistolStrafeLowerBody.mask";

    static PistolStrafeAnimatorSetup() => EditorApplication.delayCall += Install;

    [MenuItem("Tools/Dead Shot/Setup Pistol Strafe Animations")]
    private static void Install()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimationClip leftClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(LeftClipPath);
        AnimationClip rightClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RightClipPath);
        if (controller == null || leftClip == null || rightClip == null || controller.layers.Length == 0) return;

        SetLoop(leftClip);
        SetLoop(rightClip);
        RemoveOldStrafeLayers(controller);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = Find(machine, "Pistol Idle");
        AnimatorState forward = Find(machine, "Setup1_Locomotion");
        AnimatorState backward = Find(machine, "Pistol Walk Backward");
        AnimatorState normal = Find(machine, "Locomotion");
        if (idle == null || forward == null || backward == null || normal == null) return;

        AnimatorState left = Find(machine, "Pistol Left") ?? machine.AddState("Pistol Left", new Vector3(690, 310));
        AnimatorState right = Find(machine, "Pistol Right") ?? machine.AddState("Pistol Right", new Vector3(690, 390));
        ConfigureState(left, leftClip, .4f);
        ConfigureState(right, rightClip, .5f);

        AddEntry(idle, left, AnimatorConditionMode.Less, -0.1f);
        AddEntry(forward, left, AnimatorConditionMode.Less, -0.1f);
        AddEntry(backward, left, AnimatorConditionMode.Less, -0.1f);
        AddEntry(idle, right, AnimatorConditionMode.Greater, 0.1f);
        AddEntry(forward, right, AnimatorConditionMode.Greater, 0.1f);
        AddEntry(backward, right, AnimatorConditionMode.Greater, 0.1f);
        AddTransition(left, right, (AnimatorConditionMode.Greater, 0.1f, "Direction"));
        AddTransition(right, left, (AnimatorConditionMode.Less, -0.1f, "Direction"));
        AddLocomotionExits(left, idle, forward, backward, normal, AnimatorConditionMode.Greater, -0.1f);
        AddLocomotionExits(right, idle, forward, backward, normal, AnimatorConditionMode.Less, 0.1f);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureState(AnimatorState state, AnimationClip clip, float speed)
    {
        state.motion = clip;
        state.speed = speed;
        state.cycleOffset = 0f;
        state.mirror = false;
        state.iKOnFeet = true;
        state.writeDefaultValues = true;
    }

    private static void RemoveOldStrafeLayers(AnimatorController controller)
    {
        for (int i = controller.layers.Length - 1; i >= 1; i--)
        {
            string name = controller.layers[i].name;
            if (name == "Pistol Strafe Lower Body" || name == "Pistol Strafe Upper Body") controller.RemoveLayer(i);
        }
        if (AssetDatabase.LoadAssetAtPath<AvatarMask>(OldMaskPath) != null) AssetDatabase.DeleteAsset(OldMaskPath);
    }

    private static void AddEntry(AnimatorState source, AnimatorState target, AnimatorConditionMode mode, float threshold)
    {
        AddTransition(source, target, (mode, threshold, "Direction"), (AnimatorConditionMode.If, 0f, "Setup"));
    }

    private static void AddLocomotionExits(AnimatorState source, AnimatorState idle, AnimatorState forward,
        AnimatorState backward, AnimatorState normal, AnimatorConditionMode directionMode, float directionThreshold)
    {
        AddTransition(source, forward, (directionMode, directionThreshold, "Direction"),
            (AnimatorConditionMode.Greater, 0.1f, "Speed"), (AnimatorConditionMode.If, 0f, "Setup"));
        AddTransition(source, backward, (directionMode, directionThreshold, "Direction"),
            (AnimatorConditionMode.Less, -0.1f, "Speed"), (AnimatorConditionMode.If, 0f, "Setup"));
        AddTransition(source, idle, (directionMode, directionThreshold, "Direction"),
            (AnimatorConditionMode.Greater, -0.1f, "Speed"), (AnimatorConditionMode.Less, 0.1f, "Speed"),
            (AnimatorConditionMode.If, 0f, "Setup"));
        AddTransition(source, normal, (AnimatorConditionMode.IfNot, 0f, "Setup"));
    }

    private static void AddTransition(AnimatorState source, AnimatorState target,
        params (AnimatorConditionMode mode, float threshold, string parameter)[] conditions)
    {
        foreach (AnimatorStateTransition existing in source.transitions)
            if (existing.destinationState == target && SameConditions(existing, conditions)) return;

        AnimatorStateTransition transition = source.AddTransition(target);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.hasFixedDuration = true;
        transition.canTransitionToSelf = false;
        foreach (var condition in conditions) transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
    }

    private static bool SameConditions(AnimatorStateTransition transition,
        (AnimatorConditionMode mode, float threshold, string parameter)[] expected)
    {
        if (transition.conditions.Length != expected.Length) return false;
        foreach (var item in expected)
        {
            bool found = false;
            foreach (AnimatorCondition condition in transition.conditions)
            {
                if (condition.mode == item.mode && condition.parameter == item.parameter &&
                    Mathf.Approximately(condition.threshold, item.threshold)) { found = true; break; }
            }
            if (!found) return false;
        }
        return true;
    }

    private static AnimatorState Find(AnimatorStateMachine machine, string stateName)
    {
        foreach (ChildAnimatorState child in machine.states) if (child.state.name == stateName) return child.state;
        return null;
    }

    private static void SetLoop(AnimationClip clip)
    {
        SerializedObject serializedClip = new SerializedObject(clip);
        SerializedProperty loop = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopTime");
        SerializedProperty loopPose = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopBlend");
        SerializedProperty bakeRotation = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopBlendOrientation");
        SerializedProperty bakePositionXZ = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopBlendPositionXZ");
        SerializedProperty originalOrientation = serializedClip.FindProperty("m_AnimationClipSettings.m_KeepOriginalOrientation");
        if (loop == null) return;

        // 参考動画のように、モーションの全身の動きは残しつつ
        // Rootの向きとXZ移動はBake Into Poseにし、キャラクター制御側で移動させる。
        loop.boolValue = true;
        if (loopPose != null) loopPose.boolValue = true;
        if (bakeRotation != null) bakeRotation.boolValue = true;
        if (bakePositionXZ != null) bakePositionXZ.boolValue = true;
        if (originalOrientation != null) originalOrientation.boolValue = true;
        serializedClip.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(clip);
    }
}
#endif
