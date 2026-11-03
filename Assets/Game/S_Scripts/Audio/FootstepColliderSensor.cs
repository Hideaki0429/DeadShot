// 【役割】足音の接触検出に使われていた参照を互換性のために保持する。
using UnityEngine;

// Retained only to preserve existing scene references. Contact-based playback is retired.
public sealed class FootstepColliderSensor : MonoBehaviour
{
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        foreach (Collider sensor in GetComponents<Collider>())
        {
            sensor.enabled = false;
        }

        enabled = false;
    }

    public void Configure(FootstepAudioController targetController)
    {
    }
}
