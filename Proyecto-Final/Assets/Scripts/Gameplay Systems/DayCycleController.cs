
using UnityEngine;
using UnityEngine.Events;

public class DayCycleController : MonoBehaviour
{
    public static DayCycleController Instance { get; private set; }

    [Header("DAY CYCLE")]
    [SerializeField] private int currentDay = 0;

    private int totalNights = 0;
    private bool weekResetPending = false;
    private bool hasStartedDay = false;

    public int CurrentDay => currentDay;
    public int TotalNights => totalNights;

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
            OnNewDay = new UnityEvent<int>();
    }

    public void StartDay()
    {
        if (GameFlowController.Instance == null)
            return;

        bool shouldNotifyNewDay =
            !hasStartedDay ||
            GameFlowController.Instance.CurrentPhase != GamePhase.Day;

        GameFlowController.Instance.SetPhase(GamePhase.Day);

        if (shouldNotifyNewDay)
        {
            hasStartedDay = true;

            OnNewDay?.Invoke(currentDay);

            Debug.Log($"[DayCycleController] Day {currentDay} started.");
        }
    }

    public void StartNight()
    {
        if (GameFlowController.Instance == null)
            return;

        if (GameFlowController.Instance.CurrentPhase == GamePhase.Night)
            return;

        if (weekResetPending)
        {
            currentDay = 0;
            weekResetPending = false;
        }

        currentDay++;
        totalNights++;

        GameFlowController.Instance.SetPhase(GamePhase.Night);

        Debug.Log(
            $"[DayCycleController] Night {totalNights} started. " +
            $"Current week day: {currentDay}"
        );
    }

    public void RequestWeekReset()
    {
        weekResetPending = true;

        Debug.Log("[DayCycleController] Week reset scheduled.");
    }

    public void ResetDayCount()
    {
        currentDay = 1;
    }
}
