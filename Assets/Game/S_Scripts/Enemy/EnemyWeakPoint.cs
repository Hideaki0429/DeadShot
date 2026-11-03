// 【役割】敵ごとの弱点部位と暗視時の弱点表示を管理する。
using UnityEngine;

/// <summary>敵ごとに弱点を1箇所抽選し、特殊ゴーグル装備中だけ発光点を表示する。</summary>
public sealed class EnemyWeakPoint : MonoBehaviour
{
    [Header("弱点表示")]
    [SerializeField, Min(0.01f)]
    private float markerDiameter = 0.035f;
    [SerializeField, Min(0f)]
    private float surfaceOffset = 0.018f;
    [SerializeField]
    private Color markerColor = new Color(1f, 0.015f, 0.01f, 1f);
    [SerializeField]
    private bool addPointLight = true;
    [SerializeField, Min(0f)]
    private float lightIntensity = 15.5f;
    [SerializeField, Min(0.01f)]
    private float lightRange = 0.23f;
    private EnemyHealth health;
    private EnemyHealth.HitPart weakPart;
    private Collider weakCollider;
    private Transform marker;
    private Vector3 fixedLocalPoint;
    public EnemyHealth.HitPart WeakPart => weakPart;

    public void Initialize(EnemyHealth owner)
    {
        health = owner;
        SelectRandomWeakPoint();
        CreateMarker();
        RefreshVisibility();
    }

    // コンポーネントが有効になったときの処理です。
    private void OnEnable() => SpecialGogglesController.EquipmentChanged += OnEquipmentChanged;
    // コンポーネントが無効になったときの処理です。
    private void OnDisable() => SpecialGogglesController.EquipmentChanged -= OnEquipmentChanged;
    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    private void LateUpdate()
    {
        if (health == null || marker == null)
        {
            return;
        }

        RefreshVisibility();
        if (!marker.gameObject.activeSelf || weakCollider == null)
        {
            return;
        }

        // 選択時に決めたCollider表面の一点に固定。カメラ移動で弱点は動かない。
        marker.position = weakCollider.transform.TransformPoint(fixedLocalPoint);
    }

    public bool IsWeakPoint(EnemyHealth.HitPart part, Collider hitCollider) => part == weakPart && hitCollider == weakCollider;
    private void SelectRandomWeakPoint()
    {
        EnemyHealth.HitPart[] candidates =
        {
            EnemyHealth.HitPart.Head,
            EnemyHealth.HitPart.Torso,
            EnemyHealth.HitPart.LeftArm,
            EnemyHealth.HitPart.RightArm,
            EnemyHealth.HitPart.LeftLeg,
            EnemyHealth.HitPart.RightLeg
        };
        int start = Random.Range(0, candidates.Length);
        for (int i = 0; i < candidates.Length; i++)
        {
            EnemyHealth.HitPart candidate = candidates[(start + i) % candidates.Length];
            Collider candidateCollider = health.GetHitPartCollider(candidate);
            if (candidateCollider == null)
            {
                continue;
            }

            weakPart = candidate;
            weakCollider = candidateCollider;
            Vector3 center = weakCollider.bounds.center;
            Vector3 outward = center - health.transform.position;
            if (outward.sqrMagnitude < .0001f)
            {
                outward = health.transform.forward;
            }

            outward.Normalize();
            float probeDistance = Mathf.Max(weakCollider.bounds.extents.x, weakCollider.bounds.extents.y, weakCollider.bounds.extents.z) * 2f + surfaceOffset;
            Vector3 surface = weakCollider.ClosestPoint(center + outward * probeDistance);
            fixedLocalPoint = weakCollider.transform.InverseTransformPoint(surface + outward * surfaceOffset);
            return;
        }

        weakPart = EnemyHealth.HitPart.Unregistered;
    }

    private void CreateMarker()
    {
        if (marker != null || weakCollider == null)
        {
            return;
        }

        GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        markerObject.name = "Dead Shot Weak Point";
        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycast >= 0)
        {
            markerObject.layer = ignoreRaycast;
        }

        markerObject.transform.SetParent(transform, true);
        markerObject.transform.localScale = Vector3.one * markerDiameter;
        Destroy(markerObject.GetComponent<Collider>());
        marker = markerObject.transform;
        Renderer markerRenderer = markerObject.GetComponent<Renderer>();
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            color = markerColor
        };
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", markerColor * 3f);
        }

        markerRenderer.material = material;
        if (!addPointLight)
        {
            return;
        }

        Light pointLight = markerObject.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = markerColor;
        pointLight.intensity = lightIntensity;
        pointLight.range = lightRange;
        pointLight.shadows = LightShadows.None;
    }

    private void OnEquipmentChanged(bool equipped) => RefreshVisibility();
    private void RefreshVisibility()
    {
        bool visible = SpecialGogglesController.IsEquippedGlobally && health != null && !health.IsDead && weakCollider != null;
        if (marker != null && marker.gameObject.activeSelf != visible)
        {
            marker.gameObject.SetActive(visible);
        }
    }
}
