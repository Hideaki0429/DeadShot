#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LocationAnnouncementSetup
{
    const string Stage1Path = "Assets/Game/S/Stage1.unity";
    const string FontPath = "Assets/Game/Font/Noto_Sans_JP/NotoSansJP-VariableFont_wght SDF.asset";

    [MenuItem("Tools/Dead Shot/Setup Location Announcement UI")]
    static void Setup()
    {
        Scene active = SceneManager.GetActiveScene();
        LocationAnnouncementUI cityUi = EnsureUi(active);
        foreach (GameObject root in active.GetRootGameObjects())
        {
        foreach (PlayerDismountDirector director in root.GetComponentsInChildren<PlayerDismountDirector>(true))
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("locationAnnouncementUI").objectReferenceValue = cityUi;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (StageTransitionTrigger transition in root.GetComponentsInChildren<StageTransitionTrigger>(true))
        {
            SerializedObject transitionData = new SerializedObject(transition);
            transitionData.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            transitionData.ApplyModifiedPropertiesWithoutUndo();
        }
        }
        EditorSceneManager.MarkSceneDirty(active);
        EditorSceneManager.SaveScene(active);

        Scene stage1 = EditorSceneManager.OpenScene(Stage1Path, OpenSceneMode.Additive);
        EnsureUi(stage1);
        EditorSceneManager.MarkSceneDirty(stage1);
        EditorSceneManager.SaveScene(stage1);
        EditorSceneManager.CloseScene(stage1, true);
        Debug.Log("[Location UI] Stage_cityとStage1にHierarchy編集可能なロケーションUIを登録しました。");
    }

    static LocationAnnouncementUI EnsureUi(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            LocationAnnouncementUI existing = root.GetComponentInChildren<LocationAnnouncementUI>(true);
            if (existing != null) return existing;
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        GameObject rootObject = new GameObject("Location Announcement Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(LocationAnnouncementUI));
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        Canvas canvas = rootObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 520;
        CanvasScaler scaler = rootObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;

        RectTransform black = Rect("Landing Black Fade", rootObject.transform, Vector2.zero);
        Stretch(black);
        Image blackImage = black.gameObject.AddComponent<Image>();
        blackImage.color = Color.black;
        blackImage.raycastTarget = false;
        CanvasGroup blackGroup = black.gameObject.AddComponent<CanvasGroup>();

        RectTransform panel = Rect("Location Display", rootObject.transform, new Vector2(700, 110));
        panel.anchoredPosition = new Vector2(0, 220);
        CanvasGroup announcementGroup = panel.gameObject.AddComponent<CanvasGroup>();

        Image upper = Line("Upper Line", panel, new Vector2(620, 2), new Vector2(0, 43));
        Image lower = Line("Lower Line", panel, new Vector2(620, 2), new Vector2(0, -43));
        TextMeshProUGUI glow = Text("Location Glow", panel, font, 37, new Vector2(680, 70));
        TextMeshProUGUI main = Text("Location Name", panel, font, 34, new Vector2(680, 70));

        SerializedObject ui = new SerializedObject(rootObject.GetComponent<LocationAnnouncementUI>());
        ui.FindProperty("announcementGroup").objectReferenceValue = announcementGroup;
        ui.FindProperty("locationText").objectReferenceValue = main;
        ui.FindProperty("glowText").objectReferenceValue = glow;
        ui.FindProperty("upperLine").objectReferenceValue = upper;
        ui.FindProperty("lowerLine").objectReferenceValue = lower;
        ui.FindProperty("blackFadeGroup").objectReferenceValue = blackGroup;
        ui.ApplyModifiedPropertiesWithoutUndo();
        return rootObject.GetComponent<LocationAnnouncementUI>();
    }

    static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        return rect;
    }
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    static Image Line(string name, Transform parent, Vector2 size, Vector2 position)
    {
        RectTransform rect = Rect(name, parent, size);
        rect.anchoredPosition = position;
        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }
    static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Vector2 dimensions)
    {
        RectTransform rect = Rect(name, parent, dimensions);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.text = "拠点１：ベースキャンプ";
        return text;
    }
}
#endif
