using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class PauseMenuSkipButtonTests
{
    static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    GameObject panelObject, buttonObject;

    [TearDown] public void Cleanup()
    {
        if (panelObject != null) UnityEngine.Object.DestroyImmediate(panelObject);
        if (buttonObject != null) UnityEngine.Object.DestroyImmediate(buttonObject);
    }

    Component PanelWithSkipButton(bool startActive)
    {
        buttonObject = new GameObject("Skip tutorial button");
        var button = buttonObject.AddComponent(TypeOf("ImprovedUIButton"));
        // EditMode does not run Awake; run it so the wrapper finds its Button.
        button.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(button, null);
        buttonObject.SetActive(startActive);
        panelObject = new GameObject("Pause menu panel");
        var panel = panelObject.AddComponent(TypeOf("PauseMenuPanel"));
        panel.GetType().GetField("_skipTutorialButton", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, button);
        return panel;
    }

    void SetInteractable(Component panel, bool interactable) => panel.GetType().GetMethod("SetSkipButtonInteractable").Invoke(panel, new object[] { interactable });

    [Test] public void SkipButtonStaysVisibleButNotClickableWithoutAnActiveTutorial()
    {
        var panel = PanelWithSkipButton(startActive: true);
        SetInteractable(panel, false);
        Assert.That(buttonObject.activeSelf, Is.True, "The skip button is never hidden");
        Assert.That(buttonObject.GetComponent<Button>().interactable, Is.False);
    }

    [Test] public void SkipButtonIsClickableWhileTheTutorialRunsEvenIfItWasHidden()
    {
        var panel = PanelWithSkipButton(startActive: false);
        SetInteractable(panel, true);
        Assert.That(buttonObject.activeSelf, Is.True);
        Assert.That(buttonObject.GetComponent<Button>().interactable, Is.True);
        SetInteractable(panel, false);
        Assert.That(buttonObject.activeSelf, Is.True);
        Assert.That(buttonObject.GetComponent<Button>().interactable, Is.False);
    }
}
