// 【役割】ヘリの飛行経路、移動、着陸と演出の進行を制御する。
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// ヘリコプター自動飛行スクリプト
///
/// 飛行フェーズ:
///   1. TakeOff         … 巡航高度まで上昇し、機首を経由地点へ向ける
///   2. FlyToWaypoint   … 経由地点まで飛行
///   3. HoverAtWaypoint … 指定ボタンが押されるまで位置を保持
///   4. FlyToDestination… 最終目的地まで飛行
///   5. Descend         … エンジンを絞りながら自然降下
///   6. Landed          … 着陸完了イベント発火
/// </summary>
[RequireComponent(typeof(HelicopterController))]
public class HelicopterAutoFlight : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Inspector 設定
    // ──────────────────────────────────────────
    [Header("経由地点")]
    [Tooltip("目的地へ向かう途中でホバリングする地点")]
    public Transform WaypointTarget;
    [Tooltip("経由地点でホバリング中にオンにすると、最終目的地への飛行を再開します")]
    public bool ResumeFlightFromWaypoint;
    [Tooltip("経由地点へ到着したとみなす水平距離 (m)")]
    public float WaypointArrivalDistance = 5f;
    [Header("最終目的地")]
    public Transform DestinationTarget;
    [Tooltip("移動スキップ時に使う、着陸済みの位置と回転")]
    public Transform LandingPoseTarget;
    [Tooltip("着陸後にプレイヤーを出現させる位置と回転")]
    public Transform PlayerSpawnTarget;
    [Header("飛行パラメータ")]
    [Tooltip("巡航高度（World Y座標）")]
    public float CruiseAltitude = 80f;
    [Tooltip("着陸目標高度（目的地TransformのY + この値）")]
    public float LandingHeightOffset = 1.5f;
    [Tooltip("最終目的地へ到着したとみなす水平距離 (m)")]
    public float ArrivalDistance = 5f;
    [Tooltip("高度到達とみなす誤差 (m)")]
    public float AltitudeTolerance = 2f;
    [Tooltip("機首の回転速度 (deg/s)")]
    public float YawSpeed = 90f;
    [Tooltip("機首が対象を向いたとみなす角度誤差 (deg)")]
    public float YawTolerance = 3f;
    [Header("エンジン・移動出力")]
    [Tooltip("自動飛行時の通常エンジン出力")]
    public float AutoEngineForce = 40f;
    [Tooltip("上昇時に加算するエンジン補正量")]
    public float ClimbEngineBoost = 5f;
    [Tooltip("前進時の hMove.y 値（0～1）")]
    [Range(0f, 1f)]
    public float ForwardInput = 0.7f;
    [Header("降下設定")]
    [Tooltip("降下開始から最低エンジン出力になるまでの秒数")]
    public float DescendDuration = 8f;
    [Tooltip("降下中のエンジン出力下限")]
    public float DescendMinEngineForce = 20f;
    [Tooltip("着陸判定タイムアウト（秒）")]
    public float LandingTimeout = 15f;
    [Header("接地判定")]
    [Tooltip("着陸可能な地面のLayer。未変更なら全Layerを対象にします")]
    public LayerMask GroundLayer = ~0;
    [Tooltip("ヘリの重心から下方へ地面を検出する距離")]
    [Min(0.1f)]
    public float GroundCheckDistance = 3f;
    [Tooltip("ヘリのCollider底面と地面の間隔を接地とみなす距離")]
    [Min(0f)]
    public float GroundContactTolerance = 0.2f;
    [Tooltip("接地安定とみなす垂直速度の上限")]
    [Min(0f)]
    public float LandedVelocityLimit = 0.5f;
    [Tooltip("接地と低速度の状態を維持する秒数")]
    [Min(0f)]
    public float LandingStableTime = 1f;
    [Tooltip("着陸完了後にエンジンを切るまでの秒数")]
    public float EngineShutdownDelay = 3f;
    [Header("自動開始")]
    public bool AutoStartOnLoad = true;
    // ──────────────────────────────────────────
    // イベント
    // ──────────────────────────────────────────
    public event Action OnWaypointHoverStarted;
    public event Action OnLandedComplete;
    // ──────────────────────────────────────────
    // 内部状態
    // ──────────────────────────────────────────
    public enum FlightPhase
    {
        Idle,
        TakeOff,
        FlyToWaypoint,
        HoverAtWaypoint,
        FlyToDestination,
        Descend,
        SkipPositioning,
        Landed
    }

    public FlightPhase CurrentPhase { get; private set; } = FlightPhase.Idle;

    private HelicopterController _heliCtrl;
    private Rigidbody _rb;
    private Vector3 _hoverPosition;
    private Quaternion _hoverRotation;
    private bool _landingCompleted;
    private bool _skipPositionPrepared;
    private bool _rigidbodyWasKinematic;
    public bool CanSkipTravel
    {
        get
        {
            return CurrentPhase == FlightPhase.FlyToDestination || CurrentPhase == FlightPhase.Descend;
        }
    }

    // ──────────────────────────────────────────
    // Unity ライフサイクル
    // ──────────────────────────────────────────
    private void Awake()
    {
        _heliCtrl = GetComponent<HelicopterController>();
        _rb = _heliCtrl.HelicopterModel;
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    private void Start()
    {
        if (AutoStartOnLoad)
        {
            StartAutoFlight();
        }
    }

    // 物理演算の一定間隔で呼ばれます。以下の状態更新を行います。
    private void FixedUpdate()
    {
        switch (CurrentPhase)
        {
            case FlightPhase.TakeOff:
                UpdateTakeOff();
                break;
            case FlightPhase.FlyToWaypoint:
                UpdateFlyToWaypoint();
                break;
            case FlightPhase.HoverAtWaypoint:
                UpdateWaypointHover();
                break;
            case FlightPhase.FlyToDestination:
                UpdateFlyToDestination();
                break;
        // DescendはCoroutineで管理
        }
    }

    // ──────────────────────────────────────────
    // 自動飛行開始
    // ──────────────────────────────────────────
    public void StartAutoFlight()
    {
        if (CurrentPhase != FlightPhase.Idle)
        {
            Debug.LogWarning("[AutoFlight] 既に飛行中です。");
            return;
        }

        if (WaypointTarget == null)
        {
            Debug.LogError("[AutoFlight] WaypointTargetが未設定です。");
            return;
        }

        if (DestinationTarget == null)
        {
            Debug.LogError("[AutoFlight] DestinationTargetが未設定です。");
            return;
        }

        if (_rb == null)
        {
            Debug.LogError("[AutoFlight] HelicopterModelが未設定です。");
            return;
        }

        ResumeFlightFromWaypoint = false;
        _heliCtrl.IsAutoFlying = true;
        _heliCtrl.LiftMultiplier = 1f;
        _heliCtrl.EngineForce = AutoEngineForce;
        _heliCtrl.IsOnGround = true;
        _heliCtrl.SetHMove(0f, 0f);
        CurrentPhase = FlightPhase.TakeOff;
        Debug.Log("[AutoFlight] フェーズ: TakeOff");
    }

    /// <summary>
    /// フェード中にヘリを着陸姿勢へ配置します。
    /// この段階では着陸完了イベントを通知しません。
    /// </summary>
    public bool BeginSkipToLandingPose()
    {
        if (!CanSkipTravel || _landingCompleted)
        {
            return false;
        }

        if (LandingPoseTarget == null)
        {
            Debug.LogWarning("[AutoFlight] LandingPoseTargetが未設定です。", this);
            return false;
        }

        if (PlayerSpawnTarget == null)
        {
            Debug.LogError("[AutoFlight] PlayerSpawnTargetが未設定のため、" + "スキップを中止します。", this);
            return false;
        }

        StopAllCoroutines();
        CurrentPhase = FlightPhase.SkipPositioning;
        _skipPositionPrepared = true;
        _rigidbodyWasKinematic = _rb.isKinematic;
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
        ApplyLandingPose();
        _heliCtrl.SetHMove(0f, 0f);
        _heliCtrl.EngineForce = 0f;
        return true;
    }

    /// <summary>
    /// ヘリが指定された着陸Transformに配置されたことを確認してから、
    /// 着陸完了イベントを通知します。
    /// </summary>
    public bool ConfirmSkipLandingAndComplete()
    {
        if (!_skipPositionPrepared || LandingPoseTarget == null)
        {
            return false;
        }

        // 物理同期後にもう一度、指定姿勢を適用します。
        ApplyLandingPose();
        float positionError = Vector3.Distance(_rb.position, LandingPoseTarget.position);
        float rotationError = Quaternion.Angle(_rb.rotation, LandingPoseTarget.rotation);
        if (positionError > 0.05f || rotationError > 1f)
        {
            Debug.LogError("[AutoFlight] ヘリの着陸姿勢を確認できません。" + $" positionError={positionError:F3}, " + $"rotationError={rotationError:F2}", this);
            return false;
        }

        Debug.Log("[AutoFlight] スキップ先へのヘリ配置を確認しました。" + $" position={_rb.position}", this);
        _skipPositionPrepared = false;
        LandingComplete();
        _rb.isKinematic = _rigidbodyWasKinematic;
        return true;
    }

    /// <summary>ゲームオーバー後に、保存済み着陸地点から直接再開します。</summary>
    public bool ForceCompleteAtLandingPose()
    {
        if (LandingPoseTarget == null || PlayerSpawnTarget == null || _rb == null)
        {
            return false;
        }

        StopAllCoroutines();
        bool wasKinematic = _rb.isKinematic;
        _rb.isKinematic = true;
        ApplyLandingPose();
        _heliCtrl.SetHMove(0f, 0f);
        _heliCtrl.EngineForce = 0f;
        LandingComplete();
        _rb.isKinematic = wasKinematic;
        return true;
    }

    private void ApplyLandingPose()
    {
        _rb.position = LandingPoseTarget.position;
        _rb.rotation = LandingPoseTarget.rotation;
        _rb.transform.SetPositionAndRotation(LandingPoseTarget.position, LandingPoseTarget.rotation);
        if (!_rb.isKinematic)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();
    }

    // ──────────────────────────────────────────
    // フェーズ1: 離陸
    // ──────────────────────────────────────────
    private void UpdateTakeOff()
    {
        _heliCtrl.SetHMove(0f, 0f);
        float currentY = _rb.position.y;
        bool reachedAltitude = Mathf.Abs(currentY - CruiseAltitude) < AltitudeTolerance;
        _heliCtrl.EngineForce = reachedAltitude ? AutoEngineForce : AutoEngineForce + ClimbEngineBoost;
        bool facingWaypoint = RotateYawDirect(WaypointTarget.position);
        if (reachedAltitude && facingWaypoint)
        {
            _heliCtrl.IsOnGround = false;
            CurrentPhase = FlightPhase.FlyToWaypoint;
            Debug.Log("[AutoFlight] フェーズ: FlyToWaypoint");
        }
    }

    // ──────────────────────────────────────────
    // フェーズ2: 経由地点へ飛行
    // ──────────────────────────────────────────
    private void UpdateFlyToWaypoint()
    {
        _heliCtrl.EngineForce = AutoEngineForce;
        RotateYawDirect(WaypointTarget.position);
        float horizontalDistance = HorizontalDistance(_rb.position, WaypointTarget.position);
        if (horizontalDistance > WaypointArrivalDistance)
        {
            SetForwardInputForDistance(horizontalDistance);
            return;
        }

        BeginWaypointHover();
    }

    // ──────────────────────────────────────────
    // フェーズ3: 経由地点でホバリング
    // ──────────────────────────────────────────
    private void BeginWaypointHover()
    {
        _heliCtrl.SetHMove(0f, 0f);
        _heliCtrl.EngineForce = AutoEngineForce;
        _hoverPosition = _rb.position;
        _hoverRotation = _rb.rotation;
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        CurrentPhase = FlightPhase.HoverAtWaypoint;
        Debug.Log("[AutoFlight] 経由地点へ到着しました。" + "入力待ちホバリングを開始します。");
        OnWaypointHoverStarted?.Invoke();
    }

    private void UpdateWaypointHover()
    {
        _heliCtrl.SetHMove(0f, 0f);
        _heliCtrl.EngineForce = AutoEngineForce;
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.MovePosition(_hoverPosition);
        _rb.MoveRotation(_hoverRotation);
        if (ResumeFlightFromWaypoint)
        {
            ContinueToDestination();
        }
    }

    private void ContinueToDestination()
    {
        if (CurrentPhase != FlightPhase.HoverAtWaypoint)
        {
            return;
        }

        ResumeFlightFromWaypoint = false;
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        CurrentPhase = FlightPhase.FlyToDestination;
        Debug.Log("[AutoFlight] Inspectorの飛行再開チェックを検出しました。" + "最終目的地への飛行を開始します。");
    }

    // ──────────────────────────────────────────
    // フェーズ4: 最終目的地へ飛行
    // ──────────────────────────────────────────
    private void UpdateFlyToDestination()
    {
        _heliCtrl.EngineForce = AutoEngineForce;
        RotateYawDirect(DestinationTarget.position);
        float horizontalDistance = HorizontalDistance(_rb.position, DestinationTarget.position);
        if (horizontalDistance > ArrivalDistance)
        {
            SetForwardInputForDistance(horizontalDistance);
            return;
        }

        _heliCtrl.SetHMove(0f, 0f);
        CurrentPhase = FlightPhase.Descend;
        Debug.Log("[AutoFlight] フェーズ: Descend");
        StartCoroutine(DescendCoroutine());
    }

    private void SetForwardInputForDistance(float distance)
    {
        float interpolation = Mathf.Clamp01(distance / 20f);
        float forwardInput = Mathf.Lerp(0.1f, ForwardInput, interpolation);
        _heliCtrl.SetHMove(0f, forwardInput);
    }

    // ──────────────────────────────────────────
    // フェーズ5: 降下
    // ──────────────────────────────────────────
    private IEnumerator DescendCoroutine()
    {
        _heliCtrl.SetHMove(0f, 0f);
        float targetY = DestinationTarget.position.y + LandingHeightOffset;
        float startForce = _heliCtrl.EngineForce;
        float elapsed = 0f;
        float stableElapsed = 0f;
        bool timeoutWarningShown = false;
        Debug.Log($"[Descend] 開始 currentY={_rb.position.y:F1} " + $"targetY={targetY:F1}");
        while (true)
        {
            elapsed += Time.fixedDeltaTime;
            float duration = Mathf.Max(DescendDuration, Mathf.Epsilon);
            float interpolation = Mathf.Clamp01(elapsed / duration);
            _heliCtrl.EngineForce = Mathf.Lerp(startForce, DescendMinEngineForce, interpolation);
            bool isGroundedAndStable = IsGroundedAndStable(out float groundDistance);
            if (isGroundedAndStable)
            {
                stableElapsed += Time.fixedDeltaTime;
                if (stableElapsed >= LandingStableTime)
                {
                    Debug.Log("[Descend] 地面への接地と安定を確認。" + $" distance={groundDistance:F2}, " + $"verticalSpeed={Mathf.Abs(_rb.velocity.y):F2}");
                    LandingComplete();
                    yield break;
                }
            }
            else
            {
                stableElapsed = 0f;
            }

            // タイムアウト時もプレイヤーは出現させず、
            // 実際に接地するまで降下と判定を継続します。
            if (!timeoutWarningShown && LandingTimeout > 0f && elapsed >= LandingTimeout)
            {
                timeoutWarningShown = true;
                Debug.LogWarning($"[Descend] {LandingTimeout}秒経過しましたが、" + "接地が確認できるまで降下を継続します。");
            }

            yield return new WaitForFixedUpdate();
        }
    }

    /// <summary>
    /// 自身のColliderを除外して下方の地面を検出し、
    /// 同時にヘリの垂直速度が十分低いか確認します。
    /// </summary>
    private bool IsGroundedAndStable(out float groundDistance)
    {
        groundDistance = float.PositiveInfinity;
        Vector3 rayOrigin = _rb.worldCenterOfMass;
        float colliderBottomY = rayOrigin.y;
        bool foundOwnCollider = false;
        Collider[] ownColliders = _rb.GetComponentsInChildren<Collider>();
        foreach (Collider ownCollider in ownColliders)
        {
            if (ownCollider == null || !ownCollider.enabled || ownCollider.isTrigger || ownCollider.attachedRigidbody != _rb)
            {
                continue;
            }

            colliderBottomY = foundOwnCollider ? Mathf.Min(colliderBottomY, ownCollider.bounds.min.y) : ownCollider.bounds.min.y;
            foundOwnCollider = true;
        }

        float bottomOffset = foundOwnCollider ? Mathf.Max(0f, rayOrigin.y - colliderBottomY) : 0f;
        float rayLength = bottomOffset + GroundCheckDistance;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, rayLength, GroundLayer, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.attachedRigidbody == _rb)
            {
                continue;
            }

            groundDistance = Mathf.Min(groundDistance, hit.distance);
        }

        bool groundDetected = !float.IsPositiveInfinity(groundDistance);
        if (groundDetected && foundOwnCollider)
        {
            // Ray原点からの距離ではなく、
            // ヘリのCollider底面から地面までの隙間へ変換します。
            groundDistance = Mathf.Max(0f, groundDistance - bottomOffset);
        }

        bool isTouchingGround = groundDetected && foundOwnCollider && groundDistance <= GroundContactTolerance;
        bool verticalSpeedIsStable = Mathf.Abs(_rb.velocity.y) <= LandedVelocityLimit;
        return isTouchingGround && verticalSpeedIsStable;
    }

    // ──────────────────────────────────────────
    // フェーズ6: 着陸完了
    // ──────────────────────────────────────────
    private void LandingComplete()
    {
        if (_landingCompleted)
        {
            return;
        }

        _landingCompleted = true;
        CurrentPhase = FlightPhase.Landed;
        _heliCtrl.IsOnGround = true;
        _heliCtrl.IsAutoFlying = false;
        _heliCtrl.LiftMultiplier = 1f;
        _heliCtrl.SetHMove(0f, 0f);
        Debug.Log("[AutoFlight] 着陸完了");
        StartCoroutine(ShutdownEngine());
        OnLandedComplete?.Invoke();
    }

    private IEnumerator ShutdownEngine()
    {
        float startForce = _heliCtrl.EngineForce;
        if (EngineShutdownDelay <= 0f)
        {
            _heliCtrl.EngineForce = 0f;
            Debug.Log("[AutoFlight] エンジン停止");
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < EngineShutdownDelay)
        {
            elapsed += Time.deltaTime;
            _heliCtrl.EngineForce = Mathf.Lerp(startForce, 0f, Mathf.Clamp01(elapsed / EngineShutdownDelay));
            yield return null;
        }

        _heliCtrl.EngineForce = 0f;
        Debug.Log("[AutoFlight] エンジン停止");
    }

    // ──────────────────────────────────────────
    // 回転・距離計算
    // ──────────────────────────────────────────
    private bool RotateYawDirect(Vector3 target)
    {
        float targetAngle = GetTargetYAngle(target);
        float currentAngle = _rb.transform.eulerAngles.y;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, YawSpeed * Time.fixedDeltaTime);
        Vector3 euler = _rb.transform.eulerAngles;
        euler.y = newAngle;
        _rb.transform.eulerAngles = euler;
        return Mathf.Abs(Mathf.DeltaAngle(newAngle, targetAngle)) < YawTolerance;
    }

    private float GetTargetYAngle(Vector3 target)
    {
        Vector3 direction = target - _rb.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
        {
            return _rb.transform.eulerAngles.y;
        }

        return Quaternion.LookRotation(direction).eulerAngles.y;
    }

    private float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    // ──────────────────────────────────────────
    // Scene表示
    // ──────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (WaypointTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(WaypointTarget.position, WaypointArrivalDistance);
            Gizmos.DrawWireSphere(new Vector3(WaypointTarget.position.x, CruiseAltitude, WaypointTarget.position.z), WaypointArrivalDistance);
        }

        if (DestinationTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(DestinationTarget.position, ArrivalDistance);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(new Vector3(DestinationTarget.position.x, CruiseAltitude, DestinationTarget.position.z), ArrivalDistance);
        }
    }
#endif
}
