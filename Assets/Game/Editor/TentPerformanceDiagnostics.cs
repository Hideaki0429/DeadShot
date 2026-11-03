#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TentPerformanceDiagnostics
{
    const string PrefabPath = "Assets/Game/Prefabs/Buliding/拠点テント.prefab";

    [MenuItem("Tools/Dead Shot/Diagnostics/Analyze Base Tent")]
    static void Analyze()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[Tent Performance] Prefab not found: " + PrefabPath); return; }

        MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
        SkinnedMeshRenderer[] skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        Light[] lights = prefab.GetComponentsInChildren<Light>(true);
        Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
        ParticleSystem[] particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
        MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        var meshes = new HashSet<Mesh>();
        var materials = new HashSet<Material>();
        long vertices = 0, triangles = 0;

        foreach (MeshFilter filter in filters) if (filter.sharedMesh != null && meshes.Add(filter.sharedMesh))
        { vertices += filter.sharedMesh.vertexCount; triangles += filter.sharedMesh.triangles.LongLength / 3; }
        foreach (SkinnedMeshRenderer renderer in skinned) if (renderer.sharedMesh != null && meshes.Add(renderer.sharedMesh))
        { vertices += renderer.sharedMesh.vertexCount; triangles += renderer.sharedMesh.triangles.LongLength / 3; }
        foreach (Renderer renderer in renderers) foreach (Material material in renderer.sharedMaterials) if (material != null) materials.Add(material);

        int realtimeShadowLights = 0;
        foreach (Light light in lights) if (light.enabled && light.lightmapBakeType == LightmapBakeType.Realtime && light.shadows != LightShadows.None) realtimeShadowLights++;
        string[] dependencies = AssetDatabase.GetDependencies(PrefabPath, true);

        Debug.Log($"[Tent Performance] Renderers={renderers.Length}, UniqueMeshes={meshes.Count}, Vertices={vertices:N0}, Triangles={triangles:N0}, Materials={materials.Count}, Lights={lights.Length}, RealtimeShadowLights={realtimeShadowLights}, Colliders={colliders.Length}, Particles={particles.Length}, Scripts={behaviours.Length}, Dependencies={dependencies.Length}", prefab);
        foreach (Mesh mesh in meshes)
            Debug.Log($"[Tent Mesh] {mesh.name}: vertices={mesh.vertexCount:N0}, triangles={mesh.triangles.LongLength / 3:N0}, subMeshes={mesh.subMeshCount}", mesh);
        foreach (Light light in lights)
            Debug.Log($"[Tent Light] {light.name}: type={light.type}, range={light.range}, shadows={light.shadows}, bake={light.lightmapBakeType}", light);
    }
}
#endif
