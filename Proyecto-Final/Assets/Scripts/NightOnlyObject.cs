using UnityEngine;

/// <summary>Shows its target only while the world phase is Night (mirror of DayOnlyObject).</summary>
public class NightOnlyObject : MonoBehaviour
{
    [Header("REFERENCES")]
    [Tooltip("Object shown at night and hidden otherwise. Defaults to this GameObject when empty.")]
    [SerializeField] private GameObject targetObject;

    private GameFlowController flowController;

    private void Awake()
    {
        if (targetObject == null)
        {
            targetObject = gameObject;
        }
    }

    // Event driven: a deactivated component keeps its subscription, so a target that is
    // this very GameObject still comes back when the night starts.
    private void Start()
    {
        flowController = GameFlowController.Instance;
        if (flowController == null)
        {
            flowController = FindObjectOfType<GameFlowController>();
        }
        if (flowController == null) return;

        UpdateVisibility(flowController.WorldPhase);
        flowController.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnDestroy()
    {
        if (flowController != null)
        {
            flowController.OnPhaseChanged -= OnPhaseChanged;
        }
    }

    private void OnPhaseChanged(GamePhase newPhase)
    {
        UpdateVisibility(newPhase);
    }

    private void UpdateVisibility(GamePhase phase)
    {
        if (targetObject == null) return;

        bool shouldBeVisible = phase == GamePhase.Night;
        if (targetObject.activeSelf != shouldBeVisible)
        {
            targetObject.SetActive(shouldBeVisible);
        }
    }
}
