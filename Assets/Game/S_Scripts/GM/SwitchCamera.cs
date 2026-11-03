// 【役割】視点やズームの切り替えと、視点に対応するモデルの表示を制御する。
using UnityEngine;

public class SwitchCamera : MonoBehaviour
{
    const string DefaultAimViewPreference = "DeadShot.DefaultFirstPersonAim";
    [SerializeField]
    private CameraManager cameraManager;
    public Camera mainCamera;
    public Camera zoomCamera;
    public GameObject target; // ターゲットオブジェクトへの参照
    [Header("--- 三人称カメラ ---")]
    [SerializeField]
    private ThirdPersonCameraController thirdPersonCamera;
    [SerializeField]
    private float normalFieldOfView = 60f;
    [SerializeField]
    private float aimFieldOfView = 55f;
    [SerializeField]
    private float qViewFieldOfView = 60f;
    [Header("--- 構え開始時の視点 ---")]
    [Tooltip("オン：1人称（既存Q視点）で構え開始。オフ：3人称。構え中は視点切り替え可能です。")]
    [SerializeField]
    private bool defaultFirstPersonView = false;
    [Header("--- Zoom時のみ表示するUI ---")]
    [Tooltip("弾数・マガジン数などが入った銃UIのCanvasGroup")]
    [SerializeField]
    private CanvasGroup ammoUiCanvasGroup;
    private bool isZoomView = false;
    private bool isQView = false;
    // 1倍→3倍→5倍
    private int zoomLevel = 1;
    public bool IsShootingView
    {
        get
        {
            return isZoomView;
        }
    }

    public bool IsQView
    {
        get
        {
            return isQView;
        }
    }

    public bool DefaultFirstPersonView => defaultFirstPersonView;

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        if (PlayerPrefs.HasKey(DefaultAimViewPreference))
        {
            defaultFirstPersonView = PlayerPrefs.GetInt(DefaultAimViewPreference, defaultFirstPersonView ? 1 : 0) != 0;
        }

        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<CameraManager>();
        }

        if (thirdPersonCamera == null)
        {
            thirdPersonCamera = FindObjectOfType<ThirdPersonCameraController>();
        }

        // CameraManager使用時は、ステージ開始直後の監視カメラを
        // ここでPlayerカメラへ上書きしません。
        // Playerカメラへの切替はPlayerDismountDirectorが行います。
        if (cameraManager == null)
        {
            SetPlayerCameraActive();
        }

        // 開始時はCameraManagerの監視カメラ設定を維持する。
        if (zoomCamera != null)
        {
            zoomCamera.enabled = false;
        }

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetViewState(false, false);
        }

        if (mainCamera != null)
        {
            mainCamera.fieldOfView = normalFieldOfView;
        }

        UpdateTargetState();
    }

    public void EnterSetupMode()
    {
        isZoomView = true;
        if (isQView && thirdPersonCamera != null)
        {
            thirdPersonCamera.CaptureCurrentAimPoint();
        }

        UpdateCameraView();
    }

    public void SetDefaultFirstPersonView(bool value, bool save = true)
    {
        defaultFirstPersonView = value;
        if (save)
        {
            PlayerPrefs.SetInt(DefaultAimViewPreference, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        // Change the active aiming camera immediately when it is currently open.
        if (isZoomView)
        {
            isQView = value;
            if (isQView && thirdPersonCamera != null)
            {
                thirdPersonCamera.CaptureCurrentAimPoint();
            }

            UpdateCameraView();
        }
    }

    public void ExitSetupMode()
    {
        isZoomView = false;
        UpdateCameraView();
    }

    public void ToggleView()
    {
        bool enteringQView = !isQView;
        if (enteringQView && thirdPersonCamera != null)
        {
            thirdPersonCamera.CaptureCurrentAimPoint();
        }

        isQView = enteringQView;
        UpdateCameraView();
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        // Vキーで倍率変更
        if (GameInputManager.Instance.ChangeZoomPressed)
        {
            zoomLevel++;
            if (zoomLevel > 3)
            {
                zoomLevel = 1;
            }

            ApplyZoom();
        }
    }

    void ApplyZoom()
    {
        switch (zoomLevel)
        {
            case 1:
                SetGameplayFieldOfView(isQView ? qViewFieldOfView : aimFieldOfView); // 1倍
                break;
            case 2:
                SetGameplayFieldOfView(20f); // 3倍
                break;
            case 3:
                SetGameplayFieldOfView(12f); // 5倍
                break;
        }
    }

    private void SetPlayerCameraActive()
    {
        if (cameraManager != null)
        {
            cameraManager.SwitchToPlayerCamera();
            return;
        }

        if (mainCamera != null)
        {
            mainCamera.enabled = true;
        }

        if (zoomCamera != null)
        {
            zoomCamera.enabled = false;
        }
    }

    private void SetZoomCameraActive()
    {
        // 三人称化後は同じPlayerカメラを肩へ移動させる。
        SetPlayerCameraActive();
    }

    private void UpdateCameraView()
    {
        SetZoomCameraActive();
        if (zoomCamera != null)
        {
            zoomCamera.enabled = false;
        }

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetViewState(isZoomView, isQView);
        }

        ApplyZoom();
        UpdateTargetState();
    }

    private void SetGameplayFieldOfView(float fieldOfView)
    {
        if (mainCamera != null)
        {
            mainCamera.fieldOfView = isZoomView ? fieldOfView : normalFieldOfView;
        }
    }

    // ターゲットオブジェクトの表示状態を更新するメソッド
    void UpdateTargetState()
    {
        if (target != null)
        {
            // isZoomView が true なら表示、false なら非表示
            target.SetActive(isZoomView);
        }

        SetAmmoUiVisible(isZoomView);
    }

    private void SetAmmoUiVisible(bool visible)
    {
        if (ammoUiCanvasGroup == null)
        {
            return;
        }

        ammoUiCanvasGroup.alpha = visible ? 1f : 0f;
        ammoUiCanvasGroup.interactable = visible;
        ammoUiCanvasGroup.blocksRaycasts = visible;
    }
}
