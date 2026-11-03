// 【役割】噛みつき中の敵とプレイヤーの拘束、位置合わせと解除を管理する。
using System.Collections.Generic;
using UnityEngine;

/// <summary>口攻撃の掴み・接近・単発ダメージ・解放。壁や床のColliderは無効にしない。</summary>
[DefaultExecutionOrder(300)]
public sealed class ZombieBiteGrab : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        public bool enabled = true;
        [Min(0.1f)]
        public float grabRange = 1.5f;
        [Range(10, 180)]
        public float grabAngle = 75f;
        [Min(0.2f)]
        public float contactDistance = 0.55f;
        [Min(0.05f)]
        public float approachDuration = 0.25f;
        [Min(0.1f)]
        public float biteDelay = 0.65f;
        [Min(0.2f)]
        public float holdDuration = 1.5f;
        [Range(0, 1)]
        public float shoulderIKWeight = 1;
        public Vector3 shoulderOffset = new Vector3(0, 0.03f, 0.04f);
        public bool ignorePairCollision = true;
        public bool logEvents = false;
    }

    public static ZombieBiteGrab Active { get; private set; }
    public static bool PlayerIsGrabbed => Active != null && Active.victim != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Active = null;
    }

    public PlayerHealth Victim => victim;
    public bool IsHolding => victim != null;

    Settings config;
    PlayerHealth victim;
    Animator enemyAnimator, victimAnimator;
    Rigidbody victimBody, enemyBody;
    RigidbodyConstraints oldConstraints;
    Vector3 origin, destination;
    Quaternion victimRotation;
    float started;
    int damage;
    bool bitten;
    readonly List<(Collider a, Collider b)> ignored = new List<(Collider, Collider)>();
    public bool TryBegin(PlayerHealth player, int attackDamage, Settings settings)
    {
        if (player == null || player.CurrentHP <= 0 || PlayerIsGrabbed || !settings.enabled || GameInputManager.Instance == null || !GameInputManager.Instance.Gameplay)
        {
            return false;
        }

        Vector3 delta = player.transform.position - transform.position;
        if (Mathf.Abs(delta.y) > 0.7f)
        {
            return false;
        }

        delta.y = 0;
        if (delta.magnitude > settings.grabRange || delta.sqrMagnitude < 0.001f || Vector3.Angle(transform.forward, delta) > settings.grabAngle)
        {
            return false;
        }

        Vector3 desired = player.transform.position - delta.normalized * settings.contactDistance;
        desired.y = transform.position.y;
        if (!ClearPath(transform.position, desired, player.transform))
        {
            return false;
        }

        // 掴みを壁越しに成立させない。
        if (!ClearPath(transform.position, player.transform.position, player.transform))
        {
            return false;
        }

        config = settings;
        victim = player;
        Active = this;
        damage = attackDamage;
        origin = transform.position;
        destination = desired;
        started = Time.time;
        bitten = false;
        enemyAnimator = GetComponent<Animator>();
        victimAnimator = player.GetComponent<Animator>();
        enemyBody = GetComponent<Rigidbody>();
        victimBody = player.GetComponent<Rigidbody>();
        if (victimBody != null)
        {
            oldConstraints = victimBody.constraints;
            if (!victimBody.isKinematic)
            {
                victimBody.velocity = Vector3.zero;
                victimBody.angularVelocity = Vector3.zero;
            }

            victimBody.constraints = RigidbodyConstraints.FreezeAll;
        }

        victimRotation = player.transform.rotation;
        player.transform.rotation = Quaternion.LookRotation(-delta.normalized);
        player.GetComponent<PlayerFlashlightController>()?.SetRaised(false);
        player.GetComponent<UnityChan.UnityChanControlScriptWithRgidBody>()?.CancelAimForDamage(true);
        if (config.ignorePairCollision)
        {
            foreach (var a in GetComponentsInChildren<Collider>())
            {
                foreach (var b in player.GetComponentsInChildren<Collider>())
                {
                    if (!a.isTrigger && !b.isTrigger && a.enabled && b.enabled && !Physics.GetIgnoreCollision(a, b))
                    {
                        Physics.IgnoreCollision(a, b, true);
                        ignored.Add((a, b));
                    }
                }
            }
        }

        if (config.logEvents)
        {
            Debug.Log("[Bite] 掴み成立・操作拘束", this);
        }

        return true;
    }

    bool ClearPath(Vector3 from, Vector3 to, Transform player)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        foreach (var hit in Physics.CapsuleCastAll(from + Vector3.up * 0.4f, from + Vector3.up * 1.3f, 0.25f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(player))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (!IsHolding)
        {
            return;
        }

        if (!victim.isActiveAndEnabled || victim.CurrentHP <= 0)
        {
            Release();
            return;
        }

        if (Time.timeScale == 0 || PauseMenuController.IsPaused)
        {
            return;
        }

        float elapsed = Time.time - started;
        float biteAt = Mathf.Max(config.biteDelay, config.approachDuration);
        if (!bitten && elapsed >= biteAt)
        {
            bitten = true;
            victim.TakeDamage(damage, true, transform.position);
            if (config.logEvents)
            {
                Debug.Log("[Bite] 噛みつきダメージ", this);
            }
        }

        if (elapsed >= Mathf.Max(config.holdDuration, biteAt + 0.25f))
        {
            Finish();
        }
    }

    // 物理演算の一定間隔で呼ばれます。以下の状態更新を行います。
    void FixedUpdate()
    {
        if (!IsHolding)
        {
            return;
        }

        float t = Mathf.Clamp01((Time.time - started) / Mathf.Max(0.05f, config.approachDuration));
        Vector3 next = Vector3.Lerp(origin, destination, Mathf.SmoothStep(0, 1, t));
        float end = Mathf.Max(config.holdDuration, Mathf.Max(config.biteDelay, config.approachDuration) + 0.25f);
        float retreat = Mathf.Clamp01((Time.time - started - (end - 0.25f)) / 0.25f);
        next = Vector3.Lerp(next, origin, Mathf.SmoothStep(0, 1, retreat));
        if (!ClearPath(transform.position, next, victim.transform))
        {
            Finish();
            return;
        }

        if (enemyBody != null)
        {
            if (!enemyBody.isKinematic)
            {
                enemyBody.velocity = Vector3.zero;
            }

            enemyBody.MovePosition(next);
            Vector3 direction = victim.transform.position - next;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
            {
                enemyBody.MoveRotation(Quaternion.LookRotation(direction));
            }
        }
        else
        {
            transform.position = next;
            Vector3 direction = victim.transform.position - next;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        if (!IsHolding || enemyAnimator == null || victimAnimator == null || !enemyAnimator.isHuman || !victimAnimator.isHuman)
        {
            return;
        }

        float elapsed = Time.time - started;
        float end = Mathf.Max(config.holdDuration, Mathf.Max(config.biteDelay, config.approachDuration) + 0.25f);
        float weight = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, config.approachDuration)) * Mathf.Clamp01((end - elapsed) / 0.15f) * config.shoulderIKWeight;
        Grip(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightUpperArm, weight);
        Grip(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.LeftUpperArm, weight);
    }

    void Grip(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, HumanBodyBones shoulderBone, float weight)
    {
        Transform upper = enemyAnimator.GetBoneTransform(upperBone), lower = enemyAnimator.GetBoneTransform(lowerBone), hand = enemyAnimator.GetBoneTransform(handBone), shoulder = victimAnimator.GetBoneTransform(shoulderBone);
        if (upper == null || lower == null || hand == null || shoulder == null)
        {
            return;
        }

        Vector3 target = shoulder.position + victim.transform.TransformVector(config.shoulderOffset);
        Quaternion u = upper.rotation, l = lower.rotation;
        for (int i = 0; i < 3; i++)
        {
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
            upper.rotation = Quaternion.FromToRotation(hand.position - upper.position, target - upper.position) * upper.rotation;
        }

        Quaternion solvedU = upper.rotation, solvedL = lower.rotation;
        upper.rotation = Quaternion.Slerp(u, solvedU, weight);
        lower.rotation = Quaternion.Slerp(l, solvedL, weight);
    }

    void Finish()
    {
        Release();
        GetComponent<EnemyAttack>()?.AnimationEvent_AttackFinished();
    }

    public void Release()
    {
        if (victim != null)
        {
            // 操作再開前に接近開始位置へ戻し、物理Colliderの重なりを解消。
            if (ClearPath(transform.position, origin, victim.transform))
            {
                if (enemyBody != null)
                {
                    enemyBody.position = origin;
                }
                else
                {
                    transform.position = origin;
                }
            }

            victim.transform.rotation = victimRotation;
            if (victimBody != null)
            {
                victimBody.constraints = oldConstraints;
            }
        }

        foreach (var pair in ignored)
        {
            if (pair.a != null && pair.b != null)
            {
                Physics.IgnoreCollision(pair.a, pair.b, false);
            }
        }

        ignored.Clear();
        if (Active == this)
        {
            Active = null;
        }

        victim = null;
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        Release();
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        Release();
    }
}
