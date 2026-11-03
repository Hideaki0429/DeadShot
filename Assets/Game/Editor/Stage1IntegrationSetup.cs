#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Stage1IntegrationSetup
{
    private const string SourceScenePath = "Assets/Game/S/Stage_city.unity";
    private const string TargetScenePath = "Assets/Game/S/Stage1.unity";
    private const string BackupScenePath = "Assets/Game/S/Stage1.before-gameplay-integration.unity";

    private static readonly HashSet<string> KeepSourceRoots = new HashSet<string>
    {
        "Canvas",
        "Damage Video System",
        "EventSystem (1)",
        "GameInputManager",
        "GM",
        "Inventory System",
        "Item Inspection System",
        "Item Pickup Notification",
        "Pause System",
        "SoundManager",
        "Sounds",
        "Survivalist",
        "Weapon Selection System"
    };

    [MenuItem("Tools/Dead Shot/Stage1/Integrate Gameplay From Stage City")]
    public static void Install()
    {
        if (!File.Exists(SourceScenePath) || !File.Exists(TargetScenePath))
        {
            Debug.LogError("[Stage1Setup] Stage_cityまたはStage1が見つかりません。");
            return;
        }

        if (!File.Exists(BackupScenePath))
        {
            AssetDatabase.CopyAsset(TargetScenePath, BackupScenePath);
            AssetDatabase.SaveAssets();
        }

        Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        string stageContentPath = File.Exists(BackupScenePath) ? BackupScenePath : TargetScenePath;
        Scene target = EditorSceneManager.OpenScene(stageContentPath, OpenSceneMode.Additive);

        // Stage1側で作成された病院用ルート（初期リスポーン地点やProbes）を保存。
        HashSet<GameObject> stage1Roots = new HashSet<GameObject>(target.GetRootGameObjects());
        foreach (GameObject root in stage1Roots)
            SceneManager.MoveGameObjectToScene(root, source);
        EditorSceneManager.CloseScene(target, true);

        foreach (GameObject root in source.GetRootGameObjects().ToArray())
        {
            // 元々Stage1にあったルートとゲームプレイ基盤のみ残す。
            bool belongsToStage1 = stage1Roots.Contains(root);
            if (!belongsToStage1 && !KeepSourceRoots.Contains(root.name))
                UnityEngine.Object.DestroyImmediate(root);
        }

        ConfigureStage1(source);
        EditorSceneManager.MarkSceneDirty(source);
        EditorSceneManager.SaveScene(source, TargetScenePath, true);
        EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        Debug.Log("[Stage1Setup] Stage1へゲームプレイ基盤を連携しました。");
    }

    [MenuItem("Tools/Dead Shot/Stage1/Validate Integration")]
    public static void ValidateIntegration()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        GameObject player = FindSceneObject(scene, "Survivalist");
        GameObject spawn = FindSceneObject(scene, "初期リスポーン地点");
        ScoreAndTimeManager score = FindInScene<ScoreAndTimeManager>(scene).FirstOrDefault();
        int missingScripts = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
        int activeMiniMaps = FindInScene<Transform>(scene)
            .Count(transform => (transform.name == "MiniMap" || transform.name == "MiniMapCamera") &&
                                transform.gameObject.activeInHierarchy);
        float spawnDistance = player != null && spawn != null
            ? Vector3.Distance(player.transform.position, spawn.transform.position)
            : float.PositiveInfinity;

        bool valid = player != null && player.activeSelf && spawn != null && spawnDistance < 0.01f &&
                     FindInScene<GameInputManager>(scene).Count() == 1 &&
                     FindInScene<PauseMenuController>(scene).Count() == 1 &&
                     FindInScene<PlayerInventory>(scene).Count() == 1 &&
                     score != null && !score.UseTimeLimit &&
                     FindInScene<EnemyHealth>(scene).Count() == 0 &&
                     activeMiniMaps == 0 && missingScripts == 0;

        string report = $"[Stage1Setup.Validation] valid={valid} " +
                        $"player={player != null} spawn={spawn != null} distance={spawnDistance:F3} " +
                        $"input={FindInScene<GameInputManager>(scene).Count()} " +
                        $"pause={FindInScene<PauseMenuController>(scene).Count()} " +
                        $"inventory={FindInScene<PlayerInventory>(scene).Count()} " +
                        $"score={score != null} timeLimit={(score != null && score.UseTimeLimit)} " +
                        $"enemies={FindInScene<EnemyHealth>(scene).Count()} activeMiniMaps={activeMiniMaps} " +
                        $"missingScripts={missingScripts}";
        if (valid) Debug.Log(report);
        else Debug.LogError(report);
        foreach (Camera camera in FindInScene<Camera>(scene))
            Debug.Log($"[Stage1Setup.ValidationCamera] root={camera.transform.root.name} " +
                      $"name={camera.name} active={camera.gameObject.activeInHierarchy} " +
                      $"enabled={camera.enabled} tag={camera.tag}");
    }

    private static void ConfigureStage1(Scene scene)
    {
        GameObject spawn = FindSceneObject(scene, "初期リスポーン地点");
        GameObject player = FindSceneObject(scene, "Survivalist");
        if (spawn != null && player != null)
        {
            player.SetActive(true);
            player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
            UnityChan.UnityChanControlScriptWithRgidBody movement =
                player.GetComponent<UnityChan.UnityChanControlScriptWithRgidBody>();
            if (movement != null) movement.canControl = true;
        }

        GameObject managerRoot = FindSceneObject(scene, "GM");
        if (managerRoot != null)
        {
            StageGameplayInitializer initializer = managerRoot.GetComponent<StageGameplayInitializer>();
            if (initializer == null) initializer = managerRoot.AddComponent<StageGameplayInitializer>();
            SerializedObject serializedInitializer = new SerializedObject(initializer);
            SerializedProperty movementProperty = serializedInitializer.FindProperty("playerMovement");
            if (movementProperty != null && player != null)
                movementProperty.objectReferenceValue =
                    player.GetComponent<UnityChan.UnityChanControlScriptWithRgidBody>();
            serializedInitializer.ApplyModifiedPropertiesWithoutUndo();
        }

        // Stage1では敵をまだ生成しない。
        foreach (SpawnManager spawner in FindInScene<SpawnManager>(scene))
            spawner.SetSpawnOnStart(false);

        // 街ステージ固有の導入・会話・着陸選択は停止。
        RemoveStageCityIntroduction(scene);

        // ミニマップはStage1では使わない。
        DisableAll<CameraMiniMapUIManager>(scene);
        foreach (Transform transform in FindInScene<Transform>(scene))
            if (transform != null && (transform.name == "MiniMap" || transform.name == "MiniMapCamera"))
                transform.gameObject.SetActive(false);

        ScoreAndTimeManager score = FindInScene<ScoreAndTimeManager>(scene).FirstOrDefault();
        if (score != null)
        {
            SerializedObject serializedScore = new SerializedObject(score);
            SerializedProperty useTimeLimit = serializedScore.FindProperty("useTimeLimit");
            SerializedProperty isMainGameStage = serializedScore.FindProperty("isMainGameStage");
            if (useTimeLimit != null) useTimeLimit.boolValue = false;
            if (isMainGameStage != null) isMainGameStage.boolValue = true;
            serializedScore.ApplyModifiedPropertiesWithoutUndo();
        }

        // 導入カメラを使わず、開始時からプレイヤーカメラにする。
        CameraManager cameraManager = FindInScene<CameraManager>(scene).FirstOrDefault();
        if (cameraManager != null)
        {
            SerializedObject serializedCamera = new SerializedObject(cameraManager);
            SerializedProperty initialMode = serializedCamera.FindProperty("initialMode");
            if (initialMode != null) initialMode.enumValueIndex = (int)CameraManager.CameraMode.Player;
            serializedCamera.ApplyModifiedPropertiesWithoutUndo();
        }

        Camera playerCamera = FindInScene<Camera>(scene)
            .FirstOrDefault(camera => camera.name == "MainCamera(Player)");
        Camera zoomCamera = FindInScene<Camera>(scene)
            .FirstOrDefault(camera => camera.name == "Zoom Camera（ｘ）");
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            playerCamera.enabled = true;
        }
        if (zoomCamera != null) zoomCamera.enabled = false;
    }

    private static void RemoveStageCityIntroduction(Scene scene)
    {
        foreach (OpeningNewsSequence opening in FindInScene<OpeningNewsSequence>(scene).ToArray())
        {
            HideReference(opening, "blackScreenPanel");
            HideReference(opening, "introBlackScreenPanel");
            HideReference(opening, "loadingUiCanvasGroup");
            HideReference(opening, "skipPrompt");
            HideReference(opening, "cockpitFrame");
            HideReference(opening, "newsText");
            UnityEngine.Object.DestroyImmediate(opening);
        }

        foreach (LandingPointSelectionUI landing in FindInScene<LandingPointSelectionUI>(scene).ToArray())
        {
            HideReference(landing, "selectionPanel");
            HideReference(landing, "mapUiCanvasGroup");
            HideReference(landing, "pointSelectionCanvasGroup");
            HideReference(landing, "descriptionPanelCanvasGroup");
            HideReference(landing, "departureWaitingCanvasGroup");
            HideReference(landing, "cockpitUiRoot");
            UnityEngine.Object.DestroyImmediate(landing);
        }

        foreach (CaptainHoverDialogue dialogue in FindInScene<CaptainHoverDialogue>(scene).ToArray())
            UnityEngine.Object.DestroyImmediate(dialogue);
        foreach (PlayerDismountDirector director in FindInScene<PlayerDismountDirector>(scene).ToArray())
            UnityEngine.Object.DestroyImmediate(director);
        foreach (TutorialUIManager tutorial in FindInScene<TutorialUIManager>(scene).ToArray())
            UnityEngine.Object.DestroyImmediate(tutorial);
        foreach (TutorialUIManager1 tutorial in FindInScene<TutorialUIManager1>(scene).ToArray())
            UnityEngine.Object.DestroyImmediate(tutorial);
        foreach (TutorialUIManager2 tutorial in FindInScene<TutorialUIManager2>(scene).ToArray())
            UnityEngine.Object.DestroyImmediate(tutorial);
    }

    private static void HideReference(UnityEngine.Object owner, string propertyName)
    {
        SerializedObject serialized = new SerializedObject(owner);
        SerializedProperty property = serialized.FindProperty(propertyName);
        UnityEngine.Object value = property != null ? property.objectReferenceValue : null;
        if (value is Component component) component.gameObject.SetActive(false);
        else if (value is GameObject gameObject) gameObject.SetActive(false);
    }

    private static void DisableAll<T>(Scene scene) where T : Behaviour
    {
        foreach (T component in FindInScene<T>(scene)) component.enabled = false;
    }

    private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(transform => transform.name == objectName)
            .Select(transform => transform.gameObject)
            .FirstOrDefault();
    }

    [MenuItem("Tools/Dead Shot/Stage1/Dump Source Roots")]
    public static void DumpSourceRoots()
    {
        Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects().OrderBy(x => x.name))
        {
            string components = string.Join(",", root.GetComponents<Component>()
                .Where(x => x != null).Select(x => x.GetType().Name));
            Debug.Log($"[Stage1Setup.Root] {root.name} active={root.activeSelf} components={components}");
        }
    }

    [MenuItem("Tools/Dead Shot/Stage1/Dump Source Cameras")]
    public static void DumpSourceCameras()
    {
        Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        foreach (Camera camera in FindInScene<Camera>(scene))
        {
            Transform root = camera.transform.root;
            string path = AnimationUtility.CalculateTransformPath(camera.transform, root);
            Debug.Log($"[Stage1Setup.Camera] root={root.name} path={path} " +
                      $"enabled={camera.enabled} tag={camera.tag}");
        }
    }
}
#endif
