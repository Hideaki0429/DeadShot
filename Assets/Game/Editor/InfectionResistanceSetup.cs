using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class InfectionResistanceSetup
{
    static InfectionResistanceSetup() { EditorApplication.delayCall += Install; }

    [MenuItem("Dead Shot/Player/Setup Infection Resistance")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "Stage_city") return;
        PlayerHealth health = Object.FindObjectOfType<PlayerHealth>(true);
        PlayerInventory inventory = Object.FindObjectOfType<PlayerInventory>(true);
        if (health == null || inventory == null) return;
        bool changed = false;

        PlayerInfectionResistance infection = health.GetComponent<PlayerInfectionResistance>();
        if (infection == null)
        {
            infection = Undo.AddComponent<PlayerInfectionResistance>(health.gameObject);
            changed = true;
        }
        if (!Mathf.Approximately(infection.maximumResistanceSeconds, 1200f))
        {
            Undo.RecordObject(infection, "Set Infection Time Maximum");
            infection.maximumResistanceSeconds = 1200f;
            EditorUtility.SetDirty(infection);
            changed = true;
        }
        Image circle = Object.FindObjectsOfType<Image>(true)
            .FirstOrDefault(value => value.gameObject.scene == scene && value.name == "ConditionIcon");
        if (circle != null && infection.resistanceIcon != circle)
        {
            Undo.RecordObject(infection, "Link Infection Resistance HUD");
            infection.resistanceIcon = circle;
            EditorUtility.SetDirty(infection);
            changed = true;
        }

        PlayerHealing healing = health.GetComponent<PlayerHealing>();
        if (healing != null && healing.infectionMedicineItemId != "m1924")
        {
            Undo.RecordObject(healing, "Set Infection Medicine ID");
            healing.infectionMedicineItemId = "m1924";
            EditorUtility.SetDirty(healing);
            changed = true;
        }

        PlayerInventory.Definition definition = inventory.Find("m1924");
        if (definition == null)
        {
            Undo.RecordObject(inventory, "Register Infection Medicine");
            definition = new PlayerInventory.Definition
            {
                id = "m1924",
                displayName = "感染緩和薬",
                description = "感染耐性の制限時間を5分間延長する。",
                category = InventoryCategory.Supplies,
                initialCount = 0,
                maxCount = 0,
                icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/UI/感染緩和薬UI.png"),
                inspectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Item/AE_Medical_Supplies_04.prefab"),
                inspectionRotation = new Vector3(10f, 30f, 0f),
                inspectionPosition = new Vector3(0f, -.5f, 0f),
                inspectionScale = 1f
            };
            inventory.catalog.Add(definition);
            EditorUtility.SetDirty(inventory);
            changed = true;
        }

        if (!changed) return;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Infection] m1924・感染耐性タイマー・ConditionIconを設定しました。", infection);
    }
}
