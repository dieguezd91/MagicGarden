using UnityEngine;

public class RitualBuffManager : MonoBehaviour
{
    public static RitualBuffManager Instance;

    [SerializeField] private float ritualDamageMultiplier = 1.25f;

    private bool ritualBuffActive;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void ActivateRitualBuff()
    {
        if (!ritualBuffActive) SoundManager.Instance?.Play("Ritual_Buff_Activate");
        ritualBuffActive = true;
    }

    public void ClearBuff()
    {
        ritualBuffActive = false;
    }

    public float GetDamageMultiplier()
    {
        return ritualBuffActive ? ritualDamageMultiplier : 1f;
    }

    public bool HasBuff()
    {
        return ritualBuffActive;
    }
}