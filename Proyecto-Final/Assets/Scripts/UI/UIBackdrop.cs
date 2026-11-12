using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Shared backdrop placed behind modal UI (inventory, the level-up session): a real blur of the world plus a dark tint, fading softly with unscaled time.
// The blur layer and the tint sit under this object's CanvasGroup, so they fade together. Show is idempotent: showing an already shown backdrop
// changes nothing (same snapshot, no restarted fade), so several modals in a row can share one continuous backdrop.
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
    [Header("Fade (unscaled time)")]
    [SerializeField, Min(0f)] private float showDuration = 0.35f;
    [SerializeField, Min(0f)] private float hideDuration = 0.35f;
    [SerializeField] private Ease showEase = Ease.OutQuad;
    [SerializeField] private Ease hideEase = Ease.InQuad;
    [Header("Blur")]
    [SerializeField] private RawImage blurImage;
    [SerializeField, Range(1, 8)] private int downsample = 4;
    [SerializeField, Range(1, 6)] private int iterations = 3;
    // A hitch (the blur capture, a shader compile) must not eat the fade, so one frame advances it by at most this much.
    private const float MaxFadeStep = 1f / 30f;
    private CanvasGroup group;
    private bool fading;
    private float fadeFrom, fadeTo, fadeDuration, fadeElapsed;
    private Ease fadeEase;
    private RenderTexture blurTexture;
    private Material blurPass;

    public bool IsShown { get; private set; }
    public bool HasBlur => blurTexture != null;

    private CanvasGroup Group => group != null ? group : group = GetComponent<CanvasGroup>();

    public void Show()
    {
        bool wasShown = IsShown;
        bool wasActive = gameObject.activeSelf;
        IsShown = true;
        if (wasShown && wasActive && (!Application.isPlaying || Group.alpha >= 1f || fading)) { Group.blocksRaycasts = true; return; } // already shown or fading in
        Kill();
        if (!wasActive) Group.alpha = 0f; // a fresh show always fades in from nothing, whatever alpha the prefab was saved with
        gameObject.SetActive(true);
        Group.blocksRaycasts = true;
        if (blurTexture == null) CaptureBlur(); // still alive while a hide is in progress: re-showing then keeps the same snapshot
        if (!Application.isPlaying) { Group.alpha = 1f; return; }
        StartFade(1f, showDuration, showEase);
    }

    public void Hide()
    {
        if (!Application.isPlaying || !gameObject.activeInHierarchy) { HideImmediate(); return; }
        IsShown = false;
        Kill();
        Group.blocksRaycasts = false;
        StartFade(0f, hideDuration, hideEase);
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

    // Unscaled-time fade of the whole group (blur layer and tint together), eased, resuming from the current alpha.
    private void StartFade(float target, float duration, Ease ease)
    {
        fadeFrom = Group.alpha; fadeTo = target; fadeEase = ease;
        fadeDuration = duration * Mathf.Abs(target - fadeFrom); // a partial fade (interrupted show/hide) takes proportionally less
        fadeElapsed = 0f;
        fading = fadeDuration > 0f;
        if (!fading) FinishFade();
    }

    private void Update()
    {
        if (!fading) return;
        fadeElapsed += Mathf.Min(Time.unscaledDeltaTime, MaxFadeStep);
        float t = Mathf.Clamp01(fadeElapsed / fadeDuration);
        Group.alpha = Mathf.Lerp(fadeFrom, fadeTo, DOVirtual.EasedValue(0f, 1f, t, fadeEase));
        if (t >= 1f) FinishFade();
    }

    private void FinishFade()
    {
        fading = false;
        Group.alpha = fadeTo;
        if (fadeTo <= 0f && !IsShown) { gameObject.SetActive(false); ReleaseBlur(); }
    }

    private void Kill() => fading = false;
    private void OnDisable() { if (!IsShown) ReleaseBlur(); }
    private void OnDestroy() { Kill(); ReleaseBlur(); }
}
