// 【役割】ミニマップ以外のカメラを管理し、使用するカメラを切り替える。
using UnityEngine;

/// <summary>
/// Stage_cityのミニマップ以外のカメラ4台を一元管理します。
/// 常に選択した1台だけを有効にします。
/// </summary>
public sealed class CameraManager : MonoBehaviour
{
    public enum CameraMode
    {
        Surveillance,
        Helicopter,
        Player,
        Zoom
    }

    [Header("管理対象（MiniMapCameraは登録しない）")]
    [SerializeField]
    private Camera surveillanceCamera;
    [SerializeField]
    private Camera helicopterCamera;
    [SerializeField]
    private Camera playerCamera;
    [SerializeField]
    private Camera zoomCamera;
    [Header("初期表示")]
    [SerializeField]
    private CameraMode initialMode = CameraMode.Surveillance;
    [Header("AudioListener同期")]
    [SerializeField]
    private bool synchronizeAudioListeners = true;
    [SerializeField]
    private bool showAudioListenerDebug;
    public CameraMode CurrentMode { get; private set; }

    public Camera ActiveCamera
    {
        get
        {
            return GetCamera(CurrentMode);
        }
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        SwitchCamera(initialMode);
    }

    public void SwitchToSurveillanceCamera()
    {
        SwitchCamera(CameraMode.Surveillance);
    }

    public void SwitchToHelicopterCamera()
    {
        SwitchCamera(CameraMode.Helicopter);
    }

    public void SwitchToPlayerCamera()
    {
        SwitchCamera(CameraMode.Player);
    }

    public void SwitchToZoomCamera()
    {
        SwitchCamera(CameraMode.Zoom);
    }

    public void SwitchCamera(CameraMode mode)
    {
        Camera targetCamera = GetCamera(mode);
        if (targetCamera == null)
        {
            Debug.LogWarning($"[CameraManager] {mode}のCameraが未設定です。", this);
            return;
        }

        SetCameraEnabled(surveillanceCamera, surveillanceCamera == targetCamera);
        SetCameraEnabled(helicopterCamera, helicopterCamera == targetCamera);
        SetCameraEnabled(playerCamera, playerCamera == targetCamera);
        SetCameraEnabled(zoomCamera, zoomCamera == targetCamera);
        CurrentMode = mode;
        if (showAudioListenerDebug)
        {
            Debug.Log($"[CameraManager] Camera/AudioListener切替: {mode} / " + $"Active:{targetCamera.name}", targetCamera);
        }
    }

    private Camera GetCamera(CameraMode mode)
    {
        switch (mode)
        {
            case CameraMode.Surveillance:
                return surveillanceCamera;
            case CameraMode.Helicopter:
                return helicopterCamera;
            case CameraMode.Player:
                return playerCamera;
            case CameraMode.Zoom:
                return zoomCamera;
            default:
                return null;
        }
    }

    private void SetCameraEnabled(Camera targetCamera, bool enabled)
    {
        if (targetCamera == null)
        {
            return;
        }

        targetCamera.enabled = enabled;
        if (!synchronizeAudioListeners)
        {
            return;
        }

        AudioListener listener = targetCamera.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = enabled;
            return;
        }

        if (enabled)
        {
            Debug.LogWarning($"[CameraManager] {targetCamera.name}にAudioListenerがありません。", targetCamera);
        }
    }
}
