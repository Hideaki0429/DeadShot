// 【役割】拠点・訓練エリア・ステージに応じて戦闘HUDを表示・フェードする。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>拠点ではスコア／体力HUDを隠し、射撃訓練と各ステージで表示します。</summary>
public sealed class CombatHudVisibilityController : MonoBehaviour
{
    static CombatHudVisibilityController instance;
    readonly List<CanvasGroup> groups = new List<CanvasGroup>();
    Coroutine fadeRoutine;
    [SerializeField, Min(.01f)]
    float trainingFadeDuration = .35f;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (instance != null)
        {
            return;
        }

        GameObject root = new GameObject("Combat HUD Visibility Controller");
        instance = root.AddComponent<CombatHudVisibilityController>();
        DontDestroyOnLoad(root);
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveGroups();
        bool isBase = scene.name.Equals("Stage_city", System.StringComparison.OrdinalIgnoreCase);
        bool isCombatStage = scene.name.StartsWith("Stage", System.StringComparison.OrdinalIgnoreCase) && !isBase;
        SetImmediate(isCombatStage);
    }

    void ResolveGroups()
    {
        groups.Clear();
        foreach (Transform candidate in FindObjectsOfType<Transform>(true))
        {
            string objectName = candidate.name.Trim();
            if (objectName != "ScoreUI" && objectName != "PlayerStatusHUD")
            {
                continue;
            }

            CanvasGroup group = candidate.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = candidate.gameObject.AddComponent<CanvasGroup>();
            }

            if (!groups.Contains(group))
            {
                groups.Add(group);
            }
        }
    }

    public static void ShowTrainingHud(bool visible)
    {
        if (instance == null)
        {
            return;
        }

        instance.ResolveGroups();
        instance.FadeTo(visible ? 1f : 0f);
    }

    void SetImmediate(bool visible)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        foreach (CanvasGroup group in groups)
        {
            if (group == null)
            {
                continue;
            }

            if (visible)
            {
                group.gameObject.SetActive(true);
            }

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }

    void FadeTo(float target)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    IEnumerator FadeRoutine(float target)
    {
        float[] starts = new float[groups.Count];
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] == null)
            {
                continue;
            }

            groups[i].gameObject.SetActive(true);
            starts[i] = groups[i].alpha;
        }

        float elapsed = 0f;
        while (elapsed < trainingFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / trainingFadeDuration);
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null)
                {
                    groups[i].alpha = Mathf.Lerp(starts[i], target, t);
                }
            }

            yield return null;
        }

        foreach (CanvasGroup group in groups)
        {
            if (group == null)
            {
                continue;
            }

            group.alpha = target;
            group.interactable = target > .99f;
            group.blocksRaycasts = target > .99f;
        }

        fadeRoutine = null;
    }
}
