// 【役割】ヘリの投光器の向きと照明を制御する。
using UnityEngine;

[RequireComponent(typeof(Light))]
public class HelicopterSpotlight : MonoBehaviour
{
    // アタッチするスポットライトコンポーネント
    private Light _light;
    [Header("ライトのON/OFFキー")]
    public KeyCode toggleKey = KeyCode.F; // FキーでON/OFF
    [Header("距離モード切り替えキー")]
    public KeyCode distanceModeKey = KeyCode.G; // Gキーで距離切り替え
    [Header("ライトの設定")]
    // 遠距離モード時の設定
    public float farDistance = 150f;
    public float farIntensity = 80f;
    // 通常（近距離）モード時の設定
    public float normalDistance = 50f;
    public float normalIntensity = 20f;
    [Header("自動制御")]
    [Tooltip("オンなら、高度とエンジン出力に応じてライトを自動制御します")]
    [SerializeField]
    private bool automaticControl = true;
    [Tooltip("高度を判定するTransform。未設定ならHelicopterControllerのRigidbodyを使用します")]
    [SerializeField]
    private Transform altitudeReference;
    [Tooltip("ヘリのEngineForceを監視するHelicopterController。未設定なら親から自動取得します")]
    [SerializeField]
    private HelicopterController helicopterController;
    [Tooltip("この高度以下で弱モードへ切り替えます（World Y座標）")]
    [SerializeField]
    private float weakModeAltitude = 35f;
    [Tooltip("モード切替のちらつきを防ぐための高度差")]
    [Min(0f)]
    [SerializeField]
    private float altitudeHysteresis = 3f;
    [Tooltip("EngineForceがこの値以下なら、エンジン停止としてライトを消灯します")]
    [Min(0f)]
    [SerializeField]
    private float engineOffThreshold = 0.1f;
    [Tooltip("オンなら、自動制御中でもF/Gキーでの手動操作を無効にします")]
    [SerializeField]
    private bool disableManualInputInAutomaticMode = true;
    // ライトの変化速度（ON/OFF時やモード切替時のフェード）
    public float changeSpeed = 5f;
    // サーチライトが向くべき方向（ヘリコプターの正面）
    private Quaternion targetRotation;
    // 現在の目標光度
    private float targetIntensity;
    // 現在の目標範囲
    private float targetRange;
    // ライトの状態管理
    private bool isLightOn = true;
    private bool isFarMode = true;
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        // 自身のGameObjectについているLightコンポーネントを取得
        _light = GetComponent<Light>();
        // ライトがスポットライトでない場合は警告を出す
        if (_light.type != LightType.Spot)
        {
            Debug.LogWarning("HelicopterSpotlightスクリプトは、Typeが'Spot'のLightコンポーネントにアタッチしてください。");
        }

        ResolveAutomaticControlReferences();
        // 初期設定
        UpdateAutomaticLightState();
        UpdateLightTargets();
        _light.intensity = isLightOn ? targetIntensity : 0f;
        _light.range = targetRange;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (automaticControl)
        {
            UpdateAutomaticLightState();
        }

        if (!automaticControl || !disableManualInputInAutomaticMode)
        {
            HandleManualInput();
        }

        // ライトの強さと範囲を滑らかに変化させる
        float currentTargetIntensity = isLightOn ? targetIntensity : 0f;
        if (isLightOn)
        {
            _light.enabled = true;
        }

        _light.intensity = Mathf.Lerp(_light.intensity, currentTargetIntensity, changeSpeed * Time.deltaTime);
        _light.range = Mathf.Lerp(_light.range, targetRange, changeSpeed * Time.deltaTime);
        if (!isLightOn && _light.intensity <= 0.05f)
        {
            _light.intensity = 0f;
            _light.enabled = false;
        }
    // 4. ライトの向きを調整する（オプション：機体の揺れなどに対応）
    // このスクリプトがヘリコプターの機首に直接アタッチされている場合、
    // ローカル座標での向きを固定することで機体に合わせて動きます。
    // もしジンバルなどで動かしたい場合は、ここを記述します。
    // 例えば、常に真下より少し前を向くようにするなど。
    // 今回はシンプルにローカル座標のZ軸正方向を向くものとします。
    // transform.localRotation = Quaternion.Euler(targetRotation.eulerAngles);
    }

    private void HandleManualInput()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isLightOn = !isLightOn;
            UpdateLightTargets();
        }

        if (isLightOn && Input.GetKeyDown(distanceModeKey))
        {
            isFarMode = !isFarMode;
            UpdateLightTargets();
        }
    }

    /// <summary>
    /// 高度が高い間は強モード、着陸に近づくと弱モードにします。
    /// EngineForceが停止値まで下がったらライトを消灯します。
    /// </summary>
    private void UpdateAutomaticLightState()
    {
        if (helicopterController == null)
        {
            ResolveAutomaticControlReferences();
        }

        bool engineRunning = helicopterController == null || helicopterController.EngineForce > engineOffThreshold;
        isLightOn = engineRunning;
        if (!engineRunning)
        {
            // エンジン停止と同時に消灯します。
            _light.intensity = 0f;
            _light.enabled = false;
            return;
        }

        if (altitudeReference == null)
        {
            ResolveAutomaticControlReferences();
        }

        if (altitudeReference == null)
        {
            return;
        }

        float currentAltitude = altitudeReference.position.y;
        if (isFarMode && currentAltitude <= weakModeAltitude)
        {
            isFarMode = false;
        }
        else if (!isFarMode && currentAltitude >= weakModeAltitude + altitudeHysteresis)
        {
            isFarMode = true;
        }

        UpdateLightTargets();
    }

    private void ResolveAutomaticControlReferences()
    {
        if (helicopterController == null)
        {
            helicopterController = GetComponentInParent<HelicopterController>();
        }

        if (altitudeReference == null)
        {
            if (helicopterController != null && helicopterController.HelicopterModel != null)
            {
                altitudeReference = helicopterController.HelicopterModel.transform;
            }
            else
            {
                altitudeReference = transform.root;
            }
        }
    }

    // 現在のモードに応じて目標値を更新するメソッド
    void UpdateLightTargets()
    {
        if (isFarMode)
        {
            targetIntensity = farIntensity;
            targetRange = farDistance;
        }
        else
        {
            targetIntensity = normalIntensity;
            targetRange = normalDistance;
        }
    }
}
