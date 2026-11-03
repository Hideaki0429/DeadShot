using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerHitReactionAnimatorSetup
{
    private const string ControllerPath =
        "Assets/unity-chan!/Unity-chan! Model/Art/Animations/Animators/UnityChanLocomotions.controller";
    private const string BiteHitPath =
        "Assets/Game/Anim/Zombie Reaction Hit.fbx";
    private const string StandingHitPath =
        "Assets/Game/Anim/Swat@Standing React Small From Headshot.fbx";
    private const string PistolHitPath =
        "Assets/Game/Anim/PistolAnim/Hit/Hit Reaction (3).fbx";

    [MenuItem("Tools/Dead Shot/Rebuild Player Hit Reactions")]
    public static void Rebuild()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("[Player Hit Setup] Animator Controllerが見つかりません。");
            return;
        }

        AnimationClip biteClip = LoadClip(BiteHitPath, "HitOverreaction");
        AnimationClip standingClip = LoadClip(StandingHitPath, "StandingHit");
        AnimationClip pistolClip = LoadClip(PistolHitPath, "Pistolhit");
        if (biteClip == null || standingClip == null || pistolClip == null)
        {
            Debug.LogError("[Player Hit Setup] 被弾Animation Clipが見つかりません。");
            return;
        }

        EnsureParameter(controller, "PlayerDamage", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "HitWithPistol", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "DamageReactionType", AnimatorControllerParameterType.Int);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState biteState = FindState(stateMachine, "HitOverReaction") ??
                                  FindState(stateMachine, "Player Hit");
        if (biteState == null)
            biteState = stateMachine.AddState("HitOverReaction", new Vector3(-240f, 530f, 0f));
        biteState.name = "HitOverReaction";
        biteState.motion = biteClip;

        AnimatorState standingState = EnsureState(stateMachine, "StandingHit", standingClip,
            new Vector3(20f, 530f, 0f));
        AnimatorState pistolState = FindState(stateMachine, "PistolHit") ??
                                    FindState(stateMachine, "HitOverReaction 0");
        if (pistolState == null)
            pistolState = stateMachine.AddState("PistolHit", new Vector3(280f, 530f, 0f));
        pistolState.name = "PistolHit";
        pistolState.motion = pistolClip;
        biteState.tag = "PlayerHit";
        standingState.tag = "PlayerHit";
        pistolState.tag = "PlayerHit";

        AnimatorState locomotion = FindState(stateMachine, "Locomotion");
        if (locomotion == null)
        {
            Debug.LogError("[Player Hit Setup] 復帰先Stateが見つかりません。");
            return;
        }

        RemoveTransitionsTo(stateMachine, biteState, standingState, pistolState);
        ConfigureReturnTransition(biteState, locomotion);
        ConfigureReturnTransition(standingState, locomotion);
        ConfigureReturnTransition(pistolState, locomotion);

        AnimatorStateTransition standingTransition = stateMachine.AddAnyStateTransition(standingState);
        ConfigureDamageTransition(standingTransition, 0);
        AnimatorStateTransition pistolTransition = stateMachine.AddAnyStateTransition(pistolState);
        ConfigureDamageTransition(pistolTransition, 1);
        AnimatorStateTransition biteTransition = stateMachine.AddAnyStateTransition(biteState);
        ConfigureDamageTransition(biteTransition, 2);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Player Hit Setup] HitOverReaction / StandingHit / PistolHitの遷移を再構築しました。");
    }

    private static AnimationClip LoadClip(string path, string clipName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => clip.name == clipName);
    }

    private static void EnsureParameter(
        AnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType type)
    {
        if (controller.parameters.Any(parameter => parameter.name == parameterName)) return;
        controller.AddParameter(parameterName, type);
    }

    private static AnimatorState EnsureState(
        AnimatorStateMachine stateMachine,
        string stateName,
        Motion motion,
        Vector3 position)
    {
        AnimatorState state = FindState(stateMachine, stateName);
        if (state == null)
            state = stateMachine.AddState(stateName, position);
        state.motion = motion;
        return state;
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        return stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state.name == stateName);
    }

    private static void RemoveTransitionsTo(
        AnimatorStateMachine stateMachine,
        AnimatorState biteState,
        AnimatorState standingState,
        AnimatorState pistolState)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            foreach (AnimatorStateTransition transition in child.state.transitions.ToArray())
            {
                if (transition.destinationState == biteState || transition.destinationState == standingState ||
                    transition.destinationState == pistolState)
                    child.state.RemoveTransition(transition);
            }
        }

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
        {
            if (transition.destinationState == biteState || transition.destinationState == standingState ||
                transition.destinationState == pistolState)
                stateMachine.RemoveAnyStateTransition(transition);
        }
    }

    private static void ConfigureReturnTransition(AnimatorState source, AnimatorState destination)
    {
        foreach (AnimatorStateTransition transition in source.transitions.ToArray())
            source.RemoveTransition(transition);

        AnimatorStateTransition returnTransition = source.AddTransition(destination);
        returnTransition.hasExitTime = true;
        returnTransition.exitTime = 0.9f;
        returnTransition.duration = 0.1f;
        returnTransition.canTransitionToSelf = false;
    }

    private static void ConfigureDamageTransition(
        AnimatorStateTransition transition,
        int reactionType)
    {
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "PlayerDamage");
        transition.AddCondition(AnimatorConditionMode.Equals, reactionType, "DamageReactionType");
    }
}
