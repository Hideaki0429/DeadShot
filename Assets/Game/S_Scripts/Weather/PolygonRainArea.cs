// 【役割】雨パーティクルの範囲と降雨表現を管理する。
using System.Collections.Generic;
using UnityEngine;

public sealed class PolygonRainArea : MonoBehaviour
{
    [Header("範囲とParticle参照")]
    [SerializeField]
    private PolygonWallBuilder polygonWall;
    [SerializeField]
    private ParticleSystem rainParticles;
    [Header("雨の発生設定")]
    [Tooltip("雨量の倍率。0で停止、1で現在と同じ、2以上で激しい雨になります。")]
    [Range(0f, 3f)]
    [SerializeField]
    private float rainIntensity = 1f;
    [Tooltip("雨粒の落下速度倍率")]
    [Range(0.1f, 3f)]
    [SerializeField]
    private float fallSpeedMultiplier = 1f;
    [Tooltip("雨粒の大きさ倍率")]
    [Range(0.1f, 3f)]
    [SerializeField]
    private float dropSizeMultiplier = 1f;
    [Min(0f)]
    [SerializeField]
    private float rainHeight = 30f;
    [Min(0f)]
    [SerializeField]
    private float particlesPerSquareMeter = 0.04f;
    [Min(1)]
    [SerializeField]
    private int maximumEmissionPerSecond = 6000;
    [Min(1)]
    [SerializeField]
    private int maximumParticles = 30000;
    [Header("開始設定")]
    [SerializeField]
    private bool playOnStart = true;
    [Header("雷雨の一括制御")]
    [SerializeField]
    private PolygonLightning lightning;
    [SerializeField]
    private bool stormEnabled = true;
    [Header("雨音（共通SE BankのRain Loopを使用）")]
    [SerializeField]
    private bool playRainSound = true;
    [Tooltip("X=Rain Intensity、Y=音量倍率")]
    [SerializeField]
    private AnimationCurve rainSoundVolumeByIntensity = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0.55f), new Keyframe(3f, 1f));
    [SerializeField]
    private Vector2 rainSoundPitchRange = new Vector2(0.9f, 1.08f);
    private readonly List<Vector2> polygon = new List<Vector2>();
    private Rect bounds;
    private float spawnHeight;
    private float emissionAccumulator;
    private float emissionRate;
    private float polygonArea;
    private bool areaReady;
    private float baseSimulationSpeed = 1f;
    private float baseStartSizeMultiplier = 1f;
    private float appliedIntensity = -1f;
    private float appliedFallSpeed = -1f;
    private float appliedDropSize = -1f;
    private AudioSource rainAudioSource;
    public bool StormEnabled => stormEnabled;

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        if (rainParticles == null)
        {
            rainParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        if (rainParticles != null)
        {
            ParticleSystem.MainModule originalMain = rainParticles.main;
            baseSimulationSpeed = originalMain.simulationSpeed;
            baseStartSizeMultiplier = originalMain.startSizeMultiplier;
        }

        PrepareParticleSystem();
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    private void Start()
    {
        RebuildRainArea();
        stormEnabled = playOnStart;
        SetStormActive(stormEnabled);
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        ApplyIntensitySettings();
        UpdateRainSound();
        if (!areaReady || rainParticles == null || !rainParticles.isPlaying)
        {
            return;
        }

        EmitRain(Time.deltaTime);
    }

    // コンポーネントが無効になったときの処理です。
    private void OnDisable()
    {
        if (rainAudioSource != null)
        {
            rainAudioSource.Stop();
        }
    }

    [ContextMenu("Rebuild Rain Area")]
    public void RebuildRainArea()
    {
        polygon.Clear();
        areaReady = false;
        if (polygonWall == null || polygonWall.points == null || polygonWall.points.Count < 3)
        {
            Debug.LogWarning("[Polygon Rain] PolygonWallBuilderの頂点が不足しています。", this);
            return;
        }

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;
        float highestY = float.NegativeInfinity;
        foreach (Vector3 point in polygonWall.points)
        {
            polygon.Add(new Vector2(point.x, point.z));
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minZ = Mathf.Min(minZ, point.z);
            maxZ = Mathf.Max(maxZ, point.z);
            highestY = Mathf.Max(highestY, point.y);
        }

        bounds = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        spawnHeight = highestY + rainHeight;
        polygonArea = CalculatePolygonArea();
        RecalculateEmissionRate();
        emissionAccumulator = 0f;
        areaReady = polygonArea > 0.01f;
        Debug.Log($"[Polygon Rain] Area:{polygonArea:F1}m2 / " + $"Emission:{emissionRate:F0}/sec / Height:{spawnHeight:F1}", this);
    }

    public void SetRainIntensity(float intensity)
    {
        rainIntensity = Mathf.Clamp(intensity, 0f, 3f);
        ApplyIntensitySettings(true);
    }

    private void RecalculateEmissionRate()
    {
        // 基準雨量を安全上限で抑えた後にIntensityを掛けるため、
        // 既に上限へ達している広いマップでも激しさを変更できる。
        emissionRate = Mathf.Min(polygonArea * particlesPerSquareMeter, maximumEmissionPerSecond) * rainIntensity;
    }

    private void ApplyIntensitySettings(bool force = false)
    {
        if (rainParticles == null)
        {
            return;
        }

        if (!force && Mathf.Approximately(appliedIntensity, rainIntensity) && Mathf.Approximately(appliedFallSpeed, fallSpeedMultiplier) && Mathf.Approximately(appliedDropSize, dropSizeMultiplier))
        {
            return;
        }

        appliedIntensity = rainIntensity;
        appliedFallSpeed = fallSpeedMultiplier;
        appliedDropSize = dropSizeMultiplier;
        ParticleSystem.MainModule main = rainParticles.main;
        main.simulationSpeed = baseSimulationSpeed * fallSpeedMultiplier;
        main.startSizeMultiplier = baseStartSizeMultiplier * dropSizeMultiplier;
        RecalculateEmissionRate();
    }

    public void SetRainActive(bool active)
    {
        if (rainParticles == null)
        {
            return;
        }

        if (active)
        {
            if (!rainParticles.isPlaying)
            {
                rainParticles.Play();
            }
        }
        else
        {
            rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    [ContextMenu("雷雨 ON / OFF")]
    public void ToggleStorm()
    {
        SetStormActive(!stormEnabled);
    }

    public void SetStormActive(bool active)
    {
        stormEnabled = active;
        if (lightning == null)
        {
            lightning = FindFirstObjectByType<PolygonLightning>();
        }

        if (!Application.isPlaying)
        {
            playOnStart = active;
            if (lightning != null)
            {
                lightning.lightningEnabled = active;
            }

            return;
        }

        SetRainActive(active);
        if (lightning != null)
        {
            lightning.lightningEnabled = active;
        }

        UpdateRainSound();
    }

    private void UpdateRainSound()
    {
        bool shouldPlay = stormEnabled && playRainSound && rainIntensity > 0f;
        if (!shouldPlay)
        {
            if (rainAudioSource != null && rainAudioSource.isPlaying)
            {
                rainAudioSource.Stop();
            }

            return;
        }

        GameSoundBank bank = GameSoundService.Bank;
        GameSoundBank.Entry soundEntry = bank != null ? bank.Find(GameSound.RainLoop) : null;
        if (soundEntry == null || soundEntry.clips == null)
        {
            return;
        }

        if (rainAudioSource == null)
        {
            AudioClip[] clips = System.Array.FindAll(soundEntry.clips, clip => clip != null);
            if (clips.Length == 0)
            {
                return;
            }

            rainAudioSource = gameObject.AddComponent<AudioSource>();
            rainAudioSource.playOnAwake = false;
            rainAudioSource.loop = true;
            rainAudioSource.spatialBlend = 0f;
            rainAudioSource.clip = clips[Random.Range(0, clips.Length)];
            rainAudioSource.outputAudioMixerGroup = bank.ambienceGroup;
        }

        rainAudioSource.volume = Mathf.Clamp01(soundEntry.volume * Mathf.Max(0f, rainSoundVolumeByIntensity.Evaluate(rainIntensity)));
        rainAudioSource.pitch = Mathf.Lerp(rainSoundPitchRange.x, rainSoundPitchRange.y, Mathf.InverseLerp(0f, 3f, rainIntensity));
        if (!rainAudioSource.isPlaying)
        {
            rainAudioSource.Play();
        }
    }

    private void PrepareParticleSystem()
    {
        if (rainParticles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = rainParticles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maximumParticles;
        ApplyIntensitySettings(true);
        ParticleSystem.EmissionModule emission = rainParticles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = rainParticles.shape;
        shape.enabled = false;
        rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void EmitRain(float deltaTime)
    {
        emissionAccumulator += emissionRate * deltaTime;
        int emitCount = Mathf.FloorToInt(emissionAccumulator);
        emissionAccumulator -= emitCount;
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            velocity = Vector3.zero
        };
        for (int i = 0; i < emitCount; i++)
        {
            if (!TryGetRandomPointInsidePolygon(out Vector2 point))
            {
                continue;
            }

            emitParams.position = new Vector3(point.x, spawnHeight, point.y);
            rainParticles.Emit(emitParams, 1);
        }
    }

    private bool TryGetRandomPointInsidePolygon(out Vector2 point)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 candidate = new Vector2(Random.Range(bounds.xMin, bounds.xMax), Random.Range(bounds.yMin, bounds.yMax));
            if (IsInsidePolygon(candidate))
            {
                point = candidate;
                return true;
            }
        }

        point = default;
        return false;
    }

    private bool IsInsidePolygon(Vector2 point)
    {
        bool inside = false;
        int previous = polygon.Count - 1;
        for (int current = 0; current < polygon.Count; current++)
        {
            Vector2 a = polygon[current];
            Vector2 b = polygon[previous];
            bool crosses = (a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
            if (crosses)
            {
                inside = !inside;
            }

            previous = current;
        }

        return inside;
    }

    private float CalculatePolygonArea()
    {
        float twiceArea = 0f;
        int previous = polygon.Count - 1;
        for (int current = 0; current < polygon.Count; current++)
        {
            Vector2 a = polygon[previous];
            Vector2 b = polygon[current];
            twiceArea += a.x * b.y - b.x * a.y;
            previous = current;
        }

        return Mathf.Abs(twiceArea) * 0.5f;
    }
}
