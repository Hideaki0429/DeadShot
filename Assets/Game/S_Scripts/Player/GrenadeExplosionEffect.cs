// 【役割】手榴弾の爆発時に発生する視覚・音響効果を制御する。
using UnityEngine;
using System.Collections.Generic;

// 投げた弾体が所有するタイマー。投擲アニメーションの終了や中断には影響されない。
public sealed class GrenadeExplosionEffect : MonoBehaviour
{
    GameObject prefab;
    float remaining, scale, lifetime;
    bool initialized, exploded;
    AudioSource rollingSound;
    Rigidbody body;
    SphereCollider damageArea;
    int damage;
    float nextContactSoundTime;
    const float MinimumRollSpeed = .15f;
    // 物理的に衝突した相手と接触情報を受け取り、以下の衝突処理を行います。
    void OnCollisionEnter(Collision collision)
    {
        if (IsEnemy(collision.collider))
        {
            Explode();
            return;
        }

        PlayContactSound(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (body != null && body.velocity.sqrMagnitude > MinimumRollSpeed * MinimumRollSpeed)
        {
            PlayContactSound(collision);
        }
    }

    void PlayContactSound(Collision collision)
    {
        if (exploded || !initialized || Time.timeScale == 0 || PauseMenuController.IsPaused || Time.time < nextContactSoundTime || (rollingSound != null && rollingSound.isPlaying))
        {
            return;
        }

        // 地面向きの接触のみ。壁に擦れて毎フレーム鳴ることを防ぐ。
        bool grounded = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Vector3.Dot(collision.GetContact(i).normal, Vector3.up) > .35f)
            {
                grounded = true;
                break;
            }
        }

        if (!grounded)
        {
            return;
        }

        rollingSound = GameSoundService.Play(GameSound.GrenadeRoll, transform.position);
        nextContactSoundTime = Time.time + .35f;
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (rollingSound != null)
        {
            rollingSound.Stop();
        }
    }

    public void Initialize(GameObject effect, float delay, float effectScale, float effectLifetime, SphereCollider area, int explosionDamage)
    {
        prefab = effect;
        remaining = delay;
        scale = Mathf.Max(.01f, effectScale);
        lifetime = Mathf.Max(.1f, effectLifetime);
        damageArea = area;
        damage = Mathf.Max(0, explosionDamage);
        initialized = true;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (!initialized || exploded || PauseMenuController.IsPaused)
        {
            return;
        }

        if (rollingSound != null)
        {
            rollingSound.transform.position = transform.position;
        }

        remaining -= Time.deltaTime;
        if (remaining > 0)
        {
            return;
        }

        Explode();
    }

    bool IsEnemy(Collider target)
    {
        if (target == null)
        {
            return false;
        }

        var health = target.GetComponentInParent<EnemyHealth>();
        return health != null && (target.CompareTag("Enemy") || health.CompareTag("Enemy"));
    }

    void Explode()
    {
        if (exploded)
        {
            return;
        }

        exploded = true;
        if (rollingSound != null)
        {
            rollingSound.Stop();
        }

        // 共通サウンドサービスが所有するため、弾体消去後も最後まで再生される。
        GameSoundService.Play(GameSound.GrenadeExplosion, transform.position);
        ApplyExplosionDamage();
        if (prefab != null)
        {
            // 地面用の煙なので、弾体の回転を引き継がず上向きに生成。
            var effect = Instantiate(prefab, transform.position, Quaternion.identity);
            effect.transform.localScale *= scale;
            foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.loop = false;
            }

            effect.SetActive(true);
            foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>())
            {
                particles.Play();
            }

            Destroy(effect, lifetime);
        }

        Destroy(gameObject);
    }

    void ApplyExplosionDamage()
    {
        if (damageArea == null || damage <= 0)
        {
            return;
        }

        damageArea.enabled = true;
        float radius = damageArea.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z));
        Vector3 center = damageArea.transform.TransformPoint(damageArea.center);
        var hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        var damaged = new HashSet<EnemyHealth>();
        foreach (var hit in hits)
        {
            var health = hit != null ? hit.GetComponentInParent<EnemyHealth>() : null;
            if (health == null || damaged.Contains(health))
            {
                continue;
            }

            if (!hit.CompareTag("Enemy") && !health.CompareTag("Enemy"))
            {
                continue;
            }

            damaged.Add(health);
            health.TakeGrenadeDamage(damage, center);
        }

        damageArea.enabled = false;
    }
}
