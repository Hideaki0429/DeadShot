using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PauseMenuSetup
{
    static PauseMenuSetup() { EditorApplication.delayCall += Configure; }

    [MenuItem("Tools/Dead Shot/Configure Pause Menu")]
    private static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Stage_city") return;
        if (Object.FindObjectsOfType<PauseMenuController>(true).Any(p => p.gameObject.scene == scene)) return;
        Canvas canvas = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true))
            .FirstOrDefault(c => c.name == "Canvas");
        if (canvas == null) return;
        Transform ui = canvas.transform.Find("ポーズUI");
        if (ui == null) return;
        RectTransform accent = ui.Find("項目/カーソルLINE") as RectTransform;
        string[] paths = { "再開", "操作方法", "設定", "タイトル" };
        RectTransform[] labels = paths.Select(p => ui.Find("項目/" + p) as RectTransform).ToArray();
        if (accent == null || labels.Any(t => t == null))
        {
            Debug.LogWarning("[PauseSetup] 項目またはカーソルLINEが見つかりません。");
            return;
        }
        var go = new GameObject("Pause System");
        Undo.RegisterCreatedObjectUndo(go, "Configure Pause Menu");
        var controller = Undo.AddComponent<PauseMenuController>(go);
        var visible = new List<CanvasGroup> { Group(ui.gameObject) };
        var hidden = new List<CanvasGroup>();
        foreach (Transform child in canvas.transform)
        {
            if (child == ui) continue;
            if (child.name == "ポーズフィルター") visible.Add(Group(child.gameObject));
            else hidden.Add(Group(child.gameObject));
        }
        controller.pauseGroups = visible.ToArray();
        controller.hideGroups = hidden.ToArray();
        controller.selectionAccent = accent;
        controller.items = new PauseMenuController.MenuItem[labels.Length];
        Vector3 baseLocal = accent.localPosition;
        for (int i = 0; i < labels.Length; i++)
        {
            var point = new GameObject("PauseAccent_" + paths[i], typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(point, "Create Pause Accent Position");
            var rect = (RectTransform)point.transform;
            rect.SetParent(accent.parent, false);
            rect.anchorMin = accent.anchorMin;
            rect.anchorMax = accent.anchorMax;
            rect.pivot = accent.pivot;
            rect.sizeDelta = Vector2.zero;
            // Preserve the designed horizontal alignment; move by each label's vertical offset.
            rect.localPosition = baseLocal + Vector3.up * (labels[i].localPosition.y - labels[0].localPosition.y);
            controller.items[i] = new PauseMenuController.MenuItem { itemName = paths[i], accentPosition = rect };
        }
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PauseSetup] Stage_city: 4項目・カーソルLINE・フィルターを登録して保存しました。");
    }

    private static CanvasGroup Group(GameObject go)
    {
        var group = go.GetComponent<CanvasGroup>();
        return group != null ? group : Undo.AddComponent<CanvasGroup>(go);
    }
}
