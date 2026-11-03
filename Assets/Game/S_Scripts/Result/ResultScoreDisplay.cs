// 【役割】リザルトのスコア・ランク表示と再開・タイトル復帰の操作を管理する。
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ResultScoreDisplay : MonoBehaviour
{
    [System.Serializable]
    public class RankData
    {
        [Tooltip("表示するランク名")]
        public string rankName;
        [Tooltip("このランクになるために必要な最低スコア")]
        public int requiredScore;
    }

    [Header("--- Canvas Groups ---")]
    [SerializeField]
    private CanvasGroup resultTitleGroup;
    [SerializeField]
    private CanvasGroup scoreGroup;
    [SerializeField]
    private CanvasGroup killGroup;
    [SerializeField]
    private CanvasGroup survivalBonusGroup;
    [SerializeField]
    private CanvasGroup totalScoreGroup;
    [SerializeField]
    private CanvasGroup rankGroup;
    [Header("--- Texts ---")]
    [SerializeField]
    private TextMeshProUGUI scoreText;
    [SerializeField]
    private TextMeshProUGUI killCountText;
    [SerializeField]
    private TextMeshProUGUI survivalBonusText;
    [SerializeField]
    private TextMeshProUGUI totalScoreText;
    [SerializeField]
    private TextMeshProUGUI rankText;
    [Header("--- Line Effects ---")]
    [SerializeField]
    private LineRevealEffect resultTitleLine;
    [SerializeField]
    private LineRevealEffect scoreLine;
    [SerializeField]
    private LineRevealEffect killLine;
    [SerializeField]
    private LineRevealEffect survivalLine;
    [SerializeField]
    private LineRevealEffect totalLine;
    [Header("--- Rank Settings ---")]
    [SerializeField]
    private RankData[] rankList =
    {
        new RankData
        {
            rankName = "S",
            requiredScore = 8000
        },
        new RankData
        {
            rankName = "A",
            requiredScore = 6000
        },
        new RankData
        {
            rankName = "B",
            requiredScore = 4000
        },
        new RankData
        {
            rankName = "C",
            requiredScore = 2000
        },
        new RankData
        {
            rankName = "D",
            requiredScore = 0
        },
    };
    [Header("--- Animation Settings ---")]
    [Tooltip("各項目がフェードインする時間")]
    [SerializeField]
    private float fadeTime = 0.5f;
    [Tooltip("数値が0から最終値まで増える時間")]
    [SerializeField]
    private float countUpTime = 0.8f;
    [Tooltip("各項目の演出後に待機する時間")]
    [SerializeField]
    private float waitBetweenItems = 0.25f;
    [Header("--- Audio Source ---")]
    [SerializeField]
    private AudioSource audioSource;
    [Header("--- RESULT Audio ---")]
    [Tooltip("RESULTタイトル表示時の少し重いWhoosh")]
    [SerializeField]
    private AudioClip resultAppearSE;
    [Header("--- Normal Item Audio ---")]
    [Tooltip("Score、撃破数、生存ボーナス表示時の小さいWhoosh")]
    [SerializeField]
    private AudioClip normalItemAppearSE;
    [Tooltip("通常項目から次の項目へ切り替わる時のWhoosh")]
    [SerializeField]
    private AudioClip itemTransitionSE;
    [Header("--- Count Up Audio ---")]
    [Tooltip("数字が増えている間に連続再生するTick音")]
    [SerializeField]
    private AudioClip countTickSE;
    [Tooltip("Tick音を鳴らす間隔")]
    [SerializeField]
    private float tickInterval = 0.06f;
    [Tooltip("Tick音の音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float tickVolume = 0.7f;
    [Header("--- Total Score Audio ---")]
    [Tooltip("Total Score表示時の少し重いWhoosh")]
    [SerializeField]
    private AudioClip totalScoreAppearSE;
    [Header("--- Rank Audio ---")]
    [Tooltip("通常ランク表示時のImpact＋Whoosh")]
    [SerializeField]
    private AudioClip rankImpactWhooshSE;
    [Tooltip("Sランク表示時の金属音")]
    [SerializeField]
    private AudioClip sRankMetalSE;
    [Tooltip("Sランク表示時の低音インパクト")]
    [SerializeField]
    private AudioClip sRankLowImpactSE;
    [Header("--- Rank Effect ---")]
    [SerializeField]
    private BioStyleRankEffect rankEffect;
    [Header("--- Return Button ---")]
    [Tooltip("未設定の場合はTitle2へ戻るButton_SwitchSceneCmdを自動検出します。")]
    [SerializeField]
    private CanvasGroup returnButtonGroup;
    [SerializeField]
    private CanvasGroup continueButtonGroup;
    [Tooltip("ランク演出終了後、RETURNボタンの表示を始めるまでの待ち時間")]
    [Min(0f)]
    [SerializeField]
    private float returnButtonDelay = 0.25f;
    [Tooltip("RETURNボタンのフェード表示時間")]
    [Min(0.01f)]
    [SerializeField]
    private float returnButtonFadeTime = 0.5f;
    [SerializeField]
    private AudioSource resultMenuAudio;
    [SerializeField]
    private AudioClip resultMoveSound;
    [SerializeField]
    private AudioClip resultConfirmSound;
    private Button returnButton;
    private Button continueButton;
    private bool resultMenuReady;
    private int selectedResultButton;
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    private void Start()
    {
        ResolveReturnButton();
        HideAll();
        StartCoroutine(ResultSequence());
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (!resultMenuReady || GameInputManager.Instance == null)
        {
            return;
        }

        int horizontal = GameInputManager.Instance.UiHorizontal;
        if (horizontal != 0)
        {
            selectedResultButton = horizontal < 0 ? 0 : 1;
            RefreshResultSelection();
            PlayResultSound(resultMoveSound);
        }

        if (GameInputManager.Instance.UiConfirmPressed)
        {
            PlayResultSound(resultConfirmSound);
            if (selectedResultButton == 0 && continueButton != null)
            {
                GameProgressData.SetNextStartMode(GameResultData.stageCleared ? GameProgressData.StartMode.ResumeAfterClear : GameProgressData.StartMode.RespawnAtLastLanding);
                continueButton.onClick.Invoke();
            }
            else if (returnButton != null)
            {
                GameProgressData.SetNextStartMode(GameProgressData.StartMode.NormalOpening);
                returnButton.onClick.Invoke();
            }
        }
    }

    private void HideAll()
    {
        SetAlpha(resultTitleGroup, 0f);
        SetAlpha(scoreGroup, 0f);
        SetAlpha(killGroup, 0f);
        SetAlpha(survivalBonusGroup, 0f);
        SetAlpha(totalScoreGroup, 0f);
        SetAlpha(rankGroup, 0f);
        SetReturnButtonState(0f, false);
        SetButtonState(continueButtonGroup, 0f, false);
        if (scoreText != null)
        {
            scoreText.text = "0";
        }

        if (killCountText != null)
        {
            killCountText.text = "0";
        }

        if (survivalBonusText != null)
        {
            survivalBonusText.text = "0";
        }

        if (totalScoreText != null)
        {
            totalScoreText.text = "0";
        }

        if (rankText != null)
        {
            rankText.text = "";
        }

        ResetAllLines();
    }

    private void ResetAllLines()
    {
        if (resultTitleLine != null)
        {
            resultTitleLine.ResetLine();
        }

        if (scoreLine != null)
        {
            scoreLine.ResetLine();
        }

        if (killLine != null)
        {
            killLine.ResetLine();
        }

        if (survivalLine != null)
        {
            survivalLine.ResetLine();
        }

        if (totalLine != null)
        {
            totalLine.ResetLine();
        }
    }

    private IEnumerator ResultSequence()
    {
        // RESULT表示
        yield return FadeIn(resultTitleGroup, resultAppearSE);
        // RESULT下の横線を伸ばす
        if (resultTitleLine != null)
        {
            yield return resultTitleLine.Play();
        }

        yield return new WaitForSeconds(waitBetweenItems);
        // Score
        yield return ShowNumber(scoreGroup, scoreText, GameResultData.finalScore, normalItemAppearSE, scoreLine);
        PlaySE(itemTransitionSE);
        // 撃破数
        yield return ShowNumber(killGroup, killCountText, GameResultData.finalKillCount, normalItemAppearSE, killLine);
        PlaySE(itemTransitionSE);
        // 生存ボーナス
        yield return ShowNumber(survivalBonusGroup, survivalBonusText, GameResultData.finalSurvivalBonus, normalItemAppearSE, survivalLine);
        PlaySE(itemTransitionSE);
        // Total Score
        yield return ShowNumber(totalScoreGroup, totalScoreText, GameResultData.finalTotalScore, totalScoreAppearSE, totalLine);
        yield return new WaitForSeconds(waitBetweenItems);
        // Rank
        string rank = GetRank(GameResultData.finalTotalScore);
        if (rankText != null)
        {
            rankText.text = rank;
        }

        yield return FadeInWithoutSound(rankGroup);
        PlayRankSound(rank);
        if (rankEffect != null)
        {
            yield return rankEffect.PlayEffect();
        }

        if (returnButtonDelay > 0f)
        {
            yield return new WaitForSeconds(returnButtonDelay);
        }

        yield return FadeInReturnButton();
    }

    private void ResolveReturnButton()
    {
        if (returnButtonGroup != null)
        {
            returnButton = returnButtonGroup.GetComponent<Button>();
        }

        Button_SwitchSceneCmd[] sceneButtons = FindObjectsOfType<Button_SwitchSceneCmd>(true);
        foreach (Button_SwitchSceneCmd sceneButton in sceneButtons)
        {
            if (sceneButton == null)
            {
                continue;
            }

            if (sceneButton.sceneName == "Title2")
            {
                returnButton = sceneButton.GetComponent<Button>();
                returnButtonGroup = sceneButton.GetComponent<CanvasGroup>();
                if (returnButtonGroup == null)
                {
                    returnButtonGroup = sceneButton.gameObject.AddComponent<CanvasGroup>();
                }
            }
            else if (sceneButton.gameObject.name.IndexOf("CONTINUE", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continueButton = sceneButton.GetComponent<Button>();
                continueButtonGroup = sceneButton.GetComponent<CanvasGroup>();
                if (continueButtonGroup == null)
                {
                    continueButtonGroup = sceneButton.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }
    }

    private IEnumerator FadeInReturnButton()
    {
        if (returnButtonGroup == null)
        {
            yield break;
        }

        returnButtonGroup.gameObject.SetActive(true);
        returnButtonGroup.interactable = false;
        returnButtonGroup.blocksRaycasts = false;
        float timer = 0f;
        while (timer < returnButtonFadeTime)
        {
            timer += Time.deltaTime;
            returnButtonGroup.alpha = Mathf.Clamp01(timer / returnButtonFadeTime);
            yield return null;
        }

        SetReturnButtonState(1f, true);
        SetButtonState(continueButtonGroup, 1f, true);
        selectedResultButton = 0;
        resultMenuReady = true;
        RefreshResultSelection();
    }

    private static void SetButtonState(CanvasGroup group, float alpha, bool interactive)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = alpha;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }

    private void RefreshResultSelection()
    {
        Button selected = selectedResultButton == 0 ? continueButton : returnButton;
        if (EventSystem.current != null && selected != null)
        {
            EventSystem.current.SetSelectedGameObject(selected.gameObject);
        }
    }

    private void PlayResultSound(AudioClip clip)
    {
        AudioSource source = resultMenuAudio != null ? resultMenuAudio : audioSource;
        if (source != null && clip != null)
        {
            source.PlayOneShot(clip);
        }
    }

    private void SetReturnButtonState(float alpha, bool interactive)
    {
        if (returnButtonGroup == null)
        {
            return;
        }

        returnButtonGroup.alpha = alpha;
        returnButtonGroup.interactable = interactive;
        returnButtonGroup.blocksRaycasts = interactive;
    }

    private IEnumerator ShowNumber(CanvasGroup group, TextMeshProUGUI text, int targetValue, AudioClip appearClip, LineRevealEffect lineEffect)
    {
        yield return FadeIn(group, appearClip);
        if (lineEffect != null)
        {
            yield return lineEffect.Play();
        }

        yield return CountUp(text, targetValue);
        yield return new WaitForSeconds(waitBetweenItems);
    }

    private IEnumerator FadeIn(CanvasGroup group, AudioClip appearClip)
    {
        if (group == null)
        {
            yield break;
        }

        PlaySE(appearClip);
        float timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            group.alpha = Mathf.Clamp01(timer / fadeTime);
            yield return null;
        }

        group.alpha = 1f;
    }

    private IEnumerator FadeInWithoutSound(CanvasGroup group)
    {
        if (group == null)
        {
            yield break;
        }

        float timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            group.alpha = Mathf.Clamp01(timer / fadeTime);
            yield return null;
        }

        group.alpha = 1f;
    }

    private IEnumerator CountUp(TextMeshProUGUI text, int targetValue)
    {
        if (text == null)
        {
            yield break;
        }

        if (targetValue <= 0)
        {
            text.text = "0";
            yield break;
        }

        float timer = 0f;
        float tickTimer = 0f;
        int previousValue = -1;
        while (timer < countUpTime)
        {
            timer += Time.deltaTime;
            tickTimer += Time.deltaTime;
            float rate = Mathf.Clamp01(timer / countUpTime);
            float easedRate = 1f - Mathf.Pow(1f - rate, 3f);
            int value = Mathf.RoundToInt(targetValue * easedRate);
            if (value != previousValue)
            {
                text.text = value.ToString();
                previousValue = value;
            }

            if (tickTimer >= tickInterval)
            {
                PlaySE(countTickSE, tickVolume);
                tickTimer = 0f;
            }

            yield return null;
        }

        text.text = targetValue.ToString();
    }

    private string GetRank(int totalScore)
    {
        if (rankList == null || rankList.Length == 0)
        {
            Debug.LogWarning("[ResultScoreDisplay] Rank Listが設定されていないため、Dランクを表示します。");
            return "D";
        }

        RankData selectedRank = null;
        foreach (RankData rank in rankList)
        {
            if (rank == null)
            {
                continue;
            }

            if (totalScore >= rank.requiredScore)
            {
                if (selectedRank == null || rank.requiredScore > selectedRank.requiredScore)
                {
                    selectedRank = rank;
                }
            }
        }

        return selectedRank != null ? selectedRank.rankName : "D";
    }

    private void PlayRankSound(string rank)
    {
        if (string.Equals(rank, "S", System.StringComparison.OrdinalIgnoreCase))
        {
            PlaySE(sRankMetalSE);
            PlaySE(sRankLowImpactSE);
        }
        else
        {
            PlaySE(rankImpactWhooshSE);
        }
    }

    private void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group != null)
        {
            group.alpha = alpha;
        }
    }

    private void PlaySE(AudioClip clip)
    {
        PlaySE(clip, 1f);
    }

    private void PlaySE(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
