// 【役割】照準の収束状態とフォーカス中の表示・射撃補正を管理する。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>敵へ照準を留めると縮小し、最大収束時に白くする。</summary>
public sealed class AimFocusController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private Camera aimCamera;
    [SerializeField]
    private OnKeyPress_Throw shooting;
    [SerializeField]
    private SwitchCamera switchCamera;
    [SerializeField]
    private WeaponSelectionUI weaponSelection;
    [SerializeField]
    private Image[] reticles;
    [SerializeField]
    private string targetSpriteName = "Target2";
    [Header("収束設定")]
    [SerializeField, Min(0.05f)]
    private float focusSeconds = 0.25f;
    [SerializeField, Range(0.1f, 1f)]
    private float focusedScale = 0.48f;
    [SerializeField, Min(0f)]
    private float releaseSpeed = 3f;
    [Tooltip("敵の動きで照準が一瞬外れても収束を維持する秒数")]
    [SerializeField, Min(0f)]
    private float targetGraceSeconds = 0.6f;
    [Tooltip("照準Ray周辺も敵へのフォーカス対象にするSphereCast半径")]
    [SerializeField, Min(0f)]
    private float focusAssistRadius = 0.25f;
    [Tooltip("通常時と通常の敵を収束した時の色")]
    [SerializeField]
    private Color focusingColor = Color.white;
    [SerializeField]
    private Color fullyFocusedColor = Color.white;
    [Tooltip("ゴーグル装備中に弱点へ完全収束した時だけ使用")]
    [SerializeField]
    private Color weakPointFocusedColor = Color.red;
    [SerializeField]
    private LayerMask aimLayers = ~0;
    [SerializeField, Min(1f)]
    private float maximumDistance = 100f;
    private readonly List<Vector3> baseScales = new List<Vector3>();
    private readonly List<float> baseAlphas = new List<float>();
    private Collider focusedCollider;
    private EnemyHealth focusedEnemy;
    private bool hasLiveTarget;
    private float focus01;
    private float lastTargetTime = float.NegativeInfinity;
    public bool IsFullyFocused => focus01 >= 0.999f && focusedCollider != null;
    public float FocusAmount => focus01;

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        if (shooting == null)
        {
            shooting = GetComponent<OnKeyPress_Throw>();
        }

        if (switchCamera == null)
        {
            switchCamera = GetComponentInParent<SwitchCamera>();
        }

        if (weaponSelection == null)
        {
            weaponSelection = FindObjectOfType<WeaponSelectionUI>(true);
        }

        if (aimCamera == null && shooting != null)
        {
            aimCamera = shooting.mainCamera;
        }

        ResolveReticles();
        ApplyVisuals();
    }

    public void BindShooting(OnKeyPress_Throw target)
    {
        shooting = target;
        if (switchCamera == null)
        {
            switchCamera = GetComponentInParent<SwitchCamera>();
        }

        if (aimCamera == null && shooting != null)
        {
            aimCamera = shooting.mainCamera;
        }

        ResolveReticles();
        ApplyVisuals();
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (weaponSelection == null)
        {
            weaponSelection = FindObjectOfType<WeaponSelectionUI>(true);
        }

        if (shooting != null && weaponSelection != null && !string.IsNullOrWhiteSpace(shooting.requiredWeaponId) && weaponSelection.SelectedWeaponId != shooting.requiredWeaponId)
        {
            // 他の銃用FocusControllerが同じレティクルを更新するため、
            // 非選択中のコントローラーは表示へ書き込まない。
            focus01 = 0f;
            focusedCollider = null;
            focusedEnemy = null;
            hasLiveTarget = false;
            return;
        }

        aimCamera = ResolveActiveAimCamera();
        bool animatorAiming = shooting != null && shooting.animator != null && shooting.animator.GetBool(shooting.setupParameter);
        bool cameraAiming = switchCamera != null && switchCamera.IsShootingView;
        bool aiming = shooting != null && !shooting.isReloading && (animatorAiming || cameraAiming);
        Collider target = aiming ? FindAimedEnemyPart() : null;
        hasLiveTarget = target != null;
        if (target != null)
        {
            lastTargetTime = Time.unscaledTime;
            EnemyHealth targetEnemy = target.GetComponentInParent<EnemyHealth>();
            // 同じ敵の腕・胸・脚へ照準がまたがっても収束をリセットしない。
            if (targetEnemy != focusedEnemy)
            {
                focusedEnemy = targetEnemy;
                focus01 = 0f;
            }

            focusedCollider = target;
            focus01 = Mathf.MoveTowards(focus01, 1f, Time.deltaTime / Mathf.Max(0.05f, focusSeconds));
        }
        else if (aiming && focusedEnemy != null && Time.unscaledTime - lastTargetTime <= targetGraceSeconds)
        {
            // 歩行・Hitリアクションで敵が一瞬だけ照準から外れても収束を継続する。
            focus01 = Mathf.MoveTowards(focus01, 1f, Time.deltaTime / Mathf.Max(0.05f, focusSeconds));
        }
        else
        {
            focus01 = Mathf.MoveTowards(focus01, 0f, Time.deltaTime * releaseSpeed);
            if (focus01 <= 0f)
            {
                focusedCollider = null;
                focusedEnemy = null;
            }
        }

        if (reticles == null || reticles.Length == 0)
        {
            ResolveReticles();
        }

        ApplyVisuals();
    }

    public bool IsFullyFocusedOn(Collider target)
    {
        if (!IsFullyFocused || target == null || focusedEnemy == null)
        {
            return false;
        }

        return target.GetComponentInParent<EnemyHealth>() == focusedEnemy;
    }

    private Collider FindAimedEnemyPart()
    {
        if (aimCamera == null)
        {
            return null;
        }

        Vector2 screenPoint = GetVisibleReticleScreenPoint();
        Ray ray = aimCamera.ScreenPointToRay(screenPoint);
        RaycastHit[] hits = Physics.RaycastAll(ray, maximumDistance, aimLayers, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        Collider exact = FindFirstEnemyPartOrBlocker(hits);
        if (exact != null)
        {
            return exact;
        }

        if (focusAssistRadius <= 0f)
        {
            return null;
        }

        RaycastHit[] assistedHits = Physics.SphereCastAll(ray, focusAssistRadius, maximumDistance, aimLayers, QueryTriggerInteraction.Collide);
        Array.Sort(assistedHits, (a, b) => a.distance.CompareTo(b.distance));
        return FindFirstEnemyPart(assistedHits);
    }

    private static Collider FindFirstEnemyPartOrBlocker(RaycastHit[] hits)
    {
        foreach (RaycastHit hit in hits)
        {
            if (IsDetectionCollider(hit.collider))
            {
                continue;
            }

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                if (enemy.GetHitPart(hit.collider) != EnemyHealth.HitPart.Unregistered)
                {
                    return hit.collider;
                }

                // 敵ルートの移動用Colliderなどは遮蔽物として扱わない。
                continue;
            }

            return null;
        }

        return null;
    }

    private static Collider FindFirstEnemyPart(RaycastHit[] hits)
    {
        foreach (RaycastHit hit in hits)
        {
            if (IsDetectionCollider(hit.collider))
            {
                continue;
            }

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null && enemy.GetHitPart(hit.collider) != EnemyHealth.HitPart.Unregistered)
            {
                return hit.collider;
            }

            if (enemy != null)
            {
                continue;
            }

            // 壁などの手前から、背後の敵へフォーカスが貫通しないようにする。
            return null;
        }

        return null;
    }

    private static bool IsDetectionCollider(Collider target) => target != null && target.isTrigger && target.GetComponent<EnemyAttack>() != null;
    private Camera ResolveActiveAimCamera()
    {
        if (shooting != null && shooting.mainCamera != null && shooting.mainCamera.enabled)
        {
            return shooting.mainCamera;
        }

        Camera tagged = Camera.main;
        if (tagged != null && tagged.enabled)
        {
            return tagged;
        }

        Camera[] cameras = Camera.allCameras;
        foreach (Camera camera in cameras)
        {
            if (camera != null && camera.enabled && camera.targetTexture == null)
            {
                return camera;
            }
        }

        return aimCamera;
    }

    private Vector2 GetVisibleReticleScreenPoint()
    {
        if (reticles != null)
        {
            foreach (Image image in reticles)
            {
                if (image == null || !image.enabled || !image.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Canvas canvas = image.canvas;
                Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                return RectTransformUtility.WorldToScreenPoint(canvasCamera, image.rectTransform.TransformPoint(image.rectTransform.rect.center));
            }
        }

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private void ResolveReticles()
    {
        List<Image> found = new List<Image>();
        foreach (Image image in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (image == null || !image.gameObject.scene.IsValid() || image.sprite == null)
            {
                continue;
            }

            if (image.sprite.name == targetSpriteName)
            {
                found.Add(image);
            }
        }

        reticles = found.ToArray();
        baseScales.Clear();
        baseAlphas.Clear();
        foreach (Image image in reticles)
        {
            baseScales.Add(image.rectTransform.localScale);
            baseAlphas.Add(image.color.a);
        }
    }

    private void ApplyVisuals()
    {
        if (reticles == null)
        {
            return;
        }

        float scale = Mathf.Lerp(1f, focusedScale, focus01);
        bool weakPointFocused = hasLiveTarget && focus01 >= 0.999f && SpecialGogglesController.IsEquippedGlobally && focusedEnemy != null && focusedEnemy.WeakPoint != null && focusedEnemy.WeakPoint.IsWeakPoint(focusedEnemy.GetHitPart(focusedCollider), focusedCollider);
        Color color = weakPointFocused ? weakPointFocusedColor : focus01 >= 0.999f ? fullyFocusedColor : focusingColor;
        for (int i = 0; i < reticles.Length; i++)
        {
            Image image = reticles[i];
            if (image == null)
            {
                continue;
            }

            Vector3 baseScale = i < baseScales.Count ? baseScales[i] : Vector3.one;
            image.rectTransform.localScale = baseScale * scale;
            color.a = i < baseAlphas.Count ? baseAlphas[i] : image.color.a;
            image.color = color;
        }
    }
}
