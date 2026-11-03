// 【役割】敵の待機・徘徊・追跡・這い移動と移動アニメーションを管理する。
using System.Collections;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    private enum MovementMode
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Crawl = 3
    }

    public string tagName;
    [SerializeField]
    private bool logMovementChoices;
    public AudioSource audioSource;
    public EnemyAttack enemyAttack;
    [Header("移動速度")]
    public float walkSpeed = 2.0f;
    [SerializeField]
    private float runSpeed = 4.0f;
    [Header("這い移動")]
    [SerializeField]
    private float crawlSpeed = 1.2f;
    [SerializeField]
    private float crawlStopDistance = 1.5f;
    [Header("死体保持")]
    [SerializeField]
    private float corpseHoldDuration = 1.5f;
    [Header("索敵歩行時間")]
    [SerializeField]
    private float minIdleTime = 1.0f;
    [SerializeField]
    private float maxIdleTime = 5.0f;
    [SerializeField]
    private float minWalkTime = 2.0f;
    [SerializeField]
    private float maxWalkTime = 6.0f;
    private Animator animator;
    private MovementMode movementMode = MovementMode.Idle;
    private Transform chaseTarget;
    private Coroutine roamingCoroutine;
    private bool isDead;
    private bool movementLocked;
    private bool isCrawling;
    private bool deathFinished;
    public bool IsChasing => movementMode == MovementMode.Run || movementMode == MovementMode.Crawl;
    public bool IsCrawling => isCrawling;
    public bool CanPlayMovementFootsteps => !isDead && !movementLocked && !isCrawling && (movementMode == MovementMode.Walk || movementMode == MovementMode.Run);

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    private void Start()
    {
        animator = GetComponent<Animator>();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        enemyAttack = GetComponent<EnemyAttack>();
        SetMovementMode(MovementMode.Idle);
        roamingCoroutine = StartCoroutine(RoamingRoutine());
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (isDead || movementLocked)
        {
            return;
        }

        switch (movementMode)
        {
            case MovementMode.Walk:
                MoveForward(walkSpeed);
                break;
            case MovementMode.Run:
                ChaseTarget();
                break;
            case MovementMode.Crawl:
                CrawlTowardTarget();
                break;
        }
    }

    public void StartChasing(Transform target)
    {
        if (isDead || target == null)
        {
            return;
        }

        chaseTarget = target;
        if (roamingCoroutine != null)
        {
            StopCoroutine(roamingCoroutine);
            roamingCoroutine = null;
        }

        if (isCrawling)
        {
            SetMovementMode(MovementMode.Crawl);
            return;
        }

        int runVariant = Random.Range(0, 2);
        if (animator != null)
        {
            animator.SetInteger("RunVariant", runVariant);
        }

        SetMovementMode(MovementMode.Run);
        Debug.Log($"[Zombie] 走行モーション{runVariant + 1}を選択", this);
        StartCoroutine(LogRunStateAfterTransition(runVariant));
    }

    public void StopChasing()
    {
        if (isDead)
        {
            return;
        }

        chaseTarget = null;
        if (isCrawling)
        {
            SetMovementMode(MovementMode.Crawl);
            return;
        }

        SetMovementMode(MovementMode.Idle);
        if (roamingCoroutine == null)
        {
            roamingCoroutine = StartCoroutine(RoamingRoutine());
        }
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
    }

    public void EnterCrawlMode()
    {
        if (isDead)
        {
            return;
        }

        isCrawling = true;
        if (roamingCoroutine != null)
        {
            StopCoroutine(roamingCoroutine);
            roamingCoroutine = null;
        }

        SetMovementMode(MovementMode.Crawl);
    }

    // 外部（弾のスクリプト）から呼び出される死亡受付メソッド
    public void TupleDeadSequence(string hitPart, bool hitFromFront, bool diedWhileCrawling)
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        string deathType = diedWhileCrawling ? "這い姿勢で静止" : hitFromFront ? "Death" : "Dying Death";
        Debug.Log($"[Enemy] {hitPart}への被弾により死亡。処理:{deathType}", this);
        StopAllCoroutines();
        roamingCoroutine = null;
        chaseTarget = null;
        movementLocked = true;
        SetMovementMode(MovementMode.Idle);
        if (enemyAttack != null)
        {
            enemyAttack.EnterDeathState();
        }

        if (animator != null)
        {
            animator.ResetTrigger("Attack");
            animator.ResetTrigger("Hit");
            animator.ResetTrigger("Fall");
            if (diedWhileCrawling)
            {
                animator.speed = 0f;
            }
            else
            {
                animator.SetBool("DeathFromFront", hitFromFront);
                animator.SetBool("DeathFromBack", !hitFromFront);
                animator.SetBool("IsDead", true);
            }
        }

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
        }

        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        if (diedWhileCrawling)
        {
            StartCoroutine(DestroyCorpseAfterDelay());
        }
    }

    public void AnimationEvent_DeathFinished()
    {
        if (!isDead || deathFinished)
        {
            return;
        }

        deathFinished = true;
        StartCoroutine(DestroyCorpseAfterDelay());
    }

    private IEnumerator DestroyCorpseAfterDelay()
    {
        yield return new WaitForSeconds(corpseHoldDuration);
        Destroy(gameObject);
    }

    private IEnumerator RoamingRoutine()
    {
        while (!isDead && chaseTarget == null)
        {
            SetMovementMode(MovementMode.Idle);
            yield return new WaitForSeconds(Random.Range(minIdleTime, maxIdleTime));
            if (isDead || chaseTarget != null)
            {
                break;
            }

            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            int walkVariant = Random.Range(0, 3);
            if (animator != null)
            {
                animator.SetInteger("WalkVariant", walkVariant);
            }

            SetMovementMode(MovementMode.Walk);
            if (logMovementChoices)
            {
                Debug.Log($"[Zombie] 歩行モーション{walkVariant + 1}を選択", this);
            }

            yield return new WaitForSeconds(Random.Range(minWalkTime, maxWalkTime));
        }

        roamingCoroutine = null;
    }

    private void SetMovementMode(MovementMode newMode)
    {
        movementMode = newMode;
        if (animator != null)
        {
            animator.SetInteger("MovementMode", (int)newMode);
        }
    }

    private IEnumerator LogRunStateAfterTransition(int selectedVariant)
    {
        yield return new WaitForSeconds(0.3f);
        if (animator == null)
        {
            Debug.LogWarning("[Zombie Animator確認] Animatorが取得できません。", this);
            yield break;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        int movementModeValue = animator.GetInteger("MovementMode");
        string currentState = "Unknown";
        if (state.IsName("Z_Idle"))
        {
            currentState = "Z_Idle";
        }
        else if (state.IsName("Walk"))
        {
            currentState = "Walk";
        }
        else if (state.IsName("Run1"))
        {
            currentState = "Run1";
        }
        else if (state.IsName("Run"))
        {
            currentState = "Run";
        }
        else if (state.IsName("RightAttack"))
        {
            currentState = "RightAttack";
        }
        else if (state.IsName("MouthAttack"))
        {
            currentState = "MouthAttack";
        }

        Debug.Log($"[Zombie Animator確認] " + $"選択RunVariant={selectedVariant}, " + $"MovementMode={movementModeValue}, " + $"現在ステート={currentState}, " + $"遷移中={animator.IsInTransition(0)}", this);
    }

    private void MoveForward(float speed)
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void ChaseTarget()
    {
        if (chaseTarget == null)
        {
            StopChasing();
            return;
        }

        Vector3 direction = chaseTarget.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        direction.Normalize();
        transform.position += direction * runSpeed * Time.deltaTime;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * runSpeed);
    }

    private void CrawlTowardTarget()
    {
        if (chaseTarget == null)
        {
            return;
        }

        Vector3 direction = chaseTarget.position - transform.position;
        direction.y = 0f;
        float stopDistanceSqr = crawlStopDistance * crawlStopDistance;
        if (direction.sqrMagnitude <= stopDistanceSqr)
        {
            return;
        }

        direction.Normalize();
        transform.position += direction * crawlSpeed * Time.deltaTime;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * crawlSpeed);
    }
}
