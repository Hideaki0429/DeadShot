using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[InitializeOnLoad]
public static class DamageVideoSetup
{
    static DamageVideoSetup() { EditorApplication.delayCall += Setup; }
    static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name!="Stage_city" || Object.FindObjectsOfType<PlayerDamageVideoEffect>(true).Any(x=>x.gameObject.scene==scene)) return;
        var health=Object.FindObjectsOfType<PlayerHealth>(true).FirstOrDefault(x=>x.gameObject.scene==scene);
        var canvas=Object.FindObjectsOfType<Canvas>(true).FirstOrDefault(x=>x.gameObject.scene==scene && x.name=="Canvas");
        var clip=AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Game/UI/DamageVideo/DamageEffect.mp4");
        var shader=Shader.Find("DeadShot/UI/DamageVideo");
        if(health==null || canvas==null || clip==null || shader==null) return;
        const string path="Assets/Game/UI/DamageVideo/DamageVideo.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null) { mat=new Material(shader); AssetDatabase.CreateAsset(mat,path); }
        var go=new GameObject("Damage Video Overlay",typeof(RectTransform),typeof(CanvasGroup),typeof(RawImage));
        go.transform.SetParent(canvas.transform,false);
        var r=(RectTransform)go.transform; r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
        var image=go.GetComponent<RawImage>(); image.material=mat; image.color=new Color(1,1,1,0);image.raycastTarget=false;
        var system=new GameObject("Damage Video System");
        var effect=system.AddComponent<PlayerDamageVideoEffect>();
        effect.playerHealth=health;effect.overlay=image;effect.clip=clip;
        effect.existingFilter=Object.FindObjectsOfType<PlayerDamageScreenEffect>(true).FirstOrDefault(x=>x.gameObject.scene==scene);
        system.GetComponent<VideoPlayer>().playOnAwake=false;
        foreach(var pause in Object.FindObjectsOfType<PauseMenuController>(true).Where(x=>x.gameObject.scene==scene))
        { pause.hideGroups=(pause.hideGroups??new CanvasGroup[0]).Concat(new[]{go.GetComponent<CanvasGroup>()}).ToArray();EditorUtility.SetDirty(pause); }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("[DamageVideoSetup] 被ダメージ動画と透過マテリアルを登録しました。");
    }
}
