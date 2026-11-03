// 【役割】自由視点カメラの入力による回転を制御する。
using UnityEngine;

public class FreeLookCamera : MonoBehaviour
{
    [Header("ターゲットと距離")]
    public Transform Target;
    public float Distance = 10f; // ヘリからカメラまでの距離
    [Header("カメラの操作感度と制限")]
    public float MouseSensitivity = 300f; // マウスの感度
    public float MinYAngle = -20f; // カメラが下に行ける限界の角度（見上げる限界）
    public float MaxYAngle = 80f; // カメラが上に行ける限界の角度（見下ろす限界）
    [Header("操作を許可する飛行フェーズ")]
    [Tooltip("ホバリング終了後、最終目的地へ飛行している間だけカメラ操作を許可するHelicopterAutoFlight")]
    [SerializeField]
    private HelicopterAutoFlight helicopterAutoFlight;
    private float currentX = 0f; // 現在の横の回転角度
    private float currentY = 0f; // 現在の縦の回転角度
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        // 初期状態のカメラの角度を取得しておく
        Vector3 angles = transform.eulerAngles;
        currentX = angles.y;
        currentY = angles.x;
    }

    // カメラの移動は、ターゲットが動いた"後"に行うため LateUpdate を使用します
    void LateUpdate()
    {
        if (PauseMenuController.IsPaused)
        {
            return;
        }

        if (Target == null)
        {
            return;
        }

        if (CanControlCamera() && GameInputManager.Instance.Mode != GameInputManager.InputMode.Dialogue)
        {
            // 1. マウスの移動量を取得
            // ※もしマウスではなく、別のキーボード入力で視点移動したい場合はここを変更します
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            // 2. 入力に応じて角度を更新
            currentX += mouseX * MouseSensitivity * Time.deltaTime;
            currentY -= mouseY * MouseSensitivity * Time.deltaTime; // 上下反転させたい場合は += に変更
        }

        // 3. 縦の角度を制限（地面にめり込んだり、真上を通り越したりしないように）
        currentY = Mathf.Clamp(currentY, MinYAngle, MaxYAngle);
        // 4. 新しい回転を計算
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        // 5. ターゲットの位置から、回転させた方向の逆へ「Distance（距離）」分だけ離した位置を計算
        Vector3 position = Target.position - (rotation * Vector3.forward * Distance);
        // 6. カメラに位置と回転を適用
        transform.position = position;
        transform.rotation = rotation;
    }

    /// <summary>
    /// ホバリングから最終目的地へ向かう飛行中だけ、プレイヤーの視点操作を許可します。
    /// それ以外のOP・上昇・経由地点への飛行・ホバリング・着陸中は角度を固定します。
    /// </summary>
    private bool CanControlCamera()
    {
        return helicopterAutoFlight != null && helicopterAutoFlight.CurrentPhase == HelicopterAutoFlight.FlightPhase.FlyToDestination;
    }
}
