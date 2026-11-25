using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
[DisallowMultipleComponent]
public class ImprovedUISlider : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Slider Events")]
    [HideInInspector] public UnityEvent<float> OnValueChanged = new();
    [HideInInspector] public UnityEvent OnHover = new();

    [Header("Audio Clips Hover/ValueChanged")]
    [SerializeField] [Range(0, 1)] private float _volume = 1f;
    [SerializeField] private bool _enableHoverSound = true;
    [SerializeField] private AudioClip _onHoverSound;
    [SerializeField] private bool _enableValueChangedSound = true;
    [SerializeField] private AudioClip _onValueChangedSound;

    [Header("Audio Clips Disabled Hover/ValueChanged")]
    [SerializeField] private bool _enableDisabledValueChangedSound = true;
    [SerializeField] private AudioClip _onDisabledValueChangedSound;
    [SerializeField] private bool _enableDisabledHoverSound = true;
    [SerializeField] private AudioClip _onDisabledHoverSound;
    
    private Slider sliderComponent;
    private SliderState currentState;
    private bool isHovering;

    private bool isDragging;
    private float valueChangedTimer;
    [SerializeField] private float valueChangedSoundDelay = 0.2f;
    [SerializeField] private float slowMovementDelayMultiplier = 2.5f;
    [SerializeField] private float smallMovementThreshold = 0.05f;
    [SerializeField] private float slowMovementThreshold = 0.1f;

    private float lastSliderValue;
    private bool soundPlayedForCurrentPause;
    private float initialClickValue;
    private bool hasMovedSinceClick;
    private float lastMovementTime;
    private float totalMovementDistance;

    public SliderState CurrentState => currentState;
    public bool IsHovering => isHovering;
    public bool Interactable
    {
        get => sliderComponent != null && sliderComponent.interactable;
        set
        {
            if (sliderComponent != null)
            {
                sliderComponent.interactable = value;
                UpdateSliderState();
            }
        }
    }


    private void Awake()
    {
        sliderComponent = GetComponent<Slider>();
        if (sliderComponent != null)
        {
            sliderComponent.onValueChanged.RemoveListener(HandleValueChanged);
            sliderComponent.onValueChanged.AddListener(HandleValueChanged);
            lastSliderValue = sliderComponent.value;
        }
    }

    private void Start() => UpdateSliderState();

    private void Update()
    {
        if (isDragging)
        {
            valueChangedTimer += Time.unscaledDeltaTime;
            
            float currentDelay = CalculateDynamicDelay();
            
            if (!soundPlayedForCurrentPause && valueChangedTimer >= currentDelay)
            {
                PlaySound(_onValueChangedSound, _enableValueChangedSound);
                soundPlayedForCurrentPause = true;
                // Reset movement distance tracking after sound plays for next pause detection
                totalMovementDistance = 0f;
            }
        }
    }

    private float CalculateDynamicDelay()
    {
        float baseDelay = valueChangedSoundDelay;
        
        // Check if movement is small (total distance moved is small)
        bool isSmallMovement = totalMovementDistance < smallMovementThreshold;
        
        // Check if movement is slow (based on recent movement activity)
        float timeSinceLastMovement = Time.unscaledTime - lastMovementTime;
        bool isSlowMovement = timeSinceLastMovement > 0.05f; // Recent movement check
        
        // Also check if individual movement increments are small
        float lastIncrement = Mathf.Abs(sliderComponent.value - lastSliderValue);
        bool isSmallIncrement = lastIncrement < 0.01f; // Very small individual changes
        
        // Increase delay significantly for small or slow movements
        if (isSmallMovement || isSlowMovement || isSmallIncrement)
        {
            float dynamicDelay = baseDelay * slowMovementDelayMultiplier;
            return dynamicDelay;
        }
        
        return baseDelay;
    }

    private void OnDestroy()
    {
        if (sliderComponent != null)
            sliderComponent.onValueChanged.RemoveListener(HandleValueChanged);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (currentState == SliderState.Disabled)
        {
            PlaySound(_onDisabledValueChangedSound, _enableDisabledValueChangedSound);
            return;
        }
        
        isDragging = true;
        valueChangedTimer = 0f;
        soundPlayedForCurrentPause = false;
        hasMovedSinceClick = false;
        totalMovementDistance = 0f;
        lastMovementTime = Time.unscaledTime;
        
        if (sliderComponent != null)
        {
            initialClickValue = sliderComponent.value;
            lastSliderValue = sliderComponent.value;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        
        // Play sound if value changed OR if user clicked and held for any amount of time (even quick clicks)
        if (sliderComponent != null && !soundPlayedForCurrentPause)
        {
            bool valueChanged = Mathf.Abs(initialClickValue - sliderComponent.value) > 0.001f;
            bool wasQuickClick = !hasMovedSinceClick && valueChangedTimer < valueChangedSoundDelay;
            
            if (valueChanged || wasQuickClick)
            {
                PlaySound(_onValueChangedSound, _enableValueChangedSound);
            }
        }
        
        isDragging = false;
        valueChangedTimer = 0f;
        soundPlayedForCurrentPause = false;
        hasMovedSinceClick = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        if (currentState == SliderState.Disabled)
            PlaySound(_onDisabledHoverSound, _enableDisabledHoverSound);
        else
            HandleHover();
    }

    public void OnPointerExit(PointerEventData eventData) => isHovering = false;

    private void HandleValueChanged(float value)
    {
        OnValueChanged.Invoke(value);
        if (isDragging)
        {
            if (Mathf.Abs(value - lastSliderValue) > 0.001f)
            {
                hasMovedSinceClick = true;
                valueChangedTimer = 0f; // Reset timer on value change while dragging
                soundPlayedForCurrentPause = false; // Allow sound to play again after next pause
                
                // Track movement for dynamic delay calculation
                totalMovementDistance += Mathf.Abs(value - lastSliderValue);
                lastMovementTime = Time.unscaledTime;
                
                lastSliderValue = value;
            }
        }
    }

    private void HandleHover()
    {
        PlaySound(_onHoverSound, _enableHoverSound);
        OnHover.Invoke();
    }

    private void PlaySound(AudioClip clip, bool enabledFlag)
    {
        if (enabledFlag && clip != null && SoundManager.Instance != null)
            SoundManager.Instance.PlayAudioClip(clip, _volume);
    }

    private void UpdateSliderState()
    {
        if (sliderComponent != null)
            currentState = sliderComponent.interactable ? SliderState.Normal : SliderState.Disabled;
    }

    public void TriggerHover() => HandleHover();

    public void TriggerValueChanged(float value) => HandleValueChanged(value);
}

public enum SliderState
{
    Normal,
    Disabled
}
