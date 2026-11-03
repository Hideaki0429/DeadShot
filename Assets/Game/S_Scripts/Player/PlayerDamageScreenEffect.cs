// 【役割】被ダメージ時の画面エフェクトの強さと表示時間を管理する。
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class PlayerDamageScreenEffect : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private PlayerHealth playerHealth;
    [SerializeField]
    private RawImage redFilter;
    [Header("フィルター色")]
    [SerializeField]
    private Color filterColor = new Color(0.1415f, 0f, 0.0111f, 1f);
    [Header("HPに応じた透明度")]
    [Tooltip("横軸：HP割合 0～1、縦軸：フィルター透明度")]
    [SerializeField]
    private AnimationCurve healthToAlpha = new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.3f, 0.2f), new Keyframe(0.6f, 0.05f), new Keyframe(1f, 0f));
    [Header("Danger時の明滅")]
    [Tooltip("このHP割合以下で明滅を開始")]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float dangerHpThreshold = 0.3f;
    [Tooltip("Danger時の最小透明度")]
    [Range(0f, 1f)]
    [SerializeField]
    private float dangerMinAlpha = 0.25f;
    [Tooltip("Danger時の最大透明度")]
    [Range(0f, 1f)]
    [SerializeField]
    private float dangerMaxAlpha = 0.6f;
    [Tooltip("透明度を上げ下げする速さ")]
    [Min(0.01f)]
    [SerializeField]
    private float dangerPulseSpeed = 2f;
    private bool effectEnabled = true;
    public bool EffectEnabled => effectEnabled;

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        if (redFilter == null)
        {
            redFilter = GetComponent<RawImage>();
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (!effectEnabled)
        {
            return;
        }

        if (playerHealth == null || redFilter == null)
        {
            return;
        }

        float hpRate = playerHealth.MaxHP > 0f ? playerHealth.CurrentHP / playerHealth.MaxHP : 0f;
        hpRate = Mathf.Clamp01(hpRate);
        float targetAlpha;
        if (hpRate > 0f && hpRate <= dangerHpThreshold)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * dangerPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            targetAlpha = Mathf.Lerp(dangerMinAlpha, dangerMaxAlpha, pulse);
        }
        else
        {
            targetAlpha = Mathf.Clamp01(healthToAlpha.Evaluate(hpRate));
        }

        Color currentColor = filterColor;
        currentColor.a = targetAlpha;
        redFilter.color = currentColor;
    }

    /// <summary>
    /// OPなど、HPフィルターを表示しない区間から呼び出します。
    /// 再度有効にすると、次のUpdateから現在HPに応じた表示へ戻ります。
    /// </summary>
    public void SetEffectEnabled(bool enabled)
    {
        effectEnabled = enabled;
        if (redFilter == null)
        {
            redFilter = GetComponent<RawImage>();
        }

        if (!enabled && redFilter != null)
        {
            Color hiddenColor = redFilter.color;
            hiddenColor.a = 0f;
            redFilter.color = hiddenColor;
        }
    }
}
