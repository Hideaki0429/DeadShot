// 【役割】ゾンビの状態に合わせた声の再生を管理する。
using UnityEngine;

public class ZombieVoiceSound : MonoBehaviour
{
    float nextVoice;
    bool dead;
    AudioSource voice;
    // コンポーネントが有効になったときの処理です。
    void OnEnable()
    {
        Schedule();
    }

    void Schedule()
    {
        var interval = GameSoundService.Bank != null ? GameSoundService.Bank.voiceInterval : new Vector2(6, 12);
        nextVoice = Time.time + Random.Range(Mathf.Max(1, interval.x), Mathf.Max(interval.x, interval.y));
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        if (dead || Time.time < nextVoice)
        {
            return;
        }

        Schedule();
        if (!GameInputManager.Instance.Gameplay || (voice != null && voice.isPlaying))
        {
            return;
        }

        var camera = Camera.main;
        var entry = GameSoundService.Bank != null ? GameSoundService.Bank.Find(GameSound.ZombieVoice) : null;
        if (camera == null || entry == null || Vector3.Distance(camera.transform.position, transform.position) > entry.maxDistance)
        {
            return;
        }

        voice = GameSoundService.Play(GameSound.ZombieVoice, transform.position + Vector3.up);
    }

    // 通常のフレーム更新後に呼ばれます。更新済みの状態を使って以下の処理を行います。
    void LateUpdate()
    {
        if (voice != null)
        {
            voice.transform.position = transform.position + Vector3.up;
        }
    }

    public void Die()
    {
        dead = true;
        if (voice != null)
        {
            voice.Stop();
        }
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        if (voice != null)
        {
            voice.Stop();
        }
    }
}
