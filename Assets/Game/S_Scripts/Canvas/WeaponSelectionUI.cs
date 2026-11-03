// 【役割】武器選択パネルの表示、カーソル、確定と直接選択を管理する。
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runs after input sampling and before player aim/attack consumers.
[DefaultExecutionOrder(-900)]
public sealed class WeaponSelectionUI : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public bool registered = true;
        [Tooltip("この枠へ登録したインベントリアイテムID")]
        public string inventoryItemId;
        public string weaponId;
        public string displayName;
        public RectTransform frame;
        public Image weaponImage;
        public Sprite icon;
        public TMP_Text ammoText;
        public string ammoDisplay;
        [Tooltip("残弾総数の参照先。銃以外は未指定にします。")]
        public WeaponAmmoSystem ammoSource;
    }

    [Serializable]
    public class ShortcutWeaponVisual
    {
        public string weaponId;
        [Tooltip("武器選択パネル専用の表示名")]
        public string displayName;
        public Sprite icon;
        public WeaponAmmoSystem ammoSource;
    }

    [Header("既存UI")]
    public GameObject panel;
    public RectTransform cursor;
    public TMP_Text weaponNameText;
    [Tooltip("選択武器名を下方向スロットの中央へ揃えます。")]
    public bool alignNameToBottomSlot = true;
    [Header("上・右・下・左の順")]
    public Slot[] slots = new Slot[4];
    [Header("ショートカット用画像一覧（インベントリ画像とは別）")]
    [Tooltip("武器IDごとに、武器選択UI専用の白色画像と弾数参照を登録します。")]
    public ShortcutWeaponVisual[] availableWeapons;
    [Header("構え・リロード判定")]
    public Animator playerAnimator;
    public string aimParameter = "Setup";
    public OnKeyPress_Throw weaponController;
    [Header("UI効果音（未指定時はポーズメニューと共通）")]
    public AudioClip cursorMoveSe;
    public AudioClip confirmSe;
    [Range(0, 1)]
    public float seVolume = .5f;
    private AudioSource uiAudioSource;
    [Header("残弾の表示色")]
    public Color sufficientAmmoColor = new Color(.7f, 1f, .1f);
    public Color lowAmmoColor = Color.red;
    [Tooltip("handgunスロットの参照が空なら現在の銃の弾数管理を使用します。")]
    public bool autoConnectHandgunAmmo = true;
    [Header("選択状態（実際の装備は変更しません）")]
    [SerializeField]
    private int confirmedSlot = 1;
    public int ConfirmedSlot => confirmedSlot;
    public string SelectedWeaponId => Valid(confirmedSlot) && slots[confirmedSlot].registered ? slots[confirmedSlot].weaponId : "";

    public PlayerFlashlightController flashlight;
    [Header("手榴弾の所持数表示")]
    public PlayerInventory inventory;
    public string grenadeItemId = "m1916";
    public Color grenadeAvailableColor = new Color(.7f, 1f, .1f, 1f);
    public Color grenadeEmptyColor = Color.red;
    [Header("暗視ゴーグルの稼働時間表示")]
    public Color gogglesBatteryColor = new Color(.7f, 1f, .1f, 1f);
    public Color gogglesBatteryLowColor = Color.red;
    [Min(0f)]
    public float gogglesLowTimeSeconds = 10f;
    public bool IsOpen { get; private set; }

    private int selectedSlot;
    private int openedFrame;
    private Vector3 cursorOffset;
    private float savedTimeScale = 1f;
    private bool savedAudioPause;
    private bool ownsGamePause;
    private const string ShortcutPrefPrefix = "DeadShot.WeaponShortcut.v1.";
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        // A dedicated source on the system object keeps confirmation audible after the panel closes.
        uiAudioSource = gameObject.AddComponent<AudioSource>();
        uiAudioSource.playOnAwake = false;
        uiAudioSource.spatialBlend = 0;
        uiAudioSource.ignoreListenerPause = true;
        if (cursor != null && Valid(confirmedSlot) && slots[confirmedSlot].frame != null)
        {
            cursorOffset = cursor.position - slots[confirmedSlot].frame.position;
        }

        if (panel != null)
        {
            panel.SetActive(false);
        }

        ConfigureWeaponNameText();
        EnsureAvailableWeaponCatalog();
        RestoreInventoryShortcuts();
        ApplyDefaultGrenadeShortcut();
        if (inventory == null)
        {
            inventory = FindObjectOfType<PlayerInventory>(true);
        }

        RefreshSlots();
    }

    private void ApplyDefaultGrenadeShortcut()
    {
        const int leftDirection = 3;
        if (!Valid(leftDirection) || PlayerPrefs.HasKey(ShortcutPrefPrefix + leftDirection))
        {
            return;
        }

        ShortcutWeaponVisual visual = FindVisual("grenade");
        Slot slot = slots[leftDirection];
        slot.registered = true;
        slot.inventoryItemId = "m1916";
        slot.weaponId = "grenade";
        slot.displayName = visual != null && !string.IsNullOrWhiteSpace(visual.displayName) ? visual.displayName : "手榴弾";
        slot.icon = visual != null ? visual.icon : slot.icon;
        slot.ammoSource = null;
    }

    bool Valid(int index) => slots != null && index >= 0 && index < slots.Length && slots[index] != null;
    public bool CanSelect => GameInputManager.Instance != null && GameInputManager.Instance.Gameplay && playerAnimator != null && weaponController != null && !weaponController.isReloading && !playerAnimator.GetBool(aimParameter) && (flashlight == null || !flashlight.IsRaised) && !IsSmgPuttingAway();

    bool IsSmgPuttingAway()
    {
        SubmachineGunController smg = FindObjectOfType<SubmachineGunController>(true);
        return smg != null && smg.IsPuttingAway;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        var input = GameInputManager.Instance;
        if (input == null)
        {
            return;
        }

        // 開く前だけ構え・リロード状態を検査する。開いた後は確定するまで閉じない。
        if (!IsOpen && (!CanSelect || input.PausePressed))
        {
            return;
        }

        int quickDirection = input.WeaponQuickDirection;
        bool menuPressed = input.WeaponMenuPressed;
        if (!IsOpen)
        {
            if (quickDirection >= 0)
            {
                OpenSelection(input);
                selectedSlot = quickDirection;
                RefreshSlots();
                UpdateSelection();
                Confirm();
                return;
            }

            if (!menuPressed)
            {
                return;
            }

            if (panel == null || slots == null || slots.Length != 4)
            {
                return;
            }

            OpenSelection(input);
            return;
        }

        if (Time.frameCount != openedFrame && menuPressed)
        {
            Close();
            return;
        }

        if (quickDirection >= 0)
        {
            selectedSlot = quickDirection;
            RefreshSlots();
            UpdateSelection();
            Confirm();
            return;
        }

        int direction = input.WeaponDirection;
        float wheel = input.WeaponScroll;
        int previousSlot = selectedSlot;
        if (direction >= 0)
        {
            selectedSlot = direction;
        }
        else if (Mathf.Abs(wheel) > .01f)
        {
            selectedSlot = (selectedSlot + (wheel > 0 ? 1 : 3)) % 4;
        }

        if (selectedSlot != previousSlot)
        {
            PlayUiSound(false);
        }

        RefreshSlots();
        UpdateSelection();
        if (Time.frameCount != openedFrame && input.WeaponConfirmPressed)
        {
            Confirm();
        }
    }

    void OpenSelection(GameInputManager input)
    {
        if (panel == null || slots == null || slots.Length != 4)
        {
            return;
        }

        IsOpen = true;
        openedFrame = Time.frameCount;
        selectedSlot = Mathf.Clamp(confirmedSlot, 0, 3);
        input.SetWeaponSelection(this, true);
        BeginSelectionPause();
        panel.SetActive(true);
        RefreshSlots();
        UpdateSelection();
    }

    public void RefreshSlots()
    {
        if (slots == null)
        {
            return;
        }

        foreach (var slot in slots)
        {
            if (slot == null)
            {
                continue;
            }

            if (slot.ammoSource == null && autoConnectHandgunAmmo && slot.weaponId == "handgun" && weaponController != null)
            {
                slot.ammoSource = weaponController.ammoSystem;
            }

            bool showAmmo = slot.registered && slot.ammoSource != null;
            bool showGrenadeCount = slot.registered && slot.weaponId == "grenade";
            bool showGogglesTime = slot.registered && slot.weaponId == "goggles";
            slot.ammoDisplay = showAmmo ? $"{slot.ammoSource.TotalOwnedAmmo} / {slot.ammoSource.MagazineCapacity}" : "";
            if (slot.weaponImage != null)
            {
                slot.weaponImage.gameObject.SetActive(slot.registered && slot.icon != null);
                slot.weaponImage.sprite = slot.icon;
            }

            if (slot.ammoText != null)
            {
                slot.ammoText.gameObject.SetActive(slot.registered);
                slot.ammoText.richText = true;
                slot.ammoText.overrideColorTags = false;
                slot.ammoText.color = Color.white;
                if (showGrenadeCount)
                {
                    if (inventory == null)
                    {
                        inventory = FindObjectOfType<PlayerInventory>(true);
                    }

                    int current = inventory != null ? inventory.Count(grenadeItemId) : 0;
                    int maximum = inventory != null ? inventory.MaxCount(grenadeItemId) : 30;
                    Color remainingColor = current <= 0 ? grenadeEmptyColor : grenadeAvailableColor;
                    string hex = ColorUtility.ToHtmlStringRGBA(remainingColor);
                    slot.ammoDisplay = $"{current} / {maximum}";
                    slot.ammoText.text = $"<color=#{hex}>{current}</color><color=#FFFFFFFF> / {maximum}</color>";
                }
                else if (showGogglesTime)
                {
                    SpecialGogglesController goggles = SpecialGogglesController.Instance ?? FindObjectOfType<SpecialGogglesController>(true);
                    float seconds = goggles != null ? goggles.TotalAvailableSeconds : 0f;
                    Color timeColor = seconds < gogglesLowTimeSeconds ? gogglesBatteryLowColor : gogglesBatteryColor;
                    string hex = ColorUtility.ToHtmlStringRGBA(timeColor);
                    slot.ammoDisplay = SpecialGogglesController.FormatSelectionTime(seconds);
                    slot.ammoText.text = $"<color=#{hex}>{slot.ammoDisplay}</color>";
                }
                else if (showAmmo)
                {
                    Color remainingColor = slot.ammoSource.TotalOwnedAmmo >= slot.ammoSource.MagazineCapacity ? sufficientAmmoColor : lowAmmoColor;
                    string hex = ColorUtility.ToHtmlStringRGBA(remainingColor);
                    slot.ammoText.text = $"<color=#{hex}>{slot.ammoSource.TotalOwnedAmmo}</color><color=#FFFFFFFF> / {slot.ammoSource.MagazineCapacity}</color>";
                }
                else
                {
                    slot.ammoText.text = "";
                }
            }
        }
    }

    // After WeaponAmmoSystem.Update so shots and pickups are reflected in the same frame.
    void LateUpdate()
    {
        RefreshSlots();
    }

    void UpdateSelection()
    {
        if (!Valid(selectedSlot))
        {
            return;
        }

        var slot = slots[selectedSlot];
        if (cursor != null && slot.frame != null)
        {
            cursor.position = slot.frame.position + cursorOffset;
        }

        if (weaponNameText != null)
        {
            weaponNameText.text = slot.registered ? slot.displayName : "EMPTY / 未登録";
        }
    }

    public void Confirm()
    {
        if (!IsOpen || !CanSelect || !Valid(selectedSlot))
        {
            return;
        }

        var slot = slots[selectedSlot];
        if (!slot.registered)
        {
            Debug.Log("[WeaponSelection] 未登録のため選択できません。", this);
            return;
        }

        PlayUiSound(true);
        int nextSlot = selectedSlot;
        string previousWeaponId = SelectedWeaponId;
        Debug.Log("[WeaponSelection] 選択確定：" + slot.displayName + " (" + slot.weaponId + ")", this);
        Close();
        if (previousWeaponId == "smg" && slot.weaponId != "smg")
        {
            SubmachineGunController smg = FindObjectOfType<SubmachineGunController>(true);
            if (smg != null && smg.PutAwayThenSwitch(() => CommitSelection(nextSlot)))
            {
                return;
            }
        }

        CommitSelection(nextSlot);
    }

    void CommitSelection(int slotIndex)
    {
        if (!Valid(slotIndex) || !slots[slotIndex].registered)
        {
            return;
        }

        confirmedSlot = slotIndex;
        RefreshSlots();
    }

    public bool RegisterInventoryWeapon(PlayerInventory.Definition definition, int direction)
    {
        if (definition == null || !definition.CanRegisterShortcut || !Valid(direction))
        {
            return false;
        }

        string weaponId = definition.ResolvedShortcutWeaponId;
        ShortcutWeaponVisual visual = FindVisual(weaponId);
        WeaponAmmoSystem source = visual != null && visual.ammoSource != null ? visual.ammoSource : FindAmmoSource(weaponId);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!Valid(i) || i == direction)
            {
                continue;
            }

            Slot previous = slots[i];
            if (previous.inventoryItemId == definition.id || previous.weaponId == weaponId)
            {
                previous.registered = false;
                previous.inventoryItemId = "";
                PlayerPrefs.SetString(ShortcutPrefPrefix + i, "");
            }
        }

        Slot slot = slots[direction];
        slot.registered = true;
        slot.inventoryItemId = definition.id;
        slot.weaponId = weaponId;
        slot.displayName = visual != null && !string.IsNullOrWhiteSpace(visual.displayName) ? visual.displayName : definition.displayName;
        slot.icon = visual != null ? visual.icon : null;
        slot.ammoSource = source;
        confirmedSlot = direction;
        PlayerPrefs.SetString(ShortcutPrefPrefix + direction, definition.id);
        PlayerPrefs.Save();
        RefreshSlots();
        Debug.Log($"[WeaponShortcut] {definition.displayName} を {DirectionLabel(direction)} に登録しました。", this);
        return true;
    }

    public string RegisteredDirections(PlayerInventory.Definition definition)
    {
        if (definition == null || !definition.CanRegisterShortcut || slots == null)
        {
            return "";
        }

        string result = "";
        for (int i = 0; i < slots.Length; i++)
        {
            if (Valid(i) && slots[i].registered && (slots[i].inventoryItemId == definition.id || slots[i].weaponId == definition.ResolvedShortcutWeaponId))
            {
                result += (result.Length == 0 ? "" : "  ") + DirectionLabel(i);
            }
        }

        return result;
    }

    private WeaponAmmoSystem FindAmmoSource(string weaponId)
    {
        if (slots != null)
        {
            foreach (Slot slot in slots)
            {
                if (slot != null && slot.weaponId == weaponId && slot.ammoSource != null)
                {
                    return slot.ammoSource;
                }
            }
        }

        if (weaponId == "handgun" && weaponController != null)
        {
            return weaponController.ammoSystem;
        }

        SubmachineGunController smg = FindObjectOfType<SubmachineGunController>(true);
        return weaponId == "smg" && smg != null ? smg.AmmoSystem : null;
    }

    private ShortcutWeaponVisual FindVisual(string weaponId)
    {
        if (availableWeapons == null)
        {
            return null;
        }

        return Array.Find(availableWeapons, item => item != null && item.weaponId == weaponId);
    }

    public Sprite GetHudIcon(string weaponId)
    {
        ShortcutWeaponVisual visual = FindVisual(weaponId);
        return visual != null ? visual.icon : null;
    }

    private void EnsureAvailableWeaponCatalog()
    {
        if (availableWeapons != null && availableWeapons.Length > 0)
        {
            return;
        }

        if (slots == null)
        {
            availableWeapons = new ShortcutWeaponVisual[0];
            return;
        }

        availableWeapons = new ShortcutWeaponVisual[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            availableWeapons[i] = slot == null ? null : new ShortcutWeaponVisual
            {
                weaponId = slot.weaponId,
                displayName = slot.displayName,
                icon = slot.icon,
                ammoSource = slot.ammoSource
            };
        }
    }

    private void ConfigureWeaponNameText()
    {
        if (weaponNameText == null)
        {
            return;
        }

        RectTransform rect = weaponNameText.rectTransform;
        if (rect.sizeDelta.x < 600f)
        {
            rect.sizeDelta = new Vector2(600f, Mathf.Max(50f, rect.sizeDelta.y));
        }

        if (alignNameToBottomSlot && Valid(2) && slots[2].frame != null)
        {
            Vector3 bottomCenter = slots[2].frame.TransformPoint(slots[2].frame.rect.center);
            Vector3 localCenter = rect.parent.InverseTransformPoint(bottomCenter);
            rect.anchoredPosition = new Vector2(localCenter.x, rect.anchoredPosition.y);
        }

        weaponNameText.alignment = TextAlignmentOptions.Center;
        weaponNameText.enableAutoSizing = true;
        weaponNameText.fontSizeMin = 16f;
        weaponNameText.fontSizeMax = 30f;
        weaponNameText.enableWordWrapping = false;
        weaponNameText.overflowMode = TextOverflowModes.Overflow;
    }

    private void RestoreInventoryShortcuts()
    {
        PlayerInventory inventory = FindObjectOfType<PlayerInventory>(true);
        if (inventory == null || slots == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            string key = ShortcutPrefPrefix + i;
            if (!PlayerPrefs.HasKey(key))
            {
                continue;
            }

            string itemId = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(itemId))
            {
                if (Valid(i))
                {
                    slots[i].registered = false;
                    slots[i].inventoryItemId = "";
                }

                continue;
            }

            PlayerInventory.Definition definition = inventory.Find(itemId);
            if (definition == null || !definition.CanRegisterShortcut)
            {
                continue;
            }

            Slot slot = slots[i];
            ShortcutWeaponVisual visual = FindVisual(definition.ResolvedShortcutWeaponId);
            WeaponAmmoSystem source = visual != null && visual.ammoSource != null ? visual.ammoSource : FindAmmoSource(definition.ResolvedShortcutWeaponId);
            slot.registered = true;
            slot.inventoryItemId = itemId;
            slot.weaponId = definition.ResolvedShortcutWeaponId;
            slot.displayName = visual != null && !string.IsNullOrWhiteSpace(visual.displayName) ? visual.displayName : definition.displayName;
            slot.icon = visual != null ? visual.icon : null;
            slot.ammoSource = source;
        }
    }

    public static string DirectionLabel(int direction)
    {
        switch (direction)
        {
            case 0:
                return "↑ 上";
            case 1:
                return "→ 右";
            case 2:
                return "↓ 下";
            case 3:
                return "← 左";
            default:
                return "未登録";
        }
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.SetWeaponSelection(this, false);
        }

        if (panel != null)
        {
            panel.SetActive(false);
        }

        EndSelectionPause();
    }

    void BeginSelectionPause()
    {
        if (ownsGamePause)
        {
            return;
        }

        savedTimeScale = Time.timeScale;
        savedAudioPause = AudioListener.pause;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        ownsGamePause = true;
    }

    void EndSelectionPause()
    {
        if (!ownsGamePause)
        {
            return;
        }

        Time.timeScale = savedTimeScale;
        AudioListener.pause = savedAudioPause;
        ownsGamePause = false;
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        Close();
    }

    void PlayUiSound(bool confirm)
    {
        AudioClip clip = confirm ? confirmSe : cursorMoveSe;
        if (clip == null)
        {
            var pause = FindObjectOfType<PauseMenuController>(true);
            if (pause != null)
            {
                clip = confirm ? pause.selectionConfirmSe : pause.selectionMoveSe;
            }
        }

        if (clip != null && uiAudioSource != null)
        {
            uiAudioSource.PlayOneShot(clip, seVolume);
        }
    }
}
