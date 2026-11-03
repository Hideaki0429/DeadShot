// 【役割】メニュー画面で流すBGMの再生を管理する。
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MenuScreenBGM : MonoBehaviour
{
    AudioSource source;
    float level;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0;
        source.ignoreListenerPause = true;
        source.volume = 0;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        source.Stop();
        level = 0;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        var bank = GameSoundService.Bank;
        if (bank == null)
        {
            return;
        }

        bool open = false;
        foreach (var ui in FindObjectsOfType<InventoryUI>())
        {
            if (ui.isActiveAndEnabled && ui.IsOpen)
            {
                open = true;
                break;
            }
        }

        if (!open)
        {
            foreach (var ui in FindObjectsOfType<KeyBindingSettingsUI>())
            {
                if (ui.isActiveAndEnabled)
                {
                    open = true;
                    break;
                }
            }
        }

        if (LoadingSceneRequest.IsLoading)
        {
            open = false;
        }

        source.outputAudioMixerGroup = bank.ambienceGroup;
        if (source.clip != bank.menuBgm)
        {
            source.Stop();
            source.clip = bank.menuBgm;
        }

        level = Mathf.MoveTowards(level, open ? 1 : 0, Time.unscaledDeltaTime / Mathf.Max(.01f, bank.menuBgmFadeSeconds));
        source.volume = bank.menuBgmVolume * level;
        if (source.clip != null && level > 0)
        {
            if (!source.isPlaying)
            {
                source.Play();
            }
        }
        else
        {
            source.Stop();
        }
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        if (source != null)
        {
            source.Stop();
        }

        level = 0;
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
    }
}
