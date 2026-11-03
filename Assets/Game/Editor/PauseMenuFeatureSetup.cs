using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PauseMenuFeatureSetup
{
    static PauseMenuFeatureSetup() { EditorApplication.delayCall += Configure; }

    [MenuItem("Tools/Dead Shot/Configure Pause Menu Features")]
    static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Stage_city") return;
        var p = Object.FindObjectsOfType<PauseMenuController>(true).FirstOrDefault(x => x.gameObject.scene == scene);
        if (p == null || p.menuFeatureVersion >= 1) return;
        var canvas = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).FirstOrDefault(x => x.name == "Canvas");
        if (canvas == null) return;
        var ui = canvas.transform.Find("ポーズUI");
        if (ui == null) return;
        p.mainMenuGroup = ui.GetComponent<CanvasGroup>();
        p.descriptionText = ui.Find("項目/説明文").GetComponent<TMP_Text>();
        string[] descriptions = { "中断したゲームを再開します。", "操作方法を確認します。", "設定画面を開きます。", "タイトル画面に戻ります。" };
        var actions = new[] { PauseMenuController.MenuAction.Resume, PauseMenuController.MenuAction.Controls, PauseMenuController.MenuAction.Settings, PauseMenuController.MenuAction.Title };
        for (int i = 0; i < p.items.Length && i < actions.Length; i++)
        {
            p.items[i].description = descriptions[i];
            p.items[i].action = actions[i];
        }
        var landing = Object.FindObjectsOfType<LandingPointSelectionUI>(true).FirstOrDefault(x => x.gameObject.scene == scene);
        if (landing != null)
        {
            var data = new SerializedObject(landing);
            p.selectionMoveSe = data.FindProperty("selectionMoveSe").objectReferenceValue as AudioClip;
            p.selectionConfirmSe = data.FindProperty("selectionConfirmSe").objectReferenceValue as AudioClip;
            p.selectionSeVolume = data.FindProperty("selectionSeVolume").floatValue;
        }
        var audio = new GameObject("Pause UI Audio", typeof(AudioSource));
        audio.transform.SetParent(p.transform, false);
        p.uiAudioSource = audio.GetComponent<AudioSource>();
        p.uiAudioSource.playOnAwake = false;
        p.uiAudioSource.spatialBlend = 0;
        p.uiAudioSource.ignoreListenerPause = true;
        p.settingsPanel = Panel(canvas.transform, "Pause Settings Panel", "OPTIONS / 設定", p.descriptionText.font, out p.settingsBackButton);
        p.controlsPanel = Panel(canvas.transform, "Pause Controls Panel", "CONTROLS / 操作方法", p.descriptionText.font, out p.controlsBackButton);
        p.titleSceneName = "Title2";
        p.menuFeatureVersion = 1;
        EditorUtility.SetDirty(p);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PauseFeatures] 説明文・OP選択SE・各項目の動作・サブパネルを登録して保存しました。");
    }

    static CanvasGroup Panel(Transform parent, string name, string title, TMP_FontAsset font, out Button back)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0.035f, 0.05f, 0.06f, 0.97f);
        Text(go.transform, "Heading", title, font, new Vector2(0, 220), new Vector2(800, 80), 36);
        var button = new GameObject("Back Button", typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(go.transform, false);
        var br = (RectTransform)button.transform;
        br.sizeDelta = new Vector2(320, 64); br.anchoredPosition = new Vector2(0, -240);
        button.GetComponent<Image>().color = new Color(0.18f, 0.24f, 0.28f, 1);
        back = button.GetComponent<Button>();
        back.targetGraphic = button.GetComponent<Image>();
        Text(button.transform, "Label", "BACK / 戻る", font, Vector2.zero, br.sizeDelta, 26);
        var group = go.GetComponent<CanvasGroup>();
        go.SetActive(false);
        return group;
    }

    static void Text(Transform parent, string name, string value, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position;
    }
}
