#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
internal static class PlayerTurnAnimatorSetup
{
    const string ControllerPath = "Assets/unity-chan!/Unity-chan! Model/Art/Animations/Animators/UnityChanLocomotions.controller";

    static readonly (string state, string path, Vector3 position)[] Turns =
    {
        ("Left90", "Assets/Game/Anim/turn/Swat@Left Turn (2).fbx", new Vector3(1050, -210)),
        ("Left180", "Assets/Game/Anim/turn/Swat@Left Turn (1).fbx", new Vector3(1050, -130)),
        ("Right90", "Assets/Game/Anim/turn/Swat@Right Turn.fbx", new Vector3(1050, -50)),
        ("Right180", "Assets/Game/Anim/turn/Swat@Right Turn (1).fbx", new Vector3(1050, 30))
    };

    static PlayerTurnAnimatorSetup() => EditorApplication.delayCall += Install;

    [MenuItem("Dead Shot/Player/Setup Camera Turn Animations")]
    static void Install()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || controller.layers.Length == 0) return;
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = machine.states.FirstOrDefault(child => child.state.name == "Idle").state;
        AnimatorState locomotion = machine.states.FirstOrDefault(child => child.state.name == "Locomotion").state;
        if (idle == null || locomotion == null) return;

        foreach (var entry in Turns)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(entry.path)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate.name == entry.state);
            if (clip == null)
            {
                Debug.LogError($"[Player Turn] AnimationClip '{entry.state}' was not found at {entry.path}");
                continue;
            }

            AnimatorState state = machine.states.FirstOrDefault(child => child.state.name == entry.state).state;
            if (state == null) state = machine.AddState(entry.state, entry.position);
            state.motion = clip;
            state.speed = 1f;
            state.iKOnFeet = true;
            state.writeDefaultValues = true;

            if (!controller.parameters.Any(parameter => parameter.name == entry.state))
                controller.AddParameter(entry.state, AnimatorControllerParameterType.Trigger);

            // Any State → 各ターン。作成後はAnimator上の矢印からDurationを自由に編集可能。
            AnimatorStateTransition enter = machine.anyStateTransitions.FirstOrDefault(transition =>
                transition.destinationState == state &&
                transition.conditions.Any(condition => condition.parameter == entry.state));
            if (enter == null)
            {
                enter = machine.AddAnyStateTransition(state);
                enter.hasExitTime = false;
                enter.hasFixedDuration = true;
                enter.duration = .08f;
                enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.If, 0f, entry.state);
            }

            // 各ターン → Idle。ここが待機へ戻る際のブレンド編集箇所。
            AnimatorStateTransition toIdle = state.transitions.FirstOrDefault(transition =>
                transition.destinationState == idle);
            if (toIdle == null)
            {
                toIdle = state.AddTransition(idle);
                toIdle.hasExitTime = true;
                toIdle.exitTime = .92f;
                toIdle.hasFixedDuration = true;
                toIdle.duration = .08f;
                toIdle.canTransitionToSelf = false;
            }

            // 移動入力時は完走を待たずLocomotionへ抜ける。
            AnimatorStateTransition toLocomotion = state.transitions.FirstOrDefault(transition =>
                transition.destinationState == locomotion &&
                transition.conditions.Any(condition => condition.parameter == "Speed"));
            if (toLocomotion == null)
            {
                toLocomotion = state.AddTransition(locomotion);
                toLocomotion.hasExitTime = true;
                toLocomotion.exitTime = .22f;
                toLocomotion.hasFixedDuration = true;
                toLocomotion.duration = .08f;
                toLocomotion.canTransitionToSelf = false;
                toLocomotion.AddCondition(AnimatorConditionMode.Greater, .1f, "Speed");
            }
            else if (!toLocomotion.hasExitTime)
            {
                // 旧方式（入力で即キャンセル）の遷移だけを一度更新する。
                // 更新後はAnimator上でExit Time/Durationを自由に編集できる。
                toLocomotion.hasExitTime = true;
                toLocomotion.exitTime = .22f;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[Player Turn] Left/Right 90 and 180 degree animations installed.", controller);
    }
}
#endif
