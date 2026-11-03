// 【役割】キー設定UIのレイアウトを表示領域に合わせて調整する。
using UnityEngine;

[ExecuteAlways]
public sealed class KeyBindingLayoutFit : MonoBehaviour
{
    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        var parent = transform.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        float scale = Mathf.Min(parent.rect.width / 1600f, parent.rect.height / 900f);
        transform.localScale = Vector3.one * Mathf.Max(.01f, scale);
    }
}
