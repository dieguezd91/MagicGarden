using UnityEngine;
using UnityEngine.Events;

public class DayCycleController : MonoBehaviour
{
    public static DayCycleController Instance { get; private set; }

    [SerializeField] private int currentDay = 0;

    private int totalNights = 0;
    private bool weekResetPending = false;

    public int CurrentDay => currentDay;
    public int TotalNights => totalNights;

    // This run-local generation never wraps with the displayed week/day number.
    public int GameDaySerial { get; private set; }
    private bool dayStarted;
    private bool nightInProgress;
    public event System.Action<int> GameDayStarted;
    public event System.Action RunReset;

    public UnityEvent<int> OnNewDay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (OnNewDay == null)
        {
            OnNewDay = new UnityEvent<int>();
        }
    }

    public void StartDay()
    {
        bool newDay = !dayStarted || nightInProgress;
        if (nightInProgress) GameDaySerial++;
        dayStarted = true;
        nightInProgress = false;
        // Invalidate yesterday's equipment before phase subscribers can act.
        if (newDay) GameDayStarted?.Invoke(GameDaySerial);
        GameFlowController.Instance.SetPhase(GamePhase.Day);
        OnNewDay?.Invoke(currentDay);
    }

    public void StartNight()
    {
        if (nightInProgress) return;
        nightInProgress = true;
        if (weekResetPending)
        {
            currentDay = 0;
            weekResetPending = false;
        }

        currentDay++;
        totalNights++;

        GameFlowController.Instance.SetPhase(GamePhase.Night);
    }

    public void RequestWeekReset()
    {
        weekResetPending = true;
    }

    public void ResetDayCount()
    {
        currentDay = 1;
        totalNights = 0;
        weekResetPending = false;
        GameDaySerial = 0;
        dayStarted = false;
        nightInProgress = false;
        RunReset?.Invoke();
    }
}