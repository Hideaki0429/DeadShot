// 【役割】残り制限時間と感染耐性をHUDの色に反映し、拠点での表示を管理する。
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>プレイヤーの感染耐性を残り時間として管理する。</summary>
public sealed class PlayerInfectionResistance : MonoBehaviour
{
    [Header("感染耐性（秒）")]
    [Tooltip("感染耐性の上限。現在の仕様は20分（1200秒）。")]
    [Min(1f)]
    public float maximumResistanceSeconds = 1200f;
    [Min(0f)]
    public float initialResistanceSeconds = 300f;
    [Tooltip("感染緩和薬1個で増える時間。デフォルトは5分。")]
    [Min(1f)]
    public float medicineBonusSeconds = 300f;
    [Header("HUD（体力HUD横の小さい○）")]
    public Image resistanceIcon;
    public Color safeColor = new Color(.1f, .85f, 1f, 1f);
    public Color warningColor = new Color(1f, .75f, .08f, 1f);
    public Color criticalColor = Color.red;
    [Range(0f, 1f)]
    public float warningRatio = .35f;
    [Range(0f, 1f)]
    public float criticalRatio = .12f;
    [Header("拠点シーン")]
    [Tooltip("ONの場合、指定シーン中は感染耐性をMAX・緑で固定します。")]
    [SerializeField]
    bool freezeAtMaximumInBaseScene = true;
    [SerializeField]
    string baseSceneName = "Stage_city";
    [SerializeField]
    Color baseMaximumColor = Color.green;
    public float RemainingSeconds { get; private set; }
    public float ResistanceRatio => Mathf.Clamp01(RemainingSeconds / Mathf.Max(1f, maximumResistanceSeconds));
    public bool IsExpired => RemainingSeconds <= 0f;

    public event Action Expired;
    bool expirationSent;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        RemainingSeconds = IsBaseSceneFrozen() ? Mathf.Max(1f, maximumResistanceSeconds) : Mathf.Clamp(initialResistanceSeconds, 0f, maximumResistanceSeconds);
        RefreshUI(IsBaseSceneFrozen());
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (IsBaseSceneFrozen())
        {
            RemainingSeconds = Mathf.Max(1f, maximumResistanceSeconds);
            expirationSent = false;
            RefreshUI(true);
            return;
        }

        if (RemainingSeconds <= 0f || Time.timeScale <= 0f || PauseMenuController.IsPaused)
        {
            return;
        }

        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Time.deltaTime);
        if (RemainingSeconds <= 0f && !expirationSent)
        {
            expirationSent = true;
            Expired?.Invoke();
            Debug.Log("[Infection] 感染耐性の制限時間が0になりました。", this);
        }

        RefreshUI(false);
    }

    public void AddMedicineTime()
    {
        float before = RemainingSeconds;
        RemainingSeconds = Mathf.Clamp(RemainingSeconds + medicineBonusSeconds, 0f, Mathf.Max(1f, maximumResistanceSeconds));
        float actuallyAdded = RemainingSeconds - before;
        expirationSent = false;
        RefreshUI();
        if (ScoreAndTimeManager.instance != null)
        {
            ScoreAndTimeManager.instance.AddTime(actuallyAdded);
        }
        else
        {
            Debug.LogWarning("[Infection] ScoreAndTimeManagerが見つからず、画面の制限時間を延長できません。", this);
        }

        GameSoundService.Play(GameSound.HealComplete, transform.position, Time.timeScale == 0f || AudioListener.pause);
        Debug.Log($"[Infection] 感染耐性 {before:F1}秒 → {RemainingSeconds:F1}秒", this);
    }

    public void SetRemainingSeconds(float seconds)
    {
        RemainingSeconds = Mathf.Clamp(seconds, 0f, Mathf.Max(1f, maximumResistanceSeconds));
        expirationSent = RemainingSeconds <= 0f;
        RefreshUI();
    }

    bool IsBaseSceneFrozen()
    {
        return freezeAtMaximumInBaseScene && SceneManager.GetActiveScene().name == baseSceneName;
    }

    void RefreshUI(bool forceMaximum = false)
    {
        if (resistanceIcon == null)
        {
            return;
        }

        resistanceIcon.type = Image.Type.Filled;
        resistanceIcon.fillMethod = Image.FillMethod.Radial360;
        resistanceIcon.fillOrigin = 2;
        resistanceIcon.fillClockwise = true;
        float ratio = forceMaximum ? 1f : ResistanceRatio;
        resistanceIcon.fillAmount = ratio;
        resistanceIcon.color = forceMaximum ? baseMaximumColor : ratio <= criticalRatio ? criticalColor : ratio <= warningRatio ? warningColor : safeColor;
    }
}
