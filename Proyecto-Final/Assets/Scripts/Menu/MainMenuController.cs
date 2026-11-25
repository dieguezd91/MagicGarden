using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MainMenuController : UIControllerBase
{
    [Header("Buttons")]
    [SerializeField] private ImprovedUIButton _playButton;
    [SerializeField] private ImprovedUIButton _optionsButton;
    [SerializeField] private ImprovedUIButton _controlsButton;
    [SerializeField] private ImprovedUIButton _creditsButton;
    [SerializeField] private ImprovedUIButton _exitButton;
    [SerializeField] private ImprovedUIButton _creditsBackButton;
    [SerializeField] private ImprovedUIButton _optionsBackButton;
    [SerializeField] private ImprovedUIButton _controlsBackButton;
    
    [Header("Panels")]
    [SerializeField] private UIControllerBase _optionsPanel;
    [SerializeField] private UIControllerBase _creditsPanel;
    [SerializeField] private UIControllerBase _controlsPanel;

    private bool isOptionsOpen;

    private void Start()
    {
        // Ensure the controller is properly initialized
        Initialize();
        Setup();
    }

    protected override void CacheReferences()
    {
        if (_controlsBackButton == null && _controlsPanel != null)
        {
            _controlsBackButton = _controlsPanel.GetComponentInChildren<ImprovedUIButton>(true);
        }
    }

    protected override void SetupEventListeners()
    {

        if (_playButton != null)
        {
            _playButton.OnClick.AddListener(OnPlayButtonClick);
            _playButton.OnHover.AddListener(() => { });
        }

        if (_optionsButton != null)
        {
            _optionsButton.OnClick.AddListener(ShowOptions);
            _optionsButton.OnHover.AddListener(() => { });
        }

        if (_controlsButton != null)
        {
            _controlsButton.OnClick.AddListener(ShowControls);
            _controlsButton.OnHover.AddListener(() => { });
        }

        if (_exitButton != null)
        {
            _exitButton.OnClick.AddListener(OnExitButtonClick);
            _exitButton.OnHover.AddListener(() => { });
        }
        
        if (_optionsBackButton != null)
        {
            _optionsBackButton.OnClick.AddListener(HideOptions);
            _optionsBackButton.OnHover.AddListener(() => { });
        }

        if (_creditsBackButton != null)
        {
            _creditsBackButton.OnClick.AddListener(HideCredits);
            _creditsBackButton.OnHover.AddListener(() => { });
        }

        if (_controlsBackButton != null)
        {
            _controlsBackButton.OnClick.AddListener(HideControls);
            _controlsBackButton.OnHover.AddListener(() => { });
        }

        if (_controlsPanel is InstructionsPanel instructionsPanel)
        {
            instructionsPanel.OnGoBackClicked.AddListener(HideControls);
        }

        if (_creditsButton != null)
        {
            _creditsButton.OnClick.AddListener(ShowCredits);
            _creditsButton.OnHover.AddListener(() => { });
        }
    }

    protected override void ConfigureInitialState()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ShowOptions()
    {
        if (_optionsPanel == null || isOptionsOpen) return;

        isOptionsOpen = true;

        SetMainMenuButtonsInteractable(false);

        _optionsBackButton.Interactable = true;

        _optionsPanel.Show();
        PlayPanelSound(true);

        // Nothing starts selected: a pre-selected Back button shows its selected tint before the mouse ever touches it.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void HideOptions()
    {
        if (_optionsPanel == null || !isOptionsOpen) return;

        _optionsPanel.Hide();
        PlayPanelSound(false);

        isOptionsOpen = false;

        SetMainMenuButtonsInteractable(true);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void ShowControls()
    {
        if (isOptionsOpen) return;
        _controlsPanel?.Show();
        PlayPanelSound(true);
    }

    private void ShowCredits()
    {
        if (isOptionsOpen) return;

        if (_creditsPanel != null)
        {
            _creditsPanel.Show();
            PlayPanelSound(true);
        }
    }

    // Options, Controls and Credits slide in and out with a soft leafy swish.
    private static void PlayPanelSound(bool open) =>
        SoundManager.Instance?.Play(open ? "UI_Menu_Panel_Open" : "UI_Menu_Panel_Close");

    public void OnPlayButtonClick()
    {
        if (isOptionsOpen) return;

        SceneLoaderManager.Instance.LoadGameScene();
    }

    private void HideControls()
    {
        _controlsPanel?.Hide();
        PlayPanelSound(false);
    }

    private void HideCredits()
    {
        if (_creditsPanel != null)
        {
            _creditsPanel.Hide();
            PlayPanelSound(false);
        }
    }

    

    private void SetMainMenuButtonsInteractable(bool interactable)
    {
        if (_playButton != null)
            _playButton.Interactable = interactable;

        if (_optionsButton != null)
            _optionsButton.Interactable = interactable;

        if (_controlsButton != null)
            _controlsButton.Interactable = interactable;

        if (_creditsButton != null)
            _creditsButton.Interactable = interactable;

        if (_exitButton != null)
            _exitButton.Interactable = interactable;
    }

    private void OnExitButtonClick()
    {
        if (isOptionsOpen) return;

        GameManager.Instance.QuitGame();
    }
}
