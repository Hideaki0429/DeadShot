// 【役割】接近判定を使ってヒンジ付きの扉を自動開閉する。
using System.Collections;
using UnityEngine;

public sealed class AutomaticSwingDoor : MonoBehaviour
{
    [Header("扉の表示部")]
    [Tooltip("ネストされたPrefabのままでも回転可能です。ヒンジの子にする必要はありません。")]
    [SerializeField]
    Transform doorVisual;
    [Header("開閉")]
    [SerializeField]
    float openAngle = -90f;
    [SerializeField, Min(1f)]
    float angularSpeed = 120f;
    [SerializeField, Min(0f)]
    float closeDelay = .5f;
    Vector3 closedWorldPosition;
    Quaternion closedWorldRotation;
    float currentAngle;
    int playerContacts;
    Coroutine closeRoutine;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        if (doorVisual == null && transform.childCount > 0)
        {
            doorVisual = transform.GetChild(0);
        }

        if (doorVisual == null)
        {
            Debug.LogError("[AutomaticSwingDoor] Door Visualが未設定です。", this);
            enabled = false;
            return;
        }

        closedWorldPosition = doorVisual.position;
        closedWorldRotation = doorVisual.rotation;
        currentAngle = 0f;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        float targetAngle = playerContacts > 0 ? openAngle : 0f;
        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, angularSpeed * Time.deltaTime);
        // chainlink_singleはHospital内のネストPrefabなので、階層を変えず
        // ヒンジのワールド座標を中心に表示本体を旋回させる。
        Quaternion rotationFromClosed = Quaternion.AngleAxis(currentAngle, transform.up);
        doorVisual.position = transform.position + rotationFromClosed * (closedWorldPosition - transform.position);
        doorVisual.rotation = rotationFromClosed * closedWorldRotation;
    }

    // トリガー範囲へ入った相手を受け取り、以下の条件に合う場合の処理を行います。
    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerContacts++;
        if (closeRoutine != null)
        {
            StopCoroutine(closeRoutine);
            closeRoutine = null;
        }
    }

    // トリガー範囲から出た相手に対する処理を行います。
    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerContacts = Mathf.Max(0, playerContacts - 1);
        if (playerContacts == 0)
        {
            closeRoutine = StartCoroutine(DelayedClose());
        }
    }

    IEnumerator DelayedClose()
    {
        yield return new WaitForSeconds(closeDelay);
        playerContacts = 0;
        closeRoutine = null;
    }

    static bool IsPlayer(Collider other) => other.CompareTag("Player") || other.transform.root.CompareTag("Player") || other.GetComponentInParent<PlayerHealth>() != null;
}
