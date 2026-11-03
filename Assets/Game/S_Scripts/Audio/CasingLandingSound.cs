// 【役割】排莢された薬莢が地面に当たったときの音を再生する。
using UnityEngine;

/// <summary>薬莢の最初の着地だけで鳴らす。跳ね返りでは再生しない。</summary>
public class CasingLandingSound : MonoBehaviour
{
    bool played;
    // 物理的に衝突した相手と接触情報を受け取り、以下の衝突処理を行います。
    void OnCollisionEnter(Collision collision)
    {
        if (played || collision.collider.GetComponentInParent<PlayerHealth>() != null || collision.collider.GetComponentInParent<EnemyHealth>() != null)
        {
            return;
        }

        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y < 0.3f)
            {
                continue;
            }

            played = true;
            GameSoundService.Play(GameSound.CasingLand, contact.point);
            break;
        }
    }
}
