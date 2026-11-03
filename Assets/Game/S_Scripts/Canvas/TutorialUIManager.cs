// 【役割】チュートリアルの案内と進行条件を順番に処理する。
using UnityEngine;
using System.Collections;
using TMPro;
using UnityChan;

public class TutorialUIManager : MonoBehaviour
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
    [Header("操作説明設定 (非表示ですが他スクリプト連携用に維持)")]
    public string controlHeaderString = "【操作説明】";
    [TextArea(2, 4)]
    public string[] controlMessages = new string[]
    {
        "WASDキーまたは十字キーで移動して、敵に近づこう",
        "敵を捉えた！\nCキーを押して「構え状態」になろう",
        "Zキーを押して弾を発射しよう",
        "Qキーでカメラを切り替えてみよう",
        "実践！\n実際に眼の前の敵を倒してみよう！"
    };
    [Header("プレイヤー・敵の判定設定")]
    public UnityChanControlScriptWithRgidBody playerScript;
    [Tooltip("ルール確認完了後のゲーム開始時に有効化するダメージフィルター")]
    [SerializeField]
    private PlayerDamageScreenEffect damageScreenEffect;
    public string enemyTag = "Enemy";
    public float enemyDetectionDistance = 5.0f;
    [Header("【操作解禁フラグ】(他のスクリプトから参照)")]
    public bool isMovementUnlocked = false;
    public bool isAimUnlocked = false;
    public bool isFireUnlocked = false;
    public bool isCameraUnlocked = false;
    //private readonly int initialScore = 0;
    void Awake()
    {
        if (damageScreenEffect == null)
        {
            damageScreenEffect = FindObjectOfType<PlayerDamageScreenEffect>();
        }

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

        // 最初はプレイヤーを動けないようにしておく
        if (playerScript != null)
        {
            playerScript.canControl = false;
        }
    }

    // 【修正】自動起動（Start）を廃止。外部からタイミングをコントロールできるようにします。
    // ※もしチュートリアルシーンだけは最初から動かしたい場合、チュートリアル用の別スクリプトからStartTutorial()を叩くか、
    // 本番のヘリ降下完了イベントからこの関数を呼び出してください。
    public void StartTutorial()
    {
        StartCoroutine(TutorialSequence());
    }

    private IEnumerator TutorialSequence()
    {
        // ルール説明中はプレイヤーの操作を強制ロック
        if (playerScript != null)
        {
            playerScript.canControl = false;
        }

        // ==========================================
        // フェーズ1：ルール説明演出
        // ==========================================
        if (ruleMessages != null && ruleMessages.Length > 0)
        {
            if (headerText != null)
            {
                headerText.text = ruleHeaderString;
                headerText.CrossFadeAlpha(1f, fadeDuration, false);
            }

            yield return new WaitForSeconds(fadeDuration);
            for (int i = 0; i < ruleMessages.Length; i++)
            {
                if (ruleBodyText != null)
                {
                    ruleBodyText.text = ruleMessages[i];
                    ruleBodyText.CrossFadeAlpha(1f, fadeDuration, false);
                }

                yield return new WaitForSeconds(displayDuration);
                if (i < ruleMessages.Length - 1)
                {
                    if (ruleBodyText != null)
                    {
                        ruleBodyText.CrossFadeAlpha(0f, fadeDuration, false);
                    }

                    yield return new WaitForSeconds(fadeDuration);
                }
            }

            if (ruleBodyText != null)
            {
                ruleBodyText.text += "\n<size=80%><color=#FFFF00>※Enterキーか左クリックでゲーム開始</color></size>";
            }
        }

        // 入力待ち（Enter または 左クリック）
        yield return new WaitUntil(() => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return));
        // ルール説明UIをフェードアウト
        if (headerText != null)
        {
            headerText.CrossFadeAlpha(0f, fadeDuration, false);
        }

        if (ruleBodyText != null)
        {
            ruleBodyText.CrossFadeAlpha(0f, fadeDuration, false);
        }

        yield return new WaitForSeconds(fadeDuration);
        // ==========================================
        // 【共通】ルール確認完了 ➔ タイマースタート＆操作解禁
        // ==========================================
        Debug.Log("[TutorialUIManager] ルール確認完了。ゲームを開始します。");
        // 1. 各種解禁フラグ（AudioManager連携用など）を全てtrueに
        isMovementUnlocked = true;
        isAimUnlocked = true;
        isFireUnlocked = true;
        isCameraUnlocked = true;
        // 2. プレイヤーを動けるようにする
        if (playerScript != null)
        {
            playerScript.canControl = true;
        }

        // OP・ヘリ飛行・ルール説明中は非表示にし、
        // Playerの操作解禁を本当のゲーム開始としてHPフィルターを開始する。
        if (damageScreenEffect != null)
        {
            damageScreenEffect.SetEffectEnabled(true);
        }

        // 3. ScoreAndTimeManagerに対して、本番タイマー（またはカウントダウン）の開始を指示
        if (ScoreAndTimeManager.instance != null)
        {
            ScoreAndTimeManager.instance.StartGameFromTutorial();
        }
        else
        {
            Debug.LogWarning("[TutorialUIManager] ScoreAndTimeManagerのインスタンスが見つかりません。");
        }

        yield break;
    }

    // 互換性維持のため残している関数
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
