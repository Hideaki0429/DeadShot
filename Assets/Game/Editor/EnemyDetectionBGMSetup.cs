using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyDetectionBGMSetup
{
    static EnemyDetectionBGMSetup() { EditorApplication.delayCall += Ensure; }
    [MenuItem("Dead Shot/Audio/Select Enemy Detection BGM")]
    static void Select()
    {
        Ensure();
        var bgm = Object.FindObjectOfType<EnemyDetectionBGM>(true);
        if (bgm != null) Selection.activeGameObject = bgm.gameObject;
    }
    static void Ensure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Game/S/Stage_city.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Game/S/Stage_city.unity", OpenSceneMode.Additive);
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<EnemyDetectionBGM>(true) != null)
            { if (opened) EditorSceneManager.CloseScene(scene, true); return; }
        var go = new GameObject("Enemy Detection BGM");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<EnemyDetectionBGM>();
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("[BGM] Enemy Detection BGM created. Assign Bgm Clip in Inspector.");
    }
}
