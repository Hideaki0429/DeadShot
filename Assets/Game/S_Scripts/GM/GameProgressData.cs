// 【役割】クリア状況、ステージ解放とコンティニュー時の再開情報を保持する。
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>ステージ解放状況と、次回Stage_City開始方法を管理します。</summary>
public static class GameProgressData
{
    public enum StartMode
    {
        NormalOpening,
        ResumeAfterClear,
        RespawnAtLastLanding
    }

    const string HighestClearedKey = "Progress.HighestClearedStage";
    const string LastLandingKey = "Progress.LastLandingIndex";
    static StartMode pendingStartMode;
    public static int HighestClearedStage => Mathf.Clamp(PlayerPrefs.GetInt(HighestClearedKey, 0), 0, 4);
    public static int LastLandingIndex => Mathf.Clamp(PlayerPrefs.GetInt(LastLandingKey, 0), 0, 3);
    public static bool CanContinue => HighestClearedStage > 0;

    public static bool IsStageUnlocked(int zeroBasedIndex) => zeroBasedIndex <= HighestClearedStage;
    public static void SaveLandingSelection(int zeroBasedIndex)
    {
        PlayerPrefs.SetInt(LastLandingKey, Mathf.Clamp(zeroBasedIndex, 0, 3));
        PlayerPrefs.Save();
    }

    public static void CompleteSelectedStage()
    {
        int completedStage = LastLandingIndex + 1;
        if (completedStage > HighestClearedStage)
        {
            PlayerPrefs.SetInt(HighestClearedKey, completedStage);
            PlayerPrefs.Save();
        }
    }

    public static void ResetStageClearHistory()
    {
        PlayerPrefs.DeleteKey(HighestClearedKey);
        PlayerPrefs.Save();
    }

    public static void SetNextStartMode(StartMode mode) => pendingStartMode = mode;
    public static StartMode ConsumeStartMode()
    {
        StartMode result = pendingStartMode;
        pendingStartMode = StartMode.NormalOpening;
        return result;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InstallStartRouter()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.Equals("Stage_City", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // UnityでStage_cityを直接Playした場合だけテスト用にクリア履歴を初期化。
        // Title/ResultからLoadingを経由した場合はCONTINUEに必要な履歴を維持する。
        if (!LoadingSceneRequest.IsLoading)
        {
            ResetStageClearHistory();
        }

        StartMode startMode = ConsumeStartMode();
        if (startMode != StartMode.RespawnAtLastLanding)
        {
            return;
        }

        OpeningNewsSequence opening = Object.FindObjectOfType<OpeningNewsSequence>(true);
        if (opening != null)
        {
            opening.AbortForDirectResume();
        }

        HelicopterAutoFlight flight = Object.FindObjectOfType<HelicopterAutoFlight>(true);
        if (flight != null)
        {
            flight.AutoStartOnLoad = false;
        }

        GameObject runnerObject = new GameObject("Saved Landing Resume");
        runnerObject.AddComponent<SavedLandingResumeRunner>();
    }
}

public sealed class SavedLandingResumeRunner : MonoBehaviour
{
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    IEnumerator Start()
    {
        yield return null;
        LandingPointSelectionUI selection = FindObjectOfType<LandingPointSelectionUI>(true);
        if (selection != null)
        {
            selection.ResumeAtSavedLanding();
        }

        Destroy(gameObject);
    }
}
