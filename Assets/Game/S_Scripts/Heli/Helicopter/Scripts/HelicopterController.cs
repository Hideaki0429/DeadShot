// 【役割】ヘリの操作入力、機体の移動・姿勢とエンジン状態を制御する。
using UnityEngine;
using UnityChan;

public class HelicopterController : MonoBehaviour
{
    public AudioSource HelicopterSound;
    public ControlPanel ControlPanel;
    public Rigidbody HelicopterModel;
    public HeliRotorController MainRotorController;
    public HeliRotorController SubRotorController;
    public float TurnForce = 3f;
    public float ForwardForce = 10f;
    public float ForwardTiltForce = 20f;
    public float TurnTiltForce = 30f;
    public float EffectiveHeight = 100f;
    public float turnTiltForcePercent = 1.5f;
    public float turnForcePercent = 1.3f;
    // ▼変更：直接プレイヤーをONにするのではなく、演出スクリプトを経由させる
    [Header("着陸後の演出管理スクリプト")]
    public PlayerDismountDirector dismountDirector;
    private float _engineForce;
    public float EngineForce
    {
        get
        {
            return _engineForce;
        }

        set
        {
            MainRotorController.RotarSpeed = value * 80;
            SubRotorController.RotarSpeed = value * 40;
            HelicopterSound.pitch = Mathf.Clamp(value / 40, 0, 1.2f);
            if (UIGameController.runtime.EngineForceView != null)
            {
                UIGameController.runtime.EngineForceView.text = string.Format("Engine value [ {0} ] ", (int)value);
            }

            _engineForce = value;
        }
    }

    [HideInInspector]
    public bool IsAutoFlying = false;
    [HideInInspector]
    public float LiftMultiplier = 1f;
    [HideInInspector]
    public bool IsManualControlLocked = false;
    public void SetHMove(float x, float y)
    {
        hMove.x = Mathf.Clamp(x, -1f, 1f);
        hMove.y = Mathf.Clamp(y, -1f, 1f);
    }

    private Vector2 hMove = Vector2.zero;
    private Vector2 hTilt = Vector2.zero;
    private float hTurn = 0f;
    public bool IsOnGround = true;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        HelicopterAutoFlight autoFlight = GetComponent<HelicopterAutoFlight>();
        if (autoFlight != null)
        {
            autoFlight.OnLandedComplete += HandleLandingComplete;
        }
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        ControlPanel.KeyPressed += OnKeyPressed;
    }

    // 物理演算の一定間隔で呼ばれます。以下の状態更新を行います。
    void FixedUpdate()
    {
        // 移動スキップで着陸Transformを検証している間は、
        // KinematicのRigidbodyへ力を加えません。
        if (HelicopterModel == null || HelicopterModel.isKinematic)
        {
            return;
        }

        LiftProcess();
        MoveProcess();
        TiltProcess();
    }

    // ▼変更：ヘリが着陸したら、プレイヤーの降下演出のスイッチを押す
    private void HandleLandingComplete()
    {
        LockManualControlAfterLanding();
        Debug.Log("[HeliController] 着陸完了。プレイヤー降下演出を要求。");
        if (dismountDirector != null)
        {
            dismountDirector.StartDismountSequence();
        }
        else
        {
            Debug.LogWarning("[HeliController] dismountDirector が割り当てられていません！");
        }
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        HelicopterAutoFlight autoFlight = GetComponent<HelicopterAutoFlight>();
        if (autoFlight != null)
        {
            autoFlight.OnLandedComplete -= HandleLandingComplete;
        }
    }

    private void MoveProcess()
    {
        var turn = TurnForce * Mathf.Lerp(hMove.x, hMove.x * (turnTiltForcePercent - Mathf.Abs(hMove.y)), Mathf.Max(0f, hMove.y));
        hTurn = Mathf.Lerp(hTurn, turn, Time.fixedDeltaTime * TurnForce);
        HelicopterModel.AddRelativeTorque(0f, hTurn * HelicopterModel.mass, 0f);
        HelicopterModel.AddRelativeForce(Vector3.forward * Mathf.Max(0f, hMove.y * ForwardForce * HelicopterModel.mass));
    }

    private void LiftProcess()
    {
        var upForce = 1 - Mathf.Clamp(HelicopterModel.transform.position.y / EffectiveHeight, 0, 1);
        upForce = Mathf.Lerp(0f, EngineForce, upForce) * HelicopterModel.mass * LiftMultiplier;
        HelicopterModel.AddRelativeForce(Vector3.up * upForce);
    }

    private void TiltProcess()
    {
        hTilt.x = Mathf.Lerp(hTilt.x, hMove.x * TurnTiltForce, Time.deltaTime);
        hTilt.y = Mathf.Lerp(hTilt.y, hMove.y * ForwardTiltForce, Time.deltaTime);
        HelicopterModel.transform.localRotation = Quaternion.Euler(hTilt.y, HelicopterModel.transform.localEulerAngles.y, -hTilt.x);
    }

    private void OnKeyPressed(PressedKeyCode[] obj)
    {
        if (IsAutoFlying || IsManualControlLocked)
        {
            return;
        }

        float tempY = 0;
        float tempX = 0;
        if (hMove.y > 0)
        {
            tempY = -Time.fixedDeltaTime;
        }
        else if (hMove.y < 0)
        {
            tempY = Time.fixedDeltaTime;
        }

        if (hMove.x > 0)
        {
            tempX = -Time.fixedDeltaTime;
        }
        else if (hMove.x < 0)
        {
            tempX = Time.fixedDeltaTime;
        }

        foreach (var pressedKeyCode in obj)
        {
            switch (pressedKeyCode)
            {
                case PressedKeyCode.SpeedUpPressed:
                    EngineForce += 0.1f;
                    break;
                case PressedKeyCode.SpeedDownPressed:
                    EngineForce -= 0.12f;
                    if (EngineForce < 0)
                    {
                        EngineForce = 0;
                    }

                    break;
                case PressedKeyCode.ForwardPressed:
                    if (IsOnGround)
                    {
                        break;
                    }

                    tempY = Time.fixedDeltaTime;
                    break;
                case PressedKeyCode.BackPressed:
                    if (IsOnGround)
                    {
                        break;
                    }

                    tempY = -Time.fixedDeltaTime;
                    break;
                case PressedKeyCode.LeftPressed:
                    if (IsOnGround)
                    {
                        break;
                    }

                    tempX = -Time.fixedDeltaTime;
                    break;
                case PressedKeyCode.RightPressed:
                    if (IsOnGround)
                    {
                        break;
                    }

                    tempX = Time.fixedDeltaTime;
                    break;
                case PressedKeyCode.TurnRightPressed:
                {
                    if (IsOnGround)
                    {
                        break;
                    }

                    var force = (turnForcePercent - Mathf.Abs(hMove.y)) * HelicopterModel.mass;
                    HelicopterModel.AddRelativeTorque(0f, force, 0);
                }

                    break;
                case PressedKeyCode.TurnLeftPressed:
                {
                    if (IsOnGround)
                    {
                        break;
                    }

                    var force = -(turnForcePercent - Mathf.Abs(hMove.y)) * HelicopterModel.mass;
                    HelicopterModel.AddRelativeTorque(0f, force, 0);
                }

                    break;
            }
        }

        hMove.x += tempX;
        hMove.x = Mathf.Clamp(hMove.x, -1, 1);
        hMove.y += tempY;
        hMove.y = Mathf.Clamp(hMove.y, -1, 1);
    }

    /// <summary>
    /// 通常着陸・スキップ着陸のどちらでも、
    /// 着陸完了後のWASD・Q/E・エンジン操作を無効化します。
    /// </summary>
    private void LockManualControlAfterLanding()
    {
        IsManualControlLocked = true;
        IsAutoFlying = false;
        IsOnGround = true;
        hMove = Vector2.zero;
        hTilt = Vector2.zero;
        hTurn = 0f;
        if (HelicopterModel != null && !HelicopterModel.isKinematic)
        {
            HelicopterModel.velocity = Vector3.zero;
            HelicopterModel.angularVelocity = Vector3.zero;
        }
    }

    // 物理的に衝突した相手と接触情報を受け取り、以下の衝突処理を行います。
    private void OnCollisionEnter()
    {
        IsOnGround = true;
    }

    private void OnCollisionExit()
    {
        IsOnGround = false;
    }
}
