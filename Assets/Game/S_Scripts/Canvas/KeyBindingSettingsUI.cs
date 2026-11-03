// 【役割】キー設定項目の選択とキー割り当ての変更を管理する。
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-950)]
public sealed class KeyBindingSettingsUI : MonoBehaviour
{
    [Serializable]
    public class KeyVisual
    {
        public KeyCode key;
        public Image image;
    }

    public PauseMenuController pauseMenu;
    public TitleMenuController titleMenu;
    public TMP_FontAsset font;
    public ScrollRect scroll;
    public RectTransform rowsRoot, accent;
    public TMP_Text operationText, promptText, messageText;
    public List<TMP_Text> values = new List<TMP_Text>();
    public List<Button> rowButtons = new List<Button>();
    public List<KeyVisual> keyboard = new List<KeyVisual>();
    public Button resetButton, applyButton, backButton;
    public Button[] tabs;
    [HideInInspector]
    public CameraSettingsUI cameraSettings;
    public int selectedIndex;
    public bool IsCapturing => capturing;
    public KeyCode[] PendingKeys => pending == null ? null : (KeyCode[])pending.Clone();

    static KeyBindingSettingsUI active;
    static int consumedFrame = -1;
    public static bool BlockPauseInput => consumedFrame == Time.frameCount || (active != null && active.isActiveAndEnabled && (active.capturing || active.waitForRelease || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)));

    KeyCode[] pending;
    bool capturing, ready, dirty;
    bool waitForRelease;
    int releaseFrame = -1;
    int captureFrame, enabledFrame;
    readonly HashSet<KeyCode> heldAtCapture = new HashSet<KeyCode>();
    static readonly Color Panel = new Color(.035f, .07f, .085f, 1);
    static readonly Color Line = new Color(.30f, .43f, .48f, 1);
    static readonly Color Red = new Color(.85f, .07f, .08f, 1);
    const float RowHeight = 48;
    TMP_Text extraKeyText;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        // New catalog actions automatically add rows without a fixed item limit.
        for (int i = rowButtons.Count; i < KeyboardBindingStore.Entries.Length; i++)
        {
            var row = Instantiate(rowButtons[0].gameObject, rowsRoot);
            row.name = "Binding " + KeyboardBindingStore.Entries[i].field;
            ((RectTransform)row.transform).anchoredPosition = new Vector2(0, -i * RowHeight);
            var texts = row.GetComponentsInChildren<TMP_Text>();
            texts[0].text = KeyboardBindingStore.Entries[i].label;
            rowButtons.Add(row.GetComponent<Button>());
            values.Add(texts[1]);
        }

        rowsRoot.sizeDelta = new Vector2(rowsRoot.sizeDelta.x, KeyboardBindingStore.Entries.Length * RowHeight);
        var keyboardPanel = transform.Find("Key Bindings Layout/Keyboard Panel");
        if (keyboardPanel != null)
        {
            extraKeyText = Label(keyboardPanel, "", 12, 275, 740, 26, 18);
            extraKeyText.color = Red;
        }

        for (int i = 0; i < rowButtons.Count; i++)
        {
            int index = i;
            rowButtons[i].onClick.AddListener(() =>
            {
                Select(index);
                BeginCapture();
            });
        }

        resetButton.onClick.AddListener(ResetDefaults);
        applyButton.onClick.AddListener(Apply);
        backButton.onClick.AddListener(Back);
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].onClick.AddListener(() =>
            {
                if (capturing)
                {
                    return;
                }

                Sound(false);
                if (index == 0 && cameraSettings != null && cameraSettings.IsOpen)
                {
                    cameraSettings.Close();
                    return;
                }

                if (index == 1 && cameraSettings != null)
                {
                    cameraSettings.Open();
                    return;
                }

                Debug.Log("[Settings] " + new[] { "操作", "カメラ", "音声", "表示" }[index] + " タブ選択" + (index == 0 ? "" : "（未実装）"));
            });
        }
    }

    // コンポーネントが有効になったときの処理です。
    void OnEnable()
    {
        active = this;
        enabledFrame = Time.frameCount;
        ready = false;
        capturing = false;
        dirty = false;
        waitForRelease = false;
        if (GameInputManager.Instance == null)
        {
            return;
        }

        pending = KeyboardBindingStore.Snapshot(GameInputManager.Instance);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, pending.Length - 1);
        messageText.text = "項目を選択して、割り当てるキーを変更します。";
        Refresh();
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        capturing = false;
        ready = false;
        pending = null;
        if (active == this)
        {
            active = null;
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (cameraSettings != null && cameraSettings.IsOpen)
        {
            return;
        }

        if (pending == null || (pauseMenu != null && pauseMenu.IsTransitioning))
        {
            return;
        }

        if (!ready)
        {
            if (waitForRelease)
            {
                if (Input.anyKey)
                {
                    releaseFrame = -1;
                    return;
                }

                if (releaseFrame < 0)
                {
                    releaseFrame = Time.frameCount;
                    return;
                }

                if (Time.frameCount <= releaseFrame)
                {
                    return;
                }

                waitForRelease = false;
            }

            if (Time.frameCount <= enabledFrame + 1 || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetMouseButton(0))
            {
                return;
            }

            ready = true;
            Refresh();
        }

        if (capturing)
        {
            consumedFrame = Time.frameCount;
            if (Time.frameCount <= captureFrame)
            {
                return;
            }

            heldAtCapture.RemoveWhere(k => !Input.GetKey(k));
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                capturing = false;
                WaitForRelease();
                Refresh();
                return;
            }

            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (KeyboardBindingStore.Allowed(key) && !heldAtCapture.Contains(key) && Input.GetKeyDown(key))
                {
                    TryAssign(key);
                    break;
                }
            }

            return;
        }

        if (titleMenu != null && Input.GetKeyDown(KeyCode.Escape))
        {
            Back();
            return;
        }

        int direction = Input.GetKeyDown(KeyCode.DownArrow) ? 1 : Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 0;
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > .01f)
        {
            direction = wheel < 0 ? 1 : -1;
        }

        if (direction != 0)
        {
            Select(Mathf.Clamp(selectedIndex + direction, 0, pending.Length - 1));
            consumedFrame = Time.frameCount;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            BeginCapture();
            consumedFrame = Time.frameCount;
        }
    }

    public void Select(int index)
    {
        if (capturing || pending == null)
        {
            return;
        }

        selectedIndex = Mathf.Clamp(index, 0, pending.Length - 1);
        if (EventSystem.current != null && selectedIndex < rowButtons.Count)
        {
            EventSystem.current.SetSelectedGameObject(rowButtons[selectedIndex].gameObject);
        }

        Sound(false);
        Refresh();
    }

    public void BeginCapture()
    {
        if (!ready || capturing || pending == null)
        {
            return;
        }

        capturing = true;
        captureFrame = Time.frameCount;
        consumedFrame = Time.frameCount;
        heldAtCapture.Clear();
        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKey(key))
            {
                heldAtCapture.Add(key);
            }
        }

        Sound(true);
        Refresh();
    }

    public bool TryAssign(KeyCode key)
    {
        if (pending == null)
        {
            return false;
        }

        if (!KeyboardBindingStore.Allowed(key))
        {
            return false;
        }

        for (int i = 0; i < pending.Length; i++)
        {
            if (i != selectedIndex && pending[i] == key && !KeyboardBindingStore.CanShare(selectedIndex, i))
            {
                messageText.text = "「" + KeyboardBindingStore.Entries[i].label + "」が使用中です。別のキーを押してください。";
                return false;
            }
        }

        pending[selectedIndex] = key;
        dirty = true;
        capturing = false;
        WaitForRelease();
        consumedFrame = Time.frameCount;
        messageText.text = "未適用の変更があります。「適用」で保存してください。";
        Sound(true);
        Refresh();
        return true;
    }

    public void ResetDefaults()
    {
        if (cameraSettings != null && cameraSettings.IsOpen)
        {
            cameraSettings.ResetDefault();
            return;
        }

        if (capturing || KeyboardBindingStore.Defaults == null)
        {
            return;
        }

        pending = KeyboardBindingStore.Defaults;
        dirty = true;
        messageText.text = "初期設定に戻しました。「適用」で保存してください。";
        Sound(true);
        Refresh();
    }

    void WaitForRelease()
    {
        ready = false;
        waitForRelease = true;
        releaseFrame = -1;
    }

    public void Apply()
    {
        if (cameraSettings != null && cameraSettings.IsOpen)
        {
            cameraSettings.Apply();
            consumedFrame = Time.frameCount;
            return;
        }

        if (capturing || pending == null)
        {
            return;
        }

        if (KeyboardBindingStore.Apply(GameInputManager.Instance, pending, out string error))
        {
            dirty = false;
            messageText.text = "キー設定を保存・適用しました。";
            Sound(true);
        }
        else
        {
            messageText.text = error;
        }

        consumedFrame = Time.frameCount;
        Refresh();
    }

    public void PlayNavigationSound(bool confirm)
    {
        Sound(confirm);
    }

    void Sound(bool confirm)
    {
        if (titleMenu != null)
        {
            titleMenu.PlaySound(confirm);
            return;
        }

        if (pauseMenu != null && pauseMenu.uiAudioSource != null)
        {
            var clip = confirm ? pauseMenu.selectionConfirmSe : pauseMenu.selectionMoveSe;
            if (clip != null)
            {
                pauseMenu.uiAudioSource.PlayOneShot(clip, pauseMenu.selectionSeVolume);
            }
        }
    }

    void Back()
    {
        if (cameraSettings != null && cameraSettings.IsOpen)
        {
            cameraSettings.Close();
            consumedFrame = Time.frameCount;
            return;
        }

        if (capturing || waitForRelease)
        {
            return;
        }

        if (titleMenu != null)
        {
            titleMenu.CloseSettings();
        }
        else if (pauseMenu != null)
        {
            pauseMenu.BackToMenu();
        }
    }

    public void RefreshAfterCameraTab()
    {
        consumedFrame = Time.frameCount;
        enabledFrame = Time.frameCount;
        ready = false;
        Refresh();
    }

    void Refresh()
    {
        if (pending == null)
        {
            return;
        }

        for (int i = 0; i < values.Count && i < pending.Length; i++)
        {
            values[i].text = KeyboardBindingStore.Display(pending[i]);
        }

        operationText.text = KeyboardBindingStore.Entries[selectedIndex].label;
        promptText.text = capturing ? "変更するキーを押してください\n<size=20>Esc：キャンセル</size>" : "Enter または項目をクリックで変更\n<size=20>Esc：戻る ／ 未適用の変更は破棄</size>";
        accent.anchoredPosition = new Vector2(0, -selectedIndex * RowHeight);
        foreach (var visual in keyboard)
        {
            visual.image.color = visual.key == pending[selectedIndex] ? Red : Panel;
        }

        if (extraKeyText != null)
        {
            extraKeyText.text = keyboard.Exists(v => v.key == pending[selectedIndex]) ? "" : "選択キー： [ " + KeyboardBindingStore.Display(pending[selectedIndex]) + " ]";
        }

        foreach (var visual in keyboard)
        {
            var text = visual.image.GetComponentInChildren<TMP_Text>();
            if (text == null)
            {
                continue;
            }

            text.text = visual.key == KeyCode.Escape ? "Esc" : visual.key == KeyCode.Backspace ? "BS" : KeyboardBindingStore.Display(visual.key);
            text.enableWordWrapping = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 9;
            text.fontSizeMax = 16;
        }

        applyButton.GetComponentInChildren<TMP_Text>().text = dirty ? "適用 *" : "適用";
        foreach (var button in rowButtons)
        {
            button.interactable = !capturing && !waitForRelease;
        }

        resetButton.interactable = applyButton.interactable = backButton.interactable = !capturing && !waitForRelease;
        foreach (var tab in tabs)
        {
            tab.interactable = !capturing && !waitForRelease;
        }

        Canvas.ForceUpdateCanvases();
        float viewport = scroll.viewport.rect.height;
        float offset = rowsRoot.anchoredPosition.y;
        float top = selectedIndex * RowHeight;
        if (top < offset)
        {
            offset = top;
        }

        if (top + RowHeight > offset + viewport)
        {
            offset = top + RowHeight - viewport;
        }

        rowsRoot.anchoredPosition = new Vector2(0, Mathf.Clamp(offset, 0, Mathf.Max(0, rowsRoot.rect.height - viewport)));
        scroll.StopMovement();
    }

    // Called by the editor setup. All resulting UI objects are saved in the scene and editable.
    public void Build()
    {
        var root = Rect("Key Bindings Layout", transform, 0, 0, 1600, 900);
        root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
        root.pivot = new Vector2(.5f, .5f);
        root.anchoredPosition = Vector2.zero;
        root.gameObject.AddComponent<KeyBindingLayoutFit>();
        Label(root, "KEY BINDINGS", 35, 40, 600, 60, 46);
        Label(root, "キー割り当て", 40, 101, 420, 36, 25);
        tabs = new Button[4];
        string[] tabNames =
        {
            "CONTROLS / 操作",
            "CAMERA / カメラ",
            "AUDIO / 音声",
            "DISPLAY / 表示"
        };
        for (int i = 0; i < 4; i++)
        {
            tabs[i] = ButtonAt(root, tabNames[i], 550 + i * 250, 104, 240, 46);
        }

        Box(root, "Selected Tab", 550, 151, 240, 3, Red);
        var listPanel = Box(root, "Action List Panel", 35, 180, 740, 560, Panel);
        Label(listPanel, "ACTION / 操作", 18, 10, 300, 36, 23);
        Label(listPanel, "PRIMARY / キー", 402, 10, 290, 36, 23);
        var view = Rect("Viewport", listPanel, 5, 56, 730, 495);
        view.gameObject.AddComponent<RectMask2D>();
        rowsRoot = Rect("Rows", view, 0, 0, 730, KeyboardBindingStore.Entries.Length * RowHeight);
        scroll = listPanel.gameObject.AddComponent<ScrollRect>();
        scroll.content = rowsRoot;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 0;
        scroll.inertia = false;
        var bar = Rect("Scrollbar", listPanel, 728, 56, 8, 495);
        bar.gameObject.AddComponent<Image>().color = new Color(.1f, .15f, .18f);
        var handle = Rect("Handle", bar, 0, 0, 8, 50);
        var hi = handle.gameObject.AddComponent<Image>();
        hi.color = Line;
        var sb = bar.gameObject.AddComponent<Scrollbar>();
        sb.handleRect = handle;
        sb.targetGraphic = hi;
        sb.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = sb;
        accent = Box(rowsRoot, "Selection Accent", 0, 0, 720, RowHeight, new Color(.35f, .07f, .08f, .65f));
        Box(accent, "Red Line", 0, 0, 4, RowHeight, Red);
        for (int i = 0; i < KeyboardBindingStore.Entries.Length; i++)
        {
            var row = Rect("Binding " + KeyboardBindingStore.Entries[i].field, rowsRoot, 0, i * RowHeight, 720, RowHeight);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, .015f);
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            NoNavigation(button);
            rowButtons.Add(button);
            Label(row, KeyboardBindingStore.Entries[i].label, 16, 0, 360, RowHeight, 24);
            var value = Label(row, "—", 405, 0, 290, RowHeight, 24);
            value.alignment = TextAlignmentOptions.Center;
            values.Add(value);
            Box(row, "Separator", 10, RowHeight - 1, 690, 1, new Color(.2f, .3f, .33f, .5f));
        }

        var keys = Box(root, "Keyboard Panel", 795, 180, 770, 307, Panel);
        string[] lines =
        {
            "Escape F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12",
            "BackQuote Alpha1 Alpha2 Alpha3 Alpha4 Alpha5 Alpha6 Alpha7 Alpha8 Alpha9 Alpha0 Minus Equals Backspace",
            "Tab Q W E R T Y U I O P LeftBracket RightBracket Backslash",
            "CapsLock A S D F G H J K L Semicolon Quote Return",
            "LeftShift Z X C V B N M Comma Period Slash RightShift",
            "LeftControl LeftAlt Space RightAlt RightControl LeftArrow DownArrow UpArrow RightArrow"
        };
        for (int y = 0; y < lines.Length; y++)
        {
            var names = lines[y].Split(' ');
            float width = 590f / names.Length;
            for (int x = 0; x < names.Length; x++)
            {
                var key = (KeyCode)Enum.Parse(typeof(KeyCode), names[x]);
                var box = Box(keys, "Key " + key, 12 + x * width, 16 + y * 43, width - 3, 36, Panel);
                string caption = KeyboardBindingStore.Display(key).Replace("Left", "L ").Replace("Right", "R ");
                if (key == KeyCode.Escape)
                {
                    caption = "Esc";
                }

                if (key == KeyCode.Backspace)
                {
                    caption = "BS";
                }

                Label(box, caption, 0, 0, width - 3, 36, 13).alignment = TextAlignmentOptions.Center;
                keyboard.Add(new KeyVisual { key = key, image = box.GetComponent<Image>() });
            }
        }

        for (int i = 0; i < 5; i++)
        {
            var key = (KeyCode)((int)KeyCode.Mouse0 + i);
            var box = Box(keys, "Mouse " + i, 620, 16 + 51 * i, 135, 43, Panel);
            Label(box, KeyboardBindingStore.Display(key), 0, 0, 135, 43, 16).alignment = TextAlignmentOptions.Center;
            keyboard.Add(new KeyVisual { key = key, image = box.GetComponent<Image>() });
        }

        var rebind = Box(root, "Rebind Panel", 795, 507, 770, 233, Panel);
        Label(rebind, "REBIND / キー変更", 20, 12, 700, 36, 24);
        operationText = Label(rebind, "前進", 15, 60, 740, 40, 30);
        operationText.alignment = TextAlignmentOptions.Center;
        promptText = Label(rebind, "Enter または項目をクリックで変更", 15, 114, 740, 105, 28);
        promptText.alignment = TextAlignmentOptions.Center;
        var footer = Box(root, "Footer", 35, 770, 1530, 97, Panel);
        messageText = Label(footer, "項目を選択して、割り当てるキーを変更します。", 18, 8, 865, 43, 22);
        Label(footer, "↑ ↓ / ホイール：選択    Enter：変更    Esc：戻る", 18, 52, 865, 30, 19);
        resetButton = ButtonAt(footer, "初期設定に戻す", 900, 24, 250, 49);
        applyButton = ButtonAt(footer, "適用", 1170, 24, 150, 49);
        backButton = ButtonAt(footer, "戻る", 1340, 24, 160, 49);
    }

    static void NoNavigation(Selectable item)
    {
        item.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };
    }

    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    static RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var r = Rect(name, parent, x, y, w, h);
        var im = r.gameObject.AddComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        var outline = r.gameObject.AddComponent<Outline>();
        outline.effectColor = Line;
        outline.effectDistance = new Vector2(1, -1);
        return r;
    }

    TMP_Text Label(Transform parent, string value, float x, float y, float w, float h, float size)
    {
        var r = Rect("Text", parent, x, y, w, h);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = value;
        t.fontSize = size;
        t.color = new Color(.91f, .91f, .87f);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.raycastTarget = false;
        t.enableWordWrapping = true;
        return t;
    }

    Button ButtonAt(Transform parent, string label, float x, float y, float w, float h)
    {
        var r = Box(parent, label, x, y, w, h, Panel);
        var im = r.GetComponent<Image>();
        im.raycastTarget = true;
        var button = r.gameObject.AddComponent<Button>();
        button.targetGraphic = im;
        NoNavigation(button);
        Label(r, label, 0, 0, w, h, 22).alignment = TextAlignmentOptions.Center;
        return button;
    }
}
