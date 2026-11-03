using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class SpecialGogglesSetup
{
    private const string EnemyPrefabPath = "Assets/Game/Enemy/MobZombie/MZombie.prefab";

    static SpecialGogglesSetup()
    {
        EditorApplication.delayCall += SetupWhenReady;
    }

    [MenuItem("Tools/Dead Shot/Setup Special Goggles")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        bool prefabChanged = SetupEnemyPrefab();
        bool sceneChanged = SetupActiveScene();

        if (prefabChanged) AssetDatabase.SaveAssets();
        if (sceneChanged)
        {
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (prefabChanged || sceneChanged)
            Debug.Log("[Special Goggles Setup] 停止中Inspector設定・Headボーン追従・弱点表示を登録しました。");
    }

    private static void SetupWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().name != "Stage_city") return;
        Setup();
    }

    private static bool SetupEnemyPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        if (root == null) return false;
        bool changed = false;
        try
        {
            EnemyHealth health = root.GetComponentInChildren<EnemyHealth>(true);
            if (health != null && health.GetComponent<EnemyWeakPoint>() == null)
            {
                health.gameObject.AddComponent<EnemyWeakPoint>();
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
                changed = true;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return changed;
    }

    private static bool SetupActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "Stage_city") return false;

        OnKeyPress_Throw shooter = Object.FindObjectsOfType<OnKeyPress_Throw>(true)
            .FirstOrDefault(component => component.gameObject.scene == scene);
        if (shooter == null) return false;

        bool changed = false;
        AimFocusController focus = shooter.GetComponent<AimFocusController>();
        if (focus == null)
        {
            focus = Undo.AddComponent<AimFocusController>(shooter.gameObject);
            changed = true;
        }

        SerializedObject shooterObject = new SerializedObject(shooter);
        SerializedProperty aimFocus = shooterObject.FindProperty("aimFocus");
        if (aimFocus != null && aimFocus.objectReferenceValue != focus)
        {
            aimFocus.objectReferenceValue = focus;
            shooterObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shooter);
            changed = true;
        }

        Animator animator = shooter.animator != null
            ? shooter.animator
            : shooter.GetComponentInParent<Animator>();
        if (animator == null) return changed;

        Transform playerRoot = animator.transform.root;
        Transform cap = FindNamed(playerRoot, "SK_Military_Cap1");
        Transform helmet = FindNamed(playerRoot, "AE_Special_Forces_Helmet");
        if (helmet == null)
            helmet = Object.FindObjectsOfType<Transform>(true)
                .FirstOrDefault(item => item.gameObject.scene == scene &&
                                        item.name == "AE_Special_Forces_Helmet" &&
                                        item.GetComponentInParent<InventoryPickup>() == null);

        SpecialGogglesController goggles = playerRoot.GetComponent<SpecialGogglesController>();
        if (goggles == null)
        {
            goggles = Undo.AddComponent<SpecialGogglesController>(playerRoot.gameObject);
            changed = true;
        }

        if (helmet != null)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null && helmet.parent != head)
            {
                Undo.SetTransformParent(helmet, head, "Attach goggles to head bone");
                changed = true;
            }
            if (helmet.gameObject.activeSelf)
            {
                Undo.RecordObject(helmet.gameObject, "Hide goggles by default");
                helmet.gameObject.SetActive(false);
                changed = true;
            }
        }
        if (cap != null && !cap.gameObject.activeSelf)
        {
            Undo.RecordObject(cap.gameObject, "Show military cap by default");
            cap.gameObject.SetActive(true);
            changed = true;
        }

        SerializedObject gogglesObject = new SerializedObject(goggles);
        changed |= Assign(gogglesObject, "militaryCap", cap != null ? cap.gameObject : null);
        changed |= Assign(gogglesObject, "specialForcesHelmet", helmet != null ? helmet.gameObject : null);
        gogglesObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(goggles);

        SerializedObject focusObject = new SerializedObject(focus);
        SerializedProperty cameraProperty = focusObject.FindProperty("aimCamera");
        if (cameraProperty != null && cameraProperty.objectReferenceValue != shooter.mainCamera)
        {
            cameraProperty.objectReferenceValue = shooter.mainCamera;
            changed = true;
        }
        SerializedProperty shootingProperty = focusObject.FindProperty("shooting");
        if (shootingProperty != null && shootingProperty.objectReferenceValue != shooter)
        {
            shootingProperty.objectReferenceValue = shooter;
            changed = true;
        }
        Image[] reticles = Object.FindObjectsOfType<Image>(true)
            .Where(image => image.gameObject.scene == scene && image.sprite != null && image.sprite.name == "Target2")
            .OrderBy(image => GetHierarchyPath(image.transform))
            .ToArray();
        SerializedProperty reticleProperty = focusObject.FindProperty("reticles");
        if (reticleProperty != null)
        {
            bool reticlesChanged = reticleProperty.arraySize != reticles.Length;
            if (!reticlesChanged)
            {
                for (int i = 0; i < reticles.Length; i++)
                {
                    if (reticleProperty.GetArrayElementAtIndex(i).objectReferenceValue == reticles[i]) continue;
                    reticlesChanged = true;
                    break;
                }
            }
            if (reticlesChanged)
            {
                reticleProperty.arraySize = reticles.Length;
                for (int i = 0; i < reticles.Length; i++)
                    reticleProperty.GetArrayElementAtIndex(i).objectReferenceValue = reticles[i];
                changed = true;
            }
        }
        focusObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(focus);
        return changed;
    }

    private static bool Assign(SerializedObject target, string propertyName, Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == value) return false;
        property.objectReferenceValue = value;
        return true;
    }

    private static Transform FindNamed(Transform root, string objectName)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(child => child.name == objectName);
    }

    private static string GetHierarchyPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
    }
}
