// 【役割】弾の命中を判定し、ダメージ、着弾効果とオブジェクトの破棄を処理する。
using System.Collections;
using UnityEngine;

public class OnCollision_DestroyBoth : MonoBehaviour
{
    public string tagName = "Enemy";
    [Header("弾ダメージ")]
    [Tooltip("DeadShotDamageSettings内のWeapon ID")]
    public string weaponId = "handgun";
    public int baseDamage = 100;
    [Tooltip("通常ヘッドショットの倍率。1なら標準ゾンビは2発必要です。")]
    public float headShotMultiplier = 1.0f;
    [Tooltip("未指定ならResourcesの共通Damage Settingsを使用")]
    public GameDamageSettings damageSettings;
    [Header("敵への命中SE")]
    [SerializeField]
    private AudioClip[] enemyHitClips;
    [SerializeField, Range(0f, 1f)]
    private float enemyHitVolume = 1f;
    [SerializeField]
    private Vector2 enemyHitPitchRange = new Vector2(0.95f, 1.05f);
    [Header("地面への着弾")]
    [SerializeField]
    private LayerMask groundImpactLayers = ~0;
    [SerializeField]
    private GameObject concreteImpactPrefab;
    [SerializeField]
    private GameObject woodImpactPrefab;
    [SerializeField]
    private GameObject metalImpactPrefab;
    [SerializeField]
    private float concreteImpactLifetime = 3f;
    [SerializeField]
    private float concreteImpactSurfaceOffset = 0.01f;
    [SerializeField]
    private float concreteImpactScale = 1f;
    [SerializeField, Range(-1f, 1f)]
    private float minimumGroundNormalY = 0.35f;
    [SerializeField]
    private AudioClip[] groundImpactClips;
    [SerializeField, Range(0f, 1f)]
    private float groundImpactVolume = 1f;
    [SerializeField]
    private Vector2 groundImpactPitchRange = new Vector2(0.95f, 1.05f);
    private bool initializedAsTracer;
    private bool precisionReady;
    private int catalogPowerOverride = -1;
    private float focusedDamageMultiplier = 1f;
    private float focusedHeadshotMultiplier = 1f;
    public void InitializeAsHitscanTracer(bool didHit, RaycastHit hit, Vector3 destination, float moveSpeed, bool isPrecisionReady = false, string sourceWeaponId = null, int powerOverride = -1, float focusDamageMultiplier = 1f, float focusHeadshotMultiplier = 1f)
    {
        if (initializedAsTracer)
        {
            return;
        }

        initializedAsTracer = true;
        precisionReady = isPrecisionReady;
        if (!string.IsNullOrWhiteSpace(sourceWeaponId))
        {
            weaponId = sourceWeaponId;
        }

        catalogPowerOverride = powerOverride;
        focusedDamageMultiplier = Mathf.Max(0f, focusDamageMultiplier);
        focusedHeadshotMultiplier = Mathf.Max(0f, focusHeadshotMultiplier);
        DisablePhysicalCollision();
        if (didHit)
        {
            ProcessHitscanHit(hit);
        }

        StartCoroutine(MoveTracer(destination, moveSpeed));
    }

    private void DisablePhysicalCollision()
    {
        foreach (Collider targetCollider in GetComponentsInChildren<Collider>())
        {
            targetCollider.enabled = false;
        }

        Rigidbody targetRigidbody = GetComponent<Rigidbody>();
        if (targetRigidbody == null)
        {
            return;
        }

        targetRigidbody.velocity = Vector3.zero;
        targetRigidbody.angularVelocity = Vector3.zero;
        targetRigidbody.useGravity = false;
        targetRigidbody.isKinematic = true;
    }

    private bool ProcessHitscanHit(RaycastHit hit)
    {
        EnemyHealth enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null)
        {
            Debug.Log($"[Hitscan] 環境へ命中 Collider:{hit.collider.name}", hit.collider);
            PlayGroundImpact(hit);
            return false;
        }

        EnemyHealth.HitPart hitPart = enemyHealth.GetHitPart(hit.collider);
        if (hitPart == EnemyHealth.HitPart.Unregistered)
        {
            Debug.Log($"[Hitscan] 未登録Colliderのためダメージ対象外: " + $"{hit.collider.name}", hit.collider);
            return false;
        }

        string hitPartName = enemyHealth.GetHitPartDisplayName(hitPart);
        bool isWeakPointKill = SpecialGogglesController.IsEquippedGlobally && precisionReady && enemyHealth.WeakPoint != null && enemyHealth.WeakPoint.IsWeakPoint(hitPart, hit.collider);
        var settings = damageSettings != null ? damageSettings : GameDamageSettings.Load();
        int weaponDamage = settings != null ? settings.GetWeaponDamage(weaponId, hitPart, catalogPowerOverride) : catalogPowerOverride >= 0 ? catalogPowerOverride : baseDamage;
        int damage = isWeakPointKill ? (settings != null ? settings.GetWeakPointDamage(enemyHealth.maxHP) : Mathf.Max(1, enemyHealth.maxHP)) : weaponDamage;
        if (precisionReady && !isWeakPointKill)
        {
            if (hitPart == EnemyHealth.HitPart.Head)
            {
                // 既存の頭部倍率へ1.35を重ねず、基本威力×Focus×Headshotとして計算する。
                int focusedBaseDamage = settings != null ? settings.GetWeaponDamage(weaponId, EnemyHealth.HitPart.Unregistered, catalogPowerOverride) : catalogPowerOverride >= 0 ? catalogPowerOverride : baseDamage;
                damage = Mathf.Max(0, Mathf.RoundToInt(focusedBaseDamage * focusedDamageMultiplier * focusedHeadshotMultiplier));
            }
            else
            {
                damage = Mathf.Max(0, Mathf.RoundToInt(damage * focusedDamageMultiplier));
            }
        }

        Debug.Log($"[Hitscan命中] ゾンビ:{enemyHealth.name} / 部位:{hitPartName} / " + $"Collider:{hit.collider.name} / Damage:{damage}" + (isWeakPointKill ? " / DEAD SHOT成立" : string.Empty), hit.collider);
        enemyHealth.TakeDamage(damage, hitPart, hit.point, hit.normal, transform.forward, hit.collider.transform, isWeakPointKill);
        PlayClipAtPoint(enemyHitClips, hit.point, enemyHitVolume, enemyHitPitchRange);
        return true;
    }

    private void PlayGroundImpact(RaycastHit hit)
    {
        int hitLayerMask = 1 << hit.collider.gameObject.layer;
        if ((groundImpactLayers.value & hitLayerMask) == 0)
        {
            return;
        }

        GameObject selectedImpact = null;
        if (hit.collider.CompareTag("WOOD"))
        {
            selectedImpact = woodImpactPrefab;
        }
        else if (hit.collider.CompareTag("Metal"))
        {
            selectedImpact = metalImpactPrefab;
        }
        else
        {
            // 従来のコンクリート着弾は、上向きの地面だけに限定する。
            if (hit.normal.y < minimumGroundNormalY)
            {
                return;
            }

            selectedImpact = concreteImpactPrefab;
        }

        if (selectedImpact != null)
        {
            // WarFXのエフェクト正面（+Z）を命中面の法線へ合わせる。
            // 法線軸回りだけをランダム化するため、壁・斜面・天井でも傾かない。
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, hit.normal) * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
            GameObject impact = Instantiate(selectedImpact, hit.point + hit.normal * concreteImpactSurfaceOffset, rotation);
            impact.transform.localScale *= Mathf.Max(0.01f, concreteImpactScale);
            Destroy(impact, Mathf.Max(0.1f, concreteImpactLifetime));
        }

        PlayClipAtPoint(groundImpactClips, hit.point, groundImpactVolume, groundImpactPitchRange);
    }

    private static void PlayClipAtPoint(AudioClip[] clips, Vector3 position, float volume, Vector2 pitchRange)
    {
        if (clips == null || clips.Length == 0)
        {
            return;
        }

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
        {
            return;
        }

        GameObject audioObject = new GameObject("One Shot Impact SE");
        audioObject.transform.position = position;
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 25f;
        source.Play();
        Destroy(audioObject, clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch)) + 0.1f);
    }

    private IEnumerator MoveTracer(Vector3 destination, float moveSpeed)
    {
        float safeSpeed = Mathf.Max(0.01f, moveSpeed);
        while ((transform.position - destination).sqrMagnitude > 0.0001f)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, safeSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = destination;
        Destroy(gameObject);
    }
}
