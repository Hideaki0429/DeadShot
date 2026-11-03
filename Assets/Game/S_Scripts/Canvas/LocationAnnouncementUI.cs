// 【役割】エリア到着時の場所名を発光・フェード付きで表示する。
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
public sealed class LocationAnnouncementUI : MonoBehaviour
{
    public static LocationAnnouncementUI Instance { get; private set; }

    [Header("Hierarchy UI References")]
    [SerializeField]
    CanvasGroup announcementGroup;
    [SerializeField]
    TextMeshProUGUI locationText;
    [SerializeField]
    TextMeshProUGUI glowText;
    [SerializeField]
    Image upperLine;
    [SerializeField]
    Image lowerLine;
    [SerializeField]
    CanvasGroup blackFadeGroup;
    [Header("フェード演出")]
    [Min(0f)]
    public float fadeInDuration = .55f;
    [Min(0f)]
    public float visibleDuration = 2.2f;
    [Min(0f)]
    public float fadeOutDuration = .8f;
    [Min(0f)]
    public float landingBlackFadeIn = .22f;
    [Min(0f)]
    public float landingBlackHold = .12f;
    [Min(0f)]
    public float landingBlackFadeOut = .4f;
    [Header("文字と発光")]
    public Color textColor = Color.white;
    [ColorUsage(true, true)]
    public Color glowColor = new Color(.45f, .8f, 1f, .65f);
    [Range(0f, 3f)]
    public float glowIntensity = 1f;
    [Range(1f, 1.2f)]
    public float glowScale = 1.035f;
    public Color lineColor = new Color(.55f, .8f, 1f, .65f);
    Coroutine running;
    public bool IsShowing => running != null;

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ApplyAppearance();
        SetAlpha(announcementGroup, 0f, false);
        SetAlpha(blackFadeGroup, 0f, false);
    }

    // Inspectorの値の変更・読み込み時に、設定の検証や反映を行います。
    void OnValidate()
    {
        ApplyAppearance();
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Show(string locationName)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (running != null)
        {
            StopCoroutine(running);
        }

        running = StartCoroutine(ShowRoutine(locationName));
    }

    public IEnumerator PlayLandingSequence(string locationName, Action onAnnouncementVisible)
    {
        if (running != null)
        {
            StopCoroutine(running);
        }

        SetAlpha(announcementGroup, 0f, false);
        yield return Fade(blackFadeGroup, 0f, 1f, landingBlackFadeIn, true);
        if (landingBlackHold > 0f)
        {
            yield return new WaitForSecondsRealtime(landingBlackHold);
        }

        SetLocationName(locationName);
        SetAlpha(announcementGroup, 1f, true);
        onAnnouncementVisible?.Invoke();
        yield return Fade(blackFadeGroup, 1f, 0f, landingBlackFadeOut, false);
        if (visibleDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(visibleDuration);
        }

        yield return Fade(announcementGroup, 1f, 0f, fadeOutDuration, false);
    }

    IEnumerator ShowRoutine(string locationName)
    {
        SetLocationName(locationName);
        yield return Fade(announcementGroup, 0f, 1f, fadeInDuration, true);
        if (visibleDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(visibleDuration);
        }

        yield return Fade(announcementGroup, 1f, 0f, fadeOutDuration, false);
        running = null;
    }

    void SetLocationName(string value)
    {
        if (locationText != null)
        {
            locationText.text = value;
        }

        if (glowText != null)
        {
            glowText.text = value;
        }

        ApplyAppearance();
    }

    void ApplyAppearance()
    {
        if (locationText != null)
        {
            locationText.color = textColor;
        }

        if (glowText != null)
        {
            Color color = glowColor;
            color.a = Mathf.Clamp01(glowColor.a * glowIntensity);
            glowText.color = color;
            glowText.rectTransform.localScale = Vector3.one * glowScale;
        }

        if (upperLine != null)
        {
            upperLine.color = lineColor;
        }

        if (lowerLine != null)
        {
            lowerLine.color = lineColor;
        }
    }

    static IEnumerator Fade(CanvasGroup group, float from, float to, float duration, bool active)
    {
        if (group == null)
        {
            yield break;
        }

        group.gameObject.SetActive(true);
        group.alpha = from;
        if (duration <= 0f)
        {
            group.alpha = to;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
        }

        group.alpha = to;
        group.blocksRaycasts = false;
        group.interactable = false;
        if (!active && to <= 0f)
        {
            group.gameObject.SetActive(false);
        }
    }

    static void SetAlpha(CanvasGroup group, float alpha, bool active)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = alpha;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.gameObject.SetActive(active);
    }
}
