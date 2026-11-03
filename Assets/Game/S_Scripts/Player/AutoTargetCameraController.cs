// 【役割】近くの敵・アイテムを選び、対象マーカー、敵HPと任意のカメラ注視を更新する。
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>範囲内かつ視認可能な最寄りの敵／取得物を自動注視する。</summary>
[DefaultExecutionOrder(600)]
public sealed class AutoTargetCameraController : MonoBehaviour
{
    [Header("対象検索")]
    [Min(1f)]
    public float targetRadius = 18f;
    [Min(.02f)]
    public float refreshInterval = .12f;
    public LayerMask visibilityLayers = ~0;
    public bool targetEnemies = true;
    public bool targetItems = true;
    [Tooltip("構え中、注目している敵を中心にカメラと移動方向を回転させます。OFFでも敵マーカーとHPは表示されます。")]
    public bool enableEnemyOrbitWhileAiming = false;
    [Tooltip("現在の対象がこの倍率以内なら、少し近い別対象が現れても頻繁に切り替えません。")]
    [Range(1f, 2f)]
    public float targetKeepBias = 1.15f;
    [Header("ターゲット矢印")]
    public Sprite targetArrowSprite;
    public Vector2 arrowSize = new Vector2(46f, 34f);
    public Vector2 arrowScreenOffset = new Vector2(0f, 20f);
    [Tooltip("白矢印画像を下向きにする回転角度")]
    public float arrowRotation = 270f;
    [Header("敵体力表示")]
    [Tooltip("注目中の敵の下矢印の下に、現在HP / 最大HPを表示します。")]
    public bool showEnemyHealth = true;
    public Vector2 enemyHealthScreenOffset = new Vector2(0f, -30f);
    public Vector2 enemyHealthTextSize = new Vector2(180f, 36f);
    [Min(1f)]
    public float enemyHealthFontSize = 24f;
    public Color enemyHealthTextColor = Color.white;
    [Tooltip("未設定の場合はTextMeshProの既定フォントを使用します。")]
    public TMP_FontAsset enemyHealthFont;
    public string enemyHealthFormat = "{0} / {1}";
    [Header("アイテム遠距離マーカー")]
    public Vector2 itemCircleSize = new Vector2(32f, 32f);
    [Min(1f)]
    public float itemCircleThickness = 3f;
    ThirdPersonCameraController cameraController;
    Camera gameplayCamera;
    Transform focusPoint;
    Canvas markerCanvas;
    Image markerImage;
    TextMeshProUGUI enemyHealthText;
    Sprite itemCircleSprite;
    Texture2D itemCircleTexture;
    Transform currentRoot;
    EnemyHealth currentEnemy;
    float nextRefresh;
    Animator playerAnimator;
    bool previousAiming;
    bool previousEnemyOrbitWhileAiming;
    Vector3 focusVelocity;
    Vector3 desiredFocusPoint;
    [Min(.01f)]
    public float markerSmoothTime = .06f;
    public Transform CurrentTarget => currentRoot;
    public EnemyHealth CurrentEnemy => currentEnemy;
    public bool HasEnemyTarget => currentEnemy != null;
    public bool IsEnemyOrbitMovementEnabled => enableEnemyOrbitWhileAiming && IsWeaponAiming() && currentEnemy != null;

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        cameraController = GetComponent<ThirdPersonCameraController>();
        playerAnimator = GetComponent<Animator>();
        GameObject point = new GameObject("Auto Target Focus Point");
        focusPoint = point.transform;
        focusPoint.SetParent(transform, false);
        BuildMarker();
        previousEnemyOrbitWhileAiming = enableEnemyOrbitWhileAiming;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (!GameInputManager.Instance || !GameInputManager.Instance.Gameplay)
        {
            SetTarget(null, null);
            return;
        }

        if (gameplayCamera == null || !gameplayCamera.enabled)
        {
            gameplayCamera = ResolveCamera();
        }

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + refreshInterval;
            FindNearestTarget();
        }

        bool aiming = IsWeaponAiming();
        if (aiming != previousAiming || enableEnemyOrbitWhileAiming != previousEnemyOrbitWhileAiming)
        {
            previousAiming = aiming;
            previousEnemyOrbitWhileAiming = enableEnemyOrbitWhileAiming;
            ApplyCameraTarget();
        }

        UpdateFocusPoint();
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        UpdateMarker();
    }

    void FindNearestTarget()
    {
        Transform best = null;
        EnemyHealth bestEnemy = null;
        float bestDistance = targetRadius;
        if (targetEnemies)
        {
            foreach (EnemyHealth enemy in FindObjectsOfType<EnemyHealth>())
            {
                if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float actualDistance = Vector3.Distance(transform.position, enemy.transform.position);
                // 現在の対象を優先する場合も、実距離が索敵範囲を越えたら必ず除外する。
                if (actualDistance > targetRadius)
                {
                    continue;
                }

                float score = enemy.transform == currentRoot ? actualDistance / targetKeepBias : actualDistance;
                if (score > bestDistance || !Visible(enemy.transform, EnemyPoint(enemy)))
                {
                    continue;
                }

                best = enemy.transform;
                bestEnemy = enemy;
                bestDistance = score;
            }
        }

        if (targetItems)
        {
            foreach (InventoryPickup item in InventoryPickup.Active)
            {
                if (item == null || !item.Available)
                {
                    continue;
                }

                float actualDistance = Vector3.Distance(transform.position, item.transform.position);
                if (actualDistance > targetRadius)
                {
                    continue;
                }

                float score = item.transform == currentRoot ? actualDistance / targetKeepBias : actualDistance;
                if (score > bestDistance || !Visible(item.transform, ItemPoint(item.transform)))
                {
                    continue;
                }

                best = item.transform;
                bestEnemy = null;
                bestDistance = score;
            }
        }

        SetTarget(best, bestEnemy);
    }

    bool Visible(Transform root, Vector3 point)
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 path = point - origin;
        if (!Physics.Raycast(origin, path.normalized, out RaycastHit hit, path.magnitude, visibilityLayers, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return hit.transform == root || hit.transform.IsChildOf(root);
    }

    void SetTarget(Transform root, EnemyHealth enemy)
    {
        // Unityで破棄済みObjectは == null になるため、参照そのものを比較する。
        // これにより敵消滅・アイテム取得後もマーカーが残らない。
        if (ReferenceEquals(currentRoot, root) && ReferenceEquals(currentEnemy, enemy))
        {
            return;
        }

        currentRoot = root;
        currentEnemy = enemy;
        ApplyCameraTarget();
        if (enemy != null)
        {
            EnemyAwarenessVisual awareness = enemy.GetComponent<EnemyAwarenessVisual>();
            if (awareness == null)
            {
                awareness = enemy.gameObject.AddComponent<EnemyAwarenessVisual>();
            }

            awareness.MarkDiscovered();
        }

        if (markerImage != null)
        {
            markerImage.gameObject.SetActive(root != null);
        }

        if (enemyHealthText != null)
        {
            enemyHealthText.gameObject.SetActive(root != null && enemy != null && showEnemyHealth);
        }
    }

    void ApplyCameraTarget()
    {
        bool orbit = enableEnemyOrbitWhileAiming && IsWeaponAiming() && currentEnemy != null;
        cameraController?.SetAutoTarget(orbit ? focusPoint : null, orbit ? -1f : 0f);
    }

    void UpdateFocusPoint()
    {
        if (currentRoot == null || !currentRoot.gameObject.activeInHierarchy || (currentEnemy != null && currentEnemy.IsDead))
        {
            SetTarget(null, null);
            return;
        }

        desiredFocusPoint = currentEnemy != null ? EnemyPoint(currentEnemy) : ItemPoint(currentRoot);
        focusPoint.position = Vector3.SmoothDamp(focusPoint.position, desiredFocusPoint, ref focusVelocity, markerSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    void UpdateMarker()
    {
        if (currentRoot == null || markerImage == null || gameplayCamera == null)
        {
            if (enemyHealthText != null)
            {
                enemyHealthText.gameObject.SetActive(false);
            }

            return;
        }

        // 矢印はプレイヤーや画面上の前回位置から移動させず、
        // 選ばれた対象の現在座標から毎フレーム直接表示する。
        InventoryPickup item = currentEnemy == null ? currentRoot.GetComponent<InventoryPickup>() : null;
        // アイテムは取得可能距離に入ったら、ItemPickupMarkerUI 側の
        // 下矢印＋入力アイコンに引き継ぐ。ここでは遠距離の○だけを表示する。
        if (item != null && item.InRange(transform))
        {
            markerImage.gameObject.SetActive(false);
            if (enemyHealthText != null)
            {
                enemyHealthText.gameObject.SetActive(false);
            }

            return;
        }

        bool enemyMarker = currentEnemy != null;
        markerImage.sprite = enemyMarker ? targetArrowSprite : itemCircleSprite;
        markerImage.rectTransform.sizeDelta = enemyMarker ? arrowSize : itemCircleSize;
        markerImage.rectTransform.localRotation = enemyMarker ? Quaternion.Euler(0f, 0f, arrowRotation) : Quaternion.identity;
        Vector3 markerWorldPoint = enemyMarker ? EnemyPoint(currentEnemy) : ItemPoint(currentRoot);
        Vector3 screen = gameplayCamera.WorldToScreenPoint(markerWorldPoint);
        bool visible = screen.z > 0f;
        markerImage.gameObject.SetActive(visible);
        bool showHealth = visible && enemyMarker && showEnemyHealth && !currentEnemy.IsDead;
        if (enemyHealthText != null)
        {
            enemyHealthText.gameObject.SetActive(showHealth);
        }

        if (visible)
        {
            markerImage.rectTransform.position = (Vector2)screen + arrowScreenOffset;
            if (showHealth)
            {
                enemyHealthText.rectTransform.position = (Vector2)screen + arrowScreenOffset + enemyHealthScreenOffset;
                enemyHealthText.rectTransform.sizeDelta = enemyHealthTextSize;
                enemyHealthText.fontSize = enemyHealthFontSize;
                enemyHealthText.color = enemyHealthTextColor;
                if (enemyHealthFont != null && enemyHealthText.font != enemyHealthFont)
                {
                    enemyHealthText.font = enemyHealthFont;
                }

                string format = string.IsNullOrWhiteSpace(enemyHealthFormat) ? "{0} / {1}" : enemyHealthFormat;
                enemyHealthText.text = string.Format(format, Mathf.Max(0, currentEnemy.currentHP), Mathf.Max(1, currentEnemy.maxHP));
            }
        }
    }

    bool IsWeaponAiming() => playerAnimator != null && playerAnimator.GetBool("Setup");
    static Vector3 EnemyPoint(EnemyHealth enemy)
    {
        Animator animator = enemy.GetComponentInChildren<Animator>();
        Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        return head != null ? head.position + Vector3.up * .35f : RendererTop(enemy.transform, .35f);
    }

    static Vector3 ItemPoint(Transform item) => RendererTop(item, .2f);
    static Vector3 RendererTop(Transform root, float padding)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return root.position + Vector3.up * padding;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return new Vector3(bounds.center.x, bounds.max.y + padding, bounds.center.z);
    }

    void BuildMarker()
    {
        GameObject canvasObject = new GameObject("Auto Target Marker Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        markerCanvas = canvasObject.GetComponent<Canvas>();
        markerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        markerCanvas.sortingOrder = 150;
        GameObject imageObject = new GameObject("Target Down Arrow", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        markerImage = imageObject.GetComponent<Image>();
        markerImage.sprite = targetArrowSprite;
        markerImage.color = Color.white;
        markerImage.preserveAspect = true;
        markerImage.raycastTarget = false;
        markerImage.rectTransform.sizeDelta = arrowSize;
        markerImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, arrowRotation);
        markerImage.gameObject.SetActive(false);
        GameObject healthObject = new GameObject("Enemy Health", typeof(RectTransform), typeof(TextMeshProUGUI));
        healthObject.transform.SetParent(canvasObject.transform, false);
        enemyHealthText = healthObject.GetComponent<TextMeshProUGUI>();
        enemyHealthText.alignment = TextAlignmentOptions.Center;
        enemyHealthText.raycastTarget = false;
        enemyHealthText.enableWordWrapping = false;
        enemyHealthText.fontSize = enemyHealthFontSize;
        enemyHealthText.color = enemyHealthTextColor;
        enemyHealthText.rectTransform.sizeDelta = enemyHealthTextSize;
        if (enemyHealthFont != null)
        {
            enemyHealthText.font = enemyHealthFont;
        }

        enemyHealthText.gameObject.SetActive(false);
        BuildItemCircleSprite();
    }

    void BuildItemCircleSprite()
    {
        const int resolution = 128;
        itemCircleTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        itemCircleTexture.name = "Auto Target Item Circle";
        itemCircleTexture.wrapMode = TextureWrapMode.Clamp;
        float outer = 61f;
        float inner = outer - Mathf.Clamp(itemCircleThickness, 1f, 16f) / Mathf.Max(1f, itemCircleSize.x) * resolution;
        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float distance = new Vector2(x - 63.5f, y - 63.5f).magnitude;
                float alpha = Mathf.Clamp01(outer + .5f - distance) * Mathf.Clamp01(distance - inner + .5f);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        itemCircleTexture.SetPixels(pixels);
        itemCircleTexture.Apply();
        itemCircleSprite = Sprite.Create(itemCircleTexture, new Rect(0, 0, resolution, resolution), new Vector2(.5f, .5f));
    }

    Camera ResolveCamera()
    {
        if (cameraController != null && cameraController.ControlledCamera != null)
        {
            return cameraController.ControlledCamera;
        }

        return Camera.main;
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (markerCanvas != null)
        {
            Destroy(markerCanvas.gameObject);
        }

        if (itemCircleSprite != null)
        {
            Destroy(itemCircleSprite);
        }

        if (itemCircleTexture != null)
        {
            Destroy(itemCircleTexture);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, targetRadius);
    }
}
