// 【役割】スタンドライトの左右のSpot Lightに明るさ、照射範囲と位置の設定を反映する。
using UnityEngine;

/// <summary>軍用スタンドライトの左右投光器をまとめて調整する。</summary>
[ExecuteAlways]
public sealed class StandLightRig : MonoBehaviour
{
    [Header("明るさ")]
    [Min(0f)]
    public float intensity = 4f;
    [Min(.1f)]
    public float range = 25f;
    [Range(1f, 179f)]
    public float spotAngle = 95f;
    [Range(0f, 179f)]
    public float innerSpotAngle = 60f;
    public Color lightColor = new Color(1f, .88f, .68f, 1f);
    [Header("影")]
    public LightShadows leftShadows = LightShadows.Soft;
    [Tooltip("負荷を抑えるため、右ライトは初期状態で影なしです。")]
    public LightShadows rightShadows = LightShadows.None;
    [Range(0f, 1f)]
    public float shadowStrength = .8f;
    [Header("ライト位置（モデルのローカル座標）")]
    public Vector3 leftLocalPosition = new Vector3(-.35f, 2.25f, .08f);
    public Vector3 rightLocalPosition = new Vector3(.35f, 2.25f, .08f);
    [Tooltip("Spot Lightが正面を向く角度。モデルの向きに合わせて実行中も調整できます。")]
    public Vector3 localEulerAngles = Vector3.zero;
    [SerializeField]
    Light leftLight;
    [SerializeField]
    Light rightLight;
    // コンポーネントの追加・リセット時に使う既定設定を用意します。
    void Reset()
    {
        EnsureLights();
        ApplySettings();
    }

    // コンポーネントが有効になったときの処理です。
    void OnEnable()
    {
        EnsureLights();
        ApplySettings();
    }

    // Inspectorの値の変更・読み込み時に、設定の検証や反映を行います。
    void OnValidate()
    {
        innerSpotAngle = Mathf.Min(innerSpotAngle, spotAngle);
        EnsureLights();
        ApplySettings();
    }

    public void EnsureLights()
    {
        leftLight = ResolveLight("Light_Left", leftLight, leftLocalPosition);
        rightLight = ResolveLight("Light_Right", rightLight, rightLocalPosition);
    }

    Light ResolveLight(string objectName, Light current, Vector3 localPosition)
    {
        if (current == null)
        {
            Transform child = transform.Find(objectName);
            if (child == null)
            {
                GameObject lightObject = new GameObject(objectName);
                child = lightObject.transform;
                child.SetParent(transform, false);
            }

            current = child.GetComponent<Light>();
            if (current == null)
            {
                current = child.gameObject.AddComponent<Light>();
            }
        }

        current.transform.localPosition = localPosition;
        current.transform.localRotation = Quaternion.Euler(localEulerAngles);
        current.transform.localScale = Vector3.one;
        return current;
    }

    public void ApplySettings()
    {
        Configure(leftLight, leftShadows);
        Configure(rightLight, rightShadows);
    }

    void Configure(Light target, LightShadows shadows)
    {
        if (target == null)
        {
            return;
        }

        target.type = LightType.Spot;
        target.color = lightColor;
        target.intensity = intensity;
        target.range = range;
        target.spotAngle = spotAngle;
        target.innerSpotAngle = innerSpotAngle;
        target.shadows = shadows;
        target.shadowStrength = shadowStrength;
        target.renderMode = LightRenderMode.Auto;
    }
}
