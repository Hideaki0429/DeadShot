// 【役割】着陸後のプレイヤー位置に応じて遠距離の背景描画とライトを休止・復帰する。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CityDistanceVisibility : MonoBehaviour
{
    [Tooltip("着陸してプレイヤー操作が可能になってから遠方の背景描画を省きます。")]
    public bool distanceCulling = true;
    [Min(30f)]
    public float hideDistance = 160f;
    [Min(0f)]
    public float hysteresis = 20f;
    [Min(.1f)]
    public float checkInterval = .5f;
    [Tooltip("この距離より先にしか光が届かないライトを休止します。")]
    [Min(10f)]
    public float lightingDistance = 65f;
    [SerializeField]
    int backgroundRenderers;
    [SerializeField]
    int hiddenRenderers;
    [SerializeField]
    int sleepingLights;
    readonly List<Light> lights = new List<Light>();
    readonly HashSet<Light> sleeping = new HashSet<Light>();
    readonly List<Renderer> targets = new List<Renderer>();
    readonly HashSet<Renderer> hidden = new HashSet<Renderer>();
    UnityChan.UnityChanControlScriptWithRgidBody player;
    float nextCheck;
    bool landed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= Install;
        SceneManager.sceneLoaded += Install;
    }

    static void Install(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.Equals("Stage_city", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<CityDistanceVisibility>(true) != null)
            {
                return;
            }
        }

        var go = new GameObject("City Distance Visibility");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<CityDistanceVisibility>();
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.type != LightType.Directional && light.GetComponentInParent<Animator>() == null && light.GetComponentInParent<Rigidbody>() == null)
                {
                    lights.Add(light);
                }
            }
        }

        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                // Characters, pickups and moving physics objects keep their own visibility rules.
                if (r.GetComponentInParent<Animator>() != null || r.GetComponentInParent<Rigidbody>() != null || r.GetComponentInParent<InventoryPickup>() != null)
                {
                    continue;
                }

                targets.Add(r);
            }
        }

        backgroundRenderers = targets.Count;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (!distanceCulling)
        {
            Restore();
            return;
        }

        if (Time.unscaledTime < nextCheck)
        {
            return;
        }

        nextCheck = Time.unscaledTime + checkInterval;
        if (player == null)
        {
            player = FindObjectOfType<UnityChan.UnityChanControlScriptWithRgidBody>();
        }

        if (player == null)
        {
            return;
        }

        if (player.canControl)
        {
            landed = true;
        }

        if (!landed)
        {
            return;
        }

        Vector3 origin = player.transform.position;
        foreach (var r in targets)
        {
            if (r == null)
            {
                continue;
            }

            bool ours = hidden.Contains(r);
            if (!ours && r.forceRenderingOff)
            {
                continue;
            }

            float distance = ours ? hideDistance : hideDistance + hysteresis;
            bool hide = r.bounds.SqrDistance(origin) > distance * distance;
            if (hide == ours)
            {
                continue;
            }

            r.forceRenderingOff = hide;
            if (hide)
            {
                hidden.Add(r);
            }
            else
            {
                hidden.Remove(r);
            }
        }

        hiddenRenderers = hidden.Count;
        foreach (var light in lights)
        {
            if (light == null)
            {
                continue;
            }

            bool ours = sleeping.Contains(light);
            if (!ours && !light.enabled)
            {
                continue;
            }

            float distance = lightingDistance + light.range + (ours ? 0f : hysteresis);
            bool sleep = (light.transform.position - origin).sqrMagnitude > distance * distance;
            if (sleep == ours)
            {
                continue;
            }

            light.enabled = !sleep;
            if (sleep)
            {
                sleeping.Add(light);
            }
            else
            {
                sleeping.Remove(light);
            }
        }

        sleepingLights = sleeping.Count;
    }

    void Restore()
    {
        foreach (var r in hidden)
        {
            if (r != null)
            {
                r.forceRenderingOff = false;
            }
        }

        hidden.Clear();
        hiddenRenderers = 0;
        foreach (var light in sleeping)
        {
            if (light != null)
            {
                light.enabled = true;
            }
        }

        sleeping.Clear();
        sleepingLights = 0;
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        Restore();
    }
}
