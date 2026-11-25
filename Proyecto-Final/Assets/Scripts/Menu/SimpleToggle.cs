using UnityEngine;

public class SimpleToggle : MonoBehaviour
{
    // Silent: the buttons that call this (Options / Back) already play their own click.
    [SerializeField] private GameObject Options;

    public void EnableFeature()
    {
        Options.SetActive(true);
    }

    public void DisableFeature()
    {
        Options.SetActive(false);
    }
}
