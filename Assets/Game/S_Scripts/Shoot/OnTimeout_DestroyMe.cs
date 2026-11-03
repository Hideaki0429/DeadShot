// 【役割】指定時間が経過したオブジェクトを破棄する。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 時間が経ったら、自分自身を削除する
public class OnTimeout_DestroyMe : MonoBehaviour
{
    public float limitSec = 3; // 秒数：Inspectorで指定
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start() // 最初に、指定した秒数で削除する予約をする
    {
        Destroy(this.gameObject, limitSec);
    }
}
