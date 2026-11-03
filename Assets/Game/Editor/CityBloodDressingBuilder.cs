using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CityBloodDressingBuilder
{
    private const string RootName = "City Horror Dressing";
    private const string PrefabDirectory = "Assets/Blood decal pack/blood/prefab";
    private const int RandomSeed = 318;
    private const int IncidentClusterCount = 12;
    private const int ScatteredFloorDecals = 20;
    private const int WallDecals = 8;
    private const float MinimumGroundY = -2f;
    private const float MaximumGroundY = 10f;
    private const float SurfaceOffset = 0.015f;

    [MenuItem("Tools/Dead Shot/Build City Blood Dressing")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[City Blood] Play Modeを終了してから実行してください。");
            return;
        }

        PolygonWallBuilder polygonWall = UnityEngine.Object.FindObjectOfType<PolygonWallBuilder>();
        GameObject[] bloodPrefabs = LoadBloodPrefabs();
        if (polygonWall == null || polygonWall.points == null ||
            polygonWall.points.Count < 3 || bloodPrefabs.Length == 0)
        {
            Debug.LogError("[City Blood] PolygonWallBuilderまたはBlood Prefabが見つかりません。");
            return;
        }

        ClearInternal();
        UnityEngine.Random.State previousRandomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(RandomSeed);

        GameObject root = new GameObject(RootName);
        GameObject clustersRoot = CreateChild("Incident Clusters", root.transform);
        GameObject scatteredRoot = CreateChild("Scattered Floor Blood", root.transform);
        GameObject wallRoot = CreateChild("Wall Blood", root.transform);

        List<Vector2> polygon = polygonWall.points
            .Select(point => new Vector2(point.x, point.z))
            .ToList();
        Rect bounds = CalculateBounds(polygon);
        List<Vector3> clusterCenters = new List<Vector3>();
        int floorCount = 0;
        int wallCount = 0;

        for (int clusterIndex = 0; clusterIndex < IncidentClusterCount; clusterIndex++)
        {
            if (!TryFindRandomGround(polygon, bounds, out RaycastHit centerHit))
                continue;

            clusterCenters.Add(centerHit.point);
            GameObject cluster = CreateChild(
                $"Incident Cluster {clusterIndex + 1:00}",
                clustersRoot.transform);
            int decalCount = UnityEngine.Random.Range(3, 7);

            for (int decalIndex = 0; decalIndex < decalCount; decalIndex++)
            {
                float radius = decalIndex == 0
                    ? UnityEngine.Random.Range(0f, 1.5f)
                    : UnityEngine.Random.Range(1.2f, 7f);
                Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
                Vector3 candidate = centerHit.point +
                    new Vector3(direction.x, 0f, direction.y) * radius;

                if (!TryFindGroundAt(candidate.x, candidate.z, out RaycastHit hit))
                    continue;
                if (!IsInsidePolygon(new Vector2(hit.point.x, hit.point.z), polygon))
                    continue;

                float targetSize = decalIndex == 0
                    ? UnityEngine.Random.Range(2.5f, 5f)
                    : UnityEngine.Random.Range(0.8f, 2.2f);
                PlaceBloodPrefab(
                    bloodPrefabs,
                    hit.point,
                    hit.normal,
                    targetSize,
                    cluster.transform,
                    $"Cluster Blood {decalIndex + 1:00}");
                floorCount++;
            }
        }

        for (int index = 0; index < ScatteredFloorDecals; index++)
        {
            if (!TryFindRandomGround(polygon, bounds, out RaycastHit hit))
                continue;

            PlaceBloodPrefab(
                bloodPrefabs,
                hit.point,
                hit.normal,
                UnityEngine.Random.Range(0.5f, 1.5f),
                scatteredRoot.transform,
                $"Scattered Blood {index + 1:00}");
            floorCount++;
        }

        for (int index = 0; index < WallDecals && clusterCenters.Count > 0; index++)
        {
            Vector3 origin = clusterCenters[UnityEngine.Random.Range(0, clusterCenters.Count)];
            if (!TryFindWall(origin, out RaycastHit wallHit))
                continue;

            PlaceBloodPrefab(
                bloodPrefabs,
                wallHit.point,
                wallHit.normal,
                UnityEngine.Random.Range(0.8f, 2f),
                wallRoot.transform,
                $"Wall Blood {index + 1:00}");
            wallCount++;
        }

        UnityEngine.Random.state = previousRandomState;
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        MeshRenderer firstBlood = root.GetComponentInChildren<MeshRenderer>();
        MeshFilter firstMeshFilter = root.GetComponentInChildren<MeshFilter>();
        if (firstMeshFilter != null && firstMeshFilter.sharedMesh != null &&
            firstMeshFilter.sharedMesh.normals.Length > 0)
        {
            Debug.Log(
                $"[City Blood] Mesh:{firstMeshFilter.sharedMesh.name} / " +
                $"LocalNormal:{firstMeshFilter.sharedMesh.normals[0]} / " +
                $"Bounds:{firstMeshFilter.sharedMesh.bounds.size}");
        }
        Selection.activeGameObject = firstBlood != null
            ? firstBlood.gameObject
            : root;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log(
            $"[City Blood] 事件現場:{clusterCenters.Count} / " +
            $"床血痕:{floorCount} / 壁血痕:{wallCount} を配置しました。");
    }

    [MenuItem("Tools/Dead Shot/Clear City Blood Dressing")]
    public static void Clear()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[City Blood] Play Modeを終了してから実行してください。");
            return;
        }

        ClearInternal();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[City Blood] 配置した街の血痕を削除しました。");
    }

    private static GameObject[] LoadBloodPrefabs()
    {
        return Enumerable.Range(1, 7)
            .Select(index => AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabDirectory}/blood{index}.prefab"))
            .Where(prefab => prefab != null)
            .ToArray();
    }

    private static void PlaceBloodPrefab(
        GameObject[] prefabs,
        Vector3 position,
        Vector3 normal,
        float targetSize,
        Transform parent,
        string objectName)
    {
        GameObject prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Length)];
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = objectName;
        instance.transform.SetParent(parent, true);
        instance.transform.position = position + normal * SurfaceOffset;
        Quaternion alignToSurface = Quaternion.FromToRotation(Vector3.up, normal);
        Quaternion randomSurfaceRotation = Quaternion.AngleAxis(
            UnityEngine.Random.Range(0f, 360f),
            normal);
        instance.transform.rotation = randomSurfaceRotation * alignToSurface;

        MeshRenderer meshRenderer = instance.GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null)
        {
            float currentSize = Mathf.Max(
                meshRenderer.bounds.size.x,
                meshRenderer.bounds.size.y,
                meshRenderer.bounds.size.z);
            if (currentSize > 0.001f)
                instance.transform.localScale *= targetSize / currentSize;
        }
        instance.isStatic = true;

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);
    }

    private static bool TryFindRandomGround(
        IReadOnlyList<Vector2> polygon,
        Rect bounds,
        out RaycastHit hit)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Vector2 point = new Vector2(
                UnityEngine.Random.Range(bounds.xMin, bounds.xMax),
                UnityEngine.Random.Range(bounds.yMin, bounds.yMax));
            if (!IsInsidePolygon(point, polygon))
                continue;
            if (TryFindGroundAt(point.x, point.y, out hit))
                return true;
        }

        hit = default;
        return false;
    }

    private static bool TryFindGroundAt(float x, float z, out RaycastHit result)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(x, 60f, z),
            Vector3.down,
            100f,
            ~0,
            QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.point.y < MinimumGroundY || hit.point.y > MaximumGroundY)
                continue;
            if (hit.normal.y < 0.65f)
                continue;
            if (hit.collider != null && hit.collider.transform.IsChildOf(
                    GameObject.Find(RootName)?.transform))
                continue;

            result = hit;
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryFindWall(Vector3 groundPosition, out RaycastHit result)
    {
        float height = UnityEngine.Random.Range(0.6f, 1.8f);
        Vector3 origin = groundPosition + Vector3.up * height;
        float angleOffset = UnityEngine.Random.Range(0f, 360f);

        for (int directionIndex = 0; directionIndex < 12; directionIndex++)
        {
            float angle = angleOffset + directionIndex * 30f;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            if (!Physics.Raycast(
                    origin,
                    direction,
                    out RaycastHit hit,
                    18f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
                continue;
            if (Mathf.Abs(hit.normal.y) > 0.35f)
                continue;

            result = hit;
            return true;
        }

        result = default;
        return false;
    }

    private static Rect CalculateBounds(IReadOnlyList<Vector2> polygon)
    {
        float minX = polygon.Min(point => point.x);
        float maxX = polygon.Max(point => point.x);
        float minY = polygon.Min(point => point.y);
        float maxY = polygon.Max(point => point.y);
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static bool IsInsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        bool inside = false;
        int previous = polygon.Count - 1;

        for (int current = 0; current < polygon.Count; current++)
        {
            Vector2 a = polygon[current];
            Vector2 b = polygon[previous];
            bool crosses = (a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) /
                (b.y - a.y) + a.x;
            if (crosses)
                inside = !inside;
            previous = current;
        }

        return inside;
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void ClearInternal()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);
    }
}
