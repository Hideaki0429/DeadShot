// 【役割】探索に応じてマップ上の未探索領域の表示を更新する。
using UnityEngine;

public class MapFogManager : MonoBehaviour
{
    [Header("霧のPrefab設定")]
    public GameObject smokePrefab; // WFX_SmokeGrenade Additiveを指定
    [Header("配置設定")]
    public Vector2 mapSize = new Vector2(50f, 50f); // マップの広さ（X,Z）
    public float spacing = 8f; // パーティクル間隔
    public float groundHeight = 0f; // 地面のY座標
    [Header("パーティクル調整")]
    public float startSizeMultiplier = 5f; // パーティクルのサイズ倍率
    public float startSpeedMultiplier = 0.2f; // 上昇速度（遅くするほど霧らしい）
    [Range(0f, 1f)]
    public float alphaMultiplier = 0.3f; // 透明度（薄くする）
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        SpawnFog();
    }

    void SpawnFog()
    {
        int countX = Mathf.CeilToInt(mapSize.x / spacing);
        int countZ = Mathf.CeilToInt(mapSize.y / spacing);
        float startX = transform.position.x - mapSize.x * 0.5f;
        float startZ = transform.position.z - mapSize.y * 0.5f;
        for (int x = 0; x <= countX; x++)
        {
            for (int z = 0; z <= countZ; z++)
            {
                float posX = startX + x * spacing + Random.Range(-1f, 1f); // 少しランダムにずらす
                float posZ = startZ + z * spacing + Random.Range(-1f, 1f);
                Vector3 spawnPos = new Vector3(posX, groundHeight, posZ);
                GameObject fog = Instantiate(smokePrefab, spawnPos, Quaternion.identity, transform);
                SetupParticle(fog);
            }
        }
    }

    void SetupParticle(GameObject fog)
    {
        ParticleSystem ps = fog.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            return;
        }

        var main = ps.main;
        main.loop = true; // ループ再生にする
        main.startSize = new ParticleSystem.MinMaxCurve(main.startSize.constantMin * startSizeMultiplier, main.startSize.constantMax * startSizeMultiplier);
        main.startSpeed = new ParticleSystem.MinMaxCurve(startSpeedMultiplier);
        // 透明度を下げる
        var startColor = main.startColor.color;
        startColor.a = (byte)(255 * alphaMultiplier);
        main.startColor = startColor;
        // 子のParticleSystemも同様に設定
        foreach (ParticleSystem child in fog.GetComponentsInChildren<ParticleSystem>())
        {
            var childMain = child.main;
            childMain.loop = true;
            var childColor = childMain.startColor.color;
            childColor.a = (byte)(255 * alphaMultiplier);
            childMain.startColor = childColor;
        }

        ps.Play();
    }
}
