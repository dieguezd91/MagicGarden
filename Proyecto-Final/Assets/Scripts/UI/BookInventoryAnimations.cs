using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class BookButton
{
    public Button button;
    public InventoryPage page;
    public string buttonName;
}

public class BookInventoryAnimations : MonoBehaviour
{
    [Header("Book Buttons")]
    [SerializeField] private List<BookButton> bookButtons = new List<BookButton>();

    [Header("Animation Controller")]
    [SerializeField] private InventoryAnimationController animationController;

    [Header("Settings")]
    [SerializeField] private bool playClickSound = true;
    [SerializeField] private InterfaceSoundType buttonClickSound = InterfaceSoundType.MenuButtonClick;

    private bool listenersSetup = false;

    private void Awake()
    {
        CacheAnimationController();
    }

    private void OnEnable()
    {
        if (!listenersSetup)
        {
            SetupButtonListeners();
        }
    }

    private void Start()
    {
        if (!listenersSetup)
        {
            SetupButtonListeners();
        }
    }

    private void CacheAnimationController()
    {
        if (animationController == null)
        {
            animationController = GetComponent<InventoryAnimationController>();
        }
    }

    private void SetupButtonListeners()
    {
        if (bookButtons == null || bookButtons.Count == 0)
        {
            return;
        }

        int validButtons = 0;
        foreach (var bookButton in bookButtons)
        {
            if (bookButton.button == null)
            {
                continue;
            }

            bookButton.button.onClick.RemoveAllListeners();

            InventoryPage page = bookButton.page;
            string name = bookButton.buttonName;

            bookButton.button.onClick.AddListener(() => OnButtonClicked(page, name));
            validButtons++;
        }

        listenersSetup = true;
    }

    private void OnButtonClicked(InventoryPage page, string buttonName)
    {
        if (animationController == null)
        {
            return;
        }

        if (animationController.IsAnimating)
        {
            return;
        }

        // The tab is for the page already open: no flip, a "can't" book sound instead.
        // Otherwise the page flip itself is the sound (no extra generic click on top of a book).
        if (animationController.CurrentPage == page)
        {
            if (playClickSound) UIManager.Instance?.InterfaceSounds?.PlaySound(InterfaceSoundType.GameInventoryBookSamePage);
            return;
        }

        ChangePage(page);
    }

    public void ChangePage(InventoryPage page)
    {
        if (animationController == null)
        {
            return;
        }

        animationController.ChangePage(page);
    }

    public InventoryPage? GetCurrentPage()
    {
        return animationController != null ? animationController.CurrentPage : null;
    }

    public void AddButton(Button button, InventoryPage page, string buttonName = "")
    {
        if (button == null)
        {
            return;
        }

        var bookButton = new BookButton
        {
            button = button,
            page = page,
            buttonName = string.IsNullOrEmpty(buttonName) ? button.name : buttonName
        };

        bookButtons.Add(bookButton);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnButtonClicked(page, bookButton.buttonName));
    }

    private void OnDestroy()
    {
        foreach (var bookButton in bookButtons)
        {
            if (bookButton.button != null)
            {
                bookButton.button.onClick.RemoveAllListeners();
            }
        }
    }

    public void ShowInventory() => ChangePage(InventoryPage.Inventory);
    public void ShowOptions() => ChangePage(InventoryPage.Options);
    public void ShowGlossary() => ChangePage(InventoryPage.Glossary);
    public void ShowCalendar() => ChangePage(InventoryPage.Calendar);
    public void ShowPlaceholder() => ChangePage(InventoryPage.Placeholder);
}