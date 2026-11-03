// 【役割】Editor上で病院の壁・床・扉に着弾表現用の表面タグを設定する。
// 【実行場所】Unity Editor専用。設定の作成・適用処理を担当します。
#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class Stage1SurfaceTagSetup
{
    const int Version = 1;
    const string VersionKey = "DeadShot.Stage1SurfaceTagSetupVersion";
    const string HospitalPath = "Assets/Abandoned_Asylum/Prefabs/Asylum.prefab";
    static Stage1SurfaceTagSetup() => EditorApplication.delayCall += SetupIfNeeded;
    [MenuItem("Dead Shot/Stage1/Apply Wood And Metal Surface Tags")]
    public static void Apply()
    {
        GameObject hospital = PrefabUtility.LoadPrefabContents(HospitalPath);
        if (hospital == null)
        {
            return;
        }

        int woodCount = 0, metalCount = 0;
        try
        {
            Regex doorName = new Regex(@"^(DoorD?_V[123])( \(\d+\))?$");
            foreach (Transform target in hospital.GetComponentsInChildren<Transform>(true))
            {
                string name = target.name;
                bool isWoodArchitecture = name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Stair", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isWoodArchitecture)
                {
                    woodCount += TagColliders(target, "WOOD");
                }

                Match door = doorName.Match(name);
                if (!door.Success)
                {
                    continue;
                }

                bool woodDoor = door.Groups[1].Value.EndsWith("V1", StringComparison.OrdinalIgnoreCase);
                if (woodDoor)
                {
                    woodCount += TagColliders(target, "WOOD");
                }
                else
                {
                    metalCount += TagColliders(target, "Metal");
                }
            }

            PrefabUtility.SaveAsPrefabAsset(hospital, HospitalPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(hospital);
        }

        AssetDatabase.SaveAssets();
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log("[Stage1 Surface] WOOD Collider:" + woodCount + " / Metal Collider:" + metalCount);
    }

    static int TagColliders(Transform root, string tag)
    {
        int count = 0;
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            collider.gameObject.tag = tag;
            count++;
        }

        return count;
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

