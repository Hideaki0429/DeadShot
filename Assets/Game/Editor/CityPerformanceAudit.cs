#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class CityPerformanceAudit
{
    [MenuItem("Tools/Dead Shot/Diagnostics/Install City Distance Visibility")]
    static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.name.Equals("Stage_city", System.StringComparison.OrdinalIgnoreCase)) return;
        var existing = Object.FindObjectOfType<CityDistanceVisibility>();
        if (existing == null)
        {
            var go = new GameObject("City Distance Visibility");
            Undo.RegisterCreatedObjectUndo(go, "Add city visibility settings");
            existing = go.AddComponent<CityDistanceVisibility>();
        }
        Selection.activeGameObject = existing.gameObject;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }
    [MenuItem("Tools/Dead Shot/Diagnostics/Analyze City")]
    static void Analyze()
    {
        var renderers = Object.FindObjectsOfType<MeshFilter>();
        var sorted = renderers.Where(x => x.sharedMesh != null)
            .Select(x => new { filter = x, triangles = Enumerable.Range(0, x.sharedMesh.subMeshCount).Sum(i => (long)x.sharedMesh.GetIndexCount(i) / 3) })
            .OrderByDescending(x => x.triangles).ToArray();
        Debug.Log("[City Audit] Active meshes=" + sorted.Length + " triangles=" + sorted.Sum(x => x.triangles) +
            " lights=" + Object.FindObjectsOfType<Light>().Length + " enemies=" + Object.FindObjectsOfType<EnemyManager>().Length);
        foreach (var item in sorted.Take(12)) Debug.Log("[City Audit] " + item.filter.name + " triangles=" + item.triangles, item.filter);
    }
}
#endif
