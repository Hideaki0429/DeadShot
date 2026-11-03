// 【役割】ヘリ操作用のキー入力を集め、操作イベントとして通知する。
using System;
using System.Collections.Generic;
using UnityEngine;

public class ControlPanel : MonoBehaviour
{
    public AudioSource MusicSound;
    [SerializeField]
    KeyCode SpeedUp = KeyCode.Space;
    [SerializeField]
    KeyCode SpeedDown = KeyCode.C;
    [SerializeField]
    KeyCode Forward = KeyCode.W;
    [SerializeField]
    KeyCode Back = KeyCode.S;
    [SerializeField]
    KeyCode Left = KeyCode.A;
    [SerializeField]
    KeyCode Right = KeyCode.D;
    [SerializeField]
    KeyCode TurnLeft = KeyCode.Q;
    [SerializeField]
    KeyCode TurnRight = KeyCode.E;
    [SerializeField]
    KeyCode MusicOffOn = KeyCode.M;
    private KeyCode[] keyCodes;
    public Action<PressedKeyCode[]> KeyPressed;
    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    private void Awake()
    {
        keyCodes = new[]
        {
            SpeedUp,
            SpeedDown,
            Forward,
            Back,
            Left,
            Right,
            TurnLeft,
            TurnRight
        };
    }

    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    void Start()
    {
    }

    // 物理演算の一定間隔で呼ばれます。以下の状態更新を行います。
    void FixedUpdate()
    {
        var pressedKeyCode = new List<PressedKeyCode>();
        if (GameInputManager.Instance != null && GameInputManager.Instance.Mode != GameInputManager.InputMode.Disabled)
        {
            if (KeyPressed != null)
            {
                KeyPressed(pressedKeyCode.ToArray());
            }

            return;
        }

        for (int index = 0; index < keyCodes.Length; index++)
        {
            var keyCode = keyCodes[index];
            if (Input.GetKey(keyCode))
            {
                pressedKeyCode.Add((PressedKeyCode)index);
            }
        }

        if (KeyPressed != null)
        {
            KeyPressed(pressedKeyCode.ToArray());
        }

        // for test
        if (Input.GetKey(MusicOffOn))
        {
            if (MusicSound.volume == 1)
            {
                return;
            }

            /*            if (MusicSound.isPlaying)
                MusicSound.Stop();
            else*/
            MusicSound.volume = 1;
            MusicSound.Play();
        }
    }
}
