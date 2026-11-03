// 【役割】敵のHP、命中部位ごとのダメージ、部位破壊と死亡状態を管理する。
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public enum HitPart
    {
        Unregistered,
        Torso,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg,
        Head
    }

    [Header("敵体力")]
    public int maxHP = 200;
    public int currentHP;
    [Header("被弾判定Collider")]
    [SerializeField]
    private Collider torsoCollider;
    [SerializeField]
    private Collider leftArmCollider;
    [SerializeField]
    private Collider rightArmCollider;
    [SerializeField]
    private Collider leftLegCollider;
    [SerializeField]
    private Collider rightLegCollider;
    [SerializeField]
    private Collider headCollider;
    [Header("ミニマップ判定")]
    public Camera miniMapCamera;
    public bool resetHPWhenOutOfMiniMap = true;
    [Header("脚被弾・転倒")]
    [SerializeField]
    private int legHitsToFall = 2;
    [Header("這い状態Collider（任意）")]
    [SerializeField]
    private CapsuleCollider movementCollider;
    [SerializeField]
    private Vector3 crawlColliderCenter = new Vector3(0f, 0.3f, 0f);
    [SerializeField]
    private float crawlColliderHeight = 1.2f;
    [SerializeField]
    private float crawlColliderRadius = 0.25f;
    private EnemyManager enemyManager;
    private EnemyAttack enemyAttack;
    private EnemyBloodEffects bloodEffects;
    private Animator animator;
    private Renderer[] renderers;
    private bool wasInMiniMap = true;
    private bool isDead = false;
    private int legHitCount;
    [Header("部位欠損")]
    [Tooltip("OFFの場合、腕脚に被弾しても欠損・転倒・這い・攻撃制限を発生させません。")]
    public bool enableLimbDestruction = true;
    [Min(1)]
    public int hitsToSever = 2;
    [Min(.1f)]
    public float severedLifetime = 6f;
    private readonly int[] limbHits = new int[7];
    private readonly bool[] severedLimbs = new bool[7];
    public bool LeftHandMissing { get; private set; }
    public bool RightHandMissing { get; private set; }

    private bool isFalling;
    private bool isCrawling;
    private EnemyWeakPoint weakPoint;
    public bool IsDead => isDead;
    public EnemyWeakPoint WeakPoint => weakPoint;

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        if (GetComponent<ZombieVoiceSound>() == null)
        {
            gameObject.AddComponent<ZombieVoiceSound>();
        }

        GameDamageSettings settings = GameDamageSettings.Load();
        if (settings != null)
        {
            maxHP = Mathf.Max(1, settings.zombieMaxHP);
        }

        currentHP = maxHP;
        enemyManager = GetComponent<EnemyManager>();
        enemyAttack = GetComponent<EnemyAttack>();
        bloodEffects = GetComponent<EnemyBloodEffects>();
        animator = GetComponent<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
        weakPoint = GetComponent<EnemyWeakPoint>();
        if (weakPoint == null)
        {
            weakPoint = gameObject.AddComponent<EnemyWeakPoint>();
        }

        weakPoint.Initialize(this);
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (!resetHPWhenOutOfMiniMap || miniMapCamera == null || isDead)
        {
            return;
        }

        bool isInMiniMap = IsVisibleFromMiniMap();
        // ミニマップ内から一度でも消えたらHP回復
        if (wasInMiniMap && !isInMiniMap)
        {
            ResetHP();
        }

        wasInMiniMap = isInMiniMap;
    }

    public void TakeDamage(int damage, HitPart hitPart, Vector3 hitPoint, Vector3 hitNormal, Vector3 shotDirection, Transform hitTransform, bool weakPointKill = false)
    {
        if (isDead)
        {
            return;
        }

        if (bloodEffects != null)
        {
            bloodEffects.PlayHitEffect(hitPart, hitPoint, hitNormal, shotDirection, hitTransform);
        }

        currentHP -= damage;
        currentHP = Mathf.Max(currentHP, 0);
        if (damage > 0)
        {
            GameSoundService.Blood(hitPoint);
        }

        bool lostLeg = false;
        if (enableLimbDestruction && damage > 0 && hitPart >= HitPart.LeftArm && hitPart <= HitPart.RightLeg && ++limbHits[(int)hitPart] == hitsToSever)
        {
            var sever = GetComponent<ZombieLimbSever>();
            if (sever == null)
            {
                sever = gameObject.AddComponent<ZombieLimbSever>();
            }

            sever.Sever(hitPart, shotDirection, severedLifetime);
            severedLimbs[(int)hitPart] = true;
            if (hitPart == HitPart.LeftArm)
            {
                LeftHandMissing = true;
            }

            if (hitPart == HitPart.RightArm)
            {
                RightHandMissing = true;
            }

            lostLeg = IsLegHit(hitPart);
        }

        string hitPartName = GetHitPartDisplayName(hitPart);
        Debug.Log($"[Enemy] {hitPartName} Damage:{damage} HP:{currentHP}/{maxHP}");
        if (currentHP <= 0)
        {
            isDead = true;
            GameDamageSettings damageSettings = GameDamageSettings.Load();
            int killScore = damageSettings != null ? damageSettings.GetKillScore(hitPart, weakPointKill) : 100;
            if (ScoreAndTimeManager.instance != null)
            {
                ScoreAndTimeManager.instance.AddScore(killScore);
                ScoreAndTimeManager.instance.AddKill();
            }

            GetComponent<ZombieVoiceSound>()?.Die();
            GameSoundService.Play(GameSound.ZombieDeath, transform.position + Vector3.up);
            if (bloodEffects != null)
            {
                bloodEffects.PlayCorpseBlood();
            }

            if (enemyManager != null)
            {
                bool hitFromFront = IsHitPointOnFrontHalf(hitPoint);
                enemyManager.TupleDeadSequence(hitPartName, hitFromFront, isCrawling);
            }
            else
            {
                Destroy(gameObject);
            }

            return;
        }

        if (lostLeg && !isFalling && !isCrawling)
        {
            StartFalling();
            return;
        }

        PlayHitReaction(hitPart);
    }

    public void TakeGrenadeDamage(int damage, Vector3 explosionPoint)
    {
        if (isDead || damage <= 0)
        {
            return;
        }

        HitPart[] candidates =
        {
            HitPart.LeftArm,
            HitPart.RightArm,
            HitPart.LeftLeg,
            HitPart.RightLeg
        };
        int start = Random.Range(0, candidates.Length);
        HitPart selected = HitPart.Torso;
        for (int i = 0; i < candidates.Length; i++)
        {
            HitPart candidate = candidates[(start + i) % candidates.Length];
            if (!severedLimbs[(int)candidate])
            {
                selected = candidate;
                break;
            }
        }

        Vector3 direction = transform.position - explosionPoint;
        if (direction.sqrMagnitude < .001f)
        {
            direction = Vector3.up;
        }

        if (enableLimbDestruction && selected != HitPart.Torso)
        {
            limbHits[(int)selected] = Mathf.Max(limbHits[(int)selected], hitsToSever - 1);
        }

        TakeDamage(damage, selected, transform.position + Vector3.up, -direction.normalized, direction.normalized, transform);
    }

    private bool IsHitPointOnFrontHalf(Vector3 hitPoint)
    {
        Vector3 directionToHit = hitPoint - transform.position;
        directionToHit.y = 0f;
        Vector3 zombieForward = transform.forward;
        zombieForward.y = 0f;
        if (directionToHit.sqrMagnitude < 0.0001f || zombieForward.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        float frontDot = Vector3.Dot(zombieForward.normalized, directionToHit.normalized);
        bool isFrontHalf = frontDot >= 0f;
        Debug.Log(isFrontHalf ? $"[Zombie Death] 前半分への命中 Dot:{frontDot:F2} → Death" : $"[Zombie Death] 後ろ半分への命中 Dot:{frontDot:F2} → Dying Death", this);
        return isFrontHalf;
    }

    private bool IsLegHit(HitPart hitPart)
    {
        return hitPart == HitPart.LeftLeg || hitPart == HitPart.RightLeg;
    }

    private void StartFalling()
    {
        if (isDead || isFalling || isCrawling)
        {
            return;
        }

        isFalling = true;
        if (enemyAttack != null)
        {
            enemyAttack.BeginHitReaction();
        }

        if (enemyManager != null)
        {
            enemyManager.SetMovementLocked(true);
        }

        if (animator != null)
        {
            animator.SetBool("IsHit", false);
            animator.ResetTrigger("Hit");
            animator.SetBool("IsFalling", true);
            animator.SetTrigger("Fall");
        }

        Debug.Log("[Zombie Leg] 転倒開始", this);
    }

    public void AnimationEvent_FallFinished()
    {
        if (isDead)
        {
            return;
        }

        isFalling = false;
        isCrawling = true;
        if (animator != null)
        {
            animator.SetBool("IsFalling", false);
            animator.SetBool("IsCrawling", true);
        }

        if (enemyAttack != null)
        {
            enemyAttack.EndHitReaction();
            enemyAttack.EnableCrawlContactHitbox();
        }

        if (enemyManager != null)
        {
            enemyManager.EnterCrawlMode();
            enemyManager.SetMovementLocked(false);
        }

        ConfigureCrawlCollider();
        Debug.Log("[Zombie Leg] 転倒終了・這い状態へ移行", this);
    }

    private void ConfigureCrawlCollider()
    {
        if (movementCollider == null)
        {
            return;
        }

        movementCollider.direction = 2;
        movementCollider.center = crawlColliderCenter;
        movementCollider.height = crawlColliderHeight;
        movementCollider.radius = crawlColliderRadius;
    }

    private void PlayHitReaction(HitPart hitPart)
    {
        int hitType = hitPart == HitPart.Torso ? 1 : 0;
        if (enemyAttack != null)
        {
            enemyAttack.BeginHitReaction();
        }

        if (enemyManager != null)
        {
            enemyManager.SetMovementLocked(true);
        }

        if (animator != null)
        {
            animator.ResetTrigger("Hit");
            animator.SetInteger("HitType", hitType);
            animator.SetBool("IsHit", true);
            animator.SetTrigger("Hit");
        }

        Debug.Log(hitType == 1 ? "[Zombie Hit] 胴Hitを再生" : "[Zombie Hit] 通常Hitを再生", this);
    }

    public void AnimationEvent_HitFinished()
    {
        if (isDead)
        {
            return;
        }

        if (animator != null)
        {
            animator.SetBool("IsHit", false);
        }

        if (enemyAttack != null)
        {
            enemyAttack.EndHitReaction();
        }

        if (enemyManager != null)
        {
            enemyManager.SetMovementLocked(false);
        }

        Debug.Log("[Zombie Hit] Hit終了・移動再開", this);
    }

    public HitPart GetHitPart(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return HitPart.Unregistered;
        }

        if (hitCollider == torsoCollider)
        {
            return HitPart.Torso;
        }

        if (hitCollider == leftArmCollider)
        {
            return HitPart.LeftArm;
        }

        if (hitCollider == rightArmCollider)
        {
            return HitPart.RightArm;
        }

        if (hitCollider == leftLegCollider)
        {
            return HitPart.LeftLeg;
        }

        if (hitCollider == rightLegCollider)
        {
            return HitPart.RightLeg;
        }

        if (hitCollider == headCollider)
        {
            return HitPart.Head;
        }

        return HitPart.Unregistered;
    }

    public Collider GetHitPartCollider(HitPart hitPart)
    {
        switch (hitPart)
        {
            case HitPart.Torso:
                return torsoCollider;
            case HitPart.LeftArm:
                return leftArmCollider;
            case HitPart.RightArm:
                return rightArmCollider;
            case HitPart.LeftLeg:
                return leftLegCollider;
            case HitPart.RightLeg:
                return rightLegCollider;
            case HitPart.Head:
                return headCollider;
            default:
                return null;
        }
    }

    public string GetHitPartDisplayName(HitPart hitPart)
    {
        switch (hitPart)
        {
            case HitPart.Torso:
                return "胴";
            case HitPart.LeftArm:
                return "左腕";
            case HitPart.RightArm:
                return "右腕";
            case HitPart.LeftLeg:
                return "左脚";
            case HitPart.RightLeg:
                return "右脚";
            case HitPart.Head:
                return "頭";
            default:
                return "未登録部位";
        }
    }

    private void ResetHP()
    {
        currentHP = maxHP;
        if (!isFalling && !isCrawling)
        {
            legHitCount = 0;
        }

        Debug.Log($"[Enemy] ミニマップ外に出たためHP回復：{currentHP}/{maxHP}");
    }

    private bool IsVisibleFromMiniMap()
    {
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(miniMapCamera);
        foreach (Renderer r in renderers)
        {
            if (r != null && GeometryUtility.TestPlanesAABB(planes, r.bounds))
            {
                return true;
            }
        }

        return false;
    }
}
