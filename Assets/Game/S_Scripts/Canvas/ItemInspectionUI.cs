// 【役割】初取得アイテムの3Dモデル、説明、回転と拡大縮小の操作を管理する。
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-960)]
public sealed class ItemInspectionUI : MonoBehaviour
{
    struct PendingPickup
    {
        public PlayerInventory.Definition definition;
        public GameObject source;
        public int amount;
    }

    static readonly Queue<PendingPickup> pendingPickups = new Queue<PendingPickup>();
    static ItemInspectionUI instance;
    const int PreviewLayer = 31;
    static readonly Color Line = new Color(0.29f, 0.47f, 0.53f, 0.9f);
    static readonly Color Dark = new Color(0.012f, 0.018f, 0.021f, 0.97f);
    [Header("操作")]
    [Min(1f)]
    public float rotateSpeed = 8f;
    [Min(0.01f)]
    public float zoomSpeed = 0.22f;
    [Min(0.1f)]
    public float minZoom = 2.2f;
    [Min(0.2f)]
    public float maxZoom = 6.5f;
    [Min(0.1f)]
    public float initialZoom = 4.1f;
    [Min(1f)]
    public float controllerRotateSpeed = 110f;
    [Min(0.01f)]
    public float controllerZoomSpeed = 1.6f;
    [Header("Hierarchy編集用参照")]
    [SerializeField]
    GameObject panel;
    [SerializeField]
    TMP_FontAsset uiFont;
    [SerializeField]
    RawImage preview;
    [SerializeField]
    RectTransform previewRect;
    [SerializeField]
    TMP_Text itemName, itemType, description;
    [SerializeField]
    Camera previewCamera;
    [SerializeField]
    Light keyLight, fillLight;
    [SerializeField]
    Transform pivot;
    GameObject displayedObject;
    RenderTexture texture;
    Vector3 initialEuler;
    Vector3 viewOffset;
    PlayerInventory.Definition activeDefinition;
    Vector3 normalizedBaseScale;
    Vector3 lastPosition, lastRotation, lastPivotOffset;
    float lastScale;
    float distance;
    float yawDegrees;
    int openedFrame;
    bool waitForLeftRelease;
    float savedScale;
    bool savedAudio, savedCursorVisible, savedNavigation;
    CursorLockMode savedCursorLock;
    EventSystem savedEventSystem;
    public bool IsOpen { get; private set; }
    public bool HasEditableHierarchy => panel != null;

    public static void ShowPickup(PlayerInventory.Definition definition, GameObject source, int amount)
    {
        if (definition == null)
        {
            return;
        }

        EnsureInstance();
        if (instance.IsOpen)
        {
            pendingPickups.Enqueue(new PendingPickup { definition = definition, source = source, amount = amount });
            return;
        }

        instance.Open(definition, source, amount);
    }

    static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindObjectOfType<ItemInspectionUI>(true);
        if (instance == null)
        {
            instance = new GameObject("Item Inspection System").AddComponent<ItemInspectionUI>();
        }

        // 非表示にするのはPanelだけ。親Systemが無効だとPanelを有効化しても描画されない。
        if (!instance.gameObject.activeSelf)
        {
            instance.gameObject.SetActive(true);
        }

        instance.EnsureBuilt();
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureBuilt();
    }

    void EnsureBuilt()
    {
        if (panel != null)
        {
            EnsureRenderTexture();
            return;
        }

        InventoryUI inventoryUI = FindObjectOfType<InventoryUI>(true);
        uiFont = inventoryUI != null && inventoryUI.font != null ? inventoryUI.font : TMP_Settings.defaultFontAsset;
        BuildPreviewWorld();
        BuildCanvas();
        panel.SetActive(false);
    }

    // Editor設定から一度だけ呼び、編集可能なCanvasをシーンに保存する。
    public void BuildEditableHierarchy()
    {
        if (panel != null)
        {
            return;
        }

        InventoryUI inventoryUI = FindObjectOfType<InventoryUI>(true);
        uiFont = inventoryUI != null && inventoryUI.font != null ? inventoryUI.font : TMP_Settings.defaultFontAsset;
        BuildPreviewWorld();
        BuildCanvas();
        panel.SetActive(false);
    }

    void BuildPreviewWorld()
    {
        var world = new GameObject("Inspection Preview World");
        world.transform.SetParent(transform, false);
        world.transform.position = new Vector3(10000f, -10000f, 10000f);
        pivot = new GameObject("Item Pivot").transform;
        pivot.SetParent(world.transform, false);
        var cameraObject = new GameObject("Item Preview Camera");
        cameraObject.transform.SetParent(world.transform, false);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        // RenderTextureの背景は完全透過。UIの背景画像の上に3Dモデルだけを重ねる。
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.fieldOfView = 30f;
        previewCamera.nearClipPlane = 0.05f;
        previewCamera.farClipPlane = 50f;
        EnsureRenderTexture();
        previewCamera.enabled = false;
        keyLight = CreateLight("Inspection Key Light", world.transform, new Vector3(-2f, 2.5f, -2f), 1.25f, new Color(.72f, .86f, 1f));
        fillLight = CreateLight("Inspection Fill Light", world.transform, new Vector3(2f, -.5f, -1f), .55f, new Color(1f, .72f, .58f));
    }

    void EnsureRenderTexture()
    {
        if (previewCamera == null)
        {
            return;
        }

        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        if (texture == null)
        {
            texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            texture.name = "Item Inspection Render Texture";
            texture.Create();
        }

        previewCamera.targetTexture = texture;
        if (preview != null)
        {
            preview.texture = texture;
        }
    }

    Light CreateLight(string lightName, Transform parent, Vector3 position, float intensity, Color color)
    {
        var go = new GameObject(lightName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 10f;
        light.intensity = intensity;
        light.color = color;
        light.cullingMask = 1 << PreviewLayer;
        return light;
    }

    void BuildCanvas()
    {
        var canvasObject = new GameObject("Item Inspection Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        panel = Stretch("Item Inspection Panel", canvasObject.transform).gameObject;
        Image background = panel.AddComponent<Image>();
        background.color = Dark;
        RectTransform topAccent = Box(panel.transform, "Header Accent", 72, 56, 6, 42, Line);
        Label(panel.transform, "RECOVERED OBJECT / 回収品", 96, 54, 620, 48, 28, FontStyles.Normal);
        Box(panel.transform, "Header Line", 530, 77, 1320, 1.5f, Line);
        RectTransform viewFrame = Rect("Inspection View", panel.transform, 390, 150, 1140, 560);
        previewRect = Rect("3D Item Preview", viewFrame, 0, 0, 1140, 560);
        preview = previewRect.gameObject.AddComponent<RawImage>();
        preview.texture = texture;
        preview.color = Color.white;
        preview.raycastTarget = false;
        Corner(viewFrame, 0, 0, 1, 1);
        Corner(viewFrame, 1140, 0, -1, 1);
        Corner(viewFrame, 0, 560, 1, -1);
        Corner(viewFrame, 1140, 560, -1, -1);
        EllipseGuide(viewFrame);
        itemName = Label(panel.transform, "", 95, 744, 620, 70, 52, FontStyles.Normal);
        Box(panel.transform, "Name Divider", 78, 820, 625, 1.5f, Line);
        itemType = Label(panel.transform, "", 100, 835, 560, 40, 25, FontStyles.Normal);
        RectTransform detail = Frame(panel.transform, "Description Frame", 940, 740, 895, 145);
        Label(detail, "DESCRIPTION", 32, 18, 350, 30, 18, FontStyles.Normal).color = Line;
        Box(detail, "Description Divider", 28, 52, 840, 1, Line);
        description = Label(detail, "", 32, 65, 820, 65, 22, FontStyles.Normal);
        description.enableWordWrapping = true;
        Box(panel.transform, "Footer Divider", 75, 930, 1770, 1.5f, Line);
        TMP_Text guide = Label(panel.transform, "マウス移動：回転      ホイール：拡大・縮小      右クリック：リセット      左クリック：戻る", 710, 957, 1135, 44, 22, FontStyles.Normal);
        guide.alignment = TextAlignmentOptions.Right;
    }

    void Open(PlayerInventory.Definition definition, GameObject source, int amount)
    {
        EnsureBuilt();
        if (IsOpen)
        {
            Close();
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (panel.transform.parent != null)
        {
            panel.transform.parent.localScale = Vector3.one;
        }

        panel.transform.localScale = Vector3.one;
        GameObject template = definition.inspectionPrefab != null ? definition.inspectionPrefab : source;
        displayedObject = template != null ? Instantiate(template, pivot) : new GameObject("Missing Inspection Model");
        if (template == null)
        {
            displayedObject.transform.SetParent(pivot, false);
        }

        displayedObject.name = "Displayed " + definition.displayName;
        PrepareDisplayObject(displayedObject);
        activeDefinition = definition;
        initialEuler = definition.inspectionRotation;
        yawDegrees = 0f;
        ApplyPreviewRotation();
        FitObject(definition);
        distance = Mathf.Clamp(initialZoom, minZoom, maxZoom);
        PositionCamera();
        itemName.text = definition.displayName + (amount > 1 ? "  x" + amount : "");
        itemType.text = definition.category == InventoryCategory.Weapons ? "武器" : "供給品";
        description.text = string.IsNullOrWhiteSpace(definition.description) ? "詳細情報はありません。" : definition.description;
        savedScale = Time.timeScale;
        savedAudio = AudioListener.pause;
        savedCursorVisible = Cursor.visible;
        savedCursorLock = Cursor.lockState;
        savedEventSystem = EventSystem.current;
        if (savedEventSystem != null)
        {
            savedNavigation = savedEventSystem.sendNavigationEvents;
            savedEventSystem.sendNavigationEvents = false;
            savedEventSystem.SetSelectedGameObject(null);
        }

        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.SetPaused(true);
        }

        // 検査中はカーソルを画面中央に固定して隠す。
        // Mouse Xはそのまま取得できるため、モデルは連続して360度回転できる。
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        openedFrame = Time.frameCount;
        waitForLeftRelease = Input.GetMouseButton(0);
        panel.SetActive(true);
        previewCamera.enabled = true;
        IsOpen = true;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (!IsOpen || Time.frameCount <= openedFrame)
        {
            return;
        }

        ApplyCatalogPreviewChanges();
        if (waitForLeftRelease)
        {
            if (!Input.GetMouseButton(0))
            {
                waitForLeftRelease = false;
            }

            return;
        }

        if (RectTransformUtility.RectangleContainsScreenPoint(previewRect, Input.mousePosition))
        {
            float x = Input.GetAxisRaw("Mouse X");
            if (Mathf.Abs(x) > .001f)
            {
                // 上下方向の回転は行わず、ワールドY軸だけを無制限に回す。
                yawDegrees = Mathf.Repeat(yawDegrees - x * rotateSpeed, 360f);
                ApplyPreviewRotation();
            }

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > .001f)
            {
                distance = Mathf.Clamp(distance - wheel * zoomSpeed, minZoom, maxZoom);
                PositionCamera();
            }
        }

        GameInputManager gameInput = GameInputManager.Instance;
        if (gameInput != null)
        {
            Vector2 stick = gameInput.InspectionRotate;
            if (Mathf.Abs(stick.x) > .001f)
            {
                yawDegrees = Mathf.Repeat(yawDegrees - stick.x * controllerRotateSpeed * Time.unscaledDeltaTime, 360f);
                ApplyPreviewRotation();
            }

            float padZoom = gameInput.InspectionZoom;
            if (Mathf.Abs(padZoom) > .01f)
            {
                distance = Mathf.Clamp(distance - padZoom * controllerZoomSpeed * Time.unscaledDeltaTime, minZoom, maxZoom);
                PositionCamera();
            }
        }

        if (Input.GetMouseButtonDown(1) || (gameInput != null && gameInput.InspectionResetPressed))
        {
            ResetView();
        }
        else if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Escape) || (gameInput != null && gameInput.InspectionClosePressed))
        {
            Close();
        }
    }

    void ResetView()
    {
        yawDegrees = 0f;
        ApplyPreviewRotation();
        distance = Mathf.Clamp(initialZoom, minZoom, maxZoom);
        PositionCamera();
    }

    void ApplyPreviewRotation()
    {
        pivot.localRotation = Quaternion.AngleAxis(yawDegrees, Vector3.up) * Quaternion.Euler(initialEuler);
    }

    void PositionCamera()
    {
        // 表示位置はカメラで調整し、モデルの回転軸と分離する。
        previewCamera.transform.localPosition = new Vector3(-viewOffset.x, -viewOffset.y, -distance + viewOffset.z);
        previewCamera.transform.localRotation = Quaternion.identity;
    }

    void FitObject(PlayerInventory.Definition definition)
    {
        Renderer[] renderers = displayedObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float autoScale = largest > .0001f ? 2.2f / largest : 1f;
        normalizedBaseScale = displayedObject.transform.localScale * autoScale;
        ApplyDefinitionTransform(definition, true);
    }

    void ApplyCatalogPreviewChanges()
    {
        if (activeDefinition == null || displayedObject == null)
        {
            return;
        }

        if (activeDefinition.inspectionPosition == lastPosition && activeDefinition.inspectionRotation == lastRotation && activeDefinition.inspectionPivotOffset == lastPivotOffset && Mathf.Approximately(activeDefinition.inspectionScale, lastScale))
        {
            return;
        }

        ApplyDefinitionTransform(activeDefinition, false);
    }

    void ApplyDefinitionTransform(PlayerInventory.Definition definition, bool force)
    {
        float custom = definition.inspectionScale > 0f ? definition.inspectionScale : 1f;
        displayedObject.transform.localScale = normalizedBaseScale * custom;
        displayedObject.transform.localPosition = Vector3.zero;
        if (force || definition.inspectionRotation != lastRotation)
        {
            initialEuler = definition.inspectionRotation;
            yawDegrees = 0f;
            ApplyPreviewRotation();
        }

        Renderer[] renderers = displayedObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 rotationCenter = bounds.center + definition.inspectionPivotOffset;
            displayedObject.transform.position += pivot.position - rotationCenter;
        }

        viewOffset = definition.inspectionPosition;
        lastPosition = definition.inspectionPosition;
        lastRotation = definition.inspectionRotation;
        lastPivotOffset = definition.inspectionPivotOffset;
        lastScale = definition.inspectionScale;
        PositionCamera();
    }

    void PrepareDisplayObject(GameObject root)
    {
        SetLayer(root.transform, PreviewLayer);
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>(true))
        {
            audio.enabled = false;
        }

        foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
        }
    }

    static void SetLayer(Transform target, int layer)
    {
        target.gameObject.layer = layer;
        for (int i = 0; i < target.childCount; i++)
        {
            SetLayer(target.GetChild(i), layer);
        }
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        previewCamera.enabled = false;
        panel.SetActive(false);
        if (displayedObject != null)
        {
            Destroy(displayedObject);
        }

        displayedObject = null;
        activeDefinition = null;
        Time.timeScale = savedScale;
        AudioListener.pause = savedAudio;
        Cursor.lockState = savedCursorLock;
        Cursor.visible = savedCursorVisible;
        if (savedEventSystem != null)
        {
            savedEventSystem.SetSelectedGameObject(null);
            savedEventSystem.sendNavigationEvents = savedNavigation;
        }

        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.SetPaused(false);
        }

        if (pendingPickups.Count > 0)
        {
            PendingPickup next = pendingPickups.Dequeue();
            Open(next.definition, next.source, next.amount);
        }
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    RectTransform Stretch(string objectName, Transform parent)
    {
        RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    RectTransform Rect(string objectName, Transform parent, float x, float y, float w, float h)
    {
        RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    RectTransform Box(Transform parent, string objectName, float x, float y, float w, float h, Color color)
    {
        RectTransform rect = Rect(objectName, parent, x, y, w, h);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    RectTransform Frame(Transform parent, string objectName, float x, float y, float w, float h)
    {
        RectTransform frame = Rect(objectName, parent, x, y, w, h);
        Box(frame, "Top", 0, 0, w, 1.5f, Line);
        Box(frame, "Bottom", 0, h - 1.5f, w, 1.5f, Line);
        Box(frame, "Left", 0, 0, 1.5f, h, Line);
        Box(frame, "Right", w - 1.5f, 0, 1.5f, h, Line);
        return frame;
    }

    TMP_Text Label(Transform parent, string value, float x, float y, float w, float h, float size, FontStyles style)
    {
        TMP_Text label = Rect(value, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = value;
        label.font = uiFont;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        return label;
    }

    void Corner(Transform parent, float x, float y, float sx, float sy)
    {
        float px = sx > 0 ? x : x - 34, py = sy > 0 ? y : y - 2;
        Box(parent, "Corner H", px, py, 34, 2, Line);
        px = sx > 0 ? x : x - 2;
        py = sy > 0 ? y : y - 34;
        Box(parent, "Corner V", px, py, 2, 34, Line);
    }

    void EllipseGuide(Transform parent)
    {
        RectTransform line = Rect("Rotation Guide", parent, 160, 270, 820, 1.5f);
        Image image = line.gameObject.AddComponent<Image>();
        image.color = new Color(Line.r, Line.g, Line.b, .45f);
        image.raycastTarget = false;
        for (int i = 0; i < 25; i++)
        {
            Box(parent, "Rotation Tick", 165 + i * 33.5f, 282 + Mathf.Abs(12 - i) * 1.2f, 1, 9, new Color(Line.r, Line.g, Line.b, .55f));
        }
    }
}
