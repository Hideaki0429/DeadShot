// 【役割】攻撃中に有効になる接触判定とプレイヤーへのダメージ伝達を処理する。
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ZombieAttackHitbox : MonoBehaviour
{
    private EnemyAttack owner;
    private Collider hitboxCollider;
    private bool alreadyHit;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        hitboxCollider = GetComponent<Collider>();
        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = false;
    }

    public void Initialize(EnemyAttack attackOwner)
    {
        owner = attackOwner;
        if (hitboxCollider == null)
        {
            hitboxCollider = GetComponent<Collider>();
        }

        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = false;
    }

    public void Activate()
    {
        alreadyHit = false;
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = true;
        }
    }

    public void Deactivate()
    {
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }
    }

    // トリガー範囲へ入った相手を受け取り、以下の条件に合う場合の処理を行います。
    private void OnTriggerEnter(Collider other)
    {
        if (alreadyHit || owner == null)
        {
            return;
        }

        PlayerDamageReceiver damageReceiver = other.GetComponent<PlayerDamageReceiver>();
        if (damageReceiver == null || damageReceiver.PlayerHealth == null)
        {
            return;
        }

        alreadyHit = true;
        owner.ApplyHitboxDamage(damageReceiver.PlayerHealth, this);
    }

    // コンポーネントが無効になったときの処理です。
    private void OnDisable()
    {
        Deactivate();
    }
}
