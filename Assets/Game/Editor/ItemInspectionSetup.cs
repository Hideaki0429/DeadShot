using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ItemInspectionSetup
{
    static ItemInspectionSetup() { EditorApplication.delayCall += EnsureEditablePanel; }

    [MenuItem("Tools/Dead Shot/Setup Editable Item Inspection Panel")]
    public static void EnsureEditablePanel()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Stage_city") return;

        ItemInspectionUI ui = Object.FindObjectOfType<ItemInspectionUI>(true);
        bool changed = false;
        if (ui == null)
        {
            GameObject root = new GameObject("Item Inspection System");
            Undo.RegisterCreatedObjectUndo(root, "Create Item Inspection UI");
            ui = root.AddComponent<ItemInspectionUI>();
            changed = true;
        }

        if (!ui.HasEditableHierarchy) { ui.BuildEditableHierarchy(); changed = true; }
        if (!changed) return;
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ItemInspectionSetup] Item Inspection CanvasをHierarchy編集式で作成しました。");
    }
}
