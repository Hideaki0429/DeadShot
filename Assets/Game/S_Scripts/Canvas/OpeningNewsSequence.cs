// 【役割】オープニングのニュース、字幕・音声とスキップの進行を管理する。
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class OpeningNewsSequence : MonoBehaviour
{
    public void AbortForDirectResume()
    {
        StopAllCoroutines();
        enabled = false;
        isNewsPlaying = false;
        isFading = false;
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        HideGroupImmediately(loadingUiCanvasGroup);
        HideGroupImmediately(introBlackScreenPanel);
        HideGroupImmediately(blackScreenPanel);
        HideGroupImmediately(cockpitFrame);
        HideSkipPromptImmediate();
    }

    private static void HideGroupImmediately(CanvasGroup group)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.gameObject.SetActive(false);
    }

    [Header("ニュース設定")]
    [SerializeField]
    private AudioClip newsAudio;
    [SerializeField]
    private TMP_Text newsText;
    [Header("画面設定")]
    [SerializeField]
    private CanvasGroup blackScreenPanel;
    [SerializeField]
    private float waitBeforeFade = 1.0f;
    [Header("ゲーム開始まで非表示にするダメージフィルター")]
    [SerializeField]
    private PlayerDamageScreenEffect damageScreenEffect;
    [Header("導入ローディング")]
    [Tooltip("ローディング背景画像だけをまとめた背景UI専用CanvasGroup")]
    [SerializeField]
    private CanvasGroup introBlackScreenPanel;
    [Tooltip("ローディングUIが消えた後、背景UIが消えるまでの時間（秒）")]
    [Min(0.01f)]
    [SerializeField]
    private float introFadeDuration = 1.0f;
    [Tooltip("LOADING文字、ピストル、進捗バーをまとめたローディングUI専用CanvasGroup")]
    [SerializeField]
    private CanvasGroup loadingUiCanvasGroup;
    [Tooltip("読み込み完了後、背景より先にローディングUIを消す時間（秒）")]
    [Min(0.01f)]
    [SerializeField]
    private float loadingUiFadeDuration = 0.35f;
    [Tooltip("Image TypeをFilledにして使用する進捗バーのFill画像")]
    [SerializeField]
    private Image loadingProgressFillImage;
    [Tooltip("1→2→3の順で表示するピストルImageを3つ登録する")]
    [SerializeField]
    private Image[] loadingPistolImages = new Image[3];
    [Tooltip("ピストル表示を次へ切り替える間隔（秒）")]
    [Min(0.05f)]
    [SerializeField]
    private float loadingPistolSwitchInterval = 0.25f;
    [Tooltip("進捗バーの表示値が報告値へ追従する速度")]
    [Min(0.01f)]
    [SerializeField]
    private float loadingProgressSmoothSpeed = 3.0f;
    [Tooltip("画面のちらつきを防ぐためにローディング画面を表示する最低時間（秒）")]
    [Min(0f)]
    [SerializeField]
    private float minimumLoadingDisplayTime = 1.0f;
    [Tooltip("オンの場合、外部の読み込み処理からCompleteLoadingを呼ぶまでOPを開始しない")]
    [SerializeField]
    private bool waitForExternalLoadingCompletion = true;
    [Header("ニューススキップ")]
    [Tooltip("何か入力した時にフェード表示する、長押しスキップ案内UIのCanvasGroup")]
    [SerializeField]
    private CanvasGroup skipPrompt;
    [Tooltip("Enterまたは右クリックを長押ししてスキップが成立するまでの時間（秒）")]
    [Min(0.1f)]
    [SerializeField]
    private float skipHoldDuration = 1.2f;
    [Tooltip("スキップ案内UIの表示フェード時間（秒）")]
    [Min(0.01f)]
    [SerializeField]
    private float skipPromptFadeDuration = 0.2f;
    [Tooltip("長押し進捗を円形で表示するImage。Image TypeをFilled、Fill MethodをRadial 360に設定してください")]
    [SerializeField]
    private Image skipHoldProgressImage;
    [Header("コックピットUI")]
    [Tooltip("黒画面のフェードと同時に表示するコックピットUIフレーム")]
    [SerializeField]
    private CanvasGroup cockpitFrame;
    [Tooltip("コックピットUIフレームのフェード時間（秒）")]
    [Min(0.01f)]
    [SerializeField]
    private float cockpitFadeDuration = 1.0f;
    [Tooltip("コックピットUIの表示完了後に開始する隊長会話")]
    [SerializeField]
    private CaptainHoverDialogue captainDialogue;
    [Header("カメラ遷移")]
    [Tooltip("ステージ内のカメラ切替を一元管理するCameraManager")]
    [SerializeField]
    private CameraManager cameraManager;
    [Tooltip("通常はオフ。オンにした時だけ、ニュースのフェード開始でカメラを切り替えます。地点選択まで監視カメラを使う場合はオフのままにしてください。")]
    [SerializeField]
    private bool switchCameraOnFade;
    [Tooltip("ニュース表示中に使うカメラ。フェード完了時に停止します。")]
    [SerializeField]
    private Camera openingCamera;
    [Tooltip("黒画面のフェード開始時に有効化する遷移先カメラ。")]
    [SerializeField]
    private Camera transitionTargetCamera;
    [Tooltip("フェード完了後に開始カメラを停止するかどうか。")]
    [SerializeField]
    private bool disableOpeningCameraAfterTransition = true;
    [Header("ニュース中に消音する音声")]
    [Tooltip("ニュース中は音量を0にし、画面フェード開始時に元の音量へ戻すAudioSource")]
    [SerializeField]
    private AudioSource[] audioSourcesToFadeIn;
    [Header("再生速度")]
    [Tooltip("ニュース音声と文字送りの再生倍率")]
    [Range(0.1f, 3f)]
    [SerializeField]
    private float playbackSpeed = 1.1f;
    [Header("文字送り設定")]
    [Tooltip("1行が完成してから消すまでの表示時間")]
    [SerializeField]
    private float completedLineDisplayTime = 0.25f;
    [Tooltip("行を消してから次の行を開始するまでの時間")]
    [SerializeField]
    private float waitBetweenLines = 0.1f;
    [Tooltip("音声未設定時に使用する、1文字あたりの表示時間")]
    [SerializeField]
    private float fallbackCharacterInterval = 0.06f;
    private const float FadeDuration = 1.0f;
    private readonly string[] newsLines =
    {
        "臨時ニュースです。",
        "市内各所で、住民が突然、周囲の人々を襲う異常事案が相次いでいます。",
        "警察および対処部隊が現場へ向かっています。",
        "市民の皆さまは、外出を避け、建物の施錠を徹底してください。",
        "異常行動を起こしている人物を見かけても、決して近づかないでください。"
    };
    private AudioSource audioSource;
    private float[] targetAudioVolumes;
    private bool registeredAudioPrepared;
    private bool cameraTransitionStarted;
    private bool cockpitFrameDisabledForFlight;
    private bool cockpitFrameFadeHasStarted;
    private bool isNewsPlaying;
    private bool isFading;
    private bool skipPromptShown;
    private float skipHoldTime;
    private Coroutine openingCoroutine;
    private Coroutine skipPromptFadeCoroutine;
    private Coroutine loadingPistolLoopCoroutine;
    private float reportedLoadingProgress;
    private float visibleLoadingProgress;
    private bool loadingCompleted;
    private bool externalOpeningStarted;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (damageScreenEffect == null)
        {
            damageScreenEffect = FindObjectOfType<PlayerDamageScreenEffect>();
        }

        SetDamageScreenEffectEnabled(false);
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        if (newsText != null)
        {
            newsText.text = string.Empty;
            newsText.gameObject.SetActive(false);
        }

        if (cockpitFrame != null && !cockpitFrameDisabledForFlight)
        {
            cockpitFrame.alpha = 0f;
            cockpitFrame.gameObject.SetActive(false);
        }

        SetSkipPromptVisible(false);
        UpdateSkipHoldProgress(0f);
        if (introBlackScreenPanel != null)
        {
            introBlackScreenPanel.alpha = 1f;
            introBlackScreenPanel.interactable = true;
            introBlackScreenPanel.blocksRaycasts = true;
            introBlackScreenPanel.gameObject.SetActive(true);
        }

        if (loadingUiCanvasGroup != null && loadingUiCanvasGroup != introBlackScreenPanel)
        {
            loadingUiCanvasGroup.alpha = 1f;
            loadingUiCanvasGroup.interactable = false;
            loadingUiCanvasGroup.blocksRaycasts = false;
            loadingUiCanvasGroup.gameObject.SetActive(true);
        }
        else if (loadingUiCanvasGroup == introBlackScreenPanel && loadingUiCanvasGroup != null)
        {
            Debug.LogWarning("[OpeningNewsSequence] Loading UI Canvas Groupと" + "Intro Black Screen Panelには別々のCanvasGroupを登録してください。", this);
        }

        InitializeLoadingView();
        if (LoadingSceneRequest.IsLoading)
        {
            HideInternalLoadingPanelsForLoadingScene();
        }

        if (switchCameraOnFade && transitionTargetCamera != null && transitionTargetCamera != openingCamera)
        {
            transitionTargetCamera.enabled = false;
        }
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    private void Start()
    {
        // 導入暗転の開始前から、登録音声を消音します。
        PrepareRegisteredAudioSources();
        // 専用Loadingシーン経由の場合、永続化されたLoadingCanvasが
        // フェードを終えてBeginOpeningFromLoadingSceneを呼ぶまで待ちます。
        if (LoadingSceneRequest.IsLoading)
        {
            return;
        }

        bool completeLoadingRequestedBySceneButton = Button_SwitchSceneCmd.ConsumeCompleteLoadingRequest();
        if (!waitForExternalLoadingCompletion || completeLoadingRequestedBySceneButton)
        {
            CompleteLoading();
        }

        openingCoroutine = StartCoroutine(PlayLoadingThenOpeningSequence());
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (PauseMenuController.IsPaused)
        {
            return;
        }

        if (!isNewsPlaying || isFading)
        {
            return;
        }

        if (!skipPromptShown && HasAnyInputStarted())
        {
            ShowSkipPrompt();
        }

        if (!skipPromptShown)
        {
            return;
        }

        bool isSkipHeld = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetMouseButton(0) || (GameInputManager.Instance != null && GameInputManager.Instance.UiConfirmHeld);
        skipHoldTime = isSkipHeld ? skipHoldTime + Time.deltaTime : 0f;
        UpdateSkipHoldProgress(skipHoldTime / skipHoldDuration);
        if (skipHoldTime >= skipHoldDuration)
        {
            SkipNews();
        }
    }

    private IEnumerator PlayOpeningSequence()
    {
        if (blackScreenPanel == null)
        {
            Debug.LogError("[OpeningNewsSequence] BlackScreenPanelが未設定です。");
            yield break;
        }

        PrepareRegisteredAudioSources();
        SetDamageScreenEffectEnabled(false);
        isNewsPlaying = true;
        blackScreenPanel.alpha = 1f;
        blackScreenPanel.gameObject.SetActive(true);
        if (newsText != null)
        {
            newsText.text = string.Empty;
            newsText.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[OpeningNewsSequence] NewsTextが未設定です。");
        }

        if (newsAudio != null)
        {
            float speed = Mathf.Max(0.1f, playbackSpeed);
            audioSource.pitch = speed;
            audioSource.clip = newsAudio;
            audioSource.Play();
            float adjustedAudioDuration = newsAudio.length / speed;
            if (newsText != null)
            {
                yield return StartCoroutine(PlayNewsText(adjustedAudioDuration, speed));
            }

            while (audioSource.isPlaying)
            {
                yield return null;
            }
        }
        else
        {
            Debug.LogWarning("[OpeningNewsSequence] ニュース音声が未設定です。");
            if (newsText != null)
            {
                float speed = Mathf.Max(0.1f, playbackSpeed);
                yield return StartCoroutine(PlayNewsTextWithoutAudio(speed));
            }
        }

        isNewsPlaying = false;
        HideSkipPromptImmediate();
        yield return new WaitForSeconds(waitBeforeFade);
        if (newsText != null)
        {
            newsText.text = string.Empty;
            newsText.gameObject.SetActive(false);
        }

        yield return StartCoroutine(FadeOutBlackScreen());
    }

    /// <summary>
    /// 読み込み進捗を表示し、完了後にローディング画面を
    /// フェードアウトしてからニュース放送を開始します。
    /// </summary>
    private IEnumerator PlayLoadingThenOpeningSequence()
    {
        if (introBlackScreenPanel != null)
        {
            float loadingShownAt = Time.unscaledTime;
            if (HasValidLoadingPistolImages())
            {
                loadingPistolLoopCoroutine = StartCoroutine(LoopLoadingPistols());
            }

            while (!loadingCompleted || visibleLoadingProgress < 1f)
            {
                visibleLoadingProgress = Mathf.MoveTowards(visibleLoadingProgress, reportedLoadingProgress, loadingProgressSmoothSpeed * Time.unscaledDeltaTime);
                UpdateLoadingProgressImage(visibleLoadingProgress);
                yield return null;
            }

            float loadingElapsed = Time.unscaledTime - loadingShownAt;
            if (loadingElapsed < minimumLoadingDisplayTime)
            {
                yield return new WaitForSecondsRealtime(minimumLoadingDisplayTime - loadingElapsed);
            }

            if (loadingPistolLoopCoroutine != null)
            {
                StopCoroutine(loadingPistolLoopCoroutine);
                loadingPistolLoopCoroutine = null;
            }

            // 先に右下のローディングUIだけを消す。
            if (loadingUiCanvasGroup != null && loadingUiCanvasGroup != introBlackScreenPanel)
            {
                yield return StartCoroutine(FadeOutCanvasGroup(loadingUiCanvasGroup, loadingUiFadeDuration));
                loadingUiCanvasGroup.gameObject.SetActive(false);
            }

            SetAllLoadingPistolsVisible(false);
            // ローディングUIが消えた後、背景UIを消してOPを見せる。
            yield return StartCoroutine(FadeOutCanvasGroup(introBlackScreenPanel, introFadeDuration));
            introBlackScreenPanel.interactable = false;
            introBlackScreenPanel.blocksRaycasts = false;
            introBlackScreenPanel.gameObject.SetActive(false);
        }

        yield return StartCoroutine(PlayOpeningSequence());
    }

    /// <summary>
    /// 外部の読み込み処理から、現在の進捗を0～1で通知します。
    /// 進捗バーが逆走しないよう、以前より小さい値は無視します。
    /// 1が通知された場合は読み込み完了として扱います。
    /// </summary>
    public void ReportLoadingProgress(float progress01)
    {
        float clampedProgress = Mathf.Clamp01(progress01);
        reportedLoadingProgress = Mathf.Max(reportedLoadingProgress, clampedProgress);
        if (reportedLoadingProgress >= 1f)
        {
            loadingCompleted = true;
        }
    }

    /// <summary>
    /// 必須データの読み込みがすべて終わった時に呼びます。
    /// 進捗を1まで表示してからフェードアウトします。
    /// </summary>
    public void CompleteLoading()
    {
        reportedLoadingProgress = 1f;
        loadingCompleted = true;
    }

    /// <summary>
    /// 専用Loadingシーンの背景フェード前に呼び、
    /// 背後をニュース用の黒画面へ準備します。
    /// </summary>
    public void PrepareOpeningFromLoadingScene()
    {
        PrepareRegisteredAudioSources();
        SetDamageScreenEffectEnabled(false);
        if (blackScreenPanel != null)
        {
            blackScreenPanel.alpha = 1f;
            blackScreenPanel.interactable = false;
            blackScreenPanel.blocksRaycasts = false;
            blackScreenPanel.gameObject.SetActive(true);
        }

        if (newsText != null)
        {
            newsText.text = string.Empty;
            newsText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 専用LoadingシーンのUIと背景が消えた後に呼び、
    /// ニュースOPを開始します。
    /// </summary>
    public void BeginOpeningFromLoadingScene()
    {
        if (externalOpeningStarted)
        {
            return;
        }

        externalOpeningStarted = true;
        PrepareOpeningFromLoadingScene();
        if (openingCoroutine != null)
        {
            StopCoroutine(openingCoroutine);
        }

        openingCoroutine = StartCoroutine(PlayOpeningSequence());
    }

    private void HideInternalLoadingPanelsForLoadingScene()
    {
        if (loadingUiCanvasGroup != null)
        {
            loadingUiCanvasGroup.alpha = 0f;
            loadingUiCanvasGroup.interactable = false;
            loadingUiCanvasGroup.blocksRaycasts = false;
            loadingUiCanvasGroup.gameObject.SetActive(false);
        }

        if (introBlackScreenPanel != null)
        {
            introBlackScreenPanel.alpha = 0f;
            introBlackScreenPanel.interactable = false;
            introBlackScreenPanel.blocksRaycasts = false;
            introBlackScreenPanel.gameObject.SetActive(false);
        }

        SetAllLoadingPistolsVisible(false);
    }

    private void InitializeLoadingView()
    {
        reportedLoadingProgress = 0f;
        visibleLoadingProgress = 0f;
        loadingCompleted = false;
        if (loadingProgressFillImage != null)
        {
            loadingProgressFillImage.type = Image.Type.Filled;
            loadingProgressFillImage.fillMethod = Image.FillMethod.Horizontal;
            loadingProgressFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            loadingProgressFillImage.fillClockwise = true;
            loadingProgressFillImage.fillAmount = 0f;
        }
        else
        {
            Debug.LogWarning("[OpeningNewsSequence] " + "Loading Progress Fill Imageが未設定です。", this);
        }

        SetAllLoadingPistolsVisible(false);
    }

    private void UpdateLoadingProgressImage(float progress01)
    {
        if (loadingProgressFillImage != null)
        {
            loadingProgressFillImage.fillAmount = Mathf.Clamp01(progress01);
        }
    }

    private bool HasValidLoadingPistolImages()
    {
        if (loadingPistolImages == null || loadingPistolImages.Length != 3)
        {
            Debug.LogWarning("[OpeningNewsSequence] " + "Loading Pistol Imagesへ3つ登録してください。", this);
            return false;
        }

        for (int index = 0; index < loadingPistolImages.Length; index++)
        {
            if (loadingPistolImages[index] == null)
            {
                Debug.LogWarning("[OpeningNewsSequence] " + "Loading Pistol Imagesに空欄があります。", this);
                return false;
            }
        }

        return true;
    }

    private IEnumerator LoopLoadingPistols()
    {
        int currentPistolIndex = 0;
        while (!loadingCompleted)
        {
            for (int index = 0; index < loadingPistolImages.Length; index++)
            {
                loadingPistolImages[index].enabled = index == currentPistolIndex;
            }

            currentPistolIndex = (currentPistolIndex + 1) % loadingPistolImages.Length;
            yield return new WaitForSecondsRealtime(loadingPistolSwitchInterval);
        }
    }

    private void SetAllLoadingPistolsVisible(bool visible)
    {
        if (loadingPistolImages == null)
        {
            return;
        }

        foreach (Image pistolImage in loadingPistolImages)
        {
            if (pistolImage != null)
            {
                pistolImage.enabled = visible;
            }
        }
    }

    private IEnumerator FadeOutCanvasGroup(CanvasGroup targetCanvasGroup, float duration)
    {
        if (targetCanvasGroup == null)
        {
            yield break;
        }

        float safeDuration = Mathf.Max(0.01f, duration);
        float startAlpha = targetCanvasGroup.alpha;
        float elapsedTime = 0f;
        while (elapsedTime < safeDuration)
        {
            if (PauseMenuController.IsPaused)
            {
                yield return null;
                continue;
            }

            elapsedTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsedTime / safeDuration);
            targetCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);
            yield return null;
        }

        targetCanvasGroup.alpha = 0f;
        targetCanvasGroup.interactable = false;
        targetCanvasGroup.blocksRaycasts = false;
    }

    private IEnumerator PlayNewsText(float audioDuration, float speed)
    {
        int totalCharacterCount = 0;
        foreach (string line in newsLines)
        {
            totalCharacterCount += line.Length;
        }

        float adjustedCompletedLineDisplayTime = completedLineDisplayTime / speed;
        float adjustedWaitBetweenLines = waitBetweenLines / speed;
        float totalCompletedLineDisplayTime = adjustedCompletedLineDisplayTime * newsLines.Length;
        float totalLineWait = adjustedWaitBetweenLines * Mathf.Max(0, newsLines.Length - 1);
        float availableTypingTime = Mathf.Max(0.01f, audioDuration - totalCompletedLineDisplayTime - totalLineWait);
        float characterInterval = availableTypingTime / Mathf.Max(1, totalCharacterCount);
        for (int lineIndex = 0; lineIndex < newsLines.Length; lineIndex++)
        {
            yield return StartCoroutine(TypeCurrentLine(newsLines[lineIndex], characterInterval, adjustedCompletedLineDisplayTime));
            if (lineIndex < newsLines.Length - 1)
            {
                yield return new WaitForSeconds(adjustedWaitBetweenLines);
            }
        }
    }

    private IEnumerator PlayNewsTextWithoutAudio(float speed)
    {
        float adjustedCharacterInterval = fallbackCharacterInterval / speed;
        float adjustedCompletedLineDisplayTime = completedLineDisplayTime / speed;
        float adjustedWaitBetweenLines = waitBetweenLines / speed;
        for (int lineIndex = 0; lineIndex < newsLines.Length; lineIndex++)
        {
            yield return StartCoroutine(TypeCurrentLine(newsLines[lineIndex], adjustedCharacterInterval, adjustedCompletedLineDisplayTime));
            if (lineIndex < newsLines.Length - 1)
            {
                yield return new WaitForSeconds(adjustedWaitBetweenLines);
            }
        }
    }

    private IEnumerator TypeCurrentLine(string line, float characterInterval, float lineDisplayTime)
    {
        newsText.text = string.Empty;
        for (int characterIndex = 0; characterIndex < line.Length; characterIndex++)
        {
            newsText.text += line[characterIndex];
            yield return new WaitForSeconds(characterInterval);
        }

        yield return new WaitForSeconds(lineDisplayTime);
        newsText.text = string.Empty;
    }

    private void PrepareRegisteredAudioSources()
    {
        if (registeredAudioPrepared)
        {
            return;
        }

        if (audioSourcesToFadeIn == null)
        {
            targetAudioVolumes = new float[0];
            return;
        }

        targetAudioVolumes = new float[audioSourcesToFadeIn.Length];
        for (int index = 0; index < audioSourcesToFadeIn.Length; index++)
        {
            AudioSource targetSource = audioSourcesToFadeIn[index];
            if (targetSource == null)
            {
                continue;
            }

            if (targetSource == audioSource)
            {
                Debug.LogWarning("[OpeningNewsSequence] ニュース用AudioSourceが" + "フェード対象に登録されているため除外しました。");
                continue;
            }

            targetAudioVolumes[index] = targetSource.volume;
            targetSource.volume = 0f;
        }

        registeredAudioPrepared = true;
    }

    private void UpdateRegisteredAudioVolumes(float progress)
    {
        if (!registeredAudioPrepared || audioSourcesToFadeIn == null || targetAudioVolumes == null)
        {
            return;
        }

        for (int index = 0; index < audioSourcesToFadeIn.Length; index++)
        {
            AudioSource targetSource = audioSourcesToFadeIn[index];
            if (targetSource == null || targetSource == audioSource)
            {
                continue;
            }

            targetSource.volume = Mathf.Lerp(0f, targetAudioVolumes[index], progress);
        }
    }

    private bool HasAnyInputStarted()
    {
        return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2) || (GameInputManager.Instance != null && GameInputManager.Instance.AnyControllerButtonPressed);
    }

    private void ShowSkipPrompt()
    {
        skipPromptShown = true;
        skipHoldTime = 0f;
        UpdateSkipHoldProgress(0f);
        if (skipPrompt == null)
        {
            return;
        }

        if (skipPromptFadeCoroutine != null)
        {
            StopCoroutine(skipPromptFadeCoroutine);
        }

        skipPrompt.alpha = 0f;
        skipPrompt.gameObject.SetActive(true);
        skipPromptFadeCoroutine = StartCoroutine(FadeSkipPrompt(0f, 1f));
    }

    private void SkipNews()
    {
        if (isFading)
        {
            return;
        }

        isNewsPlaying = false;
        audioSource.Stop();
        if (newsText != null)
        {
            newsText.text = string.Empty;
            newsText.gameObject.SetActive(false);
        }

        HideSkipPromptImmediate();
        if (openingCoroutine != null)
        {
            StopCoroutine(openingCoroutine);
            openingCoroutine = null;
        }

        StartCoroutine(FadeOutBlackScreen());
    }

    private IEnumerator FadeSkipPrompt(float from, float to)
    {
        float elapsedTime = 0f;
        while (elapsedTime < skipPromptFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / skipPromptFadeDuration);
            skipPrompt.alpha = Mathf.Lerp(from, to, progress);
            yield return null;
        }

        skipPrompt.alpha = to;
        skipPromptFadeCoroutine = null;
    }

    private void HideSkipPromptImmediate()
    {
        skipPromptShown = false;
        skipHoldTime = 0f;
        UpdateSkipHoldProgress(0f);
        if (skipPromptFadeCoroutine != null)
        {
            StopCoroutine(skipPromptFadeCoroutine);
            skipPromptFadeCoroutine = null;
        }

        SetSkipPromptVisible(false);
    }

    private void SetSkipPromptVisible(bool visible)
    {
        if (skipPrompt == null)
        {
            return;
        }

        skipPrompt.alpha = visible ? 1f : 0f;
        skipPrompt.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 長押し時間を0〜1に変換して、円形ゲージのFill Amountへ反映します。
    /// </summary>
    private void UpdateSkipHoldProgress(float progress)
    {
        if (skipHoldProgressImage != null)
        {
            skipHoldProgressImage.fillAmount = Mathf.Clamp01(progress);
        }
    }

    private IEnumerator FadeOutBlackScreen()
    {
        isFading = true;
        bool shouldFadeCockpitFrame = cockpitFrame != null && !cockpitFrameDisabledForFlight && !cockpitFrameFadeHasStarted;
        if (shouldFadeCockpitFrame)
        {
            cockpitFrameFadeHasStarted = true;
        }

        if (switchCameraOnFade)
        {
            BeginCameraTransition();
        }

        float elapsedTime = 0f;
        float presentationDuration = Mathf.Max(FadeDuration, cockpitFadeDuration);
        if (shouldFadeCockpitFrame)
        {
            cockpitFrame.alpha = 0f;
            cockpitFrame.gameObject.SetActive(true);
            Debug.Log("[OpeningNewsSequence] コックピットUIのフェード表示を開始しました。", this);
        }

        while (elapsedTime < presentationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / FadeDuration);
            blackScreenPanel.alpha = Mathf.Lerp(1f, 0f, progress);
            if (shouldFadeCockpitFrame)
            {
                float cockpitProgress = Mathf.Clamp01(elapsedTime / cockpitFadeDuration);
                cockpitFrame.alpha = cockpitProgress;
            }

            UpdateRegisteredAudioVolumes(progress);
            yield return null;
        }

        blackScreenPanel.alpha = 0f;
        if (shouldFadeCockpitFrame)
        {
            cockpitFrame.alpha = 1f;
            Debug.Log("[OpeningNewsSequence] コックピットUIのフェード表示が完了しました。", this);
        }

        UpdateRegisteredAudioVolumes(1f);
        blackScreenPanel.gameObject.SetActive(false);
        if (switchCameraOnFade)
        {
            FinishCameraTransition();
        }

        if (captainDialogue != null)
        {
            captainDialogue.BeginDialogue();
        }
        else
        {
            Debug.LogWarning("[OpeningNewsSequence] Captain Dialogueが未設定です。", this);
        }
    }

    private void SetDamageScreenEffectEnabled(bool enabled)
    {
        if (damageScreenEffect != null)
        {
            damageScreenEffect.SetEffectEnabled(enabled);
        }
    }

    /// <summary>
    /// 地点確定後に呼び出し、飛行フェーズ中はコックピットUIを再表示させません。
    /// </summary>
    public void HideCockpitFrameForFlight()
    {
        cockpitFrameDisabledForFlight = true;
        if (cockpitFrame != null)
        {
            cockpitFrame.alpha = 0f;
            cockpitFrame.interactable = false;
            cockpitFrame.blocksRaycasts = false;
            Debug.Log("[OpeningNewsSequence] 飛行フェーズ開始のためコックピットUIを非表示にしました。", this);
        }
    }

    /// <summary>
    /// 黒画面がフェードし始めるタイミングで遷移先カメラを有効化します。
    /// フェード越しに新しいカメラ映像が現れるため、自然な画面切替になります。
    /// </summary>
    private void BeginCameraTransition()
    {
        if (cameraTransitionStarted)
        {
            return;
        }

        cameraTransitionStarted = true;
        if (cameraManager != null)
        {
            cameraManager.SwitchToHelicopterCamera();
            return;
        }

        if (transitionTargetCamera == null)
        {
            return;
        }

        if (transitionTargetCamera == openingCamera)
        {
            Debug.LogWarning("[OpeningNewsSequence] 開始カメラと遷移先カメラが同じです。", this);
            return;
        }

        if (openingCamera != null)
        {
            transitionTargetCamera.depth = openingCamera.depth + 1f;
        }

        transitionTargetCamera.enabled = true;
    }

    /// <summary>
    /// フェード完了後、必要に応じて開始カメラを停止します。
    /// </summary>
    private void FinishCameraTransition()
    {
        if (cameraManager != null)
        {
            return;
        }

        if (!disableOpeningCameraAfterTransition || openingCamera == null || openingCamera == transitionTargetCamera)
        {
            return;
        }

        openingCamera.enabled = false;
    }
}
