// 【役割】指定したHumanoidボーンに当たり判定などのTransformを追従させる。
using UnityEngine;

/// <summary>FBXのボーン階層を改変せず、Prefab側の当たり判定をHumanoidボーンへ追従させます。</summary>
[DefaultExecutionOrder(100)]
public sealed class HumanoidBoneFollower : MonoBehaviour
{
    [SerializeField]
    Animator animator;
    [SerializeField]
    HumanBodyBones bone;
    [SerializeField]
    Vector3 positionOffset;
    [SerializeField]
    Vector3 rotationOffset;
    Transform target;
    public void Configure(Animator source, HumanBodyBones targetBone, Vector3 offset = default)
    {
        animator = source;
        bone = targetBone;
        positionOffset = offset;
        Resolve();
        Apply();
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        Resolve();
        Apply();
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate() => Apply();
    void Resolve()
    {
        target = animator != null && animator.isHuman ? animator.GetBoneTransform(bone) : null;
    }

    void Apply()
    {
        if (target == null)
        {
            Resolve();
            if (target == null)
            {
                return;
            }
        }

        transform.SetPositionAndRotation(target.TransformPoint(positionOffset), target.rotation * Quaternion.Euler(rotationOffset));
    }
}
