// 【役割】キャラクターの移動状態・移動距離に合わせて足音を再生する。
using UnityEngine;

// Shared distance-based footsteps. Existing serialized clip/source references are preserved.
public sealed class FootstepAudioController : MonoBehaviour
{
    [Header("足音SE（1歩分の短い音源）")]
    [SerializeField]
    private AudioSource audioSource;
    [SerializeField]
    private AudioClip[] footstepClips;
    [SerializeField]
    private AudioClip[] runFootstepClips;
    [SerializeField, Range(0f, 1f)]
    private float volume = 0.8f;
    [SerializeField, Range(0f, 1f)]
    private float runVolume = 1f;
    [SerializeField]
    private Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField]
    private Vector2 runPitchRange = new Vector2(0.98f, 1.08f);
    [Header("1歩の移動距離（m）")]
    [SerializeField, Min(0.05f)]
    private float walkStepDistance = 0.8f;
    [SerializeField, Min(0.05f)]
    private float runStepDistance = 1.2f;
    [SerializeField, Min(0.05f)]
    private float aimStepDistance = 0.55f;
    [SerializeField, Min(0f)]
    private float minimumActualSpeed = 0.1f;
    [SerializeField, Min(0.01f)]
    private float minimumInterval = 0.12f;
    [SerializeField, Min(0.1f)]
    private float teleportResetDistance = 2f;
    [Header("本体基準の接地確認")]
    [SerializeField]
    private LayerMask groundLayers = ~0;
    [SerializeField, Min(0.01f)]
    private float groundTolerance = 0.2f;
    [SerializeField]
    private bool showFootstepDebug;
    private Vector3 previousPosition;
    private float accumulatedDistance;
    private float lastPlayedAt = -100f;
    private bool hasMovementInput, isRunning;
    private EnemyManager enemy;
    private UnityChan.UnityChanControlScriptWithRgidBody player;
    private Animator animator;
    private CapsuleCollider body;
    private AudioClip lastClip;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        enemy = GetComponent<EnemyManager>();
        player = GetComponent<UnityChan.UnityChanControlScriptWithRgidBody>();
        animator = GetComponent<Animator>();
        body = GetComponent<CapsuleCollider>();
        if (audioSource == null)
        {
            var audioObject = new GameObject("Movement Footstep Audio");
            audioObject.transform.SetParent(transform, false);
            audioSource = audioObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.Stop();
        audioSource.spatialBlend = enemy != null ? 1f : 0f;
        if (enemy != null)
        {
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 25f;
        }

        ResetDistance();
    }

    // コンポーネントが有効になったときの処理です。
    private void OnEnable()
    {
        ResetDistance();
    }

    // コンポーネントが無効になったときの処理です。
    private void OnDisable()
    {
        ResetDistance();
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    public void ResetDistance()
    {
        previousPosition = transform.position;
        accumulatedDistance = 0f;
    }

    public void ReportMovementState(bool hasInput, bool running)
    {
        hasMovementInput = hasInput;
        isRunning = running;
    }

    private bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        if (body != null)
        {
            origin.y = body.bounds.min.y + 0.3f;
        }

        foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 0.3f + groundTolerance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.root == transform.root)
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<EnemyManager>() != null || hit.collider.GetComponentInParent<UnityChan.UnityChanControlScriptWithRgidBody>() != null)
            {
                continue;
            }

            if (hit.normal.y > 0.4f)
            {
                return true;
            }
        }

        return false;
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    private void LateUpdate()
    {
        Vector3 delta = transform.position - previousPosition;
        previousPosition = transform.position;
        float displacement = delta.magnitude;
        delta.y = 0f;
        float distance = delta.magnitude;
        bool allowed = false;
        bool running = isRunning;
        bool aiming = false;
        if (enemy != null)
        {
            allowed = enemy.isActiveAndEnabled && enemy.CanPlayMovementFootsteps;
            running = enemy.IsChasing;
        }
        else if (player != null)
        {
            allowed = player.isActiveAndEnabled && player.canControl && hasMovementInput && GameInputManager.Instance != null && GameInputManager.Instance.Gameplay;
            if (animator != null)
            {
                aiming = animator.GetBool("Setup");
                allowed &= !animator.GetCurrentAnimatorStateInfo(0).IsTag("PlayerHit");
            }
        }

        if (!allowed || Time.deltaTime <= 0f || displacement > teleportResetDistance || !IsGrounded())
        {
            accumulatedDistance = 0f;
            return;
        }

        // Physics may not advance on every rendered frame; retain the partial stride.
        if (distance / Time.deltaTime < minimumActualSpeed)
        {
            return;
        }

        accumulatedDistance += distance;
        float step = Mathf.Max(0.05f, aiming ? aimStepDistance : running ? runStepDistance : walkStepDistance);
        if (accumulatedDistance < step || Time.time - lastPlayedAt < minimumInterval)
        {
            return;
        }

        accumulatedDistance %= step;
        PlayStep(running);
    }

    private void PlayStep(bool running)
    {
        AudioClip[] clips = running && runFootstepClips != null && runFootstepClips.Length > 0 ? runFootstepClips : footstepClips;
        if (audioSource == null || clips == null || clips.Length == 0)
        {
            return;
        }

        AudioClip clip = null;
        int start = Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip candidate = clips[(start + i) % clips.Length];
            if (candidate == null)
            {
                continue;
            }

            clip = candidate;
            if (clip != lastClip)
            {
                break;
            }
        }

        if (clip == null)
        {
            return;
        }

        lastClip = clip;
        lastPlayedAt = Time.time;
        Vector2 pitch = running ? runPitchRange : pitchRange;
        audioSource.pitch = Random.Range(Mathf.Min(pitch.x, pitch.y), Mathf.Max(pitch.x, pitch.y));
        audioSource.PlayOneShot(clip, running ? runVolume : volume);
        if (showFootstepDebug)
        {
            Debug.Log($"[Footstep] {name} / {(running ? "Run" : "Walk")} / {clip.name}", this);
        }
    }
}
