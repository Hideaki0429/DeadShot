using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ItemPickupNotificationSetup
{
    static ItemPickupNotificationSetup()
    {
        EditorApplication.delayCall += EnsureEditableNotification;
    }

    [MenuItem("Tools/Dead Shot/Setup Editable Pickup Notification")]
    public static void EnsureEditableNotification()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Stage_city") return;

        ItemPickupNotificationUI ui = Object.FindObjectOfType<ItemPickupNotificationUI>(true);
        bool changed = false;
        if (ui == null)
        {
            GameObject root = new GameObject("Item Pickup Notification");
            Undo.RegisterCreatedObjectUndo(root, "Create Pickup Notification UI");
            ui = root.AddComponent<ItemPickupNotificationUI>();
            changed = true;
        }

        if (!ui.HasEditableHierarchy)
        {
            ui.BuildEditableHierarchy();
            changed = true;
        }

        if (!changed) return;
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ItemPickupNotificationSetup] 取得通知UIをHierarchy編集式で作成しました。");
    }
}
