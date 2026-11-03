// 【役割】接近表示と長押し操作を受け取り、ロード画面を経由するステージ移動を開始する。
using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public sealed class StageTransitionTrigger : MonoBehaviour
{
    [Header("遷移先")]
    [SerializeField]
    string targetSceneName = "Stage1";
    [SerializeField]
    string loadingSceneName = "Loading";
    [SerializeField]
    bool startTimerAfterArrival = true;
    [Header("操作")]
    [SerializeField, Min(.1f)]
    float interactionDistance = 2.5f;
    [SerializeField, Min(.1f)]
    float holdDuration = 1.25f;
    [SerializeField]
    string destinationLabel = "ステージ1:廃病院";
    [SerializeField]
    Vector3 markerWorldOffset = new Vector3(0f, 1.2f, 0f);
    [Header("UI")]
    [SerializeField]
    Camera viewCamera;
    [SerializeField]
    TMP_FontAsset font;
    [SerializeField]
    Color ringColor = Color.white;
    [SerializeField]
    Color progressColor = new Color(.35f, .85f, 1f, 1f);
    [SerializeField, Min(20f)]
    float markerSize = 54f;
    [SerializeField, Min(1f)]
    float ringThickness = 4f;
    [SerializeField, Min(10f)]
    float fontSize = 20f;
    [Tooltip("文字と円全体をワールド座標からずらす画面座標")]
    [SerializeField]
    Vector2 markerScreenOffset = Vector2.zero;
    [Tooltip("文字位置を基準にした○マーカーの位置")]
    [SerializeField]
    Vector2 ringScreenOffset = Vector2.zero;
    [SerializeField]
    Vector2 labelScreenOffset = new Vector2(0f, -50f);
    Transform player;
    Canvas canvas;
    RectTransform marker;
    Image ringTrack;
    Image ringProgress;
    TMP_Text label;
    float heldTime;
    bool transitioning;
    readonly HashSet<Collider> playerContacts = new HashSet<Collider>();
    // コンポーネントの追加・リセット時に使う既定設定を用意します。
    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        BuildUi();
    }

    // トリガー範囲へ入った相手を受け取り、以下の条件に合う場合の処理を行います。
    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        Transform root = other.transform.root;
        if (player == null)
        {
            player = root;
        }

        if (root == player)
        {
            playerContacts.Add(other);
        }
    }

    // トリガー範囲から出た相手に対する処理を行います。
    void OnTriggerExit(Collider other)
    {
        if (player == null || other.transform.root != player)
        {
            return;
        }

        playerContacts.Remove(other);
        if (playerContacts.Count > 0)
        {
            return;
        }

        player = null;
        heldTime = 0f;
        SetVisible(false);
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        playerContacts.Clear();
        player = null;
        heldTime = 0f;
        SetVisible(false);
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (transitioning || player == null)
        {
            SetVisible(false);
            return;
        }

        var camera = viewCamera != null ? viewCamera : Camera.main;
        if (camera == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 anchor = GetComponent<Collider>().bounds.center + markerWorldOffset;
        Vector3 screen = camera.WorldToScreenPoint(anchor);
        if (screen.z <= camera.nearClipPlane)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out var local);
        marker.anchoredPosition = local + markerScreenOffset;
        Vector3 interactionPoint = GetComponent<Collider>().bounds.center;
        interactionPoint.y = player.position.y;
        float distance = Vector3.Distance(player.position, interactionPoint);
        bool closeEnough = distance <= interactionDistance;
        label.text = closeEnough ? destinationLabel : string.Empty;
        var input = GameInputManager.Instance;
        if (closeEnough && input != null && input.PickupHeld)
        {
            heldTime += Time.unscaledDeltaTime;
        }
        else
        {
            heldTime = 0f;
        }

        if (ringProgress != null)
        {
            ringProgress.fillAmount = Mathf.Clamp01(heldTime / holdDuration);
        }

        if (heldTime >= holdDuration)
        {
            Transition();
        }
    }

    void Transition()
    {
        if (transitioning)
        {
            return;
        }

        transitioning = true;
        StageArrivalRequest.Begin(startTimerAfterArrival);
        LoadingSceneRequest.Begin(targetSceneName);
        SceneManager.LoadScene(loadingSceneName);
    }

    void BuildUi()
    {
        var root = new GameObject("Stage Transition Marker Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 460;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        marker = new GameObject("Stage Marker", typeof(RectTransform)).GetComponent<RectTransform>();
        marker.SetParent(root.transform, false);
        marker.anchorMin = marker.anchorMax = marker.pivot = new Vector2(.5f, .5f);
        marker.sizeDelta = Vector2.zero;
        Sprite ringSprite = CreateRingSprite(128, ringThickness);
        ringTrack = CreateRingImage("Ring Track", marker, ringSprite, new Color(ringColor.r, ringColor.g, ringColor.b, Mathf.Max(.35f, ringColor.a * .55f)), false);
        ringProgress = CreateRingImage("Hold Progress", marker, ringSprite, progressColor, true);
        var textRect = new GameObject("Destination Name", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
        textRect.SetParent(marker, false);
        textRect.anchorMin = textRect.anchorMax = textRect.pivot = new Vector2(.5f, .5f);
        textRect.sizeDelta = new Vector2(340f, 40f);
        textRect.anchoredPosition = labelScreenOffset;
        label = textRect.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        SetVisible(false);
    }

    Image CreateRingImage(string objectName, Transform parent, Sprite sprite, Color tint, bool filled)
    {
        RectTransform rect = new GameObject(objectName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = Vector2.one * markerSize;
        rect.anchoredPosition = ringScreenOffset;
        Image image = rect.GetComponent<Image>();
        image.sprite = sprite;
        image.color = tint;
        image.raycastTarget = false;
        if (filled)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 0f;
        }

        return image;
    }

    static Sprite CreateRingSprite(int size, float thickness)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime Stage Marker Ring";
        texture.hideFlags = HideFlags.HideAndDontSave;
        float radius = size * .5f - 1f;
        float inner = Mathf.Max(0f, radius - Mathf.Max(2f, thickness * size / 54f));
        Vector2 center = new Vector2((size - 1) * .5f, (size - 1) * .5f);
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 solid = new Color32(255, 255, 255, 255);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius && distance >= inner ? solid : clear);
            }
        }

        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
    }

    void SetVisible(bool visible)
    {
        if (marker != null && marker.gameObject.activeSelf != visible)
        {
            marker.gameObject.SetActive(visible);
        }
    }

    static bool IsPlayer(Collider other) => other.CompareTag("Player") || other.transform.root.CompareTag("Player") || other.GetComponentInParent<PlayerHealth>() != null;
    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (canvas != null)
        {
            Destroy(canvas.gameObject);
        }
    }
}
