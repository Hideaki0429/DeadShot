// 【役割】ロード対象のシーン名などをロード画面へ受け渡す。
/// <summary>
/// Titleなどの遷移元から専用Loadingシーンへ、
/// 最終的に読み込むステージ名を渡します。
/// </summary>
public static class LoadingSceneRequest
{
    public static string TargetSceneName { get; private set; }
    public static bool IsLoading { get; private set; }

    public static void Begin(string targetSceneName)
    {
        TargetSceneName = targetSceneName;
        IsLoading = true;
    }

    public static void Clear()
    {
        TargetSceneName = string.Empty;
        IsLoading = false;
    }
}
