#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
internal static class GrenadeLowerBodyAnimatorSetup
{
    const string ControllerPath = "Assets/unity-chan!/Unity-chan! Model/Art/Animations/Animators/UnityChanLocomotions.controller";
    const string MaskPath = "Assets/Game/Anim/Grenade/GrenadeLowerBody.mask";
    const string LayerName = "Grenade Lower Body";

    static GrenadeLowerBodyAnimatorSetup() => EditorApplication.delayCall += Install;

    [MenuItem("Dead Shot/Player/Setup Grenade Lower Body")]
    static void Install()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || controller.layers.Length == 0) return;
        // 既に構築済みなら、Domain ReloadのたびにControllerを作り直さない。
        if (controller.layers.Any(l => l.name == LayerName)) return;
        EnsureParameter(controller, "GrenadeAim", AnimatorControllerParameterType.Bool, 0f);
        EnsureParameter(controller, "GrenadeForwardAnimSpeed", AnimatorControllerParameterType.Float, .75f);
        EnsureParameter(controller, "GrenadeBackwardAnimSpeed", AnimatorControllerParameterType.Float, .75f);

        AnimatorStateMachine baseMachine = controller.layers[0].stateMachine;
        Motion forwardMotion = Find(baseMachine, "Setup1_Locomotion")?.motion;
        Motion backwardMotion = Find(baseMachine, "Pistol Walk Backward")?.motion;
        Motion leftMotion = Find(baseMachine, "Pistol Left")?.motion;
        Motion rightMotion = Find(baseMachine, "Pistol Right")?.motion;
        if (forwardMotion == null || backwardMotion == null || leftMotion == null || rightMotion == null) return;

        int oldIndex = System.Array.FindIndex(controller.layers, l => l.name == LayerName);
        if (oldIndex >= 0) controller.RemoveLayer(oldIndex);

        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            mask = new AvatarMask { name = "Grenade Lower Body" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, true);
            AssetDatabase.CreateAsset(mask, MaskPath);
        }

        AnimatorControllerLayer layer = new AnimatorControllerLayer
        {
            name = LayerName, defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = mask, stateMachine = new AnimatorStateMachine { name = LayerName }
        };
        AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
        AnimatorStateMachine machine = layer.stateMachine;
        AnimatorState ready = machine.AddState("Ready", new Vector3(220, 100));
        AnimatorState forward = Add(machine, "Forward", forwardMotion, new Vector3(480, 20), 1f, "GrenadeForwardAnimSpeed");
        AnimatorState backward = Add(machine, "Backward", backwardMotion, new Vector3(480, 100), 1f, "GrenadeBackwardAnimSpeed");
        AnimatorState left = Add(machine, "Pistol Left", leftMotion, new Vector3(480, 180), .4f, null);
        AnimatorState right = Add(machine, "Pistol Right", rightMotion, new Vector3(480, 260), .5f, null);
        machine.defaultState = ready;

        AddAny(machine, forward, C(AnimatorConditionMode.If, 0, "GrenadeAim"), C(AnimatorConditionMode.Greater, .1f, "Speed"), C(AnimatorConditionMode.Greater, -.1f, "Direction"), C(AnimatorConditionMode.Less, .1f, "Direction"));
        AddAny(machine, backward, C(AnimatorConditionMode.If, 0, "GrenadeAim"), C(AnimatorConditionMode.Less, -.1f, "Speed"), C(AnimatorConditionMode.Greater, -.1f, "Direction"), C(AnimatorConditionMode.Less, .1f, "Direction"));
        AddAny(machine, left, C(AnimatorConditionMode.If, 0, "GrenadeAim"), C(AnimatorConditionMode.Less, -.1f, "Direction"));
        AddAny(machine, right, C(AnimatorConditionMode.If, 0, "GrenadeAim"), C(AnimatorConditionMode.Greater, .1f, "Direction"));
        AddAny(machine, ready, C(AnimatorConditionMode.IfNot, 0, "GrenadeAim"));
        AddAny(machine, ready, C(AnimatorConditionMode.If, 0, "GrenadeAim"), C(AnimatorConditionMode.Greater, -.1f, "Speed"), C(AnimatorConditionMode.Less, .1f, "Speed"), C(AnimatorConditionMode.Greater, -.1f, "Direction"), C(AnimatorConditionMode.Less, .1f, "Direction"));

        controller.AddLayer(layer);
        // 下半身を先に適用し、手榴弾の上半身レイヤーが必ず後から上書きできる順序にする。
        AnimatorControllerLayer[] layers = controller.layers;
        int addedIndex = layers.Length - 1;
        int grenadeUpperIndex = System.Array.FindIndex(layers,
            l => l.name == "Grenade Standing" || l.name == "Grenade Moving");
        if (grenadeUpperIndex >= 0 && grenadeUpperIndex < addedIndex)
        {
            AnimatorControllerLayer added = layers[addedIndex];
            for (int i = addedIndex; i > grenadeUpperIndex; i--) layers[i] = layers[i - 1];
            layers[grenadeUpperIndex] = added;
            controller.layers = layers;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[Grenade] Lower-body locomotion installed: adjustable forward/back and original pistol strafes.");
    }

    static AnimatorState Add(AnimatorStateMachine machine, string name, Motion motion, Vector3 position, float speed, string speedParameter)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = motion; state.speed = speed; state.iKOnFeet = true;
        if (!string.IsNullOrEmpty(speedParameter)) { state.speedParameter = speedParameter; state.speedParameterActive = true; }
        return state;
    }
    static AnimatorCondition C(AnimatorConditionMode mode, float threshold, string parameter) =>
        new AnimatorCondition { mode = mode, threshold = threshold, parameter = parameter };
    static void AddAny(AnimatorStateMachine machine, AnimatorState destination, params AnimatorCondition[] conditions)
    {
        AnimatorStateTransition transition = machine.AddAnyStateTransition(destination);
        transition.hasExitTime = false; transition.duration = .08f; transition.canTransitionToSelf = false;
        foreach (AnimatorCondition condition in conditions)
            transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
    }
    static AnimatorState Find(AnimatorStateMachine machine, string name) =>
        machine.states.FirstOrDefault(s => s.state.name == name).state;
    static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type, float value)
    {
        if (controller.parameters.Any(p => p.name == name)) return;
        controller.AddParameter(new AnimatorControllerParameter { name = name, type = type, defaultFloat = value });
    }
}
#endif
