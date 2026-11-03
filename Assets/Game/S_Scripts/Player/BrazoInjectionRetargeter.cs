// 【役割】BRAZOの注射アニメーションをプレイヤーのボーンへ転写する。
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>BRAZOのGeneric腕アニメーションをHumanoidプレイヤーの両腕へ転写する。</summary>
[DefaultExecutionOrder(900)]
public sealed class BrazoInjectionRetargeter : MonoBehaviour
{
    [Header("BRAZO転写元")]
    public GameObject brazoPrefab;
    public AnimationClip brazoClip;
    [Tooltip("Editorで生成した確認・Animator登録用のHumanoidクリップ")]
    public AnimationClip bakedClip;
    [Tooltip("現在の回復アクション中にBRAZO腕動作を重ねる")]
    public bool applyWhileHealing = false;
    [Range(0f, 1f)]
    public float weight = 1f;
    [Min(.1f)]
    public float blendSpeed = 8f;
    [Header("位置・回転補正")]
    [Min(.01f)]
    public float positionScaleMultiplier = 1f;
    public Vector3 leftHandPositionOffset;
    public Vector3 rightHandPositionOffset;
    public Vector3 leftHandRotationOffset;
    public Vector3 rightHandRotationOffset;
    Animator targetAnimator, driverAnimator;
    Transform targetChest, targetLeftUpper, targetLeftLower, targetLeftHand;
    Transform targetRightUpper, targetRightLower, targetRightHand;
    Transform sourceChest, sourceLeftShoulder, sourceLeftElbow, sourceLeftHand;
    Transform sourceRightShoulder, sourceRightElbow, sourceRightHand;
    GameObject driver;
    PlayableGraph graph;
    AnimationClipPlayable playable;
    float currentWeight;
    bool wasActive;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        targetAnimator = GetComponent<Animator>();
        ResolveTargetBones();
        BuildDriver();
    }

    void ResolveTargetBones()
    {
        if (targetAnimator == null || !targetAnimator.isHuman)
        {
            return;
        }

        targetChest = targetAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (targetChest == null)
        {
            targetChest = targetAnimator.GetBoneTransform(HumanBodyBones.Chest);
        }

        targetLeftUpper = targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        targetLeftLower = targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        targetLeftHand = targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        targetRightUpper = targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        targetRightLower = targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        targetRightHand = targetAnimator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    void BuildDriver()
    {
        if (brazoPrefab == null || brazoClip == null || driver != null)
        {
            return;
        }

        driver = Instantiate(brazoPrefab, transform);
        driver.name = "BRAZO Injection Motion Driver";
        driver.transform.localPosition = Vector3.zero;
        driver.transform.localRotation = Quaternion.identity;
        foreach (Renderer renderer in driver.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = false;
        }

        foreach (Collider collider in driver.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        driverAnimator = driver.GetComponent<Animator>();
        if (driverAnimator == null)
        {
            driverAnimator = driver.AddComponent<Animator>();
        }

        driverAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        graph = PlayableGraph.Create("BRAZO Injection Retarget");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        playable = AnimationClipPlayable.Create(graph, brazoClip);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "BRAZO Driver", driverAnimator);
        output.SetSourcePlayable(playable);
        graph.Play();
        playable.SetSpeed(0);
        ResolveSourceBones();
    }

    void ResolveSourceBones()
    {
        if (driver == null)
        {
            return;
        }

        foreach (Transform bone in driver.GetComponentsInChildren<Transform>(true))
        {
            string n = bone.name;
            if (n.EndsWith("Spine2"))
            {
                sourceChest = bone;
            }
            else if (n.EndsWith("LeftArm"))
            {
                sourceLeftShoulder = bone;
            }
            else if (n.EndsWith("LeftForeArm"))
            {
                sourceLeftElbow = bone;
            }
            else if (n.EndsWith("LeftHand"))
            {
                sourceLeftHand = bone;
            }
            else if (n.EndsWith("RightArm"))
            {
                sourceRightShoulder = bone;
            }
            else if (n.EndsWith("RightForeArm") && !n.EndsWith("RightForeArm.001"))
            {
                sourceRightElbow = bone;
            }
            else if (n.EndsWith("RightHand"))
            {
                sourceRightHand = bone;
            }
        }

        // BRAZOで表示用の手に直接つながる前腕は.001側。
        foreach (Transform bone in driver.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name.EndsWith("LeftForeArm.001"))
            {
                sourceLeftElbow = bone;
            }

            if (bone.name.EndsWith("RightForeArm.001"))
            {
                sourceRightElbow = bone;
            }
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        bool active = applyWhileHealing && PlayerHealing.Busy && brazoClip != null;
        if (active && !wasActive)
        {
            playable.SetTime(0);
            playable.SetSpeed(1);
            graph.Evaluate(0);
        }
        else if (!active && wasActive)
        {
            playable.SetSpeed(0);
        }

        wasActive = active;
        currentWeight = Mathf.MoveTowards(currentWeight, active ? weight : 0f, Time.deltaTime * Mathf.Max(.1f, blendSpeed));
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        if (currentWeight <= .0001f || sourceChest == null || targetChest == null)
        {
            return;
        }

        float sourceWidth = sourceLeftShoulder != null && sourceRightShoulder != null ? Vector3.Distance(sourceLeftShoulder.position, sourceRightShoulder.position) : 1f;
        float targetWidth = Vector3.Distance(targetLeftUpper.position, targetRightUpper.position);
        float scale = targetWidth / Mathf.Max(.0001f, sourceWidth) * positionScaleMultiplier;
        Quaternion map = targetChest.rotation * Quaternion.Inverse(sourceChest.rotation);
        ApplyArm(targetLeftUpper, targetLeftLower, targetLeftHand, targetChest.position + map * ((sourceLeftElbow.position - sourceChest.position) * scale), targetChest.position + map * ((sourceLeftHand.position - sourceChest.position) * scale) + targetChest.TransformVector(leftHandPositionOffset), map * sourceLeftHand.rotation * Quaternion.Euler(leftHandRotationOffset), currentWeight);
        ApplyArm(targetRightUpper, targetRightLower, targetRightHand, targetChest.position + map * ((sourceRightElbow.position - sourceChest.position) * scale), targetChest.position + map * ((sourceRightHand.position - sourceChest.position) * scale) + targetChest.TransformVector(rightHandPositionOffset), map * sourceRightHand.rotation * Quaternion.Euler(rightHandRotationOffset), currentWeight);
    }

    static void ApplyArm(Transform upper, Transform lower, Transform hand, Vector3 elbowTarget, Vector3 target, Quaternion rotation, float blend)
    {
        if (upper == null || lower == null || hand == null)
        {
            return;
        }

        Quaternion oldUpper = upper.rotation, oldLower = lower.rotation, oldHand = hand.rotation;
        Vector3 shoulder = upper.position;
        Vector3 currentUpper = lower.position - shoulder;
        Vector3 wantedUpper = elbowTarget - shoulder;
        if (currentUpper.sqrMagnitude > .0001f && wantedUpper.sqrMagnitude > .0001f)
        {
            upper.rotation = Quaternion.FromToRotation(currentUpper, wantedUpper) * upper.rotation;
        }

        Vector3 lowerToHand = hand.position - lower.position;
        Vector3 lowerToTarget = target - lower.position;
        if (lowerToHand.sqrMagnitude > .0001f && lowerToTarget.sqrMagnitude > .0001f)
        {
            lower.rotation = Quaternion.FromToRotation(lowerToHand, lowerToTarget) * lower.rotation;
        }

        hand.rotation = rotation;
        upper.rotation = Quaternion.Slerp(oldUpper, upper.rotation, blend);
        lower.rotation = Quaternion.Slerp(oldLower, lower.rotation, blend);
        hand.rotation = Quaternion.Slerp(oldHand, hand.rotation, blend);
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }
}
