// 【役割】アイテムへの接近距離に応じて取得マーカーと操作案内を表示する。
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(500)]
public sealed class ItemPickupMarkerUI : MonoBehaviour
{
    public PlayerItemCollector collector;
    public Camera viewCamera;
    public TMP_FontAsset font;
    [Header("マーカー位置・見た目")]
    public Vector3 worldOffset = new Vector3(0, .7f, 0);
    [Min(16)]
    public float ringSize = 48;
    [Min(1)]
    public float ringThickness = 4;
    public Color markerColor = Color.white;
    public Color progressColor = Color.white;
    public Color trackColor = new Color(1, 1, 1, .22f);
    public Sprite downArrowSprite;
    public Sprite pickupInputSprite;
    public Vector2 arrowSize = new Vector2(46, 34);
    public Vector2 inputIconSize = new Vector2(44, 58);
    public Vector2 inputIconOffset = new Vector2(0, 54);
    public float arrowRotation = 270;
    public string prompt = "";
    [Min(10)]
    public float fontSize = 20;
    [Header("遮蔽物")]
    public LayerMask obstructionMask = Physics.DefaultRaycastLayers;
    private Canvas canvas;
    private RectTransform marker;
    private Image arrow, inputIcon;
    private TMP_Text label;
    public bool IsVisible => marker != null && marker.gameObject.activeSelf;
    public float DisplayedProgress => collector != null ? collector.Progress : 0;

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        Build();
    }

    void Build()
    {
        var root = new GameObject("Item Pickup Marker Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;
        marker = Rect("Marker", root.transform, Vector2.zero);
        arrow = Icon("Pickup Down Arrow", marker);
        inputIcon = Icon("Left Click Input", marker);
        var text = Rect("Hold Prompt", marker, new Vector2(300, 36));
        text.anchoredPosition = new Vector2(0, -50);
        label = text.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        marker.gameObject.SetActive(false);
    }

    static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.sizeDelta = size;
        return r;
    }

    static Image Icon(string name, Transform parent)
    {
        var i = Rect(name, parent, Vector2.one).gameObject.AddComponent<Image>();
        i.raycastTarget = false;
        return i;
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        if (marker == null)
        {
            return;
        }

        var target = collector != null ? collector.GetDisplayTarget() : null;
        var camera = viewCamera != null ? viewCamera : Camera.main;
        if (target == null || camera == null || !camera.isActiveAndEnabled)
        {
            marker.gameObject.SetActive(false);
            return;
        }

        Vector3 anchor = target.Trigger.bounds.center + worldOffset;
        Vector3 screen = camera.WorldToScreenPoint(anchor);
        if (screen.z <= camera.nearClipPlane || !camera.pixelRect.Contains(new Vector2(screen.x, screen.y)) || Occluded(camera, target))
        {
            marker.gameObject.SetActive(false);
            return;
        }

        marker.gameObject.SetActive(true);
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out var position);
        marker.anchoredPosition = position;
        RefreshStyle(target);
    }

    bool Occluded(Camera camera, InventoryPickup target)
    {
        Vector3 delta = target.Trigger.bounds.center - camera.transform.position;
        foreach (var hit in Physics.RaycastAll(camera.transform.position, delta.normalized, delta.magnitude, obstructionMask, QueryTriggerInteraction.Ignore))
        {
            var t = hit.collider.transform;
            if (t == target.transform || t.IsChildOf(target.transform) || t == collector.transform || t.IsChildOf(collector.transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    void RefreshStyle(InventoryPickup target)
    {
        arrow.sprite = downArrowSprite;
        arrow.color = markerColor;
        arrow.preserveAspect = true;
        arrow.rectTransform.sizeDelta = arrowSize;
        arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, arrowRotation);
        inputIcon.sprite = pickupInputSprite;
        inputIcon.color = Color.white;
        inputIcon.preserveAspect = true;
        inputIcon.rectTransform.sizeDelta = inputIconSize;
        inputIcon.rectTransform.anchoredPosition = inputIconOffset;
        inputIcon.enabled = pickupInputSprite != null;
        // 初回取得前は名前を隠し、2回目以降は個数なしのアイテム名だけ表示する。
        label.text = target != null ? target.MarkerPrompt : "";
        label.fontSize = fontSize;
        label.color = markerColor;
        label.rectTransform.anchoredPosition = new Vector2(0, -arrowSize.y * .5f - 24);
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        if (marker != null)
        {
            marker.gameObject.SetActive(false);
        }
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (canvas != null)
        {
            Destroy(canvas.gameObject);
        }
    }
}
