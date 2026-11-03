// 【役割】移動先ステージの到着時に必要な情報をシーン間で受け渡す。
public static class StageArrivalRequest
{
    public static bool StartTimerAfterLoad { get; private set; }

    public static void Begin(bool startTimerAfterLoad)
    {
        StartTimerAfterLoad = startTimerAfterLoad;
    }

    public static bool ConsumeStartTimerRequest()
    {
        bool value = StartTimerAfterLoad;
        StartTimerAfterLoad = false;
        return value;
    }
}
