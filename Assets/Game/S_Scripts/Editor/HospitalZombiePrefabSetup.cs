// 【役割】Editor上で看護師・医者Prefabへ敵AIと部位当たり判定を設定する。
// 【実行場所】Unity Editor専用。設定の作成・適用処理を担当します。
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HospitalZombiePrefabSetup
{
    const int Version = 2;
    const string VersionKey = "DeadShot.HospitalZombiePrefabSetupVersion";
    const string MobPath = "Assets/Game/Enemy/MobZombie/MZombie.prefab";
    static readonly string[] Targets =
    {
        "Assets/Game/Enemy/NarsM/看護師.prefab",
        "Assets/Game/Enemy/DoctorM/医者.prefab"
    };
    static HospitalZombiePrefabSetup() => EditorApplication.delayCall += SetupIfNeeded;
    [MenuItem("Dead Shot/Enemy/Setup Doctor And Nurse Zombies")]
    public static void Apply()
    {
        GameObject mob = PrefabUtility.LoadPrefabContents(MobPath);
        if (mob == null)
        {
            return;
        }

        try
        {
            foreach (string path in Targets)
            {
                SetupTarget(path, mob);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(mob);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log("[Hospital Zombie] 看護師・医者をMOBゾンビAI化しました（部位破壊なし）。");
    }

    static void SetupTarget(string path, GameObject mob)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            return;
        }

        try
        {
            root.tag = "Enemy";
            Animator animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[Hospital Zombie] Animatorがありません: " + path);
                return;
            }

            AudioSource audio = GetOrAdd<AudioSource>(root);
            EnemyManager manager = CopyComponent(mob.GetComponent<EnemyManager>(), root);
            EnemyAttack attack = CopyComponent(mob.GetComponent<EnemyAttack>(), root);
            EnemyHealth health = CopyComponent(mob.GetComponent<EnemyHealth>(), root);
            CopyComponent(mob.GetComponent<EnemyBloodEffects>(), root);
            CopyComponent(mob.GetComponent<EnemyWeakPoint>(), root);
            CapsuleCollider movement = root.GetComponentInChildren<CapsuleCollider>(true);
            Collider torso = CreateHurtbox(animator, HumanBodyBones.Chest, "Hurtbox_Torso", false);
            if (torso == null)
            {
                torso = movement;
            }

            Collider head = CreateHurtbox(animator, HumanBodyBones.Head, "Hurtbox_Head", true);
            Collider leftArm = CreateHurtbox(animator, HumanBodyBones.LeftUpperArm, "Hurtbox_LeftArm", false);
            Collider rightArm = CreateHurtbox(animator, HumanBodyBones.RightUpperArm, "Hurtbox_RightArm", false);
            Collider leftLeg = CreateHurtbox(animator, HumanBodyBones.LeftUpperLeg, "Hurtbox_LeftLeg", false);
            Collider rightLeg = CreateHurtbox(animator, HumanBodyBones.RightUpperLeg, "Hurtbox_RightLeg", false);
            ZombieAttackHitbox armHitbox = CreateAttackHitbox(animator, HumanBodyBones.RightHand, "HitBox_RightArm", .18f);
            ZombieAttackHitbox mouthHitbox = CreateAttackHitbox(animator, HumanBodyBones.Head, "HitBox_Mouth", .16f);
            SerializedObject managerSO = new SerializedObject(manager);
            managerSO.FindProperty("enemyAttack").objectReferenceValue = attack;
            managerSO.FindProperty("audioSource").objectReferenceValue = audio;
            managerSO.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject attackSO = new SerializedObject(attack);
            attackSO.FindProperty("rightArmHitbox").objectReferenceValue = armHitbox;
            attackSO.FindProperty("mouthHitbox").objectReferenceValue = mouthHitbox;
            attackSO.FindProperty("crawlHitbox").objectReferenceValue = null;
            attackSO.FindProperty("audioSource").objectReferenceValue = audio;
            attackSO.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject healthSO = new SerializedObject(health);
            SetReference(healthSO, "torsoCollider", torso);
            SetReference(healthSO, "headCollider", head);
            SetReference(healthSO, "leftArmCollider", leftArm);
            SetReference(healthSO, "rightArmCollider", rightArm);
            SetReference(healthSO, "leftLegCollider", leftLeg);
            SetReference(healthSO, "rightLegCollider", rightLeg);
            SetReference(healthSO, "movementCollider", movement);
            healthSO.FindProperty("enableLimbDestruction").boolValue = false;
            healthSO.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static T CopyComponent<T>(T source, GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        if (source != null)
        {
            EditorUtility.CopySerialized(source, component);
        }

        return component;
    }

    static T GetOrAdd<T>(GameObject target)
        where T : Component
    {
        // UnityEngine.Objectの「破棄済みだがC#参照は残っている」状態は
        // ??では判定できないため、Unityの == null を使う。
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }

    static Collider CreateHurtbox(Animator animator, HumanBodyBones bone, string name, bool sphere)
    {
        if (!animator.isHuman || animator.GetBoneTransform(bone) == null)
        {
            return null;
        }

        Transform parent = animator.transform;
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject go = sphere ? new GameObject(name, typeof(SphereCollider), typeof(HumanoidBoneFollower)) : new GameObject(name, typeof(CapsuleCollider), typeof(HumanoidBoneFollower));
        go.transform.SetParent(parent, false);
        go.GetComponent<HumanoidBoneFollower>().Configure(animator, bone);
        go.layer = LayerMask.NameToLayer("HurtBox");
        go.tag = "Enemy";
        if (sphere)
        {
            SphereCollider collider = go.GetComponent<SphereCollider>();
            collider.radius = .16f;
            collider.isTrigger = true;
            return collider;
        }

        CapsuleCollider capsule = go.GetComponent<CapsuleCollider>();
        capsule.radius = .11f;
        capsule.height = .42f;
        capsule.direction = 1;
        capsule.isTrigger = true;
        return capsule;
    }

    static ZombieAttackHitbox CreateAttackHitbox(Animator animator, HumanBodyBones bone, string name, float radius)
    {
        if (!animator.isHuman || animator.GetBoneTransform(bone) == null)
        {
            return null;
        }

        Transform parent = animator.transform;
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject go = new GameObject(name, typeof(SphereCollider), typeof(HumanoidBoneFollower), typeof(ZombieAttackHitbox));
        go.transform.SetParent(parent, false);
        Vector3 offset = bone == HumanBodyBones.Head ? new Vector3(0f, .02f, .12f) : Vector3.zero;
        go.GetComponent<HumanoidBoneFollower>().Configure(animator, bone, offset);
        go.layer = LayerMask.NameToLayer("HitBox");
        go.tag = "Enemy";
        SphereCollider collider = go.GetComponent<SphereCollider>();
        collider.radius = radius;
        collider.isTrigger = true;
        collider.enabled = false;
        return go.GetComponent<ZombieAttackHitbox>();
    }

    static void SetReference(SerializedObject serialized, string name, Object value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    static void SetupIfNeeded()
    {
        if (EditorPrefs.GetInt(VersionKey, 0) < Version)
        {
            Apply();
        }
    }
}
#endif

