// 【役割】ゲーム内の音声を用途に対応した出力先へ振り分ける。
using UnityEngine;

public enum GameAudioCategory
{
    Effects,
    UI,
    Voice,
    Ambience
}

/// <summary>自動分類を変えたいAudioSourceの同じオブジェクトに追加する。</summary>
[DisallowMultipleComponent]
public class GameAudioRouting : MonoBehaviour
{
    public GameAudioCategory category;
}
