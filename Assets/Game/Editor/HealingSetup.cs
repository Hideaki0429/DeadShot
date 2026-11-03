using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HealingSetup
{
    static HealingSetup() { EditorApplication.delayCall += AutoSetup; }
    static void AutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var health = Object.FindObjectOfType<PlayerHealth>(true);
        if (health != null)
        {
            var healing = health.GetComponent<PlayerHealing>();
            var inventory = Object.FindObjectOfType<PlayerInventory>(true);
            if (healing != null && healing.inventory == null && inventory != null)
            {
                healing.inventory = inventory;
                EditorUtility.SetDirty(healing);
                EditorSceneManager.MarkSceneDirty(health.gameObject.scene);
                EditorSceneManager.SaveScene(health.gameObject.scene);
                Debug.Log("[Healing] Inventory linked: " + healing.healingItemId);
            }
            var animator = health.GetComponent<Animator>();
            var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
            if (controller != null)
            {
                var layers = controller.layers;
                bool changed = false;
                foreach (var layer in layers)
                    if (layer.name == "Healing" && layer.avatarMask != null)
                    { layer.avatarMask = null; changed = true; }
                if (changed)
                {
                    controller.layers = layers;
                    EditorUtility.SetDirty(controller);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Healing] Full-body Drinking layer applied.");
                }
            }
        }
        if (health != null && (health.GetComponent<PlayerHealing>() == null ||
            (health.GetComponent<PlayerHealing>().heldItem != null && health.GetComponent<PlayerHealing>().heldItem.isStatic))) Setup();
    }
    [MenuItem("Dead Shot/Player/Setup Drinking Healing")]
    static void Setup()
    {
        var health = Object.FindObjectOfType<PlayerHealth>(true);
        if (health == null || EditorApplication.isPlaying) return;
        var animator = health.GetComponent<Animator>();
        var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Game/Anim/Drinking.anim");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Item/AE_Medical_Supplies_04.prefab");
        if (controller == null || clip == null || prefab == null || !animator.isHuman) { Debug.LogError("[Healing] Required humanoid/controller/clip/prefab missing"); return; }
        bool mirror = false;
        float left = float.MaxValue, right = float.MaxValue;
        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
            try {
                for (int i=1;i<20;i++) {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(health.gameObject, clip, clip.length*i/20f);
                    AnimationMode.EndSampling();
                    var head=animator.GetBoneTransform(HumanBodyBones.Head);
                    left=Mathf.Min(left,Vector3.Distance(head.position,animator.GetBoneTransform(HumanBodyBones.LeftHand).position));
                    right=Mathf.Min(right,Vector3.Distance(head.position,animator.GetBoneTransform(HumanBodyBones.RightHand).position));
                }
                mirror = right < left;
            } finally { AnimationMode.StopAnimationMode(); }
        }
        const string maskPath="Assets/Game/Anim/HealingUpperBody.mask";
        var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if(mask==null) {
            mask=new AvatarMask();
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            foreach(var part in new[]{AvatarMaskBodyPart.Body,AvatarMaskBodyPart.Head,AvatarMaskBodyPart.LeftArm,AvatarMaskBodyPart.RightArm,AvatarMaskBodyPart.LeftFingers,AvatarMaskBodyPart.RightFingers})
                mask.SetHumanoidBodyPartActive(part,true);
            AssetDatabase.CreateAsset(mask,maskPath);
        }
        if(!controller.parameters.Any(p=>p.name=="HealingSpeed")) controller.AddParameter("HealingSpeed",AnimatorControllerParameterType.Float);
        if(!controller.layers.Any(l=>l.name=="Healing")) {
            controller.AddLayer("Healing");
            var layers=controller.layers; var layer=layers[layers.Length-1];
            layer.avatarMask=null; layer.defaultWeight=0; layer.blendingMode=AnimatorLayerBlendingMode.Override;
            var state=layer.stateMachine.AddState("Drinking");
            state.motion=clip; state.mirror=mirror; state.speedParameter="HealingSpeed"; state.speedParameterActive=true;
            state.writeDefaultValues=false;
            controller.layers=layers;
            EditorUtility.SetDirty(controller);
        }
        var heal=health.GetComponent<PlayerHealing>();
        if(heal==null)heal=Undo.AddComponent<PlayerHealing>(health.gameObject);
        heal.drinkingClip=clip;
        if(heal.heldItem==null) {
            var hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var item=(GameObject)PrefabUtility.InstantiatePrefab(prefab,hand);
            item.name="Healing Drink (Left Hand)";
            foreach(var c in item.GetComponentsInChildren<Collider>(true)) c.enabled=false;
            foreach(var b in item.GetComponentsInChildren<Rigidbody>(true)) {b.isKinematic=true;b.detectCollisions=false;}
            foreach(var b in item.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled=false;
            item.transform.localPosition=Vector3.zero; item.transform.localRotation=Quaternion.identity; item.transform.localScale=Vector3.one;
            heal.heldItem=item;
            var renderers=item.GetComponentsInChildren<Renderer>();
            if(renderers.Length>0) {
                var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                float scale=.18f/Mathf.Max(.001f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));
                heal.gripScale=Vector3.one*scale;
                heal.gripPosition=new Vector3(0,.025f,0)-item.transform.InverseTransformPoint(bounds.center)*scale;
            }
            item.transform.localPosition=heal.gripPosition; item.transform.localScale=heal.gripScale;
            item.SetActive(false);
        }
        if (heal.heldItem != null)
            foreach (var t in heal.heldItem.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        EditorUtility.SetDirty(heal);
        EditorSceneManager.MarkSceneDirty(health.gameObject.scene);
        EditorSceneManager.SaveScene(health.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Healing Setup] Registered full-body Drinking and left-hand prop. Mirror={mirror}, nearest hand/head L={left:F3} R={right:F3}");
    }
}
