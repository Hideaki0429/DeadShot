using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class WeaponSelectionSetup
{
    static WeaponSelectionSetup() { EditorApplication.delayCall += Setup; }
    [MenuItem("Tools/Dead Shot/Setup Weapon Selection")]
    static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Stage_city") return;
        foreach (var existing in Object.FindObjectsOfType<WeaponSelectionUI>(true))
            if (existing.gameObject.scene == scene) return;
        RectTransform panel = null;
        foreach (var t in Object.FindObjectsOfType<RectTransform>(true))
            if (t.gameObject.scene == scene && t.name == "武器選択パネル") { panel = t; break; }
        if (panel == null) return;
        var player = Object.FindObjectOfType<UnityChan.UnityChanControlScriptWithRgidBody>(true);
        if (player == null) return;
        var weapon = player.GetComponentInChildren<OnKeyPress_Throw>(true);
        if (weapon == null) weapon = Object.FindObjectOfType<OnKeyPress_Throw>(true);
        if (weapon == null) return;
        var host = new GameObject("Weapon Selection System");
        Undo.RegisterCreatedObjectUndo(host, "Setup weapon selection");
        var ui = host.AddComponent<WeaponSelectionUI>();
        ui.panel = panel.gameObject;
        ui.cursor = panel.Find("カーソルLINE (1)") as RectTransform;
        ui.weaponNameText = panel.Find("選択武器名")?.GetComponent<TMP_Text>();
        ui.playerAnimator = player.GetComponent<Animator>();
        ui.weaponController = weapon;
        string[] names = { "選択パネル (3)", "選択パネル", "選択パネル (2)", "選択パネル (1)" };
        string[] ids = { "flashlight", "handgun", "goggles", "smg" };
        string[] inventoryIds = { "m1912", "m1911", "m1914", "m1918" };
        string[] labels = { "FLASHLIGHT / ライト", "M1911 / ハンドガン", "NIGHT VISION / 暗視ゴーグル", "SMG / サブマシンガン" };
        ui.slots = new WeaponSelectionUI.Slot[4];
        ui.availableWeapons = new WeaponSelectionUI.ShortcutWeaponVisual[5];
        for (int i=0;i<4;i++)
        {
            var frame = panel.Find(names[i]) as RectTransform;
            var slot = new WeaponSelectionUI.Slot { registered=true, inventoryItemId=inventoryIds[i], weaponId=ids[i], displayName=labels[i], frame=frame };
            if (frame != null)
            {
                foreach (var image in frame.GetComponentsInChildren<Image>(true))
                    if (image.transform != frame) { slot.weaponImage=image; slot.icon=image.sprite; break; }
                slot.ammoText = frame.GetComponentInChildren<TMP_Text>(true);
                slot.ammoDisplay = slot.ammoText != null ? slot.ammoText.text : "";
            }
            ui.slots[i]=slot;
            ui.availableWeapons[i]=new WeaponSelectionUI.ShortcutWeaponVisual{weaponId=ids[i],displayName=labels[i],icon=slot.icon,ammoSource=slot.ammoSource};
        }
        Sprite gogglesIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/UI/SpecialGogglesHelmet_White_UI.png");
        if(gogglesIcon!=null){ui.slots[2].icon=gogglesIcon;ui.availableWeapons[2].icon=gogglesIcon;}
        ui.availableWeapons[4]=new WeaponSelectionUI.ShortcutWeaponVisual{
            weaponId="knife",displayName="KNIFE / ナイフ",icon=AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("77ad17bdd0bc241b4be53de15750fbe5"))};
        // Keyboard/mouse-button input is owned by the manager, not EventSystem selection.
        foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget=false;
        panel.gameObject.SetActive(false);
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[WeaponSelectionSetup] 既存4枠・画像・弾数・カーソルを登録して保存しました。");
    }
}
