// 【役割】ヘリ移動演出のスキップ案内と長押し操作を管理する。
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enterまたは左クリック長押しでヘリ移動をスキップします。
/// 画面が完全に黒くなってからヘリを着陸姿勢へ移動します。
/// </summary>
public sealed class HelicopterTravelSkipController : MonoBehaviour
{
    [SerializeField]
    private HelicopterAutoFlight autoFlight;
    [Header("OPと共用するスキップUI")]
    [Tooltip("ニュースOPで使用している〇 SKIPのCanvasGroup")]
    [SerializeField]
    private CanvasGroup skipPrompt;
    [Tooltip("長押し進捗をFill Amount 0～1で表示するImage")]
    [SerializeField]
    private Image skipHoldProgressImage;
    [Header("長押し")]
    [Min(0.1f)]
    [SerializeField]
    private float holdDuration = 1.2f;
    [Tooltip("長押しを離してからゲージが1から0へ減るまでの秒数")]
    [Min(0.1f)]
    [SerializeField]
    private float gaugeDecreaseDuration = 0.8f;
    [Tooltip("スキップUIを表示・非表示にするフェード時間")]
    [Min(0.01f)]
    [SerializeField]
    private float skipUiFadeDuration = 0.2f;
    [Header("フェード")]
    [Min(0.01f)]
    [SerializeField]
    private float fadeDuration = 0.6f;
    [Min(0f)]
    [SerializeField]
    private float blackScreenWait = 0.15f;
    private CanvasGroup fadeCanvasGroup;
    private float holdTime;
    private bool skipInProgress;
    private bool skipUiVisible;
    private Coroutine skipUiFadeCoroutine;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        PrepareSharedSkipUi();
        CreateFadeOverlay();
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (PauseMenuController.IsPaused)
        {
            return;
        }

        if (skipInProgress || autoFlight == null)
        {
            return;
        }

        if (!autoFlight.CanSkipTravel)
        {
            // OP再生中はOpeningNewsSequenceが同じUIを管理するため、
            // このControllerが表示した場合だけ非表示にします。
            if (skipUiVisible)
            {
                ResetAndHideSkipUi();
            }
            else
            {
                holdTime = 0f;
            }

            return;
        }

        if (!skipUiVisible && HasAnyInputStarted())
        {
            ShowSkipUi();
        }

        if (!skipUiVisible)
        {
            holdTime = 0f;
            UpdateSkipHoldProgress(0f);
            return;
        }

        bool isHeld = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetMouseButton(0) || (GameInputManager.Instance != null && GameInputManager.Instance.UiConfirmHeld);
        if (isHeld)
        {
            holdTime += Time.unscaledDeltaTime;
        }
        else
        {
            float decreaseSpeed = holdDuration / gaugeDecreaseDuration;
            holdTime = Mathf.Max(0f, holdTime - Time.unscaledDeltaTime * decreaseSpeed);
        }

        holdTime = Mathf.Clamp(holdTime, 0f, holdDuration);
        UpdateSkipHoldProgress(holdTime / holdDuration);
        // 長押しが継続中で、かつゲージが100%の時だけ
        // スキップを開始します。
        if (isHeld && holdTime >= holdDuration)
        {
            holdTime = 0f;
            StartCoroutine(SkipTravelSequence());
        }
    }

    private IEnumerator SkipTravelSequence()
    {
        skipInProgress = true;
        // 100%に到達したゲージを1フレーム表示してから、
        // スキップUIをフェードアウトします。
        holdTime = holdDuration;
        UpdateSkipHoldProgress(1f);
        yield return null;
        yield return FadeOutSkipUi();
        fadeCanvasGroup.gameObject.SetActive(true);
        fadeCanvasGroup.blocksRaycasts = true;
        yield return Fade(0f, 1f);
        bool positioned = autoFlight.BeginSkipToLandingPose();
        if (positioned)
        {
            // RigidbodyとTransformの同期が完了するまで待ちます。
            yield return null;
            yield return new WaitForFixedUpdate();
            bool completed = autoFlight.ConfirmSkipLandingAndComplete();
            if (!completed)
            {
                Debug.LogError("[TravelSkip] ヘリの配置を確認できなかったため、" + "プレイヤー出現を実行しません。", this);
            }
            else
            {
                // OnLandedCompleteからプレイヤー出現と
                // CameraManager切替が完了するまで1フレーム待ちます。
                yield return null;
            }

            if (blackScreenWait > 0f)
            {
                yield return new WaitForSecondsRealtime(blackScreenWait);
            }
        }

        yield return Fade(1f, 0f);
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.gameObject.SetActive(false);
        skipInProgress = false;
    }

    private void PrepareSharedSkipUi()
    {
        if (skipPrompt != null)
        {
            // OP側とヘリ移動側が同じCanvasGroupを操作すると、
            // OP側の非表示処理で移動中のUIも消えるため、
            // 見た目だけを複製して制御対象を分離します。
            Canvas parentCanvas = skipPrompt.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                CanvasGroup sourcePrompt = skipPrompt;
                Image sourceProgressImage = skipHoldProgressImage;
                GameObject clonedObject = Instantiate(sourcePrompt.gameObject, parentCanvas.transform, false);
                clonedObject.name = "Helicopter Travel Skip UI";
                clonedObject.transform.SetAsLastSibling();
                skipPrompt = clonedObject.GetComponent<CanvasGroup>();
                if (sourceProgressImage != null)
                {
                    skipHoldProgressImage = FindMatchingProgressImage(sourcePrompt.transform, sourceProgressImage.transform, clonedObject.transform);
                }
            }

            skipPrompt.interactable = false;
            skipPrompt.blocksRaycasts = false;
        }

        ResetAndHideSkipUi();
    }

    private static Image FindMatchingProgressImage(Transform sourceRoot, Transform sourceTarget, Transform clonedRoot)
    {
        string relativePath = GetRelativePath(sourceRoot, sourceTarget);
        Transform clonedTarget = string.IsNullOrEmpty(relativePath) ? clonedRoot : clonedRoot.Find(relativePath);
        return clonedTarget != null ? clonedTarget.GetComponent<Image>() : clonedRoot.GetComponentInChildren<Image>(true);
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (target == root)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null && current != root)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return current == root ? path : string.Empty;
    }

    private void SetSkipUiVisible(bool visible)
    {
        skipUiVisible = visible;
        if (skipPrompt == null)
        {
            return;
        }

        skipPrompt.alpha = visible ? 1f : 0f;
        skipPrompt.gameObject.SetActive(visible);
    }

    private void ShowSkipUi()
    {
        skipUiVisible = true;
        holdTime = 0f;
        UpdateSkipHoldProgress(0f);
        if (skipPrompt == null)
        {
            return;
        }

        if (skipUiFadeCoroutine != null)
        {
            StopCoroutine(skipUiFadeCoroutine);
        }

        skipPrompt.alpha = 0f;
        skipPrompt.gameObject.SetActive(true);
        skipUiFadeCoroutine = StartCoroutine(FadeSkipUi(0f, 1f, false));
    }

    private IEnumerator FadeOutSkipUi()
    {
        if (skipPrompt == null)
        {
            skipUiVisible = false;
            yield break;
        }

        if (skipUiFadeCoroutine != null)
        {
            StopCoroutine(skipUiFadeCoroutine);
            skipUiFadeCoroutine = null;
        }

        yield return FadeSkipUi(skipPrompt.alpha, 0f, true);
    }

    private IEnumerator FadeSkipUi(float from, float to, bool disableAfterFade)
    {
        float elapsed = 0f;
        while (elapsed < skipUiFadeDuration)
        {
            if (PauseMenuController.IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            skipPrompt.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / skipUiFadeDuration));
            yield return null;
        }

        skipPrompt.alpha = to;
        if (disableAfterFade)
        {
            skipPrompt.gameObject.SetActive(false);
            skipUiVisible = false;
        }

        skipUiFadeCoroutine = null;
    }

    private void ResetAndHideSkipUi()
    {
        if (skipUiFadeCoroutine != null)
        {
            StopCoroutine(skipUiFadeCoroutine);
            skipUiFadeCoroutine = null;
        }

        holdTime = 0f;
        UpdateSkipHoldProgress(0f);
        SetSkipUiVisible(false);
    }

    private static bool HasAnyInputStarted()
    {
        return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2) || (GameInputManager.Instance != null && GameInputManager.Instance.AnyControllerButtonPressed);
    }

    private void UpdateSkipHoldProgress(float progress)
    {
        if (skipHoldProgressImage == null)
        {
            return;
        }

        skipHoldProgressImage.type = Image.Type.Filled;
        skipHoldProgressImage.fillAmount = Mathf.Clamp01(progress);
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        fadeCanvasGroup.alpha = from;
        while (elapsed < fadeDuration)
        {
            if (PauseMenuController.IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
    }

    private void CreateFadeOverlay()
    {
        GameObject canvasObject = new GameObject("Helicopter Travel Skip Fade", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        fadeCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.interactable = false;
        fadeCanvasGroup.blocksRaycasts = false;
        GameObject imageObject = new GameObject("Black Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        canvasObject.SetActive(false);
    }
}
