using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class GrenadeAnimationSetup
{
    static GrenadeAnimationSetup() { EditorApplication.delayCall += Setup; }
    [MenuItem("Dead Shot/Player/Setup Grenade Animations")]
    static void Setup()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var health=Object.FindObjectOfType<PlayerHealth>(true);
        if(health == null) return;
        var animator=health.GetComponent<Animator>();
        var controller=animator.runtimeAnimatorController as AnimatorController;
        if(controller == null) return;
        if(controller.layers.Where(l=>l.name.StartsWith("Grenade ")).Count()==2 &&
            controller.layers.Where(l=>l.name.StartsWith("Grenade ")).All(l=>l.stateMachine.states.Any(s=>s.state.name=="Ready"))) return;
        var stop=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Game/Anim/Grenade/Stopgrenade.anim");
        var walk=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Game/Anim/Grenade/walkgrenade.anim");
        if(stop == null || walk == null) return;
        const string path="Assets/Game/Anim/Grenade/UpperBody.mask";
        var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if(mask == null)
        {
            mask=new AvatarMask();
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            foreach(var part in new[]{AvatarMaskBodyPart.Body,AvatarMaskBodyPart.Head,AvatarMaskBodyPart.LeftArm,AvatarMaskBodyPart.RightArm,AvatarMaskBodyPart.LeftFingers,AvatarMaskBodyPart.RightFingers}) mask.SetHumanoidBodyPartActive(part,true);
            AssetDatabase.CreateAsset(mask,path);
        }
        if(!controller.parameters.Any(p=>p.name=="GrenadeSpeed")) controller.AddParameter("GrenadeSpeed",AnimatorControllerParameterType.Float);
        for(int i=0;i<2;i++)
        {
            string name=i==0?"Grenade Standing":"Grenade Moving";
            if(!controller.layers.Any(l=>l.name==name)) controller.AddLayer(name);
            var layers=controller.layers; var layer=layers.First(l=>l.name==name);
            layer.avatarMask=i==0?null:mask; layer.defaultWeight=0;
            var state=layer.stateMachine.states.FirstOrDefault(s=>s.state.name=="Throw").state;
            if(state==null) state=layer.stateMachine.AddState("Throw",new Vector3(480,100));
            state.motion=i==0?stop:walk;
            state.speed=1; state.speedParameterActive=false; state.writeDefaultValues=false;
            var ready=layer.stateMachine.AddState("Ready",new Vector3(180,100));
            ready.writeDefaultValues=false; layer.stateMachine.defaultState=ready;
            string trigger=i==0?"ThrowGrenadeStanding":"ThrowGrenadeMoving";
            if(!controller.parameters.Any(p=>p.name==trigger)) controller.AddParameter(trigger,AnimatorControllerParameterType.Trigger);
            var enter=ready.AddTransition(state); enter.hasExitTime=false; enter.hasFixedDuration=true; enter.duration=.1f;
            enter.AddCondition(AnimatorConditionMode.If,0,trigger);
            var leave=state.AddTransition(ready); leave.hasExitTime=true; leave.exitTime=.9f;
            leave.hasFixedDuration=true; leave.duration=.15f;
            controller.layers=layers;
        }
        var component=health.GetComponent<PlayerGrenadeAnimation>();
        if(component==null) component=health.gameObject.AddComponent<PlayerGrenadeAnimation>();
        component.stopClip=stop; component.walkClip=walk;
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(component);
        EditorSceneManager.MarkSceneDirty(health.gameObject.scene); EditorSceneManager.SaveScene(health.gameObject.scene); AssetDatabase.SaveAssets();
        Debug.Log("[Grenade] Standing/full body and moving/upper body layers registered.");
    }
}
