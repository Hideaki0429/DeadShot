// 【役割】M1911の表示と射撃に伴うモデル側の演出を管理する。
using UnityEngine;

// Receives the original asset's animation events; never handles input, damage or ammunition.
public sealed class M1911Presentation : MonoBehaviour
{
    public Animator gunAnimator;
    public Transform muzzlePoint, casingExit;
    public GameObject muzzleFlashPrefab, casingPrefab;
    [Min(.1f)]
    public float casingLifetime = 2f;
    [Min(.01f)]
    public float flashLifetime = .15f;
    [Min(0)]
    public float ejectPower = 250f;
    private bool pendingFlash, pendingCasing;
    public int FlashCount { get; private set; }
    public int CasingCount { get; private set; }

    private static readonly int FireState = Animator.StringToHash("Base Layer.Fire");
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    // コンポーネントが有効になったときの処理です。
    void OnEnable()
    {
        if (gunAnimator == null)
        {
            gunAnimator = GetComponentInChildren<Animator>();
        }

        if (gunAnimator == null)
        {
            return;
        }

        gunAnimator.ResetTrigger("Fire");
        if (gunAnimator.HasState(0, IdleState))
        {
            gunAnimator.Play(IdleState, 0, 0f);
            gunAnimator.Update(0f);
        }
    }

    public void PlayShot()
    {
        if (gunAnimator == null)
        {
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            var player = GetComponentInParent<UnityChan.UnityChanControlScriptWithRgidBody>(true);
            if (player != null && player.gameObject.activeInHierarchy && player.Rifle != null)
            {
                player.Rifle.SetActive(true);
            }
        }

        if (!isActiveAndEnabled || !gunAnimator.isActiveAndEnabled)
        {
            return;
        }

        pendingFlash = pendingCasing = true;
        gunAnimator.ResetTrigger("Fire");
        if (gunAnimator.HasState(0, FireState))
        {
            gunAnimator.Play(FireState, 0, 0f);
        }
        else
        {
            gunAnimator.SetTrigger("Fire");
        }
    }

    public void Shoot()
    {
        if (!pendingFlash)
        {
            return;
        }

        pendingFlash = false;
        if (muzzleFlashPrefab == null || muzzlePoint == null)
        {
            return;
        }

        Destroy(Instantiate(muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation), flashLifetime);
        FlashCount++;
    }

    public void CasingRelease()
    {
        if (!pendingCasing)
        {
            return;
        }

        pendingCasing = false;
        if (casingExit == null || casingPrefab == null)
        {
            return;
        }

        var casing = Instantiate(casingPrefab, casingExit.position, casingExit.rotation);
        if (casing.GetComponent<CasingLandingSound>() == null)
        {
            casing.AddComponent<CasingLandingSound>();
        }

        var body = casing.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.AddExplosionForce(Random.Range(ejectPower * .7f, ejectPower), casingExit.position - casingExit.right * .3f - casingExit.up * .6f, 1f);
            body.AddTorque(new Vector3(0, Random.Range(100f, 500f), Random.Range(100f, 1000f)), ForceMode.Impulse);
        }

        foreach (var t in casing.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = 2;
        }

        var player = GetComponentInParent<UnityChan.UnityChanControlScriptWithRgidBody>();
        if (player != null)
        {
            foreach (var c in casing.GetComponentsInChildren<Collider>())
            {
                foreach (var p in player.GetComponentsInChildren<Collider>(true))
                {
                    Physics.IgnoreCollision(c, p);
                }
            }
        }

        Destroy(casing, casingLifetime);
        CasingCount++;
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        pendingFlash = pendingCasing = false;
        if (gunAnimator != null)
        {
            gunAnimator.ResetTrigger("Fire");
        }
    }
}
