using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TitleOptionsSync
{
    static TitleOptionsSync() { EditorApplication.delayCall += Auto; }
    static void Auto() { Sync(false); }
    [MenuItem("Dead Shot/UI/Sync Title Options From Stage")]
    static void Manual() { Sync(true); }
    static void Sync(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var titleScene = SceneManager.GetSceneByPath("Assets/Game/S/Title2.unity");
        bool openedTitle = !titleScene.isLoaded;
        if (openedTitle) titleScene = EditorSceneManager.OpenScene("Assets/Game/S/Title2.unity", OpenSceneMode.Additive);
        TitleMenuController title = null;
        foreach(var root in titleScene.GetRootGameObjects())
        { title = root.GetComponentInChildren<TitleMenuController>(true); if(title != null) break; }
        if(title == null || (!force && title.settings != null && title.settings.name == "Title Options (Stage Synced)"))
        { if(openedTitle) EditorSceneManager.CloseScene(titleScene,true); return; }
        var stage = SceneManager.GetSceneByPath("Assets/Game/S/Stage_city.unity");
        bool openedStage = !stage.isLoaded;
        if(openedStage) stage = EditorSceneManager.OpenScene("Assets/Game/S/Stage_city.unity",OpenSceneMode.Additive);
        KeyBindingSettingsUI template = null;
        foreach(var root in stage.GetRootGameObjects())
        { template = root.GetComponentInChildren<KeyBindingSettingsUI>(true); if(template != null) break; }
        if(template != null && title.settings != null)
        {
            var old = title.settings;
            var copy = Object.Instantiate(template.gameObject,old.transform.parent);
            copy.name = "Title Options (Stage Synced)";
            var rect = (RectTransform)copy.transform;
            rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
            rect.offsetMin=rect.offsetMax=Vector2.zero; rect.localScale=Vector3.one;
            copy.SetActive(false);
            var ui=copy.GetComponent<KeyBindingSettingsUI>(); ui.pauseMenu=null; ui.titleMenu=title;
            if(ui.cameraSettings != null) ui.cameraSettings.switchCamera=null;
            title.settings=ui;
            old.gameObject.SetActive(false); // 旧UIは復旧用に保持
            EditorUtility.SetDirty(title);
            EditorSceneManager.MarkSceneDirty(titleScene); EditorSceneManager.SaveScene(titleScene);
            Debug.Log("[Title Options] Stage UI copied; title navigation and shared preferences connected.");
        }
        if(openedStage) EditorSceneManager.CloseScene(stage,true);
        if(openedTitle) EditorSceneManager.CloseScene(titleScene,true);
    }
}
