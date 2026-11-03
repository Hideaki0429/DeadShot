// 【役割】チュートリアルの会話・操作案内と完了条件を処理する。
using UnityEngine;
using System.Collections;
using TMPro;
using UnityChan;

public class TutorialUIManager1 : MonoBehaviour
{
    [Header("UIコンポーネント (TextMeshPro)")]
    public TextMeshProUGUI headerText;
    [Tooltip("ルール説明用の文章テキスト")]
    public TextMeshProUGUI ruleBodyText;
    [Tooltip("操作説明用の文章テキスト（配置を変えるための別枠）")]
    public TextMeshProUGUI controlBodyText;
    [Header("時間・演出設定")]
    public float displayDuration = 5.0f;
    public float fadeDuration = 1.0f;
    [Header("ルール説明設定")]
    public string ruleHeaderString = "【ルール説明】";
    [TextArea(2, 4)]
    public string[] ruleMessages;
    [Header("操作説明設定 (段階的に表示)")]
    public string controlHeaderString = "【操作説明】";
    [TextArea(2, 4)]
    [Tooltip("0:移動, 1:構え, 2:射撃, 3:カメラ, 4:実戦(敵討伐)")]
    public string[] controlMessages = new string[]
    {
        "WASDキーまたは十字キーで移動して、敵に近づこう",
        "敵を捉えた！\nCキーを押して「構え状態」になろう",
        "Enterキーまたは右クリックで弾を発射しよう", // 【修正】説明文の変更
        "Qキーでカメラを切り替えてみよう",
        "実践！\n実際に眼の前の敵を倒してみよう！"
    };
    [Header("プレイヤー・敵の判定設定")]
    public UnityChanControlScriptWithRgidBody playerScript;
    public string enemyTag = "Enemy";
    public float enemyDetectionDistance = 5.0f; // 敵に接敵したとみなす距離
    [Header("【操作解禁フラグ】(他のスクリプトから参照)")]
    public bool isMovementUnlocked = false;
    public bool isAimUnlocked = false;
    public bool isFireUnlocked = false;
    public bool isCameraUnlocked = false;
    private int initialScore = 0; // チュートリアル開始時点のスコア
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        if (headerText != null)
        {
            headerText.canvasRenderer.SetAlpha(0f);
        }

        if (ruleBodyText != null)
        {
            ruleBodyText.canvasRenderer.SetAlpha(0f);
        }

        if (controlBodyText != null)
        {
            controlBodyText.canvasRenderer.SetAlpha(0f);
        }

        if (headerText != null)
        {
            headerText.text = "";
        }

        if (ruleBodyText != null)
        {
            ruleBodyText.text = "";
        }

        if (controlBodyText != null)
        {
            controlBodyText.text = "";
        }

        if (playerScript != null)
        {
            playerScript.canControl = false;
        }
    }

    public void StartTutorial()
    {
        StartCoroutine(TutorialSequence());
    }

    private IEnumerator TutorialSequence()
    {
        // ==========================================
        // フェーズ1：ルール説明（この間、プレイヤーは動けません）
        // ==========================================
        if (playerScript != null)
        {
            playerScript.canControl = false;
        }

        if (ruleMessages.Length > 0)
        {
            headerText.text = ruleHeaderString;
            headerText.CrossFadeAlpha(1f, fadeDuration, false);
            yield return new WaitForSeconds(fadeDuration);
            for (int i = 0; i < ruleMessages.Length; i++)
            {
                ruleBodyText.text = ruleMessages[i];
                ruleBodyText.CrossFadeAlpha(1f, fadeDuration, false);
                yield return new WaitForSeconds(displayDuration);
                if (i < ruleMessages.Length - 1)
                {
                    ruleBodyText.CrossFadeAlpha(0f, fadeDuration, false);
                    yield return new WaitForSeconds(fadeDuration);
                }
            }

            ruleBodyText.text += "\n<size=80%><color=#FFFF00>※Enterキーか左クリックでゲーム開始</color></size>";
        }

        // 入力待ち（Enter または 左クリック）
        yield return new WaitUntil(() => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return));
        headerText.CrossFadeAlpha(0f, fadeDuration, false);
        ruleBodyText.CrossFadeAlpha(0f, fadeDuration, false);
        yield return new WaitForSeconds(fadeDuration);
        // ==========================================
        // フェーズ2：操作説明
        // ==========================================
        headerText.text = controlHeaderString;
        headerText.CrossFadeAlpha(1f, fadeDuration, false);
        // --- ステップ1：移動解禁 ---
        if (controlMessages.Length > 0)
        {
            controlBodyText.text = controlMessages[0];
            controlBodyText.CrossFadeAlpha(1f, fadeDuration, false);
            if (playerScript != null)
            {
                playerScript.canControl = true;
            }

            isMovementUnlocked = true;
            Debug.Log("[Tutorial] ルール説明終了。プレイヤーの操作を有効化しました。");
        }

        // 指定の範囲内に敵を見つけるまで待機
        yield return new WaitUntil(IsEnemyClose);
        controlBodyText.CrossFadeAlpha(0f, fadeDuration / 2, false);
        yield return new WaitForSeconds(fadeDuration / 2);
        // --- ステップ2：構え解禁 (C) ---
        if (controlMessages.Length > 1)
        {
            controlBodyText.text = controlMessages[1];
            controlBodyText.CrossFadeAlpha(1f, fadeDuration / 2, false);
            isAimUnlocked = true;
        }

        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.C));
        controlBodyText.CrossFadeAlpha(0f, fadeDuration / 2, false);
        yield return new WaitForSeconds(fadeDuration / 2);
        // --- ステップ3：射撃解禁 (Enter or 右クリック) ---
        if (controlMessages.Length > 2)
        {
            controlBodyText.text = controlMessages[2];
            controlBodyText.CrossFadeAlpha(1f, fadeDuration / 2, false);
            isFireUnlocked = true;
        }

        // 【修正】Enterキー(Return) または マウス右クリック(1) のいずれかで進行
        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(1));
        controlBodyText.CrossFadeAlpha(0f, fadeDuration / 2, false);
        yield return new WaitForSeconds(fadeDuration / 2);
        // --- ステップ4：カメラ切替解禁 (Q) ---
        if (controlMessages.Length > 3)
        {
            controlBodyText.text = controlMessages[3];
            controlBodyText.CrossFadeAlpha(1f, fadeDuration / 2, false);
            isCameraUnlocked = true;
        }

        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Q));
        headerText.CrossFadeAlpha(0f, fadeDuration, false);
        yield return new WaitForSeconds(fadeDuration / 2);
        // ==========================================
        // フェーズ3：実戦（スコア加算待ち）
        // ==========================================
        if (controlMessages.Length > 4)
        {
            controlBodyText.text = controlMessages[4];
            controlBodyText.CrossFadeAlpha(1f, 0.1f, false);
        }

        if (ScoreAndTimeManager.instance != null)
        {
            initialScore = ScoreAndTimeManager.instance.TotalScore;
            Debug.Log($"[Tutorial] 実戦開始。現在のスコア: {initialScore}");
        }

        yield return new WaitUntil(() =>
        {
            if (ScoreAndTimeManager.instance != null)
            {
                return ScoreAndTimeManager.instance.TotalScore > initialScore;
            }

            return false;
        });
        // ==========================================
        // チュートリアル完了処理
        // ==========================================
        Debug.Log("[Tutorial] スコアの加算を検知しました！");
        controlBodyText.text = "<color=green>チュートリアル完了！</color>";
        yield return new WaitForSeconds(2.5f);
        controlBodyText.CrossFadeAlpha(0f, fadeDuration, false);
        yield return new WaitForSeconds(fadeDuration);
        Debug.Log("[Tutorial] チュートリアルシステムを終了します。");
    }

    private bool IsEnemyClose()
    {
        if (playerScript == null)
        {
            return false;
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        foreach (GameObject enemy in enemies)
        {
            if (enemy == null)
            {
                continue;
            }

            float distance = Vector3.Distance(playerScript.transform.position, enemy.transform.position);
            if (distance <= enemyDetectionDistance)
            {
                return true;
            }
        }

        return false;
    }
}
