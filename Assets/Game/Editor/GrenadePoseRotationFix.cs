using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GrenadePoseRotationFix
{
    static GrenadePoseRotationFix()
    {
        EditorApplication.delayCall += Apply;
        EditorApplication.playModeStateChanged += state => { if(state==PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall+=Apply; };
    }
    [MenuItem("Dead Shot/Player/Apply Grenade Pose Rotation Fix")]
    static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var player=Object.FindObjectOfType<PlayerGrenadeAnimation>(true);
        if(player==null) return;
        var controller=player.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
        if(controller==null) return;
        bool changed=false;
        for(int i=0;i<2;i++)
        {
            string original=i==0?"Stopgrenade":"walkgrenade";
            string path="Assets/Game/Anim/Grenade/"+original+"_PoseRotation.anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Game/Anim/Grenade/"+original+".anim");
                if(source==null) continue;
                clip=Object.Instantiate(source); clip.name=original+"_PoseRotation";
                var settings=AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopBlendOrientation=true; // Root Transform Rotation: Bake Into Pose
                settings.keepOriginalOrientation=true;
                settings.loopTime=false;
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                AssetDatabase.CreateAsset(clip,path);
            }
            var layer=controller.layers.FirstOrDefault(l=>l.name==(i==0?"Grenade Standing":"Grenade Moving"));
            if(layer==null) continue;
            var state=layer.stateMachine.states.FirstOrDefault(s=>s.state.name=="Throw").state;
            if(state!=null && state.motion!=clip) { state.motion=clip; EditorUtility.SetDirty(state); changed=true; }
            if(i==0 && player.stopClip!=clip){player.stopClip=clip;changed=true;}
            if(i==1 && player.walkClip!=clip){player.walkClip=clip;changed=true;}
        }
        if(!changed) return;
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene); EditorSceneManager.SaveScene(player.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Grenade Pose Fix] Rotation baked into duplicate clips. Original clips and transition values preserved.");
    }
}
