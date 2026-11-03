// 【役割】指定ターゲットの方向へオブジェクトを向ける。
using UnityEngine;
using System.Collections;

public class LookAtTargetController : MonoBehaviour
{
    public Transform Target;
    public bool smooth = true;
    public float damping = 6.0f;
    // Use this for initialization
    void Start()
    {
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        if (Target != null)
        {
            if (smooth)
            {
                // Look at and dampen the rotation
                var rotation = Quaternion.LookRotation(Target.position - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * damping);
            }
            else
            {
                transform.LookAt(Target);
            }
        }
    }
}
