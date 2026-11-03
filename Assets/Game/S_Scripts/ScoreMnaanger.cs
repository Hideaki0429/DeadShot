// 【役割】獲得スコアを保持し、スコア表示のテキストを更新する。
using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager instance;
    public int TotalScore { get; private set; }

    public Text scoreText; // UI Textへの参照
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        // シングルトンパターンの実装
        if (instance == null)
        {
            instance = this;
        //DontDestroyOnLoad(gameObject); // シーンを切り替えてもこのオブジェクトを破棄しない
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        // 初期スコアの表示を更新
        UpdateScoreText();
    }

    public void AddScore(int score)
    {
        TotalScore += score;
        Debug.Log("TotalScore = " + TotalScore);
        UpdateScoreText();
    }

    void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = "" + TotalScore;
        }
    }
}
