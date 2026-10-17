using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class LoadoutSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon, cooldown, background;
    [SerializeField] private TMP_Text label, detail;
    public Button Button => button;
    public event Action Hovered;
    public event Action Unhovered;
    private bool pointerInside;

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        Hovered?.Invoke();
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        Unhovered?.Invoke();
    }
    private void OnDisable()
    {
        if (pointerInside) Unhovered?.Invoke();
        pointerInside = false;
    }
    public bool IsConfigured => button != null && icon != null && cooldown != null &&
        background != null && label != null && detail != null;

    public void Render(string title, Sprite sprite, string info, float progress, bool available, bool selected)
    {
        label.text = title;
        detail.text = info;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        icon.color = available ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);
        cooldown.fillAmount = Mathf.Clamp01(progress);
        cooldown.enabled = progress > 0f;
        background.color = !available ? new Color(0.08f, 0.08f, 0.1f, 0.95f) :
            selected ? new Color(0.22f, 0.48f, 0.3f, 1f) : new Color(0.2f, 0.23f, 0.3f, 0.96f);
        label.color = detail.color = available ? Color.white : Color.gray;
        button.interactable = available;
    }
}
