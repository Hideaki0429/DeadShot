// 【役割】武器ごとの装弾数、残弾、発射可否とリロードを管理する。
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Serialization;

public class WeaponAmmoSystem : MonoBehaviour
{
    [Header("--- Weapon Identity ---")]
    [Tooltip("この弾数設定を使用する武器ID。handgunとsmgを別々に管理します。")]
    [SerializeField]
    private string weaponId = "handgun";
    public string WeaponId => weaponId;

    [Header("--- UI References ---")]
    [SerializeField]
    private TextMeshProUGUI txtMagCount;
    [SerializeField]
    private TextMeshProUGUI txtMaxSpare;
    [SerializeField]
    private TextMeshProUGUI txtCurrentAmmo;
    [SerializeField]
    private TextMeshProUGUI txtTotalOwned;
    [Header("--- Bullet Icons (15個をInspectorで直接登録) ---")]
    [SerializeField]
    private List<GameObject> bulletIcons = new List<GameObject>();
    [Header("--- 連携設定 ---")]
    [SerializeField]
    private OnKeyPress_Throw gunScript;
    [Header("--- Ammo Settings ---")]
    [Tooltip("1本のマガジンに入る最大弾数")]
    [SerializeField, Min(1)]
    private int maxAmmoInMagazine = 15;
    [Tooltip("所持できる予備弾薬の上限")]
    [SerializeField, Min(0)]
    private int maxSpareAmmo = 120;
    [Tooltip("ゲーム開始時に所持している予備マガジン数")]
    [SerializeField, Min(0)]
    private int currentMagCount = 1;
    [Tooltip("ゲーム開始時に銃へ装填されている弾数")]
    [SerializeField, Min(0)]
    private int currentAmmoInMag = 15;
    [Tooltip("ゲーム開始時の総所持弾数。装填中の弾も含む")]
    [SerializeField, Min(0)]
    private int totalOwnedAmmo = 30;
    public int TotalOwnedAmmo => inventory != null ? inventory.Count(ammoItemId) : Mathf.Max(0, totalOwnedAmmo);
    public int CurrentAmmoInMagazine => gunScript != null ? Mathf.Max(0, gunScript.currentAmmo) : Mathf.Max(0, currentAmmoInMag);
    public int BulletIconCount => bulletIcons != null ? bulletIcons.Count : 0;
    public int MagazineCapacity => Mathf.Max(1, maxAmmoInMagazine);
    public int MagazineCount => Mathf.CeilToInt(TotalOwnedAmmo / (float)MagazineCapacity);
    public string HandgunAmmoItemId => ammoItemId;
    public string AmmoItemId => ammoItemId;

    public void BindGun(OnKeyPress_Throw targetGun)
    {
        gunScript = targetGun;
    }

    public void ApplyCatalogCapacity(int capacity)
    {
        maxAmmoInMagazine = Mathf.Max(1, capacity);
        currentAmmoInMag = Mathf.Clamp(currentAmmoInMag, 0, maxAmmoInMagazine);
        if (gunScript != null)
        {
            gunScript.maxAmmo = maxAmmoInMagazine;
            gunScript.currentAmmo = Mathf.Clamp(gunScript.currentAmmo, 0, maxAmmoInMagazine);
        }

        if (Application.isPlaying)
        {
            RefreshBulletIcons();
            UpdateUI();
        }
    }

    [Header("弾薬とインベントリの連携")]
    public PlayerInventory inventory;
    [FormerlySerializedAs("handgunAmmoItemId")]
    [SerializeField]
    string ammoItemId = "m1922";
    [HideInInspector]
    public string magazineSetItemId = "m1917";
    public bool HasInventoryMagazine()
    {
        if (inventory == null)
        {
            inventory = FindObjectOfType<PlayerInventory>(true);
        }

        return inventory != null && inventory.Count(ammoItemId) > 0;
    }

    public bool ConsumeInventoryMagazine()
    {
        // m1922は発射時に1発ずつ消費する。リロード時には消費しない。
        return HasInventoryMagazine();
    }

    public bool ConsumeRound()
    {
        if (TutorialCombatZone.InfiniteAmmo)
        {
            return true;
        }

        if (inventory == null)
        {
            inventory = FindObjectOfType<PlayerInventory>(true);
        }

        if (inventory != null)
        {
            return inventory.RemoveItem(ammoItemId, 1);
        }

        if (totalOwnedAmmo <= 0)
        {
            return false;
        }

        totalOwnedAmmo--;
        UpdateUI();
        return true;
    }

    public int GetReloadAmount() => Mathf.Min(MagazineCapacity, TotalOwnedAmmo);
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        // 共通HUD本体はハンドガン側の「銃UI」だけに置き、
        // SMG側は選択中にそのHUDからこのコンポーネントを参照する。
        if (weaponId == "handgun")
        {
            EquippedWeaponHud hud = GetComponent<EquippedWeaponHud>();
            if (hud == null)
            {
                hud = gameObject.AddComponent<EquippedWeaponHud>();
            }

            hud.Initialize(this);
        }

        if (inventory == null)
        {
            inventory = FindObjectOfType<PlayerInventory>(true);
        }

        if (inventory != null)
        {
            inventory.Changed += SyncInventory;
            SyncInventory();
        }

        if (gunScript != null)
        {
            gunScript.maxAmmo = maxAmmoInMagazine;
            // ✅ 起動時にgunScriptの弾数もWeaponAmmoSystemに合わせる
            gunScript.currentAmmo = Mathf.Min(currentAmmoInMag, TotalOwnedAmmo);
            currentAmmoInMag = gunScript.currentAmmo;
            gunScript.onReload += OnGunReloaded;
            gunScript.ammoSystem = this;
        }

        RefreshBulletIcons();
        UpdateUI();
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.Changed -= SyncInventory;
        }

        if (gunScript != null)
        {
            gunScript.onReload -= OnGunReloaded;
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (gunScript == null)
        {
            return;
        }

        int newAmmo = gunScript.currentAmmo;
        if (newAmmo != currentAmmoInMag)
        {
            currentAmmoInMag = newAmmo;
            RefreshBulletIcons();
            UpdateUI();
        }
    }

    private void OnGunReloaded()
    {
        currentAmmoInMag = gunScript != null ? gunScript.currentAmmo : GetReloadAmount();
        SyncInventory();
        // ✅ gunScript.currentAmmoへの代入不要（既にmaxAmmoで更新済み）
        // totalOwnedAmmo は触らない
        RefreshBulletIcons();
        UpdateUI();
        Debug.Log($"リロード完了 - マガジン残: {currentMagCount} / 累計: {totalOwnedAmmo}");
    }

    public void DiscardLoadedAmmo(int remaining)
    {
        // 弾薬総数はm1922側で管理するため、装填状態だけを更新する。
        currentAmmoInMag = 0;
    }

    /// <summary>
    /// ✅ マガジンが1以上ならtrue、0ならfalse
    /// </summary>
    public bool CanReload()
    {
        return TotalOwnedAmmo > 0 && (gunScript == null || gunScript.currentAmmo <= 0);
    }

    public void AddMagazine()
    {
        TryAddMagazineSet(1);
    }

    private void SyncInventory()
    {
        if (inventory != null)
        {
            currentMagCount = MagazineCount;
        }

        totalOwnedAmmo = TotalOwnedAmmo;
        UpdateUI();
    }

    public bool UseMagazineFromInventory()
    {
        if (gunScript == null || gunScript.isReloading || gunScript.currentAmmo > 0 || !CanReload())
        {
            return false;
        }

        gunScript.currentAmmo = GetReloadAmount();
        currentAmmoInMag = gunScript.currentAmmo;
        SyncInventory();
        RefreshBulletIcons();
        return true;
    }

    private void LegacyAddMagazineUnused()
    {
        int currentSpare = totalOwnedAmmo - currentAmmoInMag;
        if (currentSpare >= maxSpareAmmo)
        {
            Debug.Log("予備弾薬が満タンのため、これ以上拾えません！");
            return;
        }

        currentMagCount++;
        totalOwnedAmmo += maxAmmoInMagazine;
        if (totalOwnedAmmo - currentAmmoInMag > maxSpareAmmo)
        {
            totalOwnedAmmo = currentAmmoInMag + maxSpareAmmo;
        }

        UpdateUI();
    }

    // セット全量が入る場合だけ補充する。装填中の弾数は変更しない。
    public bool TryAddMagazineSet(int count)
    {
        if (count <= 0)
        {
            return false;
        }

        long bullets = (long)count * MagazineCapacity;
        if (bullets > int.MaxValue || (long)TotalOwnedAmmo + bullets > int.MaxValue)
        {
            return false;
        }

        if (inventory == null)
        {
            inventory = FindObjectOfType<PlayerInventory>(true);
        }

        return inventory != null && inventory.AddItem(ammoItemId, (int)bullets);
    }

    private void RefreshBulletIcons()
    {
        for (int i = 0; i < bulletIcons.Count; i++)
        {
            if (bulletIcons[i] == null)
            {
                continue;
            }

            bulletIcons[i].SetActive(i < currentAmmoInMag);
        }
    }

    public void SetDetailedHudVisible(bool visible)
    {
        if (txtMaxSpare != null)
        {
            txtMaxSpare.gameObject.SetActive(visible);
        }

        if (txtCurrentAmmo != null)
        {
            txtCurrentAmmo.gameObject.SetActive(visible);
        }

        if (txtTotalOwned != null)
        {
            txtTotalOwned.gameObject.SetActive(visible);
        }

        for (int i = 0; i < bulletIcons.Count; i++)
        {
            if (bulletIcons[i] != null)
            {
                bulletIcons[i].SetActive(visible && i < currentAmmoInMag);
            }
        }
    }

    public void SetBulletIconsVisible(bool visible, int loadedAmmo)
    {
        for (int i = 0; i < bulletIcons.Count; i++)
        {
            if (bulletIcons[i] != null)
            {
                bulletIcons[i].SetActive(visible && i < Mathf.Max(0, loadedAmmo));
            }
        }
    }

    private void UpdateUI()
    {
        totalOwnedAmmo = TotalOwnedAmmo;
        if (txtMagCount != null)
        {
            txtMagCount.text = currentMagCount.ToString();
        }

        if (txtMaxSpare != null)
        {
            txtMaxSpare.text = maxSpareAmmo.ToString();
        }

        if (txtCurrentAmmo != null)
        {
            txtCurrentAmmo.text = currentAmmoInMag.ToString();
        }

        if (txtTotalOwned != null)
        {
            txtTotalOwned.text = totalOwnedAmmo.ToString();
        }
    }
}
