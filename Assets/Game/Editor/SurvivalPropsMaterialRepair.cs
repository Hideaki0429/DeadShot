using UnityEditor;
using UnityEngine;

/// <summary>
/// Repairs the Survival Props materials that were authored for HDRP so they
/// render correctly in this project's Built-in Render Pipeline.
/// </summary>
[InitializeOnLoad]
public static class SurvivalPropsMaterialRepair
{
    private const string MaterialFolder = "Assets/Survival Props/materials";
    private const string SessionKey = "DeadShot.SurvivalPropsMaterialRepair";

    static SurvivalPropsMaterialRepair()
    {
        EditorApplication.delayCall += RepairOnceAfterReload;
    }

    private static void RepairOnceAfterReload()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        RepairMaterials(false);
    }

    [MenuItem("Tools/Dead Shot/Repair Survival Props Materials")]
    public static void RepairFromMenu()
    {
        int repaired = RepairMaterials(true);
        EditorUtility.DisplayDialog(
            "Survival Props",
            repaired > 0
                ? $"{repaired}個のマテリアルをStandardシェーダーへ修復しました。"
                : "修復が必要なマテリアルはありませんでした。",
            "OK");
    }

    private static int RepairMaterials(bool force)
    {
        Shader standard = Shader.Find("Standard");
        if (standard == null)
        {
            Debug.LogError("[Survival Props] Standard shader was not found.");
            return 0;
        }

        int repaired = 0;
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                continue;

            bool needsRepair = material.shader == null ||
                               material.shader.name == "Hidden/InternalErrorShader" ||
                               material.shader.name.Contains("HDRP") ||
                               material.shader.name.Contains("High Definition");
            if (!force && !needsRepair)
                continue;
            if (material.shader == standard && !needsRepair)
                continue;

            SerializedObject serialized = new SerializedObject(material);
            Texture baseMap = FindTexture(serialized, "_BaseColorMap") ?? FindTexture(serialized, "_MainTex");
            Texture normalMap = FindTexture(serialized, "_NormalMap") ?? FindTexture(serialized, "_BumpMap");
            Texture maskMap = FindTexture(serialized, "_MaskMap") ?? FindTexture(serialized, "_MetallicGlossMap");
            Color baseColor = FindColor(serialized, "_BaseColor", Color.white);
            float metallic = FindFloat(serialized, "_Metallic", 0f);
            float smoothness = FindFloat(serialized, "_Smoothness", 0.5f);

            Undo.RecordObject(material, "Repair Survival Props Material");
            material.shader = standard;
            material.SetTexture("_MainTex", baseMap);
            material.SetColor("_Color", baseColor);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);

            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
            }

            if (maskMap != null)
            {
                material.SetTexture("_MetallicGlossMap", maskMap);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }

            material.SetFloat("_Mode", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = -1;

            EditorUtility.SetDirty(material);
            repaired++;
        }

        if (repaired > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Survival Props] Repaired {repaired} material(s) for the Built-in Render Pipeline.");
        }

        return repaired;
    }

    private static Texture FindTexture(SerializedObject serialized, string propertyName)
    {
        SerializedProperty entries = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
        if (entries == null)
            return null;

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == propertyName)
                return entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
        }
        return null;
    }

    private static Color FindColor(SerializedObject serialized, string propertyName, Color fallback)
    {
        SerializedProperty entries = serialized.FindProperty("m_SavedProperties.m_Colors");
        if (entries == null)
            return fallback;

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == propertyName)
                return entry.FindPropertyRelative("second").colorValue;
        }
        return fallback;
    }

    private static float FindFloat(SerializedObject serialized, string propertyName, float fallback)
    {
        SerializedProperty entries = serialized.FindProperty("m_SavedProperties.m_Floats");
        if (entries == null)
            return fallback;

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == propertyName)
                return entry.FindPropertyRelative("second").floatValue;
        }
        return fallback;
    }
}
