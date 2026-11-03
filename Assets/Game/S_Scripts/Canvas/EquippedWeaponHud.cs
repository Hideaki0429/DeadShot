// 【役割】選択・装備中の武器画像と弾数やバッテリー時間をHUDへ反映する。
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 既存の「銃UI」を、現在構えている装備の共通HUDとして再利用する。
[DefaultExecutionOrder(500)]
public sealed class EquippedWeaponHud : MonoBehaviour
{
    [Serializable]
    public sealed class EquipmentImageSetting
    {
        public string weaponId;
        [Tooltip("0以下の軸は、Hierarchyにある拳銃画像の元サイズを使用します。")]
        public Vector2 imageSize;
    }

    [Header("既存の銃UI参照（空欄なら子オブジェクト名から自動取得）")]
    [SerializeField]
    CanvasGroup canvasGroup;
    [SerializeField]
    Image equipmentImage;
    [SerializeField]
    TMP_Text magazineLabel;
    [SerializeField]
    TMP_Text magazineCount;
    [Header("連携")]
    [SerializeField]
    WeaponSelectionUI weaponSelection;
    [SerializeField]
    PlayerFlashlightController flashlight;
    [SerializeField]
    PlayerGrenadeAnimation grenade;
    [SerializeField]
    PlayerInventory inventory;
    [SerializeField]
    Animator playerAnimator;
    [SerializeField]
    string grenadeItemId = "m1916";
    [Header("手榴弾の個数表記")]
    [Tooltip("既存の『マガジン』テキストへ表示する内容です。")]
    [SerializeField]
    string grenadeCountLabel = "G：";
    [Header("ハンドガン弾薬表記")]
    [SerializeField]
    string handgunAmmoLabel = "AMMO：";
    [Header("暗視ゴーグルの稼働時間表記")]
    [SerializeField]
    string gogglesBatteryLabel = "BATTERY：";
    [SerializeField]
    Color gogglesBatteryColor = new Color(.7f, 1f, .1f, 1f);
    [SerializeField]
    Color gogglesBatteryLowColor = Color.red;
    [Min(0f), SerializeField]
    float gogglesLowTimeSeconds = 10f;
    [Header("装備ごとのHUD画像サイズ")]
    [SerializeField]
    List<EquipmentImageSetting> imageSettings = new List<EquipmentImageSetting>();
    WeaponAmmoSystem ammo;
    readonly List<GameObject> handgunOnlyObjects = new List<GameObject>();
    string originalMagazineLabel = "MAGS :";
    Sprite originalHandgunSprite;
    Vector2 originalImageSize;
    Color originalMagazineCountColor = Color.white;
    bool resolved;
    public void Initialize(WeaponAmmoSystem source)
    {
        ammo = source;
        Resolve();
        Refresh(true);
    }

    public void PrepareInspector()
    {
        Resolve();
        EnsureImageSettings();
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        Resolve();
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        Resolve();
        Refresh(false);
    }

    void Resolve()
    {
        if (resolved)
        {
            return;
        }

        if (ammo == null)
        {
            ammo = GetComponent<WeaponAmmoSystem>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (weaponSelection == null)
        {
            weaponSelection = FindObjectOfType<WeaponSelectionUI>(true);
        }

        if (flashlight == null)
        {
            flashlight = FindObjectOfType<PlayerFlashlightController>(true);
        }

        if (grenade == null)
        {
            grenade = FindObjectOfType<PlayerGrenadeAnimation>(true);
        }

        if (inventory == null)
        {
            inventory = FindObjectOfType<PlayerInventory>(true);
        }

        if (playerAnimator == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerAnimator = player.GetComponentInParent<Animator>();
            }
        }

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "拳銃")
            {
                equipmentImage = child.GetComponent<Image>();
            }
            else if (child.name == "マガジン")
            {
                magazineLabel = child.GetComponent<TMP_Text>();
            }
            else if (child.name == "マガジン数")
            {
                magazineCount = child.GetComponent<TMP_Text>();
            }
            else if (child.name == "装填数")
            {
                if (!handgunOnlyObjects.Contains(child.gameObject))
                {
                    handgunOnlyObjects.Add(child.gameObject);
                }
            }
            else if (child.name == "1マガジンの最大数")
            {
                if (!handgunOnlyObjects.Contains(child.gameObject))
                {
                    handgunOnlyObjects.Add(child.gameObject);
                }
            }
            else if (child.name == "全ての球数")
            {
                if (!handgunOnlyObjects.Contains(child.gameObject))
                {
                    handgunOnlyObjects.Add(child.gameObject);
                }
            }
            else if (child.name == "予備最大数" || child.name == "残数" || child.name == "／")
            {
                if (!handgunOnlyObjects.Contains(child.gameObject))
                {
                    handgunOnlyObjects.Add(child.gameObject);
                }
            }
        }

        if (magazineLabel != null)
        {
            originalMagazineLabel = magazineLabel.text;
        }

        if (magazineCount != null)
        {
            originalMagazineCountColor = magazineCount.color;
        }

        if (equipmentImage != null)
        {
            originalHandgunSprite = equipmentImage.sprite;
            originalImageSize = equipmentImage.rectTransform.sizeDelta;
        }

        EnsureImageSettings();
        resolved = canvasGroup != null && equipmentImage != null;
    }

    void Refresh(bool force)
    {
        if (!resolved || weaponSelection == null)
        {
            return;
        }

        string id = weaponSelection.SelectedWeaponId;
        bool raised = IsRaised(id);
        SetGroupVisible(raised);
        if (!raised)
        {
            equipmentImage.gameObject.SetActive(false);
            SetAmmoDetails(false, null);
            if (magazineLabel != null)
            {
                magazineLabel.gameObject.SetActive(false);
            }

            if (magazineCount != null)
            {
                magazineCount.gameObject.SetActive(false);
            }

            return;
        }

        // 装備HUDでは武器選択用の白画像ではなく、インベントリ登録画像を使用する。
        Sprite icon = FindInventoryIcon(id);
        if (icon == null && id == "handgun")
        {
            icon = originalHandgunSprite;
        }

        equipmentImage.sprite = icon;
        equipmentImage.preserveAspect = true;
        EquipmentImageSetting imageSetting = imageSettings.Find(value => value != null && value.weaponId == id);
        Vector2 requested = imageSetting != null ? imageSetting.imageSize : Vector2.zero;
        equipmentImage.rectTransform.sizeDelta = new Vector2(requested.x > 0f ? requested.x : originalImageSize.x, requested.y > 0f ? requested.y : originalImageSize.y);
        equipmentImage.gameObject.SetActive(icon != null);
        bool handgun = id == "handgun";
        bool smg = id == "smg";
        bool firearm = handgun || smg;
        bool isGrenade = id == "grenade";
        bool isGoggles = id == "goggles";
        WeaponAmmoSystem activeAmmo = firearm ? ResolveAmmoFor(id) : null;
        SetAmmoDetails(firearm, activeAmmo);
        if (magazineLabel != null)
        {
            magazineLabel.gameObject.SetActive(firearm || isGrenade || isGoggles);
            magazineLabel.text = isGrenade ? grenadeCountLabel : isGoggles ? gogglesBatteryLabel : handgunAmmoLabel;
        }

        if (magazineCount != null)
        {
            magazineCount.gameObject.SetActive(firearm || isGrenade || isGoggles);
            if (isGrenade)
            {
                int current = inventory != null ? inventory.Count(grenadeItemId) : 0;
                magazineCount.color = originalMagazineCountColor;
                magazineCount.text = current.ToString();
            }
            else if (isGoggles)
            {
                SpecialGogglesController goggles = SpecialGogglesController.Instance ?? FindObjectOfType<SpecialGogglesController>(true);
                float seconds = goggles != null ? goggles.TotalAvailableSeconds : 0f;
                magazineCount.color = seconds < gogglesLowTimeSeconds ? gogglesBatteryLowColor : gogglesBatteryColor;
                magazineCount.text = SpecialGogglesController.FormatEquippedTime(seconds);
            }
            else
            {
                // ゴーグル／手榴弾が既存テキストを書き換えるため、
                // 銃へ戻したフレームで、選択中の銃専用弾数を明示的に復元する。
                magazineCount.color = originalMagazineCountColor;
                if (firearm && activeAmmo != null)
                {
                    magazineCount.text = activeAmmo.TotalOwnedAmmo.ToString();
                }
            }
        }
    }

    WeaponAmmoSystem ResolveAmmoFor(string weaponId)
    {
        if (weaponId == "smg")
        {
            SubmachineGunController smg = FindObjectOfType<SubmachineGunController>(true);
            if (smg != null && smg.AmmoSystem != null)
            {
                return smg.AmmoSystem;
            }

            foreach (WeaponAmmoSystem source in FindObjectsOfType<WeaponAmmoSystem>(true))
            {
                if (source != null && source.WeaponId == "smg")
                {
                    return source;
                }
            }

            return null;
        }

        return ammo;
    }

    void EnsureImageSettings()
    {
        string[] ids =
        {
            "handgun",
            "smg",
            "flashlight",
            "knife",
            "grenade",
            "goggles"
        };
        foreach (string id in ids)
        {
            if (!imageSettings.Exists(value => value != null && value.weaponId == id))
            {
                imageSettings.Add(new EquipmentImageSetting { weaponId = id, imageSize = Vector2.zero });
            }
        }
    }

    bool IsRaised(string id)
    {
        if (id == "flashlight")
        {
            return flashlight != null && flashlight.IsRaised;
        }

        if (id == "grenade")
        {
            return grenade != null && grenade.IsThrowing;
        }

        if (id == "goggles")
        {
            return SpecialGogglesController.IsEquippedGlobally;
        }

        return (id == "handgun" || id == "smg" || id == "knife") && playerAnimator != null && playerAnimator.GetBool("Setup");
    }

    Sprite FindInventoryIcon(string weaponId)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(weaponId))
        {
            return null;
        }

        foreach (PlayerInventory.Definition definition in inventory.catalog)
        {
            if (definition != null && definition.ResolvedShortcutWeaponId == weaponId)
            {
                return definition.icon;
            }
        }

        return null;
    }

    void SetAmmoDetails(bool visible, WeaponAmmoSystem activeAmmo)
    {
        if (ammo != null)
        {
            ammo.SetDetailedHudVisible(visible && activeAmmo == ammo);
        }

        if (activeAmmo != null && activeAmmo != ammo)
        {
            activeAmmo.SetDetailedHudVisible(visible);
        }

        // SMG側に専用アイコンが未登録の場合も、既存HUDのアイコン列を
        // SMGの現在装填数で再利用して表示する。
        if (visible && activeAmmo != null && activeAmmo != ammo && activeAmmo.BulletIconCount == 0 && ammo != null)
        {
            ammo.SetBulletIconsVisible(true, activeAmmo.CurrentAmmoInMagazine);
        }

        foreach (GameObject target in handgunOnlyObjects)
        {
            if (target != null)
            {
                target.SetActive(visible);
            }
        }
    }

    void SetGroupVisible(bool visible)
    {
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
