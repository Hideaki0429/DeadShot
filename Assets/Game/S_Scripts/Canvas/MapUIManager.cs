// 【役割】マップ表示とプレイヤー・敵のアイコンを更新する。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraMiniMapUIManager : MonoBehaviour
{
    [Header("参照")]
    public Transform player;
    public Camera miniMapCamera;
    public RectTransform miniMapViewport;
    public RectTransform playerIcon;
    public RectTransform enemyDotPrefab;
    [Header("敵設定")]
    public string enemyTag = "Enemy";
    [Header("カメラ設定")]
    public float cameraHeight = 80f;
    public float viewSize = 150f;
    [Header("更新設定")]
    public float updateInterval = 0.05f;
    [Header("表示設定")]
    public bool hideEnemyOutsideMiniMap = true;
    [Header("Scene確認")]
    public bool showGizmo = true;
    public Color gizmoColor = Color.cyan;
    [Header("索敵範囲表示")]
    [Tooltip("プレイヤーのAuto Target Camera Controllerと同じ索敵半径を使用します。")]
    public bool useAutoTargetRadius = true;
    [Min(1f)]
    public float searchRadius = 18f;
    public Color searchRangeColor = new Color(.35f, .85f, 1f, .18f);
    [Header("索敵範囲内の敵・発見時の点滅")]
    public Color enemyDetectedColor = Color.red;
    [Min(.1f)]
    public float enemyBlinkSpeed = 3f;
    [Range(0f, 1f)]
    public float enemyBlinkMinimumAlpha = .2f;
    private readonly List<RectTransform> enemyDots = new List<RectTransform>();
    private float timer;
    private Image searchRangeImage;
    private AutoTargetCameraController autoTarget;
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        SetupCamera();
        SetupSearchRange();
        UpdateMiniMap();
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            UpdateMiniMap();
        }
    }

    void UpdateMiniMap()
    {
        if (player == null || miniMapCamera == null || miniMapViewport == null || playerIcon == null)
        {
            return;
        }

        UpdateCameraPosition();
        UpdatePlayerIcon();
        UpdateSearchRange();
        UpdateEnemyDots();
        playerIcon.SetAsLastSibling();
    }

    void SetupCamera()
    {
        if (miniMapCamera == null)
        {
            return;
        }

        miniMapCamera.orthographic = true;
        miniMapCamera.orthographicSize = viewSize * 0.5f;
        miniMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void UpdateCameraPosition()
    {
        Vector3 pos = player.position;
        miniMapCamera.transform.position = new Vector3(pos.x, pos.y + cameraHeight, pos.z);
    }

    void UpdatePlayerIcon()
    {
        // プレイヤーは常に中央固定
        playerIcon.anchoredPosition = Vector2.zero;
        // 回転だけ同期
        playerIcon.localEulerAngles = new Vector3(0f, 0f, -player.eulerAngles.y + 180f);
        playerIcon.gameObject.SetActive(true);
    }

    void UpdateEnemyDots()
    {
        GameObject[] taggedEnemies = GameObject.FindGameObjectsWithTag(enemyTag);
        // 腕・脚・攻撃HitBoxなど複数の子にEnemyタグがあっても、
        // EnemyHealth / EnemyAttackの親ルート単位で1体として数える。
        List<Transform> enemies = new List<Transform>();
        HashSet<int> registered = new HashSet<int>();
        foreach (GameObject tagged in taggedEnemies)
        {
            EnemyHealth health = tagged.GetComponentInParent<EnemyHealth>();
            if (health == null)
            {
                health = tagged.GetComponentInChildren<EnemyHealth>();
            }

            if (health != null && health.IsDead)
            {
                continue;
            }

            EnemyAttack attack = health != null ? health.GetComponent<EnemyAttack>() : tagged.GetComponentInParent<EnemyAttack>();
            Transform enemyRoot = health != null ? health.transform : attack != null ? attack.transform : tagged.transform;
            if (registered.Add(enemyRoot.GetInstanceID()))
            {
                enemies.Add(enemyRoot);
            }
        }

        while (enemyDots.Count < enemies.Count)
        {
            RectTransform dot = Instantiate(enemyDotPrefab, miniMapViewport);
            dot.gameObject.SetActive(true);
            enemyDots.Add(dot);
        }

        for (int i = 0; i < enemyDots.Count; i++)
        {
            if (i >= enemies.Count)
            {
                enemyDots[i].gameObject.SetActive(false);
                continue;
            }

            float radius = CurrentSearchRadius();
            Vector3 planar = enemies[i].position - player.position;
            planar.y = 0f;
            if (planar.sqrMagnitude > radius * radius)
            {
                enemyDots[i].gameObject.SetActive(false);
                continue;
            }

            Vector2 pos = WorldToMiniMapPosition(enemies[i].position);
            bool visible = IsInsideMiniMap(pos);
            if (hideEnemyOutsideMiniMap && !visible)
            {
                enemyDots[i].gameObject.SetActive(false);
                continue;
            }

            enemyDots[i].gameObject.SetActive(true);
            enemyDots[i].anchoredPosition = pos;
            // 索敵範囲より前、プレイヤーアイコンより後ろに描画する。
            enemyDots[i].SetAsLastSibling();
            Image dotImage = enemyDots[i].GetComponent<Image>();
            if (dotImage != null)
            {
                dotImage.color = enemyDetectedColor;
            }

            CanvasGroup pulse = enemyDots[i].GetComponent<CanvasGroup>();
            if (pulse == null)
            {
                pulse = enemyDots[i].gameObject.AddComponent<CanvasGroup>();
            }

            // Enemyタグが子Collider側に付いている構成でも、
            // 親ゾンビの発見状態を確実に参照する。
            EnemyAwarenessVisual awareness = enemies[i].GetComponent<EnemyAwarenessVisual>();
            if (awareness == null)
            {
                awareness = enemies[i].GetComponentInChildren<EnemyAwarenessVisual>();
            }

            pulse.alpha = awareness != null && awareness.IsAlerted ? Mathf.Lerp(enemyBlinkMinimumAlpha, 1f, .5f + .5f * Mathf.Sin(Time.unscaledTime * enemyBlinkSpeed * Mathf.PI * 2f)) : 1f;
        }

        playerIcon.SetAsLastSibling();
    }

    void SetupSearchRange()
    {
        if (miniMapViewport == null)
        {
            return;
        }

        Transform existing = miniMapViewport.Find("Player Search Range");
        if (existing != null)
        {
            searchRangeImage = existing.GetComponent<Image>();
        }

        if (searchRangeImage == null)
        {
            GameObject range = new GameObject("Player Search Range", typeof(RectTransform), typeof(Image));
            range.transform.SetParent(miniMapViewport, false);
            searchRangeImage = range.GetComponent<Image>();
            searchRangeImage.sprite = CreateCircleSprite();
            searchRangeImage.raycastTarget = false;
        }

        searchRangeImage.color = searchRangeColor;
        searchRangeImage.rectTransform.anchorMin = searchRangeImage.rectTransform.anchorMax = new Vector2(.5f, .5f);
        searchRangeImage.rectTransform.pivot = new Vector2(.5f, .5f);
        searchRangeImage.rectTransform.anchoredPosition = Vector2.zero;
        PlaceSearchRangeAboveBackground();
        if (autoTarget == null && player != null)
        {
            autoTarget = player.GetComponent<AutoTargetCameraController>();
        }
    }

    void UpdateSearchRange()
    {
        if (searchRangeImage == null || miniMapViewport == null)
        {
            return;
        }

        float diameter = CurrentSearchRadius() / Mathf.Max(1f, viewSize) * Mathf.Min(miniMapViewport.rect.width, miniMapViewport.rect.height) * 2f;
        searchRangeImage.rectTransform.sizeDelta = Vector2.one * diameter;
        searchRangeImage.color = searchRangeColor;
        PlaceSearchRangeAboveBackground();
    }

    void PlaceSearchRangeAboveBackground()
    {
        if (searchRangeImage == null)
        {
            return;
        }

        // SetAsFirstSiblingだとミニマップの不透明背景の裏に隠れる。
        // いったん最前面に出し、UpdateEnemyDots/UpdatePlayerIconで
        // 各アイコンをさらに前へ出すことで描画順を保証する。
        searchRangeImage.transform.SetAsLastSibling();
    }

    float CurrentSearchRadius()
    {
        if (useAutoTargetRadius)
        {
            if (autoTarget == null && player != null)
            {
                autoTarget = player.GetComponent<AutoTargetCameraController>();
            }

            if (autoTarget != null)
            {
                return autoTarget.targetRadius;
            }
        }

        return Mathf.Max(1f, searchRadius);
    }

    static Sprite CreateCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Search Range Circle",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * (size - 1) * .5f;
        float radius = size * .49f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float edge = Mathf.Clamp01(radius - Vector2.Distance(new Vector2(x, y), center));
                pixels[y * size + x] = new Color(1f, 1f, 1f, edge);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100f);
    }

    Vector2 WorldToMiniMapPosition(Vector3 worldPos)
    {
        Vector3 diff = worldPos - player.position;
        float halfView = viewSize * 0.5f;
        float x = diff.x / halfView * (miniMapViewport.rect.width * 0.5f);
        float y = diff.z / halfView * (miniMapViewport.rect.height * 0.5f);
        return new Vector2(x, y);
    }

    bool IsInsideMiniMap(Vector2 uiPos)
    {
        float halfW = miniMapViewport.rect.width * 0.5f;
        float halfH = miniMapViewport.rect.height * 0.5f;
        return uiPos.x >= -halfW && uiPos.x <= halfW && uiPos.y >= -halfH && uiPos.y <= halfH;
    }

    void OnDrawGizmos()
    {
        if (!showGizmo || player == null)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(new Vector3(player.position.x, player.position.y, player.position.z), new Vector3(viewSize, 0.1f, viewSize));
    }
}
