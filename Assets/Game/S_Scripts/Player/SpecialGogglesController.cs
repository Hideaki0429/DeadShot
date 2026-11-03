// 【役割】暗視ゴーグルの装備、視覚効果とバッテリー使用時間を管理する。
using System;
using UnityEngine;

/// <summary>特殊ゴーグルの装備状態と頭部モデルの表示を一元管理する。</summary>
public sealed class SpecialGogglesController : MonoBehaviour
{
    public static SpecialGogglesController Instance { get; private set; }
    public static bool IsEquippedGlobally => Instance != null && Instance.IsEquipped;

    public static event Action<bool> EquipmentChanged;
    [Header("装備モデル")]
    [SerializeField]
    private GameObject militaryCap;
    [SerializeField]
    private GameObject specialForcesHelmet;
    [SerializeField]
    private string militaryCapObjectName = "SK_Military_Cap1";
    [SerializeField]
    private string helmetObjectName = "AE_Special_Forces_Helmet";
    [Header("開始設定")]
    [SerializeField]
    private bool equippedOnStart;
    [Header("ショートカット操作")]
    [SerializeField]
    private WeaponSelectionUI weaponSelection;
    [SerializeField]
    private string shortcutWeaponId = "goggles";
    [Header("バッテリー")]
    [SerializeField]
    private PlayerInventory inventory;
    [Tooltip("battery smallのインベントリアイテムID")]
    [SerializeField]
    private string smallBatteryItemId = "m1919";
    [Tooltip("battery bigのインベントリアイテムID")]
    [SerializeField]
    private string bigBatteryItemId = "m1920";
    [Min(1f), SerializeField]
    private float smallBatterySeconds = 60f;
    [Min(1f), SerializeField]
    private float bigBatterySeconds = 300f;
    [SerializeField, Min(0f)]
    private float activeBatterySeconds;
    public bool IsEquipped { get; private set; }
    public float RemainingBatterySeconds => Mathf.Max(0f, activeBatterySeconds);
    public float TotalAvailableSeconds => RemainingBatterySeconds + (inventory != null ? inventory.Count(smallBatteryItemId) * smallBatterySeconds + inventory.Count(bigBatteryItemId) * bigBatterySeconds : 0f);
    public bool HasUsableBattery => TotalAvailableSeconds > 0.01f;

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveModels();
        ResolveInventory();
        SetEquipped(equippedOnStart, false);
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        ResolveInventory();
        UpdateBattery();
        if (weaponSelection == null)
        {
            weaponSelection = FindObjectOfType<WeaponSelectionUI>(true);
        }

        GameInputManager input = GameInputManager.Instance;
        if (weaponSelection == null || input == null || !input.Gameplay || weaponSelection.SelectedWeaponId != shortcutWeaponId || !input.AimPressed)
        {
            return;
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null && (animator.GetCurrentAnimatorStateInfo(0).IsTag("PlayerHit") || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("PlayerHit"))))
        {
            return;
        }

        ToggleEquipped();
    }

    public bool IsShortcutSelected
    {
        get
        {
            if (weaponSelection == null)
            {
                weaponSelection = FindObjectOfType<WeaponSelectionUI>(true);
            }

            return weaponSelection != null && weaponSelection.SelectedWeaponId == shortcutWeaponId;
        }
    }

    // オブジェクトが破棄されるときの終了処理です。
    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        Instance = null;
        EquipmentChanged?.Invoke(false);
    }

    public void ToggleEquipped() => SetEquipped(!IsEquipped);
    public void SetEquipped(bool equipped, bool notify = true)
    {
        ResolveModels();
        ResolveInventory();
        if (equipped && !EnsureActiveBattery())
        {
            IsEquipped = false;
            if (militaryCap != null)
            {
                militaryCap.SetActive(true);
            }

            if (specialForcesHelmet != null)
            {
                specialForcesHelmet.SetActive(false);
            }

            Debug.Log("[特殊ゴーグル] 使用可能なバッテリーがないため装備できません。", this);
            return;
        }

        IsEquipped = equipped;
        if (militaryCap != null)
        {
            militaryCap.SetActive(!equipped);
        }

        if (specialForcesHelmet != null)
        {
            specialForcesHelmet.SetActive(equipped);
        }

        if (notify)
        {
            EquipmentChanged?.Invoke(equipped);
        }

        Debug.Log(equipped ? "[特殊ゴーグル] 装備しました。敵の弱点を可視化します。" : "[特殊ゴーグル] 外しました。", this);
    }

    private void UpdateBattery()
    {
        if (!IsEquipped)
        {
            return;
        }

        activeBatterySeconds = Mathf.Max(0f, activeBatterySeconds - Time.deltaTime);
        if (activeBatterySeconds > 0f)
        {
            return;
        }

        if (EnsureActiveBattery())
        {
            return;
        }

        SetEquipped(false);
        Debug.Log("[特殊ゴーグル] バッテリーを使い切ったため装備を解除しました。", this);
    }

    private bool EnsureActiveBattery()
    {
        if (activeBatterySeconds > 0.01f)
        {
            return true;
        }

        if (inventory == null)
        {
            return false;
        }

        // 短時間の電池から使用し、大容量電池を温存する。
        if (inventory.RemoveItem(smallBatteryItemId, 1))
        {
            activeBatterySeconds = smallBatterySeconds;
            return true;
        }

        if (inventory.RemoveItem(bigBatteryItemId, 1))
        {
            activeBatterySeconds = bigBatterySeconds;
            return true;
        }

        activeBatterySeconds = 0f;
        return false;
    }

    private void ResolveInventory()
    {
        if (inventory == null)
        {
            inventory = FindObjectOfType<PlayerInventory>(true);
        }
    }

    public static string FormatSelectionTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{total / 60}:{total % 60:00}";
    }

    // 装備HUDは指定どおり、ちょうど1分なら 0:60 と表示する。
    public static string FormatEquippedTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        if (total > 0 && total % 60 == 0)
        {
            return $"{total / 60 - 1}:60";
        }

        return $"{total / 60}:{total % 60:00}";
    }

    private void ResolveModels()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (militaryCap == null && child.name == militaryCapObjectName)
            {
                militaryCap = child.gameObject;
            }

            if (specialForcesHelmet == null && child.name == helmetObjectName)
            {
                specialForcesHelmet = child.gameObject;
            }
        }
    }

    public static SpecialGogglesController FindOrCreateOnPlayer()
    {
        if (Instance != null)
        {
            return Instance;
        }

        SpecialGogglesController existing = FindObjectOfType<SpecialGogglesController>(true);
        if (existing != null)
        {
            return existing;
        }

        GameObject player = null;
        try
        {
            GameObject[] candidates = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject candidate in candidates)
            {
                Transform current = candidate.transform;
                while (current != null)
                {
                    bool hasCap = false;
                    bool hasHelmet = false;
                    foreach (Transform child in current.GetComponentsInChildren<Transform>(true))
                    {
                        hasCap |= child.name == "SK_Military_Cap1";
                        hasHelmet |= child.name == "AE_Special_Forces_Helmet";
                    }

                    if (hasCap && hasHelmet)
                    {
                        player = current.gameObject;
                        break;
                    }

                    current = current.parent;
                }

                if (player != null)
                {
                    break;
                }
            }

            if (player == null && candidates.Length > 0)
            {
                player = candidates[0];
            }
        }
        catch (UnityException)
        {
        }

        if (player == null)
        {
            return null;
        }

        return player.GetComponent<SpecialGogglesController>() ?? player.AddComponent<SpecialGogglesController>();
    }
}
