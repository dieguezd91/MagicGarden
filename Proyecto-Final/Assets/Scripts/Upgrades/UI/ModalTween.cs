using System;
using DG.Tweening;
using UnityEngine;

// Open/close tween shared by the level-up modals, in the style of TutorialUI: fade in + scale from a smaller size with OutBack,
// scale down with InBack + fade on close. Unscaled time (timeScale is 0 during a level-up). Outside Play Mode it snaps.
[Serializable]
public sealed class ModalTween
{
    [Header("Open")]
    [SerializeField] private float openDuration = 0.3f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [Header("Close")]
    [SerializeField] private float closeDuration = 0.25f;
    [SerializeField] private Ease closeEase = Ease.InBack;
    [Header("Shared")]
    [SerializeField, Range(0f, 1f)] private float hiddenScale = 0.85f;

    private Sequence sequence;

    public bool IsOpening { get; private set; }
    public bool IsClosing { get; private set; }
    public bool IsRunning => IsOpening || IsClosing;

    public void Kill()
    {
        if (sequence != null) { sequence.Kill(); sequence = null; }
        IsOpening = IsClosing = false;
    }

    // Final resting state: fully visible and interactive, or fully hidden.
    public void Snap(RectTransform content, CanvasGroup group, bool visible)
    {
        Kill();
        if (content != null) content.localScale = visible ? Vector3.one : Vector3.one * hiddenScale;
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    public void Open(RectTransform content, CanvasGroup group, Action completed = null)
    {
        Kill();
        if (!Application.isPlaying) { Snap(content, group, true); completed?.Invoke(); return; }
        Snap(content, group, false);
        group.blocksRaycasts = true; // blocks clicks behind the modal while it opens
        IsOpening = true;
        sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(group.DOFade(1f, openDuration).SetEase(Ease.OutQuad));
        sequence.Join(content.DOScale(1f, openDuration).SetEase(openEase));
        sequence.OnComplete(() =>
        {
            IsOpening = false; sequence = null;
            Snap(content, group, true);
            completed?.Invoke();
        });
    }

    public void Close(RectTransform content, CanvasGroup group, Action completed = null)
    {
        Kill();
        if (!Application.isPlaying) { Snap(content, group, false); completed?.Invoke(); return; }
        group.interactable = false;
        group.blocksRaycasts = false;
        IsClosing = true;
        sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(content.DOScale(hiddenScale, closeDuration).SetEase(closeEase));
        sequence.Join(group.DOFade(0f, closeDuration).SetEase(Ease.InQuad));
        sequence.OnComplete(() =>
        {
            IsClosing = false; sequence = null;
            Snap(content, group, false);
            completed?.Invoke();
        });
    }
}
