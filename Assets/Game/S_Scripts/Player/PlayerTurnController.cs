// 【役割】視点と身体の向きの差に応じたターンアニメーションと回転を制御する。
using UnityEngine;

/// <summary>
/// 通常状態で停止中、カメラとPlayerの水平角度差に応じて90/180度ターンを再生します。
/// </summary>
[DefaultExecutionOrder(-60)]
[RequireComponent(typeof(Animator), typeof(Rigidbody))]
public sealed class PlayerTurnController : MonoBehaviour
{
    [Header("ターンモーション使用設定")]
    [Tooltip("OFFの場合、専用ターンを使わずWalks / Runsのまま入力方向へ旋回します。")]
    public bool useTurnAnimations = false;
    [Header("ターン開始角度")]
    [Range(10f, 90f)]
    public float turn90Threshold = 60f;
    [Range(90f, 179f)]
    public float turn180Threshold = 130f;
    [Header("ターン再生時間（クリップ速度調整用）")]
    [Min(.05f)]
    public float turn90Duration = 1f;
    [Min(.05f)]
    public float turn180Duration = 1.67f;
    public bool IsTurning { get; private set; }

    Animator animator;
    Rigidbody body;
    ThirdPersonCameraController cameraController;
    UnityChan.UnityChanControlScriptWithRgidBody movement;
    float finishAt;
    Quaternion targetRotation;
    bool savedRootMotion;
    float rootRotationScale = 1f;
    bool previousMoveInput;
    bool enteredTurnState;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody>();
        cameraController = GetComponent<ThirdPersonCameraController>();
        movement = GetComponent<UnityChan.UnityChanControlScriptWithRgidBody>();
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (!useTurnAnimations)
        {
            if (IsTurning)
            {
                FinishTurn(false);
            }

            previousMoveInput = false;
            return;
        }

        GameInputManager input = GameInputManager.Instance;
        bool hasMoveInput = input != null && input.Move.sqrMagnitude > .001f;
        if (IsTurning)
        {
            if (Time.timeScale == 0f)
            {
                return;
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool inTurnState = IsTurnState(state);
            if (inTurnState)
            {
                enteredTurnState = true;
            }

            // AnimatorのTurn→Locomotion/Idle遷移が完了した時点でRoot Motion制御を終了。
            if (enteredTurnState && !inTurnState && !animator.IsInTransition(0))
            {
                FinishTurn(true);
            }
            else if (Time.time >= finishAt)
            {
                FinishTurn(true);
            }
            else if (!CanContinueTurning())
            {
                FinishTurn(false);
            }

            previousMoveInput = hasMoveInput;
            return;
        }

        // カメラを回しただけではターンしない。移動入力が入った瞬間だけ判定する。
        if (hasMoveInput && !previousMoveInput)
        {
            TryStartTurn(input);
        }

        previousMoveInput = hasMoveInput;
    }

    void TryStartTurn(GameInputManager input)
    {
        if (input == null || !input.Gameplay || movement == null || !movement.canControl || cameraController == null || animator == null || body == null || input.Move.sqrMagnitude <= .001f || animator.GetBool("Setup") || PlayerRollController.Busy || PlayerGrenadeAnimation.Busy || PlayerHealing.Busy || ZombieBiteGrab.PlayerIsGrabbed || animator.GetCurrentAnimatorStateInfo(0).IsTag("PlayerHit"))
        {
            return;
        }

        Vector2 moveInput = Vector2.ClampMagnitude(input.Move, 1f);
        Vector3 desiredDirection = cameraController.PlanarForward * moveInput.y + cameraController.PlanarRight * moveInput.x;
        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude <= .001f)
        {
            return;
        }

        desiredDirection.Normalize();
        Vector3 playerForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float signedAngle = Vector3.SignedAngle(playerForward, desiredDirection, Vector3.up);
        float absoluteAngle = Mathf.Abs(signedAngle);
        if (absoluteAngle < turn90Threshold)
        {
            return;
        }

        bool turnLeft = signedAngle < 0f;
        bool use180 = absoluteAngle >= turn180Threshold;
        string stateName = turnLeft ? (use180 ? "Left180" : "Left90") : (use180 ? "Right180" : "Right90");
        float nominalAngle = use180 ? 180f : 90f;
        float duration = use180 ? turn180Duration : turn90Duration;
        savedRootMotion = animator.applyRootMotion;
        animator.applyRootMotion = true;
        animator.SetFloat("Speed", input.RunHeld ? .8f : .2f);
        animator.SetFloat("Direction", 0f);
        ResetTurnTriggers();
        // ステートへの入り方とブレンド時間はAnimatorのAny State遷移で編集する。
        animator.SetTrigger(stateName);
        // モーションは90/180度を選択するが、最終角度は実際のカメラ方向へ合わせる。
        targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
        rootRotationScale = absoluteAngle / nominalAngle;
        finishAt = Time.time + duration;
        enteredTurnState = false;
        IsTurning = true;
    }

    bool CanContinueTurning()
    {
        GameInputManager input = GameInputManager.Instance;
        return input != null && input.Gameplay && movement != null && movement.canControl && !PlayerRollController.Busy && !PlayerGrenadeAnimation.Busy && !PlayerHealing.Busy && !ZombieBiteGrab.PlayerIsGrabbed && !animator.GetCurrentAnimatorStateInfo(0).IsTag("PlayerHit");
    }

    // クリップ本来の回転量をPlayerのRigidbodyへ適用し、移動Root Motionは捨てる。
    void OnAnimatorMove()
    {
        if (!IsTurning || animator == null || body == null || Time.timeScale == 0f)
        {
            return;
        }

        Quaternion scaledDelta = Quaternion.SlerpUnclamped(Quaternion.identity, animator.deltaRotation, rootRotationScale);
        body.MoveRotation(body.rotation * scaledDelta);
    }

    void FinishTurn(bool complete)
    {
        if (!IsTurning)
        {
            return;
        }

        IsTurning = false;
        // 完走時だけカメラ方向へ正確に揃える。移動割込み時は現在角度を維持する。
        if (complete && body != null)
        {
            body.MoveRotation(targetRotation);
        }

        if (animator != null)
        {
            animator.applyRootMotion = savedRootMotion;
            if (complete)
            {
                // ターン→IdleはAnimatorのExit Time/Transition Durationに任せる。
                animator.SetFloat("Speed", 0f);
                animator.SetFloat("Direction", 0f);
            }
            else
            {
                GameInputManager input = GameInputManager.Instance;
                if (input != null && input.Move.sqrMagnitude > .001f)
                {
                    animator.SetFloat("Speed", input.RunHeld ? .8f : .2f);
                    animator.SetFloat("Direction", 0f);
                }
            }
        }
    }

    void ResetTurnTriggers()
    {
        animator.ResetTrigger("Left90");
        animator.ResetTrigger("Left180");
        animator.ResetTrigger("Right90");
        animator.ResetTrigger("Right180");
    }

    static bool IsTurnState(AnimatorStateInfo state)
    {
        return state.IsName("Base Layer.Left90") || state.IsName("Base Layer.Left180") || state.IsName("Base Layer.Right90") || state.IsName("Base Layer.Right180");
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        previousMoveInput = false;
        FinishTurn(false);
    }
}
