// 【役割】キーボードとコントローラーの入力を、ゲーム・会話・メニューの状態に応じて提供する。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;

#endif
// All gameplay bindings are edited on this component. UI owners explicitly acquire dialogue input.
[DefaultExecutionOrder(-1000)]
public sealed class GameInputManager : MonoBehaviour
{
    public enum InputMode
    {
        Gameplay,
        Dialogue,
        Disabled,
        Paused
    }

    public static GameInputManager Instance { get; private set; }

    [Header("移動")]
    public KeyCode forward = KeyCode.W, back = KeyCode.S, left = KeyCode.A, right = KeyCode.D;
    public KeyCode run = KeyCode.LeftShift;
    [Header("戦闘")]
    public KeyCode aim = KeyCode.Mouse1, attack = KeyCode.Mouse0, reload = KeyCode.R;
    public KeyCode changeView = KeyCode.C, changeZoom = KeyCode.V;
    [Header("メニュー・ショートカット")]
    public KeyCode map = KeyCode.M, items = KeyCode.Tab, pause = KeyCode.P, heal = KeyCode.Q;
    public KeyCode weaponUp = KeyCode.W, weaponDown = KeyCode.S;
    public KeyCode weaponLeft = KeyCode.A, weaponRight = KeyCode.D;
    [Header("武器の直接選択")]
    public KeyCode weaponQuickLeft = KeyCode.F, weaponQuickUp = KeyCode.T;
    public KeyCode weaponQuickDown = KeyCode.G, weaponQuickRight = KeyCode.H;
    [Header("武器選択パネル")]
    public KeyCode weaponMenu = KeyCode.E;
    public string wheelAxis = "Mouse ScrollWheel";
    [Header("会話")]
    public KeyCode confirm = KeyCode.Return, alternateConfirm = KeyCode.KeypadEnter, mouseConfirm = KeyCode.Mouse0;
    public KeyCode selectUp = KeyCode.UpArrow, selectDown = KeyCode.DownArrow;
    public KeyCode selectLeft = KeyCode.LeftArrow, selectRight = KeyCode.RightArrow;
    [Header("視点")]
    public string lookX = "Mouse X", lookY = "Mouse Y";
    [Header("Nintendo Switch Pro コントローラー")]
    [Tooltip("拠点シーンとして扱うシーン名。大文字・小文字は区別しません。")]
    public string controllerBaseSceneName = "Stage_City";
    [Tooltip("各ステージで使う右スティックの視点感度。実行中も調整できます。")]
    [FormerlySerializedAs("controllerLookSensitivity")]
    [Min(0f)]
    public float stageControllerLookSensitivity = .6f;
    [Tooltip("拠点で使う右スティックの視点感度。実行中も調整できます。")]
    [Min(0f)]
    public float baseControllerLookSensitivity = 2f;
    [Range(0f, .95f)]
    public float controllerStickDeadZone = .18f;
    public bool invertControllerLookY;
    [Tooltip("グレネード距離調整に使う右スティック上下の感度。")]
    [Min(0f)]
    public float controllerGrenadeDistanceSensitivity = .12f;
    [Header("追加操作")]
    public KeyCode jump = KeyCode.Space, releaseCursor = KeyCode.Escape;
    public bool JumpPressed => false; // ジャンプ操作は現在無効。
    public bool RollPressed => !WeaponInputBlocked && (Pressed(jump) || GamepadSouthPressed);

    private readonly List<Object> dialogueOwners = new List<Object>();
    private readonly HashSet<KeyCode> blocked = new HashSet<KeyCode>();
    private UnityChan.UnityChanControlScriptWithRgidBody player;
    private int transitionFrame = -1;
    private bool paused;
    public bool PausePressed => weaponSelectionOwner == null && Time.frameCount != transitionFrame && (Down(pause) || GamepadStartPressed);
    public bool PauseConfirmPressed => paused && Time.frameCount != transitionFrame && (Down(confirm) || Down(alternateConfirm) || GamepadEastPressed);
    public bool PauseBackPressed => paused && Time.frameCount != transitionFrame && (Down(releaseCursor) || GamepadSouthPressed);
    public int PauseSelection => !paused || Time.frameCount == transitionFrame ? 0 : (Down(selectDown) || GamepadDpadDownPressed ? 1 : 0) - (Down(selectUp) || GamepadDpadUpPressed ? 1 : 0);

    public void SetPaused(bool value)
    {
        paused = value;
        transitionFrame = Time.frameCount;
        foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKey(key))
            {
                blocked.Add(key);
            }
        }
    }

    /// <summary>
    /// シーン導入を使わず直接ゲームプレイを開始するステージ用。
    /// DontDestroyOnLoadで残ったポーズ・会話・武器選択状態を解除する。
    /// </summary>
    public void ResetForGameplay()
    {
        paused = false;
        dialogueOwners.Clear();
        weaponSelectionOwner = null;
        weaponSelectionEndFrame = -1;
        blocked.Clear();
        transitionFrame = Time.frameCount;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>("GameInputManager");
        if (prefab != null)
        {
            Instantiate(prefab);
        }
        else
        {
            new GameObject("Game Input Manager").AddComponent<GameInputManager>();
        }
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        KeyboardBindingStore.Initialize(this);
        DontDestroyOnLoad(gameObject);
    }

    public InputMode Mode => paused ? InputMode.Paused : dialogueOwners.Count > 0 ? InputMode.Dialogue : player != null && player.isActiveAndEnabled && player.canControl ? InputMode.Gameplay : InputMode.Disabled;
    public bool Gameplay => Mode == InputMode.Gameplay && Time.frameCount != transitionFrame && !ZombieBiteGrab.PlayerIsGrabbed;

    private bool Held(KeyCode key) => !blocked.Contains(key) && Input.GetKey(key);
    private bool Down(KeyCode key) => !blocked.Contains(key) && Input.GetKeyDown(key);
    public bool Pressed(KeyCode key) => Gameplay && Down(key);
    public Vector2 Move => Gameplay ? Vector2.ClampMagnitude(new Vector2((Held(right) ? 1 : 0) - (Held(left) ? 1 : 0), (Held(forward) ? 1 : 0) - (Held(back) ? 1 : 0)) + GamepadMove, 1f) : Vector2.zero;
    public Vector2 Look => Gameplay ? new Vector2(Input.GetAxis(lookX), Input.GetAxis(lookY)) + GamepadLook : Vector2.zero;
    public bool RunHeld => Gameplay && (Held(run) || GamepadLeftStickHeld);

    private Object weaponSelectionOwner;
    public bool WeaponSelectionActive => weaponSelectionOwner != null;

    private int weaponSelectionEndFrame = -1;
    // Switch ProのRショルダーで感染制限時間を回復。キーボードQは従来どおり。
    public bool HealPressed => !WeaponInputBlocked && (Pressed(heal) || GamepadRightShoulderPressed);
    private bool WeaponInputBlocked => PlayerGrenadeAnimation.Busy || PlayerHealing.Busy || PlayerRollController.Busy || weaponSelectionOwner != null || weaponSelectionEndFrame == Time.frameCount;

    public void SetWeaponSelection(Object owner, bool active)
    {
        if (active)
        {
            weaponSelectionOwner = owner;
        }
        else if (weaponSelectionOwner == owner)
        {
            weaponSelectionOwner = null;
            weaponSelectionEndFrame = Time.frameCount;
            foreach (var key in new[]
            {
                aim,
                attack,
                reload,
                confirm,
                alternateConfirm,
                mouseConfirm
            }

            )
            {
                if (Input.GetKey(key))
                {
                    blocked.Add(key);
                }
            }
        }
    }

    // コントローラーでの選択は十字キー専用。左スティックは移動にだけ使う。
    public int WeaponDirection => PlayerGrenadeAnimation.Busy || PlayerHealing.Busy ? -1 : GamepadDpadUpPressed ? 0 : GamepadDpadRightPressed ? 1 : GamepadDpadDownPressed ? 2 : GamepadDpadLeftPressed ? 3 : Pressed(weaponUp) ? 0 : Pressed(weaponRight) ? 1 : Pressed(weaponDown) ? 2 : Pressed(weaponLeft) ? 3 : -1;
    public int WeaponQuickDirection => PlayerGrenadeAnimation.Busy || PlayerHealing.Busy ? -1 : Pressed(weaponQuickLeft) ? 3 : Pressed(weaponQuickUp) ? 0 : Pressed(weaponQuickDown) ? 2 : Pressed(weaponQuickRight) ? 1 : -1;
    public bool WeaponMenuPressed => !PlayerGrenadeAnimation.Busy && !PlayerHealing.Busy && (Pressed(weaponMenu) || GamepadLeftShoulderPressed);
    public float WeaponScroll => Gameplay && !PlayerHealing.Busy && !PlayerGrenadeAnimation.Busy ? Input.GetAxis(wheelAxis) : 0f;
    public bool WeaponConfirmPressed => Pressed(confirm) || Pressed(alternateConfirm) || Pressed(mouseConfirm) || GamepadEastPressed;
    public bool AimPressed => !WeaponInputBlocked && (Pressed(aim) || GamepadLeftTriggerPressed);
    // 手榴弾の投げ構え中だけ使用。Busyによる通常武器入力ブロックを受けず、右クリックで解除できる。
    public bool GrenadeCancelPressed => Gameplay && (Down(aim) || GamepadLeftTriggerPressed);
    public bool GrenadeReleasePressed => Gameplay && (Down(attack) || GamepadRightTriggerPressed);
    public bool AttackPressed => !WeaponInputBlocked && (Pressed(attack) || GamepadRightTriggerPressed);
    public bool AttackHeld => !WeaponInputBlocked && Gameplay && (Held(attack) || GamepadRightTriggerHeld);
    // アイテム取得は通常時の「攻撃」キーを文脈に応じて共用する。
    // これにより攻撃キーを変更した場合、取得の長押しキーも同時に変更される。
    public bool PickupPressed => !WeaponInputBlocked && (Pressed(attack) || (Gameplay && GamepadEastPressed));
    public bool PickupHeld => !WeaponInputBlocked && Gameplay && (Held(attack) || GamepadEastHeld);
    public bool PickupAvailable => !WeaponInputBlocked && Gameplay;
    public bool ReloadPressed => !WeaponInputBlocked && (Pressed(reload) || GamepadNorthPressed);
    public bool ChangeViewPressed => Pressed(changeView) || GamepadRightStickPressed;
    public bool ChangeZoomPressed => Pressed(changeZoom);
    public bool ConfirmPressed => !paused && Time.frameCount != transitionFrame && (Down(confirm) || Down(alternateConfirm) || Down(mouseConfirm) || GamepadEastPressed);
    public bool PointerConfirmPressed => !paused && Time.frameCount != transitionFrame && Down(mouseConfirm);
    public bool KeyboardConfirmPressed => !paused && Time.frameCount != transitionFrame && (Down(confirm) || Down(alternateConfirm) || GamepadEastPressed);
    public int DialogueHorizontal => paused || Time.frameCount == transitionFrame ? 0 : (Down(left) || Down(selectLeft) || GamepadDpadLeftPressed ? -1 : 0) + (Down(right) || Down(selectRight) || GamepadDpadRightPressed ? 1 : 0);
    public int DialogueVertical => paused || Time.frameCount == transitionFrame ? 0 : (Down(back) || Down(selectDown) || GamepadDpadDownPressed ? -1 : 0) + (Down(forward) || Down(selectUp) || GamepadDpadUpPressed ? 1 : 0);
    public int DialogueSelection => paused || Time.frameCount == transitionFrame ? 0 : (Down(left) || Down(forward) || Down(selectLeft) || Down(selectUp) || GamepadDpadLeftPressed || GamepadDpadUpPressed ? -1 : 0) + (Down(right) || Down(back) || Down(selectRight) || Down(selectDown) || GamepadDpadRightPressed || GamepadDpadDownPressed ? 1 : 0);
    public bool ItemsPressed => Pressed(items) || (Gameplay && GamepadSelectPressed);
    public bool ItemsClosePressed => Down(items) || GamepadSelectPressed;
    public int UiHorizontal => (Down(selectRight) || GamepadDpadRightPressed ? 1 : 0) - (Down(selectLeft) || GamepadDpadLeftPressed ? 1 : 0);
    public int UiVertical => (Down(selectDown) || GamepadDpadDownPressed ? 1 : 0) - (Down(selectUp) || GamepadDpadUpPressed ? 1 : 0);
    public bool UiConfirmPressed => Down(confirm) || Down(alternateConfirm) || GamepadEastPressed;
    public bool UiConfirmHeld => Input.GetKey(confirm) || Input.GetKey(alternateConfirm) || GamepadEastHeld;
    public bool UiBackPressed => Down(releaseCursor) || GamepadSouthPressed;
    public bool AnyControllerButtonPressed => GamepadAnyButtonPressed;
    public Vector2 InspectionRotate => GamepadLookRaw;
    public float InspectionZoom => GamepadDpadY;
    public bool InspectionResetPressed => GamepadEastPressed;
    public bool InspectionClosePressed => GamepadSouthPressed;
    public float GrenadeDistanceInput => Gameplay ? Input.GetAxis(wheelAxis) + GamepadGrenadeDistance : 0f;
#if ENABLE_INPUT_SYSTEM
    private Gamepad Pad => Gamepad.current;

    private Vector2 ApplyDeadZone(Vector2 value) => value.magnitude <= controllerStickDeadZone ? Vector2.zero : value;
    private Vector2 GamepadMove => Pad == null ? Vector2.zero : ApplyDeadZone(Pad.leftStick.ReadValue());

    private Vector2 GamepadLook
    {
        get
        {
            if (Pad == null)
            {
                return Vector2.zero;
            }

            Vector2 value = ApplyDeadZone(Pad.rightStick.ReadValue()) * CurrentControllerLookSensitivity;
            if (invertControllerLookY)
            {
                value.y = -value.y;
            }

            return value;
        }
    }

    private float CurrentControllerLookSensitivity => SceneManager.GetActiveScene().name.Equals(controllerBaseSceneName, System.StringComparison.OrdinalIgnoreCase) ? baseControllerLookSensitivity : stageControllerLookSensitivity;
    private Vector2 GamepadLookRaw => Pad == null ? Vector2.zero : ApplyDeadZone(Pad.rightStick.ReadValue());
    private float GamepadDpadY => Pad == null ? 0f : Pad.dpad.ReadValue().y;
    private float GamepadGrenadeDistance => Pad == null ? 0f : ApplyDeadZone(Pad.rightStick.ReadValue()).y * controllerGrenadeDistanceSensitivity;
    private bool GamepadLeftStickHeld => Pad != null && Pad.leftStickButton.isPressed;
    private bool GamepadRightStickPressed => Pad != null && Pad.rightStickButton.wasPressedThisFrame;
    private bool GamepadLeftShoulderPressed => Pad != null && Pad.leftShoulder.wasPressedThisFrame;
    private bool GamepadRightShoulderPressed => Pad != null && Pad.rightShoulder.wasPressedThisFrame;
    private bool GamepadLeftTriggerPressed => Pad != null && Pad.leftTrigger.wasPressedThisFrame;
    private bool GamepadRightTriggerPressed => Pad != null && Pad.rightTrigger.wasPressedThisFrame;
    private bool GamepadRightTriggerHeld => Pad != null && Pad.rightTrigger.isPressed;
    private bool GamepadNorthPressed => Pad != null && Pad.buttonNorth.wasPressedThisFrame;
    private bool GamepadSouthPressed => Pad != null && Pad.buttonSouth.wasPressedThisFrame;
    private bool GamepadEastPressed => Pad != null && Pad.buttonEast.wasPressedThisFrame;
    private bool GamepadEastHeld => Pad != null && Pad.buttonEast.isPressed;
    private bool GamepadAnyButtonPressed => Pad != null && (Pad.buttonSouth.wasPressedThisFrame || Pad.buttonEast.wasPressedThisFrame || Pad.buttonWest.wasPressedThisFrame || Pad.buttonNorth.wasPressedThisFrame || Pad.leftShoulder.wasPressedThisFrame || Pad.rightShoulder.wasPressedThisFrame || Pad.leftTrigger.wasPressedThisFrame || Pad.rightTrigger.wasPressedThisFrame || Pad.startButton.wasPressedThisFrame || Pad.selectButton.wasPressedThisFrame || Pad.leftStickButton.wasPressedThisFrame || Pad.rightStickButton.wasPressedThisFrame || Pad.dpad.up.wasPressedThisFrame || Pad.dpad.down.wasPressedThisFrame || Pad.dpad.left.wasPressedThisFrame || Pad.dpad.right.wasPressedThisFrame);
    private bool GamepadStartPressed => Pad != null && Pad.startButton.wasPressedThisFrame;
    private bool GamepadSelectPressed => Pad != null && Pad.selectButton.wasPressedThisFrame;
    private bool GamepadDpadUpPressed => Pad != null && Pad.dpad.up.wasPressedThisFrame;
    private bool GamepadDpadDownPressed => Pad != null && Pad.dpad.down.wasPressedThisFrame;
    private bool GamepadDpadLeftPressed => Pad != null && Pad.dpad.left.wasPressedThisFrame;
    private bool GamepadDpadRightPressed => Pad != null && Pad.dpad.right.wasPressedThisFrame;

#else
    private Vector2 GamepadMove => Vector2.zero;
    private Vector2 GamepadLook => Vector2.zero;
    private Vector2 GamepadLookRaw => Vector2.zero;
    private float GamepadDpadY => 0f;
    private float GamepadGrenadeDistance => 0f;
    private bool GamepadLeftStickHeld => false;
    private bool GamepadRightStickPressed => false;
    private bool GamepadLeftShoulderPressed => false;
    private bool GamepadRightShoulderPressed => false;
    private bool GamepadLeftTriggerPressed => false;
    private bool GamepadRightTriggerPressed => false;
    private bool GamepadRightTriggerHeld => false;
    private bool GamepadNorthPressed => false;
    private bool GamepadSouthPressed => false;
    private bool GamepadEastPressed => false;
    private bool GamepadEastHeld => false;
    private bool GamepadAnyButtonPressed => false;
    private bool GamepadStartPressed => false;
    private bool GamepadSelectPressed => false;
    private bool GamepadDpadUpPressed => false;
    private bool GamepadDpadDownPressed => false;
    private bool GamepadDpadLeftPressed => false;
    private bool GamepadDpadRightPressed => false;

#endif
    public void SetDialogue(Object owner, bool active)
    {
        bool changed = false;
        if (active && !dialogueOwners.Contains(owner))
        {
            dialogueOwners.Add(owner);
            changed = true;
        }

        if (!active)
        {
            changed = dialogueOwners.Remove(owner);
        }

        if (!changed)
        {
            return;
        }

        transitionFrame = Time.frameCount;
        foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKey(key))
            {
                blocked.Add(key);
            }
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (player == null)
        {
            player = FindObjectOfType<UnityChan.UnityChanControlScriptWithRgidBody>();
        }

        dialogueOwners.RemoveAll(owner => owner == null);
        blocked.RemoveWhere(key => !Input.GetKey(key));
        if (!Gameplay)
        {
            return;
        }

        Log(map, "全体マップ");
    }

    private void Log(KeyCode key, string action)
    {
        if (Pressed(key))
        {
            Debug.Log("[GameInput] " + action + "：" + key + "（未実装）");
        }
    }
}
