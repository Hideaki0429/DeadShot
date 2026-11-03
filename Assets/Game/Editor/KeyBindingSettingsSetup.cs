using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class KeyBindingSettingsSetup
{
    static KeyBindingSettingsSetup() { EditorApplication.delayCall += Setup; }
    [MenuItem("Tools/Dead Shot/Setup Key Binding Settings")]
    static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Stage_city") return;
        foreach (var pause in Object.FindObjectsOfType<PauseMenuController>(true))
        {
            if (pause.gameObject.scene != scene || pause.settingsPanel == null) continue;
            if (pause.settingsPanel.GetComponent<KeyBindingSettingsUI>() != null) continue;
            var old = pause.settingsPanel;
            // Preserve the previous placeholder as an inactive backup rather than deleting it.
            var go = new GameObject("Key Binding Settings", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.SetActive(false); go.transform.SetParent(old.transform.parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = Color.black;
            var ui = go.AddComponent<KeyBindingSettingsUI>(); ui.pauseMenu = pause; ui.font = pause.descriptionText.font; ui.Build();
            pause.settingsPanel = go.GetComponent<CanvasGroup>();
            // This panel owns its Back listener and keyboard focus.
            pause.settingsBackButton = null;
            old.gameObject.SetActive(false);
            EditorUtility.SetDirty(pause); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[KeyBindingSetup] スクロール対応キー設定UIをStage_cityに登録しました。");
        }
    }
}
