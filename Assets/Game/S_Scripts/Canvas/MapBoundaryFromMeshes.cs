// 【役割】背景メッシュの範囲からマップの境界を求める。
using UnityEngine;

public class MapBoundaryFromMeshes : MonoBehaviour
{
    [Header("囲みたいマップMesh")]
    public MeshFilter[] mapMeshes;
    [Header("壁設定")]
    public float wallHeight = 5f;
    public float wallThickness = 1f;
    public float extraMargin = 1f;
    [Header("表示設定")]
    public bool generateOnStart = true;
    public bool showGizmos = true;
    private Bounds combinedBounds;
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        if (generateOnStart)
        {
            GenerateBoundaryWalls();
        }
    }

    [ContextMenu("Generate Boundary Walls")]
    public void GenerateBoundaryWalls()
    {
        if (mapMeshes == null || mapMeshes.Length == 0)
        {
            Debug.LogWarning("Map Meshes が設定されていません。");
            return;
        }

        DeleteOldWalls();
        combinedBounds = GetCombinedMeshBounds();
        float minX = combinedBounds.min.x - extraMargin;
        float maxX = combinedBounds.max.x + extraMargin;
        float minZ = combinedBounds.min.z - extraMargin;
        float maxZ = combinedBounds.max.z + extraMargin;
        float width = maxX - minX;
        float depth = maxZ - minZ;
        float centerX = (minX + maxX) / 2f;
        float centerZ = (minZ + maxZ) / 2f;
        float centerY = combinedBounds.center.y + wallHeight / 2f;
        CreateWall("Wall_North", new Vector3(centerX, centerY, maxZ), new Vector3(width + wallThickness * 2f, wallHeight, wallThickness));
        CreateWall("Wall_South", new Vector3(centerX, centerY, minZ), new Vector3(width + wallThickness * 2f, wallHeight, wallThickness));
        CreateWall("Wall_East", new Vector3(maxX, centerY, centerZ), new Vector3(wallThickness, wallHeight, depth));
        CreateWall("Wall_West", new Vector3(minX, centerY, centerZ), new Vector3(wallThickness, wallHeight, depth));
        Debug.Log("指定したMesh範囲を囲む壁Colliderを生成しました。");
    }

    Bounds GetCombinedMeshBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds();
        foreach (MeshFilter meshFilter in mapMeshes)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            Bounds meshBounds = GetWorldBounds(meshFilter);
            if (!hasBounds)
            {
                bounds = meshBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(meshBounds);
            }
        }

        return bounds;
    }

    Bounds GetWorldBounds(MeshFilter meshFilter)
    {
        Mesh mesh = meshFilter.sharedMesh;
        Bounds localBounds = mesh.bounds;
        Vector3 center = meshFilter.transform.TransformPoint(localBounds.center);
        Vector3 size = Vector3.Scale(localBounds.size, meshFilter.transform.lossyScale);
        return new Bounds(center, size);
    }

    void CreateWall(string wallName, Vector3 position, Vector3 size)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(transform);
        wall.transform.position = position;
        BoxCollider boxCollider = wall.AddComponent<BoxCollider>();
        boxCollider.size = size;
        boxCollider.isTrigger = false;
        wall.layer = gameObject.layer;
    }

    void DeleteOldWalls()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }

    void OnDrawGizmos()
    {
        if (!showGizmos)
        {
            return;
        }

        if (mapMeshes == null || mapMeshes.Length == 0)
        {
            return;
        }

        Bounds bounds = GetCombinedMeshBounds();
        float minX = bounds.min.x - extraMargin;
        float maxX = bounds.max.x + extraMargin;
        float minZ = bounds.min.z - extraMargin;
        float maxZ = bounds.max.z + extraMargin;
        float width = maxX - minX;
        float depth = maxZ - minZ;
        float centerX = (minX + maxX) / 2f;
        float centerZ = (minZ + maxZ) / 2f;
        float centerY = bounds.center.y + wallHeight / 2f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new Vector3(centerX, centerY, maxZ), new Vector3(width + wallThickness * 2f, wallHeight, wallThickness));
        Gizmos.DrawWireCube(new Vector3(centerX, centerY, minZ), new Vector3(width + wallThickness * 2f, wallHeight, wallThickness));
        Gizmos.DrawWireCube(new Vector3(maxX, centerY, centerZ), new Vector3(wallThickness, wallHeight, depth));
        Gizmos.DrawWireCube(new Vector3(minX, centerY, centerZ), new Vector3(wallThickness, wallHeight, depth));
    }
}
