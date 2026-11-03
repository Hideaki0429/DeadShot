// 【役割】着陸地点のカーソル移動、説明、解放状態と選択確定を管理する。
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 着陸地点4箇所をキーボードまたはマウスで選び、
/// 説明UIと HelicopterAutoFlight の最終目的地を更新します。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class LandingPointSelectionUI : MonoBehaviour
{
    [Serializable]
    private class LandingPoint
    {
        [Header("地点UI")]
        [Tooltip("地点をクリックするためのButton")]
        public Button Button;
        [Tooltip("この地点を選んだ時のヘリ最終目的地")]
        public Transform DestinationTarget;
        [Tooltip("移動スキップ時のヘリ着陸位置と回転")]
        public Transform LandedPoseTarget;
        [Tooltip("着陸後のプレイヤー出現位置と回転")]
        public Transform PlayerSpawnTarget;
        [Header("説明文")]
        [Tooltip("例: POINT 01")]
        public string PointName = "POINT 01";
        [Tooltip("ステージ名。空欄の場合は標準名（廃病院／教会／コンビナート／ポイントゼロ）を使用します")]
        public string StageName;
        [TextArea(1, 3)]
        public string DangerLevel = "★☆☆";
        [TextArea(1, 3)]
        public string EnemyLevel = "★☆☆";
        [TextArea(1, 3)]
        public string RecommendedRoute = "★☆☆";
    }

    [Header("開始条件")]
    [SerializeField]
    private HelicopterAutoFlight helicopterAutoFlight;
    [Tooltip("オンなら、経由地点でホバリングを開始した時点で選択UIを開きます。隊長会話の後に開く場合はオフにしてOpenSelectionを呼んでください")]
    [SerializeField]
    private bool openWhenWaypointHoverStarts;
    [Header("カメラ切替")]
    [Tooltip("ステージ内のカメラ切替を一元管理するCameraManager")]
    [SerializeField]
    private CameraManager cameraManager;
    [Tooltip("ニュース・隊長説明・地点選択で使う監視カメラ")]
    [SerializeField]
    private Camera surveillanceCamera;
    [Tooltip("地点確定後、ヘリが最終目的地へ飛び始める時に使うフォローカメラ")]
    [SerializeField]
    private Camera followCamera;
    [Header("選択UI")]
    [SerializeField]
    private GameObject selectionPanel;
    [Header("UIフェード")]
    [Tooltip("MAP UI全体のCanvasGroup。地点選択の開始時にフェード表示します")]
    [SerializeField]
    private CanvasGroup mapUiCanvasGroup;
    [Tooltip("地点ボタン・選択マーカーの親CanvasGroup。MAPと同時にフェード表示します")]
    [SerializeField]
    private CanvasGroup pointSelectionCanvasGroup;
    [Tooltip("右側の説明パネル用CanvasGroup。MAP UIとは別にフェード表示します")]
    [SerializeField]
    private CanvasGroup descriptionPanelCanvasGroup;
    [Tooltip("MAP UIと説明パネルを表示するフェード時間（秒）")]
    [Min(0.01f)]
    [SerializeField]
    private float uiOpenFadeDuration = 0.35f;
    [Tooltip("MAP UIと説明パネルを閉じるフェード時間（秒）")]
    [Min(0.01f)]
    [SerializeField]
    private float uiCloseFadeDuration = 0.2f;
    [Header("地点確定後に消すUI")]
    [Tooltip("地点確定時に非表示にするコックピットUIフレームの親オブジェクト")]
    [SerializeField]
    private GameObject cockpitUiRoot;
    [Tooltip("コックピットUIの再表示を防止するためのOpeningNewsSequence")]
    [SerializeField]
    private OpeningNewsSequence openingNewsSequence;
    [Header("地点確定後の飛行開始待機")]
    [Tooltip("地点確定からフォローカメラ切替・最終目的地への飛行開始までに必ず待つ秒数")]
    [Min(0f)]
    [SerializeField]
    private float minimumWaitBeforeFlight = 3f;
    [Header("出発待機テキストUI")]
    [Tooltip("地点確定後にフェード表示する待機メッセージのCanvasGroup。Selection Panelの外に配置してください")]
    [SerializeField]
    private CanvasGroup departureWaitingCanvasGroup;
    [Tooltip("待機メッセージを表示するTextMeshPro。未設定ならCanvasGroupだけを表示します")]
    [SerializeField]
    private TMP_Text departureWaitingText;
    [TextArea(1, 3)]
    [SerializeField]
    private string departureWaitingMessage = "最終目的地への移動を開始します。\nしばらくお待ちください。";
    [Min(0.01f)]
    [SerializeField]
    private float departureWaitingFadeDuration = 0.35f;
    [Header("選択SE")]
    [Tooltip("左右キーで地点を切り替えた時に鳴らすSE")]
    [SerializeField]
    private AudioClip selectionMoveSe;
    [Tooltip("Enterキーまたはマウスクリックで地点を確定した時に鳴らすSE")]
    [SerializeField]
    private AudioClip selectionConfirmSe;
    [Range(0f, 1f)]
    [SerializeField]
    private float selectionSeVolume = 0.8f;
    [Tooltip("MAP UIと説明パネルを開き始める時に鳴らすSE")]
    [SerializeField]
    private AudioClip uiOpenSe;
    [Tooltip("MAP UIと説明パネルを閉じ始める時に鳴らすSE")]
    [SerializeField]
    private AudioClip uiCloseSe;
    [Range(0f, 1f)]
    [SerializeField]
    private float uiSeVolume = 0.8f;
    [Header("説明UI")]
    [SerializeField]
    private TMP_Text pointNameText;
    [Tooltip("説明パネルの空行に表示するステージ名。未設定ならPoint Nameから自動生成します")]
    [SerializeField]
    private TMP_Text stageNameText;
    [SerializeField]
    private Vector2 generatedStageNameOffset = new Vector2(0f, -54f);
    [SerializeField]
    private TMP_Text dangerLevelText;
    [SerializeField]
    private TMP_Text enemyLevelText;
    [SerializeField]
    private TMP_Text recommendedRouteText;
    [Header("未解放ステージUI")]
    [SerializeField]
    private TMP_Text lockedStageMessageText;
    [TextArea]
    [SerializeField]
    private string lockedStageMessage = "そのステージへは進めない";
    [SerializeField]
    private Vector2 lockedMessagePosition = new Vector2(0f, 180f);
    [Min(.1f)]
    [SerializeField]
    private float lockedMessageDuration = 1.6f;
    [Header("4つの着陸地点")]
    [SerializeField]
    private LandingPoint[] landingPoints = new LandingPoint[4];
    [Header("着陸時セーフティポイント")]
    [SerializeField]
    private LandingSafetyZone landingSafetyZone;
    private int selectedIndex;
    private bool isSelectionOpen;
    private bool isSelectionConfirmed;
    private Coroutine uiFadeCoroutine;
    private Coroutine departureWaitCoroutine;
    private Coroutine departureWaitingFadeCoroutine;
    private AudioSource audioSource;
    private Coroutine lockedMessageCoroutine;
    private static readonly string[] DefaultStageNames =
    {
        "廃病院",
        "教会",
        "コンビナート",
        "ポイントゼロ"
    };
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        RegisterPointButtons();
        DisableBuiltInButtonNavigation();
        EnsureAdditionalTexts();
        PrepareCameras();
        AllowSelectionUiToIgnoreCockpitFrame();
        if (selectionPanel != null)
        {
            selectionPanel.SetActive(false);
        }

        SetCanvasGroupVisible(mapUiCanvasGroup, false);
        SetCanvasGroupVisible(pointSelectionCanvasGroup, false);
        SetCanvasGroupVisible(descriptionPanelCanvasGroup, false);
        SetCanvasGroupVisible(departureWaitingCanvasGroup, false);
        SetIgnoreParentGroups(departureWaitingCanvasGroup);
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
        if (openWhenWaypointHoverStarts && helicopterAutoFlight != null && helicopterAutoFlight.CurrentPhase == HelicopterAutoFlight.FlightPhase.HoverAtWaypoint)
        {
            OpenSelection();
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (!isSelectionOpen)
        {
            return;
        }

        int horizontal = GameInputManager.Instance.DialogueHorizontal;
        int vertical = GameInputManager.Instance.DialogueVertical;
        if (horizontal != 0)
        {
            MoveSelectionSpatial(new Vector2(horizontal, 0f));
        }
        else if (vertical != 0)
        {
            MoveSelectionSpatial(new Vector2(0f, vertical));
        }

        if (GameInputManager.Instance.KeyboardConfirmPressed)
        {
            ConfirmSelection();
        }

        // 地点Button以外を左クリックした場合は、現在選択中の地点を決定します。
        // 地点Buttonの左クリックはButton側のイベントで「選択して決定」されます。
        if (GameInputManager.Instance.PointerConfirmPressed && !IsPointerOverLandingPointButton())
        {
            ConfirmSelection();
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
    /// 外部スクリプトやUnityEventから呼び出して、地点選択を表示します。
    /// </summary>
    public void OpenSelection()
    {
        if (!HasValidLandingPoints())
        {
            return;
        }

        isSelectionConfirmed = false;
        if (selectionPanel != null)
        {
            selectionPanel.SetActive(true);
        }

        SelectPoint(selectedIndex, false);
        if (uiFadeCoroutine != null)
        {
            StopCoroutine(uiFadeCoroutine);
        }

        uiFadeCoroutine = StartCoroutine(FadeInSelectionUi());
    }

    /// <summary>
    /// 地点選択を閉じます。確定は行いません。
    /// </summary>
    public void CloseSelection()
    {
        isSelectionOpen = false;
        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.SetDialogue(this, false);
        }

        if (uiFadeCoroutine != null)
        {
            StopCoroutine(uiFadeCoroutine);
            uiFadeCoroutine = null;
        }

        uiFadeCoroutine = StartCoroutine(FadeOutSelectionUi());
    }

    private void HandleWaypointHoverStarted()
    {
        if (openWhenWaypointHoverStarts)
        {
            OpenSelection();
        }
    }

    private void RegisterPointButtons()
    {
        if (landingPoints == null)
        {
            return;
        }

        for (int index = 0; index < landingPoints.Length; index++)
        {
            LandingPoint point = landingPoints[index];
            int capturedIndex = index;
            if (point != null && point.Button != null)
            {
                point.Button.onClick.AddListener(() => ConfirmPointByMouse(capturedIndex));
            }
        }
    }

    /// <summary>
    /// 地点選択の矢印・WASD入力はUpdate内で一元管理するため、
    /// EventSystemの標準Navigationによる二重移動を無効化します。
    /// </summary>
    private void DisableBuiltInButtonNavigation()
    {
        if (landingPoints == null)
        {
            return;
        }

        foreach (LandingPoint point in landingPoints)
        {
            if (point == null || point.Button == null)
            {
                continue;
            }

            Navigation navigation = point.Button.navigation;
            navigation.mode = Navigation.Mode.None;
            point.Button.navigation = navigation;
        }
    }

    private bool IsPointerOverLandingPointButton()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);
        foreach (RaycastResult result in results)
        {
            Button button = result.gameObject.GetComponentInParent<Button>();
            if (button == null || landingPoints == null)
            {
                continue;
            }

            foreach (LandingPoint point in landingPoints)
            {
                if (point != null && point.Button == button)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void MoveSelectionSpatial(Vector2 direction)
    {
        if (landingPoints == null || selectedIndex < 0 || selectedIndex >= landingPoints.Length)
        {
            return;
        }

        Button currentButton = landingPoints[selectedIndex]?.Button;
        if (currentButton == null)
        {
            return;
        }

        Vector2 currentPosition = currentButton.transform.position;
        int bestIndex = -1;
        float bestScore = float.MaxValue;
        for (int index = 0; index < landingPoints.Length; index++)
        {
            if (index == selectedIndex || landingPoints[index]?.Button == null)
            {
                continue;
            }

            Vector2 candidatePosition = landingPoints[index].Button.transform.position;
            Vector2 delta = candidatePosition - currentPosition;
            float forward = Vector2.Dot(delta, direction);
            if (forward <= .01f)
            {
                continue;
            }

            float side = Mathf.Abs(Vector2.Dot(delta, new Vector2(-direction.y, direction.x)));
            float score = forward + side * 2f;
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = index;
            }
        }

        if (bestIndex >= 0)
        {
            SelectPoint(bestIndex, true);
        }
    }

    private void ConfirmPointByMouse(int pointIndex)
    {
        if (!isSelectionOpen)
        {
            return;
        }

        SelectPoint(pointIndex, false);
        ConfirmSelection();
    }

    private void SelectPoint(int pointIndex, bool playMoveSe)
    {
        selectedIndex = Mathf.Clamp(pointIndex, 0, landingPoints.Length - 1);
        LandingPoint point = landingPoints[selectedIndex];
        SelectButton(point);
        UpdateDescription(point);
        if (playMoveSe && selectionMoveSe != null)
        {
            audioSource.PlayOneShot(selectionMoveSe, selectionSeVolume);
        }
    }

    private void PlayMoveSe()
    {
        if (selectionMoveSe != null)
        {
            audioSource.PlayOneShot(selectionMoveSe, selectionSeVolume);
        }
    }

    private void EnsureAdditionalTexts()
    {
        if (stageNameText == null && pointNameText != null)
        {
            stageNameText = Instantiate(pointNameText, pointNameText.transform.parent);
            stageNameText.name = "Stage Name";
            stageNameText.text = string.Empty;
            RectTransform rect = stageNameText.rectTransform;
            rect.anchoredPosition = pointNameText.rectTransform.anchoredPosition + generatedStageNameOffset;
        }

        if (lockedStageMessageText == null && pointNameText != null)
        {
            Canvas canvas = pointNameText.GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : pointNameText.transform.root;
            lockedStageMessageText = Instantiate(pointNameText, parent);
            lockedStageMessageText.name = "Locked Stage Message";
            lockedStageMessageText.alignment = TextAlignmentOptions.Center;
            lockedStageMessageText.raycastTarget = false;
            RectTransform rect = lockedStageMessageText.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = lockedMessagePosition;
            rect.sizeDelta = new Vector2(900f, 100f);
            lockedStageMessageText.gameObject.SetActive(false);
        }
    }

    private void ShowLockedMessage()
    {
        if (lockedStageMessageText == null)
        {
            return;
        }

        if (lockedMessageCoroutine != null)
        {
            StopCoroutine(lockedMessageCoroutine);
        }

        lockedMessageCoroutine = StartCoroutine(ShowLockedMessageRoutine());
    }

    private IEnumerator ShowLockedMessageRoutine()
    {
        lockedStageMessageText.text = lockedStageMessage;
        lockedStageMessageText.alpha = 1f;
        lockedStageMessageText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(lockedMessageDuration);
        lockedStageMessageText.gameObject.SetActive(false);
        lockedMessageCoroutine = null;
    }

    public void ResumeAtSavedLanding()
    {
        if (landingPoints == null || landingPoints.Length == 0 || helicopterAutoFlight == null)
        {
            return;
        }

        selectedIndex = Mathf.Clamp(GameProgressData.LastLandingIndex, 0, landingPoints.Length - 1);
        LandingPoint point = landingPoints[selectedIndex];
        if (point == null)
        {
            return;
        }

        helicopterAutoFlight.DestinationTarget = point.DestinationTarget;
        helicopterAutoFlight.LandingPoseTarget = point.LandedPoseTarget;
        helicopterAutoFlight.PlayerSpawnTarget = point.PlayerSpawnTarget;
        helicopterAutoFlight.ForceCompleteAtLandingPose();
    }

    /// <summary>
    /// UnityのEventSystemにButtonを選択させ、Button標準のSelected色を表示します。
    /// </summary>
    private void SelectButton(LandingPoint point)
    {
        if (point == null || point.Button == null)
        {
            return;
        }

        if (EventSystem.current == null)
        {
            Debug.LogWarning("[LandingPointSelectionUI] EventSystemがないためButtonを選択できません。", this);
            return;
        }

        EventSystem.current.SetSelectedGameObject(point.Button.gameObject);
    }

    private void UpdateDescription(LandingPoint point)
    {
        if (point == null)
        {
            return;
        }

        if (pointNameText != null)
        {
            pointNameText.text = point.PointName;
        }

        if (stageNameText != null)
        {
            bool unlocked = GameProgressData.IsStageUnlocked(selectedIndex);
            string configured = string.IsNullOrWhiteSpace(point.StageName) && selectedIndex < DefaultStageNames.Length ? DefaultStageNames[selectedIndex] : point.StageName;
            stageNameText.text = unlocked ? configured : "???";
        }

        if (dangerLevelText != null)
        {
            dangerLevelText.text = point.DangerLevel;
        }

        if (enemyLevelText != null)
        {
            enemyLevelText.text = point.EnemyLevel;
        }

        if (recommendedRouteText != null)
        {
            recommendedRouteText.text = point.RecommendedRoute;
        }
    }

    private void ConfirmSelection()
    {
        if (isSelectionConfirmed)
        {
            return;
        }

        LandingPoint point = landingPoints[selectedIndex];
        if (!GameProgressData.IsStageUnlocked(selectedIndex))
        {
            ShowLockedMessage();
            PlayMoveSe();
            return;
        }

        if (point == null || point.DestinationTarget == null)
        {
            Debug.LogWarning("[LandingPointSelectionUI] 選択地点のDestination Targetを設定してください。", this);
            return;
        }

        if (helicopterAutoFlight == null)
        {
            Debug.LogWarning("[LandingPointSelectionUI] HelicopterAutoFlightが未設定です。", this);
            return;
        }

        helicopterAutoFlight.DestinationTarget = point.DestinationTarget;
        helicopterAutoFlight.LandingPoseTarget = point.LandedPoseTarget;
        helicopterAutoFlight.PlayerSpawnTarget = point.PlayerSpawnTarget;
        GameProgressData.SaveLandingSelection(selectedIndex);
        if (landingSafetyZone != null)
        {
            Vector3 safetyDirection = point.PlayerSpawnTarget != null ? point.PlayerSpawnTarget.position - point.DestinationTarget.position : point.DestinationTarget.forward;
            safetyDirection.y = 0f;
            landingSafetyZone.Configure(point.DestinationTarget.position, safetyDirection);
        }

        PlayConfirmSe();
        isSelectionConfirmed = true;
        isSelectionOpen = false;
        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.SetDialogue(this, false);
        }

        // MAP UIを閉じ、出発待機メッセージを表示してから実際の到着を待ちます。
        CloseSelection();
        ShowDepartureWaitingMessage();
        departureWaitCoroutine = StartCoroutine(WaitForWaypointThenBeginFlight());
    }

    /// <summary>
    /// 地点確定後、最低待機時間と経由地点でのホバリング開始の両方を満たしてから
    /// カメラ切替・UIフェードアウト・最終目的地への飛行を開始します。
    /// </summary>
    private IEnumerator WaitForWaypointThenBeginFlight()
    {
        float confirmedTime = Time.time;
        while (helicopterAutoFlight.CurrentPhase != HelicopterAutoFlight.FlightPhase.HoverAtWaypoint)
        {
            yield return null;
        }

        float remainingWait = Mathf.Max(0f, minimumWaitBeforeFlight - (Time.time - confirmedTime));
        if (remainingWait > 0f)
        {
            yield return new WaitForSeconds(remainingWait);
        }

        Debug.Log("[LandingPointSelectionUI] 経由地点到着と待機時間を確認。" + "最終目的地への飛行を開始します。", this);
        yield return StartCoroutine(HideDepartureWaitingMessage());
        HideCockpitUi();
        SwitchToFollowCamera();
        helicopterAutoFlight.ResumeFlightFromWaypoint = true;
        departureWaitCoroutine = null;
    }

    private void ShowDepartureWaitingMessage()
    {
        if (departureWaitingCanvasGroup == null)
        {
            return;
        }

        if (departureWaitingText != null)
        {
            departureWaitingText.text = departureWaitingMessage;
        }

        if (departureWaitingFadeCoroutine != null)
        {
            StopCoroutine(departureWaitingFadeCoroutine);
        }

        departureWaitingCanvasGroup.alpha = 0f;
        departureWaitingCanvasGroup.gameObject.SetActive(true);
        departureWaitingFadeCoroutine = StartCoroutine(FadeCanvasGroup(departureWaitingCanvasGroup, 0f, 1f, false));
    }

    private IEnumerator HideDepartureWaitingMessage()
    {
        if (departureWaitingCanvasGroup == null)
        {
            yield break;
        }

        if (departureWaitingFadeCoroutine != null)
        {
            StopCoroutine(departureWaitingFadeCoroutine);
        }

        yield return StartCoroutine(FadeCanvasGroup(departureWaitingCanvasGroup, 1f, 0f, true));
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float from, float to, bool disableAfterFade)
    {
        float elapsedTime = 0f;
        while (elapsedTime < departureWaitingFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / departureWaitingFadeDuration);
            canvasGroup.alpha = Mathf.Lerp(from, to, progress);
            yield return null;
        }

        canvasGroup.alpha = to;
        if (disableAfterFade)
        {
            canvasGroup.gameObject.SetActive(false);
        }

        departureWaitingFadeCoroutine = null;
    }

    /// <summary>
    /// 地点決定後、監視用のコックピットUIを非表示にします。
    /// </summary>
    private void HideCockpitUi()
    {
        if (openingNewsSequence != null)
        {
            openingNewsSequence.HideCockpitFrameForFlight();
            return;
        }

        // OpeningNewsSequence未登録時の代替処理です。
        // 親を無効化せず、コックピットのCanvasGroupだけを透明にします。
        if (cockpitUiRoot != null)
        {
            CanvasGroup cockpitCanvasGroup = cockpitUiRoot.GetComponent<CanvasGroup>();
            if (cockpitCanvasGroup != null)
            {
                cockpitCanvasGroup.alpha = 0f;
                cockpitCanvasGroup.interactable = false;
                cockpitCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                cockpitUiRoot.SetActive(false);
            }
        }
    }

    private void PlayConfirmSe()
    {
        if (selectionConfirmSe != null)
        {
            audioSource.PlayOneShot(selectionConfirmSe, selectionSeVolume);
        }
    }

    /// <summary>
    /// MAPと説明パネルを同時にフェード表示し、完了後に入力を有効化します。
    /// </summary>
    private IEnumerator FadeInSelectionUi()
    {
        SetCanvasGroupVisible(mapUiCanvasGroup, true);
        SetCanvasGroupVisible(pointSelectionCanvasGroup, true);
        SetCanvasGroupVisible(descriptionPanelCanvasGroup, true);
        PlayUiSe(uiOpenSe);
        float elapsedTime = 0f;
        while (elapsedTime < uiOpenFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / uiOpenFadeDuration);
            SetCanvasGroupAlpha(mapUiCanvasGroup, progress);
            SetCanvasGroupAlpha(pointSelectionCanvasGroup, progress);
            SetCanvasGroupAlpha(descriptionPanelCanvasGroup, progress);
            yield return null;
        }

        SetCanvasGroupAlpha(mapUiCanvasGroup, 1f);
        SetCanvasGroupAlpha(pointSelectionCanvasGroup, 1f);
        SetCanvasGroupAlpha(descriptionPanelCanvasGroup, 1f);
        isSelectionOpen = true;
        GameInputManager.Instance.SetDialogue(this, true);
        uiFadeCoroutine = null;
    }

    /// <summary>
    /// MAPと説明パネルを同時にフェード非表示し、完了後に親UIを閉じます。
    /// </summary>
    private IEnumerator FadeOutSelectionUi()
    {
        PlayUiSe(uiCloseSe);
        float elapsedTime = 0f;
        while (elapsedTime < uiCloseFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(elapsedTime / uiCloseFadeDuration);
            SetCanvasGroupAlpha(mapUiCanvasGroup, progress);
            SetCanvasGroupAlpha(pointSelectionCanvasGroup, progress);
            SetCanvasGroupAlpha(descriptionPanelCanvasGroup, progress);
            yield return null;
        }

        SetCanvasGroupVisible(mapUiCanvasGroup, false);
        SetCanvasGroupVisible(pointSelectionCanvasGroup, false);
        SetCanvasGroupVisible(descriptionPanelCanvasGroup, false);
        if (selectionPanel != null)
        {
            selectionPanel.SetActive(false);
        }

        uiFadeCoroutine = null;
    }

    private void SetCanvasGroupVisible(CanvasGroup canvasGroup, bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.gameObject.SetActive(visible);
    }

    /// <summary>
    /// MAP・説明・地点ButtonはコックピットUIの子要素でも、
    /// コックピットフレームの透明度に影響されず表示できるようにします。
    /// </summary>
    private void AllowSelectionUiToIgnoreCockpitFrame()
    {
        SetIgnoreParentGroups(mapUiCanvasGroup);
        SetIgnoreParentGroups(pointSelectionCanvasGroup);
        SetIgnoreParentGroups(descriptionPanelCanvasGroup);
    }

    private void SetIgnoreParentGroups(CanvasGroup canvasGroup)
    {
        if (canvasGroup != null)
        {
            canvasGroup.ignoreParentGroups = true;
        }
    }

    private void SetCanvasGroupAlpha(CanvasGroup canvasGroup, float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
    }

    private void PlayUiSe(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.PlayOneShot(clip, uiSeVolume);
        }
    }

    /// <summary>
    /// 地点確定の瞬間だけ、監視カメラからフォローカメラへ切り替えます。
    /// </summary>
    private void SwitchToFollowCamera()
    {
        if (cameraManager != null)
        {
            cameraManager.SwitchToHelicopterCamera();
            return;
        }

        if (followCamera == null)
        {
            Debug.LogWarning("[LandingPointSelectionUI] Follow Cameraが未設定です。", this);
            return;
        }

        if (followCamera == surveillanceCamera)
        {
            return;
        }

        if (surveillanceCamera != null)
        {
            followCamera.depth = surveillanceCamera.depth + 1f;
        }

        followCamera.enabled = true;
        if (surveillanceCamera != null)
        {
            surveillanceCamera.enabled = false;
        }
    }

    /// <summary>
    /// OP中は監視カメラだけを映し、フォローカメラは地点確定まで待機させます。
    /// </summary>
    private void PrepareCameras()
    {
        if (cameraManager != null)
        {
            cameraManager.SwitchToSurveillanceCamera();
            return;
        }

        if (surveillanceCamera != null)
        {
            surveillanceCamera.enabled = true;
        }

        if (followCamera != null && followCamera != surveillanceCamera)
        {
            followCamera.enabled = false;
        }
    }

    private bool HasValidLandingPoints()
    {
        if (landingPoints == null || landingPoints.Length != 4)
        {
            Debug.LogError("[LandingPointSelectionUI] 着陸地点を4つ登録してください。", this);
            return false;
        }

        for (int index = 0; index < landingPoints.Length; index++)
        {
            if (landingPoints[index] == null)
            {
                Debug.LogError("[LandingPointSelectionUI] 未設定の着陸地点があります。", this);
                return false;
            }
        }

        return true;
    }
}
