// 【役割】敵の発見状態をミニマップや目の発光などの表示へ反映する。
using UnityEngine;

/// <summary>プレイヤーによる発見状態、AI警戒状態、目の赤色ライトを管理する。</summary>
public sealed class EnemyAwarenessVisual : MonoBehaviour
{
    public bool IsDiscovered { get; private set; }
    public bool IsAlerted { get; private set; }

    [Header("赤い眼光")]
    public Color eyeColor = Color.red;
    [Min(0f)]
    public float eyeIntensity = 2.5f;
    [Min(.01f)]
    public float eyeRange = .55f;
    public Vector3 leftEyeLocalPosition = new Vector3(-.035f, .035f, .09f);
    public Vector3 rightEyeLocalPosition = new Vector3(.035f, .035f, .09f);
    Light leftEye, rightEye;
    EnemyAttack attack;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        attack = GetComponent<EnemyAttack>();
        BuildEyes();
        SetEyeState(false);
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        bool alerted = attack != null && attack.isChasing;
        if (alerted != IsAlerted)
        {
            IsAlerted = alerted;
            SetEyeState(alerted);
        }
    }

    public void MarkDiscovered() => IsDiscovered = true;
    void BuildEyes()
    {
        Animator animator = GetComponentInChildren<Animator>();
        Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head == null)
        {
            head = transform;
        }

        leftEye = CreateEye("Left Eye Alert Light", head, leftEyeLocalPosition);
        rightEye = CreateEye("Right Eye Alert Light", head, rightEyeLocalPosition);
    }

    Light CreateEye(string name, Transform parent, Vector3 localPosition)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        Light light = go.GetComponent<Light>();
        if (light == null)
        {
            light = go.AddComponent<Light>();
        }

        light.type = LightType.Point;
        light.color = eyeColor;
        light.intensity = eyeIntensity;
        light.range = eyeRange;
        light.shadows = LightShadows.None;
        return light;
    }

    void SetEyeState(bool visible)
    {
        if (leftEye != null)
        {
            leftEye.enabled = visible;
        }

        if (rightEye != null)
        {
            rightEye.enabled = visible;
        }
    }
}
