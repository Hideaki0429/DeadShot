// 【役割】ポーズ状態の開始・解除とポーズ画面の操作を管理する。
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(-900)]
public sealed class PauseMenuController : MonoBehaviour
{
    public enum MenuAction
    {
        LogOnly,
        Resume,
        Controls,
        Settings,
        Title
    }

    [Serializable]
    public class MenuItem
    {
        public string itemName;
        public RectTransform accentPosition;
        [TextArea(2, 4)]
        public string description;
        public MenuAction action;
    }

    [Header("表示するUI（親子で重複登録しない）")]
    public CanvasGroup[] pauseGroups;
    [Header("ポーズ中に隠すHUD")]
    public CanvasGroup[] hideGroups;
    [Header("選択")]
    public RectTransform selectionAccent;
    public MenuItem[] items;
    [Header("説明文")]
    public TMP_Text descriptionText;
    [Header("ポーズ中の任務状況")]
    public TMP_Text pauseTimeText;
    public TMP_Text pauseScoreText;
    public TMP_Text pauseKillText;
    [Header("サブパネル（ポーズUIの外に配置）")]
    public CanvasGroup mainMenuGroup;
    public CanvasGroup settingsPanel;
    public CanvasGroup controlsPanel;
    public Button settingsBackButton;
    public Button controlsBackButton;
    [Header("ポーズ専用SE")]
    public AudioSource uiAudioSource;
    public AudioClip selectionMoveSe;
    public AudioClip selectionConfirmSe;
    [Range(0f, 1f)]
    public float selectionSeVolume = 0.5f;
    [Header("タイトル")]
    public string titleSceneName = "Title2";
    public string loadingSceneName = "Loading";
    [HideInInspector]
    public int menuFeatureVersion;
    [Header("フェード時間")]
    [Min(0f)]
    public float fadeInDuration = 0.25f;
    [Min(0f)]
    public float fadeOutDuration = 0.2f;
    public static bool IsPaused { get; private set; }
    public bool IsTransitioning => transitioning;
    public int SelectedIndex => selectedIndex;

    private static PauseMenuController owner;
    private bool transitioning;
    private int selectedIndex;
    private float savedTimeScale;
    private bool savedAudioPause, savedCursorVisible;
    private CursorLockMode savedCursorLock;
    private CanvasGroup activePanel;
    private GameObject savedSelectedObject;
    private float panelAlpha = 1f;
    private int subpanelFrame;
    public bool IsSubpanelOpen => activePanel != null;

    private readonly List<GroupState> hiddenStates = new List<GroupState>();
    private readonly List<BaseInputModule> suspendedModules = new List<BaseInputModule>();
    private readonly Dictionary<CanvasGroup, float> visibleAlpha = new Dictionary<CanvasGroup, float>();
    private struct GroupState
    {
        public CanvasGroup group;
        public float alpha;
        public bool interactable, blocksRaycasts;
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    private void Start()
    {
        if (uiAudioSource == null)
        {
            var audioObject = new GameObject("Pause UI Audio");
            audioObject.transform.SetParent(transform, false);
            uiAudioSource = audioObject.AddComponent<AudioSource>();
        }

        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;
        uiAudioSource.ignoreListenerPause = true;
        HidePanel(settingsPanel);
        HidePanel(controlsPanel);
        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.AddListener(BackToMenu);
        }

        if (controlsBackButton != null)
        {
            controlsBackButton.onClick.AddListener(BackToMenu);
        }

        if (pauseGroups == null)
        {
            return;
        }

        foreach (CanvasGroup group in pauseGroups)
        {
            if (group == null)
            {
                continue;
            }

            visibleAlpha[group] = group.alpha > 0f ? group.alpha : 1f;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.gameObject.SetActive(false);
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        GameInputManager input = GameInputManager.Instance;
        if (input == null || transitioning)
        {
            return;
        }

        if (KeyBindingSettingsUI.BlockPauseInput)
        {
            return;
        }

        if (input.PausePressed)
        {
            if (owner == this)
            {
                StartCoroutine(Close());
            }
            else if (!IsPaused && (input.Mode == GameInputManager.InputMode.Gameplay || input.Mode == GameInputManager.InputMode.Dialogue))
            {
                Open();
            }

            return;
        }

        if (owner != this)
        {
            return;
        }

        if (activePanel != null)
        {
            if (input.PauseBackPressed)
            {
                BackToMenu();
            }

            return;
        }

        if (input.PauseSelection != 0)
        {
            MoveSelection(input.PauseSelection);
        }

        if (input.PauseConfirmPressed)
        {
            ConfirmSelection();
        }
    }

    private void Open()
    {
        if (pauseGroups == null || Array.Find(pauseGroups, g => g != null) == null)
        {
            Debug.LogWarning("[PauseMenu] 表示するCanvasGroupを登録してください。", this);
            return;
        }

        owner = this;
        IsPaused = true;
        transitioning = true;
        savedTimeScale = Time.timeScale;
        savedAudioPause = AudioListener.pause;
        savedCursorLock = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        GameInputManager.Instance.SetPaused(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        hiddenStates.Clear();
        if (hideGroups != null)
        {
            foreach (CanvasGroup group in hideGroups)
            {
                if (group == null || Array.IndexOf(pauseGroups, group) >= 0)
                {
                    continue;
                }

                hiddenStates.Add(new GroupState { group = group, alpha = group.alpha, interactable = group.interactable, blocksRaycasts = group.blocksRaycasts });
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        // Prevent the UI EventSystem from forwarding Enter/clicks to underlying conversations.
        suspendedModules.Clear();
        savedSelectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        foreach (BaseInputModule module in FindObjectsOfType<BaseInputModule>())
        {
            if (module.enabled)
            {
                suspendedModules.Add(module);
                module.enabled = false;
            }
        }

        foreach (CanvasGroup group in pauseGroups)
        {
            if (group == null)
            {
                continue;
            }

            if (!visibleAlpha.ContainsKey(group))
            {
                visibleAlpha[group] = group.alpha > 0 ? group.alpha : 1f;
            }

            group.gameObject.SetActive(true);
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        selectedIndex = 0;
        RefreshStatus();
        UpdateAccent();
        StartCoroutine(Fade(true));
        Debug.Log("[PauseMenu] ポーズ開始", this);
    }

    private void RefreshStatus()
    {
        var manager = ScoreAndTimeManager.instance;
        if (manager == null)
        {
            return;
        }

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, manager.RemainingTime));
        if (pauseTimeText != null)
        {
            pauseTimeText.text = manager.UseTimeLimit ? $"{seconds / 60:00}:{seconds % 60:00}" : "--:--";
        }

        if (pauseScoreText != null)
        {
            pauseScoreText.text = manager.TotalScore.ToString();
        }

        if (pauseKillText != null)
        {
            pauseKillText.text = manager.KillCount.ToString();
        }
    }

    private IEnumerator Fade(bool show)
    {
        float duration = show ? fadeInDuration : fadeOutDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            SetAlpha(show ? elapsed / duration : 1f - elapsed / duration);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        SetAlpha(show ? 1f : 0f);
        if (show)
        {
            transitioning = false;
        }
    }

    private void SetAlpha(float factor)
    {
        foreach (CanvasGroup group in pauseGroups)
        {
            if (group != null)
            {
                group.alpha = activePanel != null && group == mainMenuGroup ? 0f : visibleAlpha[group] * factor;
            }
        }

        if (activePanel != null)
        {
            activePanel.alpha = panelAlpha * factor;
        }
    }

    private IEnumerator Close()
    {
        transitioning = true;
        SetModulesEnabled(false);
        if (activePanel != null)
        {
            activePanel.interactable = false;
        }

        yield return Fade(false);
        Restore();
        Debug.Log("[PauseMenu] ゲーム再開", this);
    }

    private void MoveSelection(int direction)
    {
        if (items == null || items.Length == 0)
        {
            return;
        }

        selectedIndex = (selectedIndex + direction + items.Length) % items.Length;
        UpdateAccent();
        PlayUiSound(selectionMoveSe);
    }

    private void UpdateAccent()
    {
        if (items == null || items.Length == 0)
        {
            return;
        }

        if (descriptionText != null)
        {
            descriptionText.text = items[selectedIndex].description;
        }

        RectTransform destination = items[selectedIndex].accentPosition;
        if (selectionAccent != null && destination != null)
        {
            selectionAccent.position = destination.position;
        }
    }

    private void PlayUiSound(AudioClip clip)
    {
        if (uiAudioSource != null && clip != null)
        {
            uiAudioSource.PlayOneShot(clip, selectionSeVolume);
        }
    }

    private void ConfirmSelection()
    {
        if (owner != this || transitioning || activePanel != null || items == null || items.Length == 0)
        {
            return;
        }

        MenuItem item = items[selectedIndex];
        PlayUiSound(selectionConfirmSe);
        switch (item.action)
        {
            case MenuAction.Resume:
                StartCoroutine(Close());
                break;
            case MenuAction.Controls:
                ShowPanel(controlsPanel, controlsBackButton);
                break;
            case MenuAction.Settings:
                ShowPanel(settingsPanel, settingsBackButton);
                break;
            case MenuAction.Title:
                if (!Application.CanStreamedLevelBeLoaded(titleSceneName) || !Application.CanStreamedLevelBeLoaded(loadingSceneName))
                {
                    Debug.LogWarning("[PauseMenu] タイトルシーンを読み込めません: " + titleSceneName, this);
                    return;
                }

                StartCoroutine(ReturnToTitle());
                break;
            default:
                Debug.Log("[PauseMenu] " + item.itemName + " を選択", this);
                break;
        }
    }

    private IEnumerator ReturnToTitle()
    {
        transitioning = true;
        SetModulesEnabled(false);
        double startedAt = Time.realtimeSinceStartupAsDouble;
        yield return Fade(false);
        // Let the confirmation SE finish before its scene-owned AudioSource is destroyed.
        float clipDuration = selectionConfirmSe != null ? selectionConfirmSe.length / Mathf.Max(0.01f, Mathf.Abs(uiAudioSource.pitch)) : 0f;
        while (Time.realtimeSinceStartupAsDouble - startedAt < clipDuration)
        {
            yield return null;
        }

        Restore();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        LoadingSceneRequest.Begin(titleSceneName);
        SceneManager.LoadScene(loadingSceneName);
    }

    private static void HidePanel(CanvasGroup panel)
    {
        if (panel == null)
        {
            return;
        }

        panel.interactable = false;
        panel.blocksRaycasts = false;
        panel.gameObject.SetActive(false);
    }

    private void ShowPanel(CanvasGroup panel, Button backButton)
    {
        if (panel == null)
        {
            Debug.LogWarning("[PauseMenu] 表示先パネルをInspectorに登録してください。", this);
            return;
        }

        if (mainMenuGroup != null && (panel == mainMenuGroup || panel.transform.IsChildOf(mainMenuGroup.transform)))
        {
            Debug.LogWarning("[PauseMenu] サブパネルはポーズUIの外へ配置してください。", this);
            return;
        }

        activePanel = panel;
        panelAlpha = panel.alpha > 0f ? panel.alpha : 1f;
        panel.alpha = panelAlpha;
        panel.gameObject.SetActive(true);
        panel.transform.SetAsLastSibling();
        panel.interactable = false;
        panel.blocksRaycasts = true;
        if (mainMenuGroup != null)
        {
            mainMenuGroup.alpha = 0f;
            mainMenuGroup.interactable = false;
            mainMenuGroup.blocksRaycasts = false;
        }

        subpanelFrame = Time.frameCount;
        GameInputManager.Instance.SetPaused(true);
        StartCoroutine(EnablePanelInput(panel, backButton));
    }

    private IEnumerator EnablePanelInput(CanvasGroup panel, Button backButton)
    {
        // Do not pass the opening Enter key to the new panel's default button.
        var input = GameInputManager.Instance;
        yield return null;
        while (activePanel == panel && (Input.GetKey(input.confirm) || Input.GetKey(input.alternateConfirm) || Input.GetMouseButton(0)))
        {
            yield return null;
        }

        if (activePanel != panel || transitioning || owner != this)
        {
            yield break;
        }

        panel.interactable = true;
        SetModulesEnabled(true);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(backButton != null ? backButton.gameObject : null);
        }
    }

    public void BackToMenu()
    {
        if (owner != this || transitioning || activePanel == null || Time.frameCount == subpanelFrame)
        {
            return;
        }

        PlayUiSound(selectionConfirmSe);
        SetModulesEnabled(false);
        HidePanel(activePanel);
        activePanel = null;
        if (mainMenuGroup != null)
        {
            mainMenuGroup.alpha = visibleAlpha.ContainsKey(mainMenuGroup) ? visibleAlpha[mainMenuGroup] : 1f;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        GameInputManager.Instance.SetPaused(true);
        UpdateAccent();
    }

    private void SetModulesEnabled(bool value)
    {
        foreach (BaseInputModule module in suspendedModules)
        {
            if (module != null)
            {
                module.enabled = value;
            }
        }
    }

    private void Restore()
    {
        if (owner != this)
        {
            return;
        }

        HidePanel(activePanel);
        activePanel = null;
        if (pauseGroups != null)
        {
            foreach (CanvasGroup group in pauseGroups)
            {
                if (group != null)
                {
                    group.alpha = 0f;
                    group.gameObject.SetActive(false);
                }
            }
        }

        foreach (GroupState state in hiddenStates)
        {
            if (state.group != null)
            {
                state.group.alpha = state.alpha;
                state.group.interactable = state.interactable;
                state.group.blocksRaycasts = state.blocksRaycasts;
            }
        }

        hiddenStates.Clear();
        foreach (BaseInputModule module in suspendedModules)
        {
            if (module != null)
            {
                module.enabled = true;
            }
        }

        suspendedModules.Clear();
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(savedSelectedObject);
        }

        Time.timeScale = savedTimeScale;
        AudioListener.pause = savedAudioPause;
        Cursor.lockState = savedCursorLock;
        Cursor.visible = savedCursorVisible;
        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.SetPaused(false);
        }

        owner = null;
        IsPaused = false;
        transitioning = false;
    }

    // コンポーネントが無効になったときの処理です。
    private void OnDisable()
    {
        StopAllCoroutines();
        Restore();
    }

    // オブジェクトが破棄されるときの終了処理です。
    private void OnDestroy()
    {
        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.RemoveListener(BackToMenu);
        }

        if (controlsBackButton != null)
        {
            controlsBackButton.onClick.RemoveListener(BackToMenu);
        }
    }
}
