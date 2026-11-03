// 【役割】ヘリ着陸後のプレイヤー配置、暗転、HUD表示と操作開始を制御する。
using UnityEngine;
using System.Collections;
using UnityChan;

public class PlayerDismountDirector : MonoBehaviour
{
    [Header("カメラ連携")]
    [Tooltip("プレイヤー出現時に通常視点へ切り替えるCameraManager")]
    [SerializeField]
    private CameraManager cameraManager;
    [Header("UI連携")]
    public TutorialUIManager tutorialUI;
    [Tooltip("YES/NO判定後のチュートリアルと本編開始を管理するGM")]
    [SerializeField]
    private TutorialUIManager2 tutorialGameMaster;
    [Tooltip("着陸後の暗転と拠点名表示")]
    [SerializeField]
    private LocationAnnouncementUI locationAnnouncementUI;
    [SerializeField]
    private string landingLocationName = "拠点１：ベースキャンプ";
    [Header("非表示にするゲームUI群")]
    [Tooltip("ヘリ着陸〜ルール確認の間、非表示にしたいUIオブジェクト（マップ、スコア、銃UIなど）をここに登録してください")]
    public GameObject[] gameUIObjects;
    [Header("参照オブジェクト")]
    public Transform HelicopterTransform;
    [Tooltip("シーン上に配置済みのプレイヤーオブジェクト")]
    public UnityChanControlScriptWithRgidBody PlayerScript;
    [Tooltip("選択地点ごとのPlayerSpawnTargetを取得するHelicopterAutoFlight")]
    [SerializeField]
    private HelicopterAutoFlight autoFlight;
    [Header("演出パラメータ")]
    public Vector3 SpawnOffset = new Vector3(1.5f, 0f, 0f);
    [Tooltip("ヘリの向きを基準に、プレイヤーのY軸（左右の向き）を何度回転させるか")]
    public float spawnRotationY = 0f;
    [Tooltip("出現してからルール説明が表示されるまでの秒数（着地までの滞空時間など）")]
    public float ControlDelay = 1.0f;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        if (PlayerScript != null)
        {
            PlayerScript.canControl = false;
            PlayerScript.gameObject.SetActive(false);
        }

        // 【新規】ゲーム開始前は、登録されたすべてのUIを非アクティブ化する
        SetGameUIVisibility(false);
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        // 【新規】ルール説明が終わり、ゲームが始まったか（操作解禁されたか）を監視
        // TutorialUIManager側で操作が解禁された瞬間にUIを表示させる
        bool gameUiCanBeShown = (tutorialGameMaster != null && tutorialGameMaster.isMovementUnlocked) || (tutorialGameMaster == null && tutorialUI != null && tutorialUI.isMovementUnlocked);
        if (gameUiCanBeShown)
        {
            // 操作が解禁されたのでUIを表示
            SetGameUIVisibility(true);
            Collider playerCollider = PlayerScript.GetComponent<Collider>();
            Collider[] helicopterColliders = HelicopterTransform.GetComponentsInChildren<Collider>();
            if (playerCollider != null)
            {
                foreach (Collider col in helicopterColliders)
                {
                    Physics.IgnoreCollision(playerCollider, col, true);
                }
            }
        }
    }

    // 【重要】ヘリの移動が終わったタイミングなど、外のスクリプトからこの関数を呼び出してください
    public void StartDismountSequence()
    {
        if (HelicopterTransform == null || PlayerScript == null)
        {
            Debug.LogError("[Director] 必要なオブジェクトが設定されていません。インスペクターを確認してください。");
            return;
        }

        Debug.Log("[Director] 既存のプレイヤーを配置し、ヘリからの降下を開始します。");
        // 1. 選択した地点専用の出現Transformを最優先で使います。
        Transform playerSpawnTarget = autoFlight != null ? autoFlight.PlayerSpawnTarget : null;
        if (autoFlight != null && playerSpawnTarget == null)
        {
            Debug.LogError("[Director] 選択地点のPlayerSpawnTargetが未設定です。" + "古いヘリ位置に出現することを防ぐため、処理を中止します。", this);
            return;
        }

        if (playerSpawnTarget != null)
        {
            PlayerScript.transform.SetPositionAndRotation(playerSpawnTarget.position, playerSpawnTarget.rotation);
        }
        else
        {
            // 旧シーンとの互換用フォールバックです。
            PlayerScript.transform.position = HelicopterTransform.position + HelicopterTransform.TransformDirection(SpawnOffset);
            PlayerScript.transform.rotation = HelicopterTransform.rotation * Quaternion.Euler(0f, spawnRotationY, 0f);
        }

        // 2. プレイヤーを表示する（この時点ではまだ動けない状態をキープ）
        PlayerScript.gameObject.SetActive(true);
        if (playerSpawnTarget != null)
        {
            // OnEnableやAnimatorによる位置の上書きを防ぐため、
            // 有効化後にもう一度、指定位置を適用します。
            PlayerScript.transform.SetPositionAndRotation(playerSpawnTarget.position, playerSpawnTarget.rotation);
            Physics.SyncTransforms();
            float spawnPositionError = Vector3.Distance(PlayerScript.transform.position, playerSpawnTarget.position);
            if (spawnPositionError > 0.05f)
            {
                Debug.LogError("[Director] プレイヤーを指定出現地点へ" + $"配置できません。 error={spawnPositionError:F3}", this);
                PlayerScript.gameObject.SetActive(false);
                return;
            }

            Debug.Log("[Director] プレイヤーの出現位置を確認しました。" + $" position={PlayerScript.transform.position}", this);
        }

        if (cameraManager != null)
        {
            cameraManager.SwitchToPlayerCamera();
        }

        // 3. 物理演算のリセット（重力でヘリから下に落とす）
        Rigidbody playerRb = PlayerScript.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.velocity = Vector3.zero;
            playerRb.useGravity = true;
        }

        // 4. 指定秒数後（ヘリから降りて着地した頃合い）にルール説明を開始する
        StartCoroutine(TriggerRulesAfterDismount(ControlDelay));
    }

    private IEnumerator TriggerRulesAfterDismount(float delay)
    {
        // ヘリから飛び降りて地面に着地するまでの時間を待つ
        yield return new WaitForSeconds(delay);
        Debug.Log("[Director] ヘリ降下完了。チュートリアルGMへ通知します。");
        if (locationAnnouncementUI == null)
        {
            locationAnnouncementUI = FindObjectOfType<LocationAnnouncementUI>(true);
        }

        if (locationAnnouncementUI != null)
        {
            yield return locationAnnouncementUI.PlayLandingSequence(landingLocationName, () =>
            {
                if (PlayerScript != null)
                {
                    PlayerScript.canControl = true;
                }

                if (GameInputManager.Instance != null)
                {
                    GameInputManager.Instance.ResetForGameplay();
                }
            });
        }

        if (tutorialGameMaster == null)
        {
            tutorialGameMaster = FindObjectOfType<TutorialUIManager2>();
        }

        if (tutorialGameMaster != null)
        {
            tutorialGameMaster.BeginAfterLanding();
            yield break;
        }

        // 新GMがない旧シーンでは、従来処理を維持します。
        if (tutorialUI != null)
        {
            tutorialUI.StartTutorial();
        }
        else
        {
            // エラー回避のためにヒエラルキー上から自動検索を試みる
            tutorialUI = FindObjectOfType<TutorialUIManager>();
            if (tutorialUI != null)
            {
                tutorialUI.StartTutorial();
            }
            else
            {
                Debug.LogError("[Director] TutorialUIManagerがシーン内に見つかりません！割り当てを確認してください。");
            }
        }
    }

    // 【新規】登録されたすべてのUIの表示・非表示を一括切り替えする関数
    private void SetGameUIVisibility(bool isVisible)
    {
        if (gameUIObjects == null)
        {
            return;
        }

        foreach (GameObject uiObj in gameUIObjects)
        {
            if (uiObj != null && uiObj.activeSelf != isVisible)
            {
                uiObj.SetActive(isVisible);
            }
        }
    }
}
