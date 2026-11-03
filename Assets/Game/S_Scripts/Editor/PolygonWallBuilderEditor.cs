// 【役割】壁の作成・編集に使うInspectorとEditor操作を提供する。
// 【実行場所】Unity Editor専用。設定の作成・適用処理を担当します。
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PolygonWallBuilder))]
public class PolygonWallBuilderEditor : Editor
{
    private PolygonWallBuilder builder;
    // コンポーネントが有効になったときの処理です。
    void OnEnable()
    {
        builder = (PolygonWallBuilder)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(10);
        if (GUILayout.Button("壁Colliderを生成"))
        {
            Undo.RegisterFullObjectHierarchyUndo(builder.gameObject, "Generate Polygon Walls");
            builder.GenerateWalls();
            EditorUtility.SetDirty(builder);
        }

        if (GUILayout.Button("最後の点を削除"))
        {
            Undo.RecordObject(builder, "Remove Last Point");
            builder.RemoveLastPoint();
            EditorUtility.SetDirty(builder);
        }

        if (GUILayout.Button("点と壁をすべて削除"))
        {
            Undo.RegisterFullObjectHierarchyUndo(builder.gameObject, "Clear Points And Walls");
            builder.ClearPoints();
            EditorUtility.SetDirty(builder);
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Sceneビュー上で Shift + 左クリックすると頂点を追加できます。\n" + "追加した点をもとに、壁Colliderを生成できます。", MessageType.Info);
    }

    void OnSceneGUI()
    {
        Event e = Event.current;
        if (e.shift && e.type == EventType.MouseDown && e.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
            {
                Undo.RecordObject(builder, "Add Polygon Wall Point");
                builder.AddPoint(hit.point);
                EditorUtility.SetDirty(builder);
                e.Use();
            }
        }

        DrawHandles();
    }

    void DrawHandles()
    {
        if (builder.points == null)
        {
            return;
        }

        Handles.color = Color.yellow;
        for (int i = 0; i < builder.points.Count; i++)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(builder.points[i], Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(builder, "Move Polygon Point");
                builder.points[i] = newPos;
                EditorUtility.SetDirty(builder);
            }

            Handles.Label(builder.points[i] + Vector3.up * 0.5f, "Point " + i);
        }
    }
}
