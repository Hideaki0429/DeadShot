// 【役割】Editor上でスタンドライトPrefabへ左右のSpot Lightを設定する。
// 【実行場所】Unity Editor専用。設定の作成・適用処理を担当します。
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>StandLight Prefabへ調整可能な二灯式Spot Lightを追加する。</summary>
[InitializeOnLoad]
public static class StandLightPrefabSetup
{
    const string PrefabPath = "Assets/Game/Prefabs/StandLight.prefab";
    const string VersionKey = "DeadShot.StandLightPrefabSetupVersion";
    const int Version = 1;
    static StandLightPrefabSetup() => EditorApplication.delayCall += SetupIfNeeded;
    [MenuItem("Dead Shot/Environment/Setup Stand Light")]
    public static void Apply()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            return;
        }

        try
        {
            StandLightRig rig = root.GetComponent<StandLightRig>();
            if (rig == null)
            {
                rig = root.AddComponent<StandLightRig>();
            }

            rig.EnsureLights();
            rig.ApplySettings();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            EditorPrefs.SetInt(VersionKey, Version);
            AssetDatabase.SaveAssets();
            Debug.Log("[StandLight] 左右のSpot LightをPrefabへ設定しました。");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void SetupIfNeeded()
    {
        if (EditorPrefs.GetInt(VersionKey, 0) >= Version)
        {
            return;
        }

        Apply();
    }
}
#endif

