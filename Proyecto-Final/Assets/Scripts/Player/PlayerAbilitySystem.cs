using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

public enum PlayerAbility
{
    None,
    Planting,
    Harvesting,
    Digging,
    Removing
}

public class PlayerAbilitySystem : MonoBehaviour
{
    [SerializeField] private PauseController pauseController;
    [Header("DIG ABILITY")]
    [SerializeField] public LayerMask diggableLayer;
    [SerializeField] public float digDistance = 2f;
    [SerializeField] private float digDuration = 1.5f;
    [SerializeField] private float digManaCost;

    [Header("HARVEST ABILITY")]
    [Tooltip("Range for planting and removing. Harvest range is defined per HarvestableResourceSO.")]
    [SerializeField] public float interactionDistance = 2f;

    [Header("PLANTING ABILITY")]
    [SerializeField] private float plantManaCost;

    [Header("REMOVING ABILITY")]
    [SerializeField] private float removeManaCost;

    [Header("PROGRESS FEEDBACK")]
    [SerializeField] private ProgressBar progressBar;
    [SerializeField] private Transform progressBarTarget;
    [SerializeField] private Vector3 progressBarOffset = new Vector3(0, 1.5f, 0);

    [Header("REFERENCES")]
    [SerializeField] private SeedInventory seedInventory;
    [SerializeField] public TileBase tilledSoilTile;
    private ManaSystem manaSystem;
    private DayTimerController dayTimerController;
    private PlantQuantitySystem plantQuantitySystem;

    [Header("ANIMATION")]
    [SerializeField] private Animator handAnimator;
    [SerializeField] private float interactionAnimationDuration = 0.5f;
    private bool isPlayingInteractionAnimation = false;
    [SerializeField] private GameObject plantingParticlesPrefab;

    [Header("INPUT")]
    [SerializeField] private InputReader input;



    private PlayerAbility currentAbility = PlayerAbility.Digging;
    private bool isDigging = false;
    private Vector3 digPosition;

    private WarningBubble warningBubble;
    private FloatingTextController floatingTextController;

    private bool isHarvesting = false;
    private HarvestablePlant currentHarvestPlant = null;

    public delegate void AbilityChangedHandler(PlayerAbility newAbility);
    public event AbilityChangedHandler OnAbilityChanged;

    [SerializeField] private GameObject digAnimationPrefab;

    private bool _cancelDiggingRequested = false;

    public PlayerAbility CurrentAbility => currentAbility;
    public float PlantManaCost => plantManaCost;

    private void OnEnable()
    {
        if (input == null) return;

        input.OnEscapePressed  += HandleEscapeInput;
        input.OnCycleInput     += HandleCycleInput;
        input.OnScrollInput    += HandleScrollInput;
        input.OnPrimaryPressed += HandlePrimaryInput;
    }

    private void OnDisable()
    {
        if (input == null) return;

        input.OnEscapePressed  -= HandleEscapeInput;
        input.OnCycleInput     -= HandleCycleInput;
        input.OnScrollInput    -= HandleScrollInput;
        input.OnPrimaryPressed -= HandlePrimaryInput;
    }

    private void Awake()
    {
        if (input == null) input = FindObjectOfType<InputReader>();
        if (pauseController == null) pauseController = FindObjectOfType<PauseController>();
        manaSystem = GetComponent<ManaSystem>();
        progressBar ??= FindObjectOfType<ProgressBar>();
        progressBarTarget ??= transform;
        seedInventory ??= FindObjectOfType<SeedInventory>();
        dayTimerController = FindObjectOfType<DayTimerController>();
        plantQuantitySystem = FindObjectOfType<PlantQuantitySystem>();

        if (handAnimator == null)
        {
            Transform handTransform = transform.Find("PlayerHand");
            if (handTransform != null)
            {
                handAnimator = handTransform.GetComponent<Animator>();
            }
        }

        if (seedInventory != null)
        {
            seedInventory.onSlotSelected += OnSeedSlotSelected;
        }

        warningBubble = GetComponentInChildren<WarningBubble>();
        floatingTextController = GetComponentInChildren<FloatingTextController>();
    }

    private void OnDestroy()
    {
        if (seedInventory != null)
        {
            seedInventory.onSlotSelected -= OnSeedSlotSelected;
        }
    }

    private void Start()
    {
        SetAbility(PlayerAbility.Digging);
    }

    private void Update()
    {
        if ((isDigging || isHarvesting) && progressBar != null && progressBarTarget != null)
        {
            progressBar.transform.position = Camera.main.WorldToScreenPoint(
                progressBarTarget.position + progressBarOffset);
        }
    }

    private void PlayInteractionAnimation()
    {
        if (handAnimator != null && GameFlowController.Instance.CurrentPhase != GamePhase.Night)
        {
            handAnimator.SetBool("IsDay", true);
            handAnimator.SetTrigger("IsInteracting");
        }
    }

    private void StopInteractionAnimation()
    {
        if (handAnimator != null)
        {
            handAnimator.ResetTrigger("IsInteracting");
        }
    }

    private void CycleAbility(int direction)
    {
        if (isDigging || isHarvesting)
            return;

        if (!IsAbilityGameState())
            return;

        PlayerAbility[] validAbilities = {
            PlayerAbility.Digging,
            PlayerAbility.Planting,
            PlayerAbility.Harvesting,
            PlayerAbility.Removing
        };

        int currentIndex = System.Array.IndexOf(validAbilities, currentAbility);
        if (currentIndex == -1) currentIndex = 0;

        int nextIndex = (currentIndex + direction + validAbilities.Length) % validAbilities.Length;

        SetAbility(validAbilities[nextIndex]);
    }

    private void HandleEscapeInput()
    {
        if (InputConsumptionManager.IsEscapeConsumed) return;
        if (!IsAbilityGameState()) return;
        if ((pauseController != null && pauseController.IsPaused)) return;

        if (isDigging || isHarvesting)
        {
            InputConsumptionManager.ConsumeEscape();
            _cancelDiggingRequested = true;
            CancelCurrentAction();
        }
    }

    private void HandleCycleInput(int direction)
    {
        if (!IsAbilityGameState()) return;
        if ((pauseController != null && pauseController.IsPaused)) return;

        CycleAbility(direction);
    }

    private void HandleScrollInput(float scrollDelta)
    {
        if (isDigging || isHarvesting) return;
        if (!IsAbilityGameState()) return;

        int direction = scrollDelta > 0 ? -1 : 1;
        CycleAbility(direction);
    }

    private void HandlePrimaryInput()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!IsAbilityGameState()) return;
        if ((pauseController != null && pauseController.IsPaused)) return;

        switch (currentAbility)
        {
            case PlayerAbility.Planting:   HandlePlanting();   break;
            case PlayerAbility.Harvesting: HandleHarvesting(); break;
            case PlayerAbility.Digging:    HandleDigging();    break;
            case PlayerAbility.Removing:   HandleRemoving();   break;
        }
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector3 screen = input != null ? (Vector3)input.MouseScreenPosition : Input.mousePosition;
        return Camera.main.ScreenToWorldPoint(screen);
    }

    private void OnSeedSlotSelected(int slotIndex)
    {
        if (GameFlowController.Instance.CurrentPhase == GamePhase.Night)
            return;

        SetAbility(PlayerAbility.Planting);
    }

    public void SetAbility(PlayerAbility ability)
    {
        CancelCurrentAction();

        if (currentAbility == ability) return;

        if (LevelManager.Instance != null && GameFlowController.Instance.CurrentPhase == GamePhase.Night)
        {
            return;
        }

        currentAbility = ability;
        OnAbilityChanged?.Invoke(currentAbility);
        TutorialEvents.InvokeAbilityChanged();


    }

    private void HandlePlanting()
    {
        if (seedInventory == null || seedInventory.Loadout == null ||
            !seedInventory.Loadout.CanUse(false, seedInventory.Loadout.SelectedPlantId)) return;
        if (!isPlayingInteractionAnimation)
        {
            Vector3 mouseWorld = GetMouseWorldPosition();

            Vector3Int cellPos =
                TilePlantingSystem.Instance.PlantingTilemap
                .WorldToCell(mouseWorld);

            GameObject selectedPlant =
                seedInventory.GetSelectedPlantPrefab();

            if (selectedPlant == null)
                return;

            if (!seedInventory.HasSeedsInSelectedSlot())
            {
                warningBubble?.ShowMessage("No seeds left.");
                return;
            }

            Vector3 center =
                TilePlantingSystem.Instance.PlantingTilemap
                .GetCellCenterWorld(cellPos);

            if (Vector2.Distance(transform.position, center) >
                interactionDistance)
            {
                warningBubble?.ShowMessage("Too far to plant.");
                return;
            }

            if (plantQuantitySystem != null &&
                !plantQuantitySystem.CanPlant())
            {
                warningBubble?.ShowMessage(
                    "You reached the plant limit."
                );

                return;
            }

            // Charge at the validated planting commit, not before the animation.
            if (manaSystem != null &&
                manaSystem.GetCurrentMana() < plantManaCost)
            {
                warningBubble?.ShowMessage(
                    "Not enough mana to plant!"
                );

                if (floatingTextController != null)
                {
                    warningBubble.ShowMessage(
                        "Insufficient Mana"
                    );
                }

                SoundManager.Instance?.PlayOneShot("Error");
                return;
            }

            StartCoroutine(
                PlantWithAnimation(
                    cellPos,
                    selectedPlant
                )
            );
        }
    }

    private IEnumerator PlantWithAnimation(Vector3Int cellPos, GameObject selectedPlant)
    {
        var loadout = seedInventory.Loadout;
        string plantId = loadout.SelectedPlantId;
        int day = loadout.Day;
        isPlayingInteractionAnimation = true;
        PlayInteractionAnimation();

        yield return new WaitForSeconds(interactionAnimationDuration * 0.4f);

        string reason = "Planting cancelled: equipment or day changed.";
        bool planted = loadout.Day == day && loadout.SelectedPlantId == plantId &&
            loadout.CanUse(false, plantId) &&
            TilePlantingSystem.Instance.TryPlant(cellPos, selectedPlant, out reason);

        if (planted)
        {
            SoundManager.Instance.Play("Plant");

            dayTimerController?.ConsumeAction(DayTimerController.DayActionType.Plant);

            if (plantingParticlesPrefab != null)
            {
                Vector3 particlePosition = TilePlantingSystem.Instance.PlantingTilemap.GetCellCenterWorld(cellPos);
                Instantiate(plantingParticlesPrefab, particlePosition, Quaternion.identity);
            }
        }
        else
        {
            warningBubble?.ShowMessage(reason);
        }

        yield return new WaitForSeconds(interactionAnimationDuration * 0.6f);

        isPlayingInteractionAnimation = false;
        StopInteractionAnimation();
    }


    public HarvestablePlant GetHarvestableAtPosition(Vector2 worldPosition)
    {
        Vector3Int cellPos =
            TilePlantingSystem.Instance.PlantingTilemap
            .WorldToCell(worldPosition);

        Plant plantedPlant =
            TilePlantingSystem.Instance.GetPlantAt(cellPos);

        if (plantedPlant != null)
        {
            HarvestablePlant harvestable =
                plantedPlant.GetComponent<HarvestablePlant>();

            if (harvestable != null)
                return harvestable;
        }

        // Sprite or click collider only; the range trigger circle is ignored.
        return HarvestablePlant.FindAtPoint(worldPosition);
    }


    private void HandleHarvesting()
    {
        Vector3 mouseWorld = GetMouseWorldPosition();

        HarvestablePlant harvestable =
            GetHarvestableAtPosition(mouseWorld);

        if (harvestable == null)
            return;

        if (harvestable.IsBeingHarvested() || isHarvesting)
            return;

        if (!harvestable.IsPlayerInRange)
        {
            warningBubble?.ShowMessage("Too far to harvest.");
            return;
        }

        if (!harvestable.IsReadyToHarvest())
        {
            warningBubble?.ShowMessage(
                "Nothing left to harvest today."
            );
            return;
        }

        if (dayTimerController != null &&
            dayTimerController.RemainingActions <= 0)
        {
            warningBubble?.ShowMessage("No actions left today.");
            SoundManager.Instance?.PlayOneShot("Error");
            return;
        }

        StartHarvesting(harvestable);
    }

    private void HandleRemoving()
    {
        if (!isPlayingInteractionAnimation)
        {
            Vector3 mouseWorld = GetMouseWorldPosition();
            Vector3Int cellPos = TilePlantingSystem.Instance.PlantingTilemap.WorldToCell(mouseWorld);

            Plant plant = TilePlantingSystem.Instance.GetPlantAt(cellPos);
            if (plant == null)
            {
                warningBubble?.ShowMessage("No plant to remove.");
                return;
            }

            if (Vector2.Distance(transform.position, plant.transform.position) > interactionDistance)
            {
                warningBubble?.ShowMessage("Too far to remove plant.");
                return;
            }

            if (manaSystem != null && !manaSystem.UseMana(removeManaCost))
            {
                warningBubble?.ShowMessage("Not enough mana to remove!");
                if (floatingTextController != null)
                {
                    warningBubble.ShowMessage("Insufficient Mana");
                }
                SoundManager.Instance?.PlayOneShot("Error");
                return;
            }

            StartCoroutine(RemoveWithAnimation(cellPos, plant));
        }
    }

    private IEnumerator RemoveWithAnimation(Vector3Int cellPos, Plant plant)
    {
        isPlayingInteractionAnimation = true;
        PlayInteractionAnimation();

        yield return new WaitForSeconds(interactionAnimationDuration * 0.4f);

        SoundManager.Instance.Play("Remove");
        TilePlantingSystem.Instance.UnregisterPlantAt(cellPos);
        Destroy(plant.gameObject);
        warningBubble?.ShowMessage("Plant removed.");

        yield return new WaitForSeconds(interactionAnimationDuration * 0.6f);

        isPlayingInteractionAnimation = false;
        StopInteractionAnimation();
    }

    public void StartHarvesting(HarvestablePlant plant)
    {
        if (UpgradeRuntime.GameplayBlocked) return;
        if (currentAbility != PlayerAbility.Harvesting ||
            plant == null ||
            !plant.IsReadyToHarvest() ||
            plant.IsBeingHarvested() ||
            isPlayingInteractionAnimation)
        {
            return;
        }

        currentHarvestPlant = plant;
        isHarvesting = true;

        // Tint, sound, particles and animation are handled by the plant from its HarvestableResourceSO.
        PlayInteractionAnimation();

        progressBar?.SetImmediateProgress(0f);
        progressBar?.Show(true);
        progressBar?.gameObject.SetActive(true);

        plant.StartHarvest();

        StartCoroutine(MonitorHarvest());
    }

    private IEnumerator MonitorHarvest()
    {
        float elapsed = 0f;
        float harvestDuration = currentHarvestPlant.GetHarvestDuration();

        while (isHarvesting && currentHarvestPlant != null)
        {
            if ((pauseController != null && pauseController.IsPaused))
            {
                yield return null;
                continue;
            }

            if (!currentHarvestPlant.IsPlayerInRange)
            {
                warningBubble?.ShowMessage("Too far to harvest.");
                CancelHarvest();
                yield break;
            }

            elapsed += Time.deltaTime;
            progressBar?.SetProgress(harvestDuration > 0f ? Mathf.Clamp01(elapsed / harvestDuration) : 1f);

            if (elapsed >= harvestDuration)
            {
                CompleteHarvest();
                yield break;
            }

            yield return null;
        }
    }

    public void CancelHarvest()
    {
        if (!isHarvesting || currentHarvestPlant == null)
            return;

        currentHarvestPlant.CancelHarvest();
        isHarvesting = false;
        progressBar?.Hide();

        currentHarvestPlant = null;
    }

    private void CompleteHarvest()
    {
        isHarvesting = false;
        progressBar?.Hide();
        StopInteractionAnimation();

        HarvestablePlant plant = currentHarvestPlant;
        currentHarvestPlant = null;

        // The plant spawns the pickup; the pickup shows the floating text when collected.
        if (plant != null && plant.CompleteHarvest())
        {
            TutorialEvents.InvokePlantHarvested();

            // May end the day (and start the night) when it was the last action.
            dayTimerController?.ConsumeAction(DayTimerController.DayActionType.Harvest);
        }
    }

    private void HandleDigging()
    {
        if (isDigging)
            return;

        Collider2D roofHit = Physics2D.OverlapPoint(transform.position, LayerMask.GetMask("House"));
        if (roofHit != null)
            return;

        Vector2 mousePos = GetMouseWorldPosition();
        TryDig(mousePos);
    }

    public bool TryDig(Vector2 position)
    {
        if (UpgradeRuntime.GameplayBlocked) return false;
        if (currentAbility != PlayerAbility.Digging || isDigging)
            return false;

        RaycastHit2D hit = Physics2D.Raycast(position, Vector2.zero, Mathf.Infinity, diggableLayer);
        if (hit.collider == null)
            return false;
        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("House"))
            return false;

        Vector3Int cell = TilePlantingSystem.Instance.PlantingTilemap.WorldToCell(position);
        Vector3Int playerCell = TilePlantingSystem.Instance.PlantingTilemap.WorldToCell(transform.position);

        if (cell == playerCell)
        {
            warningBubble?.ShowMessage("Don't dig your own grave!");
            SoundManager.Instance?.PlayOneShot("Error");
            return false;
        }

        TileBase existingTile = TilePlantingSystem.Instance.PlantingTilemap.GetTile(cell);
        if (existingTile == tilledSoilTile)
        {
            warningBubble?.ShowMessage("There is already tilled soil at this position.");
            return false;
        }

        float distance = Vector2.Distance(transform.position, position);
        if (distance > digDistance)
        {
            warningBubble?.ShowMessage("The spot is too far away.");
            return false;
        }

        if (manaSystem != null && !manaSystem.UseMana(digManaCost))
        {
            warningBubble?.ShowMessage("Not enough mana to dig!");

            if (floatingTextController != null)
            {
                warningBubble.ShowMessage("Insufficient Mana");
            }

            SoundManager.Instance?.PlayOneShot("Error");
            return false;
        }

        StartDigging(position);
        return true;
    }

    private void StartDigging(Vector2 position)
    {
        if ((pauseController != null && pauseController.IsPaused) ||
            isPlayingInteractionAnimation)
            return;

        isDigging = true;
        digPosition = new Vector3(position.x, position.y, 0);

        PlayInteractionAnimation();

        progressBar?.SetImmediateProgress(0f);
        progressBar?.Show(false);

        if (digAnimationPrefab != null)
        {
            Vector3Int cell = TilePlantingSystem.Instance.PlantingTilemap.WorldToCell(digPosition);
            Vector3 spawnPos = TilePlantingSystem.Instance.PlantingTilemap.GetCellCenterWorld(cell);
            Instantiate(digAnimationPrefab, spawnPos, Quaternion.identity);
            SoundManager.Instance.Play("Dig");
        }

        StartCoroutine(DiggingProcess());
    }

    private IEnumerator DiggingProcess()
    {
        float timer = 0;
        _cancelDiggingRequested = false;

        while (timer < digDuration)
        {
            if ((pauseController != null && pauseController.IsPaused))
            {
                yield return null;
                continue;
            }

            timer += Time.deltaTime;
            progressBar?.SetProgress(timer / digDuration);

            if (_cancelDiggingRequested)
            {
                _cancelDiggingRequested = false;
                CancelDigging();
                yield break;
            }

            yield return null;
        }

        CompleteDigging();
    }

    private void CompleteDigging()
    {
        Vector3Int cell = TilePlantingSystem.Instance.PlantingTilemap.WorldToCell(digPosition);
        TilePlantingSystem.Instance.PlantingTilemap.SetTile(cell, tilledSoilTile);

        isDigging = false;
        progressBar?.Hide();

        StopInteractionAnimation();

        dayTimerController?.ConsumeAction(DayTimerController.DayActionType.Dig);

        TutorialEvents.InvokeGroundDug();
    }

    private void CancelDigging()
    {
        isDigging = false;
        progressBar?.Hide();

        StopInteractionAnimation();
    }

    private void CancelCurrentAction()
    {
        if (isHarvesting)
            CancelHarvest();
        if (isDigging)
            CancelDigging();

        if (isPlayingInteractionAnimation)
        {
            StopAllCoroutines();
            isPlayingInteractionAnimation = false;
            StopInteractionAnimation();
        }
    }

    public bool IsDigging() => isDigging;
    public bool IsHarvesting() => isHarvesting;

    private bool IsAbilityGameState()
    {
        var phase = GameFlowController.Instance.CurrentPhase;
        return !UpgradeRuntime.GameplayBlocked && phase == GamePhase.Day &&
            (UIManager.Instance == null || UIManager.Instance.Flow == null || !UIManager.Instance.Flow.HasOpenModal);
    }

    public bool IsBusy()
    {
        return isDigging || isHarvesting || isPlayingInteractionAnimation;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, digDistance);
    }
}