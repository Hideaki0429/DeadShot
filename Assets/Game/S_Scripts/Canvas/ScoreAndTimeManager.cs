// 【役割】スコア、制限時間、終了条件とリザルトへ渡す値を管理する。
using UnityEngine;
using System.Collections;
using System;
using UnityEngine.SceneManagement;
using TMPro;

public class ScoreAndTimeManager : MonoBehaviour
{
    public static ScoreAndTimeManager instance;
    public static event Action OnMainGameStarted;
    [Header("--- モード設定 ---")]
    [SerializeField]
    private bool isMainGameStage = false;
    [Tooltip("オフにするとスコア機能は維持し、制限時間とタイマーUIだけを停止します。")]
    [SerializeField]
    private bool useTimeLimit = true;
    [Header("--- UI References (TextMeshPro) ---")]
    [SerializeField]
    private TextMeshProUGUI countdownText;
    [SerializeField]
    private TextMeshProUGUI scoreText;
    [SerializeField]
    private TextMeshProUGUI killCountText;
    [Header("--- タイマーUI自体のゲームオブジェクト ---")]
    [SerializeField]
    private GameObject timerUiObject;
    [Header("--- Time Settings ---")]
    public float countdownTime = 3f;
    public float gameDuration = 180f;
    [Tooltip("薬で延長できる制限時間の上限。デフォルトは20分。")]
    [Min(1f)]
    public float maximumGameDuration = 1200f;
    public string nextSceneName;
    [Header("--- Survival Bonus Settings ---")]
    [Tooltip("HPが100%残っていた場合の最大生存ボーナス")]
    [SerializeField]
    private int maxSurvivalBonus = 2000;
    [Tooltip("プレイヤーの体力スクリプトを登録")]
    [SerializeField]
    private PlayerHealth playerHealth;
    [Header("--- Audio ---")]
    public AudioSource audioSource;
    public AudioClip mgameStartClip;
    public int TotalScore { get; private set; } = 0;

    private int currentKillCount = 0;
    private bool gameStarted = false;
    private float timer;
    public float RemainingTime => timer;
    public int KillCount => currentKillCount;
    public bool UseTimeLimit => useTimeLimit;

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        UpdateScoreUI();
        UpdateKillUI();
        if (!useTimeLimit)
        {
            timer = Mathf.Max(0f, gameDuration);
            gameStarted = true;
            SetTimerUiVisible(false);
            OnMainGameStarted?.Invoke();
            return;
        }

        SetTimerUiVisible(false);
    }

    public void StartGameFromTutorial()
    {
        if (!useTimeLimit)
        {
            if (!gameStarted)
            {
                gameStarted = true;
                OnMainGameStarted?.Invoke();
            }

            SetTimerUiVisible(false);
            return;
        }

        Debug.Log("[ScoreAndTimeManager] チュートリアル/ルール説明終了信号を受信。タイマーを起動します。");
        if (timerUiObject != null)
        {
            timerUiObject.SetActive(true);
        }
        else if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
        }

        if (audioSource != null)
        {
            if (mgameStartClip != null)
            {
                audioSource.clip = mgameStartClip;
            }

            audioSource.Play();
        }

        StartCoroutine(StartMainGameCountdown());
    }

    IEnumerator StartMainGameCountdown()
    {
        float countdown = countdownTime;
        while (countdown > 0)
        {
            if (countdownText != null)
            {
                countdownText.text = Mathf.Ceil(countdown).ToString();
            }

            yield return new WaitForSeconds(1f);
            countdown--;
        }

        if (countdownText != null)
        {
            countdownText.text = "Game Start";
        }

        yield return new WaitForSeconds(1f);
        timer = gameDuration;
        gameStarted = true;
        OnMainGameStarted?.Invoke();
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (gameStarted && useTimeLimit)
        {
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                timer = 0;
                gameStarted = false;
                LoadNextScene(true);
            }

            UpdateTimerDisplay();
        }
    }

    private void UpdateTimerDisplay()
    {
        if (countdownText == null)
        {
            return;
        }

        int minutes = Mathf.FloorToInt(timer / 60f);
        int seconds = Mathf.CeilToInt(timer % 60f);
        if (seconds == 60)
        {
            minutes += 1;
            seconds = 0;
        }

        countdownText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    /// <summary>感染緩和薬などでステージの残り制限時間を延長する。</summary>
    public void AddTime(float seconds)
    {
        if (!useTimeLimit || seconds <= 0f)
        {
            return;
        }

        float maximum = Mathf.Max(1f, maximumGameDuration);
        float before = gameStarted ? timer : gameDuration;
        if (gameStarted)
        {
            timer = Mathf.Min(maximum, timer + seconds);
            UpdateTimerDisplay();
        }
        else
        {
            // チュートリアル中など、本編タイマー開始前に使った場合も反映する。
            gameDuration = Mathf.Min(maximum, gameDuration + seconds);
        }

        float after = gameStarted ? timer : gameDuration;
        Debug.Log($"[Timer] 感染緩和薬で制限時間を{after - before:F0}秒延長しました。上限={maximum:F0}秒", this);
    }

    private void SetTimerUiVisible(bool visible)
    {
        if (timerUiObject != null)
        {
            timerUiObject.SetActive(visible);
        }
        else if (countdownText != null)
        {
            countdownText.gameObject.SetActive(visible);
        }
    }

    public void AddScore(int score)
    {
        TotalScore += score;
        UpdateScoreUI();
    }

    public void AddKill()
    {
        currentKillCount++;
        UpdateKillUI();
    }

    /// <summary>
    /// チュートリアル中の仮スコアと撃破数を本編開始前に戻します。
    /// </summary>
    public void ResetForMainGame()
    {
        TotalScore = 0;
        currentKillCount = 0;
        timer = 0f;
        gameStarted = false;
        UpdateScoreUI();
        UpdateKillUI();
        Debug.Log("[ScoreAndTimeManager] 本編開始用に" + "スコアと撃破数をリセットしました。");
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = TotalScore.ToString();
        }
    }

    private void UpdateKillUI()
    {
        if (killCountText != null)
        {
            killCountText.text = currentKillCount.ToString();
        }
    }

    private int CalculateSurvivalBonus()
    {
        if (playerHealth == null)
        {
            Debug.LogWarning("[ScoreAndTimeManager] PlayerHealthが設定されていません。生存ボーナスは0になります。");
            return 0;
        }

        float hpRate = playerHealth.CurrentHP / playerHealth.MaxHP;
        hpRate = Mathf.Clamp01(hpRate);
        int bonus = Mathf.RoundToInt(maxSurvivalBonus * hpRate);
        Debug.Log($"[ScoreAndTimeManager] HP割合={hpRate * 100f}% SurvivalBonus={bonus}");
        return bonus;
    }

    public void LoadNextScene(bool stageCleared = false)
    {
        Debug.Log("[ScoreAndTimeManager] LoadNextSceneが呼ばれた");
        int survivalBonus = CalculateSurvivalBonus();
        Debug.Log($"[ScoreAndTimeManager] 保存前 Score={TotalScore} Kill={currentKillCount} SurvivalBonus={survivalBonus}");
        GameResultData.SetResult(TotalScore, currentKillCount, survivalBonus, stageCleared);
        SceneManager.LoadScene(nextSceneName);
    }
}
