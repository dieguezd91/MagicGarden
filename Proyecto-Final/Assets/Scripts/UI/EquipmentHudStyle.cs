using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared equipment-only palette. Inventory slots keep their existing presentation.</summary>
public static class EquipmentHudStyle
{
    public static readonly Color ActiveFrame = new Color(.82f, .82f, .82f, 1f);
    public static readonly Color InactiveFrame = new Color(.42f, .44f, .46f, 1f);
    public static readonly Color EmptyFrame = new Color(.32f, .34f, .36f, 1f);
    public static readonly Color InactiveContent = new Color(.5f, .52f, .54f, 1f);
    public static readonly Color Position = new Color(1f, .957f, .855f, 1f);
    public static readonly Color MutedPosition = new Color(.78f, .8f, .82f, 1f);

    public static void Apply(Transform root, Image frame, Image icon, TMP_Text number, TMP_Text stock,
        bool occupied, bool selected, bool phaseActive)
    {
        bool highlighted = occupied && selected && phaseActive;
        if (frame != null)
            frame.color = !occupied ? EmptyFrame : !phaseActive ? InactiveFrame : highlighted ? Color.white : ActiveFrame;
        if (icon != null) icon.color = phaseActive ? Color.white : InactiveContent;
        if (number != null) number.color = occupied && phaseActive ? Position : MutedPosition;
        if (stock != null) stock.color = phaseActive ? Position : MutedPosition;
        float scale = highlighted ? 1.2f : 1f;
        root.localScale = new Vector3(scale, scale, 1f);
    }
}
