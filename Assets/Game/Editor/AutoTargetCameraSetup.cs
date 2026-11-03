using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AutoTargetCameraSetup
{
    static AutoTargetCameraSetup() { EditorApplication.delayCall += Install; }

    [MenuItem("Tools/Dead Shot/Setup Auto Target Camera")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Stage_city") return;
        var player = Object.FindObjectOfType<UnityChan.UnityChanControlScriptWithRgidBody>(true);
        if (player == null) return;
        AutoTargetCameraController controller = player.GetComponent<AutoTargetCameraController>();
        bool changed = false;
        if (controller == null)
        {
            controller = Undo.AddComponent<AutoTargetCameraController>(player.gameObject);
            changed = true;
        }
        Sprite arrow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/UI/白矢印.png");
        if (controller.targetArrowSprite != arrow)
        {
            Undo.RecordObject(controller, "Assign Auto Target Arrow");
            controller.targetArrowSprite = arrow;
            EditorUtility.SetDirty(controller);
            changed = true;
        }
        const string clickIconPath = "Assets/Game/UI/InputIcons/MouseLeftClick.png";
        TextureImporter clickImporter = AssetImporter.GetAtPath(clickIconPath) as TextureImporter;
        if (clickImporter != null && (clickImporter.textureType != TextureImporterType.Sprite ||
                                      clickImporter.spriteImportMode != SpriteImportMode.Single ||
                                      !clickImporter.alphaIsTransparency))
        {
            clickImporter.textureType = TextureImporterType.Sprite;
            clickImporter.spriteImportMode = SpriteImportMode.Single;
            clickImporter.alphaIsTransparency = true;
            clickImporter.mipmapEnabled = false;
            clickImporter.SaveAndReimport();
        }
        ItemPickupMarkerUI pickupMarker = Object.FindObjectOfType<ItemPickupMarkerUI>(true);
        if (pickupMarker != null)
        {
            Sprite clickIcon = AssetDatabase.LoadAssetAtPath<Sprite>(clickIconPath);
            if (pickupMarker.downArrowSprite != arrow || pickupMarker.pickupInputSprite != clickIcon)
            {
                Undo.RecordObject(pickupMarker, "Assign Item Pickup Marker Sprites");
                pickupMarker.downArrowSprite = arrow;
                pickupMarker.pickupInputSprite = clickIcon;
                EditorUtility.SetDirty(pickupMarker);
                changed = true;
            }
        }
        if (!Mathf.Approximately(controller.arrowRotation, 270f))
        {
            Undo.RecordObject(controller, "Fix Auto Target Arrow Rotation");
            controller.arrowRotation = 270f;
            EditorUtility.SetDirty(controller);
            changed = true;
        }
        ThirdPersonCameraController thirdPerson = player.GetComponent<ThirdPersonCameraController>();
        if (thirdPerson != null)
        {
            SerializedObject serialized = new SerializedObject(thirdPerson);
            SerializedProperty firstPersonOffset = serialized.FindProperty("normalFirstPersonOffset");
            SerializedProperty aimFirstPersonOffset = serialized.FindProperty("aimFirstPersonOffset");
            Vector3 safeOffset = new Vector3(0f, .08f, .28f);
            if (firstPersonOffset != null && firstPersonOffset.vector3Value != safeOffset)
            {
                Undo.RecordObject(thirdPerson, "Fix First Person Camera Offset");
                firstPersonOffset.vector3Value = safeOffset;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(thirdPerson);
                changed = true;
            }
            if (aimFirstPersonOffset != null && aimFirstPersonOffset.vector3Value == Vector3.zero)
            {
                Undo.RecordObject(thirdPerson, "Set Aim First Person Camera Offset");
                aimFirstPersonOffset.vector3Value = safeOffset;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(thirdPerson);
                changed = true;
            }
        }
        if (!changed) return;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Auto Target] Playerへ自動ターゲットカメラと白矢印を設定しました。", controller);
    }
}
