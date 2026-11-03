using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GameDamageSettingsSetup
{
    const string Folder="Assets/Game/Resources";
    const string Path=Folder+"/DeadShotDamageSettings.asset";
    static GameDamageSettingsSetup(){EditorApplication.delayCall+=EnsureDelayed;}
    static void EnsureDelayed(){Ensure();}
    [MenuItem("Dead Shot/Combat/Open Damage Settings")]
    static void Open(){Selection.activeObject=Ensure();}
    static GameDamageSettings Ensure()
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameDamageSettings>(Path);
        if(asset!=null)
        {
            if(asset.EnsureDefaultWeaponProfiles())
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
            return asset;
        }
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Game","Resources");
        asset=ScriptableObject.CreateInstance<GameDamageSettings>();
        asset.EnsureDefaultWeaponProfiles();
        AssetDatabase.CreateAsset(asset,Path); AssetDatabase.SaveAssets();
        Debug.Log("[Damage] 共通ダメージ設定を作成しました: "+Path);
        return asset;
    }
}
