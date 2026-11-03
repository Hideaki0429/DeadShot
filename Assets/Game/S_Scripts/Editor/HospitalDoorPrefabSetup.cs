// 【役割】Editor上で病院のドアPrefabに自動開閉とヒンジを設定する。
// 【実行場所】Unity Editor専用。設定の作成・適用処理を担当します。
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HospitalDoorPrefabSetup
{
    const int Version = 3;
    const string VersionKey = "DeadShot.HospitalDoorPrefabSetupVersion";
    const string Folder = "Assets/Abandoned_Asylum/Prefabs/";
    static HospitalDoorPrefabSetup() => EditorApplication.delayCall += SetupIfNeeded;
    [MenuItem("Dead Shot/Setup Hospital Door Prefabs")]
    public static void SetupAll()
    {
        Setup("Door_V1", false);
        Setup("Door_V2", false);
        Setup("Door_V3", false);
        Setup("DoorD_V1", true);
        Setup("DoorD_V2", true);
        SetupPlacedDoorsInHospital();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log("[HospitalDoor] Door_V1~V3 / DoorD_V1~V2へ自動開閉設定を保存しました。");
    }

    static void SetupIfNeeded()
    {
        if (EditorPrefs.GetInt(VersionKey, 0) < Version)
        {
            SetupAll();
        }
    }

    static void Setup(string prefabName, bool doubleDoor)
    {
        string path = Folder + prefabName + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            return;
        }

        try
        {
            ConfigureDoor(root, doubleDoor);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void SetupPlacedDoorsInHospital()
    {
        const string path = Folder + "Asylum.prefab";
        GameObject hospital = PrefabUtility.LoadPrefabContents(path);
        if (hospital == null)
        {
            return;
        }

        int count = 0;
        try
        {
            // Asylum内のドアは元Prefabとの接続がなく、名称末尾に (1) 等が付きます。
            Regex doorName = new Regex(@"^(Door_V[123]|DoorD_V[12])( \(\d+\))?$");
            foreach (Transform child in hospital.GetComponentsInChildren<Transform>(true))
            {
                if (!doorName.IsMatch(child.name))
                {
                    continue;
                }

                bool doubleDoor = child.name.StartsWith("DoorD_", StringComparison.OrdinalIgnoreCase);
                ConfigureDoor(child.gameObject, doubleDoor);
                count++;
            }

            PrefabUtility.SaveAsPrefabAsset(hospital, path);
            Debug.Log("[HospitalDoor] Stage1の病院Prefab内ドア " + count + "か所を変換しました。");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(hospital);
        }
    }

    static void ConfigureDoor(GameObject root, bool doubleDoor)
    {
        ProximityHospitalDoor controller = root.GetComponent<ProximityHospitalDoor>();
        if (controller == null)
        {
            controller = root.AddComponent<ProximityHospitalDoor>();
        }

        List<Transform> parts = new List<Transform>();
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root.transform)
            {
                continue;
            }

            string name = child.name;
            if (name.IndexOf("Frame", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            bool isLeaf = doubleDoor ? name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Window", StringComparison.OrdinalIgnoreCase) >= 0 : string.Equals(name, "Door", StringComparison.OrdinalIgnoreCase);
            if (isLeaf)
            {
                parts.Add(child);
            }
        }

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("useOuterEdgeHinges").boolValue = root.name.StartsWith("DoorD_V2", StringComparison.OrdinalIgnoreCase);
        SerializedProperty array = serialized.FindProperty("doorParts");
        array.arraySize = parts.Count;
        for (int i = 0; i < parts.Count; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        BoxCollider range = root.GetComponent<BoxCollider>();
        range.isTrigger = true;
        ConfigureRange(root, range);
        foreach (Transform part in parts)
        {
            ClearStatic(part);
        }
    }

    static void ConfigureRange(GameObject root, BoxCollider range)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        range.center = root.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = root.transform.InverseTransformVector(bounds.size);
        range.size = new Vector3(Mathf.Abs(localSize.x) + 2f, Mathf.Max(2.4f, Mathf.Abs(localSize.y)), Mathf.Abs(localSize.z) + 3f);
    }

    static void ClearStatic(Transform target)
    {
        target.gameObject.isStatic = false;
        foreach (Transform child in target)
        {
            ClearStatic(child);
        }
    }
}
#endif

