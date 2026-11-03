// 【役割】効果音の識別名と音声クリップ・再生設定を保持する。
using System;
using UnityEngine;
using UnityEngine.Audio;

public enum GameSound
{
    CasingLand,
    EmptyGun,
    PlayerImpact,
    PlayerBite,
    ZombieDeath,
    ZombieVoice,
    BloodDrip,
    Drinking,
    HealComplete,
    GrenadePinPull,
    GrenadeRoll,
    GrenadeExplosion,
    LightningThunder,
    RainLoop
}

[CreateAssetMenu(menuName = "Dead Shot/SE Bank")]
public class GameSoundBank : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public GameSound sound;
        public AudioClip[] clips = new AudioClip[0];
        [Range(0, 1)]
        public float volume = 0.8f;
        public Vector2 pitch = new Vector2(0.95f, 1.05f);
        public bool spatial = true;
        [Min(1)]
        public float maxDistance = 25;
    }

    public AudioMixer mixer;
    public AudioMixerGroup effectsGroup;
    public AudioMixerGroup uiGroup;
    public AudioMixerGroup voiceGroup;
    public AudioMixerGroup ambienceGroup;
    [Header("インベントリ・設定画面専用BGM（タイトル／本編共通）")]
    public AudioClip menuBgm;
    [Range(0, 1)]
    public float menuBgmVolume = .15f;
    [Min(.01f)]
    public float menuBgmFadeSeconds = .5f;
    [Tooltip("共通SE。Drinkingは飲む音、HealCompleteは回復成立音。未登録の場合は無音です。")]
    public Entry[] sounds = new Entry[0];
    [Tooltip("通常声の間隔（秒）。開始時もランダムに待機します。")]
    public Vector2 voiceInterval = new Vector2(6, 12);
    [Min(1)]
    public int maxSimultaneousVoices = 3;
    [Min(0)]
    public float bloodDripDelay = 0.15f;
    public Entry Find(GameSound sound) => Array.Find(sounds, x => x != null && x.sound == sound);
}
