// 【役割】リザルトなどの線が現れるUI演出を制御する。
using System.Collections;
using UnityEngine;

public class LineRevealEffect : MonoBehaviour
{
    [Header("--- Reference ---")]
    [SerializeField]
    private RectTransform lineRect;
    [Header("--- Animation Settings ---")]
    [Tooltip("線が伸び切るまでの時間")]
    [SerializeField]
    private float duration = 0.4f;
    [Tooltip("線が伸びた後の横幅")]
    [SerializeField]
    private float targetWidth = 700f;
    private Coroutine currentCoroutine;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        if (lineRect == null)
        {
            lineRect = GetComponent<RectTransform>();
        }

        ResetLine();
    }

    public void ResetLine()
    {
        if (lineRect == null)
        {
            return;
        }

        lineRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
    }

    public IEnumerator Play()
    {
        if (lineRect == null)
        {
            yield break;
        }

        ResetLine();
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float rate = Mathf.Clamp01(timer / duration);
            // 最後に少し減速
            float easedRate = 1f - Mathf.Pow(1f - rate, 3f);
            float width = Mathf.Lerp(0f, targetWidth, easedRate);
            lineRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            yield return null;
        }

        lineRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
    }
}
