// 【役割】攻撃の開始条件、アニメーションと攻撃判定の有効期間を管理する。
using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [Header("索敵設定")]
    public string targetTagName = "Player";
    public float detectionRadius = 5f;
    [SerializeField]
    private float loseTargetRadius = 6.5f;
    public bool isChasing;
    [Header("攻撃設定")]
    [SerializeField]
    private float attackDistance = 1.5f;
    [Tooltip("攻撃終了（噛みつき解放）から次の攻撃までの秒数。待機中も追跡は続けます。")]
    [Min(0f)]
    [SerializeField]
    private float attackCooldown = 2f;
    public int attackPower = 100;
    [Header("噛みつき：掴み・接近")]
    public ZombieBiteGrab.Settings biteGrab = new ZombieBiteGrab.Settings();
    private ZombieBiteGrab grab;
    [Header("攻撃用HitBox")]
    [SerializeField]
    private ZombieAttackHitbox rightArmHitbox;
    [SerializeField]
    private ZombieAttackHitbox mouthHitbox;
    [Header("這い接触HitBox")]
    [SerializeField]
    private ZombieCrawlHitbox crawlHitbox;
    public AudioSource audioSource;
    private Animator animator;
    private EnemyManager enemyManager;
    private GameObject target;
    private bool isAttacking;
    private bool damageAppliedThisAttack;
    private float nextAttackTime;
    private int currentAttackVariant;
    private bool isHitStunned;
    private EnemyAwarenessVisual awarenessVisual;
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    private void Start()
    {
        animator = GetComponent<Animator>();
        grab = GetComponent<ZombieBiteGrab>();
        if (grab == null)
        {
            grab = gameObject.AddComponent<ZombieBiteGrab>();
        }

        enemyManager = GetComponent<EnemyManager>();
        awarenessVisual = GetComponent<EnemyAwarenessVisual>();
        if (awarenessVisual == null)
        {
            awarenessVisual = gameObject.AddComponent<EnemyAwarenessVisual>();
        }

        SphereCollider detectionCollider = gameObject.AddComponent<SphereCollider>();
        detectionCollider.isTrigger = true;
        detectionCollider.radius = detectionRadius;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        InitializeHitbox(rightArmHitbox);
        InitializeHitbox(mouthHitbox);
        DisableAllAttackHitboxes();
        crawlHitbox?.Deactivate();
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (target == null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, target.transform.position);
        if (distance > loseTargetRadius)
        {
            LoseTarget();
            return;
        }

        if (distance <= attackDistance)
        {
            TryStartAttack();
        }
    }

    // トリガー範囲へ入った相手を受け取り、以下の条件に合う場合の処理を行います。
    private void OnTriggerEnter(Collider other)
    {
        TryDetectPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // 索敵Collider生成時に既にプレイヤーが内側にいた場合も検知する。
        TryDetectPlayer(other);
    }

    private void TryDetectPlayer(Collider other)
    {
        if (target != null)
        {
            return;
        }

        Transform candidate = other.transform;
        Transform tagged = candidate.CompareTag(targetTagName) ? candidate : candidate.root;
        if (!tagged.CompareTag(targetTagName))
        {
            return;
        }

        target = tagged.gameObject;
        isChasing = true;
        if (audioSource != null)
        {
            audioSource.Play();
        }

        if (enemyManager != null)
        {
            enemyManager.StartChasing(target.transform);
        }
    }

    private void TryStartAttack()
    {
        var health = GetComponent<EnemyHealth>();
        if (health != null && health.RightHandMissing && target != null && Vector3.Distance(transform.position, target.transform.position) > attackDistance * .65f)
        {
            return;
        }

        if (enemyManager != null && enemyManager.IsCrawling)
        {
            return;
        }

        if (isHitStunned || isAttacking || target == null || Time.time < nextAttackTime)
        {
            return;
        }

        isAttacking = true;
        damageAppliedThisAttack = false;
        currentAttackVariant = Random.Range(0, 2);
        if (health != null && health.LeftHandMissing)
        {
            currentAttackVariant = 0;
        }

        DisableAllAttackHitboxes();
        FaceTarget();
        if (enemyManager != null)
        {
            enemyManager.SetMovementLocked(true);
        }

        if (animator != null)
        {
            animator.SetInteger("AttackVariant", currentAttackVariant);
            animator.SetBool("IsAttacking", true);
            animator.SetTrigger("Attack");
        }

        Debug.Log($"[Zombie Attack] Attack{currentAttackVariant + 1}を開始", this);
    }

    public void AnimationEvent_EnableRightArmHitbox()
    {
        if (!isAttacking || currentAttackVariant != 0)
        {
            return;
        }

        DisableAllAttackHitboxes();
        rightArmHitbox?.Activate();
    }

    public void AnimationEvent_DisableRightArmHitbox()
    {
        rightArmHitbox?.Deactivate();
    }

    public void AnimationEvent_EnableMouthHitbox()
    {
        if (GetComponent<EnemyHealth>()?.LeftHandMissing == true)
        {
            return;
        }

        if (!isAttacking || currentAttackVariant != 1)
        {
            return;
        }

        DisableAllAttackHitboxes();
        mouthHitbox?.Activate();
        // 口Colliderの大きさだけでなく距離・正面角度・壁の有無で掴みを判定する。
        if (biteGrab.enabled && target != null)
        {
            TryGrab(target.GetComponentInParent<PlayerHealth>());
        }
    }

    public void AnimationEvent_DisableMouthHitbox()
    {
        mouthHitbox?.Deactivate();
    }

    public void AnimationEvent_AttackFinished()
    {
        grab?.Release();
        if (!isAttacking)
        {
            return;
        }

        DisableAllAttackHitboxes();
        isAttacking = false;
        damageAppliedThisAttack = false;
        nextAttackTime = Time.time + attackCooldown;
        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
        }

        if (enemyManager != null)
        {
            enemyManager.SetMovementLocked(false);
        }
    }

    public void BeginHitReaction()
    {
        grab?.Release();
        isHitStunned = true;
        isAttacking = false;
        damageAppliedThisAttack = false;
        nextAttackTime = Time.time + attackCooldown;
        DisableAllAttackHitboxes();
        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
            animator.ResetTrigger("Attack");
        }
    }

    public void EndHitReaction()
    {
        isHitStunned = false;
    }

    public void EnableCrawlContactHitbox()
    {
        crawlHitbox?.Activate();
    }

    public void EnterDeathState()
    {
        grab?.Release();
        isHitStunned = true;
        isAttacking = false;
        damageAppliedThisAttack = false;
        isChasing = false;
        target = null;
        DisableAllAttackHitboxes();
        crawlHitbox?.Deactivate();
        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
            animator.SetBool("IsHit", false);
            animator.ResetTrigger("Attack");
            animator.ResetTrigger("Hit");
        }
    }

    public void ApplyHitboxDamage(PlayerHealth playerHealth, ZombieAttackHitbox hitbox)
    {
        ApplyHitboxDamage(playerHealth, hitbox != null ? hitbox.name : "", hitbox != null && hitbox == mouthHitbox);
    }

    public void ApplyHitboxDamage(PlayerHealth playerHealth, string hitboxName)
    {
        ApplyHitboxDamage(playerHealth, hitboxName, false);
    }

    private void ApplyHitboxDamage(PlayerHealth playerHealth, string hitboxName, bool isBite)
    {
        var health = GetComponent<EnemyHealth>();
        if (isBite && health != null && health.LeftHandMissing)
        {
            return;
        }

        if (!isBite && health != null && health.RightHandMissing && playerHealth != null && Vector3.Distance(transform.position, playerHealth.transform.position) > attackDistance * .65f)
        {
            return;
        }

        if (!isAttacking || damageAppliedThisAttack || playerHealth == null)
        {
            return;
        }

        if (isBite && biteGrab.enabled)
        {
            TryGrab(playerHealth);
            return;
        }

        damageAppliedThisAttack = true;
        var damageSettings = GameDamageSettings.Load();
        int resolvedDamage = damageSettings != null ? (isBite ? damageSettings.zombieBiteDamage : damageSettings.zombieStrikeDamage) : attackPower;
        playerHealth.TakeDamage(resolvedDamage, isBite, transform.position);
        DisableAllAttackHitboxes();
        Debug.Log($"[Zombie Attack] {hitboxName}がプレイヤーへ命中 Damage:{resolvedDamage}", this);
    }

    private void LoseTarget()
    {
        grab?.Release();
        DisableAllAttackHitboxes();
        isAttacking = false;
        damageAppliedThisAttack = false;
        isChasing = false;
        target = null;
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
        }

        if (enemyManager != null)
        {
            enemyManager.SetMovementLocked(false);
            enemyManager.StopChasing();
        }
    }

    private void FaceTarget()
    {
        if (target == null)
        {
            return;
        }

        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }

    private void InitializeHitbox(ZombieAttackHitbox hitbox)
    {
        if (hitbox != null)
        {
            hitbox.Initialize(this);
        }
    }

    private void DisableAllAttackHitboxes()
    {
        rightArmHitbox?.Deactivate();
        mouthHitbox?.Deactivate();
    }

    // コンポーネントが無効になったときの処理です。
    private void OnDisable()
    {
        grab?.Release();
        DisableAllAttackHitboxes();
        crawlHitbox?.Deactivate();
    }

    private void TryGrab(PlayerHealth player)
    {
        GameDamageSettings damageSettings = GameDamageSettings.Load();
        int biteDamage = damageSettings != null ? damageSettings.zombieBiteDamage : attackPower;
        if (damageAppliedThisAttack || grab == null || !grab.TryBegin(player, biteDamage, biteGrab))
        {
            return;
        }

        damageAppliedThisAttack = true;
        DisableAllAttackHitboxes();
    }
}
