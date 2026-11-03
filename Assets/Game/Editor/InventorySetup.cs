using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
[InitializeOnLoad]
public static class InventorySetup
{
    static InventorySetup(){EditorApplication.delayCall+=Setup;}
    [MenuItem("Tools/Dead Shot/Setup Inventory")]
    static void Setup(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="Stage_city")return;
        foreach(var e in Object.FindObjectsOfType<InventoryUI>(true))if(e.gameObject.scene==scene)return;
        var pause=Object.FindObjectOfType<PauseMenuController>(true);if(pause==null||pause.descriptionText==null)return;
        var go=new GameObject("Inventory System");Undo.RegisterCreatedObjectUndo(go,"Create Inventory");
        var data=go.AddComponent<PlayerInventory>();
        data.catalog.Add(new PlayerInventory.Definition{id="m1911",displayName="M1911",category=InventoryCategory.Weapons,shortcutWeaponId="handgun",initialCount=1,description="近距離での戦闘に適した半自動ハンドガン。扱いやすく、確実な狙いで敵を撃退できる。",icon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/UI/ハンドガンUI透過.png")});
        data.catalog.Add(new PlayerInventory.Definition{id="m1914",displayName="特殊ゴーグル",category=InventoryCategory.Weapons,initialCount=1,description="敵ごとに異なる弱点を赤い発光点として可視化する特殊装備。照準を完全に収束させて弱点を撃ち抜くと一撃で倒せる。",icon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/UI/ヘルム.png")});
        var canvasGo=new GameObject("Inventory Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasGo.transform.SetParent(go.transform,false);
        var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10000;
        var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        var ui=go.AddComponent<InventoryUI>();ui.inventory=data;ui.font=pause.descriptionText.font;ui.audioSource=go.AddComponent<AudioSource>();ui.audioSource.playOnAwake=false;ui.audioSource.ignoreListenerPause=true;
        ui.moveSe=pause.selectionMoveSe;ui.confirmSe=pause.selectionConfirmSe;ui.volume=pause.selectionSeVolume;ui.Build(canvasGo.transform);
        EditorUtility.SetDirty(ui);EditorUtility.SetDirty(data);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("[InventorySetup] M1911・分類タブ・停止制御・共通SEをStage_cityに登録しました。");
    }
}
