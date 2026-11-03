// 【役割】衝突をきっかけに自身のオブジェクトを破棄する。
using System.Collections;
using UnityEngine;

public class OnCollision_Destroy_Me : MonoBehaviour
{
    public string tagName; // タグ名：Inspectorで指定
    // 物理的に衝突した相手と接触情報を受け取り、以下の衝突処理を行います。
    private void OnCollisionEnter(Collision collision) // 衝突したとき
    {
        // もし、衝突したものが目標のタグを持っていたら
        if (collision.gameObject.tag == tagName)
        {
            // 1秒後に自分を削除する
            Destroy(this.gameObject, 1.0f);
        }
    }
}
