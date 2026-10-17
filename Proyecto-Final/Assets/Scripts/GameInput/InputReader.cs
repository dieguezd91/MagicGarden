using System;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class InputReader : MonoBehaviour
{
    [Header("Gameplay Keys")]
    [SerializeField] private KeyCode teleportKey    = KeyCode.Space;
    [SerializeField] private KeyCode cyclePrevKey   = KeyCode.Q;
    [SerializeField] private KeyCode cycleNextKey   = KeyCode.E;
    [SerializeField] private KeyCode interactionKey = KeyCode.F;

    [Header("UI Keys")]
    [SerializeField] private KeyCode inventoryKey1  = KeyCode.Tab;
    [SerializeField] private KeyCode inventoryKey2  = KeyCode.I;
    [SerializeField] private KeyCode loadoutKey = KeyCode.O;

    [Header("Input Settings")]
    [Tooltip("Umbral mínimo del scroll para considerarlo intencional")]
    [SerializeField] private float scrollDeadzone = 0.01f;

    public Vector2 MoveInput { get; private set; }

    public Vector2 MouseScreenPosition { get; private set; }

    public event Action OnPrimaryPressed;

    public event Action OnTeleportPressed;

    public event Action<int> OnCycleInput;

    public event Action<float> OnScrollInput;

    public event Action OnInteractPressed;

    public event Action OnEscapePressed;

    public event Action OnConfirmPressed;

    public event Action OnInventoryToggled;
    public event Action OnLoadoutToggled;

    public event Action<int> OnSlotSelected;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>[K]</summary>
    public event Action OnDebugLevelUp;

    /// <summary>[L]</summary>
    public event Action OnDebugNextLevel;

    /// <summary>[M]</summary>
    public event Action OnDebugNextMoonPhase;

    /// <summary>[B]</summary>
    public event Action OnDebugSpawnBoss;

    /// <summary>[J]</summary>
    public event Action OnDebugBossJump;
#endif

    private void Update()
    {
        ReadContinuousState();
        if (UpgradeRuntime.GameplayBlocked)
        {
            MoveInput = Vector2.zero;
            if (Input.GetKeyDown(KeyCode.Escape)) InputConsumptionManager.ConsumeEscape();
            return; // EventSystem UI remains active; no independent gameplay subscribers run.
        }
        ReadGameplayKeys();
        ReadUIKeys();
        ReadSlotKeys();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ReadDebugKeys();
#endif
    }

    private void ReadContinuousState()
    {
        MouseScreenPosition = Input.mousePosition;

        var raw = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        MoveInput = raw.sqrMagnitude > 1f ? raw.normalized : raw;
    }

    private void ReadGameplayKeys()
    {
        if (Input.GetMouseButtonDown(0))
            OnPrimaryPressed?.Invoke();

        if (Input.GetKeyDown(teleportKey))
            OnTeleportPressed?.Invoke();

        if (Input.GetKeyDown(cyclePrevKey))
            OnCycleInput?.Invoke(-1);

        if (Input.GetKeyDown(cycleNextKey))
            OnCycleInput?.Invoke(1);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > scrollDeadzone)
            OnScrollInput?.Invoke(scroll);

        if (Input.GetKeyDown(interactionKey))
            OnInteractPressed?.Invoke();
    }

    private void ReadUIKeys()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            OnEscapePressed?.Invoke();

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            OnConfirmPressed?.Invoke();

        if (Input.GetKeyDown(inventoryKey1) || Input.GetKeyDown(inventoryKey2))
            OnInventoryToggled?.Invoke();
        if (Input.GetKeyDown(loadoutKey)) OnLoadoutToggled?.Invoke();
    }

    private void ReadSlotKeys()
    {
        for (int i = 0; i < 8; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                OnSlotSelected?.Invoke(i);
                return; // máximo un slot por frame
            }
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void ReadDebugKeys()
    {
        if (Input.GetKeyDown(KeyCode.K)) OnDebugLevelUp?.Invoke();
        if (Input.GetKeyDown(KeyCode.L)) OnDebugNextLevel?.Invoke();
        if (Input.GetKeyDown(KeyCode.M)) OnDebugNextMoonPhase?.Invoke();
        if (Input.GetKeyDown(KeyCode.B)) OnDebugSpawnBoss?.Invoke();
        if (Input.GetKeyDown(KeyCode.J)) OnDebugBossJump?.Invoke();
    }
#endif
}
