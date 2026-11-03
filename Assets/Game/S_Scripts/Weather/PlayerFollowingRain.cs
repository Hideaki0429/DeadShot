// 【役割】プレイヤー付近を雨の描画範囲が追従するように制御する。
using UnityEngine;

public sealed class PlayerFollowingRain : MonoBehaviour
{
    [Header("追従対象")]
    [SerializeField]
    private Transform player;
    [Header("Particle参照")]
    [SerializeField]
    private ParticleSystem rainParticles;
    [SerializeField]
    private ParticleSystem splashParticles;
    [Header("追従位置")]
    [SerializeField]
    private float rainHeight = 15f;
    [SerializeField]
    private float splashHeight = 0.05f;
    [Header("開始設定")]
    [SerializeField]
    private bool playOnStart = true;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    private void Start()
    {
        SetRainActive(playOnStart);
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    private void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        Vector3 playerPosition = player.position;
        if (rainParticles != null)
        {
            rainParticles.transform.position = new Vector3(playerPosition.x, playerPosition.y + rainHeight, playerPosition.z);
        }

        if (splashParticles != null)
        {
            splashParticles.transform.position = new Vector3(playerPosition.x, playerPosition.y + splashHeight, playerPosition.z);
        }
    }

    public void SetRainActive(bool active)
    {
        SetParticleActive(rainParticles, active);
        SetParticleActive(splashParticles, active);
    }

    private static void SetParticleActive(ParticleSystem particles, bool active)
    {
        if (particles == null)
        {
            return;
        }

        if (active)
        {
            particles.Play();
        }
        else
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
