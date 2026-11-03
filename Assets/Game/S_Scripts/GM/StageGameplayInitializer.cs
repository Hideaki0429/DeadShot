// 【役割】ステージの開始位置、入力、HUDと制限時間などを初期化する。
using UnityEngine;
using System.Collections;

/// <summary>
/// ヘリ着陸やチュートリアルを経由せず、直接プレイを開始するステージの初期化。
/// </summary>
[DefaultExecutionOrder(-850)]
public sealed class StageGameplayInitializer : MonoBehaviour
{
    [SerializeField]
    private UnityChan.UnityChanControlScriptWithRgidBody playerMovement;
    [SerializeField]
    private bool showLocationOnStart = true;
    [SerializeField]
    private string locationName = "廃病院館内";
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        if (playerMovement == null)
        {
            playerMovement = FindObjectOfType<UnityChan.UnityChanControlScriptWithRgidBody>();
        }

        if (playerMovement != null)
        {
            playerMovement.canControl = true;
        }

        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.ResetForGameplay();
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    private void Start()
    {
        // GameInputManagerがRuntimeInitializeOnLoadで生成された場合も確実に初期化。
        if (playerMovement != null)
        {
            playerMovement.canControl = true;
        }

        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.ResetForGameplay();
        }

        StartCoroutine(ConfirmGameplayReady());
        if (showLocationOnStart)
        {
            StartCoroutine(ShowLocationWhenLoadingFinished());
        }
    }

    private IEnumerator ConfirmGameplayReady()
    {
        yield return null;
        if (playerMovement != null)
        {
            playerMovement.canControl = true;
        }

        if (GameInputManager.Instance != null && GameInputManager.Instance.Mode != GameInputManager.InputMode.Gameplay)
        {
            GameInputManager.Instance.ResetForGameplay();
        }

        Debug.Log($"[StageGameplayInitializer] canControl=" + $"{(playerMovement != null && playerMovement.canControl)} " + $"inputMode={(GameInputManager.Instance != null ? GameInputManager.Instance.Mode.ToString() : "Missing")}", this);
    }

    private IEnumerator ShowLocationWhenLoadingFinished()
    {
        while (LoadingSceneRequest.IsLoading)
        {
            yield return null;
        }

        yield return null;
        LocationAnnouncementUI.Instance?.Show(locationName);
    }
}
