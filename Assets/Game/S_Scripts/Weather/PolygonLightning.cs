// 【役割】稲光の発光と雷の演出タイミングを制御する。
using System.Collections;
using UnityEngine;

public sealed class PolygonLightning : MonoBehaviour
{
    [Header("マップ範囲・雷プレハブ")]
    public PolygonWallBuilder polygonWall;
    public GameObject[] lightningPrefabs;
    [Header("雷の発生設定")]
    public bool lightningEnabled = true;
    public Vector2 intervalSeconds = new Vector2(6, 12);
    [Min(0)]
    public float heightAboveMap = 35f;
    [Min(.01f)]
    public float effectScale = 1f;
    [Min(.05f)]
    public float emissionSeconds = .6f;
    [Min(.1f)]
    public float fadeOutSeconds = 3f;
    [Header("雷鳴SE")]
    [Tooltip("オンの場合、雷の発生地点から3D雷鳴を再生します。音源は共通SE BankのLightning Thunderです。")]
    public bool playThunder = true;
    [Tooltip("距離に応じて光った後に雷鳴が届くようにします。")]
    public bool useSpeedOfSoundDelay = true;
    [Min(1f)]
    public float speedOfSound = 343f;
    [Min(0f)]
    public float additionalThunderDelay;
    [Min(0f)]
    public float maximumThunderDelay = 5f;
    public bool logStrikes;
    private float timer;
    private GameObject activeEffect;
    private Coroutine strike;
    public int StrikeCount { get; private set; }

    // コンポーネントが有効になったときの処理です。
    void OnEnable()
    {
        ResetTimer();
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (!lightningEnabled)
        {
            if (activeEffect != null)
            {
                ClearStrike();
            }

            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0 && strike == null)
        {
            TriggerLightning();
            ResetTimer();
        }
    }

    void ResetTimer()
    {
        float min = Mathf.Max(.5f, intervalSeconds.x);
        timer = Random.Range(min, Mathf.Max(min, intervalSeconds.y));
    }

    [ContextMenu("雷をテスト再生（Play中）")]
    public void TriggerLightning()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || strike != null)
        {
            return;
        }

        if (lightningPrefabs == null || lightningPrefabs.Length == 0 || polygonWall == null || polygonWall.points.Count < 3)
        {
            return;
        }

        if (!TryPoint(out Vector3 point))
        {
            return;
        }

        int first = Random.Range(0, lightningPrefabs.Length);
        GameObject prefab = null;
        for (int i = 0; i < lightningPrefabs.Length; i++)
        {
            prefab = lightningPrefabs[(first + i) % lightningPrefabs.Length];
            if (prefab != null)
            {
                break;
            }
        }

        if (prefab != null)
        {
            strike = StartCoroutine(PlayStrike(prefab, point));
        }
    }

    IEnumerator PlayStrike(GameObject prefab, Vector3 point)
    {
        activeEffect = Instantiate(prefab, point, prefab.transform.rotation, transform);
        activeEffect.transform.localScale = prefab.transform.localScale * effectScale;
        activeEffect.SetActive(true);
        var particles = activeEffect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.stopAction = ParticleSystemStopAction.None;
        }

        // Preserve the prefab's emission, trails, noise, rotations and sub-emitter wiring.
        foreach (var ps in particles)
        {
            if (ps.transform.parent == null || ps.transform.parent.GetComponentInParent<ParticleSystem>() == null)
            {
                ps.Play(true);
            }
        }

        StrikeCount++;
        if (playThunder)
        {
            StartCoroutine(PlayThunder(point));
        }

        if (logStrikes)
        {
            Debug.Log("[Lightning] " + prefab.name + " at " + point, this);
        }

        yield return new WaitForSeconds(Mathf.Max(.05f, emissionSeconds));
        foreach (var ps in particles)
        {
            if (ps != null)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        yield return new WaitForSeconds(Mathf.Max(.1f, fadeOutSeconds));
        if (activeEffect != null)
        {
            Destroy(activeEffect);
        }

        activeEffect = null;
        strike = null;
    }

    IEnumerator PlayThunder(Vector3 point)
    {
        float delay = additionalThunderDelay;
        if (useSpeedOfSoundDelay)
        {
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener != null && listener.isActiveAndEnabled)
            {
                delay += Vector3.Distance(listener.transform.position, point) / Mathf.Max(1f, speedOfSound);
            }
        }

        if (maximumThunderDelay > 0f)
        {
            delay = Mathf.Min(delay, maximumThunderDelay);
        }

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (isActiveAndEnabled && lightningEnabled)
        {
            GameSoundService.Play(GameSound.LightningThunder, point);
        }
    }

    bool TryPoint(out Vector3 point)
    {
        point = Vector3.zero;
        var vertices = polygonWall.points;
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minZ = minX, maxZ = maxX, top = maxX;
        foreach (var v in vertices)
        {
            minX = Mathf.Min(minX, v.x);
            maxX = Mathf.Max(maxX, v.x);
            minZ = Mathf.Min(minZ, v.z);
            maxZ = Mathf.Max(maxZ, v.z);
            top = Mathf.Max(top, v.y);
        }

        for (int n = 0; n < 128; n++)
        {
            float x = Random.Range(minX, maxX), z = Random.Range(minZ, maxZ);
            bool inside = false;
            for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
            {
                var a = vertices[i];
                var b = vertices[j];
                if ((a.z > z) != (b.z > z) && x < (b.x - a.x) * (z - a.z) / (b.z - a.z) + a.x)
                {
                    inside = !inside;
                }
            }

            if (inside)
            {
                point = new Vector3(x, top + heightAboveMap, z);
                return true;
            }
        }

        return false;
    }

    void ClearStrike()
    {
        if (strike != null)
        {
            StopCoroutine(strike);
        }

        strike = null;
        if (activeEffect != null)
        {
            Destroy(activeEffect);
        }

        activeEffect = null;
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        ClearStrike();
    }
}
