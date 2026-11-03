// 【役割】終了時のスコアやクリア状態をリザルトシーンへ受け渡す。
using UnityEngine;

public static class GameResultData
{
    public static int finalScore = 0;
    public static int finalKillCount = 0;
    public static int finalSurvivalBonus = 0;
    public static int finalTotalScore = 0;
    public static bool stageCleared;
    public static void SetResult(int score, int killCount, int survivalBonus, bool cleared = false)
    {
        finalScore = score;
        finalKillCount = killCount;
        finalSurvivalBonus = survivalBonus;
        finalTotalScore = score + survivalBonus;
        stageCleared = cleared;
        if (cleared)
        {
            GameProgressData.CompleteSelectedStage();
        }

        Debug.Log($"[GameResultData] Score={finalScore} Kill={finalKillCount} SurvivalBonus={finalSurvivalBonus} Total={finalTotalScore}");
    }

    public static void ResetResult()
    {
        finalScore = 0;
        finalKillCount = 0;
        finalSurvivalBonus = 0;
        finalTotalScore = 0;
        stageCleared = false;
    }
}
