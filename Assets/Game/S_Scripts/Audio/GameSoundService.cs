// 【役割】共通の効果音設定を使ってゲーム内の音を再生する。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>SEバンクと永続音量設定。既存のフェード・ピッチはMixerの手前で維持する。</summary>
public class GameSoundService : MonoBehaviour
{
    static GameSoundService instance;
    public static GameSoundBank Bank { get; private set; }

    readonly List<AudioSource> voices = new List<AudioSource>();
    readonly List<AudioSource> active = new List<AudioSource>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        Bank = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (instance != null)
        {
            return;
        }

        Bank = Resources.Load<GameSoundBank>("DeadShotSoundBank");
        instance = new GameObject("Dead Shot Audio").AddComponent<GameSoundService>();
        DontDestroyOnLoad(instance.gameObject);
        var menu = new GameObject("Inventory and Settings BGM");
        menu.transform.SetParent(instance.transform);
        menu.AddComponent<MenuScreenBGM>();
        SceneManager.sceneLoaded += instance.SceneLoaded;
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        ApplyVolumes();
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
    }

    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var source in active)
        {
            if (source != null)
            {
                Destroy(source.gameObject);
            }
        }

        active.Clear();
        voices.Clear();
        RouteSources();
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        RouteSources();
    }

    void RouteSources()
    {
        if (Bank == null || Bank.effectsGroup == null)
        {
            return;
        }

        // 既に別Mixerを使用する音源は上書きしない。動的生成される既存SEも収容する。
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (source.outputAudioMixerGroup == null)
            {
                source.outputAudioMixerGroup = SelectGroup(source);
            }
        }
    }

    static UnityEngine.Audio.AudioMixerGroup SelectGroup(AudioSource source)
    {
        var routing = source.GetComponent<GameAudioRouting>();
        if (routing != null)
        {
            return routing.category == GameAudioCategory.UI ? Bank.uiGroup : routing.category == GameAudioCategory.Voice ? Bank.voiceGroup : routing.category == GameAudioCategory.Ambience ? Bank.ambienceGroup : Bank.effectsGroup;
        }

        if (source.ignoreListenerPause)
        {
            return Bank.uiGroup;
        }

        if (source.GetComponentInParent<OpeningNewsSequence>() != null || source.GetComponentInParent<CaptainHoverDialogue>() != null)
        {
            return Bank.voiceGroup;
        }

        if (source.loop)
        {
            return Bank.ambienceGroup;
        }

        return Bank.effectsGroup;
    }

    public static float MasterVolume => PlayerPrefs.GetFloat("DeadShot.Audio.Master", 1);
    public static float EffectsVolume => PlayerPrefs.GetFloat("DeadShot.Audio.Effects", 1);

    public static void SetMasterVolume(float value)
    {
        PlayerPrefs.SetFloat("DeadShot.Audio.Master", Mathf.Clamp01(value));
        ApplyVolumes();
    }

    public static void SetEffectsVolume(float value)
    {
        PlayerPrefs.SetFloat("DeadShot.Audio.Effects", Mathf.Clamp01(value));
        ApplyVolumes();
    }

    public static void SaveVolumes()
    {
        PlayerPrefs.Save();
    }

    public static float GetCategoryVolume(GameAudioCategory category) => PlayerPrefs.GetFloat("DeadShot.Audio." + category, 1);
    public static void SetCategoryVolume(GameAudioCategory category, float value)
    {
        PlayerPrefs.SetFloat("DeadShot.Audio." + category, Mathf.Clamp01(value));
        ApplyVolumes();
    }

    static void ApplyVolumes()
    {
        if (Bank != null && Bank.mixer != null)
        {
            SetMixerVolume("MasterVolume", MasterVolume);
            SetMixerVolume("EffectsVolume", EffectsVolume);
            SetMixerVolume("UIVolume", GetCategoryVolume(GameAudioCategory.UI));
            SetMixerVolume("VoiceVolume", GetCategoryVolume(GameAudioCategory.Voice));
            SetMixerVolume("AmbienceVolume", GetCategoryVolume(GameAudioCategory.Ambience));
        }
    }

    static void SetMixerVolume(string parameter, float v) => Bank.mixer.SetFloat(parameter, v <= 0 ? -80 : 20 * Mathf.Log10(v));
    public static AudioSource Play(GameSound sound, Vector3 position, bool ignorePause = false)
    {
        if (instance == null || Bank == null)
        {
            return null;
        }

        var entry = Bank.Find(sound);
        if (entry == null || entry.clips == null)
        {
            return null;
        }

        var available = System.Array.FindAll(entry.clips, c => c != null);
        if (available.Length == 0)
        {
            return null;
        }

        instance.voices.RemoveAll(s => s == null || !s.isPlaying);
        if (sound == GameSound.ZombieVoice && instance.voices.Count >= Bank.maxSimultaneousVoices)
        {
            return null;
        }

        var go = new GameObject("SE " + sound);
        go.transform.SetParent(instance.transform);
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.ignoreListenerPause = ignorePause;
        source.clip = available[Random.Range(0, available.Length)];
        source.volume = entry.volume;
        source.pitch = Mathf.Clamp(Random.Range(entry.pitch.x, entry.pitch.y), 0.1f, 3);
        source.spatialBlend = entry.spatial ? 1 : 0;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1;
        source.maxDistance = entry.maxDistance;
        source.outputAudioMixerGroup = sound == GameSound.ZombieVoice || sound == GameSound.ZombieDeath ? Bank.voiceGroup : Bank.effectsGroup;
        source.Play();
        instance.active.Add(source);
        if (sound == GameSound.ZombieVoice)
        {
            instance.voices.Add(source);
        }

        instance.StartCoroutine(instance.Release(source, source.clip.length / source.pitch));
        return source;
    }

    IEnumerator Release(AudioSource source, float duration)
    {
        if (source != null && source.ignoreListenerPause)
        {
            yield return new WaitForSecondsRealtime(duration + 0.1f);
        }
        else
        {
            yield return new WaitForSeconds(duration + 0.1f);
        }

        active.Remove(source);
        if (source != null)
        {
            Destroy(source.gameObject);
        }
    }

    public static void Blood(Vector3 position)
    {
        if (instance != null)
        {
            instance.StartCoroutine(instance.DelayedBlood(position));
        }
    }

    IEnumerator DelayedBlood(Vector3 position)
    {
        int scene = SceneManager.GetActiveScene().handle;
        yield return new WaitForSeconds(Bank != null ? Bank.bloodDripDelay : 0.15f);
        if (SceneManager.GetActiveScene().handle == scene)
        {
            Play(GameSound.BloodDrip, position);
        }
    }
}
