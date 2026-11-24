using UnityEngine;

public class SimpleToggle : MonoBehaviour
{
    [SerializeField] private GameObject Options;

    public void EnableFeature()
    {
        SoundManager.Instance.PlayOneShot("ButtonClick", ui: true);
        Options.SetActive(true);
    }

    public void DisableFeature()
    {
        SoundManager.Instance.PlayOneShot("ButtonClick", ui: true);
        Options.SetActive(false);
    }
}
