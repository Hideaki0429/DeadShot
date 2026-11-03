// 【役割】接触などで受けたダメージをプレイヤーの体力処理へ渡す。
using UnityEngine;

[RequireComponent(typeof(CapsuleCollider))]
public class PlayerDamageReceiver : MonoBehaviour
{
    public PlayerHealth PlayerHealth { get; private set; }

    private CapsuleCollider damageCollider;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        damageCollider = GetComponent<CapsuleCollider>();
        damageCollider.isTrigger = true;
        PlayerHealth = GetComponentInParent<PlayerHealth>();
        if (PlayerHealth == null)
        {
            Debug.LogError("[Player Damage Receiver] 親にPlayerHealthがありません。", this);
        }
    }

    // Inspectorの値の変更・読み込み時に、設定の検証や反映を行います。
    private void OnValidate()
    {
        CapsuleCollider targetCollider = GetComponent<CapsuleCollider>();
        if (targetCollider != null)
        {
            targetCollider.isTrigger = true;
        }
    }
}
