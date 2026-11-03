// 【役割】ピストル構え中の腕・手の姿勢をIKで補正する。
using UnityEngine;

/// <summary>
/// Pistol Idleの手首と銃の姿勢を維持したまま、
/// カメラの上下角度へ左右の上腕・前腕だけを追従させます。
/// </summary>
[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(100)]
public sealed class PlayerPistolAimIK : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private ThirdPersonCameraController thirdPersonCamera;
    [SerializeField]
    private OnKeyPress_Throw weaponController;
    [Header("上下照準")]
    [Tooltip("このカメラPitchをPistol Idleの正面角度として扱います。")]
    [SerializeField]
    private float neutralCameraPitch = 6f;
    [SerializeField]
    private float maximumUpAngle = 35f;
    [SerializeField]
    private float maximumDownAngle = 35f;
    [SerializeField]
    private float blendSpeed = 5f;
    [Header("右腕への角度配分")]
    [Range(0f, 1f)]
    [SerializeField]
    private float rightShoulderPitchWeight = 0f;
    [Range(0f, 1f)]
    [SerializeField]
    private float rightUpperArmPitchWeight = 0.65f;
    [Range(0f, 1f)]
    [SerializeField]
    private float rightLowerArmPitchWeight = 0.35f;
    [Header("左腕への角度配分")]
    [Range(0f, 1f)]
    [SerializeField]
    private float leftShoulderPitchWeight = 0.25f;
    [Range(0f, 1f)]
    [SerializeField]
    private float leftUpperArmPitchWeight = 0.55f;
    [Range(0f, 1f)]
    [SerializeField]
    private float leftLowerArmPitchWeight = 0.2f;
    [Tooltip("1で右腕と同じ方向、-1で反対方向へ追従します。")]
    [SerializeField]
    private float leftPitchDirection = 1f;
    [Header("腕のRotation補正")]
    [SerializeField]
    private Vector3 rightArmRotationOffset = Vector3.zero;
    [SerializeField]
    private Vector3 leftArmRotationOffset = Vector3.zero;
    private Animator animator;
    private Transform rightShoulder;
    private Transform rightUpperArm;
    private Transform rightLowerArm;
    private Transform leftShoulder;
    private Transform leftUpperArm;
    private Transform leftLowerArm;
    private float currentWeight;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (thirdPersonCamera == null)
        {
            thirdPersonCamera = GetComponent<ThirdPersonCameraController>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponentInChildren<OnKeyPress_Throw>(true);
        }

        if (animator != null && animator.isHuman)
        {
            rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        bool setup = animator != null && animator.GetBool("Setup");
        bool reloading = weaponController != null && weaponController.isReloading;
        float targetWeight = setup && !reloading ? 1f : 0f;
        currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, blendSpeed * Time.deltaTime);
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    private void LateUpdate()
    {
        if (PlayerGrenadeAnimation.Busy)
        {
            return;
        }

        if (thirdPersonCamera == null || currentWeight <= 0f)
        {
            return;
        }

        if (rightUpperArm == null || rightLowerArm == null)
        {
            return;
        }

        if (leftUpperArm == null || leftLowerArm == null)
        {
            return;
        }

        float pitchDelta = Mathf.Clamp(thirdPersonCamera.CurrentPitch - neutralCameraPitch, -maximumUpAngle, maximumDownAngle);
        Vector3 rotationAxis = transform.right;
        float leftDirection = Mathf.Approximately(leftPitchDirection, 0f) ? 1f : Mathf.Sign(leftPitchDirection);
        RotateBone(rightShoulder, pitchDelta * rightShoulderPitchWeight, rotationAxis);
        RotateBone(rightUpperArm, pitchDelta * rightUpperArmPitchWeight, rotationAxis);
        RotateBone(rightLowerArm, pitchDelta * rightLowerArmPitchWeight, rotationAxis);
        RotateBone(leftShoulder, pitchDelta * leftShoulderPitchWeight * leftDirection, rotationAxis);
        RotateBone(leftUpperArm, pitchDelta * leftUpperArmPitchWeight * leftDirection, rotationAxis);
        RotateBone(leftLowerArm, pitchDelta * leftLowerArmPitchWeight * leftDirection, rotationAxis);
        // モデル固有の軸ずれをInspectorから補正する。
        rightUpperArm.localRotation *= Quaternion.Euler(rightArmRotationOffset * currentWeight);
        leftUpperArm.localRotation *= Quaternion.Euler(leftArmRotationOffset * currentWeight);
    }

    private void RotateBone(Transform bone, float angle, Vector3 worldAxis)
    {
        if (bone == null || Mathf.Approximately(angle, 0f))
        {
            return;
        }

        bone.rotation = Quaternion.AngleAxis(angle * currentWeight, worldAxis) * bone.rotation;
    }
}
