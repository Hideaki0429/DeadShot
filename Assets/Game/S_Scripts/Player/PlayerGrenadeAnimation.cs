// 【役割】手榴弾の構え、投距離、予測軌跡と投擲アニメーションを制御する。
using UnityEngine;

public sealed class PlayerGrenadeAnimation : MonoBehaviour
{
    public AnimationClip stopClip, walkClip;
    [Header("インベントリ連携")]
    public PlayerInventory inventory;
    public string grenadeItemId = "m1916";
    PlayerInventory consumingInventory;
    string consumingItemId;
    [Header("グレネード投擲")]
    [Tooltip("プレイヤーの子に配置済みのモデルのルート。位置・回転・サイズは変更しません。")]
    public Transform grenadeModel;
    [Tooltip("投げる瞬間のプレイヤーの向きを基準にした初速度（m/s）。X＝右、Y＝上、Z＝前。負の値で逆方向。")]
    public Vector3 initialThrowVelocity = new Vector3(0, 3, 8);
    [Header("構え投擲・予測軌跡")]
    public WeaponSelectionUI weaponSelection;
    public string grenadeWeaponId = "grenade";
    [Min(.1f)]
    public float minDistanceMultiplier = .45f;
    [Min(.1f)]
    public float maxDistanceMultiplier = 1.8f;
    [Min(.01f)]
    public float distanceWheelStep = .12f;
    [SerializeField]
    float distanceMultiplier = 1f;
    [Min(4)]
    public int trajectoryPoints = 32;
    [Min(.01f)]
    public float trajectoryTimeStep = .075f;
    public Color trajectoryColor = new Color(1f, 1f, 1f, .85f);
    [Min(.001f)]
    public float trajectoryWidth = .025f;
    public LineRenderer trajectoryLine;
    [Header("軌跡の落下地点ライト")]
    [Tooltip("予測軌跡の先端にPoint Lightを表示します。")]
    public bool showTrajectoryLandingLight = true;
    [Tooltip("空欄でも実行時に自動生成します。Hierarchyから編集したい場合はLightを登録できます。")]
    public Light trajectoryLandingLight;
    [ColorUsage(true, true)]
    public Color landingLightColor = new Color(1f, .18f, .04f, 1f);
    [Min(0f)]
    public float landingLightIntensity = 5f;
    [Min(.01f)]
    public float landingLightRange = 2.5f;
    public Vector3 landingLightPositionOffset = new Vector3(0f, .12f, 0f);
    public LightShadows landingLightShadows = LightShadows.None;
    [Header("手榴弾構え中の前後移動")]
    [Tooltip("手榴弾を構えて前進する移動速度")]
    [Min(0f)]
    public float forwardMovementSpeed = 1.6f;
    [Tooltip("手榴弾を構えて後退する移動速度")]
    [Min(0f)]
    public float backwardMovementSpeed = 1.2f;
    [Tooltip("前進中の下半身アニメーション再生速度")]
    [Min(.01f)]
    public float forwardAnimationSpeed = .75f;
    [Tooltip("後退中の下半身アニメーション再生速度")]
    [Min(.01f)]
    public float backwardAnimationSpeed = .75f;
    [Min(0)]
    public int standingAimHoldFrame = 24;
    [Min(0)]
    public int movingAimHoldFrame = 28;
    [SerializeField, HideInInspector]
    private float throwSpeed = 8f;
    [SerializeField, HideInInspector]
    private float upwardSpeed = 3f;
    [SerializeField, HideInInspector]
    private bool velocitySettingsMigrated;
    [Range(0, 1)]
    public float inheritMovement = 1f;
    [Min(.01f)]
    public float grenadeMass = .4f;
    [Min(.001f)]
    public float grenadeColliderRadius = .055f;
    [Tooltip("投擲方向を基準とした回転速度（度/秒）")]
    public Vector3 throwAngularVelocity = new Vector3(360, 120, 0);
    [Min(.1f)]
    public float thrownLifetime = 15f;
    public PhysicMaterial grenadePhysicsMaterial;
    [Header("爆発エフェクト（ダメージなし）")]
    public GameObject explosionPrefab;
    [Min(.1f)]
    public float explosionDelay = 3f;
    [Min(.01f)]
    public float explosionScale = 1f;
    [Min(.1f)]
    public float explosionEffectLifetime = 10f;
    [Min(.1f)]
    public float explosionDamageRadius = 5f;
    [Header("爆発範囲Gizmos（再生中）")]
    public bool showExplosionDamageGizmo = true;
    public Color explosionDamageGizmoColor = new Color(1f, .15f, .05f, .9f);
    [Tooltip("未指定なら共通Damage Settingsを使用")]
    public GameDamageSettings damageSettings;
    [Min(0), Tooltip("共通設定が見つからない場合だけ使用")]
    public int fallbackExplosionDamage = 200;
    [Header("ピン抜き（初期のモデル配置は変更しません）")]
    public Transform grenadePin, grenadeRing;
    [Min(0)]
    public int standingPinFrame = 30, movingPinFrame = 32;
    [Min(.1f)]
    public float pinLifetime = 4f;
    [Tooltip("ピン抜き前後に頭を下げる角度。Humanoidの頭のローカル軸で調整")]
    public Vector3 headRotationOffset = new Vector3(18, 0, 0);
    [Min(.01f)]
    public float headBlendIn = .3f, headBlendOut = .25f;
    bool pinReleased;
    float poseSeconds;
    AnimationClip playingClip;
    Transform head;
    [Header("手から離すタイミング（アニメーション全体に対する割合）")]
    [Tooltip("停止中の投擲。小さくするほど早く離します。0.5＝アニメーションの50％地点。")]
    [Range(0, 1)]
    public float standingReleaseNormalizedTime = .65f;
    [UnityEngine.Serialization.FormerlySerializedAs("releaseNormalizedTime")]
    [Tooltip("移動中の投擲。以前の共通タイミング設定を引き継ぎます。")]
    [Range(0, 1)]
    public float movingReleaseNormalizedTime = .65f;
    [Min(0)]
    public float movingThreshold = .05f;
    public bool IsThrowing { get; private set; }

    static PlayerGrenadeAnimation active;
    public static bool Busy => active != null && active.IsThrowing;
    // 停止中から開始した場合もwalkgrenadeを使うため、投擲中の移動は禁止しない。
    public static bool BlockMovement => false;

    public static float CurrentMovementSpeed(float horizontal, float vertical, float handgunStrafeSpeed)
    {
        if (active == null)
        {
            return 0f;
        }

        // A/Dはハンドガン構え移動と同じ速度をそのまま使う。
        if (Mathf.Abs(horizontal) > Mathf.Abs(vertical))
        {
            return handgunStrafeSpeed;
        }

        return vertical < -.1f ? active.backwardMovementSpeed : active.forwardMovementSpeed;
    }

    Animator animator;
    PlayerHealth health;
    bool moving, released, rootMotion;
    int layer = -1;
    bool enteredThrow;
    bool releaseRequested;
    int startFrame;
    Transform activeGrenadeProjectile;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        active = null;
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        MigrateVelocitySettings();
        animator = GetComponent<Animator>();
        health = GetComponent<PlayerHealth>();
        if (weaponSelection == null)
        {
            weaponSelection = FindObjectOfType<WeaponSelectionUI>(true);
        }

        EnsureTrajectoryLine();
        ResolvePinReferences();
        SetHeldGrenadeVisible(false);
    }

    bool GrenadeSelected => weaponSelection != null && weaponSelection.SelectedWeaponId == grenadeWeaponId;

    void EnsureTrajectoryLine()
    {
        if (trajectoryLine == null)
        {
            var go = new GameObject("Grenade Trajectory");
            go.transform.SetParent(transform, false);
            trajectoryLine = go.AddComponent<LineRenderer>();
            trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
            trajectoryLine.useWorldSpace = true;
            trajectoryLine.textureMode = LineTextureMode.Stretch;
        }

        trajectoryLine.startWidth = trajectoryLine.endWidth = trajectoryWidth;
        trajectoryLine.startColor = trajectoryLine.endColor = trajectoryColor;
        trajectoryLine.enabled = false;
    }

    void SetHeldGrenadeVisible(bool visible)
    {
        if (grenadeModel != null)
        {
            grenadeModel.gameObject.SetActive(visible);
        }

        if (grenadePin != null)
        {
            grenadePin.gameObject.SetActive(visible);
        }

        if (grenadeRing != null)
        {
            grenadeRing.gameObject.SetActive(visible);
        }
    }

    // Inspectorの値の変更・読み込み時に、設定の検証や反映を行います。
    void OnValidate()
    {
        MigrateVelocitySettings();
        ApplyLandingLightSettings();
        if (trajectoryLine != null)
        {
            trajectoryLine.startWidth = trajectoryLine.endWidth = trajectoryWidth;
            trajectoryLine.startColor = trajectoryLine.endColor = trajectoryColor;
        }
    }

    void EnsureLandingPointLight()
    {
        if (trajectoryLandingLight == null)
        {
            var lightObject = new GameObject("Grenade Landing Point Light");
            lightObject.transform.SetParent(transform, false);
            trajectoryLandingLight = lightObject.AddComponent<Light>();
            trajectoryLandingLight.type = LightType.Point;
        }

        ApplyLandingLightSettings();
    }

    void ApplyLandingLightSettings()
    {
        if (trajectoryLandingLight == null)
        {
            return;
        }

        trajectoryLandingLight.type = LightType.Point;
        trajectoryLandingLight.color = landingLightColor;
        trajectoryLandingLight.intensity = Mathf.Max(0f, landingLightIntensity);
        trajectoryLandingLight.range = Mathf.Max(.01f, landingLightRange);
        trajectoryLandingLight.shadows = landingLightShadows;
    }

    void SetLandingPointLight(bool visible, Vector3 landingPoint = default)
    {
        if (visible && showTrajectoryLandingLight)
        {
            EnsureLandingPointLight();
            trajectoryLandingLight.transform.position = landingPoint + landingLightPositionOffset;
            trajectoryLandingLight.enabled = true;
        }
        else if (trajectoryLandingLight != null)
        {
            trajectoryLandingLight.enabled = false;
        }
    }

    void MigrateVelocitySettings()
    {
        if (velocitySettingsMigrated)
        {
            return;
        }

        initialThrowVelocity = new Vector3(0, upwardSpeed, Mathf.Max(0, throwSpeed));
        velocitySettingsMigrated = true;
    }

    public void ResolvePinReferences()
    {
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (grenadeModel == null && (child.name == "WPN_MK2Grenade" || child.name.StartsWith("WPN_MK2Grenade (")))
            {
                grenadeModel = child;
            }

            if (grenadePin == null && child.name == "WPN_MK2Grenade_pin")
            {
                grenadePin = child;
            }

            if (grenadeRing == null && child.name == "WPN_MK2Grenade_ring")
            {
                grenadeRing = child;
            }
        }
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        if (!IsThrowing || playingClip == null || Time.timeScale == 0 || PauseMenuController.IsPaused)
        {
            return;
        }

        var state = animator.GetCurrentAnimatorStateInfo(layer);
        if (animator.IsInTransition(layer))
        {
            var next = animator.GetNextAnimatorStateInfo(layer);
            if (next.IsName("Throw"))
            {
                state = next;
            }
        }

        if (!state.IsName("Throw"))
        {
            return;
        }

        poseSeconds = state.normalizedTime * playingClip.length;
        if (!releaseRequested)
        {
            int holdFrame = moving ? movingAimHoldFrame : standingAimHoldFrame;
            float holdNormalized = Mathf.Clamp01((holdFrame / Mathf.Max(1f, playingClip.frameRate)) / Mathf.Max(.01f, playingClip.length));
            if (state.normalizedTime >= holdNormalized)
            {
                animator.Play("Throw", layer, holdNormalized);
                ShowTrajectory();
                return;
            }
        }

        float releaseTime = (moving ? movingPinFrame : standingPinFrame) / Mathf.Max(1, playingClip.frameRate);
        float delta = poseSeconds - releaseTime;
        float weight = delta < 0 ? Mathf.Clamp01(1 + delta / Mathf.Max(.01f, headBlendIn)) : Mathf.Clamp01(1 - delta / Mathf.Max(.01f, headBlendOut));
        weight = Mathf.SmoothStep(0, 1, weight) * animator.GetLayerWeight(layer);
        if (head != null)
        {
            head.localRotation = head.localRotation * Quaternion.Euler(headRotationOffset * weight);
        }

        // Animator評価後のワールド座標から複製するため、落下開始位置が前フレームにずれない。
        if (!pinReleased && poseSeconds >= releaseTime)
        {
            ReleasePin();
        }

        float throwReleaseTime = moving ? movingReleaseNormalizedTime : standingReleaseNormalizedTime;
        if (releaseRequested && !released && state.normalizedTime >= throwReleaseTime)
        {
            ReleaseGrenade();
        }
    }

    void ReleaseGrenade()
    {
        released = true;
        if (grenadeModel == null)
        {
            Debug.LogWarning("[Grenade] Grenade Modelに手元のモデルを登録してください。", this);
            return;
        }

        // スクリプトや取得用Triggerを複製せず、表示中のメッシュだけを物理弾体にする。
        var projectile = new GameObject("Thrown Grenade");
        activeGrenadeProjectile = projectile.transform;
        projectile.AddComponent<CameraIgnoredDebris>();
        projectile.transform.SetPositionAndRotation(grenadeModel.position, grenadeModel.rotation);
        bool hasBounds = false;
        Bounds bounds = new Bounds(grenadeModel.position, Vector3.zero);
        foreach (var mesh in grenadeModel.GetComponentsInChildren<MeshFilter>(true))
        {
            if ((grenadePin != null && mesh.transform.IsChildOf(grenadePin)) || (grenadeRing != null && mesh.transform.IsChildOf(grenadeRing)))
            {
                continue;
            }

            var source = mesh.GetComponent<MeshRenderer>();
            if (source == null || !source.enabled || mesh.sharedMesh == null)
            {
                continue;
            }

            var visual = new GameObject(mesh.name);
            visual.transform.SetPositionAndRotation(mesh.transform.position, mesh.transform.rotation);
            visual.transform.localScale = mesh.transform.lossyScale;
            visual.transform.SetParent(projectile.transform, true);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Destroy(projectile);
            Debug.LogWarning("[Grenade] モデル内に投擲用のMeshRendererがありません。", this);
            return;
        }

        // 弾体が生成できることを確認した後で、一度だけ所持数を消費する。
        if (consumingInventory == null || !consumingInventory.RemoveItem(consumingItemId, 1))
        {
            Destroy(projectile);
            Cancel();
            return;
        }

        var collision = projectile.AddComponent<SphereCollider>();
        collision.center = projectile.transform.InverseTransformPoint(bounds.center);
        collision.radius = Mathf.Max(.001f, grenadeColliderRadius);
        collision.sharedMaterial = grenadePhysicsMaterial;
        foreach (var own in GetComponentsInChildren<Collider>(true))
        {
            Physics.IgnoreCollision(collision, own);
        }

        var body = projectile.AddComponent<Rigidbody>();
        body.mass = Mathf.Max(.01f, grenadeMass);
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // 手やモデルのアニメーション回転ではなく、Playerルートの水平な正面を使う。
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < .0001f)
        {
            forward = Vector3.forward;
        }

        Quaternion direction = Quaternion.LookRotation(forward.normalized, Vector3.up);
        var playerBody = GetComponent<Rigidbody>();
        Vector3 adjustedVelocity = new Vector3(initialThrowVelocity.x * distanceMultiplier, initialThrowVelocity.y, initialThrowVelocity.z * distanceMultiplier);
        body.velocity = direction * adjustedVelocity + (playerBody != null ? playerBody.velocity * Mathf.Clamp01(inheritMovement) : Vector3.zero);
        body.angularVelocity = (direction * throwAngularVelocity) * Mathf.Deg2Rad;
        body.maxAngularVelocity = Mathf.Max(7, body.angularVelocity.magnitude);
        SetHeldGrenadeVisible(false);
        var damageArea = projectile.AddComponent<SphereCollider>();
        damageArea.isTrigger = true;
        damageArea.radius = Mathf.Max(.1f, explosionDamageRadius);
        damageArea.enabled = false;
        var settings = damageSettings != null ? damageSettings : GameDamageSettings.Load();
        int damage = settings != null ? settings.GetGrenadeDamage() : fallbackExplosionDamage;
        projectile.AddComponent<GrenadeExplosionEffect>().Initialize(explosionPrefab, Mathf.Min(Mathf.Max(.1f, explosionDelay), Mathf.Max(.1f, thrownLifetime)), explosionScale, explosionEffectLifetime, damageArea, damage);
    }

    private void OnDrawGizmos()
    {
        if (!showExplosionDamageGizmo || !Application.isPlaying || activeGrenadeProjectile == null)
        {
            return;
        }

        Gizmos.color = explosionDamageGizmoColor;
        Gizmos.DrawWireSphere(activeGrenadeProjectile.position, Mathf.Max(0.1f, explosionDamageRadius));
    }

    void ReleasePin()
    {
        pinReleased = true;
        GameSoundService.Play(GameSound.GrenadePinPull, grenadeModel != null ? grenadeModel.position : transform.position);
        if (grenadePin == null && grenadeRing == null)
        {
            return;
        }

        var anchor = grenadePin != null ? grenadePin : grenadeRing;
        var dropped = new GameObject("Grenade Pin and Ring (Dropped)");
        dropped.AddComponent<CameraIgnoredDebris>();
        dropped.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        foreach (var part in new[]
        {
            grenadePin,
            grenadeRing
        }

        )
        {
            if (part == null || !part.gameObject.activeInHierarchy)
            {
                continue;
            }

            // 描画のみをコピーし、元プレハブの取得処理やコライダーは持ち込まない。
            foreach (var mesh in part.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = mesh.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    continue;
                }

                var visual = new GameObject(mesh.name);
                visual.transform.SetPositionAndRotation(mesh.transform.position, mesh.transform.rotation);
                visual.transform.localScale = mesh.transform.lossyScale;
                visual.transform.SetParent(dropped.transform, true);
                visual.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                visual.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
        }

        if (grenadePin != null)
        {
            grenadePin.gameObject.SetActive(false);
        }

        if (grenadeRing != null)
        {
            grenadeRing.gameObject.SetActive(false);
        }

        var collider = dropped.AddComponent<SphereCollider>();
        collider.radius = .015f;
        foreach (var ownCollider in GetComponentsInChildren<Collider>(true))
        {
            Physics.IgnoreCollision(collider, ownCollider);
        }

        var rb = dropped.AddComponent<Rigidbody>();
        rb.mass = .02f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.angularVelocity = Random.insideUnitSphere * 3f;
        Destroy(dropped, Mathf.Max(.1f, pinLifetime));
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
        var input = GameInputManager.Instance;
        if (input == null || animator == null)
        {
            return;
        }

        if (weaponSelection == null)
        {
            weaponSelection = FindObjectOfType<WeaponSelectionUI>(true);
        }

        // 武器選択側がプレハブの外側を有効にしていても、こちらで管理している
        // モデル・ピン・リングが非表示のままだと見えないため、非投擲時は選択状態へ同期する。
        if (!IsThrowing)
        {
            SetHeldGrenadeVisible(GrenadeSelected);
        }

        if (!IsThrowing && GrenadeSelected && input.AimPressed)
        {
            TryThrow();
        }

        if (!IsThrowing)
        {
            return;
        }

        animator.SetBool("GrenadeAim", true);
        animator.SetFloat("GrenadeForwardAnimSpeed", Mathf.Max(.01f, forwardAnimationSpeed));
        animator.SetFloat("GrenadeBackwardAnimSpeed", Mathf.Max(.01f, backwardAnimationSpeed));
        // 投擲確定（左クリック）前なら、右クリックをもう一度押して構えを解除できる。
        if (!releaseRequested && Time.frameCount > startFrame && input.GrenadeCancelPressed)
        {
            Cancel();
            Debug.Log("[Grenade] 投げ構えを解除しました。", this);
            return;
        }

        if (health == null || health.CurrentHP <= 0 || ZombieBiteGrab.PlayerIsGrabbed)
        {
            Cancel();
            return;
        }

        if (Time.timeScale == 0 || PauseMenuController.IsPaused)
        {
            return;
        }

        if (!releaseRequested)
        {
            float wheel = input.GrenadeDistanceInput;
            if (Mathf.Abs(wheel) > .001f)
            {
                distanceMultiplier = Mathf.Clamp(distanceMultiplier + Mathf.Sign(wheel) * distanceWheelStep, minDistanceMultiplier, maxDistanceMultiplier);
            }

            if (input.GrenadeReleasePressed)
            {
                releaseRequested = true;
                if (trajectoryLine != null)
                {
                    trajectoryLine.enabled = false;
                }

                SetLandingPointLight(false);
                int holdFrame = moving ? movingAimHoldFrame : standingAimHoldFrame;
                float holdNormalized = Mathf.Clamp01((holdFrame / Mathf.Max(1f, playingClip.frameRate)) / Mathf.Max(.01f, playingClip.length));
                animator.Play("Throw", layer, holdNormalized);
            }
        }

        var state = animator.GetCurrentAnimatorStateInfo(layer);
        bool transition = animator.IsInTransition(layer);
        var next = transition ? animator.GetNextAnimatorStateInfo(layer) : default(AnimatorStateInfo);
        bool throwing = state.IsName("Throw");
        bool entering = transition && next.IsName("Throw");
        enteredThrow |= throwing || entering;
        // レイヤーの重みはAnimatorのTransition進行率に追従。
        // ブレンド秒数と再生速度はAnimatorだけで設定する。
        float progress = transition ? Mathf.Clamp01(animator.GetAnimatorTransitionInfo(layer).normalizedTime) : 0;
        float weight = entering ? progress : throwing ? (transition ? 1 - progress : 1) : 0;
        animator.SetLayerWeight(layer, weight);
        if (enteredThrow && !throwing && !transition && Time.frameCount > startFrame)
        {
            Cancel();
        }
    }

    public void TryThrow()
    {
        if (Busy || PlayerHealing.Busy || animator == null || health == null || health.CurrentHP <= 0 || GameInputManager.Instance == null || !GameInputManager.Instance.Gameplay)
        {
            return;
        }

        var gun = GetComponent<OnKeyPress_Throw>();
        if (gun != null && gun.isReloading)
        {
            return;
        }

        if (animator.GetBool("Jump") || animator.GetCurrentAnimatorStateInfo(0).IsTag("PlayerHit") || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("PlayerHit")))
        {
            return;
        }

        // 開始時に停止していても常に歩行用の投擲モーションを使用する。
        // Base Layerの待機／歩行に、Grenade Movingの上半身を重ねる。
        moving = true;
        var clip = walkClip;
        layer = animator.GetLayerIndex("Grenade Moving");
        if (clip == null || layer < 0)
        {
            return;
        }

        ResolvePinReferences();
        if (grenadeModel == null)
        {
            Debug.LogWarning("[Grenade] 手元のモデルが未登録です。", this);
            return;
        }

        if (inventory == null)
        {
            inventory = FindObjectOfType<PlayerInventory>(true);
        }

        if (inventory == null || inventory.Find(grenadeItemId) == null || inventory.Count(grenadeItemId) < 1)
        {
            Debug.Log("[Grenade] 手榴弾を所持していません。", this);
            return;
        }

        consumingInventory = inventory;
        consumingItemId = grenadeItemId;
        ResolvePinReferences();
        playingClip = clip;
        pinReleased = false;
        poseSeconds = 0;
        SetHeldGrenadeVisible(true);
        head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        active = this;
        IsThrowing = true;
        released = false;
        releaseRequested = false;
        enteredThrow = false;
        startFrame = Time.frameCount;
        animator.SetBool("GrenadeAim", true);
        animator.SetFloat("GrenadeForwardAnimSpeed", Mathf.Max(.01f, forwardAnimationSpeed));
        animator.SetFloat("GrenadeBackwardAnimSpeed", Mathf.Max(.01f, backwardAnimationSpeed));
        rootMotion = animator.applyRootMotion;
        animator.applyRootMotion = false;
        animator.SetTrigger("ThrowGrenadeMoving");
    }

    public void Cancel()
    {
        if (IsThrowing && animator != null)
        {
            animator.applyRootMotion = rootMotion;
            animator.ResetTrigger("ThrowGrenadeMoving");
            animator.ResetTrigger("ThrowGrenadeStanding");
            if (layer >= 0)
            {
                animator.SetLayerWeight(layer, 0);
                animator.Play(animator.GetLayerName(layer) + ".Ready", layer, 0);
            }
        }

        if (animator != null)
        {
            animator.SetBool("GrenadeAim", false);
        }

        SetHeldGrenadeVisible(false);
        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = false;
        }

        SetLandingPointLight(false);
        consumingInventory = null;
        consumingItemId = null;
        IsThrowing = false;
        if (active == this)
        {
            active = null;
        }
    }

    void ShowTrajectory()
    {
        EnsureTrajectoryLine();
        Vector3 start = grenadeModel != null ? grenadeModel.position : transform.position + Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .001f)
        {
            forward = Vector3.forward;
        }

        Quaternion direction = Quaternion.LookRotation(forward, Vector3.up);
        Vector3 local = new Vector3(initialThrowVelocity.x * distanceMultiplier, initialThrowVelocity.y, initialThrowVelocity.z * distanceMultiplier);
        Vector3 velocity = direction * local;
        trajectoryLine.positionCount = trajectoryPoints;
        Vector3 previous = start;
        Vector3 landingPoint = start;
        for (int i = 0; i < trajectoryPoints; i++)
        {
            float t = i * trajectoryTimeStep;
            Vector3 point = start + velocity * t + .5f * Physics.gravity * t * t;
            if (i > 0 && Physics.Linecast(previous, point, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                landingPoint = point;
                trajectoryLine.positionCount = i + 1;
                trajectoryLine.SetPosition(i, point);
                break;
            }

            trajectoryLine.SetPosition(i, point);
            previous = point;
            landingPoint = point;
        }

        trajectoryLine.enabled = true;
        SetLandingPointLight(true, landingPoint);
    }
}
