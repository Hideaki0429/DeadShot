// 【役割】プレイヤーによる周囲のアイテム取得処理を担当する。
using UnityEngine;

public sealed class PlayerItemCollector : MonoBehaviour
{
    public Animator playerAnimator;
    public PlayerHealth playerHealth;
    public InventoryPickup CurrentTarget { get; private set; }
    public float Progress { get; private set; }
    public bool IsCollecting => false;

    private Rigidbody playerBody;
    private FootstepAudioController footsteps;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        playerBody = GetComponent<Rigidbody>();
        footsteps = GetComponent<FootstepAudioController>();
    }

    private bool waitForRelease;
    private float elapsed;
    private int damageFrame = -1;
    // コンポーネントが有効になったときの処理です。
    void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged += OnDamaged;
        }
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged -= OnDamaged;
        }

        Cancel();
    }

    void OnDamaged()
    {
        damageFrame = Time.frameCount;
        Cancel();
    }

    void Cancel()
    {
        CurrentTarget = null;
        elapsed = 0;
        Progress = 0;
        waitForRelease = true;
    }

    bool CanCollect()
    {
        var light = GetComponent<PlayerFlashlightController>();
        if (light != null && light.IsRaised)
        {
            return false;
        }

        var input = GameInputManager.Instance;
        if (input == null || !input.Gameplay || playerAnimator == null || playerHealth == null || playerHealth.CurrentHP <= 0 || damageFrame == Time.frameCount)
        {
            return false;
        }

        if (playerAnimator.GetBool("Setup"))
        {
            return false;
        }

        if (playerAnimator.GetCurrentAnimatorStateInfo(0).IsTag("PlayerHit"))
        {
            return false;
        }

        return !playerAnimator.IsInTransition(0) || !playerAnimator.GetNextAnimatorStateInfo(0).IsTag("PlayerHit");
    }

    public InventoryPickup GetDisplayTarget()
    {
        if (!isActiveAndEnabled || !CanCollect() || !GameInputManager.Instance.PickupAvailable)
        {
            return null;
        }

        if (CurrentTarget != null)
        {
            return CurrentTarget.InRange(transform) ? CurrentTarget : null;
        }

        return FindNearest();
    }

    InventoryPickup FindNearest()
    {
        InventoryPickup result = null;
        float nearest = float.PositiveInfinity;
        foreach (var item in InventoryPickup.Active)
        {
            if (item == null || !item.InRange(transform))
            {
                continue;
            }

            float distance = (item.Trigger.bounds.center - transform.position).sqrMagnitude;
            if (distance < nearest)
            {
                nearest = distance;
                result = item;
            }
        }

        return result;
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        var input = GameInputManager.Instance;
        CurrentTarget = null;
        elapsed = 0;
        Progress = 0;
        waitForRelease = false;
        if (input == null || !input.PickupPressed || !CanCollect())
        {
            return;
        }

        InventoryPickup target = FindNearest();
        if (target == null || !target.InRange(transform))
        {
            return;
        }

        // 長押し進捗は使わず、左クリックが押されたこのフレームで取得を完了する。
        target.CompletePickup();
    }
}
