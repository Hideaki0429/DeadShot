// 【役割】ゾンビの部位切断時のモデルと破片の表示を処理する。
using System.Collections.Generic;
using UnityEngine;

// 現在ポーズのメッシュを複製し、該当ボーンに属する三角形だけを飛散させる。
public sealed class ZombieLimbSever : MonoBehaviour
{
    readonly List<Mesh> ownedMeshes = new List<Mesh>();
    public void Sever(EnemyHealth.HitPart part, Vector3 direction, float lifetime)
    {
        var animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            return;
        }

        var bone = animator.GetBoneTransform(part == EnemyHealth.HitPart.LeftArm ? HumanBodyBones.LeftHand : part == EnemyHealth.HitPart.RightArm ? HumanBodyBones.RightHand : part == EnemyHealth.HitPart.LeftLeg ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
        if (bone == null)
        {
            return;
        }

        foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var original = renderer.sharedMesh;
            if (original == null || !original.isReadable)
            {
                Debug.LogWarning("[Sever] Mesh Read/Write required: " + renderer.name, this);
                continue;
            }

            var weights = original.boneWeights;
            var bones = renderer.bones;
            bool[] selected = new bool[original.vertexCount];
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                float sum = Weight(w.boneIndex0, w.weight0) + Weight(w.boneIndex1, w.weight1) + Weight(w.boneIndex2, w.weight2) + Weight(w.boneIndex3, w.weight3);
                selected[i] = sum >= .5f;
            }

            float Weight(int index, float value) => index >= 0 && index < bones.Length && bones[index] != null && (bones[index] == bone || bones[index].IsChildOf(bone)) ? value : 0;
            var remain = Instantiate(original);
            var fragment = new Mesh();
            renderer.BakeMesh(fragment);
            fragment.subMeshCount = original.subMeshCount;
            bool any = false;
            for (int s = 0; s < original.subMeshCount; s++)
            {
                int[] triangles = original.GetTriangles(s);
                var kept = new List<int>();
                var cut = new List<int>();
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    bool remove = (selected[triangles[t]] ? 1 : 0) + (selected[triangles[t + 1]] ? 1 : 0) + (selected[triangles[t + 2]] ? 1 : 0) >= 2;
                    var list = remove ? cut : kept;
                    list.Add(triangles[t]);
                    list.Add(triangles[t + 1]);
                    list.Add(triangles[t + 2]);
                    any |= remove;
                }

                remain.SetTriangles(kept, s);
                fragment.SetTriangles(cut, s);
            }

            if (!any)
            {
                Destroy(remain);
                Destroy(fragment);
                continue;
            }

            renderer.sharedMesh = remain;
            ownedMeshes.Add(remain);
            var go = new GameObject("Severed " + part);
            go.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
            go.transform.localScale = renderer.transform.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = fragment;
            go.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            // 原点を切断部位へ移し、小さな当たり判定で床に落下させる。
            Vector3 local = go.transform.InverseTransformPoint(bone.position);
            var vertices = fragment.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] -= local;
            }

            fragment.vertices = vertices;
            fragment.RecalculateBounds();
            go.transform.position = bone.position;
            var collider = go.AddComponent<SphereCollider>();
            collider.radius = .07f;
            foreach (var own in GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(collider, own);
            }

            var body = go.AddComponent<Rigidbody>();
            body.mass = .5f;
            body.velocity = direction.normalized * 1.5f + Vector3.up * 2f;
            body.angularVelocity = Random.insideUnitSphere * 8f;
            go.AddComponent<SeveredMeshCleanup>().mesh = fragment;
            Destroy(go, lifetime);
        }
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        foreach (var mesh in ownedMeshes)
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }
    }
}

public sealed class SeveredMeshCleanup : MonoBehaviour
{
    public Mesh mesh;
    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }
}
