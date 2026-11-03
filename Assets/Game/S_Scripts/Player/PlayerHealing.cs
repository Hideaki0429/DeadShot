// 【役割】回復アイテムの使用入力、モーションと回復効果の適用を管理する。
using UnityEngine;

public sealed class PlayerHealing : MonoBehaviour
{
    [Header("インベントリ連携")]
    public PlayerInventory inventory;
    [Tooltip("救急キットの登録ID")]
    public string healingItemId = "m1915";
    [Tooltip("Qキーで使用する感染緩和薬の登録ID")]
    public string infectionMedicineItemId = "m1924";
    [Header("飲むSE")]
    [Range(0f, 1f)]
    public float drinkingSoundStart = 0.2f;
    AudioSource drinkingSource;
    bool drinkingSoundPlayed;
    PlayerInventory consumingInventory;
    string consumingItemId;
    [Min(1)]
    public int healAmount = 100;
    [Min(0.1f)]
    public float playbackSpeed = 1f;
    public GameObject heldItem;
    public Vector3 gripPosition, gripRotation;
    public Vector3 gripScale = Vector3.one;
    public AnimationClip drinkingClip;
    [Range(0.1f, 1f)]
    public float healNormalizedTime = 0.95f;
    public bool IsDrinking { get; private set; }
    public static bool Busy => active != null && active.IsDrinking;

    static PlayerHealing active;
    Animator animator;
    PlayerHealth health;
    PlayerInfectionResistance infection;
    PlayerFlashlightController flashlight;
    int layer = -1;
    float elapsed, weight;
    bool healed;
    bool savedRootMotion;
    float savedAnimatorSpeed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        active = null;
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<PlayerHealth>();
        infection = GetComponent<PlayerInfectionResistance>();
        flashlight = GetComponent<PlayerFlashlightController>();
        layer = animator != null ? animator.GetLayerIndex("Healing") : -1;
        if (heldItem != null)
        {
            heldItem.SetActive(false);
        }
    }

    // コンポーネントが有効になったときの処理です。
    void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += Cancel;
        }
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= Cancel;
        }

        Cancel();
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (GameInputManager.Instance == null)
        {
            return;
        }

        if (!IsDrinking && GameInputManager.Instance.HealPressed)
        {
            TryDrink();
        }

        if (IsDrinking)
        {
            if (health.CurrentHP <= 0 || ZombieBiteGrab.PlayerIsGrabbed || animator.GetBool("Setup"))
            {
                Cancel();
                return;
            }

            if (Time.timeScale == 0 || PauseMenuController.IsPaused)
            {
                return;
            }

            elapsed += Time.deltaTime * playbackSpeed;
            float normalized = elapsed / Mathf.Max(0.01f, drinkingClip.length);
            if (!drinkingSoundPlayed && normalized >= drinkingSoundStart)
            {
                drinkingSoundPlayed = true;
                drinkingSource = GameSoundService.Play(GameSound.Drinking, transform.position);
            }

            if (!healed && normalized >= healNormalizedTime)
            {
                if (infection == null || consumingInventory == null || !consumingInventory.RemoveItem(consumingItemId, 1))
                {
                    Cancel();
                    return;
                }

                healed = true;
                infection.AddMedicineTime();
            }

            if (normalized >= 1)
            {
                Cancel();
            }
        }

        if (animator != null && layer >= 0)
        {
            animator.SetFloat("HealingSpeed", playbackSpeed);
            weight = Mathf.MoveTowards(weight, IsDrinking ? 1 : 0, Time.deltaTime * 10);
            animator.SetLayerWeight(layer, weight);
        }

        if (heldItem != null && IsDrinking)
        {
            heldItem.transform.localPosition = gripPosition;
            heldItem.transform.localRotation = Quaternion.Euler(gripRotation);
            heldItem.transform.localScale = gripScale;
        }
    }

    public void TryDrink()
    {
        if (Busy || health == null || infection == null || animator == null || layer < 0 || drinkingClip == null || !GameInputManager.Instance.Gameplay || health.CurrentHP <= 0 || animator.GetBool("Setup"))
        {
            return;
        }

        var shooting = GetComponent<OnKeyPress_Throw>();
        if (shooting != null && shooting.isReloading)
        {
            return;
        }

        if (animator.GetCurrentAnimatorStateInfo(0).IsTag("PlayerHit") || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("PlayerHit")) || animator.GetBool("Jump"))
        {
            return;
        }

        if (inventory == null)
        {
            inventory = FindObjectOfType<PlayerInventory>(true);
        }

        if (inventory == null || inventory.Find(infectionMedicineItemId) == null || inventory.Count(infectionMedicineItemId) < 1)
        {
            Debug.Log("[Infection] 感染緩和薬を所持していません。", this);
            return;
        }

        consumingInventory = inventory;
        consumingItemId = infectionMedicineItemId;
        active = this;
        IsDrinking = true;
        elapsed = 0;
        healed = false;
        drinkingSoundPlayed = false;
        savedRootMotion = animator.applyRootMotion;
        savedAnimatorSpeed = animator.speed;
        animator.applyRootMotion = false;
        animator.speed = 1f;
        var body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.velocity = new Vector3(0f, body.velocity.y, 0f);
        }

        animator.SetFloat("Speed", 0f);
        animator.SetFloat("Direction", 0f);
        animator.SetFloat("HealingSpeed", playbackSpeed);
        animator.Play("Healing.Drinking", layer, 0);
        if (heldItem != null)
        {
            heldItem.SetActive(true);
        }

        if (flashlight != null)
        {
            flashlight.SetHealingHidden(true);
        }
    }

    public void Cancel()
    {
        if (drinkingSource != null)
        {
            drinkingSource.Stop();
        }

        drinkingSource = null;
        if (IsDrinking && animator != null)
        {
            animator.applyRootMotion = savedRootMotion;
            animator.speed = savedAnimatorSpeed;
        }

        IsDrinking = false;
        consumingInventory = null;
        consumingItemId = null;
        if (active == this)
        {
            active = null;
        }

        if (heldItem != null)
        {
            heldItem.SetActive(false);
        }

        if (flashlight != null)
        {
            flashlight.SetHealingHidden(false);
        }

        weight = 0;
        if (animator != null && layer >= 0)
        {
            animator.SetLayerWeight(layer, 0);
        }
    }
}
