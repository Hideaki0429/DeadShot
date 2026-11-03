// 【役割】拠点の支給品箱に中身と取得動作の設定を適用する。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>拠点テントの既存ボックスに、支給品取得機能を自動設定します。</summary>
public static class SupplyBoxInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayerInventory inventory = UnityEngine.Object.FindObjectOfType<PlayerInventory>(true);
        Transform[] transforms = UnityEngine.Object.FindObjectsOfType<Transform>(true);
        foreach (Transform target in transforms)
        {
            InventoryPickup.Reward[] rewards = RewardsFor(target.name);
            if (rewards == null)
            {
                continue;
            }

            Configure(target.gameObject, inventory, rewards);
        }
    }

    static InventoryPickup.Reward[] RewardsFor(string objectName)
    {
        switch (objectName)
        {
            case "AE_Ammunition_Box_01":
                return new[]
                {
                    new InventoryPickup.Reward("m1911", 1),
                    new InventoryPickup.Reward("m1913", 1),
                    new InventoryPickup.Reward("m1912", 1),
                    new InventoryPickup.Reward("m1914", 1)
                };
            case "AE_Ammunition_Box_02":
                return new[]
                {
                    new InventoryPickup.Reward("m1918", 1)
                };
            case "AE_Ammunition_Box_03":
                return new[]
                {
                    new InventoryPickup.Reward("m1916", 3)
                };
            case "AE_Ammunition_Box_04":
                return new[]
                {
                    new InventoryPickup.Reward("m1917", 100),
                    new InventoryPickup.Reward("m1922", 21),
                    new InventoryPickup.Reward("m1919", 1),
                    new InventoryPickup.Reward("m1920", 1)
                };
            case "AE_Medical_Box":
                return new[]
                {
                    new InventoryPickup.Reward("m1924", 1),
                    new InventoryPickup.Reward("m1915", 5)
                };
            default:
                return null;
        }
    }

    static void Configure(GameObject box, PlayerInventory inventory, InventoryPickup.Reward[] rewards)
    {
        InventoryPickup pickup = box.GetComponent<InventoryPickup>();
        // Prefabに保存済みの設定は実行時に上書きしない。
        if (pickup != null)
        {
            if (pickup.inventory == null)
            {
                pickup.inventory = inventory;
            }

            return;
        }

        // 古いシーンやPrefabの互換用フォールバック。
        pickup = box.AddComponent<InventoryPickup>();
        pickup.inventory = inventory;
        pickup.isSupplyContainer = true;
        pickup.containerPrompt = "支給品を受け取る";
        pickup.containerRewards = rewards;
        box.tag = "Item";
        SphereCollider trigger = pickup.Trigger;
        trigger.isTrigger = true;
        Bounds bounds = CalculateBounds(box);
        trigger.center = box.transform.InverseTransformPoint(bounds.center);
        float scale = Mathf.Max(Mathf.Abs(box.transform.lossyScale.x), Mathf.Abs(box.transform.lossyScale.y), Mathf.Abs(box.transform.lossyScale.z));
        trigger.radius = (Mathf.Max(bounds.extents.x, bounds.extents.z) + 1.6f) / Mathf.Max(.0001f, scale);
        pickup.lidParts = FindTopParts(box);
        pickup.lidOpenEuler = new Vector3(0f, 0f, Mathf.Max(1f, pickup.lidOpenEuler.z));
    }

    static Transform[] FindTopParts(GameObject box)
    {
        List<Transform> topParts = new List<Transform>();
        foreach (Transform child in box.GetComponentsInChildren<Transform>(true))
        {
            if (child == box.transform || child.name.IndexOf("Top", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            // LOD1/LOD2はTop LOD0の子なので、親と同時に回すと回転が二重になる。
            bool hasTopAncestor = false;
            for (Transform parent = child.parent; parent != null && parent != box.transform; parent = parent.parent)
            {
                if (parent.name.IndexOf("Top", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasTopAncestor = true;
                    break;
                }
            }

            if (hasTopAncestor)
            {
                continue;
            }

            topParts.Add(child);
        }

        return topParts.Count > 0 ? topParts.ToArray() : Array.Empty<Transform>();
    }

    static Bounds CalculateBounds(GameObject box)
    {
        Renderer[] renderers = box.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(box.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }
}
