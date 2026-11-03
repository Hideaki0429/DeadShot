// 【役割】射撃入力から弾、マズルフラッシュ、煙などの発射処理を行う。
using System.Collections;
using UnityEngine;

public class OnKeyPress_Throw : MonoBehaviour
{
    [Header("武器選択連携")]
    [Tooltip("空欄なら選択武器を検査しません。handgun / smg などを指定します。")]
    public string requiredWeaponId = "handgun";
    [Tooltip("ONなら攻撃ボタンを押し続けて連射します。")]
    public bool automaticFire;
    [Header("カメラ設定（レイ配信用）")]
    public Camera mainCamera;
    public GameObject newPrefab;
    [Header("弾の発射速度")]
    public float throwVelocity = 20.0f;
    [Header("Hitscan設定")]
    [SerializeField]
    private LayerMask hitLayerMask = ~0;
    [SerializeField]
    private float maxShotDistance = 100f;
    [SerializeField]
    private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
    [Header("演出弾設定")]
    [SerializeField]
    private float tracerSpeed = 80f;
    [Header("デバッグ軌跡表示")]
    [SerializeField]
    private bool showDebugTrajectory = true;
    [SerializeField]
    private float debugTrajectoryDuration = 2f;
    [SerializeField]
    private Color cameraRayColor = Color.cyan;
    [SerializeField]
    private Color muzzleRayColor = Color.yellow;
    [SerializeField]
    private Color hitColor = Color.red;
    [Header("銃口のオフセット位置")]
    public float offsetX = 0f;
    public float offsetY = 1f;
    public float offsetZ = 0.5f;
    public Animator animator;
    public string setupParameter = "Setup";
    [Header("射撃アニメーション")]
    [SerializeField]
    private string shootParameter = "Shoot";
    [Tooltip("移動射撃専用Trigger。空欄なら停止・移動ともShoot Parameterを使用します。")]
    [SerializeField]
    private string movingShootParameter;
    [Tooltip("SpeedまたはDirectionの絶対値がこの値を超えると移動射撃として扱います。")]
    [Min(0f)]
    [SerializeField]
    private float movingShootThreshold = .01f;
    public float cooldownTime = 1.0f;
    public AudioSource audioSource;
    public GameObject effectPrefab;
    public float effectDuration = 2.0f;
    [Tooltip("銃口正面に対するマズルエフェクト固有の角度補正")]
    [SerializeField]
    private Vector3 muzzleEffectLocalEulerAngles;
    [Header("銃本体の射撃演出（入力・命中判定は既存処理）")]
    public M1911Presentation gunPresentation;
    [Header("照準収束・命中精度（銃ごとに調整可能）")]
    [SerializeField]
    private AimFocusController aimFocus;
    [SerializeField, Range(0f, 10f)]
    private float normalSpreadDegrees = 1.2f;
    [SerializeField, Range(0f, 10f)]
    private float focusedSpreadDegrees = 0.05f;
    [Header("完全フォーカス時の補正")]
    [Tooltip("完全収束時の弾拡散倍率。0.05なら通常の5%まで拡散を抑えます。")]
    [Range(0f, 1f)]
    public float fullyFocusedSpreadMultiplier = .05f;
    [Tooltip("完全収束して同じ敵へ命中した場合の通常ダメージ倍率。")]
    [Min(0f)]
    public float fullyFocusedDamageMultiplier = 1.2f;
    [Tooltip("完全収束したヘッドショット時に追加で掛ける倍率。")]
    [Min(0f)]
    public float fullyFocusedHeadshotMultiplier = 1.35f;
    float catalogPower = -1f;
    float catalogStability = 70f;
    float accumulatedRecoilSpread;
    [Header("銃口の煙")]
    [SerializeField]
    private Transform muzzlePoint;
    [SerializeField]
    private GameObject muzzleSmokePrefab;
    [SerializeField]
    private float muzzleSmokeLifetime = 2f;
    [SerializeField]
    private Vector3 muzzleSmokeLocalEulerAngles;
    [SerializeField]
    private float muzzleSmokeScale = 1f;
    [Header("弾数・リロード設定")]
    public int maxAmmo = 6;
    public float reloadTime = 2.0f;
    public string reloadParameter = "Reload";
    [Tooltip("ON: 装填弾が0になったら自動リロード / OFF: リロードキーで手動リロード")]
    public bool automaticReload = true;
    public int currentAmmo;
    bool pushFlag = false;
    bool onCooldown = false;
    public bool isReloading = false;
    [HideInInspector]
    public bool isCameraRestricted = false;
    [HideInInspector]
    public WeaponAmmoSystem ammoSystem; // WeaponAmmoSystemから設定される
    [Header("リロード設定")]
    public GameObject magazineObject;
    public System.Action onReload;
    [Header("【環境負荷スイッチ】")]
    public bool useGravityLoad = true;
    public bool useWindLoad = true;
    public bool useAirResistance = true;
    [Header("【環境負荷の数値設定】")]
    public float customGravity = 9.81f;
    public Vector3 windVelocity = new Vector3(5f, 0f, 0f);
    public float airResistanceCoeff = 0.1f;
    private Vector3 debugCameraStart;
    private Vector3 debugAimPoint;
    private Vector3 debugMuzzleStart;
    private Vector3 debugShotEnd;
    private bool debugDidHit;
    private float debugDisplayUntil;
    // 弾を実際に発射したときだけ通知する
    public event System.Action ShotFired;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        currentAmmo = maxAmmo;
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (aimFocus == null)
        {
            aimFocus = GetComponent<AimFocusController>();
        }

        if (aimFocus == null)
        {
            aimFocus = gameObject.AddComponent<AimFocusController>();
        }
    // ※ ammoSystem の設定は WeaponAmmoSystem.Start() から行われる
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        float stability01 = Mathf.Clamp01(catalogStability / 100f);
        accumulatedRecoilSpread = Mathf.MoveTowards(accumulatedRecoilSpread, 0f, Mathf.Lerp(1f, 8f, stability01) * Time.deltaTime);
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (!GameInputManager.Instance.Gameplay || animator == null)
        {
            return;
        }

        var selectedGoggles = SpecialGogglesController.Instance;
        if (selectedGoggles != null && selectedGoggles.IsShortcutSelected)
        {
            return;
        }

        var selectedWeapon = FindObjectOfType<WeaponSelectionUI>(true);
        if (selectedWeapon != null && selectedWeapon.SelectedWeaponId == "grenade")
        {
            return;
        }

        if (selectedWeapon != null && !string.IsNullOrWhiteSpace(requiredWeaponId) && selectedWeapon.SelectedWeaponId != requiredWeaponId)
        {
            pushFlag = false;
            return;
        }

        if (GameInputManager.Instance.ReloadPressed)
        {
            TryReload();
        }

        bool isSetup = animator.GetBool(setupParameter);
        if (!isSetup || isReloading || isCameraRestricted)
        {
            pushFlag = false;
            return;
        }

        bool isFireKeyPressed = automaticFire ? GameInputManager.Instance.AttackHeld : GameInputManager.Instance.AttackPressed;
        if (isFireKeyPressed && !onCooldown)
        {
            if (automaticFire || pushFlag == false)
            {
                if (currentAmmo <= 0)
                {
                    GameSoundService.Play(GameSound.EmptyGun, transform.position);
                    return;
                }

                if (ammoSystem != null && !ammoSystem.ConsumeRound())
                {
                    currentAmmo = 0;
                    GameSoundService.Play(GameSound.EmptyGun, transform.position);
                    return;
                }

                if (audioSource != null)
                {
                    audioSource.Play();
                }

                pushFlag = !automaticFire;
                if (!TutorialCombatZone.InfiniteAmmo)
                {
                    currentAmmo--;
                }

                // 発射成立をSimpleShootへ通知
                ShotFired?.Invoke();
                PlayShootingAnimation();
                //if (gunPresentation != null) gunPresentation.PlayShot();
                FireHitscan();
                accumulatedRecoilSpread += Mathf.Lerp(1.5f, .05f, stability01);
                if (currentAmmo <= 0 && automaticReload)
                {
                    TryAutomaticReload();
                }
                else
                {
                    StartCoroutine(Cooldown());
                }
            }
        }
        else
        {
            pushFlag = false;
        }
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        StopAllCoroutines();
        isReloading = false;
        onCooldown = false;
        pushFlag = false;
        if (magazineObject != null)
        {
            magazineObject.SetActive(false);
        }
    }

    private void PlayShootingAnimation()
    {
        if (animator == null || string.IsNullOrEmpty(shootParameter))
        {
            return;
        }

        bool isMoving = Mathf.Abs(animator.GetFloat("Speed")) > movingShootThreshold || Mathf.Abs(animator.GetFloat("Direction")) > movingShootThreshold;
        string parameter = isMoving && !string.IsNullOrEmpty(movingShootParameter) ? movingShootParameter : shootParameter;
        // 停止射撃と移動射撃を同時発火させず、フルボディと上半身の重複を防ぐ。
        animator.ResetTrigger(shootParameter);
        if (!string.IsNullOrEmpty(movingShootParameter))
        {
            animator.ResetTrigger(movingShootParameter);
        }

        animator.SetTrigger(parameter);
    }

    private Vector3 GetMuzzlePosition()
    {
        if (muzzlePoint != null)
        {
            return muzzlePoint.position;
        }

        Vector3 offset = new Vector3(offsetX, offsetY, offsetZ);
        return transform.position + transform.rotation * offset;
    }

    public void SetMuzzlePoint(Transform point)
    {
        muzzlePoint = point;
    }

    public void ApplyCatalogParameters(PlayerInventory.WeaponParameters parameters)
    {
        if (parameters == null || !parameters.enabled)
        {
            return;
        }

        catalogPower = Mathf.Max(0, parameters.power);
        catalogStability = Mathf.Clamp(parameters.stability, 0f, 100f);
        float precision01 = Mathf.Clamp01(parameters.precision / 100f);
        normalSpreadDegrees = Mathf.Lerp(4.8f, 0f, precision01);
        focusedSpreadDegrees = Mathf.Lerp(.2f, 0f, precision01);
        cooldownTime = 1f / Mathf.Max(.01f, parameters.rateOfFire);
        reloadTime = 1f / Mathf.Max(.01f, parameters.reloadSpeed);
        maxAmmo = Mathf.Max(1, parameters.ammoCapacity);
        currentAmmo = Mathf.Clamp(currentAmmo, 0, maxAmmo);
        fullyFocusedSpreadMultiplier = Mathf.Clamp01(parameters.focusedSpreadMultiplier);
        fullyFocusedDamageMultiplier = Mathf.Max(0f, parameters.focusedDamageMultiplier);
        fullyFocusedHeadshotMultiplier = Mathf.Max(0f, parameters.focusedHeadshotMultiplier);
    }

    public void SetAimFocus(AimFocusController focus)
    {
        aimFocus = focus;
    }

    public void SetMuzzleEffectLocalEulerAngles(Vector3 eulerAngles)
    {
        muzzleEffectLocalEulerAngles = eulerAngles;
    }

    private void FireHitscan()
    {
        Vector3 muzzlePosition = GetMuzzlePosition();
        Ray cameraRay;
        if (mainCamera != null)
        {
            cameraRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }
        else
        {
            cameraRay = new Ray(muzzlePosition, transform.forward);
        }

        Vector3 aimPoint;
        if (TryGetFirstValidHit(cameraRay, out RaycastHit cameraHit))
        {
            aimPoint = cameraHit.point;
        }
        else
        {
            aimPoint = cameraRay.GetPoint(maxShotDistance);
        }

        Vector3 shotDirection = (aimPoint - muzzlePosition).normalized;
        float focusAmount = aimFocus != null ? aimFocus.FocusAmount : 0f;
        float spread = Mathf.Lerp(normalSpreadDegrees, focusedSpreadDegrees, focusAmount) + accumulatedRecoilSpread;
        if (aimFocus != null && aimFocus.IsFullyFocused)
        {
            spread *= Mathf.Clamp01(fullyFocusedSpreadMultiplier);
        }

        if (spread > 0.001f)
        {
            shotDirection = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * shotDirection;
        }

        Ray muzzleRay = new Ray(muzzlePosition, shotDirection);
        bool didHit = TryGetFirstValidHit(muzzleRay, out RaycastHit shotHit);
        Vector3 shotEnd = didHit ? shotHit.point : muzzleRay.GetPoint(maxShotDistance);
        CreateDebugTrajectory(cameraRay.origin, aimPoint, muzzlePosition, shotEnd, didHit);
        if (newPrefab != null)
        {
            GameObject bulletObject = Instantiate(newPrefab, muzzlePosition, Quaternion.LookRotation(shotDirection));
            OnCollision_DestroyBoth bulletController = bulletObject.GetComponent<OnCollision_DestroyBoth>();
            if (bulletController != null)
            {
                bulletController.InitializeAsHitscanTracer(didHit, shotHit, shotEnd, tracerSpeed, didHit && aimFocus != null && aimFocus.IsFullyFocusedOn(shotHit.collider), requiredWeaponId, catalogPower >= 0f ? Mathf.RoundToInt(catalogPower) : -1, fullyFocusedDamageMultiplier, fullyFocusedHeadshotMultiplier);
            }
            else
            {
                Debug.LogError("[Hitscan] 弾PrefabにOnCollision_DestroyBothがありません。", newPrefab);
                Destroy(bulletObject);
            }
        }

        if (effectPrefab != null && gunPresentation == null)
        {
            Quaternion muzzleForward = muzzlePoint != null ? muzzlePoint.rotation : Quaternion.LookRotation(shotDirection);
            GameObject muzzleEffect = Instantiate(effectPrefab, muzzlePosition, muzzleForward * Quaternion.Euler(muzzleEffectLocalEulerAngles));
            // Play On Awakeだけに任せると高速連射中に初期化フレームが抜けて
            // 表示されない場合があるため、発射ごとに全ParticleSystemを再始動する。
            muzzleEffect.SetActive(true);
            foreach (ParticleSystem particles in muzzleEffect.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.gameObject.SetActive(true);
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Clear(false);
                particles.Play(false);
            }

            Destroy(muzzleEffect, Mathf.Max(0.02f, effectDuration));
        }

        if (muzzleSmokePrefab != null)
        {
            Quaternion smokeRotation = Quaternion.LookRotation(shotDirection) * Quaternion.Euler(muzzleSmokeLocalEulerAngles);
            GameObject smoke = Instantiate(muzzleSmokePrefab, muzzlePosition, smokeRotation);
            smoke.transform.localScale *= Mathf.Max(0.01f, muzzleSmokeScale);
            Destroy(smoke, Mathf.Max(0.1f, muzzleSmokeLifetime));
        }
    }

    private bool TryGetFirstValidHit(Ray ray, out RaycastHit validHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxShotDistance, hitLayerMask, triggerInteraction);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (RaycastHit hit in hits)
        {
            if (IsZombieDetectionCollider(hit.collider))
            {
                Debug.Log($"[Hitscan] 索敵Colliderを無視: {hit.collider.name}", hit.collider);
                continue;
            }

            validHit = hit;
            return true;
        }

        validHit = default;
        return false;
    }

    private bool IsZombieDetectionCollider(Collider targetCollider)
    {
        if (targetCollider == null || !targetCollider.isTrigger || !(targetCollider is SphereCollider))
        {
            return false;
        }

        return targetCollider.GetComponent<EnemyAttack>() != null;
    }

    private void CreateDebugTrajectory(Vector3 cameraStart, Vector3 aimPoint, Vector3 muzzleStart, Vector3 shotEnd, bool didHit)
    {
        if (!showDebugTrajectory)
        {
            return;
        }

        debugCameraStart = cameraStart;
        debugAimPoint = aimPoint;
        debugMuzzleStart = muzzleStart;
        debugShotEnd = shotEnd;
        debugDidHit = didHit;
        debugDisplayUntil = Time.time + debugTrajectoryDuration;
        Debug.DrawLine(cameraStart, aimPoint, cameraRayColor, debugTrajectoryDuration);
        Debug.DrawLine(muzzleStart, shotEnd, muzzleRayColor, debugTrajectoryDuration);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugTrajectory || !Application.isPlaying || Time.time > debugDisplayUntil)
        {
            return;
        }

        Gizmos.color = cameraRayColor;
        Gizmos.DrawLine(debugCameraStart, debugAimPoint);
        Gizmos.color = muzzleRayColor;
        Gizmos.DrawLine(debugMuzzleStart, debugShotEnd);
        Gizmos.color = debugDidHit ? hitColor : muzzleRayColor;
        Gizmos.DrawSphere(debugShotEnd, 0.05f);
    }

    IEnumerator Cooldown()
    {
        onCooldown = true;
        yield return new WaitForSeconds(cooldownTime);
        onCooldown = false;
    }

    public void TryReload()
    {
        // 手動リロードは、現在の装填弾が0のときだけ許可する。
        if (currentAmmo > 0)
        {
            return;
        }

        BeginReload(false);
    }

    void TryAutomaticReload()
    {
        BeginReload(true);
    }

    void BeginReload(bool automatic)
    {
        if (automatic && !automaticReload)
        {
            return;
        }

        if (!automatic && currentAmmo > 0)
        {
            return;
        }

        var flashlight = animator != null ? animator.GetComponent<PlayerFlashlightController>() : null;
        if (flashlight != null && flashlight.IsSelected)
        {
            return;
        }

        var goggles = animator != null ? animator.GetComponent<SpecialGogglesController>() : null;
        if (goggles != null && goggles.IsShortcutSelected)
        {
            return;
        }

        if (isReloading || currentAmmo >= maxAmmo || animator == null)
        {
            return;
        }

        if (ammoSystem != null && !ammoSystem.CanReload())
        {
            return;
        }

        StartCoroutine(Reload(ammoSystem != null));
    }

    IEnumerator Reload(bool useInventory)
    {
        if (ammoSystem != null && !useInventory && !ammoSystem.CanReload())
        {
            isReloading = false;
            yield break;
        }

        if (isReloading)
        {
            yield break;
        }

        int discardedAmmo = currentAmmo;
        isReloading = true;
        animator.SetTrigger(reloadParameter);
        if (magazineObject != null)
        {
            magazineObject.SetActive(true);
        }

        yield return new WaitForSeconds(reloadTime);
        if (magazineObject != null)
        {
            magazineObject.SetActive(false);
        }

        int reloadAmount = ammoSystem != null ? ammoSystem.GetReloadAmount() : maxAmmo;
        if (reloadAmount <= 0)
        {
            isReloading = false;
            yield break;
        }

        currentAmmo = Mathf.Min(maxAmmo, reloadAmount);
        isReloading = false;
        // ✅ currentAmmo = maxAmmo の代入後に発火する
        onReload?.Invoke();
    }
}
