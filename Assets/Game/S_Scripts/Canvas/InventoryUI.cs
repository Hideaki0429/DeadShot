// 【役割】インベントリの一覧、選択、説明とアイテム操作を管理する。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[DefaultExecutionOrder(-970)]
public sealed class InventoryUI : MonoBehaviour
{
    public PlayerInventory inventory;
    [Header("消耗品の使用")]
    public PlayerHealing healing;
    public WeaponAmmoSystem ammoSource;
    public string magazineSetItemId = "m1917";
    public string specialGogglesItemId = "m1914";
    [Min(1)]
    public int magazinesPerSet = 1;
    public GameObject panel;
    public TMP_FontAsset font;
    public RectTransform content, selectionLine;
    public ScrollRect scroll;
    public Image detailImage;
    public TMP_Text itemName, description, countText, emptyText;
    public TMP_Text shortcutStatusText, footerGuideText;
    public Button useButton;
    public Button backButton;
    public Button[] tabButtons;
    public Image[] tabLines;
    public AudioSource audioSource;
    public AudioClip moveSe, confirmSe;
    [Range(0, 1)]
    public float volume = .5f;
    [Header("選択表示（カード内に収まるよう自動制限）")]
    [Tooltip("白枠の幅・高さ。カード中央を基準にします。")]
    public Vector2 selectedFrameSize = new Vector2(264, 174);
    [Range(1, 12)]
    public float selectedFrameThickness = 2;
    [Tooltip("赤いラインの幅・高さ")]
    public Vector2 cursorSize = new Vector2(4, 170);
    [Tooltip("カード左上からの位置。Xは右、Yは下が正です。")]
    public Vector2 cursorOffset = new Vector2(3, 5);
    RectTransform selectedFrame;
    readonly List<RectTransform> frameEdges = new List<RectTransform>();
    public bool IsOpen { get; private set; }
    public int SelectedIndex => selected;
    public int Filter => filter;
    public int VisibleCount => visible.Count;

    readonly List<PlayerInventory.Stack> visible = new List<PlayerInventory.Stack>();
    readonly List<GameObject> cards = new List<GameObject>();
    readonly List<Image> borders = new List<Image>();
    int selected, filter, openedFrame;
    float savedScale;
    bool savedAudio, savedVisible, savedNavigation;
    bool shortcutRegistration;
    CursorLockMode savedLock;
    EventSystem savedEvents;
    [Header("各アイテムの通常フレーム")]
    [Tooltip("全アイテム共通の幅・高さ。幅に合わせて列数も自動調整します。")]
    public Vector2 itemFrameSize = new Vector2(270, 180);
    [Range(1, 12)]
    public float itemFrameThickness = 1;
    const float GAP = 14;
    float CW => Mathf.Clamp(itemFrameSize.x, 120, scroll != null && scroll.viewport != null ? Mathf.Max(120, scroll.viewport.rect.width) : 850);

    float CH => Mathf.Max(100, itemFrameSize.y);

    int Columns => Mathf.Max(1, Mathf.FloorToInt(((scroll != null && scroll.viewport != null ? scroll.viewport.rect.width : 850) + GAP) / (CW + GAP)));

    Vector2 lastFrameSize;
    float lastFrameThickness, lastViewportWidth;
    static readonly Color Line = new Color(.29f, .39f, .43f);
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        panel.SetActive(false);
        audioSource.ignoreListenerPause = true;
        audioSource.spatialBlend = 0;
        audioSource.playOnAwake = false;
        inventory.Changed += Rebuild;
        EnsureShortcutTexts();
        useButton.onClick.AddListener(UseSelected);
        backButton.onClick.AddListener(Close);
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int n = i;
            tabButtons[i].onClick.AddListener(() => SetFilter(n));
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        var input = GameInputManager.Instance;
        if (input == null)
        {
            return;
        }

        if (!IsOpen)
        {
            if (!input.WeaponSelectionActive && input.ItemsPressed && !PauseMenuController.IsPaused)
            {
                Open();
            }

            return;
        }

        if (Time.frameCount <= openedFrame)
        {
            return;
        }

        if (shortcutRegistration)
        {
            UpdateShortcutRegistration();
            return;
        }

        if (input.ItemsClosePressed || input.UiBackPressed)
        {
            Close();
            return;
        }

        int next = selected;
        int columns = Columns;
        int horizontal = input.UiHorizontal, vertical = input.UiVertical;
        if (horizontal < 0 && selected % columns > 0)
        {
            next--;
        }
        else if (horizontal > 0 && selected % columns < columns - 1 && selected + 1 < visible.Count)
        {
            next++;
        }
        else if (vertical < 0 && selected >= columns)
        {
            next -= columns;
        }
        else if (vertical > 0 && selected + columns < visible.Count)
        {
            next += columns;
        }

        if (next != selected)
        {
            Select(next, true);
        }

        // Wheel scrolls the viewport only, never changes the selected item.
        if (Mathf.Abs(Input.mouseScrollDelta.y) > .01f)
        {
            float max = Mathf.Max(0, content.rect.height - scroll.viewport.rect.height);
            content.anchoredPosition = new Vector2(0, Mathf.Clamp(content.anchoredPosition.y - Input.mouseScrollDelta.y * 65, 0, max));
        }

        if (input.UiConfirmPressed)
        {
            UseSelected();
        }
    }

    public void Open()
    {
        var input = GameInputManager.Instance;
        if (IsOpen || input == null || !input.Gameplay || PauseMenuController.IsPaused)
        {
            return;
        }

        savedScale = Time.timeScale;
        savedAudio = AudioListener.pause;
        savedVisible = Cursor.visible;
        savedLock = Cursor.lockState;
        IsOpen = true;
        openedFrame = Time.frameCount;
        input.SetPaused(true);
        Time.timeScale = 0;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        savedEvents = EventSystem.current;
        if (savedEvents != null)
        {
            savedNavigation = savedEvents.sendNavigationEvents;
            savedEvents.sendNavigationEvents = false;
            savedEvents.SetSelectedGameObject(null);
        }

        panel.SetActive(true);
        filter = 0;
        selected = 0;
        Rebuild();
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        shortcutRegistration = false;
        RestoreFooterGuide();
        IsOpen = false;
        panel.SetActive(false);
        Time.timeScale = savedScale;
        AudioListener.pause = savedAudio;
        Cursor.lockState = savedLock;
        Cursor.visible = savedVisible;
        if (savedEvents != null)
        {
            savedEvents.SetSelectedGameObject(null);
            savedEvents.sendNavigationEvents = savedNavigation;
        }

        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.SetPaused(false);
        }
    }

    public void SetFilter(int value)
    {
        if (!IsOpen || filter == value)
        {
            return;
        }

        filter = Mathf.Clamp(value, 0, 2);
        selected = 0;
        Rebuild();
        Sound(false);
    }

    public void Rebuild()
    {
        if (!IsOpen)
        {
            return;
        }

        string previous = selected < visible.Count ? visible[selected].id : null;
        visible.Clear();
        foreach (var s in inventory.Owned)
        {
            // Keep catalog definitions, but never display empty inventory stacks.
            if (s == null || s.count <= 0)
            {
                continue;
            }

            var d = inventory.Find(s.id);
            if (d != null && (filter == 0 || (filter == 1 && d.category == InventoryCategory.Weapons) || (filter == 2 && d.category == InventoryCategory.Supplies)))
            {
                visible.Add(s);
            }
        }

        foreach (var card in cards)
        {
            card.SetActive(false);
            Destroy(card);
        }

        cards.Clear();
        borders.Clear();
        lastFrameSize = itemFrameSize;
        lastFrameThickness = itemFrameThickness;
        lastViewportWidth = scroll.viewport.rect.width;
        int columns = Columns;
        content.sizeDelta = new Vector2(scroll.viewport.rect.width, Mathf.Max(scroll.viewport.rect.height, Mathf.Ceil(visible.Count / (float)columns) * (CH + GAP)));
        for (int i = 0; i < visible.Count; i++)
        {
            int index = i;
            var s = visible[i];
            var def = inventory.Find(s.id);
            var card = Rect("Item " + s.id, content, (i % columns) * (CW + GAP), (i / columns) * (CH + GAP), CW, CH);
            cards.Add(card.gameObject);
            var border = card.gameObject.AddComponent<Image>();
            border.color = Line;
            borders.Add(border);
            float thickness = Mathf.Clamp(itemFrameThickness, 1, 12);
            float iw = CW - 2 * thickness, ih = CH - 2 * thickness;
            var inner = Box(card, "Card Background", thickness, thickness, iw, ih, new Color(.035f, .045f, .05f));
            var button = inner.gameObject.AddComponent<Button>();
            button.targetGraphic = inner.GetComponent<Image>();
            NoNav(button);
            button.onClick.AddListener(() => Select(index, true));
            var icon = Rect("Icon", inner, 12, 8, iw - 24, Mathf.Max(12, ih - 52)).gameObject.AddComponent<Image>();
            icon.sprite = def.icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = def.icon != null;
            var nameLabel = Label(inner, def.displayName, 8, ih - 34, Mathf.Max(16, iw - 65), 28, 20);
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 10;
            nameLabel.fontSizeMax = 20;
            var number = Label(inner, "x" + s.count, iw - 53, ih - 34, 45, 28, 20);
            number.alignment = TextAlignmentOptions.Right;
        }

        for (int i = 0; i < 3; i++)
        {
            tabLines[i].color = i == filter ? Color.white : new Color(0, 0, 0, 0);
        }

        int keep = visible.FindIndex(s => s.id == previous);
        selected = Mathf.Clamp(keep >= 0 ? keep : selected, 0, Mathf.Max(0, visible.Count - 1));
        content.anchoredPosition = Vector2.zero;
        Select(selected, false);
    }

    public void Select(int index, bool sound)
    {
        selected = Mathf.Clamp(index, 0, Mathf.Max(0, visible.Count - 1));
        bool has = visible.Count > 0;
        emptyText.gameObject.SetActive(!has);
        useButton.interactable = has;
        selectionLine.gameObject.SetActive(has);
        if (!has)
        {
            detailImage.enabled = false;
            itemName.text = "";
            description.text = "該当するアイテムを所持していません。";
            countText.text = "";
            if (shortcutStatusText != null)
            {
                shortcutStatusText.text = "";
            }

            return;
        }

        for (int i = 0; i < borders.Count; i++)
        {
            borders[i].color = Line;
        }

        RefreshSelectionVisuals();
        var stack = visible[selected];
        var def = inventory.Find(stack.id);
        detailImage.sprite = def.icon;
        detailImage.enabled = def.icon != null;
        itemName.text = def.displayName;
        description.text = def.description;
        countText.text = "所持数    " + stack.count;
        RefreshShortcutStatus(def);
        TMP_Text useLabel = useButton != null ? useButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (useLabel != null)
        {
            useLabel.text = stack.id == specialGogglesItemId ? "装備／登録" : def.CanRegisterShortcut ? "登録する" : "使用する";
        }

        Canvas.ForceUpdateCanvases();
        float top = (selected / Columns) * (CH + GAP), offset = content.anchoredPosition.y;
        if (top < offset)
        {
            offset = top;
        }
        else if (top + CH > offset + scroll.viewport.rect.height)
        {
            offset = top + CH - scroll.viewport.rect.height;
        }

        content.anchoredPosition = new Vector2(0, Mathf.Clamp(offset, 0, Mathf.Max(0, content.rect.height - scroll.viewport.rect.height)));
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (sound)
        {
            Sound(false);
        }
    }

    public void UseSelected()
    {
        if (!IsOpen || visible.Count == 0)
        {
            return;
        }

        string id = visible[selected].id;
        if (inventory.Count(id) <= 0)
        {
            return;
        }

        PlayerInventory.Definition definition = inventory.Find(id);
        if (definition != null && definition.CanRegisterShortcut)
        {
            if (shortcutRegistration && id == specialGogglesItemId)
            {
                SpecialGogglesController goggles = SpecialGogglesController.FindOrCreateOnPlayer();
                if (goggles != null)
                {
                    goggles.ToggleEquipped();
                    shortcutRegistration = false;
                    RestoreFooterGuide();
                    Select(selected, false);
                    Sound(true);
                }

                return;
            }

            shortcutRegistration = true;
            ShowShortcutRegistrationGuide();
            Sound(true);
            return;
        }

        if (healing == null)
        {
            healing = FindObjectOfType<PlayerHealing>(true);
        }

        if (ammoSource == null)
        {
            ammoSource = FindObjectOfType<WeaponAmmoSystem>(true);
        }

        bool used = false;
        if (id == specialGogglesItemId)
        {
            SpecialGogglesController goggles = SpecialGogglesController.FindOrCreateOnPlayer();
            if (goggles == null)
            {
                Debug.LogWarning("[Inventory] プレイヤーが見つからないため特殊ゴーグルを装備できません。", this);
                return;
            }

            goggles.ToggleEquipped();
            Select(selected, false);
            Sound(true);
            return;
        }

        if (healing != null && id == healing.healingItemId)
        {
            var health = healing.GetComponent<PlayerHealth>();
            if (health == null || health.CurrentHP <= 0 || health.CurrentHP >= health.MaxHP || PlayerHealing.Busy)
            {
                Debug.Log("[Inventory] 現在は回復できません。", this);
                return;
            }

            health.Heal(healing.healAmount);
            used = true;
        }
        else if (ammoSource != null && id == ammoSource.HandgunAmmoItemId)
        {
            used = ammoSource != null && ammoSource.UseMagazineFromInventory();
            if (!used)
            {
                Debug.Log("[Inventory] 装填済み、リロード中、または弾倉がありません。", this);
                return;
            }

            Sound(true);
            return; // 装填処理側で1個消費済み。
        }

        if (!used)
        {
            Sound(true);
            Debug.Log("[Inventory] 使用する: " + inventory.Find(id).displayName + "（未実装）", this);
            return;
        }

        inventory.RemoveItem(id, 1);
        Sound(true);
    }

    void UpdateShortcutRegistration()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            shortcutRegistration = false;
            RestoreFooterGuide();
            Sound(false);
            return;
        }

        PlayerInventory.Definition selectedDefinition = visible.Count > 0 ? inventory.Find(visible[selected].id) : null;
        if (selectedDefinition != null && selectedDefinition.id == specialGogglesItemId && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            SpecialGogglesController goggles = SpecialGogglesController.FindOrCreateOnPlayer();
            if (goggles != null)
            {
                goggles.ToggleEquipped();
                shortcutRegistration = false;
                RestoreFooterGuide();
                Select(selected, false);
                Sound(true);
            }

            return;
        }

        GameInputManager input = GameInputManager.Instance;
        KeyCode up = input != null ? input.weaponUp : KeyCode.UpArrow;
        KeyCode right = input != null ? input.weaponRight : KeyCode.RightArrow;
        KeyCode down = input != null ? input.weaponDown : KeyCode.DownArrow;
        KeyCode left = input != null ? input.weaponLeft : KeyCode.LeftArrow;
        int direction = Input.GetKeyDown(up) ? 0 : Input.GetKeyDown(right) ? 1 : Input.GetKeyDown(down) ? 2 : Input.GetKeyDown(left) ? 3 : -1;
        if (direction < 0 || visible.Count == 0)
        {
            return;
        }

        PlayerInventory.Definition definition = selectedDefinition;
        WeaponSelectionUI shortcuts = FindObjectOfType<WeaponSelectionUI>(true);
        if (shortcuts == null || !shortcuts.RegisterInventoryWeapon(definition, direction))
        {
            if (footerGuideText != null)
            {
                footerGuideText.text = "登録に失敗しました。武器選択システムの参照を確認してください。";
            }

            return;
        }

        shortcutRegistration = false;
        RestoreFooterGuide();
        RefreshShortcutStatus(definition);
        Sound(true);
    }

    void RefreshShortcutStatus(PlayerInventory.Definition definition)
    {
        if (shortcutStatusText == null)
        {
            return;
        }

        if (definition == null || !definition.CanRegisterShortcut)
        {
            shortcutStatusText.text = "";
            return;
        }

        WeaponSelectionUI shortcuts = FindObjectOfType<WeaponSelectionUI>(true);
        string directions = shortcuts != null ? shortcuts.RegisteredDirections(definition) : "";
        shortcutStatusText.text = "ショートカット登録先    " + (string.IsNullOrEmpty(directions) ? "未登録" : directions);
    }

    void EnsureShortcutTexts()
    {
        Transform layout = panel != null ? panel.transform.Find("Inventory Layout") : null;
        Transform details = layout != null ? layout.Find("Details Frame") : null;
        if (shortcutStatusText == null && details != null)
        {
            shortcutStatusText = Label(details, "", 28, 585, 540, 25, 19);
        }

        if (footerGuideText == null && layout != null)
        {
            Transform old = layout.Find("↑ ↓ ← →  選択     ホイール  一覧スクロール     Enter  使用");
            footerGuideText = old != null ? old.GetComponent<TMP_Text>() : null;
        }

        RestoreFooterGuide();
    }

    void RestoreFooterGuide()
    {
        if (footerGuideText != null)
        {
            footerGuideText.text = "↑ ↓ ← →  選択     ホイール  一覧スクロール     Enter  使用／登録";
        }
    }

    void ShowShortcutRegistrationGuide()
    {
        if (footerGuideText == null)
        {
            return;
        }

        GameInputManager input = GameInputManager.Instance;
        bool goggles = visible.Count > 0 && inventory.Find(visible[selected].id)?.id == specialGogglesItemId;
        string equip = goggles ? "Enter：装備／外す    " : "";
        footerGuideText.text = input == null ? $"{equip}どちらに登録しますか？   ↑ 上   → 右   ↓ 下   ← 左      Esc：キャンセル" : $"{equip}どちらに登録しますか？   {KeyboardBindingStore.Display(input.weaponUp)} 上   {KeyboardBindingStore.Display(input.weaponRight)} 右   {KeyboardBindingStore.Display(input.weaponDown)} 下   {KeyboardBindingStore.Display(input.weaponLeft)} 左      Esc：キャンセル";
    }

    // Kept inside the masked card bounds; updates also apply while timeScale is zero.
    void LateUpdate()
    {
        if (!IsOpen)
        {
            return;
        }

        if (lastFrameSize != itemFrameSize || lastFrameThickness != itemFrameThickness || !Mathf.Approximately(lastViewportWidth, scroll.viewport.rect.width))
        {
            Rebuild();
        }

        RefreshSelectionVisuals();
    }

    void RefreshSelectionVisuals()
    {
        bool has = visible.Count > 0;
        if (selectedFrame != null)
        {
            selectedFrame.gameObject.SetActive(has);
        }

        selectionLine.gameObject.SetActive(has);
        if (!has)
        {
            return;
        }

        if (selectedFrame == null)
        {
            selectedFrame = Rect("Selected White Frame", content, 0, 0, CW, CH);
            for (int i = 0; i < 4; i++)
            {
                var edge = Box(selectedFrame, "Frame Edge " + i, 0, 0, 1, 1, Color.white);
                edge.GetComponent<Image>().raycastTarget = false;
                frameEdges.Add(edge);
            }
        }

        float w = Mathf.Clamp(selectedFrameSize.x, 4, CW - 2), h = Mathf.Clamp(selectedFrameSize.y, 4, CH - 2);
        float t = Mathf.Clamp(selectedFrameThickness, 1, Mathf.Min(w, h) / 2);
        float x = (selected % Columns) * (CW + GAP), y = (selected / Columns) * (CH + GAP);
        selectedFrame.anchoredPosition = new Vector2(x + (CW - w) / 2, -y - (CH - h) / 2);
        selectedFrame.sizeDelta = new Vector2(w, h);
        SetEdge(0, 0, 0, w, t);
        SetEdge(1, 0, h - t, w, t);
        SetEdge(2, 0, 0, t, h);
        SetEdge(3, w - t, 0, t, h);
        float cw = Mathf.Clamp(cursorSize.x, 1, CW - 2), ch = Mathf.Clamp(cursorSize.y, 1, CH - 2);
        selectionLine.sizeDelta = new Vector2(cw, ch);
        selectionLine.anchoredPosition = new Vector2(x + Mathf.Clamp(cursorOffset.x, 1, CW - cw - 1), -y - Mathf.Clamp(cursorOffset.y, 1, CH - ch - 1));
        selectedFrame.SetAsLastSibling();
        selectionLine.SetAsLastSibling();
    }

    void SetEdge(int index, float x, float y, float w, float h)
    {
        frameEdges[index].anchoredPosition = new Vector2(x, -y);
        frameEdges[index].sizeDelta = new Vector2(w, h);
    }

    void Sound(bool confirm)
    {
        var clip = confirm ? confirmSe : moveSe;
        if (clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        Close();
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.Changed -= Rebuild;
        }
    }

    public void Build(Transform parent)
    {
        var root = Rect("Inventory Panel", parent, 0, 0, 1600, 900);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        panel = root.gameObject;
        panel.SetActive(false);
        root.gameObject.AddComponent<Image>().color = Color.black;
        var layout = Rect("Inventory Layout", root, 0, 0, 1600, 900);
        layout.anchorMin = layout.anchorMax = layout.pivot = new Vector2(.5f, .5f);
        layout.anchoredPosition = Vector2.zero;
        layout.gameObject.AddComponent<KeyBindingLayoutFit>();
        Label(layout, "INVENTORY", 50, 35, 490, 65, 44);
        Label(layout, "所持アイテム", 395, 53, 250, 40, 23);
        var left = Box(layout, "Items Frame", 45, 115, 890, 675, Line);
        Box(left, "Inner", 1, 1, 888, 673, new Color(.015f, .024f, .03f));
        tabLines = new Image[3];
        tabButtons = new Button[3];
        string[] names =
        {
            "ALL",
            "WEAPONS",
            "SUPPLIES"
        };
        for (int i = 0; i < 3; i++)
        {
            tabButtons[i] = ButtonAt(left, names[i], 20 + i * 190, 9, 180, 49);
            tabLines[i] = Box(left, "Tab Line", 20 + i * 190, 60, 175, 2, Color.white).GetComponent<Image>();
        }

        var viewport = Rect("Viewport", left, 18, 79, 850, 574);
        viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .01f);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Items", viewport, 0, 0, 850, 574);
        scroll = left.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.inertia = false;
        scroll.scrollSensitivity = 0;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var track = Box(left, "Scroll Track", 875, 79, 6, 574, new Color(.08f, .11f, .13f));
        var handle = Box(track, "Handle", 0, 0, 6, 80, Line);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle;
        bar.targetGraphic = handle.GetComponent<Image>();
        scroll.verticalScrollbar = bar;
        selectionLine = Box(content, "Red Selection Line", 0, 0, 4, CH, new Color(.9f, .05f, .07f));
        selectionLine.GetComponent<Image>().raycastTarget = false;
        emptyText = Label(left, "該当するアイテムはありません", 55, 285, 790, 70, 26);
        var right = Box(layout, "Details Frame", 952, 115, 600, 675, Line);
        Box(right, "Inner", 1, 1, 598, 673, new Color(.015f, .024f, .03f));
        Label(right, "ITEM DETAILS", 24, 15, 540, 38, 24);
        Box(right, "Divider", 24, 60, 550, 1, Line);
        detailImage = Rect("Preview", right, 44, 78, 510, 235).gameObject.AddComponent<Image>();
        detailImage.preserveAspect = true;
        detailImage.raycastTarget = false;
        itemName = Label(right, "", 28, 328, 540, 56, 34);
        Box(right, "Divider", 28, 389, 540, 1, Line);
        Label(right, "説明", 28, 403, 540, 30, 20);
        description = Label(right, "", 28, 444, 540, 95, 23);
        description.enableWordWrapping = true;
        countText = Label(right, "", 28, 552, 540, 35, 24);
        shortcutStatusText = Label(right, "", 28, 585, 540, 25, 19);
        useButton = ButtonAt(right, "使用する", 135, 610, 325, 45);
        footerGuideText = Label(layout, "↑ ↓ ← →  選択     ホイール  一覧スクロール     Enter  使用／登録", 55, 825, 1130, 38, 21);
        backButton = ButtonAt(layout, "戻る", 1370, 818, 180, 47);
    }

    RectTransform Rect(string name, Transform p, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(p, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    RectTransform Box(Transform p, string n, float x, float y, float w, float h, Color color)
    {
        var r = Rect(n, p, x, y, w, h);
        r.gameObject.AddComponent<Image>().color = color;
        return r;
    }

    TMP_Text Label(Transform p, string text, float x, float y, float w, float h, float size)
    {
        var t = Rect(text, p, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = Color.white;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }

    Button ButtonAt(Transform p, string text, float x, float y, float w, float h)
    {
        var r = Box(p, text, x, y, w, h, new Color(.06f, .085f, .1f));
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = r.GetComponent<Image>();
        NoNav(b);
        var t = Label(r, text, 0, 0, w, h, 22);
        t.alignment = TextAlignmentOptions.Center;
        return b;
    }

    static void NoNav(Button b)
    {
        b.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };
    }
}
