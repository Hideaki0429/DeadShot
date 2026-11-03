// 【役割】Editor上で支給品箱Prefabに取得内容と蓋の開閉設定を適用する。
// 【実行場所】Unity Editor専用。設定の作成・適用処理を担当します。
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SupplyBoxPrefabSetup
{
    const int SetupVersion = 2;
    const string VersionKey = "DeadShot.SupplyBoxPrefabSetupVersion";
    const string Folder = "Assets/AE_Battle Royale_Props/Prefabs/";
    static SupplyBoxPrefabSetup()
    {
        EditorApplication.delayCall += SetupIfNeeded;
    }

    [MenuItem("Dead Shot/Setup Supply Box Prefabs")]
    public static void SetupAll()
    {
        SetupPrefab("AE_Ammunition_Box_01", new[] { new InventoryPickup.Reward("m1911", 1), new InventoryPickup.Reward("m1913", 1), new InventoryPickup.Reward("m1912", 1), new InventoryPickup.Reward("m1914", 1) });
        SetupPrefab("AE_Ammunition_Box_02", new[] { new InventoryPickup.Reward("m1918", 1) });
        SetupPrefab("AE_Ammunition_Box_03", new[] { new InventoryPickup.Reward("m1916", 3) });
        SetupPrefab("AE_Ammunition_Box_04", new[] { new InventoryPickup.Reward("m1917", 100), new InventoryPickup.Reward("m1922", 21), new InventoryPickup.Reward("m1919", 1), new InventoryPickup.Reward("m1920", 1) });
        SetupPrefab("AE_Medical_Box", new[] { new InventoryPickup.Reward("m1924", 1), new InventoryPickup.Reward("m1915", 5) });
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorPrefs.SetInt(VersionKey, SetupVersion);
        Debug.Log("[SupplyBox] 5種類のPrefabへInventory Pickupを保存しました。");
    }

    static void SetupIfNeeded()
    {
        if (EditorPrefs.GetInt(VersionKey, 0) >= SetupVersion)
        {
            return;
        }

        SetupAll();
    }

    static void SetupPrefab(string prefabName, InventoryPickup.Reward[] rewards)
    {
        string path = Folder + prefabName + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            return;
        }

        try
        {
            InventoryPickup pickup = root.GetComponent<InventoryPickup>();
            bool created = pickup == null;
            if (created)
            {
                pickup = root.AddComponent<InventoryPickup>();
            }

            root.tag = "Item";
            pickup.isSupplyContainer = true;
            pickup.containerPrompt = "支給品を受け取る";
            if (created || pickup.containerRewards == null || pickup.containerRewards.Length == 0)
            {
                pickup.containerRewards = rewards;
            }

            if (created)
            {
                pickup.lidOpenEuler = new Vector3(0f, 0f, 105f);
                pickup.lidOpenDuration = .45f;
                pickup.lidOpenSeVolume = .8f;
            }

            pickup.lidParts = FindTopParts(root);
            foreach (Transform lid in pickup.lidParts)
            {
                ClearStaticRecursively(lid);
            }

            ConfigureTrigger(root, pickup.Trigger);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform[] FindTopParts(GameObject root)
    {
        List<Transform> result = new List<Transform>();
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root.transform || child.name.IndexOf("Top", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            bool hasTopAncestor = false;
            for (Transform parent = child.parent; parent != null && parent != root.transform; parent = parent.parent)
            {
                if (parent.name.IndexOf("Top", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasTopAncestor = true;
                    break;
                }
            }

            if (!hasTopAncestor)
            {
                result.Add(child);
            }
        }

        return result.ToArray();
    }

    static void ClearStaticRecursively(Transform target)
    {
        target.gameObject.isStatic = false;
        foreach (Transform child in target)
        {
            ClearStaticRecursively(child);
        }
    }

    static void ConfigureTrigger(GameObject root, SphereCollider trigger)
    {
        trigger.isTrigger = true;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            trigger.radius = 1f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        trigger.center = root.transform.InverseTransformPoint(bounds.center);
        float scale = Mathf.Max(Mathf.Abs(root.transform.lossyScale.x), Mathf.Abs(root.transform.lossyScale.y), Mathf.Abs(root.transform.lossyScale.z));
        trigger.radius = (Mathf.Max(bounds.extents.x, bounds.extents.z) + 1.6f) / Mathf.Max(.0001f, scale);
    }
}
#endif

