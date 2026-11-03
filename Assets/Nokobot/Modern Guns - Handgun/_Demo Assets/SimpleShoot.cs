//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//[AddComponentMenu("Nokobot/Modern Guns/Simple Shoot")]
//public class SimpleShoot : MonoBehaviour
//{
//    [Header("Prefab Refrences")]
//    public GameObject bulletPrefab;
//    public GameObject casingPrefab;
//    public GameObject muzzleFlashPrefab;

//    [Header("Location Refrences")]
//    [SerializeField] private Animator gunAnimator;
//    [SerializeField] private Transform barrelLocation;
//    [SerializeField] private Transform casingExitLocation;

//    [Header("Settings")]
//    [Tooltip("Specify time to destory the casing object")] [SerializeField] private float destroyTimer = 2f;
//    [Tooltip("Bullet Speed")] [SerializeField] private float shotPower = 500f;
//    [Tooltip("Casing Ejection Speed")] [SerializeField] private float ejectPower = 150f;


//    void Start()
//    {
//        if (barrelLocation == null)
//            barrelLocation = transform;

//        if (gunAnimator == null)
//            gunAnimator = GetComponentInChildren<Animator>();
//    }

//    void Update()
//    {
//        //If you want a different input, change it here
//        if (Input.GetButtonDown("Fire1"))
//        {
//            //Calls animation on the gun that has the relevant animation events that will fire
//            gunAnimator.SetTrigger("Fire");
//        }
//    }


//    //This function creates the bullet behavior
//    void Shoot()
//    {
//        if (muzzleFlashPrefab)
//        {
//            //Create the muzzle flash
//            GameObject tempFlash;
//            tempFlash = Instantiate(muzzleFlashPrefab, barrelLocation.position, barrelLocation.rotation);

//            //Destroy the muzzle flash effect
//            Destroy(tempFlash, destroyTimer);
//        }

//        //cancels if there's no bullet prefeb
//        if (!bulletPrefab)
//        { return; }

//        // Create a bullet and add force on it in direction of the barrel
//        Instantiate(bulletPrefab, barrelLocation.position, barrelLocation.rotation).GetComponent<Rigidbody>().AddForce(barrelLocation.forward * shotPower);

//    }

//    //This function creates a casing at the ejection slot
//    void CasingRelease()
//    {
//        //Cancels function if ejection slot hasn't been set or there's no casing
//        if (!casingExitLocation || !casingPrefab)
//        { return; }

//        //Create the casing
//        GameObject tempCasing;
//        tempCasing = Instantiate(casingPrefab, casingExitLocation.position, casingExitLocation.rotation) as GameObject;
//        //Add force on casing to push it out
//        tempCasing.GetComponent<Rigidbody>().AddExplosionForce(Random.Range(ejectPower * 0.7f, ejectPower), (casingExitLocation.position - casingExitLocation.right * 0.3f - casingExitLocation.up * 0.6f), 1f);
//        //Add torque to make casing spin in random direction
//        tempCasing.GetComponent<Rigidbody>().AddTorque(new Vector3(0, Random.Range(100f, 500f), Random.Range(100f, 1000f)), ForceMode.Impulse);

//        //Destroy casing after X seconds
//        Destroy(tempCasing, destroyTimer);
//    }

//}
using UnityEngine;

[AddComponentMenu("Nokobot/Modern Guns/Simple Shoot")]
public class SimpleShoot : MonoBehaviour
{
    [Header("発射通知の参照先")]
    [SerializeField] private OnKeyPress_Throw shootingSystem;

    [Header("Prefab References")]
    public GameObject casingPrefab;
    public GameObject muzzleFlashPrefab;

    [Header("Location References")]
    [SerializeField] private Animator gunAnimator;
    [SerializeField] private Transform barrelLocation;
    [SerializeField] private Transform casingExitLocation;

    [Header("Settings")]
    [SerializeField, Min(0.1f)]
    private float destroyTimer = 2f;

    [SerializeField, Min(0.01f)]
    private float muzzleFlashLifetime = 0.15f;

    [SerializeField, Min(0f)]
    private float ejectPower = 150f;

    private OnKeyPress_Throw subscribedSystem;
    private bool pendingFlash;
    private bool pendingCasing;

    void Awake()
    {
        if (barrelLocation == null)
            barrelLocation = transform;

        if (gunAnimator == null)
            gunAnimator = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        ResetGunToIdle();
        subscribedSystem = shootingSystem;

        if (subscribedSystem != null)
            subscribedSystem.ShotFired += HandleShotFired;
    }

    private void ResetGunToIdle()
    {
        if (gunAnimator == null)
            gunAnimator = GetComponentInChildren<Animator>();

        if (gunAnimator == null)
            return;

        gunAnimator.ResetTrigger("Fire");
        int idleState = Animator.StringToHash("Base Layer.Idle");
        if (gunAnimator.HasState(0, idleState))
        {
            gunAnimator.Play(idleState, 0, 0f);
            // SetActive直後にもスライド等のTransformへIdle姿勢を即時反映する。
            gunAnimator.Update(0f);
        }
    }

    void OnDisable()
    {
        if (subscribedSystem != null)
            subscribedSystem.ShotFired -= HandleShotFired;

        subscribedSystem = null;
        if (gunAnimator != null) gunAnimator.ResetTrigger("Fire");
        pendingFlash = false;
        pendingCasing = false;
    }

    private void HandleShotFired()
    {
        if (gunAnimator == null)
            return;

        pendingFlash = true;
        pendingCasing = true;

        // 元のSimpleShootと同じアニメーション再生方法
        gunAnimator.SetTrigger("Fire");
    }

    // 元のAnimation Eventから呼ばれる
    void Shoot()
    {
        if (!pendingFlash)
            return;

        pendingFlash = false;

        if (muzzleFlashPrefab == null || barrelLocation == null)
            return;

        GameObject flash = Instantiate(
            muzzleFlashPrefab,
            barrelLocation.position,
            barrelLocation.rotation
        );

        Destroy(flash, muzzleFlashLifetime);

        // 命中判定・演出弾・煙はOnKeyPress_Throwが担当
    }

    // 元のAnimation Eventから呼ばれる
    void CasingRelease()
    {
        if (!pendingCasing)
            return;

        pendingCasing = false;

        if (casingExitLocation == null || casingPrefab == null)
            return;

        GameObject casing = Instantiate(
            casingPrefab,
            casingExitLocation.position,
            casingExitLocation.rotation
        );

        if (casing.GetComponent<CasingLandingSound>() == null)
            casing.AddComponent<CasingLandingSound>();

        if (casing.TryGetComponent(out Rigidbody body))
        {
            body.AddExplosionForce(
                Random.Range(ejectPower * 0.7f, ejectPower),
                casingExitLocation.position
                    - casingExitLocation.right * 0.3f
                    - casingExitLocation.up * 0.6f,
                1f
            );

            body.AddTorque(
                new Vector3(
                    0f,
                    Random.Range(100f, 500f),
                    Random.Range(100f, 1000f)
                ),
                ForceMode.Impulse
            );
        }

        // 薬莢を射撃レイの対象から除外
        foreach (Transform child in
                 casing.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = 2; // Ignore Raycast
        }

        Destroy(casing, destroyTimer);
    }
}
