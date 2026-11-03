using System.Collections.Generic;
using UnityEngine;

public class PolygonWallBuilder : MonoBehaviour
{
    [Header("クリックで作成した壁の頂点")]
    public List<Vector3> points = new List<Vector3>();

    [Header("壁設定")]
    public float wallHeight = 5f;
    public float wallThickness = 1f;
    public bool closeLoop = true;

    [Header("表示")]
    public bool showGizmos = true;

    [ContextMenu("Generate Walls")]
    public void GenerateWalls()
    {
        ClearWalls();

        if (points.Count < 2)
        {
            Debug.LogWarning("壁を生成するには最低2点必要です。");
            return;
        }

        int count = closeLoop ? points.Count : points.Count - 1;

        for (int i = 0; i < count; i++)
        {
            Vector3 start = points[i];
            Vector3 end = points[(i + 1) % points.Count];

            CreateWall(start, end, i);
        }

        Debug.Log("クリックした点をもとに壁Colliderを生成しました。");
    }

    public void ClearWalls()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }

    void CreateWall(Vector3 start, Vector3 end, int index)
    {
        Vector3 mid = (start + end) / 2f;
        Vector3 direction = end - start;
        float length = direction.magnitude;

        GameObject wall = new GameObject("PolygonWall_" + index);
        wall.transform.SetParent(transform);

        wall.transform.position = new Vector3(
            mid.x,
            mid.y + wallHeight / 2f,
            mid.z
        );

        wall.transform.rotation = Quaternion.LookRotation(direction);

        BoxCollider col = wall.AddComponent<BoxCollider>();
        col.size = new Vector3(wallThickness, wallHeight, length);
        col.isTrigger = false;
    }

    public void AddPoint(Vector3 point)
    {
        points.Add(point);
    }

    public void RemoveLastPoint()
    {
        if (points.Count > 0)
        {
            points.RemoveAt(points.Count - 1);
        }
    }

    public void ClearPoints()
    {
        points.Clear();
        ClearWalls();
    }

    void OnDrawGizmos()
    {
        if (!showGizmos || points == null) return;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < points.Count; i++)
        {
            Gizmos.DrawSphere(points[i], 0.35f);

            if (i < points.Count - 1)
            {
                Gizmos.DrawLine(points[i], points[i + 1]);
            }
        }

        if (closeLoop && points.Count > 2)
        {
            Gizmos.DrawLine(points[points.Count - 1], points[0]);
        }
    }
}
