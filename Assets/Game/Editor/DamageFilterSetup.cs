using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
[InitializeOnLoad]
public static class DamageFilterSetup
{
    static DamageFilterSetup(){EditorApplication.delayCall+=Setup;}
    static void Setup(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        foreach(var effect in Object.FindObjectsOfType<PlayerDamageVideoEffect>(true)){
            if(effect.gameObject.scene.name!="Stage_city"||effect.damageFilter!=null||effect.overlay==null)continue;
            var go=new GameObject("Damage Panel Filter",typeof(RectTransform),typeof(Image));
            Undo.RegisterCreatedObjectUndo(go,"Add damage panel filter");
            var r=(RectTransform)go.transform;var source=effect.overlay.rectTransform;
            r.SetParent(source.parent,false);r.anchorMin=source.anchorMin;r.anchorMax=source.anchorMax;r.pivot=source.pivot;r.sizeDelta=source.sizeDelta;r.anchoredPosition=source.anchoredPosition;
            r.SetSiblingIndex(source.GetSiblingIndex());
            var image=go.GetComponent<Image>();image.color=new Color(.65f,0,0,0);image.raycastTarget=false;
            Undo.RecordObject(effect,"Register damage filter");effect.damageFilter=image;
            EditorUtility.SetDirty(effect);EditorSceneManager.MarkSceneDirty(effect.gameObject.scene);EditorSceneManager.SaveScene(effect.gameObject.scene);
            Debug.Log("[DamageFilterSetup] Registered red panel behind damage video; opacity adjustable.");
        }
    }
}
