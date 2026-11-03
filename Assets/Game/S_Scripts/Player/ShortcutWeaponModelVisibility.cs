// 【役割】選択中の武器に対応するプレイヤーの武器モデルを表示する。
using UnityEngine;

/// <summary>
/// ショートカットで確定した武器の手元モデルを表示する。
/// 構え状態ではなくWeaponSelectionUI.SelectedWeaponIdが判定基準。
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class ShortcutWeaponModelVisibility : MonoBehaviour
{
    [Header("武器選択")]
    [SerializeField]
    WeaponSelectionUI weaponSelection;
    [Header("手元モデル")]
    [Tooltip("射撃コンポーネントを持つPistol_00ルート。武器選択にかかわらず常時ONにします。")]
    [SerializeField]
    GameObject alwaysActivePistolRoot;
    [SerializeField]
    GameObject handgunModel;
    [SerializeField]
    GameObject grenadeModel;
    public GameObject HandgunModel => handgunModel;
    public GameObject GrenadeModel => grenadeModel;

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        ResolveReferences();
        Apply();
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        if (weaponSelection == null)
        {
            weaponSelection = FindObjectOfType<WeaponSelectionUI>(true);
        }

        Apply();
    }

    public void ResolveReferences()
    {
        if (weaponSelection == null)
        {
            weaponSelection = FindObjectOfType<WeaponSelectionUI>(true);
        }

        if (handgunModel == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "M1911 Handgun_Black (Shooting)")
                {
                    handgunModel = child.gameObject;
                    break;
                }
            }
        }

        if (alwaysActivePistolRoot == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Pistol_00")
                {
                    alwaysActivePistolRoot = child.gameObject;
                    break;
                }
            }
        }

        if (grenadeModel == null)
        {
            PlayerGrenadeAnimation grenade = GetComponent<PlayerGrenadeAnimation>();
            if (grenade != null)
            {
                grenade.ResolvePinReferences();
                if (grenade.grenadeModel != null)
                {
                    grenadeModel = grenade.grenadeModel.gameObject;
                }
            }
        }
    }

    public void Apply()
    {
        string selected = weaponSelection != null ? weaponSelection.SelectedWeaponId : string.Empty;
        if (alwaysActivePistolRoot != null)
        {
            alwaysActivePistolRoot.SetActive(true);
        }

        if (handgunModel != null)
        {
            handgunModel.SetActive(selected == "handgun");
        }

        if (grenadeModel != null)
        {
            grenadeModel.SetActive(selected == "grenade");
        }
    }
}
