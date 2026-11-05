using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

public class TutorialUI : MonoBehaviour
{
    [SerializeField] private PauseController pauseController;
    [Header("REFERENCES")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panelTransform;
    [SerializeField] private ScrollRect textScrollRect;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueButtonTMPLabel;
    [SerializeField] private GameObject scrollIndicator;
    [SerializeField] private Image stepImageDisplay;

    [Header("SOUND REFERENCES")]
    [SerializeField] private TutorialSoundBase tutorialSoundBase;
    [SerializeField] private WrittingSoundBase writtingSoundBase;

    [Header("LAYOUT REFERENCES")]
    [SerializeField] private RectTransform textScrollViewport;

    [Header("LAYOUT SETTINGS - NO IMAGE (Full Width)")]
    [SerializeField] private float sizeDeltaX_NoImage = -50f;
    [SerializeField] private float anchoredPosX_NoImage = 0f;

    [Header("LAYOUT SETTINGS - WITH IMAGE (Reduced)")]
    [SerializeField] private float sizeDeltaX_WithImage = -230f;
    [SerializeField] private float anchoredPosX_WithImage = -80f;

    [Header("ADAPTIVE DIALOGUE")]
    [SerializeField, Range(0.25f, 1f)] private float maximumViewportHeight = 0.75f;
    private Vector2 lastLayoutParentSize;
    private float authoredWidthDelta;
    private bool dialogueLayoutReady;

    [Header("ANIMATION SETTINGS")]
    [SerializeField] private float fadeSpeed = 0.5f;
    [SerializeField] private float scaleAnimDuration = 0.3f;
    [SerializeField] private float typewriterSpeed = 0.015f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;
    [SerializeField] private float blinkFadeTime = 0.6f;

    [Header("TYPEWRITER SETTINGS")]
    [SerializeField][Range(1, 8)] private int keypressesPerSound = 1;
    [Header("CONTINUE BUTTON SETTINGS")]
    [SerializeField] private float continueCooldown = 0.6f; // time button is non-interactable after typing finishes
    [SerializeField] private Color forwardColor = new Color(200f/255f, 200f/255f, 200f/255f); // #C8C8C8
    [SerializeField] private Color continueColor = new Color(121f/255f, 1f, 203f/255f); // #79FFCB

    private bool isVisible = false;
    private Coroutine typewriterCoroutine;
    // explicit typing active flag (safer than relying solely on coroutine reference)
    private bool typingActive = false;
    // Expose whether the UI is currently typing and an event when typing finishes
    public bool IsTyping => typewriterCoroutine != null;
    public event System.Action TypingFinished;
    private Sequence currentSequence;
    private Sequence blinkSequence;
    private CanvasGroup indicatorCanvasGroup;

    private bool continueCooldownActive = false;
    private Coroutine continueCooldownCoroutine;

    // state to defer showing when inventory or pause is active
    private bool inventoryOpenFlag = false;
    private bool pauseOpenFlag = false;
    private TutorialStep pendingStep = null;
    private bool hasPendingStep = false;
    private int keypressCounter = 0;
    
    private TutorialObjectiveType _currentStepObjectiveType = TutorialObjectiveType.None;

    private void Awake()
    {
        if (pauseController == null) pauseController = FindObjectOfType<PauseController>();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (panelTransform != null) panelTransform.localScale = Vector3.zero;
        if (stepImageDisplay != null) stepImageDisplay.gameObject.SetActive(false);

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueButtonPressed);
            // keep the continue button visible as a global fast-forward/continue control
            continueButton.gameObject.SetActive(true);
            continueButton.interactable = true;
            // cache label references if present
            if (continueButtonTMPLabel == null)
                continueButtonTMPLabel = continueButton.GetComponentInChildren<TMP_Text>();
        }

        EnsureDialogueScroll();
        if (textScrollRect != null) textScrollRect.onValueChanged.AddListener(OnScrollValueChanged);

        if (scrollIndicator != null)
        {
            indicatorCanvasGroup = scrollIndicator.GetComponent<CanvasGroup>();
            scrollIndicator.SetActive(false);
        }

        if (tutorialSoundBase == null) TryGetComponent(out tutorialSoundBase);
        if (writtingSoundBase == null) TryGetComponent(out writtingSoundBase);

        tutorialPanel.SetActive(false);
    }

    private void OnEnable()
    {
        UIEvents.OnInventoryOpened += OnInventoryOpened;
        UIEvents.OnInventoryClosed += OnInventoryClosed;
        UIEvents.OnPauseMenuRequested += OnPauseMenuRequested;
        UIEvents.OnPauseMenuClosed += OnPauseMenuClosed;
    }

    private void OnContinueButtonPressed()
    {
        // If the continue button is not present, not active, or not interactable, ignore presses
        if (continueButton == null) return;
        if (!continueButton.gameObject.activeInHierarchy) return;
        if (!continueButton.interactable) return;

        // If typing is in progress, treat the button as Fast-Forward; otherwise as Confirm/Continue
        if (typewriterCoroutine != null)
        {
            FastForwardTypewriter();
            return;
        }

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.ConfirmWaitStep();
        }
    }

    public void ShowStep(TutorialStep step)
    {
        DoShowStep(step);
    }

    private void DoShowStep(TutorialStep step)
    {
        // Every presentation route, including force-show and legacy close callbacks,
        // respects modal ownership even before a LevelManager exists.
        if (inventoryOpenFlag || pauseOpenFlag ||
            (UIManager.Instance?.Flow != null && UIManager.Instance.Flow.HasOpenModal) ||
            (pauseController != null && pauseController.IsPaused))
        {
            pendingStep = step;
            hasPendingStep = true;
            HideStepImmediate();
            return;
        }
        pendingStep = null;
        hasPendingStep = false;
        _currentStepObjectiveType = step.objectiveType;
        var soundController = FindObjectOfType<TutorialSoundBase>();
        if (soundController != null) tutorialSoundBase.PlaySound(TutorialSoundType.ShowPanel);

        KillAllAnimations();
        isVisible = true;
        tutorialPanel.SetActive(true);

        // Ensure the panel is visible immediately (avoid stuck scale = 0). Keep scale at 1 so fade still shows.
        if (panelTransform != null) panelTransform.localScale = Vector3.one;

        bool hasImage = step.stepImage != null && stepImageDisplay != null;

        if (stepImageDisplay != null)
        {
            stepImageDisplay.gameObject.SetActive(hasImage);
            if (hasImage)
            {
                stepImageDisplay.sprite = step.stepImage;
                stepImageDisplay.preserveAspect = true;
            }
        }

        if (textScrollViewport != null)
        {
            float targetSizeDeltaX = hasImage ? sizeDeltaX_WithImage : sizeDeltaX_NoImage;
            float targetPosX = hasImage ? anchoredPosX_WithImage : anchoredPosX_NoImage;

            Vector2 size = textScrollViewport.sizeDelta;
            size.x = targetSizeDeltaX;
            textScrollViewport.sizeDelta = size;

            Vector2 pos = textScrollViewport.anchoredPosition;
            pos.x = targetPosX;
            textScrollViewport.anchoredPosition = pos;
        }

        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);

        // Ensure any previous keypress sounds are stopped before starting a new typewriter sequence
        writtingSoundBase?.StopKeypressSounds();

        // Ensure continue button is visible and usable as fast-forward while typing
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
            // always allow fast-forward while typing
            continueButton.interactable = true;
            // show forward color while typing
            SetContinueButtonVisualForFastForward();
            // ensure label reads 'Forward' while typing
            SetContinueButtonText("Forward");
            // If a cooldown was running, stop it (we're starting a new typing sequence)
            if (continueCooldownCoroutine != null)
            {
                StopCoroutine(continueCooldownCoroutine);
                continueCooldownCoroutine = null;
                continueCooldownActive = false;
            }
        }

        instructionText.text = step.DisplayText;
        instructionText.maxVisibleCharacters = 0;
        FitDialogue();
        typewriterCoroutine = StartCoroutine(TypewriterEffect(step.DisplayText));
        typingActive = true;

        // Animate panel in (fade + scale) — ensure we bring scale from zero to one
        if (currentSequence != null && currentSequence.IsActive())
        {
            currentSequence.Kill();
            currentSequence = null;
        }

        if (canvasGroup != null)
        {
            // Force layout update so RectTransforms have correct sizes before animating
            if (panelTransform != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panelTransform);

            canvasGroup.alpha = 0f;
            currentSequence = DOTween.Sequence();
            currentSequence.Append(canvasGroup.DOFade(1f, fadeSpeed).SetEase(Ease.OutQuad));
            currentSequence.OnComplete(() => { if (canvasGroup != null) canvasGroup.alpha = 1f; });
            currentSequence.SetUpdate(true);
        }
        else
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }
    }

    // Upgrade the existing viewport in place; retain prefab text, button and art references.
    private void EnsureDialogueScroll()
    {
        if (dialogueLayoutReady || panelTransform == null || textScrollViewport == null || instructionText == null) return;
        authoredWidthDelta = panelTransform.sizeDelta.x;
        if (textScrollRect == null)
        {
            textScrollRect = panelTransform.GetComponent<ScrollRect>();
            if (textScrollRect == null) textScrollRect = panelTransform.gameObject.AddComponent<ScrollRect>();
        }
        if (textScrollViewport.GetComponent<RectMask2D>() == null)
            textScrollViewport.gameObject.AddComponent<RectMask2D>();
        var hitArea = textScrollViewport.GetComponent<Image>();
        if (hitArea == null)
        {
            hitArea = textScrollViewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
        }
        hitArea.raycastTarget = true;
        textScrollRect.viewport = textScrollViewport;
        textScrollRect.content = instructionText.rectTransform;
        textScrollRect.horizontal = false;
        textScrollRect.vertical = true;
        textScrollRect.movementType = ScrollRect.MovementType.Clamped;
        textScrollRect.inertia = false;
        textScrollRect.scrollSensitivity = 48f;
        instructionText.alignment = TextAlignmentOptions.TopLeft;
        instructionText.overflowMode = TextOverflowModes.Overflow;
        instructionText.margin = new Vector4(8, 8, 8, 8);
        var content = instructionText.rectTransform;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        dialogueLayoutReady = true;
    }

    private void FitDialogue()
    {
        if (!dialogueLayoutReady || !(panelTransform.parent is RectTransform parent)) return;
        lastLayoutParentSize = parent.rect.size;
        float width = Mathf.Min(parent.rect.width - 48f, Mathf.Max(760f, parent.rect.width + authoredWidthDelta));
        panelTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, width));
        textScrollViewport.anchorMin = Vector2.zero;
        textScrollViewport.anchorMax = Vector2.one;
        // Reserve the existing button row even while Continue is temporarily hidden.
        textScrollViewport.offsetMin = new Vector2(textScrollViewport.offsetMin.x, 76f);
        textScrollViewport.offsetMax = new Vector2(textScrollViewport.offsetMax.x, -24f);
        Canvas.ForceUpdateCanvases();
        var content = instructionText.rectTransform;
        var margin = new Vector4(8f, 8f, 8f, 8f);
        instructionText.margin = margin;
        // Preferred height is only a seed: rich-text/sprite metrics and the final
        // margin-constrained wrapping can place the last glyph below that estimate.
        float textHeight = Mathf.Ceil(instructionText.GetPreferredValues(instructionText.text,
            textScrollViewport.rect.width, Mathf.Infinity).y);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1f, textHeight));
        int visibleCharacters = instructionText.maxVisibleCharacters;
        instructionText.maxVisibleCharacters = int.MaxValue;
        instructionText.ForceMeshUpdate();
        // Inline sprites and bold glyphs can draw beyond their pen advances. Reserve
        // those measured overhangs before reflowing, without changing shared metrics.
        float leftOverhang = 0f, rightOverhang = 0f;
        for (int i = 0; i < instructionText.textInfo.characterCount; i++)
        {
            var character = instructionText.textInfo.characterInfo[i];
            if (!character.isVisible) continue;
            leftOverhang = Mathf.Max(leftOverhang, character.origin - character.bottomLeft.x);
            rightOverhang = Mathf.Max(rightOverhang, character.topRight.x - character.xAdvance);
        }
        margin.x += Mathf.Ceil(leftOverhang);
        margin.z += Mathf.Ceil(rightOverhang);
        instructionText.margin = margin;
        instructionText.ForceMeshUpdate();
        float bottom = content.rect.yMax - instructionText.margin.y;
        var textInfo = instructionText.textInfo;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var character = textInfo.characterInfo[i];
            if (character.isVisible) bottom = Mathf.Min(bottom, character.bottomLeft.y);
        }
        // Line descenders also retain the final line's typographic space (including
        // trailing blank lines); glyph bounds above include inline sprite geometry.
        for (int i = 0; i < textInfo.lineCount; i++)
            bottom = Mathf.Min(bottom, textInfo.lineInfo[i].descender);
        textHeight = Mathf.Ceil(content.rect.yMax - bottom + instructionText.margin.w);
        instructionText.maxVisibleCharacters = visibleCharacters;
        float imageHeight = stepImageDisplay != null && stepImageDisplay.gameObject.activeSelf
            ? stepImageDisplay.rectTransform.rect.height + 48f : 0f;
        float limit = Mathf.Max(1f, Mathf.Min(parent.rect.height - 48f, parent.rect.height * maximumViewportHeight));
        float height = Mathf.Min(limit, Mathf.Max(textHeight + 100f, imageHeight + 100f));
        panelTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(textHeight, textScrollViewport.rect.height));
        content.anchoredPosition = Vector2.zero;
        // Clamp the resolved nested prefab's right offset to the current viewport too.
        var corners = new Vector3[4];
        // Fit the settled geometry, never a partially hidden/animated scale.
        Vector3 animatedScale = panelTransform.localScale;
        panelTransform.localScale = Vector3.one;
        panelTransform.GetWorldCorners(corners);
        panelTransform.localScale = animatedScale;
        Vector2 min = parent.InverseTransformPoint(corners[0]);
        Vector2 max = parent.InverseTransformPoint(corners[2]);
        Vector2 correction = new Vector2(
            Mathf.Max(0, parent.rect.xMin + 24f - min.x) - Mathf.Max(0, max.x - parent.rect.xMax + 24f),
            Mathf.Max(0, parent.rect.yMin + 24f - min.y) - Mathf.Max(0, max.y - parent.rect.yMax + 24f));
        panelTransform.anchoredPosition += correction;
        instructionText.ForceMeshUpdate();
        textScrollRect.StopMovement();
        textScrollRect.verticalNormalizedPosition = 1f;
        UpdateScrollIndicator();
    }

    private void LateUpdate()
    {
        if (isVisible && dialogueLayoutReady && panelTransform.parent is RectTransform parent &&
            parent.rect.size != lastLayoutParentSize) FitDialogue();
    }

    // Re-present the current step without overriding modal ownership.
    public void ForceShowStep(TutorialStep step)
    {
        DoShowStep(step);
    }

    public void HideStep()
    {
        if (!isVisible) return;

        if (tutorialSoundBase != null)
        {
            tutorialSoundBase.PlaySound(TutorialSoundType.HidePanel);
        }

        KillAllAnimations();
        isVisible = false;

        if (scrollIndicator != null) scrollIndicator.SetActive(false);

        StopBlinkAnimation();

        currentSequence = DOTween.Sequence();
        currentSequence.Append(panelTransform.DOScale(Vector3.zero, scaleAnimDuration).SetEase(hideEase));
        currentSequence.Join(canvasGroup.DOFade(0f, fadeSpeed).SetEase(Ease.InQuad));
        currentSequence.OnComplete(() => tutorialPanel.SetActive(false));
        currentSequence.SetUpdate(true);
    }

    public void HideStepImmediate()
    {
        // A fading panel may still be active after isVisible becomes false.
        KillAllAnimations();
        isVisible = false;

        if (scrollIndicator != null) scrollIndicator.SetActive(false);

        StopBlinkAnimation();

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (panelTransform != null) panelTransform.localScale = Vector3.zero;
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
    }

    private IEnumerator TypewriterEffect(string text)
    {
        // make sure no previous keypress sounds linger when the coroutine starts
        writtingSoundBase?.StopKeypressSounds();

        yield return null;

        instructionText.text = "";
        instructionText.maxVisibleCharacters = 0;
        instructionText.text = text;

        if (textScrollRect != null)
        {
            textScrollRect.verticalNormalizedPosition = 1f;
            UpdateScrollIndicator();
        }

        int totalCharacters = text.Length;
        keypressCounter = 0;

        for (int i = 0; i <= totalCharacters; i++)
        {
            instructionText.maxVisibleCharacters = i;
            if (i > 0 && i <= text.Length)
            {
                char addedChar = text[i - 1];
                if (!char.IsWhiteSpace(addedChar))
                {
                    // guard playback in case typing was cancelled/fast-forwarded from another thread
                    if (!typingActive) break;
                    keypressCounter++;
                    int k = Mathf.Max(1, keypressesPerSound);
                    if (writtingSoundBase != null && ((keypressCounter - 1) % k) == 0)
                    {
                        writtingSoundBase.PlayKeypressSound();
                    }
                }
            }
            yield return new WaitForSecondsRealtime(typewriterSpeed);
        }
        // final reveal ensure
        instructionText.maxVisibleCharacters = totalCharacters;

        // Keep the beginning readable after natural typing; wheel/drag can reach the rest.
        if (textScrollRect != null)
        {
            textScrollRect.verticalNormalizedPosition = 1f;
            UpdateScrollIndicator();
        }

        // typing finished: set the continue button state appropriately
        typingActive = false;
        OnTypingFinished();

        // stop any lingering keypress sounds
        writtingSoundBase?.StopKeypressSounds();

        // clear the typing coroutine reference so IsTyping is false
        typewriterCoroutine = null;

        // notify external listeners that typing finished (e.g., TutorialManager may be waiting)
        TypingFinished?.Invoke();
    }

    private IEnumerator ContinueCooldownCoroutine()
    {
        yield return new WaitForSecondsRealtime(continueCooldown);
        continueCooldownActive = false;
        if (continueButton != null) continueButton.interactable = true;
        // set visuals and label to Continue when cooldown ends
        SetContinueButtonVisualForContinue();
        SetContinueButtonText("Continue");
        continueCooldownCoroutine = null;
    }

    public void CancelPendingStep()
    {
        pendingStep = null;
        hasPendingStep = false;
        if (continueCooldownCoroutine != null) StopCoroutine(continueCooldownCoroutine);
        continueCooldownCoroutine = null;
        continueCooldownActive = false;
    }

    public void HideImmediate()
    {
        HideStepImmediate();
    }

    private void OnScrollValueChanged(Vector2 position)
    {
        UpdateScrollIndicator();
    }

    private void UpdateScrollIndicator()
    {
        if (scrollIndicator == null || textScrollRect == null || indicatorCanvasGroup == null) return;

        bool atBottom = textScrollRect.verticalNormalizedPosition <= 0.01f;
        bool contentOverflows = textScrollRect.content != null && textScrollRect.viewport != null &&
            textScrollRect.content.rect.height > textScrollRect.viewport.rect.height + 1f;

        bool showIndicator = contentOverflows && !atBottom;

        if (showIndicator)
        {
            scrollIndicator.SetActive(true);
            if (blinkSequence == null || !blinkSequence.IsActive())
            {
                StartBlinkAnimation();
            }
        }
        else
        {
            scrollIndicator.SetActive(false);
            StopBlinkAnimation();
        }
    }

    private void StartBlinkAnimation()
    {
        if (indicatorCanvasGroup == null) return;

        indicatorCanvasGroup.alpha = 1f;

        blinkSequence = DOTween.Sequence();
        blinkSequence.Append(indicatorCanvasGroup.DOFade(0.2f, blinkFadeTime).SetEase(Ease.InOutQuad))
                     .Append(indicatorCanvasGroup.DOFade(1f, blinkFadeTime).SetEase(Ease.InOutQuad))
                     .SetLoops(-1)
                     .SetUpdate(true);
    }

    private void StopBlinkAnimation()
    {
        if (blinkSequence != null && blinkSequence.IsActive())
        {
            blinkSequence.Kill();
        }
        blinkSequence = null;

        if (indicatorCanvasGroup != null)
        {
            indicatorCanvasGroup.alpha = 1f;
        }
    }

    private void KillAllAnimations()
    {
        if (textScrollRect != null) textScrollRect.StopMovement();
        if (currentSequence != null && currentSequence.IsActive())
        {
            currentSequence.Kill();
            currentSequence = null;
        }

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        // Clear typing flag to prevent keypress sounds from playing after we stop the coroutine
        typingActive = false;
        // Ensure any keypress sounds are stopped immediately when typing is killed
        writtingSoundBase?.StopKeypressSounds();
    }

    private void Update()
    {
        // Enter/Return behaviour: while typing -> fast-forward; when not typing -> act like continue button
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (typewriterCoroutine != null)
            {
                FastForwardTypewriter();
            }
            else
            {
                // behave like continue button pressed when typing has finished
                OnContinueButtonPressed();
            }
        }

        // Safety: if inventory/pause flags are set but the game state indicates those UIs are closed, process pending step.
        if (inventoryOpenFlag)
        {
            var lm = LevelManager.Instance;
            if (lm != null && !(UIManager.Instance?.Flow != null && UIManager.Instance.Flow.HasOpenModal))
            {
                // inventory appears to be closed even if we didn't get the closed event
                OnInventoryClosed();
            }
        }

        if (pauseOpenFlag)
        {
            var lm2 = LevelManager.Instance;
            if (lm2 != null && (pauseController == null || !pauseController.IsPaused))
            {
                OnPauseMenuClosed();
            }
        }

        // Safety: if typing is not active and no coroutine is running, ensure keypress sounds are stopped.
        if (!typingActive && typewriterCoroutine == null)
        {
            writtingSoundBase?.StopKeypressSounds();
        }
    }

    private void FastForwardTypewriter()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        if (instructionText != null)
        {
            instructionText.maxVisibleCharacters = instructionText.text.Length;

            // Reveal all content without skipping the beginning of a long dialogue.
            if (textScrollRect != null)
            {
                textScrollRect.verticalNormalizedPosition = 1f;
                UpdateScrollIndicator();
            }
        }

        // When fast-forwarding, treat it as typing finished and update the continue button accordingly
        typingActive = false;

        // Ensure any lingering keypress sounds are stopped when fast-forwarding
        writtingSoundBase?.StopKeypressSounds();

        OnTypingFinished();
        // notify external listeners that typing finished (fast-forward)
        TypingFinished?.Invoke();
    }

    // Centralized handler to update the Continue button after typing finishes (natural or fast-forward)
    private void OnTypingFinished()
    {
        // Always stop any keypress sounds as soon as typing is considered finished
        writtingSoundBase?.StopKeypressSounds();

        if (continueButton == null) return;

        if (_currentStepObjectiveType != TutorialObjectiveType.Wait)
        {
            // For non-wait steps, hide the continue control so player must perform the objective
            continueButton.gameObject.SetActive(false);
            return;
        }

        // For Wait steps, ensure the button is visible and shows Continue visuals
        continueButton.gameObject.SetActive(true);
        SetContinueButtonVisualForContinue();
        SetContinueButtonText("Continue");

        // Apply cooldown if configured to avoid spam
        if (continueCooldown > 0f)
        {
            continueButton.interactable = false;
            continueCooldownActive = true;
            if (continueCooldownCoroutine != null) StopCoroutine(continueCooldownCoroutine);
            continueCooldownCoroutine = StartCoroutine(ContinueCooldownCoroutine());
        }
        else
        {
            continueButton.interactable = true;
        }
    }

    private void OnDisable()
    {
        KillAllAnimations();

        UIEvents.OnInventoryOpened -= OnInventoryOpened;
        UIEvents.OnInventoryClosed -= OnInventoryClosed;
        UIEvents.OnPauseMenuRequested -= OnPauseMenuRequested;
        UIEvents.OnPauseMenuClosed -= OnPauseMenuClosed;
    }

    private void OnDestroy()
    {
        KillAllAnimations();

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueButtonPressed);
        }

        if (textScrollRect != null)
        {
            textScrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        }
    }

    private void OnInventoryOpened()
    {
        inventoryOpenFlag = true;
        // Do not leave a fading dialogue behind the newly opened modal.
        HideStepImmediate();
        // inform manager to pause tutorial state
        TutorialManager.Instance?.PauseTutorial();
    }

    private void OnInventoryClosed()
    {
        inventoryOpenFlag = false;
        // if we had a pending step and pause is not open, show it now
        if (hasPendingStep && !pauseOpenFlag)
        {
            var step = pendingStep;
            pendingStep = null;
            hasPendingStep = false;
            DoShowStep(step);
        }
        // instruct manager to resume and force the UI if needed
        TutorialManager.Instance?.ResumeTutorial();
    }

    private void OnPauseMenuRequested()
    {
        pauseOpenFlag = true;
        HideStepImmediate();
        TutorialManager.Instance?.PauseTutorial();
    }

    private void OnPauseMenuClosed()
    {
        pauseOpenFlag = false;
        if (hasPendingStep && !inventoryOpenFlag)
        {
            var step = pendingStep;
            pendingStep = null;
            hasPendingStep = false;
            DoShowStep(step);
        }
        TutorialManager.Instance?.ResumeTutorial();
    }

    private void ApplyContinueButtonColor(Color col)
    {
        if (continueButton == null) return;

        // Apply to Button's ColorBlock for consistent UI states
        var cb = continueButton.colors;
        cb.normalColor = col;
        // Slight variations for highlighted/pressed/disabled
        cb.highlightedColor = Color.Lerp(col, Color.white, 0.08f);
        cb.pressedColor = Color.Lerp(col, Color.black, 0.12f);
        cb.disabledColor = Color.Lerp(col, Color.gray, 0.5f);
        continueButton.colors = cb;

        // Also set the raw image color so the change is immediate
        if (continueButton.image != null) continueButton.image.color = col;
    }

    private void SetContinueButtonVisualForFastForward()
    {
        ApplyContinueButtonColor(forwardColor);
    }

    private void SetContinueButtonVisualForContinue()
    {
        ApplyContinueButtonColor(continueColor);
    }

    private void SetContinueButtonText(string txt)
    {
        if (continueButtonTMPLabel != null)
        {
            continueButtonTMPLabel.text = txt;
        }
        else if (continueButton != null)
        {
            continueButtonTMPLabel = continueButton.GetComponentInChildren<TextMeshProUGUI>();
            if (continueButtonTMPLabel != null)
            {
                continueButtonTMPLabel.text = txt;
            }
        }
    }
}
