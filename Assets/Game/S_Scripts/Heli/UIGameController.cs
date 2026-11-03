// 【役割】ヘリの操作説明パネルの開閉とゲーム再開始を扱う。
using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class UIGameController : MonoBehaviour
{
    public Text EngineForceView;
    public GameObject RestartButton;
    public GameObject InfoButton;
    public GameObject InfoPanel;
    // Use this for initialization
    public static UIGameController runtime;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        runtime = this;
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
    //ShowInfo();
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void ShowInfoPanel(bool isShow)
    {
        EngineForceView.gameObject.SetActive(!isShow);
        RestartButton.SetActive(!isShow);
        InfoButton.SetActive(!isShow);
        InfoPanel.SetActive(isShow);
    }

    public void ShowInfo()
    {
        ShowInfoPanel(true);
    }

    public void HideInfo()
    {
        ShowInfoPanel(false);
    }

    public void RestartGame()
    {
        Application.LoadLevel("Main");
    }
}
