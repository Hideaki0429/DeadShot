// 【役割】UIの頂点色にグラデーションを適用する。
using UnityEngine;
using UnityEngine.UI;

/// <summary>Editable, generated gradient overlay. No texture asset is required.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
[AddComponentMenu("UI/UI Gradient Filter")]
public sealed class UIGradientFilter : MonoBehaviour
{
    public enum GradientDirection
    {
        Horizontal,
        Vertical
    }

    [Header("グラデーション（色・透明度）")]
    [SerializeField]
    private Gradient gradient = CreateDefaultGradient();
    [SerializeField]
    private GradientDirection direction = GradientDirection.Horizontal;
    [Tooltip("横方向は左→右、縦方向は上→下。オンで反転します。")]
    [SerializeField]
    private bool reverse;
    [SerializeField, Range(0f, 1f)]
    private float opacity = 1f;
    private const int Resolution = 256;
    private RawImage target;
    private Texture2D generatedTexture;
    private Texture previousTexture;
    private bool previousRaycastTarget;
    private bool capturedSettings;
    private bool dirty = true;
    private GradientDirection builtDirection;
    private static Gradient CreateDefaultGradient()
    {
        var result = new Gradient();
        result.SetKeys(new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.45f, 0.5f), new GradientAlphaKey(0f, 1f) });
        return result;
    }

    // コンポーネントが有効になったときの処理です。
    private void OnEnable()
    {
        target = GetComponent<RawImage>();
        previousTexture = target.texture;
        previousRaycastTarget = target.raycastTarget;
        capturedSettings = true;
        dirty = true;
        Rebuild();
    }

    // OnValidate can be called during deserialization. Defer Unity object creation to Update.
    private void OnValidate()
    {
        dirty = true;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    private void Update()
    {
        if (dirty || generatedTexture == null)
        {
            Rebuild();
        }
    }

    [ContextMenu("グラデーションを更新")]
    public void Refresh()
    {
        dirty = true;
    }

    private void Rebuild()
    {
        if (target == null)
        {
            target = GetComponent<RawImage>();
        }

        if (target == null)
        {
            return;
        }

        if (gradient == null)
        {
            gradient = CreateDefaultGradient();
        }

        if (generatedTexture == null || builtDirection != direction)
        {
            ReleaseTexture();
            bool horizontal = direction == GradientDirection.Horizontal;
            generatedTexture = new Texture2D(horizontal ? Resolution : 1, horizontal ? 1 : Resolution, TextureFormat.RGBA32, false);
            generatedTexture.name = "UI Gradient Filter (Generated)";
            generatedTexture.hideFlags = HideFlags.HideAndDontSave;
            generatedTexture.wrapMode = TextureWrapMode.Clamp;
            generatedTexture.filterMode = FilterMode.Bilinear;
            builtDirection = direction;
        }

        var pixels = new Color[Resolution];
        for (int i = 0; i < Resolution; i++)
        {
            float t = i / (float)(Resolution - 1);
            // Texture rows start at the bottom; Inspector gradient starts at the top.
            if (direction == GradientDirection.Vertical)
            {
                t = 1f - t;
            }

            if (reverse)
            {
                t = 1f - t;
            }

            Color color = gradient.Evaluate(t);
            color.a *= Mathf.Clamp01(opacity);
            pixels[i] = color;
        }

        generatedTexture.SetPixels(pixels);
        generatedTexture.Apply(false, false);
        target.texture = generatedTexture;
        target.raycastTarget = false;
        dirty = false;
    }

    // コンポーネントが無効になったときの処理です。
    private void OnDisable()
    {
        if (target != null && capturedSettings)
        {
            if (target.texture == generatedTexture)
            {
                target.texture = previousTexture;
            }

            target.raycastTarget = previousRaycastTarget;
        }

        capturedSettings = false;
        ReleaseTexture();
    }

    // オブジェクトが破棄されるときの終了処理です。
    private void OnDestroy()
    {
        ReleaseTexture();
    }

    private void ReleaseTexture()
    {
        if (generatedTexture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(generatedTexture);
        }
        else
        {
            DestroyImmediate(generatedTexture);
        }

        generatedTexture = null;
    }
}
