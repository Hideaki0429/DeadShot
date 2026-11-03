// 【役割】設定した出現地点や条件に基づいてオブジェクトを生成する。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public GameObject prefab; // 配置するプレハブ：Inspectorで指定
    public int spawnCount = 10; // 配置する数：Inspectorで指定
    public Vector2 spawnAreaMin; // 配置範囲の最小値（x, z）：Inspectorで指定
    public Vector2 spawnAreaMax; // 配置範囲の最大値（x, z）：Inspectorで指定
    public float fixedY = 0f; // 固定するy座標：Inspectorで指定
    [Tooltip("オンの場合はシーン開始時に自動生成します。チュートリアル併用時はオフにします")]
    [SerializeField]
    private bool spawnOnStart = true;
    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        if (spawnOnStart)
        {
            SpawnConfiguredEnemies();
        }
    }

    public void SetSpawnOnStart(bool enabled)
    {
        spawnOnStart = enabled;
    }

    public void SpawnConfiguredEnemies()
    {
        if (prefab == null)
        {
            Debug.LogError("[SpawnManager] Enemy Prefabが未設定です。", this);
            return;
        }

        ClearSpawnedEnemies();
        for (int i = 0; i < spawnCount; i++)
        {
            // ランダムな位置を生成
            float randomX = Random.Range(spawnAreaMin.x, spawnAreaMax.x);
            float randomZ = Random.Range(spawnAreaMin.y, spawnAreaMax.y);
            Vector3 spawnPosition = new Vector3(randomX, fixedY, randomZ); // y座標はfixedYに設定
            // プレハブを生成
            GameObject enemy = Instantiate(prefab, spawnPosition, Quaternion.identity);
            spawnedEnemies.Add(enemy);
        }
    }

    public void ClearSpawnedEnemies()
    {
        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }

        spawnedEnemies.Clear();
    }

    private void OnDrawGizmos()
    {
        // シーンビューでのみ表示されるギズモを描画する
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3((spawnAreaMin.x + spawnAreaMax.x) / 2, 0, (spawnAreaMin.y + spawnAreaMax.y) / 2), new Vector3(spawnAreaMax.x - spawnAreaMin.x, 1, spawnAreaMax.y - spawnAreaMin.y));
    }
}
