using System;
using System.Collections;
using UnityEngine;

public class InventoryAnimationController : MonoBehaviour
{
    [Header("Animation References")]
    [SerializeField] private Animator bookAnimator;

    [Header("Animation State Names")]
    [SerializeField] private string bookOpenStateName = "bookOpen";
    [SerializeField] private string bookCloseStateName = "bookClose";
    [SerializeField] private string pageFlipStateName = "pageFlip";

    [Header("Page References")]
    [SerializeField] private GameObject inventoryPage;
    [SerializeField] private GameObject optionsPage;
    [SerializeField] private GameObject calendarPage;
    [SerializeField] private GameObject glosaryPage;
    [SerializeField] private GameObject placeholderPage;
    [SerializeField] private GameObject controlsPage;

    [Header("UI Elements")]
    [SerializeField] private GameObject[] bookButtons;

    private bool isAnimating = false;
    private InventoryPage? currentPage = null;
    private InventoryPage? pendingPage = null;

    public event Action OnOpenAnimationComplete;
    public event Action OnCloseAnimationComplete;
    public event Action OnPageReadyToShow;

    public bool IsAnimating => isAnimating;
    public InventoryPage? CurrentPage => currentPage;

    private void Awake()
    {
        if (bookAnimator == null) { bookAnimator = GetComponent<Animator>(); } if (bookAnimator != null) { bookAnimator.updateMode = AnimatorUpdateMode.UnscaledTime; }
    }

    private void OnEnable()
    {
        UIEvents.OnInventoryOpened += HandleInventoryOpened;
        UIEvents.OnPauseMenuRequested += HandlePauseMenuRequested;
        UIEvents.OnPauseMenuClosed += HandlePauseMenuClosed;
    }

    private void OnDisable()
    {
        UIEvents.OnInventoryOpened -= HandleInventoryOpened;
        UIEvents.OnPauseMenuRequested -= HandlePauseMenuRequested;
        UIEvents.OnPauseMenuClosed -= HandlePauseMenuClosed;
    }

    private void HandleInventoryOpened()
    {
        OpenWithPage(InventoryPage.Inventory);
    }

    private void HandleInventoryClosed()
    {
        CloseBook();
    }

    public void OpenWithPage(InventoryPage page)
    {
        if (bookAnimator == null || isAnimating)
        {
            return;
        }

        StartCoroutine(OpenAnimationRoutine(page));
    }

    public void CloseBook()
    {
        if (bookAnimator == null || isAnimating)
            return;

        StartCoroutine(CloseAnimationRoutine());
    }

    public void ChangePage(InventoryPage page)
    {
        if (isAnimating || currentPage == page)
        {
            return;
        }

        StartCoroutine(PageFlipAnimationRoutine(page));
    }

    private IEnumerator OpenAnimationRoutine(InventoryPage page)
    {
        isAnimating = true;
        pendingPage = page;

        SetUIElementsVisibility(false);
        HideAllPages();

        UIManager.Instance?.InterfaceSounds?.PlaySound(InterfaceSoundType.GameInventoryBookOpen);

        bookAnimator.Play(bookOpenStateName, 0, 0f);

        yield return WaitForAnimationToComplete(bookOpenStateName);

        SetUIElementsVisibility(true);

        isAnimating = false;
        pendingPage = null;
        OnOpenAnimationComplete?.Invoke();
    }

    private IEnumerator CloseAnimationRoutine()
    {
        isAnimating = true;

        SetUIElementsVisibility(false);
        HideAllPages();
        currentPage = null;

        UIManager.Instance?.InterfaceSounds?.PlaySound(InterfaceSoundType.GameInventoryBookClose);

        bookAnimator.Play(bookCloseStateName, 0, 0f);

        yield return WaitForAnimationToComplete(bookCloseStateName);

        isAnimating = false;
        OnCloseAnimationComplete?.Invoke();
    }

    private IEnumerator PageFlipAnimationRoutine(InventoryPage newPage)
    {
        isAnimating = true;
        pendingPage = newPage;

        UIManager.Instance?.InterfaceSounds?.PlaySound(InterfaceSoundType.MenuButtonClick);

        bookAnimator.Play(pageFlipStateName, 0, 0f);

        yield return WaitForAnimationToComplete(pageFlipStateName);

        isAnimating = false;
        pendingPage = null;
    }

    private IEnumerator WaitForAnimationToComplete(string animationStateName)
    {
        yield return null;

        while (true)
        {
            AnimatorStateInfo stateInfo = bookAnimator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.IsName(animationStateName))
            {
                if (stateInfo.normalizedTime >= 0.95f)
                {
                    break;
                }
            }

            yield return null;
        }
    }

    public void OnBookOpenShowContent()
    {
        if (pendingPage.HasValue)
        {
            ShowPage(pendingPage.Value);
            OnPageReadyToShow?.Invoke();
        }
    }

    public void OnPageFlipHideOldPage()
    {
        HideAllPages();
    }

    public void OnPageFlipShowNewPage()
    {
        if (pendingPage.HasValue)
        {
            ShowPage(pendingPage.Value);

            OnPageReadyToShow?.Invoke();
        }
    }

    private void HideAllPages()
    {
        if (inventoryPage != null) inventoryPage.SetActive(false);
        if (optionsPage != null) optionsPage.SetActive(false);
        if (calendarPage != null) calendarPage.SetActive(false);
        if (glosaryPage != null) glosaryPage.SetActive(false);
        if (placeholderPage != null) placeholderPage.SetActive(false);
        if (controlsPage != null) controlsPage.SetActive(false);
    }

    private GameObject GetPageObject(InventoryPage page)
    {
        return page switch
        {
            InventoryPage.Inventory => inventoryPage,
            InventoryPage.Options => optionsPage,
            InventoryPage.Controls => controlsPage,
            InventoryPage.Calendar => calendarPage,
            InventoryPage.Glossary => glosaryPage,
            InventoryPage.Placeholder => placeholderPage,
            _ => null
        };
    }

    private void ShowPage(InventoryPage page)
    {
        currentPage = page;
        HideAllPages();

        GameObject pageObj = GetPageObject(page);
        if (pageObj != null)
        {
            pageObj.SetActive(true);
            if (page == InventoryPage.Options)
            {
                var pauseMenu = pageObj.GetComponentInChildren<PauseMenuController>(true);
                if (pauseMenu != null)
                {
                    pauseMenu.gameObject.SetActive(true);
                }
            }
        }
    }

    private void SetUIElementsVisibility(bool visible)
    {
        if (bookButtons == null || bookButtons.Length == 0)
            return;

        foreach (GameObject element in bookButtons)
        {
            if (element != null)
            {
                element.SetActive(visible);
            }
        }
    }

    private void HandlePauseMenuRequested()
    {
        OpenWithPage(InventoryPage.Options);
    }

    private void HandlePauseMenuClosed()
    {
        CloseBook();
    }
}


