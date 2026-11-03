// 【役割】再生音声の大きさから波形UIを描画する。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AudioSource の音量に合わせて、UI の縦バー波形を動かします。
/// </summary>
public class AudioWaveformUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private AudioSource targetAudio;
    [SerializeField]
    private RectTransform waveformArea;
    [SerializeField]
    private Image barPrefab;
    [Header("バーの見た目")]
    [SerializeField, Min(1)]
    private int barCount = 32;
    [SerializeField, Min(1f)]
    private float barWidth = 4f;
    [SerializeField, Min(0f)]
    private float barSpacing = 5f;
    [SerializeField, Min(1f)]
    private float minimumHeight = 6f;
    [SerializeField, Min(1f)]
    private float maximumHeight = 56f;
    [Header("動き")]
    [SerializeField, Min(0f)]
    private float audioSensitivity = 35f;
    [SerializeField, Min(0f)]
    private float idleMovement = 0.08f;
    [SerializeField, Min(0.01f)]
    private float responseSpeed = 14f;
    [SerializeField, Range(0f, 1f)]
    private float mountainShapeStrength = 0.85f;
    private const int SampleCount = 64;
    private readonly float[] samples = new float[SampleCount];
    private readonly List<Image> bars = new List<Image>();
    private float[] currentHeights;
    // 最初のフレーム更新前に呼ばれます。初期化済みのオブジェクトを使って開始処理を行います。
    private void Start()
    {
        CreateBars();
        LayoutBars();
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (bars.Count == 0)
        {
            return;
        }

        UpdateBars();
    }

    private void OnRectTransformDimensionsChange()
    {
        LayoutBars();
    }

    /// <summary>
    /// Inspector で指定した Image を複製して、波形用の縦バーを生成します。
    /// </summary>
    private void CreateBars()
    {
        if (waveformArea == null || barPrefab == null)
        {
            Debug.LogWarning("AudioWaveformUI: Waveform Area と Bar Prefab を設定してください。", this);
            enabled = false;
            return;
        }

        bars.Clear();
        currentHeights = new float[barCount];
        for (int i = 0; i < barCount; i++)
        {
            Image bar = Instantiate(barPrefab, waveformArea);
            bar.name = $"Wave Bar {i + 1:00}";
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(barWidth, minimumHeight);
            bars.Add(bar);
            currentHeights[i] = minimumHeight;
        }
    }

    /// <summary>
    /// 波形エリアの幅に合わせて、生成したバーを均等に並べます。
    /// </summary>
    private void LayoutBars()
    {
        if (waveformArea == null || bars.Count == 0)
        {
            return;
        }

        float totalWidth = barCount * barWidth + (barCount - 1) * barSpacing;
        float startX = (waveformArea.rect.width - totalWidth) * 0.5f + barWidth * 0.5f;
        for (int i = 0; i < bars.Count; i++)
        {
            bars[i].rectTransform.anchoredPosition = new Vector2(startX + i * (barWidth + barSpacing), 0f);
        }
    }

    /// <summary>
    /// 音量（RMS）と小さな待機揺れを合成して、各バーの高さを更新します。
    /// </summary>
    private void UpdateBars()
    {
        float audioLevel = GetAudioLevel();
        for (int i = 0; i < bars.Count; i++)
        {
            float individualMovement = Mathf.PerlinNoise(i * 0.31f, Time.time * 2.2f);
            float activity = Mathf.Clamp01(audioLevel * (0.55f + individualMovement * 0.7f) + individualMovement * idleMovement);
            float mountainShape = GetMountainShape(i);
            float level = Mathf.Lerp(activity, activity * mountainShape, mountainShapeStrength);
            float targetHeight = Mathf.Lerp(minimumHeight, maximumHeight, level);
            currentHeights[i] = Mathf.Lerp(currentHeights[i], targetHeight, Time.deltaTime * responseSpeed);
            bars[i].rectTransform.sizeDelta = new Vector2(barWidth, currentHeights[i]);
        }
    }

    /// <summary>
    /// 両端を低く、中央を高くする山形の係数を返します。
    /// </summary>
    private float GetMountainShape(int barIndex)
    {
        if (barCount <= 1)
        {
            return 1f;
        }

        float normalizedIndex = barIndex / (float)(barCount - 1);
        return 1f - Mathf.Abs(normalizedIndex * 2f - 1f);
    }

    /// <summary>
    /// 再生中の音声データから RMS 音量を取り出し、0〜1の範囲に整えます。
    /// </summary>
    private float GetAudioLevel()
    {
        if (targetAudio == null || !targetAudio.isPlaying)
        {
            return 0f;
        }

        targetAudio.GetOutputData(samples, 0);
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }

        float rms = Mathf.Sqrt(sum / samples.Length);
        return Mathf.Clamp01(rms * audioSensitivity);
    }
}
