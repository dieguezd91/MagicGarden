using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Advanced UI Button component with comprehensive event handling and audio feedback.
/// Integrates with SoundManager for advanced audio features like randomization and cooldowns.
/// </summary>
[RequireComponent(typeof(Button))] [DisallowMultipleComponent]
public class ImprovedUIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    #region Audio Configuration
    [Header("Audio Clips Hover/Click")]
    [SerializeField] [Range(0, 1)] private float _volume = 1f;
    [SerializeField] private bool _enableHoverSound = true;
    [SerializeField] private AudioClip _onHoverSound;
    [SerializeField] private bool _enableClickSound = true;
    [SerializeField] private AudioClip _onClickSound;
    
    [Header("Audio Clips Disabled Hover/Click")]
    [SerializeField] private bool _enableDisabledClickSound = true;
    [SerializeField] private AudioClip _onDisabledClickSound;
    [SerializeField] private bool _enableDisabledHoverSound = true;
    [SerializeField] private AudioClip _onDisabledHoverSound;
    #endregion

    #region Private Fields
    private Button buttonComponent;
    private ButtonState currentState;
    private bool isHovering;
    #endregion

    #region Properties
    /// <summary>
    /// Gets the current state of the button
    /// </summary>
    public ButtonState CurrentState => currentState;
    
    /// <summary>
    /// Gets whether the button is currently being hovered
    /// </summary>
    public bool IsHovering => isHovering;
    
    /// <summary>
    /// Gets or sets the button's interactable state
    /// </summary>
    public bool Interactable
    {
        get => buttonComponent != null && buttonComponent.interactable;
        set
        {
            if (buttonComponent != null)
            {
                buttonComponent.interactable = value;
                UpdateButtonState();
            }
        }
    }
    #endregion

    #region Unity Lifecycle
    protected virtual void Awake()
    {
        InitializeComponents();
        if (buttonComponent != null)
        {
            buttonComponent.onClick.RemoveListener(HandleClick); // Prevent double subscription
            buttonComponent.onClick.AddListener(HandleClick);
        }
    }

    private void Start()
    {
        UpdateButtonState();
    }

    protected virtual void OnDestroy()
    {
        if (buttonComponent != null)
        {
            buttonComponent.onClick.RemoveListener(HandleClick);
        }
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        buttonComponent = GetComponent<Button>();
    }

    private void ValidateSoundClipData(AudioClip clip) { /* No-op for GetAudioClip */ }
    #endregion

    #region Event Handlers
    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentState == ButtonState.Disabled)
        {
            HandleDisabledClick();
        }
        // else: normal click is handled by Button.onClick (HandleClick)
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        
        if (currentState == ButtonState.Disabled)
        {
            HandleDisabledHover();
        }
        else
        {
            HandleHover();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHovering) return;
        isHovering = false;
        // No UnityEvent for exit, but you can add custom logic here if needed
    }
    #endregion

    #region Event Handling Methods
    private void HandleClick()
    {
        PlaySound(_onClickSound, _enableClickSound);
        OnClick.Invoke();
    }

    private void HandleHover()
    {
        PlaySound(_onHoverSound, _enableHoverSound);
        OnHover.Invoke();
    }

    private void HandleDisabledClick()
    {
        PlaySound(_onDisabledClickSound, _enableDisabledClickSound);
    }

    private void HandleDisabledHover()
    {
        PlaySound(_onDisabledHoverSound, _enableDisabledHoverSound);
    }
    #endregion

    #region Audio Management
    private void PlaySound(AudioClip clip, bool enabledFlag = true)
    {
        if (!enabledFlag || clip == null) return;
        if (SoundManager.Instance == null) return;
        SoundManager.Instance.PlayAudioClip(clip, _volume);
    }
    #endregion

    #region Public API
    /// <summary>
    /// Manually triggers the hover event (useful for testing or external control)
    /// </summary>
    public void TriggerHover()
    {
        if (currentState == ButtonState.Disabled)
            HandleDisabledHover();
        else
            HandleHover();
    }

    /// <summary>
    /// Manually triggers the click event (useful for testing or external control)
    /// </summary>
    public void TriggerClick()
    {
        if (currentState == ButtonState.Disabled)
            HandleDisabledClick();
        else
            HandleClick();
    }
    #endregion

    #region State Management
    private void UpdateButtonState()
    {
        if (buttonComponent == null) return;
        
        var newState = buttonComponent.interactable ? ButtonState.Normal : ButtonState.Disabled;
        
        if (newState != currentState)
        {
            currentState = newState;
        }
    }
    #endregion

    #region Public Events
    [Header("Button Events")]
    [HideInInspector]
    public UnityEvent OnClick = new();
    [HideInInspector]
    public UnityEvent OnHover = new();
    #endregion
}

#region Supporting Enums
/// <summary>
/// Represents the possible states of a button
/// </summary>
public enum ButtonState
{
    Normal,
    Disabled
}
#endregion
