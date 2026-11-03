// 【役割】這い状態のゾンビの攻撃接触判定を処理する。
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ZombieCrawlHitbox : MonoBehaviour
{
    [Header("這い接触ダメージ")]
    [SerializeField]
    private int contactDamage = 100;
    [SerializeField]
    private float damageInterval = 1f;
    private Collider hitboxCollider;
    private readonly Dictionary<int, float> nextDamageTimes = new Dictionary<int, float>();
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        hitboxCollider = GetComponent<Collider>();
        hitboxCollider.isTrigger = true;
    }

    public void Activate()
    {
        nextDamageTimes.Clear();
        gameObject.SetActive(true);
        if (hitboxCollider == null)
        {
            hitboxCollider = GetComponent<Collider>();
        }

        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = true;
        Debug.Log("[Zombie Crawl] 這い接触HitBoxを有効化", this);
    }

    public void Deactivate()
    {
        nextDamageTimes.Clear();
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }

        gameObject.SetActive(false);
    }

    // トリガー範囲へ入った相手を受け取り、以下の条件に合う場合の処理を行います。
    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyDamage(other);
    }

    // トリガー範囲から出た相手に対する処理を行います。
    private void OnTriggerExit(Collider other)
    {
        PlayerHealth playerHealth = GetPlayerHealth(other);
        if (playerHealth != null)
        {
            nextDamageTimes.Remove(playerHealth.GetInstanceID());
        }
    }

    private void TryApplyDamage(Collider other)
    {
        PlayerHealth playerHealth = GetPlayerHealth(other);
        if (playerHealth == null)
        {
            return;
        }

        int playerId = playerHealth.GetInstanceID();
        if (nextDamageTimes.TryGetValue(playerId, out float nextDamageTime) && Time.time < nextDamageTime)
        {
            return;
        }

        var damageSettings = GameDamageSettings.Load();
        int resolvedDamage = damageSettings != null ? damageSettings.zombieCrawlDamage : contactDamage;
        playerHealth.TakeDamage(resolvedDamage, false, transform.position);
        nextDamageTimes[playerId] = Time.time + Mathf.Max(0.05f, damageInterval);
        Debug.Log($"[Zombie Crawl] プレイヤーへ接触 Damage:{resolvedDamage}", this);
    }

    private PlayerHealth GetPlayerHealth(Collider other)
    {
        PlayerDamageReceiver damageReceiver = other.GetComponent<PlayerDamageReceiver>();
        return damageReceiver != null ? damageReceiver.PlayerHealth : null;
    }

    // コンポーネントが無効になったときの処理です。
    private void OnDisable()
    {
        nextDamageTimes.Clear();
    }
}
