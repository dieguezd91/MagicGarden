using DG.Tweening;
using UnityEngine;

// Shared dimmed backdrop placed behind modal UI (inventory, level-up reward). Fades with unscaled time.
[RequireComponent(typeof(CanvasGroup))]
public sealed class UIBackdrop : MonoBehaviour
{
    [SerializeField] private float showDuration = 0.08f;
    [SerializeField] private float hideDuration = 0.3f;
    private CanvasGroup group;
    private Tween tween;

    public bool IsShown { get; private set; }

    private CanvasGroup Group => group != null ? group : group = GetComponent<CanvasGroup>();

    public void Show()
    {
        IsShown = true;
        Kill();
        gameObject.SetActive(true);
        Group.blocksRaycasts = true;
        if (!Application.isPlaying) { Group.alpha = 1f; return; }
        tween = Group.DOFade(1f, showDuration).SetUpdate(true).SetEase(Ease.OutCubic);
    }

    public void Hide()
    {
        IsShown = false;
        Kill();
        Group.blocksRaycasts = false;
        if (!Application.isPlaying || !gameObject.activeInHierarchy) { Group.alpha = 0f; gameObject.SetActive(false); return; }
        tween = Group.DOFade(0f, hideDuration).SetUpdate(true).SetEase(Ease.OutCubic)
            .OnComplete(() => { if (!IsShown) gameObject.SetActive(false); });
    }

    private void Kill() { if (tween != null) { tween.Kill(); tween = null; } }
    private void OnDestroy() => Kill();
}
