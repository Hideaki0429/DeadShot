using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PolygonRainArea))]
public sealed class PolygonRainAreaEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(10);
        var rain=(PolygonRainArea)target;
        Color previous=GUI.backgroundColor;
        GUI.backgroundColor=rain.StormEnabled ? new Color(.55f,1f,.65f) : new Color(1f,.65f,.65f);
        if(GUILayout.Button(rain.StormEnabled ? "雷雨を止める" : "雷雨を開始する",GUILayout.Height(34)))
        {
            Undo.RecordObject(rain,"Toggle storm");
            rain.ToggleStorm();
            EditorUtility.SetDirty(rain);
        }
        GUI.backgroundColor=previous;
    }
}
