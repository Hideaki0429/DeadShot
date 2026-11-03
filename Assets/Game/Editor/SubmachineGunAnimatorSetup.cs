using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SubmachineGunAnimatorSetup
{
    const string ControllerPath = "Assets/unity-chan!/Unity-chan! Model/Art/Animations/Animators/UnityChanLocomotions.controller";
    const string UpperMaskPath = "Assets/Kevin Iglesias/Human Animations/Models/Avatar Masks/Human Body Upper Mask.mask";

    [MenuItem("Tools/Dead Shot/Setup Submachine Gun")]
    public static void Install()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) { Debug.LogError("[SMG Setup] Player Animator Controllerが見つかりません。"); return; }
        BuildAnimator(controller);
        InstallSceneComponents();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SMG Setup] RifleAnim・Rifle 1・Magazine1・Bullet4 (1)・RIFLE2マズルフラッシュを登録しました。");
    }

    static void BuildAnimator(AnimatorController controller)
    {
        EnsureParameter(controller, "RifleSelected", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "RifleEquip", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "RifleUnequip", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "RifleShoot", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "RifleShootMoving", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "RifleReload", AnimatorControllerParameterType.Trigger);

        RemoveLayer(controller, "SMG Full Body");
        RemoveLayer(controller, "SMG Upper Body");

        AnimatorControllerLayer full = new AnimatorControllerLayer
        {
            name = "SMG Full Body", defaultWeight = 0f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            stateMachine = new AnimatorStateMachine { name = "SMG Full Body" }
        };
        AssetDatabase.AddObjectToAsset(full.stateMachine, controller);
        controller.AddLayer(full);
        BuildFullBody(full.stateMachine);

        AnimatorControllerLayer upper = new AnimatorControllerLayer
        {
            name = "SMG Upper Body", defaultWeight = 0f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperMaskPath),
            stateMachine = new AnimatorStateMachine { name = "SMG Upper Body" }
        };
        AssetDatabase.AddObjectToAsset(upper.stateMachine, controller);
        controller.AddLayer(upper);
        BuildUpperBody(upper.stateMachine);
        EditorUtility.SetDirty(controller);
    }

    static void BuildFullBody(AnimatorStateMachine sm)
    {
        AnimatorState empty = sm.AddState("SMG Empty", new Vector3(0, 0));
        sm.defaultState = empty;
        AnimatorState pull = State(sm, "RiflePullOut", "Assets/Game/Anim/RifleAnim/Put/Swat@Rifle Pull Out.fbx", "RiflePullOut", -220, 100);
        AnimatorState idle = State(sm, "RifleIdle", "Assets/Game/Anim/RifleAnim/Swat@Rifle Idle.fbx", "RifleIdle", 0, 100);
        AnimatorState walk = State(sm, "RifleWalk", "Assets/Game/Anim/RifleAnim/Walk/Swat@Rifle Walk.fbx", "RifleWalk", 220, 40);
        AnimatorState run = State(sm, "RifleRun", "Assets/Game/Anim/RifleAnim/Run/Swat@Rifle Run.fbx", "RifleRun", 440, 40);
        AnimatorState walkStop = State(sm, "RifleWalkStop", "Assets/Game/Anim/RifleAnim/Walk/Swat@Rifle Walk To Stop.fbx", "RifleWalkStop", 220, 150);
        AnimatorState runStop = State(sm, "RifleRunStop", "Assets/Game/Anim/RifleAnim/Run/Swat@Rifle Run To Stop.fbx", "RIfleRunStop", 440, 150);
        AnimatorState put = State(sm, "RiflePutAway", "Assets/Game/Anim/RifleAnim/Put/Swat@Rifle Put Away.fbx", "RiflePutAwaay", -220, 250);
        // Setupに入った直後は2D BlendTreeの中心=RifleAimIdleを再生し、
        // そこから前後左右移動・射撃・リロードへ遷移する。
        AnimatorState aim = sm.AddState("RifleAimIdle / Movement", new Vector3(220, -120));
        aim.motion = AimTree(sm);
        AnimatorState shoot = State(sm, "RifleShoot1", "Assets/Game/Anim/RifleAnim/Shoot/Swat@Firing Rifle.fbx", "RifleShoot1", 0, -120);
        AnimatorState reload = State(sm, "RifleReloadStop", "Assets/Game/Anim/RifleAnim/Reload/Swat@Reloading.fbx", "Rilfereload2", -220, -120);
        AnimatorState hit = State(sm, "RifleHit", "Assets/Game/Anim/RifleAnim/Hit/Swat@Hit Reaction.fbx", "Riflehit", 660, -80);
        AnimatorState standingHit = State(sm, "StandingHit", "Assets/Game/Anim/Swat@Standing React Small From Headshot.fbx", "StandingHit", 660, -180);
        AnimatorState biteHit = State(sm, "HitOverReaction", "Assets/Game/Anim/Zombie Reaction Hit.fbx", "HitOverreaction", 660, -280);
        AnimatorState dieAim = State(sm, "RifleDyingAiming", "Assets/Game/Anim/RifleAnim/Hit/Swat@Rifle Death.fbx", "Rifledying1", 660, 20);
        AnimatorState dieHeld = State(sm, "RifleDyingHeld", "Assets/Game/Anim/RifleAnim/Hit/Swat@Rifle Hit To Back.fbx", "Rifledying2", 660, 120);
        hit.tag = "PlayerHit"; standingHit.tag = "PlayerHit"; biteHit.tag = "PlayerHit";
        dieAim.tag = "PlayerDeath"; dieHeld.tag = "PlayerDeath";

        Transition(empty, pull, false, 0, "RifleEquip", AnimatorConditionMode.If);
        Exit(pull, idle, .9f);
        Transition(idle, walk, false, .1f, "Speed", AnimatorConditionMode.Greater);
        Transition(walk, run, false, .79f, "Speed", AnimatorConditionMode.Greater);
        Transition(run, runStop, false, .8f, "Speed", AnimatorConditionMode.Less);
        Exit(runStop, idle, .85f);
        Transition(walk, walkStop, false, .1f, "Speed", AnimatorConditionMode.Less);
        Exit(walkStop, idle, .85f);
        foreach (AnimatorState s in new[] { idle, walk, run, walkStop, runStop })
        {
            Transition(s, aim, false, 0, "Setup", AnimatorConditionMode.If);
            Transition(s, put, false, 0, "RifleUnequip", AnimatorConditionMode.If);
        }
        Transition(aim, idle, false, 0, "Setup", AnimatorConditionMode.IfNot);
        Transition(aim, put, false, 0, "RifleUnequip", AnimatorConditionMode.If);
        Exit(put, empty, .9f);
        AnimatorStateTransition shot = sm.AddAnyStateTransition(shoot);
        ConfigureAny(shot, "RifleShoot"); shot.AddCondition(AnimatorConditionMode.If, 0, "Setup");
        Exit(shoot, aim, .85f);
        AnimatorStateTransition rel = sm.AddAnyStateTransition(reload);
        ConfigureAny(rel, "RifleReload"); rel.AddCondition(AnimatorConditionMode.If, 0, "Setup");
        Exit(reload, aim, .9f);
        AnimatorStateTransition hitT = sm.AddAnyStateTransition(hit);
        ConfigureAny(hitT, "PlayerDamage"); hitT.AddCondition(AnimatorConditionMode.Equals, 1, "DamageReactionType");
        Exit(hit, aim, .9f);
        AnimatorStateTransition standingT = sm.AddAnyStateTransition(standingHit);
        ConfigureAny(standingT, "PlayerDamage"); standingT.AddCondition(AnimatorConditionMode.Equals, 0, "DamageReactionType");
        Exit(standingHit, idle, .9f);
        AnimatorStateTransition biteT = sm.AddAnyStateTransition(biteHit);
        ConfigureAny(biteT, "PlayerDamage"); biteT.AddCondition(AnimatorConditionMode.Equals, 2, "DamageReactionType");
        Exit(biteHit, idle, .9f);
    }

    static void BuildUpperBody(AnimatorStateMachine sm)
    {
        AnimatorState empty = sm.AddState("SMG Upper Empty", new Vector3(0, 0)); sm.defaultState = empty;
        AnimatorState shoot = State(sm, "RifleShootMoving", "Assets/Game/Anim/RifleAnim/Shoot/Swat@Firing Rifle.fbx", "RifleShoot1", 220, -80);
        AnimatorState reload = State(sm, "RifleReloadMoving", "Assets/Game/Anim/RifleAnim/Reload/Swat@Reload.fbx", "Riflereload1", 220, 80);
        AnimatorStateTransition st = sm.AddAnyStateTransition(shoot); ConfigureAny(st, "RifleShootMoving");
        AnimatorStateTransition rt = sm.AddAnyStateTransition(reload); ConfigureAny(rt, "RifleReload");
        Exit(shoot, empty, .9f); Exit(reload, empty, .9f);
    }

    static BlendTree AimTree(AnimatorStateMachine owner)
    {
        BlendTree tree = new BlendTree { name = "Rifle Aim Idle / Movement 2D", blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "Direction", blendParameterY = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, owner);
        tree.AddChild(Clip("Assets/Game/Anim/RifleAnim/Idle/Rifle Aiming Idle.fbx", "RifleAimIdle"), Vector2.zero);
        tree.AddChild(Clip("Assets/Game/Anim/RifleAnim/Ready/Walk/Swat@Walk Forward.fbx", "RifleForward1"), new Vector2(0, 1));
        tree.AddChild(Clip("Assets/Game/Anim/RifleAnim/Ready/Walk/Swat@Walk Backward.fbx", "RifleBack1"), new Vector2(0, -1));
        tree.AddChild(Clip("Assets/Game/Anim/RifleAnim/Ready/Walk/Swat@Walk Left.fbx", "Rifleleft"), new Vector2(-1, 0));
        tree.AddChild(Clip("Assets/Game/Anim/RifleAnim/Ready/Walk/Swat@Walk Right.fbx", "Rifleright"), new Vector2(1, 0));
        return tree;
    }

    static void InstallSceneComponents()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Animator player = Object.FindObjectsOfType<Animator>(true).FirstOrDefault(a => a.CompareTag("Player"));
        if (player == null) { Debug.LogWarning("[SMG Setup] シーンのPlayer Animatorが見つからないため、Animatorのみ構築しました。"); return; }
        SubmachineGunController smg = player.GetComponent<SubmachineGunController>() ?? Undo.AddComponent<SubmachineGunController>(player.gameObject);
        ShortcutWeaponModelVisibility shortcutModels = player.GetComponent<ShortcutWeaponModelVisibility>() ??
            Undo.AddComponent<ShortcutWeaponModelVisibility>(player.gameObject);
        OnKeyPress_Throw pistol = player.GetComponentsInChildren<OnKeyPress_Throw>(true).FirstOrDefault(x => x.requiredWeaponId != "smg");
        OnKeyPress_Throw gun = player.GetComponents<OnKeyPress_Throw>().FirstOrDefault(x => x.requiredWeaponId == "smg");
        if (gun == null)
        {
            if (pistol != null) { UnityEditorInternal.ComponentUtility.CopyComponent(pistol); UnityEditorInternal.ComponentUtility.PasteComponentAsNew(player.gameObject); gun = player.GetComponents<OnKeyPress_Throw>().Last(); }
            else gun = Undo.AddComponent<OnKeyPress_Throw>(player.gameObject);
        }
        gun.requiredWeaponId = "smg"; gun.automaticFire = true; gun.cooldownTime = .09f; gun.maxAmmo = 30;
        SetObject(gun, "animator", player);
        SetString(gun, "shootParameter", "RifleShoot"); SetString(gun, "reloadParameter", "RifleReload");
        SetString(gun, "movingShootParameter", "RifleShootMoving");
        SetObject(gun, "newPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Rifle/Bullet4 (1).prefab"));
        SetObject(gun, "effectPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/WarFX/_Effects (Mobile)/MuzzleFlashes/4Planes/WFXMR_MF 4P RIFLE2.prefab"));
        SetObject(gun, "gunPresentation", null);

        WeaponAmmoSystem ammo = player.GetComponents<WeaponAmmoSystem>().FirstOrDefault(x => x.AmmoItemId == "m1917");
        if (ammo == null) ammo = Undo.AddComponent<WeaponAmmoSystem>(player.gameObject);
        SerializedObject ammoSo = new SerializedObject(ammo);
        ammoSo.FindProperty("weaponId").stringValue = "smg";
        ammoSo.FindProperty("gunScript").objectReferenceValue = gun;
        ammoSo.FindProperty("ammoItemId").stringValue = "m1917";
        ammoSo.FindProperty("maxAmmoInMagazine").intValue = 30;
        ammoSo.FindProperty("currentAmmoInMag").intValue = 30;
        ammoSo.ApplyModifiedPropertiesWithoutUndo();

        Transform hand = player.GetBoneTransform(HumanBodyBones.RightHand);
        GameObject rifle = GameObject.Find("SMG Rifle 1");
        if (rifle == null && hand != null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Rifle/Rifle 1.prefab");
            rifle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hand);
            rifle.name = "SMG Rifle 1"; rifle.transform.localPosition = Vector3.zero; rifle.transform.localRotation = Quaternion.identity; rifle.SetActive(false);
        }
        if (rifle != null)
        {
            Transform muzzle = rifle.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Muzzle");
            SetObject(gun, "muzzlePoint", muzzle);
        }
        GameObject magazine = GameObject.Find("SMG Magazine1");
        if (magazine == null && hand != null)
        {
            GameObject magazinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Rifle/Magazine1.prefab");
            magazine = (GameObject)PrefabUtility.InstantiatePrefab(magazinePrefab, hand);
            magazine.name = "SMG Magazine1";
            magazine.transform.localPosition = Vector3.zero;
            magazine.transform.localRotation = Quaternion.identity;
            magazine.SetActive(false);
        }
        SetObject(gun, "magazineObject", magazine);
        SerializedObject smgSo = new SerializedObject(smg);
        smgSo.FindProperty("playerAnimator").objectReferenceValue = player;
        smgSo.FindProperty("weaponSelection").objectReferenceValue = Object.FindObjectOfType<WeaponSelectionUI>(true);
        smgSo.FindProperty("gun").objectReferenceValue = gun;
        smgSo.FindProperty("ammoSystem").objectReferenceValue = ammo;
        smgSo.FindProperty("rifleModel").objectReferenceValue = rifle;
        var move = player.GetComponent<UnityChan.UnityChanControlScriptWithRgidBody>();
        if (move != null) smgSo.FindProperty("pistolModel").objectReferenceValue = move.Rifle;
        smgSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(smg);
        shortcutModels.ResolveReferences();
        EditorUtility.SetDirty(shortcutModels);

        WeaponSelectionUI ui = Object.FindObjectOfType<WeaponSelectionUI>(true);
        if (ui != null && ui.availableWeapons != null)
            foreach (var visual in ui.availableWeapons) if (visual != null && visual.weaponId == "smg") visual.ammoSource = ammo;
        EditorUtility.SetDirty(player.gameObject); if (ui != null) EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene); EditorSceneManager.SaveScene(player.gameObject.scene);
    }

    static AnimatorState State(AnimatorStateMachine sm, string name, string path, string clipName, float x, float y)
    { AnimatorState state = sm.AddState(name, new Vector3(x, y)); state.motion = Clip(path, clipName); return state; }
    static AnimationClip Clip(string path, string name) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
    static void EnsureParameter(AnimatorController c, string name, AnimatorControllerParameterType type) { if (!c.parameters.Any(p => p.name == name)) c.AddParameter(name, type); }
    static void RemoveLayer(AnimatorController c, string name) { for (int i = c.layers.Length - 1; i >= 0; --i) if (c.layers[i].name == name) c.RemoveLayer(i); }
    static void Transition(AnimatorState from, AnimatorState to, bool exit, float threshold, string parameter, AnimatorConditionMode mode) { var t = from.AddTransition(to); t.hasExitTime = exit; t.duration = .12f; t.AddCondition(mode, threshold, parameter); }
    static void Exit(AnimatorState from, AnimatorState to, float at) { var t = from.AddTransition(to); t.hasExitTime = true; t.exitTime = at; t.duration = .12f; }
    static void ConfigureAny(AnimatorStateTransition t, string trigger) { t.hasExitTime = false; t.duration = .05f; t.canTransitionToSelf = false; t.AddCondition(AnimatorConditionMode.If, 0, trigger); }
    static void SetObject(Object component, string property, Object value) { var so = new SerializedObject(component); var p = so.FindProperty(property); if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); } }
    static void SetString(Object component, string property, string value) { var so = new SerializedObject(component); var p = so.FindProperty(property); if (p != null) { p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); } }
}
