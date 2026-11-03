// 【役割】被ダメージに応じた画面上の動画演出を制御する。
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public sealed class PlayerDamageVideoEffect : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public PlayerDamageScreenEffect existingFilter;
    public RawImage overlay;
    [Header("動画の下に重ねる被ダメージ用フィルター")]
    [Tooltip("追加パネルのImageを登録します。既存の体力連動フィルターとは別のImageを使用してください。")]
    public Image damageFilter;
    [Range(0, 1)]
    public float filterOpacity = .35f;
    public VideoClip clip;
    [Range(0, 1)]
    public float opacity = .75f;
    [Min(.1f)]
    public float playbackSpeed = 1f;
    [Min(0)]
    public float displayDuration = 1.2f;
    [Min(.01f)]
    public float fadeDuration = .5f;
    VideoPlayer video;
    RenderTexture target;
    bool requested, showing, paused;
    float elapsed;
    // コンポーネントが有効になったときの処理です。
    void OnEnable()
    {
        SetAlpha(0);
        if (playerHealth != null)
        {
            playerHealth.Damaged += PlayDamage;
        }
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        video = GetComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.audioOutputMode = VideoAudioOutputMode.None;
        video.source = VideoSource.VideoClip;
        video.clip = clip;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.isLooping = true;
        video.waitForFirstFrame = true;
        video.sendFrameReadyEvents = true;
        target = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
        target.name = "Damage Video Runtime";
        target.Create();
        video.targetTexture = target;
        if (overlay != null)
        {
            overlay.texture = target;
            overlay.raycastTarget = false;
        }

        if (damageFilter != null)
        {
            damageFilter.raycastTarget = false;
            // Place the filter immediately behind the video when they share a parent.
            if (overlay != null && damageFilter.transform.parent == overlay.transform.parent)
            {
                damageFilter.transform.SetSiblingIndex(overlay.transform.GetSiblingIndex());
                overlay.transform.SetSiblingIndex(damageFilter.transform.GetSiblingIndex() + 1);
            }
        }

        video.prepareCompleted += Prepared;
        video.frameReady += FrameReady;
        video.errorReceived += Error;
        SetAlpha(0);
        if (clip != null)
        {
            video.Prepare();
        }
    }

    public void PlayDamage()
    {
        if (video == null || clip == null || overlay == null || PauseMenuController.IsPaused)
        {
            return;
        }

        if (existingFilter != null && !existingFilter.EffectEnabled)
        {
            return;
        }

        requested = true;
        showing = false;
        elapsed = 0;
        SetAlpha(0);
        if (video.isPrepared)
        {
            Restart();
        }
        else
        {
            video.Prepare();
        }
    }

    void Prepared(VideoPlayer source)
    {
        if (requested && !PauseMenuController.IsPaused)
        {
            Restart();
        }
    }

    void Restart()
    {
        video.playbackSpeed = playbackSpeed;
        video.time = 0;
        video.Play();
    }

    void FrameReady(VideoPlayer source, long frame)
    {
        if (!requested || PauseMenuController.IsPaused)
        {
            return;
        }

        if (!showing)
        {
            showing = true;
            elapsed = 0;
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (video == null)
        {
            return;
        }

        if (existingFilter != null && !existingFilter.EffectEnabled)
        {
            Hide();
            return;
        }

        if (PauseMenuController.IsPaused)
        {
            if (!paused)
            {
                paused = true;
                video.Pause();
            }

            SetAlpha(0);
            return;
        }

        if (paused)
        {
            paused = false;
            if (requested && video.isPrepared)
            {
                video.Play();
            }
        }

        if (!showing)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float fade = Mathf.Clamp01((elapsed - displayDuration) / Mathf.Max(.01f, fadeDuration));
        SetAlpha(1 - fade);
        if (fade >= 1)
        {
            Hide();
        }
    }

    void SetAlpha(float visibility)
    {
        if (overlay != null)
        {
            overlay.color = new Color(1, 1, 1, opacity * visibility);
        }

        if (damageFilter != null)
        {
            Color color = damageFilter.color;
            color.a = filterOpacity * visibility;
            damageFilter.color = color;
        }
    }

    void Hide()
    {
        requested = showing = false;
        SetAlpha(0);
        if (video != null && video.isPlaying)
        {
            video.Pause();
        }
    }

    void Error(VideoPlayer source, string error)
    {
        Hide();
        Debug.LogWarning("[DamageVideo] " + error, this);
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged -= PlayDamage;
        }

        Hide();
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (video != null)
        {
            video.prepareCompleted -= Prepared;
            video.frameReady -= FrameReady;
            video.errorReceived -= Error;
            video.Stop();
            video.targetTexture = null;
        }

        if (target != null)
        {
            target.Release();
            Destroy(target);
        }
    }
}
