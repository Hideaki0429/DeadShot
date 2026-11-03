// 【役割】プレイヤーのアイテム所持数、取得履歴とアイテム定義を管理する。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum InventoryCategory
{
    Weapons,
    Supplies
}

[DefaultExecutionOrder(-900)]
public sealed class PlayerInventory : MonoBehaviour
{
    [Serializable]
    public class WeaponParameters
    {
        [Tooltip("ONにすると、このカタログ項目の武器パラメータをゲーム中の銃へ適用します。")]
        public bool enabled;
        [Header("1. 威力（Power）")]
        [Min(0)]
        public int power = 100;
        [Header("2. 安定性（Stability）")]
        [Tooltip("0～100。高いほど連射時の追加ブレが少なく、照準が早く安定します。")]
        [Range(0f, 100f)]
        public float stability = 70f;
        [Header("3. 精度（Precision）")]
        [Tooltip("0～100。高いほど通常時・収束時の弾の拡散角度が小さくなります。")]
        [Range(0f, 100f)]
        public float precision = 75f;
        [Header("4. 連射速度（Rate of Fire）")]
        [Tooltip("1秒間に発射できる弾数。例：10なら0.1秒間隔です。")]
        [Min(.01f)]
        public float rateOfFire = 1f;
        [Header("5. 装填速度（Reload Speed）")]
        [Tooltip("1秒間あたりのリロード回数。0.5ならリロード時間は2秒です。")]
        [Min(.01f)]
        public float reloadSpeed = .5f;
        [Header("6. 装弾数（Ammo Capacity）")]
        [Min(1)]
        public int ammoCapacity = 7;
        [Header("完全フォーカス補正")]
        [Tooltip("完全収束時の弾拡散倍率。0.05なら5%まで縮小します。")]
        [Range(0f, 1f)]
        public float focusedSpreadMultiplier = .05f;
        [Tooltip("完全収束時の通常ダメージ倍率。")]
        [Min(0f)]
        public float focusedDamageMultiplier = 1.2f;
        [Tooltip("完全収束したヘッドショット倍率。")]
        [Min(0f)]
        public float focusedHeadshotMultiplier = 1.35f;
    }

    [Serializable]
    public class Definition
    {
        public string id, displayName;
        public Sprite icon;
        [TextArea(3, 6)]
        public string description;
        public InventoryCategory category;
        [Min(0)]
        public int initialCount;
        [Tooltip("最大所持数。0は無制限。手榴弾は未設定でも30個になります。")]
        [Min(0)]
        public int maxCount;
        [Header("取得時の3D検査表示")]
        [Tooltip("取得パネルに表示する専用Prefab。未登録の場合はステージ上の取得物を複製します。")]
        public GameObject inspectionPrefab;
        [Tooltip("取得パネルを開いたときの初期角度。")]
        public Vector3 inspectionRotation;
        [Tooltip("自動中央合わせ後の位置調整。")]
        public Vector3 inspectionPosition;
        [Tooltip("モデルのBounds中心を0とした回転軸の位置調整。例：Yを上げると中心より上を軸に回転します。")]
        public Vector3 inspectionPivotOffset;
        [Tooltip("自動サイズ調整後の倍率。0以下は1倍扱い。")]
        public float inspectionScale = 1f;
        [Header("武器ショートカット")]
        [Tooltip("空欄ならショートカット登録できません。WeaponSelectionUIのweaponIdと一致させます。")]
        public string shortcutWeaponId;
        [Header("銃の個別パラメータ")]
        public WeaponParameters weaponParameters = new WeaponParameters();
        public bool CanRegisterShortcut => category == InventoryCategory.Weapons && !string.IsNullOrWhiteSpace(ResolvedShortcutWeaponId);

        public string ResolvedShortcutWeaponId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(shortcutWeaponId))
                {
                    return shortcutWeaponId.Trim();
                }

                if (string.Equals(id, "m1911", StringComparison.OrdinalIgnoreCase))
                {
                    return "handgun";
                }

                if (string.Equals(id, "m1918", StringComparison.OrdinalIgnoreCase))
                {
                    return "smg";
                }

                if (string.Equals(id, "m1912", StringComparison.OrdinalIgnoreCase))
                {
                    return "flashlight";
                }

                if (string.Equals(id, "m1913", StringComparison.OrdinalIgnoreCase))
                {
                    return "knife";
                }

                if (string.Equals(id, "m1914", StringComparison.OrdinalIgnoreCase))
                {
                    return "goggles";
                }

                if (string.Equals(id, "m1916", StringComparison.OrdinalIgnoreCase))
                {
                    return "grenade";
                }

                return "";
            }
        }
    }

    [Serializable]
    public class Stack
    {
        public string id;
        public int count;
        public Stack(string id, int count)
        {
            this.id = id;
            this.count = count;
        }
    }

    public List<Definition> catalog = new List<Definition>();
    [SerializeField]
    private List<Stack> owned = new List<Stack>();
    // 所持数とは分離し、ステージ上で拾った履歴だけを記録する。
    // 使い切って0個になっても、再取得時は2回目以降と判定される。
    private readonly HashSet<string> pickedUpItemIds = new HashSet<string>();
    public IReadOnlyList<Stack> Owned => owned;

    public event Action Changed;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        owned.Clear();
        bool receiveFromBaseBoxes = SceneManager.GetActiveScene().name.Equals("Stage_city", StringComparison.OrdinalIgnoreCase);
        var seen = new HashSet<string>();
        foreach (var d in catalog)
        {
            if (d == null || string.IsNullOrWhiteSpace(d.id) || !seen.Add(d.id))
            {
                Debug.LogWarning("[Inventory] 空または重複したIDは使用できません。", this);
                continue;
            }

            if (d.initialCount > 0 && !(receiveFromBaseBoxes && IsBaseSupplyItem(d.id)))
            {
                owned.Add(new Stack(d.id, d.initialCount));
            }
        }

        ApplyCatalogWeaponParameters();
    }

    static bool IsBaseSupplyItem(string id)
    {
        switch (id)
        {
            case "m1911":
            case "m1918":
            case "m1912":
            case "m1913":
            case "m1914":
            case "m1916":
            case "m1917":
            case "m1922":
            case "m1915":
            case "m1924":
            case "m1919":
            case "m1920":
                return true;
            default:
                return false;
        }
    }

    public void ApplyCatalogWeaponParameters()
    {
        OnKeyPress_Throw[] guns = FindObjectsOfType<OnKeyPress_Throw>(true);
        WeaponAmmoSystem[] ammoSystems = FindObjectsOfType<WeaponAmmoSystem>(true);
        foreach (Definition definition in catalog)
        {
            if (definition == null || definition.category != InventoryCategory.Weapons || definition.weaponParameters == null || !definition.weaponParameters.enabled)
            {
                continue;
            }

            string weaponId = definition.ResolvedShortcutWeaponId;
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                continue;
            }

            foreach (OnKeyPress_Throw gun in guns)
            {
                if (gun != null && string.Equals(gun.requiredWeaponId, weaponId, StringComparison.OrdinalIgnoreCase))
                {
                    gun.ApplyCatalogParameters(definition.weaponParameters);
                }
            }

            foreach (WeaponAmmoSystem ammo in ammoSystems)
            {
                if (ammo != null && string.Equals(ammo.WeaponId, weaponId, StringComparison.OrdinalIgnoreCase))
                {
                    ammo.ApplyCatalogCapacity(definition.weaponParameters.ammoCapacity);
                }
            }
        }
    }

    // Inspectorの値の変更・読み込み時に、設定の検証や反映を行います。
    void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplyCatalogWeaponParameters();
        }
    }

    public Definition Find(string id) => catalog.Find(d => d != null && d.id == id);
    public int Count(string id) => owned.Find(s => s.id == id)?.count ?? 0;
    public int MaxCount(string id)
    {
        Definition definition = Find(id);
        if (definition == null)
        {
            return 0;
        }

        if (definition.maxCount > 0)
        {
            return definition.maxCount;
        }

        return string.Equals(id, "m1916", StringComparison.OrdinalIgnoreCase) ? 30 : int.MaxValue;
    }

    public bool RegisterPickupAndIsFirst(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || Find(id) == null)
        {
            return false;
        }

        return pickedUpItemIds.Add(id);
    }

    public bool HasPickedUp(string id) => !string.IsNullOrWhiteSpace(id) && pickedUpItemIds.Contains(id);
    public bool AddItem(string id, int amount = 1)
    {
        if (amount <= 0 || Find(id) == null)
        {
            Debug.LogWarning("[Inventory] 未登録IDまたは不正な個数: " + id, this);
            return false;
        }

        var stack = owned.Find(s => s.id == id);
        int current = stack != null ? stack.count : 0;
        int maximum = MaxCount(id);
        if (amount > maximum - current)
        {
            Debug.Log("[Inventory] 最大所持数のため取得できません: " + id + " (" + current + " / " + maximum + ")", this);
            return false;
        }

        if (stack == null)
        {
            owned.Add(new Stack(id, amount));
        }
        else
        {
            if (stack.count > int.MaxValue - amount)
            {
                return false;
            }

            stack.count += amount;
        }

        Changed?.Invoke();
        return true;
    }

    public bool RemoveItem(string id, int amount = 1)
    {
        var stack = owned.Find(s => s.id == id);
        if (amount <= 0 || stack == null || stack.count < amount)
        {
            return false;
        }

        stack.count -= amount;
        if (stack.count == 0)
        {
            owned.Remove(stack);
        }

        Changed?.Invoke();
        return true;
    }
}
