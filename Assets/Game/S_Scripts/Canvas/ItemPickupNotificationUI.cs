// 【役割】取得したアイテム名・画像・個数を簡易通知としてフェード表示する。
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ItemPickupNotificationUI : MonoBehaviour
{
    static ItemPickupNotificationUI instance;
    [Header("配置（ミニマップ下の位置調整）")]
    public Vector2 anchoredPosition = new Vector2(55f, -250f);
    public Vector2 size = new Vector2(330f, 54f);
    [Header("表示")]
    [Min(0f)]
    public float visibleSeconds = 1.8f;
    [Min(0.01f)]
    public float fadeSeconds = 0.25f;
    public Color backgroundColor = new Color(0f, 0f, 0f, .65f);
    public Color textColor = Color.white;
    [Header("Hierarchy編集用参照")]
    [SerializeField]
    Canvas notificationCanvas;
    [SerializeField]
    CanvasGroup group;
    [SerializeField]
    RectTransform panel;
    [SerializeField]
    TMP_Text label;
    [SerializeField]
    Image icon;
    Coroutine routine;
    public bool HasEditableHierarchy => notificationCanvas != null && panel != null && group != null && label != null && icon != null;

    public static void Show(PlayerInventory.Definition definition, int amount)
    {
        if (definition == null)
        {
            return;
        }

        Ensure();
        instance.ShowInternal(definition, amount);
    }

    public static void Show(PlayerInventory.Definition definition, int amount, Vector2 position, Vector2 panelSize, float duration, float fadeDuration)
    {
        if (definition == null)
        {
            return;
        }

        Ensure();
        instance.anchoredPosition = position;
        instance.size = panelSize;
        instance.visibleSeconds = Mathf.Max(0f, duration);
        instance.fadeSeconds = Mathf.Max(.01f, fadeDuration);
        instance.ShowInternal(definition, amount);
    }

    static void Ensure()
    {
        if (instance == null)
        {
            instance = FindObjectOfType<ItemPickupNotificationUI>(true);
        }

        if (instance == null)
        {
            instance = new GameObject("Item Pickup Notification").AddComponent<ItemPickupNotificationUI>();
        }

        instance.BuildIfNeeded();
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        instance = this;
        BuildIfNeeded();
        HideImmediate();
    }

    // Editor設定から呼び出し、シーン上で直接編集できる通知UIを作る。
    public void BuildEditableHierarchy()
    {
        BuildIfNeeded();
        HideImmediate();
    }

    void BuildIfNeeded()
    {
        if (HasEditableHierarchy)
        {
            return;
        }

        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        notificationCanvas = canvasObject.GetComponent<Canvas>();
        notificationCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        notificationCanvas.sortingOrder = 180;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;
        var panelObject = new GameObject("Pickup Message Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
        panel.anchoredPosition = anchoredPosition;
        panel.sizeDelta = size;
        group = panelObject.GetComponent<CanvasGroup>();
        panelObject.GetComponent<Image>().color = backgroundColor;
        var iconObject = new GameObject("Item Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(panel, false);
        var iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(1, .5f);
        iconRect.pivot = new Vector2(1, .5f);
        iconRect.anchoredPosition = new Vector2(-12, 0);
        iconRect.sizeDelta = new Vector2(38, 38);
        icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        var labelObject = new GameObject("Item Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(panel, false);
        var textRect = labelObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18, 4);
        textRect.offsetMax = new Vector2(-58, -4);
        label = labelObject.GetComponent<TextMeshProUGUI>();
        var inventoryUI = FindObjectOfType<InventoryUI>(true);
        label.font = inventoryUI != null && inventoryUI.font != null ? inventoryUI.font : TMP_Settings.defaultFontAsset;
        label.fontSize = 24;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.MidlineLeft;
    }

    void ShowInternal(PlayerInventory.Definition definition, int amount)
    {
        bool weapon = definition.category == InventoryCategory.Weapons;
        // 2回目以降の簡易通知では、今回取得した個数も表示する。
        label.text = $"{definition.displayName}  ×  {Mathf.Max(1, amount)}";
        icon.sprite = definition.icon;
        icon.enabled = weapon && definition.icon != null;
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(FadeRoutine());
    }

    IEnumerator FadeRoutine()
    {
        group.gameObject.SetActive(true);
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / fadeSeconds);
            yield return null;
        }

        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(visibleSeconds);
        t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / fadeSeconds);
            yield return null;
        }

        HideImmediate();
    }

    void HideImmediate()
    {
        if (group != null)
        {
            group.alpha = 0f;
            group.gameObject.SetActive(false);
        }
    }
}
