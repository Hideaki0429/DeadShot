#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class BrazoInjectionBakeUtility
{
    internal const string OutputPath = "Assets/Game/Anim/recover/BRAZO_Injection_Baked.anim";

    [MenuItem("Dead Shot/Player/Bake BRAZO Injection Motion")]
    static void BakeFromMenu()
    {
        BrazoInjectionRetargeter retarget = Object.FindObjectOfType<BrazoInjectionRetargeter>(true);
        if (retarget == null) { Debug.LogWarning("BRAZO Injection Retargeterがシーンにありません。"); return; }
        AnimationClip result = Bake(retarget);
        if (result != null) Selection.activeObject = result;
    }

    internal static AnimationClip Bake(BrazoInjectionRetargeter retarget)
    {
        if (retarget == null || retarget.brazoPrefab == null || retarget.brazoClip == null) return null;
        Animator targetAnimator = retarget.GetComponent<Animator>();
        if (targetAnimator == null || !targetAnimator.isHuman) return null;

        GameObject driver = (GameObject)PrefabUtility.InstantiatePrefab(retarget.brazoPrefab);
        if (driver == null) driver = Object.Instantiate(retarget.brazoPrefab);
        driver.hideFlags = HideFlags.HideAndDontSave;
        driver.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Transform sourceChest = Find(driver, "Spine2");
        Transform sourceLeftShoulder = Find(driver, "LeftArm");
        Transform sourceLeftElbow = Find(driver, "LeftForeArm.001");
        Transform sourceLeftHand = Find(driver, "LeftHand");
        Transform sourceRightShoulder = Find(driver, "RightArm");
        Transform sourceRightElbow = Find(driver, "RightForeArm.001");
        Transform sourceRightHand = Find(driver, "RightHand");

        Transform chest = targetAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null) chest = targetAnimator.GetBoneTransform(HumanBodyBones.Chest);
        Transform[] bones =
        {
            targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
            targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
            targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand),
            targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm),
            targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm),
            targetAnimator.GetBoneTransform(HumanBodyBones.RightHand)
        };
        if (chest == null || bones.Any(b => b == null) || sourceChest == null || sourceLeftShoulder == null ||
            sourceLeftElbow == null || sourceLeftHand == null || sourceRightShoulder == null ||
            sourceRightElbow == null || sourceRightHand == null)
        { Object.DestroyImmediate(driver); return null; }

        Quaternion[] original = bones.Select(b => b.localRotation).ToArray();
        Dictionary<string, QuaternionCurves> curves = bones.ToDictionary(
            b => AnimationUtility.CalculateTransformPath(b, targetAnimator.transform), b => new QuaternionCurves());
        const float frameRate = 30f;
        int frames = Mathf.CeilToInt(retarget.brazoClip.length * frameRate);

        AnimationMode.StartAnimationMode();
        try
        {
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = Mathf.Min(retarget.brazoClip.length, frame / frameRate);
                for (int i = 0; i < bones.Length; i++) bones[i].localRotation = original[i];
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(driver, retarget.brazoClip, time);
                AnimationMode.EndSampling();

                float sourceWidth = Vector3.Distance(sourceLeftShoulder.position, sourceRightShoulder.position);
                float targetWidth = Vector3.Distance(bones[0].position, bones[3].position);
                float scale = targetWidth / Mathf.Max(.0001f, sourceWidth) * retarget.positionScaleMultiplier;
                Quaternion map = chest.rotation * Quaternion.Inverse(sourceChest.rotation);
                Solve(bones[0], bones[1], bones[2],
                    chest.position + map * ((sourceLeftElbow.position - sourceChest.position) * scale),
                    chest.position + map * ((sourceLeftHand.position - sourceChest.position) * scale) + chest.TransformVector(retarget.leftHandPositionOffset),
                    map * sourceLeftHand.rotation * Quaternion.Euler(retarget.leftHandRotationOffset));
                Solve(bones[3], bones[4], bones[5],
                    chest.position + map * ((sourceRightElbow.position - sourceChest.position) * scale),
                    chest.position + map * ((sourceRightHand.position - sourceChest.position) * scale) + chest.TransformVector(retarget.rightHandPositionOffset),
                    map * sourceRightHand.rotation * Quaternion.Euler(retarget.rightHandRotationOffset));

                foreach (Transform bone in bones)
                    curves[AnimationUtility.CalculateTransformPath(bone, targetAnimator.transform)].Add(time, bone.localRotation);
            }
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = original[i];
            Object.DestroyImmediate(driver);
        }

        AnimationClip baked = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputPath);
        if (baked == null)
        {
            baked = new AnimationClip { name = "BRAZO_Injection_Baked", frameRate = frameRate };
            AssetDatabase.CreateAsset(baked, OutputPath);
        }
        baked.ClearCurves(); baked.frameRate = frameRate;
        foreach (KeyValuePair<string, QuaternionCurves> pair in curves) pair.Value.Apply(baked, pair.Key);
        baked.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(baked); AssetDatabase.SaveAssets();
        Debug.Log($"[BRAZO Bake] 転写済みクリップを生成: {OutputPath} ({baked.length:F2}s)", baked);
        return baked;
    }

    static Transform Find(GameObject root, string suffix, string exclude = null) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith(suffix) &&
            (string.IsNullOrEmpty(exclude) || !t.name.EndsWith(exclude)));

    static void Solve(Transform upper, Transform lower, Transform hand,
        Vector3 elbowTarget, Vector3 handTarget, Quaternion handRotation)
    {
        Vector3 currentUpper = lower.position - upper.position;
        Vector3 wantedUpper = elbowTarget - upper.position;
        if (currentUpper.sqrMagnitude > .0001f && wantedUpper.sqrMagnitude > .0001f)
            upper.rotation = Quaternion.FromToRotation(currentUpper, wantedUpper) * upper.rotation;
        Vector3 currentLower = hand.position - lower.position;
        Vector3 wantedLower = handTarget - lower.position;
        if (currentLower.sqrMagnitude > .0001f && wantedLower.sqrMagnitude > .0001f)
            lower.rotation = Quaternion.FromToRotation(currentLower, wantedLower) * lower.rotation;
        hand.rotation = handRotation;
    }

    sealed class QuaternionCurves
    {
        readonly AnimationCurve x = new AnimationCurve(), y = new AnimationCurve();
        readonly AnimationCurve z = new AnimationCurve(), w = new AnimationCurve();
        internal void Add(float time, Quaternion q)
        {
            x.AddKey(time, q.x); y.AddKey(time, q.y); z.AddKey(time, q.z); w.AddKey(time, q.w);
        }
        internal void Apply(AnimationClip clip, string path)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"), x);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"), y);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"), z);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"), w);
        }
    }
}
#endif
