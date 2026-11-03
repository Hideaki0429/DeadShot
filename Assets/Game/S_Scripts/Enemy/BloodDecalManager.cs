// 【役割】床や壁に付着する血痕デカールを生成・管理する。
using System.Collections.Generic;
using UnityEngine;

public sealed class BloodDecalManager : MonoBehaviour
{
    private static BloodDecalManager instance;
    private readonly Queue<GameObject> activeDecals = new Queue<GameObject>();
    public static void Spawn(Vector3 point, Vector3 normal, Material[] materials, Color color, Vector2 scaleRange, float lifetime, int maximumDecals)
    {
        if (materials == null || materials.Length == 0)
        {
            return;
        }

        EnsureInstance();
        instance.SpawnInternal(point, normal, materials, color, scaleRange, lifetime, maximumDecals);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("Blood Decal Manager");
        instance = managerObject.AddComponent<BloodDecalManager>();
    }

    private void SpawnInternal(Vector3 point, Vector3 normal, Material[] materials, Color color, Vector2 scaleRange, float lifetime, int maximumDecals)
    {
        Material material = GetRandomMaterial(materials);
        if (material == null)
        {
            return;
        }

        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
        decal.name = "Blood Surface Decal";
        decal.layer = 2;
        decal.transform.SetPositionAndRotation(point + safeNormal * 0.006f, Quaternion.LookRotation(safeNormal) * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward));
        float scale = Random.Range(Mathf.Min(scaleRange.x, scaleRange.y), Mathf.Max(scaleRange.x, scaleRange.y));
        decal.transform.localScale = new Vector3(scale * Random.Range(0.7f, 1.3f), scale * Random.Range(0.75f, 1.25f), 1f);
        Collider decalCollider = decal.GetComponent<Collider>();
        if (decalCollider != null)
        {
            Destroy(decalCollider);
        }

        MeshRenderer renderer = decal.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        properties.SetColor("_Color", color);
        properties.SetColor("_EmissionColor", color * 0.12f);
        properties.SetFloat("_Glossiness", 0.78f);
        properties.SetFloat("_Metallic", 0f);
        renderer.SetPropertyBlock(properties);
        activeDecals.Enqueue(decal);
        Destroy(decal, Mathf.Max(0.1f, lifetime));
        RemoveExpiredReferences();
        int safeMaximum = Mathf.Max(1, maximumDecals);
        while (activeDecals.Count > safeMaximum)
        {
            GameObject oldest = activeDecals.Dequeue();
            if (oldest != null)
            {
                Destroy(oldest);
            }
        }
    }

    private static Material GetRandomMaterial(Material[] materials)
    {
        int startIndex = Random.Range(0, materials.Length);
        for (int offset = 0; offset < materials.Length; offset++)
        {
            Material candidate = materials[(startIndex + offset) % materials.Length];
            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private void RemoveExpiredReferences()
    {
        while (activeDecals.Count > 0 && activeDecals.Peek() == null)
        {
            activeDecals.Dequeue();
        }
    }
}
