using UnityEngine;

public class PlayerSpellController : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private ManaSystem manaSystem;

    [SerializeField] private Animator handAnimator;
    [SerializeField] private SpriteRenderer handRenderer;
    [SerializeField] private int baseHandSortingOrder = 0;
    [SerializeField] private GameObject handObject;

    private SpellType currentSpellType = SpellType.Range;
    private InputReader input;
    private bool fireSpellActive = false;

    private PlayerController playerController;
    private PlayerMovementController playerMovementController;
    private PlayerAbilitySystem playerAbilitySystem;
    private Animator animator;

    private void Awake()
    {
        input = FindObjectOfType<InputReader>();
        playerController = GetComponent<PlayerController>();
        playerMovementController = GetComponent<PlayerMovementController>();
        playerAbilitySystem = GetComponent<PlayerAbilitySystem>();
        animator = GetComponent<Animator>();

        if (manaSystem == null)
            manaSystem = GetComponent<ManaSystem>();
    }

    private void Start()
    {
        GameFlowController.Instance.OnPhaseChanged += OnPhaseChanged;
        OnPhaseChanged(GameFlowController.Instance.CurrentPhase);
    }

    private void OnEnable()
    {
        if (input != null)
        {
            input.OnPrimaryPressed += HandleAttack;
            input.OnCycleInput += HandleSpellSwitchInput;
        }
    }

    private void OnDisable()
    {
        if (input != null)
        {
            input.OnPrimaryPressed -= HandleAttack;
            input.OnCycleInput -= HandleSpellSwitchInput;
        }
    }

    private void OnDestroy()
    {
        if (GameFlowController.Instance != null)
            GameFlowController.Instance.OnPhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(GamePhase newPhase)
    {
        RefreshHandNightness();
    }

    private void Update()
    {
        if (GameFlowController.Instance.CurrentPhase == GamePhase.Night && (playerController == null || playerController.CanAct()))
        {
            CheckForSpellTypeChange();
        }
    }

    private void FixedUpdate()
    {
        if (animator != null && handRenderer != null)
        {
            float aimY = animator.GetFloat("aimY");
            if (aimY > 0.1f) handRenderer.sortingOrder = baseHandSortingOrder - 1;
            else if (aimY < -0.1f) handRenderer.sortingOrder = baseHandSortingOrder + 1;
            else handRenderer.sortingOrder = baseHandSortingOrder;
        }
    }

    private void CheckForSpellTypeChange()
    {
        if (SpellInventory.Instance == null || handAnimator == null) return;

        SpellSlot selectedSpell = SpellInventory.Instance.GetSelectedSpellSlot();
        if (selectedSpell == null) return;

        if (selectedSpell.spellType != currentSpellType)
        {
            currentSpellType = selectedSpell.spellType;
            UpdateHandAnimationForSpell();
        }
    }

    private void HandleAttack()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        if (GameFlowController.Instance.CurrentPhase != GamePhase.Night) return;
        if (playerController != null && !playerController.CanAct()) return;
        if (playerAbilitySystem != null && playerAbilitySystem.IsBusy()) return;

        if (CanCastSpell())
        {
            handAnimator.SetBool("IsAttacking", true);
        }
    }

    public void OnAttackAnimationEnd()
    {
        if (handAnimator != null)
            handAnimator.SetBool("IsAttacking", false);
    }

    private void CastSpell()
    {
        if (!CanCastSpell()) return;
        WorldTransitionAnimator worldTransitionCheck = FindObjectOfType<WorldTransitionAnimator>();
        if (worldTransitionCheck != null && worldTransitionCheck.IsInInterior)
        {
            return;
        }

        int selectedSlotIndex = SpellInventory.Instance.GetSelectedSlotIndex();
        SpellSlot selectedSpell = SpellInventory.Instance.GetSelectedSpellSlot();

        if (manaSystem != null && !manaSystem.UseMana(selectedSpell.manaCost)) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(input != null ? (Vector3)input.MouseScreenPosition : Input.mousePosition);
        mousePos.z = 0f;
        Vector2 direction = (mousePos - transform.position).normalized;

        var prefabSpell = selectedSpell.spellPrefab.GetComponent<Spell>();
        if (prefabSpell == null) return;
        var descriptor = prefabSpell.DescribeUpgradeTarget(selectedSpell.UpgradeId(selectedSlotIndex), selectedSpell.spellName);
        Spell spellComponent = UpgradeCasting.Cast(selectedSpell.spellPrefab, firePoint.position, direction,
            descriptor.Id, descriptor.Capability, descriptor.Bases.ContainsKey(MagicGarden.Stat.Quantity));

        SoundManager.Instance.Play("ShootSpell", SoundSourceType.Localized, transform);
        if (playerMovementController != null) playerMovementController.ApplyAttackMovementPenalty();

        FireSpell fireSpell = spellComponent as FireSpell;

        if (fireSpell != null)
        {
            fireSpell.SetSpellSlotIndex(selectedSlotIndex);
        }
        else
        {
            SpellInventory.Instance.StartCooldown(selectedSlotIndex);
        }

        TutorialEvents.InvokeSpellCasted();
    }

    public void ShootFromHand()
    {
        if (playerController != null && !playerController.CanAct()) return;
        if (!CanCastSpell()) return;

        CastSpell();
    }

    private bool CanCastSpell()
    {
        if (UpgradeRuntime.GameplayBlocked || SpellInventory.Instance == null ||
            !SpellInventory.Instance.CanCastSelectedSpell()) return false;

        if (fireSpellActive)
            return false;


        WorldTransitionAnimator worldTransition = FindObjectOfType<WorldTransitionAnimator>();
        if (worldTransition != null && worldTransition.IsInInterior)
            return false;

        if (SpellInventory.Instance == null) return false;

        SpellSlot selectedSpell = SpellInventory.Instance.GetSelectedSpellSlot();

        if (selectedSpell == null || !selectedSpell.isUnlocked) return false;

        if (selectedSpell.currentCooldown > 0f) return false;

        if (manaSystem != null && manaSystem.GetCurrentMana() < selectedSpell.manaCost)
            return false;

        return true;
    }

    private void UpdateHandAnimationForSpell()
    {
        if (SpellInventory.Instance == null || handAnimator == null) return;

        SpellSlot selectedSpell = SpellInventory.Instance.GetSelectedSpellSlot();
        if (selectedSpell == null) return;

        ResetHandSpellAnimations();

        currentSpellType = selectedSpell.spellType;

        switch (selectedSpell.spellType)
        {
            case SpellType.Range:
                handAnimator.SetBool("BaseSpell", true);
                break;
            case SpellType.Melee:
                handAnimator.SetBool("MeleeSpell", true);
                break;
            case SpellType.Area:
                handAnimator.SetBool("AreaSpell", true);
                break;
            case SpellType.Teleport:
                handAnimator.SetBool("BaseSpell", true);
                break;
            default:
                handAnimator.SetBool("BaseSpell", true);
                break;
        }
    }

    private void ResetHandSpellAnimations()
    {
        if (handAnimator == null) return;

        handAnimator.SetBool("BaseSpell", false);
        handAnimator.SetBool("MeleeSpell", false);
        handAnimator.SetBool("AreaSpell", false);
    }

    public void RefreshHandNightness()
    {
        bool isNight = GameFlowController.Instance.CurrentPhase == GamePhase.Night;
        if (handAnimator != null) handAnimator.SetBool("IsNight", isNight);

        if (!isNight)
        {
            if (handAnimator != null) handAnimator.SetBool("IsAttacking", false);
            ResetHandSpellAnimations();
        }
        else
        {
            UpdateHandAnimationForSpell();
        }

        if (handObject != null)
            handObject.SetActive(isNight);
    }

    private void HandleSpellSwitchInput(int direction)
    {
        if (SpellInventory.Instance == null) return;
        if (GameFlowController.Instance.CurrentPhase != GamePhase.Night) return;
        if (playerController != null && !playerController.CanAct()) return;

        SpellInventory.Instance.CycleSpell(direction);
    }

    public void SetFireSpellActive(bool active)
    {
        fireSpellActive = active;
    }
}
