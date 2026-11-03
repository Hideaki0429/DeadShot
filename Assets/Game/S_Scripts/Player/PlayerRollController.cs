// 【役割】ローリング入力、アニメーション、移動距離と操作制限を管理する。
using UnityEngine;

/// <summary>Space一回で行う通常時専用の前方ローリング。</summary>
[DefaultExecutionOrder(-50)]
public sealed class PlayerRollController : MonoBehaviour
{
    [Header("ローリング")]
    [Tooltip("ONでRoringクリップのRoot移動・回転をそのまま使用")]
    public bool useOriginalRootMotion = true;
    [Tooltip("元クリップの水平移動量にかける倍率。1で元の距離")]
    [Min(0f)]
    public float rootMotionDistanceScale = 1f;
    [Tooltip("Root MotionをOFFにした場合の代替移動速度")]
    [Min(0f)]
    public float movementSpeed = 1.65f;
    [Min(.01f)]
    public float animationSpeed = 1f;
    [Tooltip("0ならRoringクリップ長から自動計算")]
    [Min(0f)]
    public float durationOverride;
    public AnimationClip roringClip;
    public bool IsRolling { get; private set; }
    public static bool Busy => active != null && active.IsRolling;

    static PlayerRollController active;
    Animator animator;
    Rigidbody body;
    UnityChan.UnityChanControlScriptWithRgidBody movement;
    PlayerHealth health;
    SubmachineGunController submachineGun;
    float finishAt;
    Vector3 direction;
    bool savedRootMotion;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => active = null;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody>();
        movement = GetComponent<UnityChan.UnityChanControlScriptWithRgidBody>();
        health = GetComponent<PlayerHealth>();
        submachineGun = GetComponent<SubmachineGunController>();
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (!IsRolling)
        {
            GameInputManager input = GameInputManager.Instance;
            if (input != null && input.RollPressed)
            {
                TryRoll();
            }

            return;
        }

        if (health == null || health.CurrentHP <= 0 || ZombieBiteGrab.PlayerIsGrabbed || Time.unscaledTime >= finishAt)
        {
            Finish();
        }
    }

    public void TryRoll()
    {
        if (Busy || animator == null || body == null || health == null || health.CurrentHP <= 0 || PlayerGrenadeAnimation.Busy || PlayerHealing.Busy || ZombieBiteGrab.PlayerIsGrabbed || GameInputManager.Instance == null || !GameInputManager.Instance.Gameplay || animator.GetBool("Setup") || (submachineGun != null && submachineGun.IsAiming) || animator.GetCurrentAnimatorStateInfo(0).IsTag("PlayerHit"))
        {
            return;
        }

        direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (direction.sqrMagnitude < .001f)
        {
            direction = Vector3.forward;
        }

        movement?.CancelAimForDamage(false);
        savedRootMotion = animator.applyRootMotion;
        animator.applyRootMotion = useOriginalRootMotion;
        animator.SetFloat("RollAnimationSpeed", Mathf.Max(.01f, animationSpeed));
        animator.ResetTrigger("Roll");
        animator.SetTrigger("Roll");
        float clipDuration = roringClip != null ? roringClip.length / Mathf.Max(.01f, animationSpeed) : .9f;
        finishAt = Time.unscaledTime + (durationOverride > 0f ? durationOverride : clipDuration);
        active = this;
        IsRolling = true;
    }

    // 物理演算の一定間隔で呼ばれます。以下の状態更新を行います。
    void FixedUpdate()
    {
        if (!IsRolling || body == null || Time.timeScale == 0f || useOriginalRootMotion)
        {
            return;
        }

        body.MovePosition(body.position + direction * movementSpeed * Time.fixedDeltaTime);
        if (direction.sqrMagnitude > .001f)
        {
            body.MoveRotation(Quaternion.LookRotation(direction, Vector3.up));
        }
    }

    void Finish()
    {
        if (!IsRolling)
        {
            return;
        }

        IsRolling = false;
        if (active == this)
        {
            active = null;
        }

        if (animator != null)
        {
            animator.applyRootMotion = savedRootMotion;
            animator.ResetTrigger("Roll");
        }
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable() => Finish();
}
