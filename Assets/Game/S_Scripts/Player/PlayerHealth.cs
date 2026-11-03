// 【役割】プレイヤーの現在HP、被ダメージ、回復と死亡処理を管理する。
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    private static readonly int SetupParameter = Animator.StringToHash("Setup");
    private static readonly int PlayerDamageParameter = Animator.StringToHash("PlayerDamage");
    private static readonly int HitWithPistolParameter = Animator.StringToHash("HitWithPistol");
    private static readonly int DamageReactionTypeParameter = Animator.StringToHash("DamageReactionType");
    [Header("プレイヤー体力")]
    public int maxHP = 500;
    public int currentHP;
    public float CurrentHP => currentHP;
    public float MaxHP => maxHP;

    public event System.Action Damaged;
    [Header("UI")]
    public Image hpBarFill;
    public Image conditionIcon; // HPバー横の◯
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI statusText;
    [Header("状態別カラー")]
    public Color normalColor = Color.green;
    public Color damagedColor = Color.yellow;
    public Color criticalColor = Color.red;
    [Header("被弾リアクション")]
    [SerializeField]
    private Animator playerAnimator;
    [SerializeField]
    private bool logDamageReaction;
    private bool isDead = false;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>();
        }
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        GameDamageSettings settings = GameDamageSettings.Load();
        if (settings != null)
        {
            maxHP = Mathf.Max(1, settings.playerMaxHP);
        }

        currentHP = maxHP;
        UpdateHealthUI();
    }

    public void Heal(int amount)
    {
        if (isDead || currentHP <= 0 || amount <= 0)
        {
            return;
        }

        int before = currentHP;
        currentHP = (int)System.Math.Min(maxHP, (long)currentHP + amount);
        UpdateHealthUI();
        if (currentHP > before)
        {
            GameSoundService.Play(GameSound.HealComplete, transform.position, Time.timeScale == 0 || AudioListener.pause);
        }
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, false);
    }

    public void TakeDamage(int damage, bool isBite)
    {
        TakeDamage(damage, isBite, transform.position + transform.forward);
    }

    public void TakeDamage(int damage, bool isBite, Vector3 attackerPosition)
    {
        if (isDead)
        {
            return;
        }

        // CancelAimForDamageでSetupが解除される前の装備状態を保存する。
        bool aimingWhenHit = playerAnimator != null && playerAnimator.GetBool(SetupParameter);
        bool handgunEquippedWhenHit = aimingWhenHit;
        bool smgEquippedWhenHit = false;
        WeaponSelectionUI weaponSelection = FindObjectOfType<WeaponSelectionUI>(true);
        if (weaponSelection != null)
        {
            handgunEquippedWhenHit &= weaponSelection.SelectedWeaponId == "handgun";
            smgEquippedWhenHit = aimingWhenHit && weaponSelection.SelectedWeaponId == "smg";
        }

        int previousHP = currentHP;
        bool tutorialProtected = TutorialCombatZone.PlayerProtected;
        if (!tutorialProtected)
        {
            currentHP -= damage;
        }

        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        UpdateHealthUI();
        if (currentHP < previousHP || (tutorialProtected && damage > 0))
        {
            if (currentHP > 0)
            {
                GetComponent<ThirdPersonCameraController>()?.PlayDamageCameraReaction(attackerPosition, isBite);
            }

            GetComponent<UnityChan.UnityChanControlScriptWithRgidBody>()?.CancelAimForDamage();
            // 構え解除後の通常ヒットへ即時移行する。
            if (currentHP > 0)
            {
                PlayDamageReaction(isBite, !isBite && handgunEquippedWhenHit, !isBite && smgEquippedWhenHit);
            }

            GameSoundService.Play(isBite ? GameSound.PlayerBite : GameSound.PlayerImpact, transform.position);
            Damaged?.Invoke();
        }

        if (!tutorialProtected && currentHP <= 0)
        {
            Die();
        }
    }

    private void PlayDamageReaction(bool isBite, bool hitWithPistol, bool hitWithSmg)
    {
        if (playerAnimator == null)
        {
            return;
        }

        playerAnimator.SetBool(HitWithPistolParameter, hitWithPistol);
        int reactionType = isBite ? 2 : (hitWithPistol || hitWithSmg) ? 1 : 0;
        playerAnimator.SetInteger(DamageReactionTypeParameter, reactionType);
        playerAnimator.ResetTrigger(PlayerDamageParameter);
        string stateName = isBite ? "HitOverReaction" : hitWithSmg ? "RifleHit" : hitWithPistol ? "PistolHit" : "StandingHit";
        bool played = false;
        // Base Layerだけでなく、被弾ステートを持つ上半身レイヤーも同期。
        // Triggerの遷移待ちや、他レイヤーの構えモーションによる上書きを避ける。
        for (int layer = 0; layer < playerAnimator.layerCount; layer++)
        {
            int state = Animator.StringToHash(playerAnimator.GetLayerName(layer) + "." + stateName);
            if (!playerAnimator.HasState(layer, state))
            {
                continue;
            }

            playerAnimator.Play(state, layer, 0f);
            played = true;
        }

        if (!played)
        {
            playerAnimator.SetTrigger(PlayerDamageParameter);
        }

        if (logDamageReaction)
        {
            Debug.Log($"[Player Damage] frame={Time.frameCount} time={Time.time:F3} state={stateName} immediate={played}", this);
        }
    }

    private void UpdateHealthUI()
    {
        // HP割合
        float hpRate = (float)currentHP / maxHP;
        // 0～1に制限
        hpRate = Mathf.Clamp01(hpRate);
        // HPバー
        if (hpBarFill != null)
        {
            hpBarFill.fillAmount = hpRate;
        }

        // HP数値
        if (hpText != null)
        {
            hpText.text = $"{currentHP} / {maxHP}";
        }

        string condition;
        Color conditionColor;
        if (hpRate > 0.6f)
        {
            condition = "NORMAL";
            conditionColor = normalColor;
        }
        else if (hpRate > 0.3f)
        {
            condition = "DAMAGED";
            conditionColor = damagedColor;
        }
        else if (hpRate > 0f)
        {
            condition = "CRITICAL";
            conditionColor = criticalColor;
        }
        else
        {
            condition = "DEAD";
            conditionColor = criticalColor;
        }

        // 状態表示
        if (statusText != null)
        {
            statusText.text = condition;
            statusText.color = conditionColor;
        }

        // HPバー色変更
        if (hpBarFill != null)
        {
            hpBarFill.color = conditionColor;
        }
    // HPバー横の◯はPlayerInfectionResistanceが感染耐性HUDとして管理する。
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("[Player] 死亡");
        WeaponSelectionUI selection = FindObjectOfType<WeaponSelectionUI>(true);
        if (playerAnimator != null && selection != null && selection.SelectedWeaponId == "smg")
        {
            string deathState = playerAnimator.GetBool(SetupParameter) ? "RifleDyingAiming" : "RifleDyingHeld";
            for (int layer = 0; layer < playerAnimator.layerCount; layer++)
            {
                int hash = Animator.StringToHash(playerAnimator.GetLayerName(layer) + "." + deathState);
                if (playerAnimator.HasState(layer, hash))
                {
                    playerAnimator.Play(hash, layer, 0f);
                }
            }
        }

        if (ScoreAndTimeManager.instance != null)
        {
            ScoreAndTimeManager.instance.LoadNextScene(false);
        }
    }
}
