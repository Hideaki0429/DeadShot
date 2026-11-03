// 【役割】タイトルメニューの選択、決定と画面遷移を管理する。
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public sealed class TitleMenuController : MonoBehaviour
{
    public GameObject menu;
    public KeyBindingSettingsUI settings;
    public RectTransform accent;
    public RectTransform[] rows;
    public TMP_Text[] labels;
    public RawImage background;
    public VideoPlayer video;
    public AudioSource uiAudio;
    public AudioClip moveSound, confirmSound;
    public string stageScene = "Stage_city", loadingScene = "Loading";
    public int selectedIndex;
    RenderTexture target;
    bool loading;
    int blockedFrame;
    bool waitForRelease;
    bool cursorReleasedByPlayer;
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        Time.timeScale = 1;
        AudioListener.pause = false;
        // タイトル開始時はカーソルを隠す。必要な場合だけPで再表示する。
        cursorReleasedByPlayer = false;
        HideCursor();
        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.SetPaused(false);
        }

        target = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
        target.Create();
        video.targetTexture = target;
        video.prepareCompleted += Prepared;
        video.errorReceived += VideoError;
        video.Prepare();
        settings.gameObject.SetActive(false);
        Select(0, false);
        RefreshContinueAppearance();
        blockedFrame = Time.frameCount;
    }

    void Prepared(VideoPlayer p)
    {
        background.texture = target;
        p.Play();
    }

    void VideoError(VideoPlayer p, string message)
    {
        Debug.LogWarning("[Title] 背景動画: " + message, this);
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            cursorReleasedByPlayer = true;
            ShowCursor();
        }

        if (LoadingSceneRequest.IsLoading)
        {
            blockedFrame = Time.frameCount;
            waitForRelease = true;
            return;
        }

        if (loading || settings.gameObject.activeSelf || Time.frameCount <= blockedFrame)
        {
            return;
        }

        if (waitForRelease)
        {
            if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Escape))
            {
                return;
            }

            waitForRelease = false;
            return;
        }

        var input = GameInputManager.Instance;
        int delta = input != null ? input.UiVertical : 0;
        if (delta == 0)
        {
            delta = Input.GetKeyDown(KeyCode.DownArrow) ? 1 : Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 0;
        }

        if (Mathf.Abs(Input.mouseScrollDelta.y) > .01f)
        {
            delta = Input.mouseScrollDelta.y < 0 ? 1 : -1;
        }

        if (delta != 0)
        {
            Navigate(delta);
        }

        if ((input != null && input.UiConfirmPressed) || Input.GetMouseButtonDown(0))
        {
            Confirm();
        }
    }

    public void Navigate(int direction)
    {
        if (!LoadingSceneRequest.IsLoading && direction != 0)
        {
            Select((selectedIndex + (direction > 0 ? 1 : 2)) % 3, true);
        }
    }

    public void Select(int index, bool sound = true)
    {
        if (loading || settings.gameObject.activeSelf)
        {
            return;
        }

        selectedIndex = Mathf.Clamp(index, 0, 2);
        accent.anchoredPosition = rows[selectedIndex].anchoredPosition;
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].color = i == selectedIndex ? Color.white : new Color(.64f, .67f, .68f);
        }

        if (sound)
        {
            PlaySound(false);
        }
    }

    public void Confirm()
    {
        if (LoadingSceneRequest.IsLoading || loading || settings.gameObject.activeSelf)
        {
            return;
        }

        PlaySound(true);
        blockedFrame = Time.frameCount;
        waitForRelease = true;
        if (selectedIndex == 0)
        {
            StartCoroutine(BeginGame());
        }
        else if (selectedIndex == 1)
        {
            if (!GameProgressData.CanContinue)
            {
                PlaySound(false);
                Debug.Log("[Title] CONTINUEはステージ１クリア後に使用できます。", this);
                return;
            }

            GameProgressData.SetNextStartMode(GameProgressData.StartMode.ResumeAfterClear);
            StartCoroutine(BeginGame());
        }
        else
        {
            menu.SetActive(false);
            settings.gameObject.SetActive(true);
            ShowCursor();
        }
    }

    void RefreshContinueAppearance()
    {
        if (labels == null || labels.Length < 2 || labels[1] == null)
        {
            return;
        }

        labels[1].alpha = GameProgressData.CanContinue ? 1f : .35f;
    }

    IEnumerator BeginGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(loadingScene) || !Application.CanStreamedLevelBeLoaded(stageScene))
        {
            Debug.LogError("[Title] Loading/本編シーンをBuild Settingsで確認してください。", this);
            yield break;
        }

        loading = true;
        yield return new WaitForSecondsRealtime(.2f);
        Time.timeScale = 1;
        AudioListener.pause = false;
        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.SetPaused(false);
        }

        LoadingSceneRequest.Begin(stageScene);
        SceneManager.LoadScene(loadingScene);
    }

    public void CloseSettings()
    {
        settings.gameObject.SetActive(false);
        menu.SetActive(true);
        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.SetPaused(false);
        }

        blockedFrame = Time.frameCount;
        waitForRelease = true;
        Select(selectedIndex, false);
        if (!cursorReleasedByPlayer)
        {
            HideCursor();
        }
    }

    public void PlaySound(bool confirm)
    {
        var clip = confirm ? confirmSound : moveSound;
        if (uiAudio != null && clip != null)
        {
            uiAudio.PlayOneShot(clip);
        }
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        // 他の常駐カーソル制御より後に、タイトルの表示方針を確定する。
        if (settings != null && settings.gameObject.activeSelf)
        {
            ShowCursor();
        }
        else if (cursorReleasedByPlayer)
        {
            ShowCursor();
        }
        else
        {
            HideCursor();
        }
    }

    static void HideCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    static void ShowCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (video != null)
        {
            video.prepareCompleted -= Prepared;
            video.errorReceived -= VideoError;
            video.Stop();
            video.targetTexture = null;
        }

        if (target != null)
        {
            target.Release();
            Destroy(target);
        }
    }
}
