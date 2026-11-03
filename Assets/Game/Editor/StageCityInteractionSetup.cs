#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StageCityInteractionSetup
{
    const string ZombiePrefabPath = "Assets/Game/Enemy/MobZombie/MZombie.prefab";

    [MenuItem("Tools/Dead Shot/Stage City/Setup Transition Door And Tutorial")]
    static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "Stage_city")
        {
            Debug.LogError("[Stage City Setup] Stage_cityを開いてから実行してください。");
            return;
        }

        GameObject transitionObject = FindSceneObject("ステージ移動判定Box");
        GameObject tutorialObject = FindSceneObject("チュートリアル開始エリア");
        GameObject respawnObject = FindSceneObject("モブゾンビリスポーン地点");
        GameObject entranceObject = FindSceneObject("入口");

        if (transitionObject != null)
        {
            Collider trigger = EnsureCollider(transitionObject);
            trigger.isTrigger = true;
            if (transitionObject.GetComponent<StageTransitionTrigger>() == null)
                Undo.AddComponent<StageTransitionTrigger>(transitionObject);
        }
        else Debug.LogWarning("[Stage City Setup] 「ステージ移動判定Box」が見つかりません。");

        if (tutorialObject != null)
        {
            Collider trigger = EnsureCollider(tutorialObject);
            trigger.isTrigger = true;
            TutorialCombatZone zone = tutorialObject.GetComponent<TutorialCombatZone>();
            if (zone == null) zone = Undo.AddComponent<TutorialCombatZone>(tutorialObject);
            SerializedObject serialized = new SerializedObject(zone);
            serialized.FindProperty("respawnPoint").objectReferenceValue = respawnObject != null ? respawnObject.transform : null;
            serialized.FindProperty("zombiePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath);
            serialized.FindProperty("entranceExitPoint").objectReferenceValue = entranceObject != null ? entranceObject.transform : null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogWarning("[Stage City Setup] 「チュートリアル開始エリア」が見つかりません。");

        if (entranceObject != null && entranceObject.GetComponent<AutomaticSwingDoor>() == null)
            PrepareRightHingeDoor(entranceObject);

        EnsureBuildScene("Assets/Game/S/Loading.unity");
        EnsureBuildScene("Assets/Game/S/Stage1.unity");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Stage City Setup] ステージ遷移・右ヒンジ自動扉・武器訓練エリアを設定しました。");
    }

    static void PrepareRightHingeDoor(GameObject door)
    {
        Renderer[] renderers = door.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[Stage City Setup] 入口にRendererがないため、ヒンジ位置は手動設定が必要です。");
            Undo.AddComponent<AutomaticSwingDoor>(door);
            return;
        }
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Transform oldParent = door.transform.parent;
        var hinge = new GameObject("入口_RightHinge");
        Undo.RegisterCreatedObjectUndo(hinge, "Create right door hinge");
        hinge.transform.SetParent(oldParent, true);
        Vector3 localRightWorld = door.transform.right * Vector3.Dot(bounds.extents, Abs(door.transform.right));
        hinge.transform.position = bounds.center + localRightWorld;
        hinge.transform.rotation = door.transform.rotation;
        // Hospital内のchainlink_singleはネストPrefabのため親変更しない。
        // AutomaticSwingDoorがヒンジ座標を中心にワールド回転させる。
        AutomaticSwingDoor swing = Undo.AddComponent<AutomaticSwingDoor>(hinge);
        SerializedObject serialized = new SerializedObject(swing);
        serialized.FindProperty("doorVisual").objectReferenceValue = door.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        BoxCollider sensor = Undo.AddComponent<BoxCollider>(hinge);
        sensor.isTrigger = true;
        sensor.center = hinge.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = hinge.transform.InverseTransformVector(bounds.size);
        sensor.size = new Vector3(Mathf.Abs(localSize.x) + 1.5f, Mathf.Abs(localSize.y), Mathf.Abs(localSize.z) + 2f);
    }

    static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    static Collider EnsureCollider(GameObject target)
    {
        Collider existing = target.GetComponent<Collider>();
        return existing != null ? existing : Undo.AddComponent<BoxCollider>(target);
    }
    static GameObject FindSceneObject(string exactName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == exactName) return child.gameObject;
        return null;
    }
    static void EnsureBuildScene(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
