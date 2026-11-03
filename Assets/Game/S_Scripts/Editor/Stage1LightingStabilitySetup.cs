// 【役割】Editor上でStage1の照明設定を安定化する。
// 【実行場所】Unity Editor専用。設定の作成・適用処理を担当します。
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Stage1専用のライトベイクを作成するまでの安定設定。
/// 病院PrefabのBaked Lightを影なしRealtimeへ変換し、誤った他シーンの
/// LightingDataに依存せず一定の明るさで表示します。
/// </summary>
[InitializeOnLoad]
public static class Stage1LightingStabilitySetup
{
    const int Version = 1;
    const string VersionKey = "DeadShot.Stage1LightingStabilityVersion";
    const string HospitalPath = "Assets/Abandoned_Asylum/Prefabs/Asylum.prefab";
    static Stage1LightingStabilitySetup() => EditorApplication.delayCall += SetupIfNeeded;
    [MenuItem("Dead Shot/Stage1/Fix Hospital Lighting Stability")]
    public static void Apply()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HospitalPath);
        if (root == null)
        {
            return;
        }

        int count = 0;
        try
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                // Intensity・Range・Colorは変更しない。
                light.lightmapBakeType = LightmapBakeType.Realtime;
                light.shadows = LightShadows.None;
                count++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, HospitalPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log("[Stage1 Lighting] 病院内ライト " + count + "個を安定化しました。Intensity/Range/Colorは維持しています。");
    }

    static void SetupIfNeeded()
    {
        if (EditorPrefs.GetInt(VersionKey, 0) < Version)
        {
            Apply();
        }
    }
}
#endif

