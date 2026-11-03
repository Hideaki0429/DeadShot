using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class PlayerShootingAnimatorSetup
{
    private const string ControllerPath =
        "Assets/unity-chan!/Unity-chan! Model/Art/Animations/Animators/UnityChanLocomotions.controller";
    private const string ShootingClipPath = "Assets/Game/PistolAnim/Shooting.anim";
    private const string MaskPath = "Assets/Game/PistolAnim/Player Upper Body Shooting.mask";
    private const string ParameterName = "Shoot";
    private const string LayerName = "Upper Body Shooting";
    private const string SetupVersionKey = "DeadShot.PlayerShootingAnimatorSetup.Version";
    private const int SetupVersion = 3;

    static PlayerShootingAnimatorSetup()
    {
        EditorApplication.delayCall += RebuildIfNeeded;
    }

    [MenuItem("Tools/Dead Shot/Rebuild Player Shooting Animation")]
    public static void Rebuild()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimationClip shootingClip =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(ShootingClipPath);

        if (controller == null || shootingClip == null)
        {
            Debug.LogError("[Player Shooting Setup] ControllerまたはShooting.animが見つかりません。");
            return;
        }

        EnsureParameter(controller);
        ConfigureAdditiveReferencePose(shootingClip);
        AvatarMask upperBodyMask = CreateOrUpdateUpperBodyMask();
        AnimatorControllerLayer layer = EnsureLayer(controller, upperBodyMask);
        ConfigureStateMachine(layer.stateMachine, shootingClip);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(upperBodyMask);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorPrefs.SetInt(SetupVersionKey, SetupVersion);

        Debug.Log("[Player Shooting Setup] 上半身のShootingレイヤーを構築しました。");
    }

    private static void ConfigureAdditiveReferencePose(AnimationClip shootingClip)
    {
        SerializedObject serializedClip = new SerializedObject(shootingClip);
        SerializedProperty clipSettings =
            serializedClip.FindProperty("m_AnimationClipSettings");

        if (clipSettings == null)
        {
            Debug.LogError("[Player Shooting Setup] Shooting.animのClip Settingsを取得できません。");
            return;
        }

        SerializedProperty hasReferencePose =
            clipSettings.FindPropertyRelative("m_HasAdditiveReferencePose");
        SerializedProperty referencePoseTime =
            clipSettings.FindPropertyRelative("m_AdditiveReferencePoseTime");

        if (hasReferencePose != null)
            hasReferencePose.boolValue = true;
        if (referencePoseTime != null)
            referencePoseTime.floatValue = 0f;

        serializedClip.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(shootingClip);
    }

    private static void RebuildIfNeeded()
    {
        if (EditorPrefs.GetInt(SetupVersionKey, 0) >= SetupVersion)
            return;

        Rebuild();
    }

    private static void EnsureParameter(AnimatorController controller)
    {
        if (controller.parameters.Any(parameter => parameter.name == ParameterName))
            return;

        controller.AddParameter(ParameterName, AnimatorControllerParameterType.Trigger);
    }

    private static AvatarMask CreateOrUpdateUpperBodyMask()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            mask = new AvatarMask { name = "Player Upper Body Shooting" };
            AssetDatabase.CreateAsset(mask, MaskPath);
        }

        for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root;
             part < AvatarMaskBodyPart.LastBodyPart;
             part++)
        {
            mask.SetHumanoidBodyPartActive(part, false);
        }

        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        return mask;
    }

    private static AnimatorControllerLayer EnsureLayer(
        AnimatorController controller,
        AvatarMask mask)
    {
        AnimatorControllerLayer layer =
            controller.layers.FirstOrDefault(candidate => candidate.name == LayerName);

        if (layer == null)
        {
            layer = new AnimatorControllerLayer
            {
                name = LayerName,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Additive,
                stateMachine = new AnimatorStateMachine { name = LayerName }
            };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
            controller.AddLayer(layer);
        }

        layer.defaultWeight = 1f;
        layer.blendingMode = AnimatorLayerBlendingMode.Additive;
        layer.avatarMask = mask;

        AnimatorControllerLayer[] layers = controller.layers;
        int layerIndex = System.Array.FindIndex(layers, item => item.name == LayerName);
        layers[layerIndex] = layer;
        controller.layers = layers;
        return controller.layers[layerIndex];
    }

    private static void ConfigureStateMachine(
        AnimatorStateMachine stateMachine,
        AnimationClip shootingClip)
    {
        AnimatorState empty = FindOrCreateState(
            stateMachine, "Empty", new Vector3(250f, 100f, 0f));
        AnimatorState shooting = FindOrCreateState(
            stateMachine, "Shooting", new Vector3(500f, 100f, 0f));

        empty.motion = null;
        shooting.motion = shootingClip;
        shooting.speed = 1f;
        stateMachine.defaultState = empty;

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
            stateMachine.RemoveAnyStateTransition(transition);
        foreach (AnimatorStateTransition transition in shooting.transitions.ToArray())
            shooting.RemoveTransition(transition);

        AnimatorStateTransition shootTransition = stateMachine.AddAnyStateTransition(shooting);
        shootTransition.hasExitTime = false;
        shootTransition.duration = 0.03f;
        shootTransition.canTransitionToSelf = true;
        shootTransition.AddCondition(AnimatorConditionMode.If, 0f, ParameterName);

        AnimatorStateTransition returnTransition = shooting.AddTransition(empty);
        returnTransition.hasExitTime = true;
        returnTransition.exitTime = 0.95f;
        returnTransition.duration = 0.05f;
        returnTransition.canTransitionToSelf = false;
    }

    private static AnimatorState FindOrCreateState(
        AnimatorStateMachine stateMachine,
        string stateName,
        Vector3 position)
    {
        AnimatorState state = stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(candidate => candidate.name == stateName);

        return state ?? stateMachine.AddState(stateName, position);
    }
}
