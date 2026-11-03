using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EquippedWeaponHudSetup
{
    static EquippedWeaponHudSetup() { EditorApplication.delayCall += Setup; }

    [MenuItem("Tools/Dead Shot/Setup Equipped Weapon HUD")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Stage_city") return;
        GameObject gunUi = GameObject.Find("銃UI");
        if (gunUi == null) return;

        EquippedWeaponHud hud = gunUi.GetComponent<EquippedWeaponHud>();
        bool changed = false;
        if (hud == null)
        {
            hud = Undo.AddComponent<EquippedWeaponHud>(gunUi);
            changed = true;
        }
        hud.PrepareInspector();
        EditorUtility.SetDirty(hud);
        PlayerInventory inventory = Object.FindObjectOfType<PlayerInventory>(true);
        PlayerInventory.Definition grenade = inventory != null ? inventory.Find("m1916") : null;
        if (grenade != null && grenade.maxCount != 30)
        {
            Undo.RecordObject(inventory, "Set Grenade Max Count");
            grenade.maxCount = 30;
            EditorUtility.SetDirty(inventory);
            changed = true;
        }
        if (!changed) return;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[EquippedWeaponHudSetup] 銃UIへ装備HUD設定を追加しました。");
    }
}
