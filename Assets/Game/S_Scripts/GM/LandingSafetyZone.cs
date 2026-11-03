// 【役割】ヘリ着陸地点の安全範囲を定義・管理する。
using System.Collections.Generic;
using UnityEngine;

public class LandingSafetyZone : MonoBehaviour
{
    [Header("セーフティポイント設定")]
    [Tooltip("Enemyが侵入できない円形セーフティゾーンの半径")]
    [SerializeField, Min(0.1f)]
    private float safetyRadius = 10f;
    [Tooltip("Enemyを境界外へ戻す余白")]
    [SerializeField, Min(0.01f)]
    private float boundaryMargin = 0.2f;
    [Header("デバッグ表示")]
    [SerializeField]
    private Color gizmoColor = new Color(0f, 1f, 0.5f, 0.8f);
    [SerializeField, Min(4)]
    private int gizmoSegments = 24;
    private readonly HashSet<Transform> checkedEnemies = new HashSet<Transform>();
    private Vector3 zoneCenter;
    private Vector3 zoneForward = Vector3.forward;
    private bool zoneActive;
    public float SafetyRadius => safetyRadius;
    public Vector3 ZoneCenter => zoneCenter;
    public bool IsZoneActive => zoneActive;

    // コンポーネントが有効になったときの処理です。
    private void OnEnable()
    {
        ScoreAndTimeManager.OnMainGameStarted += DisableSafetyZone;
    }

    // コンポーネントが無効になったときの処理です。
    private void OnDisable()
    {
        ScoreAndTimeManager.OnMainGameStarted -= DisableSafetyZone;
    }

    public void Configure(Vector3 center, Vector3 forward)
    {
        zoneCenter = center;
        zoneForward = forward;
        zoneForward.y = 0f;
        if (zoneForward.sqrMagnitude < 0.001f)
        {
            zoneForward = Vector3.forward;
        }

        zoneForward.Normalize();
        zoneActive = true;
        transform.position = zoneCenter;
        transform.rotation = Quaternion.LookRotation(zoneForward);
        Debug.Log($"[Landing Safety Zone] 有効化 Center:{zoneCenter} " + $"Radius:{safetyRadius}", this);
    }

    // 物理演算の一定間隔で呼ばれます。以下の状態更新を行います。
    private void FixedUpdate()
    {
        if (!zoneActive)
        {
            return;
        }

        Collider[] nearbyColliders = Physics.OverlapSphere(zoneCenter, safetyRadius, ~0, QueryTriggerInteraction.Collide);
        checkedEnemies.Clear();
        foreach (Collider nearbyCollider in nearbyColliders)
        {
            Transform enemyRoot = nearbyCollider.transform.root;
            if (!enemyRoot.CompareTag("Enemy") || !checkedEnemies.Add(enemyRoot))
            {
                continue;
            }

            KeepEnemyOutside(enemyRoot);
        }
    }

    private void KeepEnemyOutside(Transform enemy)
    {
        Vector3 offset = enemy.position - zoneCenter;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance > safetyRadius)
        {
            return;
        }

        Vector3 outwardDirection = distance > 0.001f ? offset.normalized : zoneForward;
        Vector3 horizontalPosition = zoneCenter + outwardDirection * (safetyRadius + boundaryMargin);
        Vector3 correctedPosition = enemy.position;
        correctedPosition.x = horizontalPosition.x;
        correctedPosition.z = horizontalPosition.z;
        enemy.position = correctedPosition;
    }

    private void DisableSafetyZone()
    {
        zoneActive = false;
        Debug.Log("[Landing Safety Zone] ゲーム開始のため無効化", this);
        gameObject.SetActive(false);
    }

    private void OnDrawGizmos()
    {
        if (!zoneActive || safetyRadius <= 0f)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        int segmentCount = Mathf.Max(8, gizmoSegments);
        Vector3 previousPoint = zoneCenter + Vector3.forward * safetyRadius;
        for (int index = 1; index <= segmentCount; index++)
        {
            float angle = 360f * index / segmentCount;
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            Vector3 nextPoint = zoneCenter + direction * safetyRadius;
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }
}
