using TMPro;
using UnityEngine;

public class DayTimerController : MonoBehaviour
{
    public enum DayActionType
    {
        Dig,
        Plant,
        Water,
        Harvest
    }

    [Header("Action Settings")]
    [SerializeField] private int maxActionsPerDay = 10;

    [Header("UI")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private GameObject timerPanel;
    [SerializeField] private Animator timerAnimator;
    [SerializeField] private string visibleBoolName = "IsVisible";

    private int remainingActions;
    private bool actionsActive = true;
    private bool resetWhenDayStarts = true;

    // Se mantiene este nombre para no romper otros scripts que ya lo usan
    public bool NightStartedByTimer { get; private set; }

    public int RemainingActions => remainingActions;
    public int MaxActions => maxActionsPerDay;

    private void Awake()
    {
        remainingActions = maxActionsPerDay;
        UpdateActionsText();
    }

    private void Start()
    {
        if (LevelManager.Instance != null &&
            GameFlowController.Instance != null)
        {
            GameFlowController.Instance.OnPhaseChanged += HandleGameStateChanged;
            HandleGameStateChanged(GameFlowController.Instance.CurrentPhase);
        }
    }

    private void OnDisable()
    {
        if (GameFlowController.Instance != null)
            GameFlowController.Instance.OnPhaseChanged -= HandleGameStateChanged;
    }

    /// <summary>
    /// Consume 1 acción del día.
    /// Devuelve true si la acción pudo ser consumida.
    /// </summary>
    public bool ConsumeAction(DayActionType actionType)
    {
        if (!actionsActive)
            return false;

        if (GameFlowController.Instance == null ||
            GameFlowController.Instance.CurrentPhase != GamePhase.Day)
        {
            return false;
        }

        if (remainingActions <= 0)
            return false;

        remainingActions--;

        UpdateActionsText();

        Debug.Log($"[DayTimerController] Acción utilizada: {actionType}. " +
                  $"Acciones restantes: {remainingActions}");

        if (remainingActions <= 0)
        {
            Debug.Log("[DayTimerController] No quedan acciones. Comienza la noche.");
            EndDayAndStartNight();
        }

        return true;
    }

    /// <summary>Same path as running out of actions: the horde starts even if the player is inside the house.</summary>
    public void EndDayAndStartNight()
    {
        remainingActions = 0;
        UpdateActionsText();
        actionsActive = false;
        resetWhenDayStarts = true;
        NightStartedByTimer = true;
        DayCycleController.Instance?.StartNight();
    }

    private void HandleGameStateChanged(GamePhase newPhase)
    {
        bool isDayState = IsDayGameplayPhase(newPhase);

        SetVisible(isDayState);

        if (newPhase == GamePhase.Night)
        {
            actionsActive = false;
            resetWhenDayStarts = true;
            return;
        }

        if (isDayState)
        {
            if (resetWhenDayStarts)
            {
                ResetActions();
                resetWhenDayStarts = false;
                NightStartedByTimer = false;
            }

            actionsActive = true;
        }
        else
        {
            actionsActive = false;
        }
    }

    private bool IsDayGameplayPhase(GamePhase phase)
    {
        return phase == GamePhase.Day ||
               phase == GamePhase.None;
    }

    private void SetVisible(bool visible)
    {
        if (timerAnimator != null)
        {
            timerAnimator.SetBool(visibleBoolName, visible);
        }
        else if (timerPanel != null)
        {
            timerPanel.SetActive(visible);
        }
    }

    private void ResetActions()
    {
        remainingActions = maxActionsPerDay;
        UpdateActionsText();
    }

    private void UpdateActionsText()
    {
        if (timerText == null)
            return;

        timerText.text = $"Remainig Actions: {remainingActions}";
    }
}