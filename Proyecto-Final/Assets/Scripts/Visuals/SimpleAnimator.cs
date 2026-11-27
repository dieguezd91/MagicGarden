
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SimpleAnimator : MonoBehaviour
{
    [Header("Sprites to Animate")]
    public List<Sprite> sprites = new List<Sprite>();

    [Header("Animation Settings")]
    public float frameDelay = 0.1f;
    public bool enableColliderToggle = false;
    public Collider2D targetCollider;

    private SpriteRenderer spriteRenderer;

    private bool isAnimating = false;
    private bool targetClosedState = false;

    private int currentFrameIndex = 0;
    private Coroutine currentCoroutine;

    public bool IsAnimating => isAnimating;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (sprites.Count > 0)
        {
            currentFrameIndex = 0;
            spriteRenderer.sprite = sprites[0];
        }
    }

    public void SetVisualState(bool showClosedState)
    {
        StopCurrentAnimation();

        if (sprites.Count == 0 || spriteRenderer == null)
            return;

        targetClosedState = showClosedState;

        currentFrameIndex = showClosedState
            ? sprites.Count - 1
            : 0;

        spriteRenderer.sprite = sprites[currentFrameIndex];

        ApplyColliderState(showClosedState);
    }

    public void SetVisualStateAnimated(bool showClosedState)
    {
        if (sprites.Count == 0 || spriteRenderer == null)
            return;

        int targetIndex = showClosedState
            ? sprites.Count - 1
            : 0;

        
        if (targetClosedState == showClosedState &&
            (isAnimating || currentFrameIndex == targetIndex))
        {
            return;
        }

        StopCurrentAnimation();

        targetClosedState = showClosedState;

        ApplyColliderState(showClosedState);

        if (currentFrameIndex == targetIndex)
            return;

        currentCoroutine = StartCoroutine(
            PlayAnimationToState(targetIndex)
        );
    }

    private IEnumerator PlayAnimationToState(int targetIndex)
    {
        isAnimating = true;

        int direction = targetIndex > currentFrameIndex ? 1 : -1;

        while (currentFrameIndex != targetIndex)
        {
            currentFrameIndex += direction;

            spriteRenderer.sprite = sprites[currentFrameIndex];

            if (frameDelay > 0f)
                yield return new WaitForSeconds(frameDelay);
            else
                yield return null;
        }

        isAnimating = false;
        currentCoroutine = null;
    }

    private void StopCurrentAnimation()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }

        isAnimating = false;
    }

    private void ApplyColliderState(bool showClosedState)
    {
        if (!enableColliderToggle || targetCollider == null)
            return;

        
        targetCollider.enabled = showClosedState;
    }

    public void TriggerAnimation()
    {
        SetVisualStateAnimated(!targetClosedState);
    }

    public bool IsShowingClosedState()
    {
        if (sprites.Count == 0 || spriteRenderer == null)
            return false;

        return currentFrameIndex == sprites.Count - 1;
    }
}
