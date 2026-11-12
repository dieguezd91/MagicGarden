using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Shared backdrop placed behind modal UI (inventory, level-up reward): a real blur of the world plus a dark tint, fading with unscaled time.
// Layout (all authored in the prefab): this object carries the CanvasGroup; the blur RawImage sits under the tint Image that blocks clicks.
// On Show the world camera is rendered once (the Screen Space Overlay UI is not part of it), downsampled and blurred on the GPU
// (separable gaussian, see MagicGarden/UI/BackdropBlur), and shown in the RawImage until the backdrop is hidden.
[RequireComponent(typeof(CanvasGroup))]
public sealed class UIBackdrop : MonoBehaviour
{
    // Opacity of the black tint over the blur (and the only dimming when no blur could be captured).
    public const float DimAlpha = 0.45f;
    public const string BlurShaderName = "MagicGarden/UI/BackdropBlur";
    private static readonly int DirectionId = Shader.PropertyToID("_Direction");
    [SerializeField] private float showDuration = 0.08f;
    [SerializeField] private float hideDuration = 0.3f;
    [Header("Blur")]
    [SerializeField] private RawImage blurImage;
    [SerializeField, Range(1, 8)] private int downsample = 4;
    [SerializeField, Range(1, 6)] private int iterations = 3;
    private CanvasGroup group;
    private Tween tween;
    private RenderTexture blurTexture;
    private Material blurPass;

    public bool IsShown { get; private set; }
    public bool HasBlur => blurTexture != null;

    private CanvasGroup Group => group != null ? group : group = GetComponent<CanvasGroup>();

    public void Show()
    {
        bool wasShown = IsShown;
        IsShown = true;
        Kill();
        gameObject.SetActive(true);
        Group.blocksRaycasts = true;
        if (!wasShown || blurTexture == null) CaptureBlur();
        if (!Application.isPlaying) { Group.alpha = 1f; return; }
        tween = Group.DOFade(1f, showDuration).SetUpdate(true).SetEase(Ease.OutCubic);
    }

    public void Hide()
    {
        if (!Application.isPlaying || !gameObject.activeInHierarchy) { HideImmediate(); return; }
        IsShown = false;
        Kill();
        Group.blocksRaycasts = false;
        tween = Group.DOFade(0f, hideDuration).SetUpdate(true).SetEase(Ease.OutCubic)
            .OnComplete(() => { if (!IsShown) { gameObject.SetActive(false); ReleaseBlur(); } });
    }

    // No fade and no tween: for teardown and for replacing one modal by another.
    public void HideImmediate()
    {
        IsShown = false;
        Kill();
        Group.blocksRaycasts = false;
        Group.alpha = 0f;
        gameObject.SetActive(false);
        ReleaseBlur();
    }

    // Renders the world camera into a small texture and blurs it. Never throws: without a camera/material the tint alone remains.
    private void CaptureBlur()
    {
        if (!Application.isPlaying) return;
        ReleaseBlur();
        if (blurImage == null) return;
        var source = blurImage.material;
        var camera = Camera.main;
        if (source == null || source.shader == null || source.shader.name != BlurShaderName || camera == null || camera.targetTexture != null) { blurImage.enabled = false; return; }
        int width = Mathf.Max(16, Screen.width / downsample), height = Mathf.Max(16, Screen.height / downsample);
        var scratch = RenderTexture.GetTemporary(width, height, 0);
        try
        {
            blurTexture = new RenderTexture(width, height, 24) { name = "UIBackdrop blur", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            camera.targetTexture = blurTexture;
            try { camera.Render(); } finally { camera.targetTexture = null; }
            scratch.filterMode = FilterMode.Bilinear; scratch.wrapMode = TextureWrapMode.Clamp;
            blurPass = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            for (int i = 1; i <= iterations; i++)
            {
                blurPass.SetVector(DirectionId, new Vector4(i, 0f, 0f, 0f)); Graphics.Blit(blurTexture, scratch, blurPass);
                blurPass.SetVector(DirectionId, new Vector4(0f, i, 0f, 0f)); Graphics.Blit(scratch, blurTexture, blurPass);
            }
            blurImage.texture = blurTexture;
            blurImage.enabled = true;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("UIBackdrop blur unavailable, using the tint only: " + exception.Message, this);
            ReleaseBlur();
        }
        finally { RenderTexture.ReleaseTemporary(scratch); }
    }

    private void ReleaseBlur()
    {
        if (blurImage != null) { blurImage.texture = null; blurImage.enabled = false; }
        if (blurTexture != null) { blurTexture.Release(); Destroy(blurTexture); blurTexture = null; }
        if (blurPass != null) { Destroy(blurPass); blurPass = null; }
    }

    private void Kill() { if (tween != null) { tween.Kill(); tween = null; } }
    private void OnDisable() { if (!IsShown) ReleaseBlur(); }
    private void OnDestroy() { Kill(); ReleaseBlur(); }
}
