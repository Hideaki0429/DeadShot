using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CombatAudioAndImpactSetup
{
    private const string PlayerPrefabPath = "Assets/Game/Prefabs/Survivalist (4).prefab";
    private const string ZombiePrefabPath = "Assets/Game/Enemy/MobZombie/MZombie.prefab";
    private const string BulletPrefabPath = "Assets/Game/Bullet/Pistolbullet.prefab";
    private const string RifleBulletPrefabPath = "Assets/Game/Rifle/Bullet4 (1).prefab";
    private const string ConcreteImpactPath =
        "Assets/JMO Assets/WarFX/_Effects/Bullet Impacts/WFX_BImpact Concrete NoCollision.prefab";
    private const string WoodImpactPath =
        "Assets/JMO Assets/WarFX/_Effects/Bullet Impacts/WFX_BImpact Wood.prefab";
    private const string MetalImpactPath =
        "Assets/JMO Assets/WarFX/_Effects/Bullet Impacts/WFX_BImpact Metal.prefab";
    private const string MuzzleSmokeFolder = "Assets/Game/Effects/Weapon";
    private const string MuzzleSmokePrefabPath = MuzzleSmokeFolder + "/PistolMuzzleSmoke.prefab";
    private const string SmokeMaterialPath =
        "Assets/JMO Assets/WarFX/Desktop/Materials/Impacts/WFX_M_Impact Smoke.mat";
    private const string SetupVersionKey = "DeadShot.CombatAudioAndImpactSetup.Version";
    private const int SetupVersion = 6;

    static CombatAudioAndImpactSetup()
    {
        EditorApplication.delayCall += RebuildIfNeeded;
    }

    [MenuItem("Tools/Dead Shot/Rebuild Combat Audio And Impacts")]
    public static void Rebuild()
    {
        ConfigureFootsteps(PlayerPrefabPath);
        ConfigureFootsteps(ZombiePrefabPath);
        ConfigurePlayerMovementDrivenFootsteps();
        ConfigureBulletImpact();
        ConfigureBulletImpact(RifleBulletPrefabPath);
        GameObject muzzleSmoke = CreateOrUpdateMuzzleSmokePrefab();
        ConfigurePlayerMuzzleSmoke(muzzleSmoke);
        EditorPrefs.SetInt(SetupVersionKey, SetupVersion);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Combat Audio Setup] 銃口煙・命中音・地面着弾・左右足音の登録欄を設定しました。");
    }

    private static void RebuildIfNeeded()
    {
        if (EditorPrefs.GetInt(SetupVersionKey, 0) < SetupVersion) Rebuild();
    }

    private static void ConfigureFootsteps(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) return;

        try
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning($"[Combat Audio Setup] Humanoid Animatorがありません: {prefabPath}");
                return;
            }

            FootstepAudioController controller = root.GetComponent<FootstepAudioController>();
            if (controller == null) controller = root.AddComponent<FootstepAudioController>();

            Transform audioTransform = root.transform.Find("Footstep Audio Source");
            if (audioTransform == null)
            {
                GameObject audioObject = new GameObject("Footstep Audio Source");
                audioTransform = audioObject.transform;
                audioTransform.SetParent(root.transform, false);
            }
            AudioSource source = audioTransform.GetComponent<AudioSource>();
            if (source == null) source = audioTransform.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 20f;

            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty sourceProperty = serializedController.FindProperty("audioSource");
            if (sourceProperty != null) sourceProperty.objectReferenceValue = source;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            ConfigureFoot(
                animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                "Left Footstep Trigger",
                controller);
            ConfigureFoot(
                animator.GetBoneTransform(HumanBodyBones.RightFoot),
                "Right Footstep Trigger",
                controller);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureFoot(
        Transform footBone,
        string sensorName,
        FootstepAudioController controller)
    {
        if (footBone == null) return;
        Transform sensor = footBone.Find(sensorName);
        if (sensor != null) Object.DestroyImmediate(sensor.gameObject);
    }

    private static void ConfigureBulletImpact(string prefabPath = BulletPrefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) return;

        try
        {
            OnCollision_DestroyBoth bullet = root.GetComponent<OnCollision_DestroyBoth>();
            GameObject concreteImpact = AssetDatabase.LoadAssetAtPath<GameObject>(ConcreteImpactPath);
            GameObject woodImpact = AssetDatabase.LoadAssetAtPath<GameObject>(WoodImpactPath);
            GameObject metalImpact = AssetDatabase.LoadAssetAtPath<GameObject>(MetalImpactPath);
            if (bullet != null && concreteImpact != null)
            {
                SerializedObject serializedBullet = new SerializedObject(bullet);
                SerializedProperty impactProperty =
                    serializedBullet.FindProperty("concreteImpactPrefab");
                if (impactProperty != null) impactProperty.objectReferenceValue = concreteImpact;
                SerializedProperty woodProperty = serializedBullet.FindProperty("woodImpactPrefab");
                if (woodProperty != null) woodProperty.objectReferenceValue = woodImpact;
                SerializedProperty metalProperty = serializedBullet.FindProperty("metalImpactPrefab");
                if (metalProperty != null) metalProperty.objectReferenceValue = metalImpact;
                serializedBullet.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigurePlayerMovementDrivenFootsteps()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null) return;

        try
        {
            FootstepAudioController controller = root.GetComponent<FootstepAudioController>();
            if (controller != null)
            {
                SerializedObject serializedController = new SerializedObject(controller);
                SetFloat(serializedController, "walkStepDistance", 0.8f);
                SetFloat(serializedController, "aimStepDistance", 0.55f);
                SetFloat(serializedController, "runStepDistance", 1.2f);
                SetFloat(serializedController, "minimumActualSpeed", 0.1f);
                serializedController.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (FootstepColliderSensor sensor in
                     root.GetComponentsInChildren<FootstepColliderSensor>(true))
            {
                sensor.enabled = false;
                CapsuleCollider capsule = sensor.GetComponent<CapsuleCollider>();
                if (capsule != null) capsule.enabled = false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetBool(
        SerializedObject serializedObject,
        string propertyName,
        bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null) property.boolValue = value;
    }

    private static void SetFloat(
        SerializedObject serializedObject,
        string propertyName,
        float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null) property.floatValue = value;
    }

    private static GameObject CreateOrUpdateMuzzleSmokePrefab()
    {
        EnsureFolder("Assets/Game/Effects", "Weapon");
        GameObject smokeObject = new GameObject("PistolMuzzleSmoke");
        ParticleSystem particles = smokeObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.12f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.72f, 0.72f, 0.18f),
            new Color(0.92f, 0.92f, 0.92f, 0.38f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 12;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)5, (short)10)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 0.005f;
        shape.radiusThickness = 1f;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.045f;
        noise.frequency = 0.7f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.72f, 0.72f, 0.72f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0.45f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = fade;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, 1.55f)));

        ParticleSystemRenderer renderer = smokeObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
        if (material != null) renderer.sharedMaterial = material;

        particles.Play();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(smokeObject, MuzzleSmokePrefabPath);
        Object.DestroyImmediate(smokeObject);
        return prefab;
    }

    private static void ConfigurePlayerMuzzleSmoke(GameObject muzzleSmoke)
    {
        if (muzzleSmoke == null) return;
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null) return;

        try
        {
            OnKeyPress_Throw shooter = root.GetComponentInChildren<OnKeyPress_Throw>(true);
            if (shooter != null)
            {
                SerializedObject serializedShooter = new SerializedObject(shooter);
                SerializedProperty smokeProperty =
                    serializedShooter.FindProperty("muzzleSmokePrefab");
                if (smokeProperty != null) smokeProperty.objectReferenceValue = muzzleSmoke;
                SerializedProperty lifetimeProperty =
                    serializedShooter.FindProperty("muzzleSmokeLifetime");
                if (lifetimeProperty != null) lifetimeProperty.floatValue = 1.2f;
                serializedShooter.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureFolder(string parentFolder, string childFolder)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Game/Effects"))
            AssetDatabase.CreateFolder("Assets/Game", "Effects");
        if (!AssetDatabase.IsValidFolder(MuzzleSmokeFolder))
            AssetDatabase.CreateFolder(parentFolder, childFolder);
    }
}
