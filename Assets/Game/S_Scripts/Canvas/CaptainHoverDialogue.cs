// 【役割】隊長との会話UI、文字送りと選択への進行を管理する。
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class TutorialChoiceEvent : UnityEvent<bool>
{
}

[RequireComponent(typeof(AudioSource))]
public class CaptainHoverDialogue : MonoBehaviour
{
    [Header("イベント開始条件")]
    [Tooltip("ホバリング開始を監視するHelicopterAutoFlight")]
    [SerializeField]
    private HelicopterAutoFlight helicopterAutoFlight;
    [Tooltip("オンなら、ホバリング開始時にも自動で会話を始めます。OPではオフにし、OpeningNewsSequenceから開始してください")]
    [SerializeField]
    private bool beginWhenWaypointHoverStarts;
    [Tooltip("隊長会話の終了後に表示する地点選択UI")]
    [SerializeField]
    private LandingPointSelectionUI landingPointSelectionUI;
    [Header("会話UI")]
    [Tooltip("会話中に表示するパネル。スクリプト本体とは別のオブジェクトを指定してください")]
    [SerializeField]
    private GameObject dialoguePanel;
    [Tooltip("隊長の文章を表示するTextMeshPro")]
    [SerializeField]
    private TMP_Text dialogueText;
    [Header("YES / NO 選択UI")]
    [Tooltip("この文字を含む会話行の表示完了後に選択UIを表示します")]
    [SerializeField]
    private string tutorialQuestionText = "始めに、任務に参加するのは初めてか？";
    [Tooltip("YES・NO・カーソルをまとめた親UIのCanvasGroup")]
    [SerializeField]
    private CanvasGroup tutorialChoiceCanvasGroup;
    [Tooltip("YES表示用UI")]
    [SerializeField]
    private GameObject yesUI;
    [Tooltip("NO表示用UI")]
    [SerializeField]
    private GameObject noUI;
    [Tooltip("選択位置を示すカーソルUI")]
    [SerializeField]
    private RectTransform choiceCursorUI;
    [Header("カーソル位置")]
    [Tooltip("YES選択時のカーソルX座標")]
    [SerializeField]
    private float yesCursorPositionX = -100f;
    [Tooltip("NO選択時のカーソルX座標")]
    [SerializeField]
    private float noCursorPositionX = 100f;
    [Tooltip("オンの場合、カーソルのY座標も指定値に固定します")]
    [SerializeField]
    private bool overrideCursorPositionY;
    [SerializeField]
    private float cursorPositionY;
    [Header("選択UIの演出")]
    [Tooltip("選択UIを表示・非表示にするフェード時間")]
    [Min(0f)]
    [SerializeField]
    private float choiceFadeDuration = 0.3f;
    [Tooltip("選択カーソルを移動したときのSE")]
    [SerializeField]
    private AudioClip choiceMoveSe;
    [Range(0f, 1f)]
    [SerializeField]
    private float choiceMoveSeVolume = 1f;
    [Header("チュートリアル選択結果")]
    [Tooltip("選択確定時に呼び出します。YES=true、NO=false")]
    [SerializeField]
    private TutorialChoiceEvent onTutorialChoiceConfirmed;
    [Header("隊長の文章")]
    [Tooltip("表示する文章を1行ずつ登録してください")]
    [TextArea(2, 5)]
    [SerializeField]
    private string[] dialogueLines;
    [Header("文字送り")]
    [Tooltip("1文字を表示する間隔（秒）")]
    [Min(0.001f)]
    [SerializeField]
    private float characterInterval = 0.05f;
    [Header("会話の自動送り")]
    [Tooltip("ONにすると、チュートリアル有無を尋ねる質問以外は自動で次の会話へ進みます")]
    [SerializeField]
    private bool autoAdvanceDialogue = true;
    [Tooltip("文字がすべて表示されてから次の会話へ進むまでの秒数")]
    [Min(0f)]
    [SerializeField]
    private float autoAdvanceDelay = 1.5f;
    [Tooltip("何文字ごとに文字送りSEを鳴らすか")]
    [Min(1)]
    [SerializeField]
    private int typingSeCharacterInterval = 2;
    [Header("SE")]
    [SerializeField]
    private AudioClip typingSe;
    [SerializeField]
    private AudioClip enterSe;
    [Range(0f, 1f)]
    [SerializeField]
    private float typingSeVolume = 0.5f;
    [Range(0f, 1f)]
    [SerializeField]
    private float enterSeVolume = 1f;
    private AudioSource audioSource;
    private Coroutine typingCoroutine;
    private Coroutine choiceFadeCoroutine;
    private Coroutine autoAdvanceCoroutine;
    private int currentLineIndex;
    private bool dialogueActive;
    private bool isTyping;
    private bool dialogueStarted;
    private bool choiceActive;
    private bool choiceInputReady;
    private bool yesSelected = true;
    public bool HasTutorialDecision { get; private set; }
    public bool ShouldRunTutorial { get; private set; } = true;

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
            dialogueText.maxVisibleCharacters = 0;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        InitializeChoiceUI();
    }

    // コンポーネントが有効になったときの処理です。
    private void OnEnable()
    {
        if (helicopterAutoFlight != null)
        {
            helicopterAutoFlight.OnWaypointHoverStarted += HandleWaypointHoverStarted;
        }
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    private void Start()
    {
        if (helicopterAutoFlight == null)
        {
            Debug.LogError("[CaptainDialogue] HelicopterAutoFlightが未設定です。");
            return;
        }

        if (beginWhenWaypointHoverStarts && helicopterAutoFlight.CurrentPhase == HelicopterAutoFlight.FlightPhase.HoverAtWaypoint)
        {
            BeginDialogue();
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (!dialogueActive)
        {
            return;
        }

        if (choiceActive)
        {
            HandleChoiceInput();
            return;
        }

        bool enterPressed = GameInputManager.Instance.ConfirmPressed;
        if (enterPressed)
        {
            HandleEnterInput();
        }
    }

    // コンポーネントが無効になったときの処理です。
    private void OnDisable()
    {
        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.SetDialogue(this, false);
        }

        if (helicopterAutoFlight != null)
        {
            helicopterAutoFlight.OnWaypointHoverStarted -= HandleWaypointHoverStarted;
        }
    }

    /// <summary>
    /// OpeningNewsSequenceから呼び出して、コックピットUI表示後に隊長会話を開始します。
    /// </summary>
    public void BeginDialogue()
    {
        if (dialogueStarted)
        {
            return;
        }

        if (dialogueText == null)
        {
            Debug.LogError("[CaptainDialogue] DialogueTextが未設定です。");
            return;
        }

        if (dialogueLines == null || dialogueLines.Length == 0)
        {
            Debug.LogWarning("[CaptainDialogue] 隊長の文章が登録されていません。");
            return;
        }

        dialogueStarted = true;
        dialogueActive = true;
        GameInputManager.Instance.SetDialogue(this, true);
        currentLineIndex = 0;
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        StartCurrentLine();
    }

    private void HandleEnterInput()
    {
        CancelAutoAdvance();
        PlayEnterSe();
        if (isTyping)
        {
            CompleteCurrentLineImmediately();
            return;
        }

        currentLineIndex++;
        if (currentLineIndex >= dialogueLines.Length)
        {
            EndDialogue();
            return;
        }

        StartCurrentLine();
    }

    private void StartCurrentLine()
    {
        CancelAutoAdvance();
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        typingCoroutine = StartCoroutine(TypeCurrentLine(dialogueLines[currentLineIndex]));
    }

    private IEnumerator TypeCurrentLine(string line)
    {
        isTyping = true;
        dialogueText.text = line;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();
        int characterCount = dialogueText.textInfo.characterCount;
        for (int characterIndex = 0; characterIndex < characterCount; characterIndex++)
        {
            dialogueText.maxVisibleCharacters = characterIndex + 1;
            if (ShouldPlayTypingSe(characterIndex, line))
            {
                audioSource.PlayOneShot(typingSe, typingSeVolume);
            }

            yield return new WaitForSeconds(characterInterval);
        }

        dialogueText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
        typingCoroutine = null;
        TryOpenTutorialChoice(line);
        if (!choiceActive)
        {
            BeginAutoAdvance();
        }
    }

    private bool ShouldPlayTypingSe(int characterIndex, string line)
    {
        if (typingSe == null)
        {
            return false;
        }

        if (characterIndex % Mathf.Max(1, typingSeCharacterInterval) != 0)
        {
            return false;
        }

        if (characterIndex >= line.Length)
        {
            return false;
        }

        char character = line[characterIndex];
        return !char.IsWhiteSpace(character) && character != '、' && character != '。';
    }

    private void CompleteCurrentLineImmediately()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        dialogueText.text = dialogueLines[currentLineIndex];
        dialogueText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
        TryOpenTutorialChoice(dialogueLines[currentLineIndex]);
        if (!choiceActive)
        {
            BeginAutoAdvance();
        }
    }

    private void BeginAutoAdvance()
    {
        CancelAutoAdvance();
        if (autoAdvanceDialogue && dialogueActive && !choiceActive)
        {
            autoAdvanceCoroutine = StartCoroutine(AutoAdvanceCurrentLine());
        }
    }

    private IEnumerator AutoAdvanceCurrentLine()
    {
        float elapsed = 0f;
        while (elapsed < autoAdvanceDelay)
        {
            if (!PauseMenuController.IsPaused)
            {
                elapsed += Time.unscaledDeltaTime;
            }

            yield return null;
        }

        autoAdvanceCoroutine = null;
        if (dialogueActive && !choiceActive && !isTyping)
        {
            HandleEnterInput();
        }
    }

    private void CancelAutoAdvance()
    {
        if (autoAdvanceCoroutine == null)
        {
            return;
        }

        StopCoroutine(autoAdvanceCoroutine);
        autoAdvanceCoroutine = null;
    }

    private void InitializeChoiceUI()
    {
        SetChoiceObjectsActive(false);
        if (tutorialChoiceCanvasGroup != null)
        {
            tutorialChoiceCanvasGroup.alpha = 0f;
            tutorialChoiceCanvasGroup.interactable = false;
            tutorialChoiceCanvasGroup.blocksRaycasts = false;
        }
    }

    private void TryOpenTutorialChoice(string line)
    {
        if (HasTutorialDecision || choiceActive || string.IsNullOrEmpty(tutorialQuestionText) || string.IsNullOrEmpty(line) || !line.Contains(tutorialQuestionText))
        {
            return;
        }

        choiceActive = true;
        choiceInputReady = false;
        yesSelected = true;
        SetChoiceObjectsActive(true);
        UpdateChoiceCursor(false);
        if (choiceFadeCoroutine != null)
        {
            StopCoroutine(choiceFadeCoroutine);
        }

        choiceFadeCoroutine = StartCoroutine(FadeChoiceUI(true));
    }

    private void HandleChoiceInput()
    {
        if (!choiceInputReady)
        {
            return;
        }

        bool selectYes = GameInputManager.Instance.DialogueSelection < 0;
        bool selectNo = GameInputManager.Instance.DialogueSelection > 0;
        if (selectYes && !yesSelected)
        {
            yesSelected = true;
            UpdateChoiceCursor(true);
        }
        else if (selectNo && yesSelected)
        {
            yesSelected = false;
            UpdateChoiceCursor(true);
        }

        bool confirmPressed = GameInputManager.Instance.ConfirmPressed;
        if (confirmPressed)
        {
            ConfirmTutorialChoice();
        }
    }

    private void UpdateChoiceCursor(bool playSe)
    {
        if (choiceCursorUI != null)
        {
            Vector2 position = choiceCursorUI.anchoredPosition;
            position.x = yesSelected ? yesCursorPositionX : noCursorPositionX;
            if (overrideCursorPositionY)
            {
                position.y = cursorPositionY;
            }

            choiceCursorUI.anchoredPosition = position;
        }

        if (playSe && choiceMoveSe != null)
        {
            audioSource.PlayOneShot(choiceMoveSe, choiceMoveSeVolume);
        }
    }

    private void ConfirmTutorialChoice()
    {
        choiceInputReady = false;
        HasTutorialDecision = true;
        ShouldRunTutorial = yesSelected;
        PlayEnterSe();
        onTutorialChoiceConfirmed?.Invoke(ShouldRunTutorial);
        Debug.Log("[CaptainDialogue] チュートリアル選択確定: " + (ShouldRunTutorial ? "YES / 実行する" : "NO / 実行しない"), this);
        if (choiceFadeCoroutine != null)
        {
            StopCoroutine(choiceFadeCoroutine);
        }

        choiceFadeCoroutine = StartCoroutine(HideChoiceAndContinueDialogue());
    }

    private IEnumerator HideChoiceAndContinueDialogue()
    {
        yield return FadeChoiceUI(false);
        choiceActive = false;
        currentLineIndex++;
        if (currentLineIndex >= dialogueLines.Length)
        {
            EndDialogue();
        }
        else
        {
            StartCurrentLine();
        }
    }

    private IEnumerator FadeChoiceUI(bool show)
    {
        if (tutorialChoiceCanvasGroup == null)
        {
            if (!show)
            {
                SetChoiceObjectsActive(false);
            }
            else
            {
                choiceInputReady = true;
            }

            choiceFadeCoroutine = null;
            yield break;
        }

        tutorialChoiceCanvasGroup.interactable = false;
        tutorialChoiceCanvasGroup.blocksRaycasts = false;
        float startAlpha = tutorialChoiceCanvasGroup.alpha;
        float targetAlpha = show ? 1f : 0f;
        float elapsed = 0f;
        if (choiceFadeDuration <= 0f)
        {
            tutorialChoiceCanvasGroup.alpha = targetAlpha;
        }
        else
        {
            while (elapsed < choiceFadeDuration)
            {
                if (PauseMenuController.IsPaused)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.unscaledDeltaTime;
                tutorialChoiceCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / choiceFadeDuration);
                yield return null;
            }
        }

        tutorialChoiceCanvasGroup.alpha = targetAlpha;
        if (show)
        {
            tutorialChoiceCanvasGroup.interactable = true;
            tutorialChoiceCanvasGroup.blocksRaycasts = true;
            choiceInputReady = true;
        }
        else
        {
            SetChoiceObjectsActive(false);
        }

        choiceFadeCoroutine = null;
    }

    private void SetChoiceObjectsActive(bool active)
    {
        if (tutorialChoiceCanvasGroup != null)
        {
            tutorialChoiceCanvasGroup.gameObject.SetActive(active);
        }

        if (yesUI != null)
        {
            yesUI.SetActive(active);
        }

        if (noUI != null)
        {
            noUI.SetActive(active);
        }

        if (choiceCursorUI != null)
        {
            choiceCursorUI.gameObject.SetActive(active);
        }
    }

    private void PlayEnterSe()
    {
        if (enterSe == null)
        {
            return;
        }

        audioSource.PlayOneShot(enterSe, enterSeVolume);
    }

    private void EndDialogue()
    {
        CancelAutoAdvance();
        dialogueActive = false;
        GameInputManager.Instance.SetDialogue(this, false);
        isTyping = false;
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        dialogueText.text = string.Empty;
        dialogueText.maxVisibleCharacters = 0;
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        Debug.Log("[CaptainDialogue] 隊長の会話が終了しました。");
        if (landingPointSelectionUI != null)
        {
            landingPointSelectionUI.OpenSelection();
        }
        else
        {
            Debug.LogWarning("[CaptainDialogue] Landing Point Selection UIが未設定です。", this);
        }
    }

    private void HandleWaypointHoverStarted()
    {
        if (beginWhenWaypointHoverStarts)
        {
            BeginDialogue();
        }
    }
}
