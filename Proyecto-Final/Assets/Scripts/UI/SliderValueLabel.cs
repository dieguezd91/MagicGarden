using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows a slider's value as a percentage. Polls instead of listening, because
// VolumeController sets the initial values with SetValueWithoutNotify.
public class SliderValueLabel : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text label;

    private int shown = -1;

    public void Bind(Slider s, TMP_Text t)
    {
        slider = s;
        label = t;
    }

    private void Update()
    {
        if (slider == null || label == null) return;
        int percent = Mathf.RoundToInt(slider.normalizedValue * 100f);
        if (percent == shown) return;
        shown = percent;
        label.text = percent + "%";
    }
}
