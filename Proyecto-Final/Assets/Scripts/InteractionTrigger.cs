using UnityEngine;

public class InteractionTrigger : MonoBehaviour
{
    [SerializeField] private GameObject interactionPromptCanvas;
    [SerializeField] private KeyCode interactionKey = KeyCode.F;
    [SerializeField] private bool hidePromptDuringInteraction = false;

    [Header("Audio")]
    [SerializeField] private SoundClipData interactSound;
    [SerializeField] private SoundClipData completeSound;

    private bool isPlayerNear = false;
    private bool isInteracting = false;
    private IInteractable interactable;
    private IInteractionLifecycle lifecycle;

    private void Awake()
    {
        interactable = GetComponent<IInteractable>();
        lifecycle = interactable as IInteractionLifecycle;

        if (interactable == null)
        {
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        if (lifecycle != null) lifecycle.InteractionCompleted += OnInteractionComplete;
        else
        {
            UIEvents.OnCraftingUIClosed += OnInteractionComplete;
            UIEvents.OnRestorationAltarUIClosed += OnInteractionComplete;
        }
    }

    private void OnDisable()
    {
        if (lifecycle != null)
        {
            lifecycle.InteractionCompleted -= OnInteractionComplete;
            lifecycle.CancelInteraction();
            isInteracting = false;
            isPlayerNear = false;
            if (interactionPromptCanvas != null) interactionPromptCanvas.SetActive(false);
        }
        else
        {
            UIEvents.OnCraftingUIClosed -= OnInteractionComplete;
            UIEvents.OnRestorationAltarUIClosed -= OnInteractionComplete;
        }
    }

    private void Start()
    {
        if (interactionPromptCanvas != null)
            interactionPromptCanvas.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(interactionKey)) TryInteract();
        UpdatePromptVisibility();
    }

    // The same entry point is used by F and integration tests; no alternate opening path.
    public bool TryInteract()
    {
        if (!isPlayerNear || isInteracting || interactable == null || !interactable.CanInteract() ||
            (TutorialManager.Instance != null && !TutorialManager.Instance.CanAcceptPlayerInput())) return false;
        if (lifecycle != null)
        {
            isInteracting = true;
            if (!lifecycle.TryInteract())
            {
                isInteracting = false;
                UpdatePromptVisibility();
                return false;
            }
            UpdatePromptVisibility();
            PlayInteractionSound();
        }
        else
        {
            if (hidePromptDuringInteraction)
            {
                isInteracting = true;
                if (interactionPromptCanvas != null)
                    interactionPromptCanvas.SetActive(false);
            }

            PlayInteractionSound();
            interactable.Interact();
        }
        return true;
    }

    private void PlayInteractionSound()
    {
        if (interactSound != null && interactSound.CanPlay())
        {
            SoundManager.Instance.PlayClip(interactSound);
            interactSound.SetLastPlayTime();
        }
    }

    private void UpdatePromptVisibility()
    {
        if (interactionPromptCanvas == null) return;

        bool shouldShow = isPlayerNear &&
                         !isInteracting &&
                         interactable != null && interactable.CanInteract() &&
                         (TutorialManager.Instance == null || TutorialManager.Instance.CanAcceptPlayerInput());

        interactionPromptCanvas.SetActive(shouldShow);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNear = true;
            UpdatePromptVisibility();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNear = false;
            isInteracting = false;
            if (interactionPromptCanvas != null)
                interactionPromptCanvas.SetActive(false);
        }
    }

    public void OnInteractionComplete()
    {
        isInteracting = false;
        UpdatePromptVisibility();

        // Play completion sound globally through SoundManager
        if (completeSound != null && completeSound.CanPlay())
        {
            SoundManager.Instance.PlayClip(completeSound);
            completeSound.SetLastPlayTime();
        }
    }
}