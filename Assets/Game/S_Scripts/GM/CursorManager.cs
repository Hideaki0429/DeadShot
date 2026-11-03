// 【役割】画面や入力状態に応じたマウスカーソルの表示・ロックを管理する。
using UnityEngine;
using UnityEngine.SceneManagement;

public class CursorManager : MonoBehaviour
{
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "Result")
        {
            SetCursorState(false); // 表示
        }
        else
        {
            SetCursorState(true); // 非表示・ロック
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        // Resultシーンではカーソル制御しない
        if (sceneName == "Result")
        {
            return;
        }

        if (GameInputManager.Instance.Pressed(GameInputManager.Instance.releaseCursor))
        {
            SetCursorState(false);
        }

        if (GameInputManager.Instance.AttackPressed)
        {
            SetCursorState(true);
        }
    }

    public void SetCursorState(bool isLocked)
    {
        if (isLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
