// 【役割】敵に発見された状態に応じてBGMを切り替える。
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDetectionBGM : MonoBehaviour
{
    [Header("敵に発見されたときのBGM")]
    public AudioClip bgmClip;
    [Range(0, 1)]
    public float volume = .3f;
    [Min(.01f)]
    public float fadeInSeconds = .6f;
    [Min(.01f)]
    public float fadeOutSeconds = 2f;
    [Tooltip("全敵の追跡終了からフェードアウトまでの猶予")]
    [Min(0)]
    public float lostDelay = 2f;
    AudioSource source;
    float nextCheck, lastDetected = -999f, level;
    bool detected;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0;
        source.volume = 0;
        // 既存のAmbience音量・Master音量管理に従う。
        var routing = GetComponent<GameAudioRouting>();
        if (routing == null)
        {
            routing = gameObject.AddComponent<GameAudioRouting>();
        }

        routing.category = GameAudioCategory.Ambience;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        // ポーズ中は曲とフェードの進行を止める。UI側のListener.pauseにも従う。
        if (Time.timeScale == 0 || AudioListener.pause)
        {
            if (source.isPlaying)
            {
                source.Pause();
            }

            return;
        }

        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + .25f;
            detected = false;
            foreach (var enemy in FindObjectsOfType<EnemyAttack>())
            {
                var health = enemy.GetComponent<EnemyHealth>();
                if (enemy.isActiveAndEnabled && enemy.isChasing && (health == null || health.currentHP > 0))
                {
                    detected = true;
                    break;
                }
            }
        }

        if (detected)
        {
            lastDetected = Time.time;
        }

        bool wanted = detected || Time.time - lastDetected < lostDelay;
        level = Mathf.MoveTowards(level, wanted ? 1 : 0, Time.deltaTime / Mathf.Max(.01f, wanted ? fadeInSeconds : fadeOutSeconds));
        if (source.clip != bgmClip)
        {
            source.Stop();
            source.clip = bgmClip;
        }

        source.volume = volume * level;
        if (bgmClip != null && level > 0)
        {
            source.UnPause();
            if (!source.isPlaying)
            {
                source.Play();
            }
        }
        else
        {
            source.Stop();
        }
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        if (source != null)
        {
            source.Stop();
        }

        level = 0;
        detected = false;
        lastDetected = -999f;
        nextCheck = 0;
    }
}
