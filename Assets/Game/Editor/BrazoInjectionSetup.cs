#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
internal static class BrazoInjectionSetup
{
    const string BrazoPath = "Assets/Game/Anim/medical-syringe-healing-fps-animation/source/BRAZO/BRAZO.fbx";
    static BrazoInjectionSetup() => EditorApplication.delayCall += Install;

    [MenuItem("Dead Shot/Player/Setup BRAZO Injection Retarget")]
    static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        PlayerHealth health = Object.FindObjectOfType<PlayerHealth>(true);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BrazoPath);
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(BrazoPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .OrderByDescending(c => c.length).FirstOrDefault();
        if (health == null || prefab == null || clip == null) return;
        BrazoInjectionRetargeter retarget = health.GetComponent<BrazoInjectionRetargeter>();
        if (retarget == null) retarget = Undo.AddComponent<BrazoInjectionRetargeter>(health.gameObject);
        Undo.RecordObject(retarget, "Assign BRAZO injection source");
        retarget.brazoPrefab = prefab;
        retarget.brazoClip = clip;
        retarget.bakedClip = BrazoInjectionBakeUtility.Bake(retarget);
        EditorUtility.SetDirty(retarget);
        EditorSceneManager.MarkSceneDirty(health.gameObject.scene);
        EditorSceneManager.SaveScene(health.gameObject.scene);
        Debug.Log($"[BRAZO Retarget] Generic arms driver assigned. Clip={clip.name}, Length={clip.length:F2}s", retarget);
    }
}
#endif
