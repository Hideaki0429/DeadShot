using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class ZombieSeverSetup
{
    static ZombieSeverSetup() { EditorApplication.delayCall += Ensure; }
    static void Ensure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemy/MobZombie/MZombie.prefab");
        if(prefab == null) return;
        foreach(var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string path = AssetDatabase.GetAssetPath(renderer.sharedMesh);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if(importer != null && !importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        }
    }
}
