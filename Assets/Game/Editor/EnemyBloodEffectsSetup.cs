using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyBloodEffectsSetup
{
    private const string ZombiePrefabPath = "Assets/Game/Enemy/MobZombie/MZombie.prefab";
    private const string HeadVFXPath =
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_Directional_01_Floor_Extra.prefab";
    private const string TorsoVFXPath =
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_01_Floor.prefab";
    private const string LimbVFXPath =
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_Directional_01_Floor_Rot.prefab";
    private const string CorpseVFXPath =
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_01_Floor_Rot_GS_Pure_02.prefab";
    private const string DripMaterialPath =
        "Assets/Vefects/Free Blood VFX/VFX/Materials/M_VFX_Fake_Blood_01_Lit.mat";
    private const string GeneratedMaterialFolder = "Assets/Game/Materials/Blood";
    private const string RibbonMaterialPath =
        GeneratedMaterialFolder + "/BloodGlossyTrail.mat";
    private static readonly string[] HeadVariationPaths =
    {
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_Directional_01_Floor_Extra.prefab",
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_Directional_01_Floor_Extra_02.prefab",
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_Directional_01_Floor_Bunch.prefab",
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_Directional_01_Floor_Rot.prefab"
    };
    private static readonly string[] TorsoVariationPaths =
    {
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_01_Floor.prefab",
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_01_Floor_Rot.prefab",
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_01_Floor_Rot_Extra.prefab",
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_02_Floor_Rot.prefab"
    };
    private static readonly string[] LimbVariationPaths =
    {
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_Directional_01_Floor_Rot.prefab",
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_Directional_01_Floor.prefab",
        "Assets/Vefects/Free Blood VFX/VFX/Particles/VFX_Splat_Directional_01_Floor_Extra_02.prefab"
    };
    private static readonly string[] SourceDecalMaterialPaths =
    {
        "Assets/Blood decal pack/blood/Materials/blood.mat",
        "Assets/Blood decal pack/blood/Materials/blood1.mat",
        "Assets/Blood decal pack/blood/Materials/blood2.mat",
        "Assets/Blood decal pack/blood/Materials/blood3.mat",
        "Assets/Blood decal pack/blood/Materials/blood4.mat",
        "Assets/Blood decal pack/blood/Materials/blood5.mat",
        "Assets/Blood decal pack/blood/Materials/blood6.mat",
        "Assets/Blood decal pack/blood/Materials/blood7.mat"
    };
    private const string SetupVersionKey = "DeadShot.EnemyBloodEffectsSetup.Version";
    private const int SetupVersion = 7;

    static EnemyBloodEffectsSetup()
    {
        EditorApplication.delayCall += RebuildIfNeeded;
    }

    [MenuItem("Tools/Dead Shot/Rebuild Enemy Blood Effects")]
    public static void Rebuild()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ZombiePrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError("[Enemy Blood Setup] MZombie.prefabが見つかりません。");
            return;
        }

        try
        {
            EnemyBloodEffects bloodEffects =
                prefabRoot.GetComponent<EnemyBloodEffects>();
            if (bloodEffects == null)
            {
                bloodEffects = prefabRoot.AddComponent<EnemyBloodEffects>();
            }

            SerializedObject serializedEffects = new SerializedObject(bloodEffects);
            EnsureGeneratedMaterialFolder();
            Material ribbonMaterial = CreateOrUpdateRibbonMaterial();
            Material[] decalMaterials = CreateOrUpdateGlossyDecalMaterials();

            AssignPrefab(serializedEffects, "headHitVFX", HeadVFXPath);
            AssignPrefab(serializedEffects, "torsoHitVFX", TorsoVFXPath);
            AssignPrefab(serializedEffects, "limbHitVFX", LimbVFXPath);
            AssignPrefab(serializedEffects, "corpseBloodVFX", CorpseVFXPath);
            AssignMaterial(serializedEffects, "dripMaterial", DripMaterialPath);
            AssignObject(serializedEffects, "ribbonMaterial", ribbonMaterial);
            AssignPrefabArray(serializedEffects, "headHitVFXVariations", HeadVariationPaths);
            AssignPrefabArray(serializedEffects, "torsoHitVFXVariations", TorsoVariationPaths);
            AssignPrefabArray(serializedEffects, "limbHitVFXVariations", LimbVariationPaths);
            AssignMaterialArray(serializedEffects, "bloodDecalMaterials", decalMaterials);
            SetBool(serializedEffects, "enableImpactBurstVFX", false);
            SetBool(serializedEffects, "enableBloodDripping", false);
            SetVector2Int(serializedEffects, "headRibbonCount", new Vector2Int(10, 14));
            SetVector2Int(serializedEffects, "torsoRibbonCount", new Vector2Int(7, 10));
            SetVector2Int(serializedEffects, "limbRibbonCount", new Vector2Int(4, 6));
            SetVector2(serializedEffects, "ribbonSpeedRange", new Vector2(2.5f, 5f));
            SetVector2(serializedEffects, "ribbonWidthRange", new Vector2(0.012f, 0.032f));
            SetVector2(serializedEffects, "ribbonLifetimeRange", new Vector2(0.25f, 0.5f));
            SetFloat(serializedEffects, "ribbonSpreadAngle", 38f);
            SetFloat(serializedEffects, "ribbonGravity", 0.5f);
            SetVector2(serializedEffects, "ribbonWiggleStrengthRange", new Vector2(0.3f, 0.8f));
            SetVector2(serializedEffects, "ribbonWiggleFrequencyRange", new Vector2(8f, 15f));
            SetFloat(serializedEffects, "ribbonVelocityDamping", 1.6f);
            SetBool(serializedEffects, "enableHeadshotMist", true);
            SetVector2Int(serializedEffects, "headshotMistParticleCount", new Vector2Int(160, 220));
            SetVector2Int(serializedEffects, "headshotMistSecondaryCount", new Vector2Int(40, 60));
            SetVector2(serializedEffects, "headshotMistSpeedRange", new Vector2(0.6f, 2f));
            SetVector2(serializedEffects, "headshotMistSizeRange", new Vector2(0.003f, 0.018f));
            SetVector2(serializedEffects, "headshotMistLifetimeRange", new Vector2(0.15f, 0.45f));
            SetFloat(serializedEffects, "headshotMistTurbulence", 0.35f);
            SetFloat(serializedEffects, "headshotMistRadius", 0.015f);
            SetBool(serializedEffects, "enableCharacterBloodStains", true);
            SetVector2Int(serializedEffects, "characterStainsPerHit", new Vector2Int(1, 2));
            SetVector2(serializedEffects, "characterStainScaleRange", new Vector2(0.05f, 0.16f));
            SetFloat(serializedEffects, "characterStainSurfaceOffset", 0.004f);
            SetInt(serializedEffects, "maximumCharacterBloodStains", 12);
            serializedEffects.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, ZombiePrefabPath);
            EditorPrefs.SetInt(SetupVersionKey, SetupVersion);
            Debug.Log("[Enemy Blood Setup] MZombieに部位別・滴下・死亡時VFXを設定しました。");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void RebuildIfNeeded()
    {
        if (EditorPrefs.GetInt(SetupVersionKey, 0) >= SetupVersion)
            return;

        Rebuild();
    }

    private static void AssignPrefab(
        SerializedObject serializedObject,
        string propertyName,
        string assetPath)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        GameObject effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

        if (property == null || effectPrefab == null)
        {
            Debug.LogError($"[Enemy Blood Setup] {propertyName}のVFXを設定できません。");
            return;
        }

        property.objectReferenceValue = effectPrefab;
    }

    private static void AssignMaterial(
        SerializedObject serializedObject,
        string propertyName,
        string assetPath)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

        if (property == null || material == null)
        {
            Debug.LogError($"[Enemy Blood Setup] {propertyName}のMaterialを設定できません。");
            return;
        }

        property.objectReferenceValue = material;
    }

    private static void AssignObject(
        SerializedObject serializedObject,
        string propertyName,
        Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || value == null)
        {
            Debug.LogError($"[Enemy Blood Setup] {propertyName}を設定できません。");
            return;
        }
        property.objectReferenceValue = value;
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

    private static void SetInt(
        SerializedObject serializedObject,
        string propertyName,
        int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null) property.intValue = value;
    }

    private static void SetVector2(
        SerializedObject serializedObject,
        string propertyName,
        Vector2 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null) property.vector2Value = value;
    }

    private static void SetVector2Int(
        SerializedObject serializedObject,
        string propertyName,
        Vector2Int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null) property.vector2IntValue = value;
    }

    private static void AssignPrefabArray(
        SerializedObject serializedObject,
        string propertyName,
        string[] assetPaths)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null) return;

        property.arraySize = assetPaths.Length;
        for (int index = 0; index < assetPaths.Length; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(assetPaths[index]);
        }
    }

    private static void AssignMaterialArray(
        SerializedObject serializedObject,
        string propertyName,
        Material[] materials)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null) return;

        property.arraySize = materials.Length;
        for (int index = 0; index < materials.Length; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue = materials[index];
        }
    }

    private static void EnsureGeneratedMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Game/Materials"))
            AssetDatabase.CreateFolder("Assets/Game", "Materials");
        if (!AssetDatabase.IsValidFolder(GeneratedMaterialFolder))
            AssetDatabase.CreateFolder("Assets/Game/Materials", "Blood");
    }

    private static Material CreateOrUpdateRibbonMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RibbonMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, RibbonMaterialPath);
        }

        ConfigureTransparentGlossyMaterial(material, null);
        material.name = "BloodGlossyTrail";
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material[] CreateOrUpdateGlossyDecalMaterials()
    {
        Material[] results = new Material[SourceDecalMaterialPaths.Length];
        for (int index = 0; index < SourceDecalMaterialPaths.Length; index++)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(
                SourceDecalMaterialPaths[index]);
            string targetPath = $"{GeneratedMaterialFolder}/GlossyBloodDecal{index}.mat";
            Material target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);

            if (target == null)
            {
                target = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(target, targetPath);
            }

            Texture texture = source != null && source.HasProperty("_MainTex")
                ? source.GetTexture("_MainTex")
                : null;
            ConfigureTransparentGlossyMaterial(target, texture);
            target.name = $"GlossyBloodDecal{index}";
            EditorUtility.SetDirty(target);
            results[index] = target;
        }
        return results;
    }

    private static void ConfigureTransparentGlossyMaterial(
        Material material,
        Texture texture)
    {
        material.shader = Shader.Find("Standard");
        material.SetColor("_Color", new Color(0.72f, 0.015f, 0.008f, 0.92f));
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0.78f);
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (texture != null)
            material.SetTexture("_MainTex", texture);
    }
}
