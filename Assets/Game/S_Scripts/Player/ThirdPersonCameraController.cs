// 【役割】プレイヤーの通常・武器構え・一人称カメラの位置、回転と追従を管理する。
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Playerを中心に回転する、右肩越しの三人称カメラです。
/// Camera Pivotは実行時に生成するため、既存シーンのCamera参照を維持できます。
/// </summary>
public sealed class ThirdPersonCameraController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private Camera controlledCamera;
    [Header("Camera Pivot")]
    [SerializeField]
    private float pivotHeight = 1.55f;
    [SerializeField]
    private float mouseSensitivityX = 2f;
    [SerializeField]
    private float mouseSensitivityY = 2f;
    [SerializeField]
    private float minimumPitch = -25f;
    [SerializeField]
    private float maximumPitch = 45f;
    [Tooltip("開始時の俯角。大きくするほどPlayerを上から見下ろします。")]
    [SerializeField]
    private float initialPitch = 6f;
    [Header("通常時の背面中央・構え時の右肩越し位置")]
    [SerializeField]
    private Vector3 normalOffset = new Vector3(0f, 0.35f, -3.6f);
    [SerializeField]
    private Vector3 aimOffset = new Vector3(0.48f, 0.08f, -0.55f);
    [SerializeField]
    private float positionSmoothTime = 0.08f;
    [Header("構え時のRotation補正")]
    [Tooltip("構え中のみ、マウスで決まる回転へ追加する角度です。")]
    [SerializeField]
    private Vector3 aimRotationOffset = Vector3.zero;
    [Header("通常時の一人称カメラ位置")]
    [Tooltip("通常状態でC切り替えした一人称視点の、Camera Pivotからの位置です。")]
    [FormerlySerializedAs("qViewOffset")]
    [SerializeField]
    private Vector3 normalFirstPersonOffset = new Vector3(0f, 0.08f, 0.28f);
    [Header("武器構え時の一人称カメラ位置")]
    [Tooltip("武器構え状態でC切り替えした一人称視点の、Camera Pivotからの位置です。")]
    [SerializeField]
    private Vector3 aimFirstPersonOffset = new Vector3(0f, 0.08f, 0.28f);
    [Tooltip("Q視点でのみ追加するRotation補正です。")]
    [SerializeField]
    private Vector3 qViewRotationOffset = Vector3.zero;
    [Header("Q切り替え時の照準地点維持")]
    [SerializeField]
    private bool preserveAimPointOnQSwitch = true;
    [SerializeField]
    private LayerMask qViewAimLayers = ~0;
    [Min(1f)]
    [SerializeField]
    private float qViewAimDistance = 100f;
    [Tooltip("Q位置への移動中、C視点の照準地点を維持する最大時間です。")]
    [Min(0f)]
    [SerializeField]
    private float qViewAimLockDuration = 0.35f;
    [Header("カメラの壁回避")]
    [SerializeField]
    private LayerMask obstacleLayers = ~0;
    [SerializeField]
    private float collisionRadius = 0.2f;
    [SerializeField]
    private float collisionPadding = 0.1f;
    [SerializeField]
    private float minimumDistance = 0.3f;
    private readonly RaycastHit[] collisionHits = new RaycastHit[16];
    [Header("被ダメージ時のカメラ反動")]
    [SerializeField]
    private bool damageCameraEnabled = true;
    [Min(0.05f)]
    [SerializeField]
    private float damageDuration = 0.35f;
    [Min(0f)]
    [SerializeField]
    private float damagePitchDegrees = 3f;
    [Min(0f)]
    [SerializeField]
    private float damageRollDegrees = 4f;
    [Min(0f)]
    [SerializeField]
    private float damageShakeDegrees = 0.65f;
    [Min(0f)]
    [SerializeField]
    private float damageShakeFrequency = 18f;
    [Range(0f, 1f)]
    [SerializeField]
    private float firstPersonDamageScale = 0.45f;
    [Range(0f, 2f)]
    [SerializeField]
    private float biteDamageScale = 1.2f;
    private float damageStartedAt = float.NegativeInfinity;
    private Vector3 damageKick;
    private float damageStrength;
    private Quaternion lastDamageRotation = Quaternion.identity;
    [Header("噛みつき専用カメラ")]
    [Tooltip("プレイヤー基準の横位置・高さ・前後位置")]
    [SerializeField]
    private Vector3 biteCameraOffset = new Vector3(1.2f, 1.55f, 0.15f);
    [SerializeField]
    private float biteLookHeight = 1.45f;
    [SerializeField]
    private float biteCameraRoll = -8f;
    [Min(0.01f)]
    [SerializeField]
    private float biteCameraBlendTime = 0.18f;
    private float biteCameraWeight;
    private Vector3 normalCameraPosition;
    private Vector3 previousFollowPosition;
    private Vector3 lastBitePosition;
    private Quaternion lastBiteRotation;
    private Transform cameraPivot;
    private Vector3 cameraVelocity;
    private float yaw;
    private float pitch;
    public float LastManualLookTime { get; private set; }

    private bool isAiming;
    private bool isQView;
    private bool hasSavedAimPoint;
    private Vector3 savedAimPoint;
    private float aimLockStartedAt;
    [Header("自動ターゲットカメラ")]
    [Min(.01f)]
    [SerializeField]
    private float autoTargetRotationSmoothTime = .12f;
    [SerializeField]
    private float autoTargetHorizontalLimit = 50f;
    [SerializeField]
    private float autoTargetVerticalLimit = 28f;
    private Transform autoTarget;
    private float autoTargetYawOffset;
    private float autoTargetPitchOffset;
    private float autoYawVelocity;
    private float autoPitchVelocity;
    private bool autoTargetPersistent;
    private float autoTargetReleaseAt;
    public Vector3 PlanarForward
    {
        get
        {
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            return forward.normalized;
        }
    }

    public Vector3 PlanarRight
    {
        get
        {
            return Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        }
    }

    public Camera ControlledCamera
    {
        get
        {
            return controlledCamera;
        }
    }

    public float CurrentPitch
    {
        get
        {
            return pitch;
        }
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        if (controlledCamera == null)
        {
            controlledCamera = GetComponentInChildren<Camera>(true);
        }

        CreateCameraPivot();
        yaw = transform.eulerAngles.y;
        pitch = Mathf.Clamp(initialPitch, minimumPitch, maximumPitch);
        LastManualLookTime = Time.unscaledTime;
        ApplyCameraImmediately();
        normalCameraPosition = controlledCamera != null ? controlledCamera.transform.position : Vector3.zero;
        previousFollowPosition = transform.position;
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    private void LateUpdate()
    {
        if (PauseMenuController.IsPaused)
        {
            return;
        }

        if (controlledCamera == null || cameraPivot == null)
        {
            return;
        }

        if (autoTarget != null && !autoTargetPersistent)
        {
            Vector2 manualLook = GameInputManager.Instance != null ? GameInputManager.Instance.Look : Vector2.zero;
            if (manualLook.sqrMagnitude > .0001f || Time.unscaledTime >= autoTargetReleaseAt)
            {
                ClearAutoTarget();
            }
        }

        // 監視・ヘリカメラ中は、見えないPlayerカメラを回転させない。
        if (controlledCamera.enabled && !hasSavedAimPoint && GameInputManager.Instance.Gameplay)
        {
            float mouseX = GameInputManager.Instance.Look.x;
            float mouseY = GameInputManager.Instance.Look.y;
            if (Mathf.Abs(mouseX) > .01f || Mathf.Abs(mouseY) > .01f)
            {
                LastManualLookTime = Time.unscaledTime;
            }

            if (autoTarget != null)
            {
                autoTargetYawOffset = Mathf.Clamp(autoTargetYawOffset + mouseX * mouseSensitivityX, -autoTargetHorizontalLimit, autoTargetHorizontalLimit);
                autoTargetPitchOffset = Mathf.Clamp(autoTargetPitchOffset - mouseY * mouseSensitivityY, -autoTargetVerticalLimit, autoTargetVerticalLimit);
                Vector3 look = autoTarget.position - (transform.position + Vector3.up * pivotHeight);
                if (look.sqrMagnitude > .001f)
                {
                    float baseYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                    float basePitch = -Mathf.Asin(Mathf.Clamp(look.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                    yaw = Mathf.SmoothDampAngle(yaw, baseYaw + autoTargetYawOffset, ref autoYawVelocity, autoTargetRotationSmoothTime);
                    pitch = Mathf.SmoothDampAngle(pitch, basePitch + autoTargetPitchOffset, ref autoPitchVelocity, autoTargetRotationSmoothTime);
                }
            }
            else if (!isAiming)
            {
                bool hasMouseInput = Mathf.Abs(mouseX) > 0.01f;
                if (hasMouseInput)
                {
                    yaw += mouseX * mouseSensitivityX;
                }
            // マウス操作を止めても視点をプレイヤー正面へ自動復帰させない。
            // 停止中の向き直りはPlayerTurnControllerの90/180度ターンで行う。
            }
            else
            {
                yaw += mouseX * mouseSensitivityX;
            }

            if (autoTarget == null)
            {
                pitch -= mouseY * mouseSensitivityY;
            }

            pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
        }

        // プレイヤー自身の移動は補間せず同量を反映。
        // 視点オフセット・壁回避・噛みつき画角だけを独立して補間する。
        Vector3 followDelta = transform.position - previousFollowPosition;
        normalCameraPosition += followDelta;
        previousFollowPosition = transform.position;
        cameraPivot.position = transform.position + Vector3.up * pivotHeight;
        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = isQView ? (isAiming ? aimFirstPersonOffset : normalFirstPersonOffset) : isAiming ? aimOffset : normalOffset;
        Vector3 desiredPosition = cameraPivot.TransformPoint(offset);
        Vector3 correctedPosition = ResolveCollision(cameraPivot.position, desiredPosition);
        controlledCamera.transform.position = Vector3.SmoothDamp(normalCameraPosition, correctedPosition, ref cameraVelocity, positionSmoothTime);
        normalCameraPosition = controlledCamera.transform.position;
        Quaternion cameraRotation = cameraPivot.rotation;
        if (isQView && hasSavedAimPoint)
        {
            Vector3 lockedDirection = savedAimPoint - controlledCamera.transform.position;
            if (lockedDirection.sqrMagnitude > 0.0001f)
            {
                cameraRotation = Quaternion.LookRotation(lockedDirection.normalized, Vector3.up);
            }

            bool cameraArrived = Vector3.Distance(controlledCamera.transform.position, correctedPosition) <= 0.01f;
            bool lockTimedOut = Time.unscaledTime - aimLockStartedAt >= qViewAimLockDuration;
            if (cameraArrived || lockTimedOut)
            {
                SynchronizeOrbitAngles(cameraRotation, qViewRotationOffset);
                hasSavedAimPoint = false;
            }
        }
        else if (isAiming || isQView)
        {
            Vector3 rotationOffset = isQView ? qViewRotationOffset : aimRotationOffset;
            cameraRotation *= Quaternion.Euler(rotationOffset);
        }

        var bite = ZombieBiteGrab.Active;
        bool grabbed = bite != null && bite.Victim != null && bite.Victim.transform == transform;
        biteCameraWeight = Mathf.MoveTowards(biteCameraWeight, grabbed ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, biteCameraBlendTime));
        if (grabbed)
        {
            Vector3 focus = Vector3.Lerp(transform.position, bite.transform.position, 0.45f) + Vector3.up * biteLookHeight;
            lastBitePosition = ResolveCollision(focus, transform.TransformPoint(biteCameraOffset), bite.transform);
            Vector3 look = focus - lastBitePosition;
            lastBiteRotation = (look.sqrMagnitude > 0.001f ? Quaternion.LookRotation(look) : cameraRotation) * Quaternion.Euler(0, 0, biteCameraRoll);
        }

        if (biteCameraWeight > 0)
        {
            Vector3 blended = Vector3.Lerp(normalCameraPosition, lastBitePosition, Mathf.SmoothStep(0, 1, biteCameraWeight));
            controlledCamera.transform.position = ResolveCollision(cameraPivot.position, blended, grabbed ? bite.transform : null);
            cameraRotation = Quaternion.Slerp(cameraRotation, lastBiteRotation, Mathf.SmoothStep(0, 1, biteCameraWeight));
        }

        lastDamageRotation = EvaluateDamageRotation();
        controlledCamera.transform.rotation = cameraRotation * lastDamageRotation;
    }

    public void PlayDamageCameraReaction(Vector3 attackerPosition, bool isBite)
    {
        if (!damageCameraEnabled || controlledCamera == null || !controlledCamera.enabled || PauseMenuController.IsPaused || (!GameInputManager.Instance.Gameplay && !ZombieBiteGrab.PlayerIsGrabbed))
        {
            return;
        }

        Quaternion baseRotation = controlledCamera.transform.rotation * Quaternion.Inverse(lastDamageRotation);
        Vector3 direction = Quaternion.Inverse(baseRotation) * (attackerPosition - transform.position).normalized;
        damageKick = new Vector3(-damagePitchDegrees * (direction.z >= 0f ? 1f : -1f), -direction.x * damagePitchDegrees * 0.5f, -direction.x * damageRollDegrees);
        damageStrength = isBite ? biteDamageScale : 1f;
        // 連続被弾は強度を加算せず再開始するため、回転が累積しない。
        damageStartedAt = Time.time;
    }

    private Quaternion EvaluateDamageRotation()
    {
        if (!damageCameraEnabled || !controlledCamera.enabled)
        {
            return Quaternion.identity;
        }

        float t = (Time.time - damageStartedAt) / Mathf.Max(0.05f, damageDuration);
        if (t < 0f || t >= 1f)
        {
            return Quaternion.identity;
        }

        float envelope = (1f - t) * (1f - t);
        float wave = (Time.time - damageStartedAt) * damageShakeFrequency * Mathf.PI * 2f;
        Vector3 shake = new Vector3(Mathf.Sin(wave), Mathf.Sin(wave * 1.37f), Mathf.Sin(wave * 0.83f)) * damageShakeDegrees;
        float scale = isAiming && isQView ? firstPersonDamageScale : 1f;
        return Quaternion.Euler((damageKick + shake) * envelope * damageStrength * scale);
    }

    public void SetViewState(bool aiming, bool qView)
    {
        isAiming = aiming;
        isQView = qView;
        if (!isQView)
        {
            hasSavedAimPoint = false;
        }
    }

    public void SetAutoTarget(Transform target, float duration = -1f)
    {
        if (autoTarget == target && (duration < 0f) == autoTargetPersistent)
        {
            return;
        }

        autoTarget = target;
        autoTargetPersistent = target != null && duration < 0f;
        autoTargetReleaseAt = target != null && duration >= 0f ? Time.unscaledTime + duration : float.PositiveInfinity;
        autoTargetYawOffset = 0f;
        autoTargetPitchOffset = 0f;
        autoYawVelocity = autoPitchVelocity = 0f;
    }

    private void ClearAutoTarget()
    {
        autoTarget = null;
        autoTargetPersistent = false;
        autoYawVelocity = autoPitchVelocity = 0f;
    }

    public void SetNormalMovementFollow(bool enabled)
    {
    // 互換用。通常カメラの自動正面復帰は廃止済み。
    }

    public void NotifyNormalDirectionChanged()
    {
    // 互換用。通常カメラの自動正面復帰は廃止済み。
    }

    public void CaptureCurrentAimPoint()
    {
        if (!preserveAimPointOnQSwitch || controlledCamera == null)
        {
            return;
        }

        // 一時的な反動角度を通常の照準方向として保存しない。
        Quaternion stableRotation = controlledCamera.transform.rotation * Quaternion.Inverse(lastDamageRotation);
        Ray centerRay = new Ray(controlledCamera.transform.position, stableRotation * Vector3.forward);
        RaycastHit[] hits = Physics.RaycastAll(centerRay, qViewAimDistance, qViewAimLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        savedAimPoint = centerRay.GetPoint(qViewAimDistance);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<CameraIgnoredDebris>() != null)
            {
                continue;
            }

            savedAimPoint = hit.point;
            break;
        }

        hasSavedAimPoint = true;
        aimLockStartedAt = Time.unscaledTime;
    }

    private void SynchronizeOrbitAngles(Quaternion finalCameraRotation, Vector3 rotationOffset)
    {
        Quaternion baseRotation = finalCameraRotation * Quaternion.Inverse(Quaternion.Euler(rotationOffset));
        Vector3 forward = baseRotation * Vector3.forward;
        yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
    }

    private void CreateCameraPivot()
    {
        Transform existingPivot = transform.Find("Camera Pivot");
        if (existingPivot != null)
        {
            cameraPivot = existingPivot;
        }
        else
        {
            GameObject pivotObject = new GameObject("Camera Pivot");
            cameraPivot = pivotObject.transform;
            cameraPivot.SetParent(transform, false);
        }

        if (controlledCamera != null)
        {
            controlledCamera.transform.SetParent(cameraPivot, true);
        }
    }

    private void ApplyCameraImmediately()
    {
        if (controlledCamera == null || cameraPivot == null)
        {
            return;
        }

        cameraPivot.position = transform.position + Vector3.up * pivotHeight;
        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
        controlledCamera.transform.position = cameraPivot.TransformPoint(normalOffset);
        controlledCamera.transform.rotation = cameraPivot.rotation;
    }

    private Vector3 ResolveCollision(Vector3 origin, Vector3 desiredPosition, Transform ignoredActor = null)
    {
        Vector3 path = desiredPosition - origin;
        float desiredDistance = path.magnitude;
        if (desiredDistance <= Mathf.Epsilon)
        {
            return desiredPosition;
        }

        Vector3 direction = path / desiredDistance;
        int hitCount = Physics.SphereCastNonAlloc(origin, collisionRadius, direction, collisionHits, desiredDistance, obstacleLayers, QueryTriggerInteraction.Ignore);
        float nearestDistance = desiredDistance;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = collisionHits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hitCollider.GetComponentInParent<CameraIgnoredDebris>() != null)
            {
                continue;
            }

            if (ignoredActor != null && hitCollider.transform.IsChildOf(ignoredActor))
            {
                continue;
            }

            nearestDistance = Mathf.Min(nearestDistance, collisionHits[i].distance);
        }

        if (nearestDistance >= desiredDistance)
        {
            return desiredPosition;
        }

        float safeDistance = Mathf.Max(minimumDistance, nearestDistance - collisionPadding);
        return origin + direction * safeDistance;
    }
}
