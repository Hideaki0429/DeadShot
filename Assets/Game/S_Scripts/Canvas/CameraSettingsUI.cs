// 【役割】カメラ設定画面の項目表示と設定値の変更を管理する。
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[DefaultExecutionOrder(-949)]
public sealed class CameraSettingsUI : MonoBehaviour
{
    [Serializable]
    public class ViewDemo
    {
        public RawImage preview;
        [Tooltip("動画を再生していない時に表示する静止画です。")]
        public Texture posterImage;
        [Tooltip("選択中だけループ再生するデモ動画です。")]
        public VideoClip loopVideo;
        public Button selectButton;
        [HideInInspector]
        public VideoPlayer player;
        [HideInInspector]
        public RenderTexture texture;
        [NonSerialized]
        public Coroutine playbackRequest;
    }

    public KeyBindingSettingsUI owner;
    public GameObject cameraLayout;
    [HideInInspector]
    public GameObject controlsLayout;
    [Header("既存設定UI内で表示を切り替える要素")]
    public GameObject[] keyboardElements;
    public GameObject[] cameraElements;
    public RectTransform tabAccent;
    public TMP_Text footerGuide;
    private Vector2 originalTabAccent;
    private string originalGuide, originalMessage;
    private bool cameraActive;
    public TMP_FontAsset font;
    public Button controlsTab, cameraTab, audioTab, displayTab, backButton;
    public Button resetButton, applyButton;
    public RectTransform selectionAccent;
    public TMP_Text statusText;
    public ViewDemo firstPerson = new ViewDemo();
    public ViewDemo thirdPerson = new ViewDemo();
    [Tooltip("未指定なら実行中のシーンから自動検索します。")]
    public SwitchCamera switchCamera;
    public bool IsOpen => cameraActive;

    int selected;
    int enabledFrame;
    bool waitingForRelease;
    [HideInInspector]
    public int layoutVersion;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        if (firstPerson.selectButton != null)
        {
            firstPerson.selectButton.onClick.AddListener(() => Select(0, true));
        }

        if (thirdPerson.selectButton != null)
        {
            thirdPerson.selectButton.onClick.AddListener(() => Select(1, true));
        }

        // Shared tabs and footer buttons are routed by KeyBindingSettingsUI.
        SetVisible(cameraElements, false);
        if (cameraLayout != null)
        {
            cameraLayout.SetActive(false);
        }
    }

    public void Open()
    {
        if (cameraActive)
        {
            return;
        }

        if (switchCamera == null)
        {
            switchCamera = FindObjectOfType<SwitchCamera>(true);
        }

        selected = (switchCamera != null ? switchCamera.DefaultFirstPersonView : PlayerPrefs.GetInt("DeadShot.DefaultFirstPersonAim", 0) == 1) ? 0 : 1;
        originalGuide = footerGuide != null ? footerGuide.text : "";
        originalMessage = statusText != null ? statusText.text : "";
        if (tabAccent != null)
        {
            originalTabAccent = tabAccent.anchoredPosition;
            tabAccent.anchoredPosition = originalTabAccent + new Vector2(((RectTransform)cameraTab.transform).anchoredPosition.x - ((RectTransform)controlsTab.transform).anchoredPosition.x, 0);
        }

        cameraActive = true;
        SetVisible(keyboardElements, false);
        SetVisible(cameraElements, true);
        if (footerGuide != null)
        {
            footerGuide.text = "← → / ホイール：選択    Enter：適用    Esc：戻る";
        }

        if (applyButton != null)
        {
            applyButton.GetComponentInChildren<TMP_Text>().text = "適用";
        }

        enabledFrame = Time.frameCount;
        waitingForRelease = true;
        Select(selected, false);
    }

    public void Close()
    {
        StopDemo(firstPerson);
        StopDemo(thirdPerson);
        if (!cameraActive)
        {
            return;
        }

        cameraActive = false;
        SetVisible(cameraElements, false);
        SetVisible(keyboardElements, true);
        if (tabAccent != null)
        {
            tabAccent.anchoredPosition = originalTabAccent;
        }

        if (footerGuide != null)
        {
            footerGuide.text = originalGuide;
        }

        if (statusText != null)
        {
            statusText.text = originalMessage;
        }

        if (owner != null)
        {
            owner.RefreshAfterCameraTab();
        }
    }

    static void SetVisible(GameObject[] elements, bool visible)
    {
        if (elements != null)
        {
            foreach (var element in elements)
            {
                if (element != null)
                {
                    element.SetActive(visible);
                }
            }
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (waitingForRelease)
        {
            if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetMouseButton(0))
            {
                return;
            }

            waitingForRelease = false;
        }

        if (Time.frameCount <= enabledFrame + 1)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        int delta = Input.GetKeyDown(KeyCode.RightArrow) ? 1 : Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 0;
        if (Mathf.Abs(Input.mouseScrollDelta.y) > .01f)
        {
            delta = Input.mouseScrollDelta.y > 0 ? -1 : 1;
        }

        if (delta != 0)
        {
            Select((selected + delta + 2) % 2, true);
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Apply();
        }
    }

    public void HoverSelect(int index)
    {
        if (IsOpen && index != selected)
        {
            Select(index, true);
        }
    }

    void Select(int index, bool sound)
    {
        selected = Mathf.Clamp(index, 0, 1);
        if (selectionAccent != null)
        {
            var target = selected == 0 ? firstPerson.selectButton : thirdPerson.selectButton;
            if (target != null)
            {
                selectionAccent.position = target.transform.position;
            }
        }

        PlayOnly(selected == 0 ? firstPerson : thirdPerson, selected == 0 ? thirdPerson : firstPerson);
        if (statusText != null)
        {
            statusText.text = selected == 0 ? "一人称視点で構え始めます。Enter：適用" : "三人称視点で構え始めます。Enter：適用";
        }

        if (sound && owner != null)
        {
            owner.PlayNavigationSound(false);
        }
    }

    public void Apply()
    {
        if (switchCamera == null)
        {
            switchCamera = FindObjectOfType<SwitchCamera>(true);
        }

        if (switchCamera != null)
        {
            switchCamera.SetDefaultFirstPersonView(selected == 0, true);
        }
        else
        {
            PlayerPrefs.SetInt("DeadShot.DefaultFirstPersonAim", selected == 0 ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (owner != null)
        {
            owner.PlayNavigationSound(true);
        }

        if (statusText != null)
        {
            statusText.text = selected == 0 ? "一人称視点を保存しました。" : "三人称視点を保存しました。";
        }
    }

    public void ResetDefault()
    {
        selected = 1;
        if (switchCamera == null)
        {
            switchCamera = FindObjectOfType<SwitchCamera>(true);
        }

        if (switchCamera != null)
        {
            switchCamera.SetDefaultFirstPersonView(false, true);
        }
        else
        {
            PlayerPrefs.SetInt("DeadShot.DefaultFirstPersonAim", 0);
            PlayerPrefs.Save();
        }

        Select(1, false);
        if (statusText != null)
        {
            statusText.text = "初期設定（三人称視点）に戻しました。";
        }

        if (owner != null)
        {
            owner.PlayNavigationSound(true);
        }
    }

    void PlayOnly(ViewDemo active, ViewDemo inactive)
    {
        StopDemo(inactive);
        StartDemo(active);
    }

    void StartDemo(ViewDemo demo)
    {
        if (demo == null || demo.preview == null)
        {
            return;
        }

        if (demo.playbackRequest != null || (demo.player != null && demo.player.isPlaying && demo.player.clip == demo.loopVideo))
        {
            return;
        }

        StopDemo(demo);
        if (demo.loopVideo == null)
        {
            return;
        }

        if (demo.player == null)
        {
            demo.texture = new RenderTexture(1024, 576, 0, RenderTextureFormat.ARGB32);
            demo.texture.Create();
            // Keep the decoder on the controller, independent of preview visibility.
            demo.player = gameObject.AddComponent<VideoPlayer>();
            demo.player.playOnAwake = false;
            demo.player.isLooping = true;
            demo.player.renderMode = VideoRenderMode.RenderTexture;
            demo.player.audioOutputMode = VideoAudioOutputMode.None;
            demo.player.targetTexture = demo.texture;
            demo.player.clip = demo.loopVideo;
        }

        demo.playbackRequest = StartCoroutine(PrepareDemo(demo));
    }

    IEnumerator PrepareDemo(ViewDemo demo)
    {
        // Allow Stop to settle before issuing a new asynchronous preparation.
        yield return null;
        var player = demo.player;
        player.clip = demo.loopVideo;
        player.Prepare();
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!player.isPrepared)
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                player.Stop();
                demo.playbackRequest = null;
                Debug.LogWarning("[CameraSettings] デモ動画を準備できませんでした。動画形式を確認してください。", this);
                yield break;
            }

            yield return null;
        }

        if (IsOpen && demo == (selected == 0 ? firstPerson : thirdPerson))
        {
            demo.preview.color = Color.white;
            demo.preview.texture = demo.texture;
            player.Play();
        }

        demo.playbackRequest = null;
    }

    void StopDemo(ViewDemo demo)
    {
        if (demo == null || demo.preview == null)
        {
            return;
        }

        if (demo.playbackRequest != null)
        {
            StopCoroutine(demo.playbackRequest);
            demo.playbackRequest = null;
        }

        if (demo.player != null)
        {
            demo.player.Stop();
        }

        SetPoster(demo);
    }

    void SetPoster(ViewDemo demo)
    {
        demo.preview.texture = demo.posterImage;
        demo.preview.color = demo.posterImage != null ? Color.white : Panel;
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        Close();
        StopDemo(firstPerson);
        StopDemo(thirdPerson);
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        Release(firstPerson);
        Release(thirdPerson);
    }

    static void Release(ViewDemo demo)
    {
        if (demo == null)
        {
            return;
        }

        if (demo.texture != null)
        {
            demo.texture.Release();
            Destroy(demo.texture);
        }
    }

    // Called once by the editor setup. All fields remain scene-editable afterwards.
    public void Build()
    {
        cameraLayout = Rect("Camera Settings Layout", transform, 0, 0, 1600, 900).gameObject;
        var root = cameraLayout.transform;
        Image bg = cameraLayout.AddComponent<Image>();
        bg.color = Color.black;
        Label(root, "OPTION", 35, 32, 430, 55, 46);
        Label(root, "設定", 38, 93, 220, 35, 24);
        controlsTab = ButtonAt(root, "CONTROLS / 操作", 550, 90, 240, 48);
        cameraTab = ButtonAt(root, "CAMERA / カメラ", 805, 90, 240, 48);
        audioTab = ButtonAt(root, "AUDIO / 音声", 1060, 90, 240, 48);
        displayTab = ButtonAt(root, "DISPLAY / 表示", 1315, 90, 240, 48);
        Box(cameraTab.transform, "Selected Tab Line", 0, 44, 240, 4, Red);
        var title = Label(root, "DEFAULT AIM VIEW / 構え時の初期視点", 0, 168, 1600, 48, 31);
        title.alignment = TextAlignmentOptions.Center;
        firstPerson.preview = Preview(root, "First Person Demo", "FIRST PERSON VIEW / 一人称視点", 45, 230, 735, 400);
        thirdPerson.preview = Preview(root, "Third Person Demo", "THIRD PERSON VIEW / 三人称視点", 820, 230, 735, 400);
        firstPerson.selectButton = ButtonAt(root, ">   FIRST PERSON / 一人称", 45, 650, 735, 70);
        thirdPerson.selectButton = ButtonAt(root, "    THIRD PERSON / 三人称", 820, 650, 735, 70);
        AddHover(firstPerson.selectButton, 0);
        AddHover(thirdPerson.selectButton, 1);
        selectionAccent = Box(root, "Selection Accent", 45, 650, 5, 70, Red);
        var footer = Box(root, "Footer", 35, 755, 1530, 112, Panel);
        statusText = Label(footer, "一人称視点で構え始めます。", 18, 10, 830, 38, 21);
        Label(footer, "← → / ホイール：選択    Enter：適用    Esc：戻る", 18, 56, 850, 30, 18);
        resetButton = ButtonAt(footer, "初期設定に戻す", 900, 27, 250, 50);
        applyButton = ButtonAt(footer, "適用", 1170, 27, 150, 50);
        backButton = ButtonAt(footer, "戻る", 1340, 27, 160, 50);
        layoutVersion = 3;
    }

    static readonly Color Panel = new Color(.025f, .07f, .08f, 1f);
    static readonly Color Line = new Color(.29f, .48f, .54f, 1f);
    static readonly Color Red = new Color(.92f, .08f, .08f, 1f);
    static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(width, height);
        return r;
    }

    static RectTransform Box(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var r = Rect(name, parent, x, y, width, height);
        var image = r.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        var outline = r.gameObject.AddComponent<Outline>();
        outline.effectColor = Line;
        outline.effectDistance = new Vector2(1, -1);
        return r;
    }

    RawImage Preview(Transform parent, string name, string caption, float x, float y, float width, float height)
    {
        var frame = Box(parent, name, x, y, width, height, Panel);
        var raw = Rect("Preview", frame, 10, 48, width - 20, height - 58).gameObject.AddComponent<RawImage>();
        raw.color = Panel;
        raw.raycastTarget = false;
        Label(frame, caption, 18, 11, width - 36, 30, 21);
        return raw;
    }

    TMP_Text Label(Transform parent, string text, float x, float y, float width, float height, float size)
    {
        var r = Rect("Text", parent, x, y, width, height);
        var value = r.gameObject.AddComponent<TextMeshProUGUI>();
        value.font = font;
        value.text = text;
        value.fontSize = size;
        value.color = new Color(.9f, .91f, .88f);
        value.alignment = TextAlignmentOptions.MidlineLeft;
        value.raycastTarget = false;
        return value;
    }

    Button ButtonAt(Transform parent, string caption, float x, float y, float width, float height)
    {
        var r = Box(parent, caption, x, y, width, height, Panel);
        var image = r.GetComponent<Image>();
        image.raycastTarget = true;
        var button = r.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };
        var text = Label(r, caption, 0, 0, width, height, 22);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    void AddHover(Button button, int index)
    {
        var hover = button.gameObject.AddComponent<CameraSettingsHover>();
        hover.owner = this;
        hover.index = index;
    }
}
