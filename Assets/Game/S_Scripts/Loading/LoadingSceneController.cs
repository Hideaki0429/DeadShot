// 【役割】ロード画面の表示と次のシーンの非同期読み込みを制御する。
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 専用Loadingシーンでステージを非同期読み込みし、
/// UI、背景の順でフェードしてからOPを開始します。
/// </summary>
public sealed class LoadingSceneController : MonoBehaviour
{
    [Header("表示グループ")]
    [Tooltip("背景画像をまとめたCanvasGroup")]
    [SerializeField]
    private CanvasGroup backgroundCanvasGroup;
    [Tooltip("LOADING文字、ピストル、進捗バーをまとめたCanvasGroup")]
    [SerializeField]
    private CanvasGroup loadingUiCanvasGroup;
    [Header("進捗")]
    [SerializeField]
    private Image progressFillImage;
    [Header("ピストルループ")]
    [Tooltip("表示順にピストルImageを3つ登録する")]
    [SerializeField]
    private Image[] pistolImages = new Image[3];
    [Min(0.05f)]
    [SerializeField]
    private float pistolSwitchInterval = 0.25f;
    [Header("表示時間とフェード")]
    [Tooltip("実際の読み込みが速くても、進捗バーが0～1へ進む最低時間")]
    [Min(0f)]
    [SerializeField]
    private float minimumDisplayTime = 2f;
    [Min(0.01f)]
    [SerializeField]
    private float loadingUiFadeDuration = 0.35f;
    [Min(0.01f)]
    [SerializeField]
    private float backgroundFadeDuration = 1f;
    private Coroutine pistolLoopCoroutine;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        // LoadSceneMode.Singleでステージを有効化した後も、
        // ローディング画面を残してフェードできるようにします。
        DontDestroyOnLoad(gameObject);
        if (backgroundCanvasGroup != null)
        {
            backgroundCanvasGroup.alpha = 1f;
            backgroundCanvasGroup.interactable = true;
            backgroundCanvasGroup.blocksRaycasts = true;
        }

        if (loadingUiCanvasGroup != null)
        {
            loadingUiCanvasGroup.alpha = 1f;
            loadingUiCanvasGroup.interactable = false;
            loadingUiCanvasGroup.blocksRaycasts = false;
        }

        if (progressFillImage != null)
        {
            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFillImage.fillClockwise = true;
            progressFillImage.fillAmount = 0f;
        }

        SetAllPistolsVisible(false);
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    private IEnumerator Start()
    {
        string targetSceneName = LoadingSceneRequest.TargetSceneName;
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[LoadingSceneController] 遷移先シーンが設定されていません。", this);
            yield break;
        }

        if (progressFillImage == null)
        {
            Debug.LogError("[LoadingSceneController] Progress Fill Imageが未設定です。", this);
            yield break;
        }

        if (HasValidPistolImages())
        {
            pistolLoopCoroutine = StartCoroutine(LoopPistols());
        }

        // LoadingシーンのUIを最低1フレーム描画してから、
        // 重いステージ読み込みを開始します。
        yield return null;
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Debug.LogError("[LoadingSceneController] ステージの非同期読み込みを開始できませんでした。", this);
            yield break;
        }

        loadOperation.allowSceneActivation = false;
        float displayedProgress = 0f;
        float safeProgressDuration = Mathf.Max(0.01f, minimumDisplayTime);
        while (loadOperation.progress < 0.9f || displayedProgress < 1f)
        {
            // Unityの非同期シーン読み込みは有効化待ちで0.9に止まるため、
            // 表示上は0～1へ正規化し、バーを滑らかに追従させます。
            float actualProgress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            if (loadOperation.progress >= 0.9f)
            {
                actualProgress = 1f;
            }

            // 読み込み中にメインスレッドが一時停止しても、
            // 復帰フレームでバーが一気に飛ばないよう時間差を制限します。
            float visualDeltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            displayedProgress = Mathf.MoveTowards(displayedProgress, actualProgress, visualDeltaTime / safeProgressDuration);
            progressFillImage.fillAmount = displayedProgress;
            yield return null;
        }

        progressFillImage.fillAmount = 1f;
        loadOperation.allowSceneActivation = true;
        while (!loadOperation.isDone)
        {
            yield return null;
        }

        // 遷移先のAwakeとStartが完了するまで1フレーム待ちます。
        yield return null;
        if (StageArrivalRequest.ConsumeStartTimerRequest())
        {
            ScoreAndTimeManager scoreAndTime = FindObjectOfType<ScoreAndTimeManager>(true);
            if (scoreAndTime != null)
            {
                scoreAndTime.StartGameFromTutorial();
            }
        }

        OpeningNewsSequence openingSequence = FindObjectOfType<OpeningNewsSequence>();
        // タイトルなどOPを持たないシーンでも共通のフェードを使用します。
        if (openingSequence != null)
        {
            // 背景フェードの背後をニュース用黒画面にして、
            // ステージ映像が一瞬見えることを防ぎます。
            openingSequence.PrepareOpeningFromLoadingScene();
        }

        if (pistolLoopCoroutine != null)
        {
            StopCoroutine(pistolLoopCoroutine);
            pistolLoopCoroutine = null;
        }

        if (loadingUiCanvasGroup != null)
        {
            yield return FadeOutCanvasGroup(loadingUiCanvasGroup, loadingUiFadeDuration);
            loadingUiCanvasGroup.gameObject.SetActive(false);
        }

        SetAllPistolsVisible(false);
        if (backgroundCanvasGroup != null)
        {
            yield return FadeOutCanvasGroup(backgroundCanvasGroup, backgroundFadeDuration);
        }

        if (openingSequence != null)
        {
            openingSequence.BeginOpeningFromLoadingScene();
        }

        LoadingSceneRequest.Clear();
        Destroy(gameObject);
    }

    private IEnumerator LoopPistols()
    {
        int currentIndex = 0;
        while (true)
        {
            for (int index = 0; index < pistolImages.Length; index++)
            {
                pistolImages[index].enabled = index == currentIndex;
            }

            currentIndex = (currentIndex + 1) % pistolImages.Length;
            yield return new WaitForSecondsRealtime(pistolSwitchInterval);
        }
    }

    private bool HasValidPistolImages()
    {
        if (pistolImages == null || pistolImages.Length != 3)
        {
            Debug.LogWarning("[LoadingSceneController] Pistol Imagesへ3つ登録してください。", this);
            return false;
        }

        foreach (Image pistolImage in pistolImages)
        {
            if (pistolImage == null)
            {
                Debug.LogWarning("[LoadingSceneController] Pistol Imagesに空欄があります。", this);
                return false;
            }
        }

        return true;
    }

    private void SetAllPistolsVisible(bool visible)
    {
        if (pistolImages == null)
        {
            return;
        }

        foreach (Image pistolImage in pistolImages)
        {
            if (pistolImage != null)
            {
                pistolImage.enabled = visible;
            }
        }
    }

    private static IEnumerator FadeOutCanvasGroup(CanvasGroup target, float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float startAlpha = target.alpha;
        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            target.alpha = Mathf.Lerp(startAlpha, 0f, progress);
            yield return null;
        }

        target.alpha = 0f;
        target.interactable = false;
        target.blocksRaycasts = false;
    }
}
