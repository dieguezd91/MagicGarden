using UnityEngine;
using UnityEngine.Events;

public class PauseMenuPanel : UIControllerBase
{
    [Header("Pause Menu Buttons")]
    [SerializeField] private ImprovedUIButton _continueButton;
    [SerializeField] private ImprovedUIButton _optionsButton;
    [SerializeField] private ImprovedUIButton _instructionsButton;
    [SerializeField] private ImprovedUIButton _mainMenuButton;
    [SerializeField] private ImprovedUIButton _exitButton;
    [SerializeField] private ImprovedUIButton _skipTutorialButton;

    [Header("Panel Events")]
    [HideInInspector] public UnityEvent OnContinueClicked = new();
    [HideInInspector] public UnityEvent OnOptionsClicked = new();
    [HideInInspector] public UnityEvent OnInstructionsClicked = new();
    [HideInInspector] public UnityEvent OnMainMenuClicked = new();
    [HideInInspector] public UnityEvent OnExitClicked = new();
    [HideInInspector] public UnityEvent OnSkipButtonClicked = new();

    private void Start()
    {
        // Ensure the controller is properly initialized
        Initialize();
        Setup();
    }

    protected override void CacheReferences()
    {
        // No longer need to cache parent controller reference
    }

    protected override void SetupEventListeners()
    {
        if (_continueButton != null)
        {
            _continueButton.OnClick.AddListener(() => OnContinueClicked.Invoke());
            _continueButton.OnHover.AddListener(() => { });
        }
        
        if (_optionsButton != null)
        {
            _optionsButton.OnClick.AddListener(() => OnOptionsClicked.Invoke());
            _optionsButton.OnHover.AddListener(() => { });
        }
        
        if (_instructionsButton != null)
        {
            _instructionsButton.OnClick.AddListener(() => OnInstructionsClicked.Invoke());
            _instructionsButton.OnHover.AddListener(() => { });
    }

        if (_mainMenuButton != null)
        {
            _mainMenuButton.OnClick.AddListener(() => OnMainMenuClicked.Invoke());
            _mainMenuButton.OnHover.AddListener(() => { });
        }

        if (_exitButton != null)
        {
            _exitButton.OnClick.AddListener(() => OnExitClicked.Invoke());
            _exitButton.OnHover.AddListener(() => { });
        }

        if (_skipTutorialButton != null)
        {
            _skipTutorialButton.OnClick.AddListener(() => OnSkipButtonClicked.Invoke());
            _skipTutorialButton.OnHover.AddListener(() => { });
        }
    }

    // The skip button always stays visible; it is only clickable while a tutorial is running.
    public void SetSkipButtonInteractable(bool interactable)
    {
        if (_skipTutorialButton != null)
        {
            _skipTutorialButton.gameObject.SetActive(true);
            _skipTutorialButton.Interactable = interactable;
        }
    }
}
