using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class GrenadeExplosionSetup
{
    static GrenadeExplosionSetup() { EditorApplication.delayCall+=Apply; }
    [MenuItem("Dead Shot/Player/Assign Grenade Explosion Effect")]
    static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/WarFX/_Effects/Explosions/WFX_ExplosiveSmokeGround.prefab");
        if(prefab==null) return;
        foreach(var player in Object.FindObjectsOfType<PlayerGrenadeAnimation>(true))
        {
            if(player.explosionPrefab!=null) continue;
            Undo.RecordObject(player,"Assign grenade explosion");
            player.explosionPrefab=prefab;
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log("[Grenade] WFX_ExplosiveSmokeGround assigned. Save the scene to retain the reference.",player);
        }
    }
}
