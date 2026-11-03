using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RainSystemBuilder
{
    private const string PrefabDirectory = "Assets/Game/Prefabs/Weather";
    private const string MaterialDirectory = "Assets/Game/Materials/Weather";
    private const string PrefabPath = PrefabDirectory + "/Rain System.prefab";
    private const string RainMaterialPath = MaterialDirectory + "/Rain Streak Material.mat";
    private const string SplashMaterialPath = MaterialDirectory + "/Rain Splash Material.mat";

    [MenuItem("Tools/Dead Shot/Build Rain System")]
    public static void Build()
    {
        EnsureDirectory(PrefabDirectory);
        EnsureDirectory(MaterialDirectory);

        Material rainMaterial = CreateOrUpdateMaterial(
            RainMaterialPath,
            new Color(0.88f, 0.90f, 0.92f, 0.20f));
        Material splashMaterial = CreateOrUpdateMaterial(
            SplashMaterialPath,
            new Color(0.86f, 0.89f, 0.92f, 0.18f));

        GameObject rainSystem = new GameObject("Rain System");
        PlayerFollowingRain follower = rainSystem.AddComponent<PlayerFollowingRain>();

        ParticleSystem rain = CreateRainEmitter(rainSystem.transform, rainMaterial);
        ParticleSystem splash = CreateSplashEmitter(rainSystem.transform, splashMaterial);

        SerializedObject followerObject = new SerializedObject(follower);
        followerObject.FindProperty("rainParticles").objectReferenceValue = rain;
        followerObject.FindProperty("splashParticles").objectReferenceValue = splash;
        followerObject.FindProperty("rainHeight").floatValue = 15f;
        followerObject.FindProperty("splashHeight").floatValue = 0.05f;
        followerObject.FindProperty("playOnStart").boolValue = true;
        followerObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rainSystem, PrefabPath);
        Object.DestroyImmediate(rainSystem);

        GameObject existing = GameObject.Find("Weather/Rain System");
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject weather = GameObject.Find("Weather");
        if (weather == null)
            weather = new GameObject("Weather");

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(weather.transform, false);

        PlayerFollowingRain sceneFollower = instance.GetComponent<PlayerFollowingRain>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            SerializedObject sceneFollowerObject = new SerializedObject(sceneFollower);
            sceneFollowerObject.FindProperty("player").objectReferenceValue = player.transform;
            sceneFollowerObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(instance.scene);
        EditorSceneManager.SaveScene(instance.scene);
        Selection.activeGameObject = instance;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Rain System] Stage_cityへ雨粒と水しぶきを配置しました。");
    }

    private static ParticleSystem CreateRainEmitter(Transform parent, Material material)
    {
        GameObject emitter = new GameObject("Rain Emitter");
        emitter.transform.SetParent(parent, false);
        emitter.transform.localPosition = Vector3.up * 15f;
        ParticleSystem particles = emitter.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 0f;
        main.startSize = 0.035f;
        main.startColor = new Color(0.92f, 0.94f, 0.96f, 0.35f);
        main.maxParticles = 3000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 1500f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(25f, 1f, 25f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = new ParticleSystem.MinMaxCurve(-25f);

        ParticleSystemRenderer renderer = emitter.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 8f;
        renderer.velocityScale = 0.1f;
        renderer.material = material;
        return particles;
    }

    private static ParticleSystem CreateSplashEmitter(Transform parent, Material material)
    {
        GameObject emitter = new GameObject("Ground Splash Emitter");
        emitter.transform.SetParent(parent, false);
        emitter.transform.localPosition = Vector3.up * 0.05f;
        ParticleSystem particles = emitter.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 0.25f;
        main.startSpeed = 0f;
        main.startSize = 0.08f;
        main.startColor = new Color(0.90f, 0.92f, 0.94f, 0.28f);
        main.maxParticles = 500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 100f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(20f, 0.1f, 20f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = new ParticleSystem.MinMaxCurve(0.7f);

        ParticleSystemRenderer renderer = emitter.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = material;
        return particles;
    }

    private static Material CreateOrUpdateMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_SurfaceType", 1f);
        material.SetFloat("_BlendMode", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureDirectory(string directory)
    {
        if (AssetDatabase.IsValidFolder(directory)) return;

        string parent = Path.GetDirectoryName(directory).Replace('\\', '/');
        string folder = Path.GetFileName(directory);
        EnsureDirectory(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
