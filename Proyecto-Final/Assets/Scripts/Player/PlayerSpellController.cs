using UnityEngine;

public class PlayerSpellController : MonoBehaviour
{
    [SerializeField] private Transform firePoint;

    /// <summary>The point every spell spawns from. FireSpell reads it to spawn each flame at the hand.</summary>
    public Transform FirePoint => firePoint;

    private Sprite handCenterSprite;
    private Vector2 handCenterLocal;

    /// <summary>
    /// World position of the hand the player sees: the centre of the opaque pixels of the current hand sprite frame (tight sprite mesh),
    /// so it follows the hand animation. False (origin = fire point) when the hand is hidden or has no sprite.
    /// </summary>
    public bool TryGetHandOrigin(out Vector3 origin)
    {
        origin = firePoint != null ? firePoint.position : transform.position;
        if (handRenderer == null || !handRenderer.enabled || !handRenderer.gameObject.activeInHierarchy) return false;

        Sprite sprite = handRenderer.sprite;
        if (sprite == null) return false;

        if (sprite != handCenterSprite)
        {
            Vector2[] vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0) return false;

            Vector2 min = vertices[0], max = vertices[0];
            for (int i = 1; i < vertices.Length; i++)
            {
                min = Vector2.Min(min, vertices[i]);
                max = Vector2.Max(max, vertices[i]);
            }
            handCenterSprite = sprite;
            handCenterLocal = (min + max) * 0.5f;
        }

        origin = handRenderer.transform.TransformPoint(new Vector3(handCenterLocal.x, handCenterLocal.y, 0f));
        return true;
    }
    [SerializeField] private ManaSystem manaSystem;

    [SerializeField] private Animator handAnimator;
    [SerializeField] private SpriteRenderer handRenderer;
    [SerializeField] private int baseHandSortingOrder = 0;
    [SerializeField] private GameObject handObject;

    [Header("Cooldown Refusal Feedback")]
    [Tooltip("Notify the spell HUD and play a sound when a cast is refused because the spell is on cooldown. Turn off to disable the feedback.")]
    [SerializeField] private bool cooldownBlockedFeedback = true;
    [Tooltip("SoundManager sound played when a cast is refused for cooldown. Leave empty for no sound.")]
    [SerializeField] private string cooldownBlockedSoundName = "Player_Spell_Cooldown";
    [Tooltip("Minimum seconds between two cooldown-refusal feedbacks, so a held attack button does not repeat it every frame.")]
    [SerializeField, Min(0f)] private float cooldownBlockedFeedbackInterval = 0.4f;

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
        if (GameFlowController.Instance.CurrentPhase != GamePhase.Night) return;
        if (!TutorialManager.Allows(TutorialAction.CastSpell)) return;
        if (playerController != null && !playerController.CanAct()) return;
        if (playerAbilitySystem != null && playerAbilitySystem.IsBusy()) return;

        if (CanCastSpell())
        {
            handAnimator.SetBool("IsAttacking", true);
        }
        else if (IsOutOfMana() && Time.time >= nextNoManaSoundTime)
        {
            nextNoManaSoundTime = Time.time + 0.4f; // attack may be held/repeated
            SoundManager.Instance?.Play("Player_Spell_No_Mana");
        }
        else if (!IsOutOfMana() && IsRefusedByCooldown())
        {
            GiveCooldownBlockedFeedback();
        }
    }

    private float nextNoManaSoundTime;
    private float nextCooldownBlockedFeedbackTime;

    // True only when the cooldown is the sole reason the cast is refused (mana, phase, loadout etc. keep their own behavior).
    private bool IsRefusedByCooldown()
    {
        if (SpellInventory.Instance == null) return false;
        SpellSlot slot = SpellInventory.Instance.GetSelectedSpellSlot();
        return slot != null && slot.isUnlocked && slot.currentCooldown > 0f && CanCastSpell(true);
    }

    /// <summary>Tells the HUD slot to show its blocked cue and plays the refusal sound, throttled. Never changes casting rules.</summary>
    public void GiveCooldownBlockedFeedback()
    {
        if (!cooldownBlockedFeedback || SpellInventory.Instance == null) return;
        if (Time.time < nextCooldownBlockedFeedbackTime) return;
        nextCooldownBlockedFeedbackTime = Time.time + cooldownBlockedFeedbackInterval;

        if (!string.IsNullOrEmpty(cooldownBlockedSoundName))
            SoundManager.Instance?.PlayOneShot(cooldownBlockedSoundName);
        SpellInventory.Instance.NotifyCastBlockedByCooldown(SpellInventory.Instance.GetSelectedSlotIndex());
    }

    private bool IsOutOfMana()
    {
        SpellSlot slot = SpellInventory.Instance != null ? SpellInventory.Instance.GetSelectedSpellSlot() : null;
        return slot != null && slot.isUnlocked && manaSystem != null && manaSystem.GetCurrentMana() < slot.manaCost;
    }

    public void OnAttackAnimationEnd()
    {
        if (handAnimator != null)
            handAnimator.SetBool("IsAttacking", false);
    }

    private void CastSpell()
    {
        // Animation events and any future caller must pass the same live boundary.
        if (!CanCastSpell()) return;
        if (UpgradeRuntime.GameplayBlocked) return;
        WorldTransitionAnimator worldTransitionCheck = FindObjectOfType<WorldTransitionAnimator>();
        if (worldTransitionCheck != null && worldTransitionCheck.IsInInterior)
        {
            return;
        }

        int selectedSlotIndex = SpellInventory.Instance.GetSelectedSlotIndex();
        SpellSlot selectedSpell = SpellInventory.Instance.GetSelectedSpellSlot();

        manaSystem.UseMana(selectedSpell.manaCost);

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(input != null ? (Vector3)input.MouseScreenPosition : Input.mousePosition);
        mousePos.z = 0f;
        Vector2 direction = (mousePos - transform.position).normalized;

        var prefabSpell = selectedSpell.spellPrefab.GetComponent<Spell>();
        if (prefabSpell == null) return;
        var descriptor = prefabSpell.DescribeUpgradeTarget(selectedSpell.UpgradeId(selectedSlotIndex), selectedSpell.spellName);
        Spell spellComponent = UpgradeCasting.Cast(selectedSpell.spellPrefab, firePoint.position, direction,
            descriptor.Id, descriptor.Capability, descriptor.Bases.ContainsKey(MagicGarden.Stat.Quantity));

        if (prefabSpell.HasCastSound) prefabSpell.PlayCastSound(transform);
        else SoundManager.Instance.Play("ShootSpell", SoundSourceType.Global, transform);
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

    private bool CanCastSpell(bool ignoreCooldown = false)
    {
        if (UpgradeRuntime.GameplayBlocked) return false;
        if (!TutorialManager.Allows(TutorialAction.CastSpell)) return false;
        var session = GetComponent<PlayerContentUnlockSystem>()?.Loadout;
        if (SpellInventory.Instance == null || session == null ||
            session.Admission.CheckSpellInventoryIndex(SpellInventory.Instance.GetSelectedSlotIndex(), out _) != EquipmentAdmissionResult.Allowed)
            return false;

        if (fireSpellActive)
            return false;


        WorldTransitionAnimator worldTransition = FindObjectOfType<WorldTransitionAnimator>();
        if (worldTransition != null && worldTransition.IsInInterior)
            return false;

        if (SpellInventory.Instance == null) return false;

        SpellSlot selectedSpell = SpellInventory.Instance.GetSelectedSpellSlot();

        if (selectedSpell == null || !selectedSpell.isUnlocked) return false;

        if (!ignoreCooldown && selectedSpell.currentCooldown > 0f) return false;

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
        if (!TutorialManager.Allows(TutorialAction.CastSpell)) return;
        if (playerController != null && !playerController.CanAct()) return;

        var session = GetComponent<PlayerContentUnlockSystem>()?.Loadout;
        new EquipmentGameplaySelection(session, null, SpellInventory.Instance).TryCycleSpell(direction);
    }

    public void SetFireSpellActive(bool active)
    {
        fireSpellActive = active;
    }
}
