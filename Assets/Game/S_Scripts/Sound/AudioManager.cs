// 【役割】音声の再生と音量などの共通設定を管理する。
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField]
    private AudioSource audioSource1;
    [SerializeField]
    private AudioSource audioSource3; // 歩行音
    [SerializeField]
    private AudioSource audioSource4; // 呼吸音
    [SerializeField]
    private AudioSource audioSourceReload;
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private OnKeyPress_Throw throwScript; // Inspectorで指定
    [Tooltip("SMG用射撃コンポーネント。空欄ならWeapon Idがsmgのものを自動取得します。")]
    [SerializeField]
    private OnKeyPress_Throw smgThrowScript;
    [Header("チュートリアル連携")]
    [SerializeField]
    private TutorialUIManager tutorialUI; // 【追加】InspectorでチュートリアルUIを指定
    private bool prevSetup = false;
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
        if (audioSource3 != null)
        {
            audioSource3.Stop();
            audioSource3.playOnAwake = false;
        }

        if (smgThrowScript == null)
        {
            foreach (OnKeyPress_Throw candidate in FindObjectsOfType<OnKeyPress_Throw>(true))
            {
                if (candidate != null && candidate.requiredWeaponId == "smg")
                {
                    smgThrowScript = candidate;
                    break;
                }
            }
        }

        // ハンドガンとSMGで同じリロード音を使用する。
        if (throwScript != null)
        {
            throwScript.onReload += PlayReloadSound;
        }

        if (smgThrowScript != null && smgThrowScript != throwScript)
        {
            smgThrowScript.onReload += PlayReloadSound;
        }
    }

    // オブジェクトが破棄されるときの終了処理です。
    void OnDestroy()
    {
        // メモリリーク防止
        if (throwScript != null)
        {
            throwScript.onReload -= PlayReloadSound;
        }

        if (smgThrowScript != null && smgThrowScript != throwScript)
        {
            smgThrowScript.onReload -= PlayReloadSound;
        }
    }

    void PlayReloadSound()
    {
        if (audioSourceReload != null)
        {
            audioSourceReload.Play();
        }
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        bool isSetup = animator.GetBool("Setup");
        if (isSetup && !prevSetup)
        {
            if (audioSource1 != null)
            {
                audioSource1.Play();
            }
        }

        prevSetup = isSetup;
        // ------------------------------------------------------------------
        // 【追加】キャラクターが実際に動ける（移動解禁）状態かチェック
        // ------------------------------------------------------------------
        if (tutorialUI != null && !tutorialUI.isMovementUnlocked)
        {
            // まだ移動が解禁されていない（ルール説明中など）なら、音を確実に止めて処理を抜ける
            if (audioSource3 != null && audioSource3.isPlaying)
            {
                audioSource3.Stop();
            }

            if (audioSource4 != null && audioSource4.isPlaying)
            {
                audioSource4.Stop();
            }

            return;
        }

        // --- ここから下は、移動解禁（実際に動けるようになってから）実行される ---
        float vertical = GameInputManager.Instance.Move.y;
        bool isForward = vertical > 0.1f;
        bool isMoving = Mathf.Abs(vertical) > 0.1f;
        // 旧ループ足音は廃止。FootstepAudioController が距離基準で再生する。
        // 呼吸音の処理
        if (audioSource4 != null)
        {
            if (!isSetup && isForward)
            {
                if (!audioSource4.isPlaying)
                {
                    audioSource4.Play();
                }
            }
            else
            {
                if (audioSource4.isPlaying)
                {
                    audioSource4.Stop();
                }
            }
        }
    }
}
