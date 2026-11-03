// 【役割】射撃訓練エリア内の無限弾・被ダメージ保護・敵の再出現と退出条件を管理する。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class TutorialCombatZone : MonoBehaviour
{
    public static bool PlayerProtected { get; private set; }
    public static bool InfiniteAmmo { get; private set; }

    [Header("ゾンビ")]
    [SerializeField]
    Transform respawnPoint;
    [SerializeField]
    GameObject zombiePrefab;
    [SerializeField, Min(0f)]
    float respawnDelay = 3f;
    [SerializeField, Min(0f)]
    float despawnDelayAfterExit = 2f;
    [Header("出口制限")]
    [Tooltip("拠点側の正規出口位置。この周囲からのみ退出できます。")]
    [SerializeField]
    Transform entranceExitPoint;
    [SerializeField, Min(.1f)]
    float entranceExitRadius = 4f;
    [Tooltip("奥側に出ようとしたプレイヤーを戻す位置。")]
    [SerializeField]
    Transform blockedReturnPoint;
    [Header("ロケーション表示")]
    [SerializeField]
    string locationAnnouncement = "射撃訓練エリア";
    Transform player;
    GameObject zombie;
    readonly List<GameObject> spawnedZombies = new List<GameObject>();
    Coroutine respawnRoutine;
    Coroutine despawnRoutine;
    bool inside;
    // コンポーネントの追加・リセット時に使う既定設定を用意します。
    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        if (inside)
        {
            Deactivate();
        }
    }

    // トリガー範囲へ入った相手を受け取り、以下の条件に合う場合の処理を行います。
    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        player = other.transform.root;
        inside = true;
        PlayerProtected = InfiniteAmmo = true;
        CombatHudVisibilityController.ShowTrainingHud(true);
        LocationAnnouncementUI.Instance?.Show(locationAnnouncement);
        if (despawnRoutine != null)
        {
            StopCoroutine(despawnRoutine);
            despawnRoutine = null;
        }

        if (zombie == null && respawnRoutine == null)
        {
            respawnRoutine = StartCoroutine(RespawnAfter(0f));
        }
    }

    // トリガー範囲から出た相手に対する処理を行います。
    void OnTriggerExit(Collider other)
    {
        if (player == null || other.transform.root != player)
        {
            return;
        }

        if (entranceExitPoint != null && Vector3.Distance(player.position, entranceExitPoint.position) > entranceExitRadius)
        {
            Vector3 returnPosition = blockedReturnPoint != null ? blockedReturnPoint.position : GetComponent<Collider>().bounds.center;
            var body = player.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.position = returnPosition;
            }
            else
            {
                player.position = returnPosition;
            }

            return;
        }

        Deactivate();
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (zombie != null)
        {
            EnemyHealth health = zombie.GetComponent<EnemyHealth>();
            if (health != null && health.IsDead)
            {
                zombie = null;
            }
        }

        if (inside && zombie == null && respawnRoutine == null)
        {
            respawnRoutine = StartCoroutine(RespawnAfter(respawnDelay));
        }
    }

    IEnumerator RespawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        respawnRoutine = null;
        if (!inside || zombiePrefab == null || respawnPoint == null)
        {
            yield break;
        }

        zombie = Instantiate(zombiePrefab, respawnPoint.position, respawnPoint.rotation);
        spawnedZombies.Add(zombie);
        if (player != null)
        {
            zombie.GetComponent<EnemyManager>()?.StartChasing(player);
        }
    }

    void Deactivate()
    {
        inside = false;
        PlayerProtected = InfiniteAmmo = false;
        CombatHudVisibilityController.ShowTrainingHud(false);
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }

        if (spawnedZombies.Count > 0 && despawnRoutine == null)
        {
            despawnRoutine = StartCoroutine(DespawnAfterExit());
        }
    }

    IEnumerator DespawnAfterExit()
    {
        yield return new WaitForSeconds(despawnDelayAfterExit);
        if (!inside)
        {
            foreach (GameObject spawned in spawnedZombies)
            {
                if (spawned != null)
                {
                    Destroy(spawned);
                }
            }

            spawnedZombies.Clear();
            zombie = null;
        }

        despawnRoutine = null;
    }

    static bool IsPlayer(Collider other) => other.CompareTag("Player") || other.transform.root.CompareTag("Player") || other.GetComponentInParent<PlayerHealth>() != null;
}
