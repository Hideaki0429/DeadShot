// 【役割】サブマシンガンの装備・構え・移動・発砲・リロードの状態を制御する。
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// SMGの選択状態、表示モデル、Animator、射撃コンポーネントを同期する。
/// アニメーションのブレンドとTransitionはAnimator側で調整できる。
/// </summary>
[DisallowMultipleComponent]
public sealed class SubmachineGunController : MonoBehaviour
{
    static readonly int RifleSelected = Animator.StringToHash("RifleSelected");
    static readonly int RifleEquip = Animator.StringToHash("RifleEquip");
    static readonly int RifleUnequip = Animator.StringToHash("RifleUnequip");
    static readonly int Setup = Animator.StringToHash("Setup");
    [Header("参照")]
    [SerializeField]
    Animator playerAnimator;
    [SerializeField]
    WeaponSelectionUI weaponSelection;
    [SerializeField]
    OnKeyPress_Throw gun;
    [SerializeField]
    WeaponAmmoSystem ammoSystem;
    [SerializeField]
    GameObject rifleModel;
    [SerializeField]
    GameObject pistolModel;
    [Tooltip("SMG専用Gate。マズルフラッシュ・弾・スモークの発生位置として使用します。")]
    [SerializeField]
    Transform smgMuzzleGate;
    [Header("SMG銃口位置（実行中も変更可）")]
    [Tooltip("SMG専用Gateを基準にした銃口のローカル位置")]
    public Vector3 smgMuzzleLocalPosition;
    [Tooltip("SMG専用Gateを基準にした銃口のローカル角度")]
    public Vector3 smgMuzzleLocalEulerAngles;
    [Tooltip("WFXMR_MF 4P RIFLE2の放射軸を銃口正面へ合わせる角度補正")]
    public Vector3 smgMuzzleEffectLocalEulerAngles = new Vector3(0f, -90f, 0f);
    Transform smgRuntimeMuzzlePoint;
    [Header("モデルの手元調整（実行中も変更可）")]
    [Tooltip("通常保持時のローカル位置")]
    public Vector3 localPosition;
    [Tooltip("通常保持時のローカル角度")]
    public Vector3 localEulerAngles;
    [Tooltip("通常保持時のローカルサイズ")]
    public Vector3 localScale = Vector3.one;
    [Header("武器構え時のモデル調整（実行中も変更可）")]
    [Tooltip("構え時のローカル位置。通常保持位置から滑らかに移行します。")]
    public Vector3 aimLocalPosition = new Vector3(.25f, 0f, -.15f);
    [Tooltip("構え時のローカル角度。通常保持角度から滑らかに移行します。")]
    public Vector3 aimLocalEulerAngles = new Vector3(29.7f, -79.9f, -80.6f);
    [Tooltip("構え時のローカルサイズ")]
    public Vector3 aimLocalScale = Vector3.one;
    [Tooltip("通常保持と構え位置を切り替える速さ。値を大きくすると素早く移行します。")]
    [Min(.01f)]
    public float aimTransformBlendSpeed = 8f;
    [Header("アニメーター")]
    [Tooltip("サブマシンガン選択中に有効化するフルボディレイヤー。")]
    public string fullBodyLayerName = "SMG Full Body";
    [Tooltip("移動中の射撃・リロードに使う上半身レイヤー。")]
    public string upperBodyLayerName = "SMG Upper Body";
    [Min(0f)]
    public float layerBlendSpeed = 12f;
    [Header("武器収納")]
    [Tooltip("RiflePutAway開始からSMGを非表示にし、次の武器へ確定変更するまでの時間。RiflePutAwayの終了フレームに合わせて調整します。")]
    [Min(0f)]
    public float putAwaySwitchDelay = 2.3f;
    bool wasSelected;
    float aimTransformBlend;
    Coroutine putAwayRoutine;
    public bool IsPuttingAway { get; private set; }
    public bool IsSelected => weaponSelection != null && weaponSelection.SelectedWeaponId == "smg";
    public bool IsAiming => IsSelected && playerAnimator != null && playerAnimator.GetBool(Setup);
    public bool IsReloading => gun != null && gun.isReloading;
    public WeaponAmmoSystem AmmoSystem => ammoSystem;

    /// <summary>
    /// 通常保持から歩行へ入る際、SMGの歩行ステートが実際に始まるまで物理移動を待たせる。
    /// Animator側のTransition/Blend設定には手を加えない。
    /// </summary>
    public bool ShouldHoldMovementForHeldLocomotion(float moveMagnitude)
    {
        if (!IsSelected || IsPuttingAway || IsAiming || playerAnimator == null || moveMagnitude <= .01f)
        {
            return false;
        }

        int layer = playerAnimator.GetLayerIndex(fullBodyLayerName);
        if (layer < 0)
        {
            return false;
        }

        AnimatorStateInfo current = playerAnimator.GetCurrentAnimatorStateInfo(layer);
        AnimatorStateInfo next = playerAnimator.GetNextAnimatorStateInfo(layer);
        // 遷移完了まで待つと、Blend中に約1秒その場足踏みになる。
        // Walk/Runへの遷移が始まったフレームから物理移動も開始する。
        bool locomotionStarted = current.IsName("RifleWalk") || current.IsName("RifleRun") || (playerAnimator.IsInTransition(layer) && (next.IsName("RifleWalk") || next.IsName("RifleRun")));
        return !locomotionStarted;
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>();
        }

        if (weaponSelection == null)
        {
            weaponSelection = FindObjectOfType<WeaponSelectionUI>(true);
        }

        if (gun == null)
        {
            foreach (OnKeyPress_Throw candidate in GetComponentsInChildren<OnKeyPress_Throw>(true))
            {
                if (candidate.requiredWeaponId == "smg")
                {
                    gun = candidate;
                    break;
                }
            }
        }

        if (ammoSystem == null)
        {
            foreach (WeaponAmmoSystem candidate in GetComponents<WeaponAmmoSystem>())
            {
                if (candidate.WeaponId == "smg" || candidate.AmmoItemId == "m1917")
                {
                    ammoSystem = candidate;
                    break;
                }
            }
        }

        ResolveSmgMuzzleGate();
        if (gun != null)
        {
            EnsureSmgMuzzlePoint();
            AimFocusController focus = gun.GetComponent<AimFocusController>();
            if (focus == null)
            {
                focus = gun.gameObject.AddComponent<AimFocusController>();
            }

            focus.BindShooting(gun);
            gun.SetAimFocus(focus);
        }

        if (ammoSystem != null && gun != null)
        {
            ammoSystem.BindGun(gun);
        }

        ApplyModelTransform();
        SetImmediate(IsSelected);
        wasSelected = IsSelected;
    }

    void ResolveSmgMuzzleGate()
    {
        if (smgMuzzleGate != null)
        {
            return;
        }

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            string lower = child.name.ToLowerInvariant();
            bool isGate = child.CompareTag("Gate") || lower.Contains("gate");
            bool isSmg = lower.Contains("smg") || lower.Contains("submachine") || lower.Contains("rifle");
            if (isGate && isSmg)
            {
                smgMuzzleGate = child;
                return;
            }
        }
    }

    void EnsureSmgMuzzlePoint()
    {
        if (smgMuzzleGate == null || gun == null)
        {
            return;
        }

        if (smgRuntimeMuzzlePoint == null)
        {
            Transform existing = smgMuzzleGate.Find("SMG Muzzle Point");
            if (existing != null)
            {
                smgRuntimeMuzzlePoint = existing;
            }
            else
            {
                GameObject point = new GameObject("SMG Muzzle Point");
                smgRuntimeMuzzlePoint = point.transform;
                smgRuntimeMuzzlePoint.SetParent(smgMuzzleGate, false);
            }
        }

        smgRuntimeMuzzlePoint.localPosition = smgMuzzleLocalPosition;
        smgRuntimeMuzzlePoint.localRotation = Quaternion.Euler(smgMuzzleLocalEulerAngles);
        gun.SetMuzzlePoint(smgRuntimeMuzzlePoint);
        gun.SetMuzzleEffectLocalEulerAngles(smgMuzzleEffectLocalEulerAngles);
    }

    // Inspectorの値の変更・読み込み時に、設定の検証や反映を行います。
    void OnValidate()
    {
        if (Application.isPlaying)
        {
            EnsureSmgMuzzlePoint();
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        bool selected = IsSelected;
        if (IsPuttingAway)
        {
            if (rifleModel != null)
            {
                rifleModel.SetActive(true);
            }

            if (pistolModel != null && !pistolModel.activeSelf)
            {
                pistolModel.SetActive(true);
            }

            SetLayer(fullBodyLayerName, 1f);
            SetLayer(upperBodyLayerName, 1f);
            return;
        }

        if (selected != wasSelected)
        {
            if (playerAnimator != null)
            {
                playerAnimator.SetBool(RifleSelected, selected);
                playerAnimator.ResetTrigger(selected ? RifleUnequip : RifleEquip);
                playerAnimator.SetTrigger(selected ? RifleEquip : RifleUnequip);
            }

            if (!selected && playerAnimator != null)
            {
                playerAnimator.SetBool(Setup, false);
            }

            wasSelected = selected;
        }

        if (rifleModel != null)
        {
            rifleModel.SetActive(selected);
        }

        // Pistol_00は射撃コンポーネント用の常設ルート。表示切替対象にしない。
        if (pistolModel != null && !pistolModel.activeSelf)
        {
            pistolModel.SetActive(true);
        }

        BlendLayer(fullBodyLayerName, selected ? 1f : 0f);
        BlendLayer(upperBodyLayerName, selected ? 1f : 0f);
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        EnsureSmgMuzzlePoint();
        // Animatorがボーンと子オブジェクトを評価した後に適用し、
        // 構えアニメーションによるモデルTransformの上書きを防ぐ。
        if (IsSelected || IsPuttingAway)
        {
            ApplyModelTransform();
        }
    }

    /// <summary>RiflePutAwayを最後まで見せた後に武器選択を確定する。</summary>
    public bool PutAwayThenSwitch(Action commitWeaponSwitch)
    {
        if (!IsSelected || IsPuttingAway || commitWeaponSwitch == null)
        {
            return false;
        }

        putAwayRoutine = StartCoroutine(PutAwayRoutine(commitWeaponSwitch));
        return true;
    }

    IEnumerator PutAwayRoutine(Action commitWeaponSwitch)
    {
        IsPuttingAway = true;
        if (playerAnimator != null)
        {
            playerAnimator.SetBool(Setup, false);
            playerAnimator.ResetTrigger(RifleEquip);
            playerAnimator.ResetTrigger(RifleUnequip);
            playerAnimator.SetTrigger(RifleUnequip);
            SetLayer(fullBodyLayerName, 1f);
            SetLayer(upperBodyLayerName, 1f);
        }

        if (rifleModel != null)
        {
            rifleModel.SetActive(true);
        }

        // 固定秒数だけで次武器へ切り替えると、クリップやTransitionの調整後に
        // RiflePutAwayの途中で別武器のIdleが見えてしまう。実ステートの終了も待つ。
        float minimumDelay = Mathf.Max(0f, putAwaySwitchDelay);
        float elapsed = 0f;
        bool enteredPutAway = false;
        int fullBodyLayer = playerAnimator != null ? playerAnimator.GetLayerIndex(fullBodyLayerName) : -1;
        while (elapsed < minimumDelay || (fullBodyLayer >= 0 && !HasPutAwayFinished(fullBodyLayer, ref enteredPutAway)))
        {
            yield return null;
            elapsed += Time.deltaTime;
            // Animator構成不良時に武器変更が永久停止することだけを防ぐ安全策。
            if (elapsed >= Mathf.Max(minimumDelay + 5f, 10f))
            {
                break;
            }
        }

        // callback内でSelectedWeaponIdが次の武器へ変わる。
        wasSelected = false;
        commitWeaponSwitch.Invoke();
        if (rifleModel != null)
        {
            rifleModel.SetActive(false);
        }

        if (playerAnimator != null)
        {
            playerAnimator.SetBool(RifleSelected, false);
        }

        SetLayer(fullBodyLayerName, 0f);
        SetLayer(upperBodyLayerName, 0f);
        IsPuttingAway = false;
        putAwayRoutine = null;
    }

    bool HasPutAwayFinished(int layer, ref bool enteredPutAway)
    {
        AnimatorStateInfo current = playerAnimator.GetCurrentAnimatorStateInfo(layer);
        AnimatorStateInfo next = playerAnimator.GetNextAnimatorStateInfo(layer);
        bool currentIsPutAway = current.IsName("RiflePutAway");
        bool nextIsPutAway = playerAnimator.IsInTransition(layer) && next.IsName("RiflePutAway");
        if (currentIsPutAway || nextIsPutAway)
        {
            enteredPutAway = true;
        }

        return enteredPutAway && !currentIsPutAway && !nextIsPutAway && !playerAnimator.IsInTransition(layer);
    }

    void SetImmediate(bool selected)
    {
        if (playerAnimator != null)
        {
            playerAnimator.SetBool(RifleSelected, selected);
            SetLayer(fullBodyLayerName, selected ? 1f : 0f);
            SetLayer(upperBodyLayerName, selected ? 1f : 0f);
        }

        if (rifleModel != null)
        {
            rifleModel.SetActive(selected);
        }
    }

    void ApplyModelTransform()
    {
        if (rifleModel == null)
        {
            return;
        }

        float target = IsAiming && !IsPuttingAway ? 1f : 0f;
        aimTransformBlend = Mathf.MoveTowards(aimTransformBlend, target, Mathf.Max(.01f, aimTransformBlendSpeed) * Time.deltaTime);
        float blend = Mathf.SmoothStep(0f, 1f, aimTransformBlend);
        Transform model = rifleModel.transform;
        model.localPosition = Vector3.LerpUnclamped(localPosition, aimLocalPosition, blend);
        model.localRotation = Quaternion.SlerpUnclamped(Quaternion.Euler(localEulerAngles), Quaternion.Euler(aimLocalEulerAngles), blend);
        model.localScale = Vector3.LerpUnclamped(localScale, aimLocalScale, blend);
    }

    void BlendLayer(string layerName, float target)
    {
        if (playerAnimator == null)
        {
            return;
        }

        int layer = playerAnimator.GetLayerIndex(layerName);
        if (layer < 0)
        {
            return;
        }

        float value = Mathf.MoveTowards(playerAnimator.GetLayerWeight(layer), target, Mathf.Max(0.01f, layerBlendSpeed) * Time.unscaledDeltaTime);
        playerAnimator.SetLayerWeight(layer, value);
    }

    void SetLayer(string layerName, float value)
    {
        int layer = playerAnimator != null ? playerAnimator.GetLayerIndex(layerName) : -1;
        if (layer >= 0)
        {
            playerAnimator.SetLayerWeight(layer, value);
        }
    }
}
