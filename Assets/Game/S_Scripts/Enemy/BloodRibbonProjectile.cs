// 【役割】飛び散る血液の軌跡と接触時の表現を制御する。
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BloodRibbonProjectile : MonoBehaviour
{
    private Transform ownerRoot;
    private Material[] decalMaterials;
    private Color bloodColor;
    private Vector3 velocity;
    private Vector2 decalScaleRange;
    private LayerMask collisionLayers;
    private float gravity;
    private float wiggleStrength;
    private float wiggleFrequency;
    private float velocityDamping;
    private float elapsedTime;
    private float wigglePhase;
    private Vector3 wiggleAxisA;
    private Vector3 wiggleAxisB;
    private float remainingLifetime;
    private float decalLifetime;
    private int maximumDecals;
    private TrailRenderer trail;
    private bool stopped;
    public void Initialize(Transform owner, Material trailMaterial, Material[] surfaceDecalMaterials, Color color, Vector3 initialVelocity, float width, float lifetime, float gravityStrength, float pathWiggleStrength, float pathWiggleFrequency, float pathVelocityDamping, LayerMask surfaceLayers, Vector2 surfaceDecalScaleRange, float surfaceDecalLifetime, int maximumSurfaceDecals)
    {
        ownerRoot = owner;
        decalMaterials = surfaceDecalMaterials;
        bloodColor = color;
        velocity = initialVelocity;
        gravity = Mathf.Max(0f, gravityStrength);
        wiggleStrength = Mathf.Max(0f, pathWiggleStrength);
        wiggleFrequency = Mathf.Max(0.01f, pathWiggleFrequency);
        velocityDamping = Mathf.Max(0f, pathVelocityDamping);
        wigglePhase = Random.Range(0f, Mathf.PI * 2f);
        Vector3 forward = initialVelocity.sqrMagnitude > 0.0001f ? initialVelocity.normalized : Vector3.forward;
        wiggleAxisA = Vector3.Cross(forward, Random.onUnitSphere).normalized;
        if (wiggleAxisA.sqrMagnitude < 0.0001f)
        {
            wiggleAxisA = Vector3.Cross(forward, Vector3.up).normalized;
        }

        if (wiggleAxisA.sqrMagnitude < 0.0001f)
        {
            wiggleAxisA = Vector3.right;
        }

        wiggleAxisB = Vector3.Cross(forward, wiggleAxisA).normalized;
        remainingLifetime = Mathf.Max(0.05f, lifetime);
        collisionLayers = surfaceLayers;
        decalScaleRange = surfaceDecalScaleRange;
        decalLifetime = Mathf.Max(0.1f, surfaceDecalLifetime);
        maximumDecals = Mathf.Max(1, maximumSurfaceDecals);
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = trailMaterial;
        trail.time = Random.Range(0.35f, 0.7f);
        trail.startWidth = Mathf.Max(0.002f, width);
        trail.endWidth = Mathf.Max(0.0005f, width * Random.Range(0.08f, 0.22f));
        trail.minVertexDistance = 0.015f;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.generateLightingData = true;
        trail.numCornerVertices = 3;
        trail.numCapVertices = 3;
        trail.colorGradient = CreateBloodGradient(color);
        trail.emitting = true;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (stopped)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        elapsedTime += deltaTime;
        remainingLifetime -= deltaTime;
        Vector3 start = transform.position;
        float primaryWave = Mathf.Sin(elapsedTime * wiggleFrequency + wigglePhase);
        float secondaryWave = Mathf.Sin(elapsedTime * wiggleFrequency * 0.67f + wigglePhase * 1.37f);
        Vector3 wiggleAcceleration = wiggleAxisA * primaryWave * wiggleStrength + wiggleAxisB * secondaryWave * wiggleStrength * 0.65f;
        velocity += wiggleAcceleration * deltaTime;
        velocity += Vector3.down * gravity * deltaTime;
        velocity *= Mathf.Exp(-velocityDamping * deltaTime);
        Vector3 end = start + velocity * deltaTime;
        if (TryGetSurfaceHit(start, end, out RaycastHit hit))
        {
            transform.position = hit.point;
            SpawnSurfaceDecal(hit);
            StopRibbon();
            return;
        }

        transform.position = end;
        if (remainingLifetime <= 0f)
        {
            StopRibbon();
        }
    }

    private bool TryGetSurfaceHit(Vector3 start, Vector3 end, out RaycastHit validHit)
    {
        Vector3 movement = end - start;
        float distance = movement.magnitude;
        if (distance <= 0.0001f)
        {
            validHit = default;
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(start, movement / distance, distance, collisionLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (RaycastHit hit in hits)
        {
            if (ownerRoot != null && hit.transform.IsChildOf(ownerRoot))
            {
                continue;
            }

            validHit = hit;
            return true;
        }

        validHit = default;
        return false;
    }

    private void SpawnSurfaceDecal(RaycastHit hit)
    {
        if (decalMaterials == null || decalMaterials.Length == 0)
        {
            return;
        }

        if (hit.collider.GetComponentInParent<EnemyHealth>() != null)
        {
            return;
        }

        BloodDecalManager.Spawn(hit.point, hit.normal, decalMaterials, bloodColor, decalScaleRange, decalLifetime, maximumDecals);
    }

    private void StopRibbon()
    {
        if (stopped)
        {
            return;
        }

        stopped = true;
        if (trail != null)
        {
            trail.emitting = false;
        }

        Object.Destroy(gameObject, trail != null ? trail.time : 0.1f);
    }

    private static Gradient CreateBloodGradient(Color color)
    {
        Gradient gradient = new Gradient();
        Color darkColor = Color.Lerp(color, Color.black, 0.28f);
        gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(darkColor, 1f) }, new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a * 0.85f, 0.55f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }
}
