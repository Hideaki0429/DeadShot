using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WorldRainBuilder
{
    private const string RainyPrefabPath = "Assets/Rainy VFX/Prefab/Rainy.prefab";

    [MenuItem("Tools/Dead Shot/Build Polygon World Rain")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[Polygon Rain] Play Modeを終了してから実行してください。");
            return;
        }

        GameObject rainyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RainyPrefabPath);
        PolygonWallBuilder polygonWall = Object.FindObjectOfType<PolygonWallBuilder>();
        if (rainyPrefab == null || polygonWall == null)
        {
            Debug.LogError("[Polygon Rain] Rainy PrefabまたはPolygonWallBuilderが見つかりません。");
            return;
        }

        GameObject oldRain = GameObject.Find("Weather/Rain System");
        if (oldRain != null)
            Object.DestroyImmediate(oldRain);

        GameObject existingWorldRain = GameObject.Find("Weather/World Rain (Rainy)");
        if (existingWorldRain != null)
            Object.DestroyImmediate(existingWorldRain);

        GameObject weather = GameObject.Find("Weather");
        if (weather == null)
            weather = new GameObject("Weather");

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(rainyPrefab);
        instance.name = "World Rain (Rainy)";
        instance.transform.SetParent(weather.transform, false);
        instance.transform.localPosition = Vector3.zero;

        ParticleSystem particles = instance.GetComponentInChildren<ParticleSystem>(true);
        PolygonRainArea rainArea = instance.GetComponent<PolygonRainArea>();
        if (rainArea == null)
            rainArea = instance.AddComponent<PolygonRainArea>();

        SerializedObject rainAreaObject = new SerializedObject(rainArea);
        rainAreaObject.FindProperty("polygonWall").objectReferenceValue = polygonWall;
        rainAreaObject.FindProperty("rainParticles").objectReferenceValue = particles;
        rainAreaObject.FindProperty("rainHeight").floatValue = 30f;
        rainAreaObject.FindProperty("particlesPerSquareMeter").floatValue = 0.04f;
        rainAreaObject.FindProperty("maximumEmissionPerSecond").intValue = 6000;
        rainAreaObject.FindProperty("maximumParticles").intValue = 30000;
        rainAreaObject.FindProperty("playOnStart").boolValue = true;
        rainAreaObject.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(instance.scene);
        EditorSceneManager.SaveScene(instance.scene);
        Selection.activeGameObject = instance;
        AssetDatabase.SaveAssets();
        Debug.Log("[Polygon Rain] RainyをPolygonWallBuilder全域へ配置しました。");
    }
}
