// 【役割】フラッシュライトの選択・装備と照明の点灯状態を管理する。
using UnityEngine;

// Analytic two-bone rig, applied after Animator and the pistol pose correction.
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(Animator))]
public sealed class PlayerFlashlightController : MonoBehaviour
{
    public WeaponSelectionUI selection;
    public UnityChan.UnityChanControlScriptWithRgidBody player;
    public GameObject flashlightObject;
    public Light spotlight;
    [Header("点灯効果音")]
    public AudioClip turnOnSe;
    [Range(0f, 1f)]
    public float seVolume = .7f;
    private AudioSource switchAudioSource;
    [Header("右手の保持位置（Playerローカル座標）")]
    public Vector3 handPosition = new Vector3(.3f, 1.35f, .42f);
    public Vector3 elbowPosition = new Vector3(.65f, 1.05f, .05f);
    public Vector3 handRotation;
    [Header("右手の握り（既存構え姿勢から取得）")]
    public HumanBodyBones[] fingerBones;
    public Quaternion[] fingerRotations;
    [Header("右手へのライト取り付け")]
    public Vector3 gripPosition;
    public Vector3 gripRotation;
    public Vector3 gripScale = Vector3.one;
    [Min(.1f)]
    public float blendSpeed = 5f;
    public bool IsRaised { get; private set; }
    public bool IsSelected => selection != null && selection.SelectedWeaponId == "flashlight";

    Animator animator;
    Transform upper, lower, hand;
    PlayerHealth health;
    float weight;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        animator = GetComponent<Animator>();
        if (player == null)
        {
            player = GetComponent<UnityChan.UnityChanControlScriptWithRgidBody>();
        }

        health = GetComponent<PlayerHealth>();
        if (animator.isHuman)
        {
            upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        SetRaised(false);
    }

    // コンポーネントが有効になったときの処理です。
    void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<PlayerHealth>();
        }

        if (health != null)
        {
            health.Damaged += OnDamaged;
        }
    }

    void OnDamaged()
    {
        SetRaised(false);
    }

    public void SetRaised(bool value)
    {
        bool wasRaised = IsRaised;
        IsRaised = value && IsSelected && upper != null && lower != null && hand != null;
        if (spotlight != null)
        {
            spotlight.enabled = IsRaised;
        }

        if (!wasRaised && IsRaised && turnOnSe != null && isActiveAndEnabled)
        {
            // Dedicated source: do not change the player's footsteps or damage audio settings.
            if (switchAudioSource == null)
            {
                switchAudioSource = gameObject.AddComponent<AudioSource>();
                switchAudioSource.playOnAwake = false;
                switchAudioSource.loop = false;
                switchAudioSource.spatialBlend = 0f;
            }

            switchAudioSource.PlayOneShot(turnOnSe, seVolume);
        }
    }

    private bool healingHidden;
    public void SetHealingHidden(bool hidden)
    {
        healingHidden = hidden;
        if (flashlightObject != null)
        {
            flashlightObject.SetActive(!hidden && IsSelected && weight > .001f);
        }

        if (spotlight != null)
        {
            spotlight.enabled = !hidden && IsRaised;
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        // player.Rifle(Pistol_00)は射撃処理用の常設ルートなので非表示にしない。
        if (player != null && player.Rifle != null && !player.Rifle.activeSelf)
        {
            player.Rifle.SetActive(true);
        }

        if (!IsSelected)
        {
            SetRaised(false);
            weight = 0;
        }

        if (health != null && health.CurrentHP <= 0)
        {
            SetRaised(false);
        }

        var input = GameInputManager.Instance;
        bool reacting = animator.GetCurrentAnimatorStateInfo(0).IsTag("PlayerHit") || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("PlayerHit"));
        if (IsSelected && player != null && player.canControl && input != null && input.Gameplay && input.AimPressed && !reacting)
        {
            SetRaised(!IsRaised);
            Debug.Log("[Flashlight] " + (IsRaised ? "点灯・右腕保持" : "消灯・構え解除"), this);
        }

        weight = Mathf.MoveTowards(weight, IsRaised ? 1 : 0, blendSpeed * Time.deltaTime);
        if (flashlightObject != null)
        {
            flashlightObject.SetActive(!healingHidden && IsSelected && weight > .001f);
            flashlightObject.transform.localPosition = gripPosition;
            flashlightObject.transform.localRotation = Quaternion.Euler(gripRotation);
            flashlightObject.transform.localScale = gripScale;
        }
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        if (PlayerGrenadeAnimation.Busy || healingHidden || weight <= 0 || upper == null || lower == null || hand == null)
        {
            return;
        }

        // Solve at full weight, then blend from this frame's animated pose.
        Quaternion u = upper.rotation, l = lower.rotation, h = hand.rotation;
        Vector3 origin = upper.position;
        Vector3 target = transform.TransformPoint(handPosition);
        float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, hand.position);
        if (a < .0001f || b < .0001f)
        {
            return;
        }

        Vector3 direction = target - origin;
        float d = Mathf.Clamp(direction.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
        direction = direction.sqrMagnitude > .00001f ? direction.normalized : transform.forward;
        Vector3 bend = Vector3.ProjectOnPlane(transform.TransformPoint(elbowPosition) - origin, direction).normalized;
        if (bend.sqrMagnitude < .01f)
        {
            bend = Vector3.Cross(direction, transform.up).normalized;
        }

        float along = (a * a + d * d - b * b) / (2 * d);
        Vector3 elbow = origin + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
        upper.rotation = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, origin + direction * d - lower.position) * lower.rotation;
        Quaternion solvedUpper = upper.rotation, solvedLower = lower.rotation;
        upper.rotation = Quaternion.Slerp(u, solvedUpper, weight);
        lower.rotation = Quaternion.Slerp(l, solvedLower, weight);
        hand.rotation = Quaternion.Slerp(h, transform.rotation * Quaternion.Euler(handRotation), weight);
        if (fingerBones != null && fingerRotations != null)
        {
            for (int i = 0; i < Mathf.Min(fingerBones.Length, fingerRotations.Length); i++)
            {
                var finger = animator.GetBoneTransform(fingerBones[i]);
                if (finger != null)
                {
                    finger.localRotation = Quaternion.Slerp(finger.localRotation, fingerRotations[i], weight);
                }
            }
        }
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
        }

        IsRaised = false;
        weight = 0;
        if (spotlight != null)
        {
            spotlight.enabled = false;
        }

        if (flashlightObject != null)
        {
            flashlightObject.SetActive(false);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.TransformPoint(handPosition), .04f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.TransformPoint(elbowPosition), .04f);
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (switchAudioSource != null)
        {
            Destroy(switchAudioSource);
        }
    }
}
