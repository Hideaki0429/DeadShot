#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
internal static class PlayerRollSetup
{
    const string ControllerPath = "Assets/unity-chan!/Unity-chan! Model/Art/Animations/Animators/UnityChanLocomotions.controller";
    const string ClipPath = "Assets/Game/Anim/Roring/Roring.anim";
    static PlayerRollSetup() => EditorApplication.delayCall += Install;

    [MenuItem("Dead Shot/Player/Setup Rolling")]
    static void Install()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (controller == null || clip == null || controller.layers.Length == 0) return;

        if (!controller.parameters.Any(p => p.name == "Roll")) controller.AddParameter("Roll", AnimatorControllerParameterType.Trigger);
        if (!controller.parameters.Any(p => p.name == "RollAnimationSpeed"))
            controller.AddParameter(new AnimatorControllerParameter { name = "RollAnimationSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = machine.states.FirstOrDefault(s => s.state.name == "Roring").state;
        if (state == null)
        {
            state = machine.AddState("Roring", new Vector3(520, 520));
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false; enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Roll");
            AnimatorState destination = machine.states.FirstOrDefault(s => s.state.name == "Locomotion").state;
            if (destination != null)
            {
                AnimatorStateTransition exit = state.AddTransition(destination);
                exit.hasExitTime = true; exit.exitTime = .98f;
            }
        }
        // 元クリップのポーズとRoot Motionをできる限りそのまま使う。
        state.motion = clip;
        state.iKOnFeet = false;
        state.writeDefaultValues = true;
        state.speed = 1f;
        state.speedParameter = "RollAnimationSpeed";
        state.speedParameterActive = true;
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            if (transition.destinationState == state) transition.duration = 0f;
        foreach (AnimatorStateTransition transition in state.transitions) transition.duration = 0f;
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();

        PlayerHealth player = Object.FindObjectOfType<PlayerHealth>(true);
        if (player == null) return;
        PlayerRollController roll = player.GetComponent<PlayerRollController>();
        if (roll == null) roll = Undo.AddComponent<PlayerRollController>(player.gameObject);
        Undo.RecordObject(roll, "Configure straight rolling");
        roll.roringClip = clip;
        roll.useOriginalRootMotion = true;
        EditorUtility.SetDirty(roll);
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Debug.Log("[Rolling] Roring clip and Space action installed.", roll);
    }

}
#endif
