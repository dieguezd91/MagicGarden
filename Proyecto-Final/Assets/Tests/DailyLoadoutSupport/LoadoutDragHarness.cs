#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Native uGUI event dispatch on production prefabs; not physical-input acceptance.</summary>
public sealed class LoadoutDragHarness : IDisposable
{
    private readonly DailyLoadoutUIFixture fixture;
    private readonly Transform oldParent;
    private readonly GameObject canvasObject, ownedEvents;
    private GameObject hovered;
    public PointerEventData Pointer { get; private set; }
    public Canvas Canvas { get; }
    public EventSystem Events { get; }

    public LoadoutDragHarness(DailyLoadoutUIFixture value)
    {
        fixture = value; oldParent = value.Panel.transform.parent;
        Events = EventSystem.current;
        if (Events == null)
        {
            ownedEvents = new GameObject("Loadout test EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Events = ownedEvents.GetComponent<EventSystem>();
        }
        canvasObject = new GameObject("Loadout test Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = value.Panel.layer;
        Canvas = canvasObject.GetComponent<Canvas>(); Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        value.Panel.transform.SetParent(canvasObject.transform, false);
        UnityEngine.Canvas.ForceUpdateCanvases();
    }

    public static Vector2 Center(Component component)
    {
        var rect = (RectTransform)component.transform;
        var canvas = component.GetComponentInParent<Canvas>();
        var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }

    public GameObject IconArea(int option)
        => ((Image)DailyLoadoutUIFixture.Field(fixture.Option(option), "icon")).transform.parent.gameObject;

    public void Begin(int option)
    {
        UnityEngine.Canvas.ForceUpdateCanvases();
        var area = IconArea(option);
        Pointer = new PointerEventData(Events)
        {
            pointerId = -1, button = PointerEventData.InputButton.Left, position = Center(area.transform),
            eligibleForClick = true, pointerPressRaycast = new RaycastResult { gameObject = area }
        };
        Pointer.pressPosition = Pointer.position;
        Pointer.pointerPress = ExecuteEvents.GetEventHandler<IPointerClickHandler>(area);
        Pointer.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(area);
        Assert.That(Pointer.pointerDrag, Is.Not.Null);
        ExecuteEvents.Execute(Pointer.pointerDrag, Pointer, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.Execute(Pointer.pointerDrag, Pointer, ExecuteEvents.beginDragHandler);
        Pointer.dragging = true;
    }

    public void Hover(Component slot)
    {
        if (hovered != null) ExecuteEvents.ExecuteHierarchy(hovered, Pointer, ExecuteEvents.pointerExitHandler);
        hovered = slot.gameObject; Pointer.pointerEnter = hovered; Pointer.position = Center(slot);
        ExecuteEvents.ExecuteHierarchy(hovered, Pointer, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(Pointer.pointerDrag, Pointer, ExecuteEvents.dragHandler);
    }

    public void Drop(Component slot)
    {
        Hover(slot); ExecuteEvents.ExecuteHierarchy(slot.gameObject, Pointer, ExecuteEvents.dropHandler); End();
    }

    public void End()
    {
        ExecuteEvents.Execute(Pointer.pointerDrag, Pointer, ExecuteEvents.endDragHandler);
        if (hovered != null) ExecuteEvents.ExecuteHierarchy(hovered, Pointer, ExecuteEvents.pointerExitHandler);
        hovered = null; Pointer.dragging = false;
    }

    public GameObject Raycast(Vector2 position)
    {
        var results = new List<RaycastResult>();
        Events.RaycastAll(new PointerEventData(Events) { position = position }, results);
        return results.Count == 0 ? null : results[0].gameObject;
    }

    public void Dispose()
    {
        if (fixture.Panel != null) fixture.Panel.transform.SetParent(oldParent, false);
        UnityEngine.Object.DestroyImmediate(canvasObject);
        if (ownedEvents != null) UnityEngine.Object.DestroyImmediate(ownedEvents);
    }
}
#endif
