// 【役割】敵の被弾、部位破壊や死亡に応じた流血演出を生成する。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyBloodEffects : MonoBehaviour
{
    [Header("部位別の被弾VFX")]
    [SerializeField]
    private bool enableImpactBurstVFX = false;
    [SerializeField]
    private GameObject headHitVFX;
    [SerializeField]
    private GameObject torsoHitVFX;
    [SerializeField]
    private GameObject limbHitVFX;
    [SerializeField]
    private GameObject[] headHitVFXVariations;
    [SerializeField]
    private GameObject[] torsoHitVFXVariations;
    [SerializeField]
    private GameObject[] limbHitVFXVariations;
    [SerializeField]
    private float hitVFXLifetime = 2f;
    [SerializeField]
    private float surfaceOffset = 0.015f;
    [SerializeField]
    private Vector2 hitVFXScaleRange = new Vector2(0.8f, 1.25f);
    [Header("血液の色補正")]
    [SerializeField]
    private Color bloodColor = new Color(0.75f, 0.02f, 0.01f, 1f);
    [Range(0f, 2f)]
    [SerializeField]
    private float bloodEmission = 0.3f;
    [SerializeField]
    private Material dripMaterial;
    [Header("立体的な血の筋")]
    [SerializeField]
    private bool enableBloodRibbons = true;
    [SerializeField]
    private Material ribbonMaterial;
    [SerializeField]
    private Material[] bloodDecalMaterials;
    [SerializeField]
    private Vector2Int headRibbonCount = new Vector2Int(8, 12);
    [SerializeField]
    private Vector2Int torsoRibbonCount = new Vector2Int(5, 8);
    [SerializeField]
    private Vector2Int limbRibbonCount = new Vector2Int(3, 5);
    [SerializeField]
    private Vector2 ribbonSpeedRange = new Vector2(3.5f, 7.5f);
    [SerializeField]
    private Vector2 ribbonWidthRange = new Vector2(0.018f, 0.055f);
    [SerializeField]
    private Vector2 ribbonLifetimeRange = new Vector2(0.35f, 0.85f);
    [SerializeField]
    private float ribbonSpreadAngle = 32f;
    [SerializeField]
    private float ribbonGravity = 10f;
    [SerializeField]
    private Vector2 ribbonWiggleStrengthRange = new Vector2(0.4f, 1.1f);
    [SerializeField]
    private Vector2 ribbonWiggleFrequencyRange = new Vector2(8f, 15f);
    [SerializeField]
    private float ribbonVelocityDamping = 1.4f;
    [SerializeField]
    private LayerMask ribbonCollisionLayers = ~0;
    [SerializeField]
    private Vector2 decalScaleRange = new Vector2(0.18f, 0.65f);
    [SerializeField]
    private float bloodDecalLifetime = 30f;
    [SerializeField]
    private int maximumBloodDecals = 50;
    [Header("ゾンビへの血痕付着")]
    [SerializeField]
    private bool enableCharacterBloodStains = true;
    [SerializeField]
    private Vector2Int characterStainsPerHit = new Vector2Int(1, 2);
    [SerializeField]
    private Vector2 characterStainScaleRange = new Vector2(0.05f, 0.16f);
    [SerializeField]
    private int maximumCharacterBloodStains = 12;
    [SerializeField]
    private float characterStainSurfaceOffset = 0.004f;
    [Header("ヘッドショットミスト")]
    [SerializeField]
    private bool enableHeadshotMist = true;
    [SerializeField]
    private Vector2Int headshotMistParticleCount = new Vector2Int(120, 180);
    [SerializeField]
    private Vector2Int headshotMistSecondaryCount = new Vector2Int(30, 50);
    [SerializeField]
    private Vector2 headshotMistSpeedRange = new Vector2(0.8f, 2.5f);
    [SerializeField]
    private Vector2 headshotMistSizeRange = new Vector2(0.008f, 0.035f);
    [SerializeField]
    private Vector2 headshotMistLifetimeRange = new Vector2(0.15f, 0.5f);
    [Range(1f, 85f)]
    [SerializeField]
    private float headshotMistSpread = 52f;
    [SerializeField]
    private float headshotMistTurbulence = 0.45f;
    [SerializeField]
    private float headshotMistRadius = 0.015f;
    [Header("血が滴る演出")]
    [SerializeField]
    private bool enableBloodDripping = true;
    [SerializeField]
    private float dripDuration = 1f;
    [SerializeField]
    private float dripRate = 12f;
    [SerializeField]
    private float dripLifetime = 0.8f;
    [SerializeField]
    private float dripSize = 0.025f;
    [SerializeField]
    private float dripSpeed = 0.15f;
    [SerializeField]
    private float dripGravity = 1.2f;
    [Header("死亡時VFX")]
    [SerializeField]
    private GameObject corpseBloodVFX;
    [SerializeField]
    private float corpseBloodDelay = 0.35f;
    [SerializeField]
    private float corpseBloodLifetime = 4f;
    [SerializeField]
    private float groundRayDistance = 5f;
    [SerializeField]
    private LayerMask groundLayers = ~0;
    private bool corpseBloodPlayed;
    private readonly Queue<GameObject> characterBloodStains = new Queue<GameObject>();
    public void PlayHitEffect(EnemyHealth.HitPart hitPart, Vector3 hitPoint, Vector3 hitNormal, Vector3 shotDirection, Transform hitTransform)
    {
        GameObject effectPrefab = GetHitEffect(hitPart);
        Vector3 safeNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : -transform.forward;
        if (enableImpactBurstVFX && effectPrefab != null)
        {
            Quaternion rotation = Quaternion.LookRotation(safeNormal);
            GameObject effect = Instantiate(effectPrefab, hitPoint + safeNormal * surfaceOffset, rotation * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward));
            float randomScale = Random.Range(Mathf.Min(hitVFXScaleRange.x, hitVFXScaleRange.y), Mathf.Max(hitVFXScaleRange.x, hitVFXScaleRange.y));
            effect.transform.localScale *= randomScale;
            ApplyBloodAppearance(effect);
            Destroy(effect, Mathf.Max(0.1f, hitVFXLifetime));
        }

        if (enableBloodRibbons && ribbonMaterial != null)
        {
            CreateBloodRibbons(hitPart, hitPoint, safeNormal, shotDirection);
        }

        if (enableHeadshotMist && hitPart == EnemyHealth.HitPart.Head)
        {
            CreateHeadshotMist(hitPoint, safeNormal);
        }

        if (enableCharacterBloodStains)
        {
            AttachBloodStains(hitPoint, safeNormal, hitTransform);
        }

        if (enableBloodDripping)
        {
            CreateDripEmitter(hitPoint, hitTransform);
        }
    }

    public void PlayCorpseBlood()
    {
        if (corpseBloodPlayed || corpseBloodVFX == null)
        {
            return;
        }

        corpseBloodPlayed = true;
        StartCoroutine(SpawnCorpseBloodAfterDelay());
    }

    private GameObject GetHitEffect(EnemyHealth.HitPart hitPart)
    {
        switch (hitPart)
        {
            case EnemyHealth.HitPart.Head:
                return GetRandomVariation(headHitVFXVariations, headHitVFX);
            case EnemyHealth.HitPart.Torso:
                return GetRandomVariation(torsoHitVFXVariations, torsoHitVFX);
            case EnemyHealth.HitPart.LeftArm:
            case EnemyHealth.HitPart.RightArm:
            case EnemyHealth.HitPart.LeftLeg:
            case EnemyHealth.HitPart.RightLeg:
                return GetRandomVariation(limbHitVFXVariations, limbHitVFX);
            default:
                return null;
        }
    }

    private GameObject GetRandomVariation(GameObject[] variations, GameObject fallback)
    {
        if (variations == null || variations.Length == 0)
        {
            return fallback;
        }

        int startIndex = Random.Range(0, variations.Length);
        for (int offset = 0; offset < variations.Length; offset++)
        {
            GameObject candidate = variations[(startIndex + offset) % variations.Length];
            if (candidate != null)
            {
                return candidate;
            }
        }

        return fallback;
    }

    private void CreateBloodRibbons(EnemyHealth.HitPart hitPart, Vector3 hitPoint, Vector3 hitNormal, Vector3 shotDirection)
    {
        Vector2Int countRange = GetRibbonCount(hitPart);
        int minimum = Mathf.Max(0, Mathf.Min(countRange.x, countRange.y));
        int maximum = Mathf.Max(minimum, Mathf.Max(countRange.x, countRange.y));
        int count = Random.Range(minimum, maximum + 1);
        Vector3 safeShotDirection = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : -hitNormal;
        Vector3 baseDirection = Vector3.Slerp(hitNormal, safeShotDirection, 0.25f).normalized;
        if (baseDirection.sqrMagnitude < 0.0001f)
        {
            baseDirection = hitNormal;
        }

        for (int index = 0; index < count; index++)
        {
            float spread = Random.Range(0f, Mathf.Max(0f, ribbonSpreadAngle));
            Vector3 spreadAxis = Vector3.Cross(baseDirection, Random.onUnitSphere);
            if (spreadAxis.sqrMagnitude < 0.001f)
            {
                spreadAxis = Vector3.up;
            }

            Vector3 direction = Quaternion.AngleAxis(spread, spreadAxis.normalized) * baseDirection;
            GameObject ribbonObject = new GameObject("3D Blood Ribbon");
            ribbonObject.transform.position = hitPoint + hitNormal * (surfaceOffset + 0.01f);
            BloodRibbonProjectile ribbon = ribbonObject.AddComponent<BloodRibbonProjectile>();
            ribbon.Initialize(transform, ribbonMaterial, bloodDecalMaterials, GetRandomBloodColor(), direction * Random.Range(Mathf.Min(ribbonSpeedRange.x, ribbonSpeedRange.y), Mathf.Max(ribbonSpeedRange.x, ribbonSpeedRange.y)), Random.Range(Mathf.Min(ribbonWidthRange.x, ribbonWidthRange.y), Mathf.Max(ribbonWidthRange.x, ribbonWidthRange.y)), Random.Range(Mathf.Min(ribbonLifetimeRange.x, ribbonLifetimeRange.y), Mathf.Max(ribbonLifetimeRange.x, ribbonLifetimeRange.y)), ribbonGravity, Random.Range(Mathf.Min(ribbonWiggleStrengthRange.x, ribbonWiggleStrengthRange.y), Mathf.Max(ribbonWiggleStrengthRange.x, ribbonWiggleStrengthRange.y)), Random.Range(Mathf.Min(ribbonWiggleFrequencyRange.x, ribbonWiggleFrequencyRange.y), Mathf.Max(ribbonWiggleFrequencyRange.x, ribbonWiggleFrequencyRange.y)), ribbonVelocityDamping, ribbonCollisionLayers, decalScaleRange, bloodDecalLifetime, maximumBloodDecals);
        }
    }

    private void CreateHeadshotMist(Vector3 hitPoint, Vector3 hitNormal)
    {
        GameObject mistObject = new GameObject("Headshot Blood Mist");
        mistObject.transform.SetPositionAndRotation(hitPoint + hitNormal * (surfaceOffset + 0.005f), Quaternion.LookRotation(hitNormal));
        ParticleSystem mist = mistObject.AddComponent<ParticleSystem>();
        mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = mist.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(Mathf.Min(headshotMistLifetimeRange.x, headshotMistLifetimeRange.y), Mathf.Max(headshotMistLifetimeRange.x, headshotMistLifetimeRange.y));
        main.startSpeed = new ParticleSystem.MinMaxCurve(Mathf.Min(headshotMistSpeedRange.x, headshotMistSpeedRange.y), Mathf.Max(headshotMistSpeedRange.x, headshotMistSpeedRange.y));
        main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Min(headshotMistSizeRange.x, headshotMistSizeRange.y), Mathf.Max(headshotMistSizeRange.x, headshotMistSizeRange.y));
        Color darkBlood = Color.Lerp(bloodColor, Color.black, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(darkBlood, bloodColor);
        main.gravityModifier = 0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(headshotMistParticleCount.x, headshotMistParticleCount.y) + Mathf.Max(headshotMistSecondaryCount.x, headshotMistSecondaryCount.y) + 16;
        ParticleSystem.EmissionModule emission = mist.emission;
        emission.rateOverTime = 0f;
        short minimumCount = (short)Mathf.Clamp(Mathf.Min(headshotMistParticleCount.x, headshotMistParticleCount.y), 1, short.MaxValue);
        short maximumCount = (short)Mathf.Clamp(Mathf.Max(headshotMistParticleCount.x, headshotMistParticleCount.y), minimumCount, short.MaxValue);
        short secondaryMinimumCount = (short)Mathf.Clamp(Mathf.Min(headshotMistSecondaryCount.x, headshotMistSecondaryCount.y), 1, short.MaxValue);
        short secondaryMaximumCount = (short)Mathf.Clamp(Mathf.Max(headshotMistSecondaryCount.x, headshotMistSecondaryCount.y), secondaryMinimumCount, short.MaxValue);
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, minimumCount, maximumCount), new ParticleSystem.Burst(0.04f, secondaryMinimumCount, secondaryMaximumCount) });
        ParticleSystem.ShapeModule shape = mist.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.001f, headshotMistRadius);
        shape.radiusThickness = 1f;
        shape.randomDirectionAmount = 0.35f;
        ParticleSystem.NoiseModule noise = mist.noise;
        noise.enabled = true;
        noise.strength = Mathf.Max(0f, headshotMistTurbulence);
        noise.frequency = 1.2f;
        noise.scrollSpeed = 0.35f;
        noise.damping = true;
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = mist.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.65f, 0.45f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fadeGradient;
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = mist.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.18f, 1f), new Keyframe(1f, 0.15f)));
        ParticleSystemRenderer mistRenderer = mistObject.GetComponent<ParticleSystemRenderer>();
        if (mistRenderer != null)
        {
            if (dripMaterial != null)
            {
                mistRenderer.sharedMaterial = dripMaterial;
            }

            ApplyBloodRendererProperties(mistRenderer);
        }

        mist.Play();
        Destroy(mistObject, Mathf.Max(headshotMistLifetimeRange.x, headshotMistLifetimeRange.y) + 0.5f);
    }

    private void AttachBloodStains(Vector3 hitPoint, Vector3 hitNormal, Transform hitTransform)
    {
        if (hitTransform == null || bloodDecalMaterials == null || bloodDecalMaterials.Length == 0 || maximumCharacterBloodStains <= 0)
        {
            return;
        }

        RemoveMissingCharacterStains();
        int minimum = Mathf.Max(0, Mathf.Min(characterStainsPerHit.x, characterStainsPerHit.y));
        int maximum = Mathf.Max(minimum, Mathf.Max(characterStainsPerHit.x, characterStainsPerHit.y));
        int count = Random.Range(minimum, maximum + 1);
        Vector3 tangent = Vector3.Cross(hitNormal, Vector3.up);
        if (tangent.sqrMagnitude < 0.001f)
        {
            tangent = Vector3.Cross(hitNormal, Vector3.right);
        }

        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(hitNormal, tangent).normalized;
        for (int index = 0; index < count; index++)
        {
            Material material = GetRandomMaterial(bloodDecalMaterials);
            if (material == null)
            {
                continue;
            }

            GameObject stain = GameObject.CreatePrimitive(PrimitiveType.Quad);
            stain.name = "Attached Blood Stain";
            stain.layer = 2;
            Vector2 scatter = Random.insideUnitCircle * 0.015f;
            stain.transform.position = hitPoint + hitNormal * characterStainSurfaceOffset + tangent * scatter.x + bitangent * scatter.y;
            stain.transform.rotation = Quaternion.LookRotation(hitNormal) * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
            float scale = Random.Range(Mathf.Min(characterStainScaleRange.x, characterStainScaleRange.y), Mathf.Max(characterStainScaleRange.x, characterStainScaleRange.y));
            stain.transform.localScale = new Vector3(scale * Random.Range(0.7f, 1.25f), scale * Random.Range(0.7f, 1.25f), 1f);
            stain.transform.SetParent(hitTransform, true);
            Collider stainCollider = stain.GetComponent<Collider>();
            if (stainCollider != null)
            {
                Destroy(stainCollider);
            }

            MeshRenderer stainRenderer = stain.GetComponent<MeshRenderer>();
            if (stainRenderer != null)
            {
                stainRenderer.sharedMaterial = material;
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                Color stainColor = GetRandomBloodColor();
                properties.SetColor("_Color", stainColor);
                properties.SetColor("_EmissionColor", stainColor * bloodEmission);
                properties.SetFloat("_Glossiness", 0.78f);
                properties.SetFloat("_Metallic", 0f);
                stainRenderer.SetPropertyBlock(properties);
            }

            characterBloodStains.Enqueue(stain);
            while (characterBloodStains.Count > maximumCharacterBloodStains)
            {
                GameObject oldest = characterBloodStains.Dequeue();
                if (oldest != null)
                {
                    Destroy(oldest);
                }
            }
        }
    }

    private static Material GetRandomMaterial(Material[] materials)
    {
        int startIndex = Random.Range(0, materials.Length);
        for (int offset = 0; offset < materials.Length; offset++)
        {
            Material candidate = materials[(startIndex + offset) % materials.Length];
            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private void RemoveMissingCharacterStains()
    {
        int count = characterBloodStains.Count;
        for (int index = 0; index < count; index++)
        {
            GameObject stain = characterBloodStains.Dequeue();
            if (stain != null)
            {
                characterBloodStains.Enqueue(stain);
            }
        }
    }

    private Vector2Int GetRibbonCount(EnemyHealth.HitPart hitPart)
    {
        if (hitPart == EnemyHealth.HitPart.Head)
        {
            return headRibbonCount;
        }

        if (hitPart == EnemyHealth.HitPart.Torso)
        {
            return torsoRibbonCount;
        }

        return limbRibbonCount;
    }

    private Color GetRandomBloodColor()
    {
        float brightness = Random.Range(0.78f, 1.12f);
        return new Color(Mathf.Clamp01(bloodColor.r * brightness), Mathf.Clamp01(bloodColor.g * brightness), Mathf.Clamp01(bloodColor.b * brightness), bloodColor.a);
    }

    private void CreateDripEmitter(Vector3 hitPoint, Transform hitTransform)
    {
        GameObject emitterObject = new GameObject("Blood Drip Emitter");
        emitterObject.transform.SetPositionAndRotation(hitPoint, Quaternion.LookRotation(Vector3.down));
        ParticleSystem particleSystem = emitterObject.AddComponent<ParticleSystem>();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = Mathf.Max(0.05f, dripDuration);
        main.loop = false;
        main.startLifetime = Mathf.Max(0.05f, dripLifetime);
        main.startSpeed = Mathf.Max(0f, dripSpeed);
        main.startSize = Mathf.Max(0.001f, dripSize);
        main.startColor = bloodColor;
        main.gravityModifier = Mathf.Max(0f, dripGravity);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(8, Mathf.CeilToInt(dripRate * dripDuration) + 4);
        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Max(0f, dripRate);
        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 3f;
        shape.radius = 0.008f;
        ParticleSystem.CollisionModule collision = particleSystem.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.collidesWith = groundLayers;
        collision.dampen = 0.2f;
        collision.bounce = 0f;
        collision.lifetimeLoss = 1f;
        ParticleSystemRenderer particleRenderer = emitterObject.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            if (dripMaterial != null)
            {
                particleRenderer.sharedMaterial = dripMaterial;
            }

            ApplyBloodRendererProperties(particleRenderer);
        }

        particleSystem.Play();
        Vector3 localHitPoint = hitTransform != null ? hitTransform.InverseTransformPoint(hitPoint) : Vector3.zero;
        StartCoroutine(FollowDripEmitter(emitterObject.transform, hitTransform, localHitPoint));
        Destroy(emitterObject, Mathf.Max(0.1f, dripDuration + dripLifetime + 0.5f));
    }

    private IEnumerator FollowDripEmitter(Transform emitter, Transform hitTransform, Vector3 localHitPoint)
    {
        float endTime = Time.time + Mathf.Max(0.05f, dripDuration);
        while (emitter != null && hitTransform != null && Time.time < endTime)
        {
            emitter.position = hitTransform.TransformPoint(localHitPoint);
            emitter.rotation = Quaternion.LookRotation(Vector3.down);
            yield return null;
        }
    }

    private IEnumerator SpawnCorpseBloodAfterDelay()
    {
        if (corpseBloodDelay > 0f)
        {
            yield return new WaitForSeconds(corpseBloodDelay);
        }

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 spawnPoint = transform.position;
        Vector3 groundNormal = Vector3.up;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, Mathf.Max(0.5f, groundRayDistance), groundLayers, QueryTriggerInteraction.Ignore))
        {
            spawnPoint = groundHit.point + groundHit.normal * surfaceOffset;
            groundNormal = groundHit.normal;
        }

        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
        GameObject corpseEffect = Instantiate(corpseBloodVFX, spawnPoint, rotation);
        ApplyBloodAppearance(corpseEffect);
        Destroy(corpseEffect, Mathf.Max(0.1f, corpseBloodLifetime));
    }

    private void ApplyBloodAppearance(GameObject effectRoot)
    {
        if (effectRoot == null)
        {
            return;
        }

        foreach (ParticleSystem particleSystem in effectRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.startColor = bloodColor;
        }

        foreach (ParticleSystemRenderer particleRenderer in effectRoot.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            ApplyBloodRendererProperties(particleRenderer);
        }
    }

    private void ApplyBloodRendererProperties(ParticleSystemRenderer particleRenderer)
    {
        if (particleRenderer == null)
        {
            return;
        }

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        particleRenderer.GetPropertyBlock(properties);
        properties.SetFloat("_FlatColor", 1f);
        properties.SetFloat("_EmissionMultiply", Mathf.Max(0f, bloodEmission));
        particleRenderer.SetPropertyBlock(properties);
    }
}
