// 【役割】リザルトのランクを段階的に表示する演出を制御する。
using System.Collections;
using TMPro;
using UnityEngine;

public class BioStyleRankEffect : MonoBehaviour
{
    [Header("--- References ---")]
    [SerializeField]
    private TextMeshProUGUI rankText;
    [SerializeField]
    private CanvasGroup rankCanvasGroup;
    [Header("--- Bio Style Effect ---")]
    [SerializeField]
    private float appearTime = 0.35f;
    [SerializeField]
    private float startScale = 3.0f;
    [SerializeField]
    private float overshootScale = 1.25f;
    [SerializeField]
    private float finalScale = 1.0f;
    [SerializeField]
    private float shakeDuration = 0.25f;
    [SerializeField]
    private float shakePower = 12f;
    [SerializeField]
    private int flashCount = 3;
    [SerializeField]
    private float flashInterval = 0.06f;
    private Vector3 defaultPosition;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        if (rankText == null)
        {
            rankText = GetComponent<TextMeshProUGUI>();
        }

        if (rankCanvasGroup == null)
        {
            rankCanvasGroup = GetComponent<CanvasGroup>();
        }

        defaultPosition = transform.localPosition;
    }

    public IEnumerator PlayEffect()
    {
        if (rankText == null)
        {
            yield break;
        }

        transform.localScale = Vector3.one * startScale;
        if (rankCanvasGroup != null)
        {
            rankCanvasGroup.alpha = 0f;
        }

        yield return ScaleAppear();
        yield return Shake();
        yield return Flash();
        transform.localPosition = defaultPosition;
        transform.localScale = Vector3.one * finalScale;
        if (rankCanvasGroup != null)
        {
            rankCanvasGroup.alpha = 1f;
        }
    }

    private IEnumerator ScaleAppear()
    {
        float timer = 0f;
        while (timer < appearTime)
        {
            timer += Time.deltaTime;
            float rate = Mathf.Clamp01(timer / appearTime);
            float scale;
            if (rate < 0.7f)
            {
                scale = Mathf.Lerp(startScale, overshootScale, rate / 0.7f);
            }
            else
            {
                scale = Mathf.Lerp(overshootScale, finalScale, (rate - 0.7f) / 0.3f);
            }

            transform.localScale = Vector3.one * scale;
            if (rankCanvasGroup != null)
            {
                rankCanvasGroup.alpha = rate;
            }

            yield return null;
        }
    }

    private IEnumerator Shake()
    {
        float timer = 0f;
        while (timer < shakeDuration)
        {
            timer += Time.deltaTime;
            float x = Random.Range(-shakePower, shakePower);
            float y = Random.Range(-shakePower, shakePower);
            transform.localPosition = defaultPosition + new Vector3(x, y, 0f);
            yield return null;
        }

        transform.localPosition = defaultPosition;
    }

    private IEnumerator Flash()
    {
        for (int i = 0; i < flashCount; i++)
        {
            if (rankCanvasGroup != null)
            {
                rankCanvasGroup.alpha = 0.25f;
            }

            yield return new WaitForSeconds(flashInterval);
            if (rankCanvasGroup != null)
            {
                rankCanvasGroup.alpha = 1f;
            }

            yield return new WaitForSeconds(flashInterval);
        }
    }
}
