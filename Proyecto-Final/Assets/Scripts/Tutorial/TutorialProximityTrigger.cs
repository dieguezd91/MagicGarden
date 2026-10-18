using UnityEngine;

public class TutorialProximityTrigger : MonoBehaviour
{
    public enum TutorialProximityEvent
    {
        None,
        CraftingProximity,
        RestorationProximity,
        RitualAltarProximity,
        DailyLoadoutProximity = 4
    }

    [SerializeField]
    private TutorialProximityEvent eventToFire = TutorialProximityEvent.None;

    [SerializeField] private bool fireOnce = true;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
        {
            return;
        }

        switch (eventToFire)
        {
            case TutorialProximityEvent.CraftingProximity:
                TutorialEvents.InvokeCraftingProximity();
                break;
            case TutorialProximityEvent.RestorationProximity:
                TutorialEvents.InvokeRestorationProximity();
                break;
            case TutorialProximityEvent.RitualAltarProximity:
                TutorialEvents.InvokeRitualAltarProximity();
                break;
            case TutorialProximityEvent.DailyLoadoutProximity:
                TutorialEvents.InvokeDailyLoadoutProximity();
                break;
        }

        if (fireOnce)
        {
            this.enabled = false;
        }
    }
}