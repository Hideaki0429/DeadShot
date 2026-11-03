// 【役割】着陸後のチュートリアル選択、操作解放と本編開始を管理する。
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityChan;
using UnityEngine;
using UnityEngine.AI;

public enum TutorialObjectiveType
{
    Confirm,
    MoveDistance,
    PressKey,
    Fire,
    DefeatTutorialEnemy
}

[System.Serializable]
public class TutorialInstruction
{
    [TextArea(2, 5)]
    public string captainMessage;
    [TextArea(1, 3)]
    public string controlHint;
    public TutorialObjectiveType objectiveType;
    [Tooltip("PressKeyで待機するキー")]
    public KeyCode requiredKey = KeyCode.None;
    [Tooltip("MoveDistanceで必要な移動距離")]
    [Min(0.1f)]
    public float requiredMoveDistance = 3f;
    [Tooltip("この指示の開始時にチュートリアル用ゾンビを生成")]
    public bool spawnTutorialEnemy;
}

[RequireComponent(typeof(AudioSource))]
public class TutorialUIManager2 : MonoBehaviour
{
    [Header("選択結果の受信")]
    [Tooltip("選択結果を直接確認するCaptainHoverDialogue。イベント未登録時の予備参照にも使用")]
    [SerializeField]
    private CaptainHoverDialogue captainDialogue;
    [Header("プレイヤー")]
    public UnityChanControlScriptWithRgidBody playerScript;
    [Tooltip("操作開始後に有効化するHPフィルター。被ダメージ動画もこの状態に連動します")]
    [SerializeField]
    private PlayerDamageScreenEffect damageScreenEffect;
    [Tooltip("チュートリアル完了後に転送する本編開始地点")]
    [SerializeField]
    private Transform mainGameStartPoint;
    [Header("タイトルUI")]
    [SerializeField]
    private CanvasGroup titleCanvasGroup;
    public TextMeshProUGUI headerText;
    [Tooltip("Enterまたは左クリックの案内表示")]
    public TextMeshProUGUI ruleBodyText;
    [SerializeField]
    private string tutorialTitle = "チュートリアル";
    [SerializeField]
    private string startPrompt = "Enterキーまたは左クリックで開始";
    [Header("隊長指示UI")]
    [SerializeField]
    private CanvasGroup instructionCanvasGroup;
    [SerializeField]
    private TextMeshProUGUI captainNameText;
    [Tooltip("旧Control Body Textも指示本文として使用できます")]
    public TextMeshProUGUI controlBodyText;
    [SerializeField]
    private TextMeshProUGUI controlHintText;
    [SerializeField]
    private string captainName = "第1部隊長　長谷川";
    [Header("隊長指示の文字送り")]
    [Tooltip("1文字を表示する間隔")]
    [Min(0.001f)]
    [SerializeField]
    private float instructionCharacterInterval = 0.05f;
    [Tooltip("何文字ごとにSEを鳴らすか")]
    [Min(1)]
    [SerializeField]
    private int instructionTypingSeInterval = 2;
    [SerializeField]
    private AudioClip instructionTypingSe;
    [Range(0f, 1f)]
    [SerializeField]
    private float instructionTypingSeVolume = 0.5f;
    [Tooltip("チュートリアル完了メッセージにも文字送りを使用")]
    [SerializeField]
    private bool typeCompletedMessage = true;
    [Header("チュートリアル手順")]
    [SerializeField]
    private TutorialInstruction[] tutorialInstructions;
    [Header("チュートリアル用ゾンビ")]
    [SerializeField]
    private GameObject tutorialZombiePrefab;
    [Min(1)]
    [SerializeField]
    private int tutorialZombieCount = 1;
    [Header("動的スポーン位置")]
    [Tooltip("プレイヤーから最低限離す距離。セーフティゾーン半径より大きくしてください")]
    [Min(1f)]
    [SerializeField]
    private float tutorialSpawnMinDistance = 12f;
    [Tooltip("プレイヤーからの最大距離")]
    [Min(1f)]
    [SerializeField]
    private float tutorialSpawnMaxDistance = 18f;
    [Tooltip("プレイヤー正面を中心に左右何度まで候補にするか")]
    [Range(0f, 180f)]
    [SerializeField]
    private float tutorialSpawnAngle = 65f;
    [Tooltip("NavMesh上の候補地点を探す半径")]
    [Min(0.1f)]
    [SerializeField]
    private float navMeshSampleRadius = 3f;
    [Tooltip("オンの場合、NavMesh上にだけゾンビを生成します。NavMesh未作成のシーンではオフにしてください")]
    [SerializeField]
    private bool requireNavMeshForTutorialSpawn;
    [Tooltip("地面を検出するLayer")]
    [SerializeField]
    private LayerMask tutorialSpawnGroundLayer = ~0;
    [Tooltip("建物など、出現位置との重なりを避けるLayer")]
    [SerializeField]
    private LayerMask tutorialSpawnObstacleLayer;
    [Tooltip("ゾンビを置ける空間があるか確認する半径")]
    [Min(0.1f)]
    [SerializeField]
    private float spawnClearanceRadius = 1f;
    [Tooltip("出現位置を探す最大回数")]
    [Min(1)]
    [SerializeField]
    private int spawnSearchAttempts = 20;
    [SerializeField]
    private LandingSafetyZone landingSafetyZone;
    [Header("本編の敵生成")]
    [SerializeField]
    private SpawnManager mainEnemySpawner;
    [Header("ブラックアウト")]
    [Tooltip("全面黒Imageを持つCanvasGroup")]
    [SerializeField]
    private CanvasGroup blackScreenCanvasGroup;
    [Header("時間・演出設定")]
    [Min(0f)]
    public float fadeDuration = 0.5f;
    [Min(0f)]
    [SerializeField]
    private float completedMessageDuration = 1.5f;
    [TextArea(1, 3)]
    [SerializeField]
    private string completedMessage = "よくやった。チュートリアルは終了だ。";
    [Header("【操作解禁フラグ】")]
    public bool isMovementUnlocked;
    public bool isAimUnlocked;
    public bool isFireUnlocked;
    public bool isCameraUnlocked;
    public bool HasReceivedTutorialChoice => hasReceivedTutorialChoice;
    public bool ShouldRunTutorial => shouldRunTutorial;
    public bool IsTutorialRunning => tutorialRunning;

    private readonly List<GameObject> tutorialEnemies = new List<GameObject>();
    private bool hasReceivedTutorialChoice;
    private bool shouldRunTutorial;
    private bool sequenceStarted;
    private bool tutorialRunning;
    private Vector3 tutorialLandingOrigin;
    private AudioSource tutorialAudioSource;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        tutorialAudioSource = GetComponent<AudioSource>();
        if (tutorialAudioSource == null)
        {
            tutorialAudioSource = gameObject.AddComponent<AudioSource>();
        }

        tutorialAudioSource.playOnAwake = false;
        tutorialAudioSource.loop = false;
        if (captainDialogue == null)
        {
            captainDialogue = FindObjectOfType<CaptainHoverDialogue>();
        }

        if (mainEnemySpawner == null)
        {
            mainEnemySpawner = FindObjectOfType<SpawnManager>();
        }

        if (landingSafetyZone == null)
        {
            landingSafetyZone = FindObjectOfType<LandingSafetyZone>();
        }

        // 子TextMeshProは不透明に保ち、表示・非表示は
        // 親CanvasGroupだけで管理する。
        SetTextAlpha(headerText, 1f);
        SetTextAlpha(ruleBodyText, 1f);
        SetTextAlpha(captainNameText, 1f);
        SetTextAlpha(controlBodyText, 1f);
        SetTextAlpha(controlHintText, 1f);
        PrepareCanvasGroup(titleCanvasGroup);
        PrepareCanvasGroup(instructionCanvasGroup);
        PrepareCanvasGroup(blackScreenCanvasGroup);
        if (blackScreenCanvasGroup != null)
        {
            blackScreenCanvasGroup.gameObject.SetActive(true);
            blackScreenCanvasGroup.alpha = 0f;
            blackScreenCanvasGroup.blocksRaycasts = false;
        }

        if (playerScript != null)
        {
            playerScript.canControl = false;
        }

        if (mainEnemySpawner != null)
        {
            mainEnemySpawner.SetSpawnOnStart(false);
        }
    }

    /// <summary>CaptainHoverDialogueのUnityEventから登録。YES=true、NO=false。</summary>
    public void ReceiveTutorialChoice(bool runTutorial)
    {
        hasReceivedTutorialChoice = true;
        shouldRunTutorial = runTutorial;
        Debug.Log(runTutorial ? "[TutorialGM] YESを受信しました。" : "[TutorialGM] NOを受信しました。", this);
    }

    /// <summary>PlayerDismountDirectorから着地待ち時間終了後に呼び出します。</summary>
    public void BeginAfterLanding()
    {
        if (sequenceStarted)
        {
            return;
        }

        sequenceStarted = true;
        if (playerScript != null)
        {
            tutorialLandingOrigin = playerScript.transform.position;
        }

        if (!hasReceivedTutorialChoice && captainDialogue != null && captainDialogue.HasTutorialDecision)
        {
            ReceiveTutorialChoice(captainDialogue.ShouldRunTutorial);
        }

        if (hasReceivedTutorialChoice && shouldRunTutorial)
        {
            StartCoroutine(TutorialSequence());
        }
        else
        {
            if (!hasReceivedTutorialChoice)
            {
                Debug.LogWarning("[TutorialGM] 選択結果がないためチュートリアルを省略します。", this);
            }

            StartCoroutine(StartMainGameSequence());
        }
    }

    private IEnumerator TutorialSequence()
    {
        tutorialRunning = true;
        GameInputManager.Instance.SetDialogue(this, true);
        LockAllPlayerActions();
        if (headerText != null)
        {
            headerText.text = tutorialTitle;
        }

        if (ruleBodyText != null)
        {
            ruleBodyText.text = startPrompt;
        }

        yield return SetTitleVisible(true);
        yield return new WaitUntil(IsConfirmPressed);
        yield return SetTitleVisible(false);
        EnableDamageEffects();
        GameInputManager.Instance.SetDialogue(this, false);
        isMovementUnlocked = true;
        if (playerScript != null)
        {
            playerScript.canControl = true;
        }

        if (tutorialInstructions != null)
        {
            foreach (TutorialInstruction instruction in tutorialInstructions)
            {
                if (instruction == null)
                {
                    continue;
                }

                ApplyUnlockForObjective(instruction.objectiveType);
                if (instruction.spawnTutorialEnemy || (instruction.objectiveType == TutorialObjectiveType.DefeatTutorialEnemy && !HasLiveTutorialEnemy()))
                {
                    SpawnTutorialEnemies();
                }

                GameInputManager.Instance.SetDialogue(this, instruction.objectiveType == TutorialObjectiveType.Confirm);
                yield return ShowInstruction(instruction);
                Vector3 startPosition = playerScript != null ? playerScript.transform.position : Vector3.zero;
                yield return new WaitUntil(() => IsInstructionCompleted(instruction, startPosition));
                GameInputManager.Instance.SetDialogue(this, false);
                yield return SetInstructionVisible(false);
            }
        }

        if (controlBodyText != null)
        {
            controlBodyText.text = completedMessage;
            controlBodyText.maxVisibleCharacters = typeCompletedMessage ? 0 : int.MaxValue;
        }

        if (controlHintText != null)
        {
            controlHintText.text = string.Empty;
        }

        yield return SetInstructionVisible(true);
        if (typeCompletedMessage)
        {
            yield return TypeInstructionText(completedMessage);
        }

        yield return new WaitForSeconds(completedMessageDuration);
        yield return SetInstructionVisible(false);
        yield return CompleteTutorialAndStartMainGame();
    }

    private IEnumerator StartMainGameSequence()
    {
        CleanupTutorialEnemies();
        if (mainEnemySpawner != null)
        {
            mainEnemySpawner.SpawnConfiguredEnemies();
        }

        StartMainGame();
        yield break;
    }

    private IEnumerator CompleteTutorialAndStartMainGame()
    {
        LockAllPlayerActions();
        yield return FadeCanvasGroup(blackScreenCanvasGroup, 1f, true);
        CleanupTutorialEnemies();
        MovePlayerToMainStartPoint();
        if (ScoreAndTimeManager.instance != null)
        {
            ScoreAndTimeManager.instance.ResetForMainGame();
        }

        if (mainEnemySpawner != null)
        {
            mainEnemySpawner.SpawnConfiguredEnemies();
        }

        yield return FadeCanvasGroup(blackScreenCanvasGroup, 0f, false);
        tutorialRunning = false;
        StartMainGame();
    }

    private void StartMainGame()
    {
        EnableDamageEffects();
        GameInputManager.Instance.SetDialogue(this, false);
        isMovementUnlocked = true;
        isAimUnlocked = true;
        isFireUnlocked = true;
        isCameraUnlocked = true;
        if (playerScript != null)
        {
            playerScript.canControl = true;
        }

        if (ScoreAndTimeManager.instance != null)
        {
            ScoreAndTimeManager.instance.StartGameFromTutorial();
        }
        else
        {
            Debug.LogWarning("[TutorialGM] ScoreAndTimeManagerが見つかりません。", this);
        }

        Debug.Log("[TutorialGM] 本編を開始しました。", this);
    }

    private void EnableDamageEffects()
    {
        if (damageScreenEffect == null)
        {
            damageScreenEffect = FindObjectOfType<PlayerDamageScreenEffect>(true);
        }

        if (damageScreenEffect != null)
        {
            damageScreenEffect.SetEffectEnabled(true);
        }
        else
        {
            Debug.LogWarning("[TutorialGM] ダメージフィルターが未登録です。", this);
        }
    }

    private IEnumerator ShowInstruction(TutorialInstruction instruction)
    {
        if (captainNameText != null)
        {
            captainNameText.text = captainName;
        }

        if (controlBodyText != null)
        {
            controlBodyText.text = instruction.captainMessage;
            controlBodyText.maxVisibleCharacters = 0;
        }

        if (controlHintText != null)
        {
            controlHintText.text = instruction.controlHint;
        }

        yield return SetInstructionVisible(true);
        yield return TypeInstructionText(instruction.captainMessage);
    }

    private IEnumerator TypeInstructionText(string message)
    {
        if (controlBodyText == null)
        {
            yield break;
        }

        if (message == null)
        {
            message = string.Empty;
        }

        controlBodyText.text = message;
        controlBodyText.maxVisibleCharacters = 0;
        controlBodyText.ForceMeshUpdate();
        int characterCount = controlBodyText.textInfo.characterCount;
        for (int index = 0; index < characterCount; index++)
        {
            controlBodyText.maxVisibleCharacters = index + 1;
            if (ShouldPlayInstructionTypingSe(index, message))
            {
                tutorialAudioSource.PlayOneShot(instructionTypingSe, instructionTypingSeVolume);
            }

            yield return new WaitForSeconds(instructionCharacterInterval);
        }

        controlBodyText.maxVisibleCharacters = int.MaxValue;
    }

    private bool ShouldPlayInstructionTypingSe(int characterIndex, string message)
    {
        if (tutorialAudioSource == null || instructionTypingSe == null)
        {
            return false;
        }

        if (characterIndex % Mathf.Max(1, instructionTypingSeInterval) != 0)
        {
            return false;
        }

        if (characterIndex >= message.Length)
        {
            return false;
        }

        char character = message[characterIndex];
        return !char.IsWhiteSpace(character) && character != '、' && character != '。' && character != '！' && character != '？';
    }

    private bool IsInstructionCompleted(TutorialInstruction instruction, Vector3 stepStartPosition)
    {
        switch (instruction.objectiveType)
        {
            case TutorialObjectiveType.Confirm:
                return IsConfirmPressed();
            case TutorialObjectiveType.MoveDistance:
                return playerScript != null && Vector3.Distance(playerScript.transform.position, stepStartPosition) >= instruction.requiredMoveDistance;
            case TutorialObjectiveType.PressKey:
                return instruction.requiredKey != KeyCode.None && GameInputManager.Instance.Pressed(instruction.requiredKey == KeyCode.C || instruction.requiredKey == KeyCode.Mouse1 ? GameInputManager.Instance.aim : instruction.requiredKey == KeyCode.Q || instruction.requiredKey == KeyCode.E ? GameInputManager.Instance.changeView : instruction.requiredKey);
            case TutorialObjectiveType.Fire:
                return GameInputManager.Instance.AttackPressed;
            case TutorialObjectiveType.DefeatTutorialEnemy:
                return !HasLiveTutorialEnemy();
            default:
                return false;
        }
    }

    private void ApplyUnlockForObjective(TutorialObjectiveType objectiveType)
    {
        if (objectiveType == TutorialObjectiveType.Fire || objectiveType == TutorialObjectiveType.DefeatTutorialEnemy)
        {
            isAimUnlocked = true;
            isFireUnlocked = true;
        }

        if (objectiveType == TutorialObjectiveType.PressKey)
        {
            isCameraUnlocked = true;
        }
    }

    private void SpawnTutorialEnemies()
    {
        if (HasLiveTutorialEnemy())
        {
            return;
        }

        CleanupTutorialEnemies();
        if (tutorialZombiePrefab == null)
        {
            Debug.LogError("[TutorialGM] Tutorial Zombie Prefabが未設定です。", this);
            return;
        }

        int count = Mathf.Max(1, tutorialZombieCount);
        for (int i = 0; i < count; i++)
        {
            if (!TryFindTutorialSpawnPosition(out Vector3 spawnPosition))
            {
                Debug.LogError("[TutorialGM] 安全なゾンビ出現地点を" + "見つけられませんでした。", this);
                continue;
            }

            Vector3 lookDirection = GetCurrentPlayerPosition() - spawnPosition;
            lookDirection.y = 0f;
            Quaternion spawnRotation = lookDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;
            GameObject enemy = Instantiate(tutorialZombiePrefab, spawnPosition, spawnRotation);
            tutorialEnemies.Add(enemy);
            Debug.Log($"[TutorialGM] ゾンビ出現位置:{spawnPosition} " + $"Player距離:" + $"{Vector3.Distance(GetCurrentPlayerPosition(), spawnPosition):F1}m", enemy);
        }

        Debug.Log($"[TutorialGM] チュートリアル用ゾンビを{tutorialEnemies.Count}体生成しました。", this);
    }

    private bool TryFindTutorialSpawnPosition(out Vector3 spawnPosition)
    {
        Vector3 playerPosition = GetCurrentPlayerPosition();
        Vector3 playerForward = playerScript != null ? playerScript.transform.forward : Vector3.forward;
        playerForward.y = 0f;
        if (playerForward.sqrMagnitude < 0.001f)
        {
            playerForward = Vector3.forward;
        }

        playerForward.Normalize();
        float minimumDistance = tutorialSpawnMinDistance;
        if (landingSafetyZone != null && landingSafetyZone.IsZoneActive)
        {
            minimumDistance = Mathf.Max(minimumDistance, landingSafetyZone.SafetyRadius + 1f);
        }

        float maximumDistance = Mathf.Max(tutorialSpawnMaxDistance, minimumDistance + 1f);
        for (int attempt = 0; attempt < Mathf.Max(1, spawnSearchAttempts); attempt++)
        {
            float angle = Random.Range(-tutorialSpawnAngle, tutorialSpawnAngle);
            float distance = Random.Range(minimumDistance, maximumDistance);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * playerForward;
            Vector3 candidate = playerPosition + direction * distance;
            if (!TryPlaceCandidateOnGround(candidate, out Vector3 groundedPosition))
            {
                continue;
            }

            if (!IsOutsideLandingSafetyZone(groundedPosition, 1f))
            {
                continue;
            }

            Vector3 clearanceCenter = groundedPosition + Vector3.up * spawnClearanceRadius;
            if (Physics.CheckSphere(clearanceCenter, spawnClearanceRadius, tutorialSpawnObstacleLayer, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            spawnPosition = groundedPosition;
            return true;
        }

        Vector3 fallback = playerPosition + playerForward * ((minimumDistance + maximumDistance) * 0.5f);
        if (TryPlaceCandidateOnGround(fallback, out spawnPosition) && IsOutsideLandingSafetyZone(spawnPosition, 1f))
        {
            return true;
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    private bool TryPlaceCandidateOnGround(Vector3 candidate, out Vector3 groundedPosition)
    {
        Vector3 rayOrigin = candidate + Vector3.up * 20f;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, 50f, tutorialSpawnGroundLayer, QueryTriggerInteraction.Ignore))
        {
            groundedPosition = Vector3.zero;
            return false;
        }

        if (NavMesh.SamplePosition(groundHit.point, out NavMeshHit navMeshHit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            groundedPosition = navMeshHit.position;
            return true;
        }

        // NavMeshを使用していないシーンでは、
        // Raycastで取得した実際の地面位置を採用する。
        if (!requireNavMeshForTutorialSpawn)
        {
            groundedPosition = groundHit.point;
            return true;
        }

        groundedPosition = Vector3.zero;
        return false;
    }

    private bool IsOutsideLandingSafetyZone(Vector3 position, float margin)
    {
        if (landingSafetyZone == null || !landingSafetyZone.IsZoneActive)
        {
            return true;
        }

        Vector3 offset = position - landingSafetyZone.ZoneCenter;
        offset.y = 0f;
        return offset.magnitude >= landingSafetyZone.SafetyRadius + margin;
    }

    private Vector3 GetCurrentPlayerPosition()
    {
        return playerScript != null ? playerScript.transform.position : tutorialLandingOrigin;
    }

    private bool HasLiveTutorialEnemy()
    {
        foreach (GameObject enemy in tutorialEnemies)
        {
            if (enemy != null)
            {
                return true;
            }
        }

        return false;
    }

    private void CleanupTutorialEnemies()
    {
        foreach (GameObject enemy in tutorialEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }

        tutorialEnemies.Clear();
    }

    private void MovePlayerToMainStartPoint()
    {
        if (playerScript == null || mainGameStartPoint == null)
        {
            Debug.LogWarning("[TutorialGM] Playerまたは本編開始地点が未設定です。", this);
            return;
        }

        Rigidbody body = playerScript.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        playerScript.transform.SetPositionAndRotation(mainGameStartPoint.position, mainGameStartPoint.rotation);
        Physics.SyncTransforms();
    }

    private void LockAllPlayerActions()
    {
        isMovementUnlocked = false;
        isAimUnlocked = false;
        isFireUnlocked = false;
        isCameraUnlocked = false;
        if (playerScript != null)
        {
            playerScript.canControl = false;
        }
    }

    private bool IsConfirmPressed()
    {
        return GameInputManager.Instance.ConfirmPressed;
    }

    // コンポーネントが無効になったときの処理です。
    private void OnDisable()
    {
        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.SetDialogue(this, false);
        }
    }

    private IEnumerator SetTitleVisible(bool visible)
    {
        if (visible)
        {
            SetTextAlpha(headerText, 1f);
            SetTextAlpha(ruleBodyText, 1f);
        }

        if (titleCanvasGroup != null)
        {
            yield return FadeCanvasGroup(titleCanvasGroup, visible ? 1f : 0f, visible);
            yield break;
        }

        CrossFadeText(headerText, visible);
        CrossFadeText(ruleBodyText, visible);
        yield return new WaitForSeconds(fadeDuration);
    }

    private IEnumerator SetInstructionVisible(bool visible)
    {
        if (visible)
        {
            SetTextAlpha(captainNameText, 1f);
            SetTextAlpha(controlBodyText, 1f);
            SetTextAlpha(controlHintText, 1f);
        }

        if (instructionCanvasGroup != null)
        {
            yield return FadeCanvasGroup(instructionCanvasGroup, visible ? 1f : 0f, visible);
            yield break;
        }

        CrossFadeText(captainNameText, visible);
        CrossFadeText(controlBodyText, visible);
        CrossFadeText(controlHintText, visible);
        yield return new WaitForSeconds(fadeDuration);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float target, bool block)
    {
        if (group == null)
        {
            yield break;
        }

        group.gameObject.SetActive(true);
        group.blocksRaycasts = block;
        float start = group.alpha;
        float elapsed = 0f;
        if (fadeDuration <= 0f)
        {
            group.alpha = target;
        }
        else
        {
            while (elapsed < fadeDuration)
            {
                if (PauseMenuController.IsPaused)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
                yield return null;
            }
        }

        group.alpha = target;
        group.interactable = target > 0f;
        group.blocksRaycasts = block && target > 0f;
        if (target <= 0f && group != blackScreenCanvasGroup)
        {
            group.gameObject.SetActive(false);
        }
    }

    private static void PrepareCanvasGroup(CanvasGroup group)
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

    private static void SetTextAlpha(TMP_Text text, float alpha)
    {
        if (text != null)
        {
            text.canvasRenderer.SetAlpha(alpha);
        }
    }

    private void CrossFadeText(TMP_Text text, bool visible)
    {
        if (text != null)
        {
            text.CrossFadeAlpha(visible ? 1f : 0f, fadeDuration, false);
        }
    }
}
