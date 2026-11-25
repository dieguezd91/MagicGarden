using UnityEngine;

public class DayOnlyObject : MonoBehaviour
{
    [Header("REFERENCES")]
    [SerializeField] private GameObject targetObject;

    [Header("SETTINGS")]
    [SerializeField] private bool deactivateGameObject = true;

    private Renderer[] renderers;
    private GameFlowController flowController;

    private void Awake()
    {
        if (targetObject == null)
        {
            targetObject = gameObject;
        }

        if (!deactivateGameObject)
        {
            renderers = targetObject.GetComponentsInChildren<Renderer>(true);
        }
    }

    // Event driven: a deactivated component keeps its subscription, so a target that is
    // this very GameObject still comes back when the day starts.
    private void Start()
    {
        flowController = GameFlowController.Instance;
        if (flowController == null)
        {
            flowController = FindObjectOfType<GameFlowController>();
        }
        if (flowController == null) return;

        UpdateVisibility(flowController.CurrentPhase);
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
        bool shouldBeVisible = IsDayPhase(phase);

        if (deactivateGameObject)
        {
            if (targetObject != null && targetObject.activeSelf != shouldBeVisible)
            {
                targetObject.SetActive(shouldBeVisible);
            }
        }
        else
        {
            if (renderers != null)
            {
                foreach (Renderer renderer in renderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = shouldBeVisible;
                    }
                }
            }
        }
    }

    private bool IsDayPhase(GamePhase phase)
    {
        return phase != GamePhase.Night && phase != GamePhase.GameOver;
    }
}
