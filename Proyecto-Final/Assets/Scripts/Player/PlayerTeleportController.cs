using System;
using UnityEngine;

public class PlayerTeleportController : MonoBehaviour
{
    [SerializeField] private GameObject teleportPrefab;
    [SerializeField] private int teleportManaCost = 10;
    [SerializeField] private float teleportCooldown = 2f;

    private float currentTeleportCooldown = 0f;
    private float previousEffectiveCooldown;

    public event Action<float, float> OnTeleportCooldownChanged;

    public float TeleportCooldown => SpellInventory.ApplyFastCooldowns(UpgradeRuntime.Cooldown("ability:teleport", teleportCooldown, "TeleportSpell"));
    public MagicGarden.Target DescribeUpgradeTarget()
    {
        var spell = teleportPrefab != null ? teleportPrefab.GetComponent<Spell>() : null;
        if (spell == null) return null;
        var target = spell.DescribeUpgradeTarget("ability:teleport", "Teleport ability");
        target.Bases[MagicGarden.Stat.AttackSpeed] = 1 / Mathf.Max(0.001f, teleportCooldown);
        return target;
    }
    public float CurrentTeleportCooldown => currentTeleportCooldown;

    private InputReader input;
    private PlayerController playerController;
    private PlayerMovementController playerMovementController;
    private PlayerAbilitySystem playerAbilitySystem;
    private ManaSystem manaSystem;
    private Transform playerTransform;

    private void Awake()
    {
        input = FindObjectOfType<InputReader>();
        playerController = GetComponent<PlayerController>();
        playerMovementController = GetComponent<PlayerMovementController>();
        playerAbilitySystem = GetComponent<PlayerAbilitySystem>();
        manaSystem = GetComponent<ManaSystem>();
        playerTransform = transform;
    }

    private void OnEnable()
    {
        if (input != null)
        {
            input.OnTeleportPressed += HandleTeleport;
        }
    }

    private void OnDisable()
    {
        if (input != null)
        {
            input.OnTeleportPressed -= HandleTeleport;
        }
    }

    private void Update()
    {
        UpdateTeleportCooldown();
    }

    private void UpdateTeleportCooldown()
    {
        float effective = TeleportCooldown;
        if (previousEffectiveCooldown > 0 && previousEffectiveCooldown != effective)
            currentTeleportCooldown *= effective / previousEffectiveCooldown;
        previousEffectiveCooldown = effective;
        if (currentTeleportCooldown > 0f)
        {
            currentTeleportCooldown -= Time.deltaTime;

            if (currentTeleportCooldown <= 0f)
            {
                currentTeleportCooldown = 0f;
            }

            OnTeleportCooldownChanged?.Invoke(currentTeleportCooldown, TeleportCooldown);
        }
    }

    private bool IsDaytime()
    {
        if (LevelManager.Instance == null) return false;
        var phase = GameFlowController.Instance.CurrentPhase;
        return phase == GamePhase.Day;
    }

    private bool IsInsideHouseLayer()
    {
        int houseLayer = LayerMask.NameToLayer("House");
        Collider2D hit = Physics2D.OverlapPoint(
            transform.position,
            1 << houseLayer
        );
        return hit != null;
    }

    private bool CanUseTeleport()
    {
        if (UpgradeRuntime.GameplayBlocked) return false;
        if (currentTeleportCooldown > 0f) return false;
        if (IsInsideHouseLayer()) return false;
        if (!IsDaytime() && manaSystem != null && manaSystem.GetCurrentMana() < teleportManaCost) return false;

        WorldTransitionAnimator worldTransition = FindObjectOfType<WorldTransitionAnimator>();
        if (worldTransition != null && worldTransition.IsInInterior) return false;

        return true;
    }

    private bool TryUseTeleport(Vector2 direction)
    {
        if (!CanUseTeleport()) return false;

        if (manaSystem != null && !IsDaytime())
        {
            manaSystem.UseMana(teleportManaCost);
        }

        if (teleportPrefab != null && playerTransform != null)
        {
            GameObject spellObject = Instantiate(teleportPrefab, playerTransform.position, Quaternion.identity);
            Spell spellComponent = spellObject.GetComponent<Spell>();

            if (spellComponent != null)
            {
                spellComponent.SetUpgradeTarget("ability:teleport", "TeleportSpell");
                spellComponent.Cast(direction, playerTransform.position);
            }
            else
            {
                Destroy(spellObject);
            }
        }

        currentTeleportCooldown = TeleportCooldown;
        previousEffectiveCooldown = currentTeleportCooldown;
        OnTeleportCooldownChanged?.Invoke(currentTeleportCooldown, TeleportCooldown);

        TutorialEvents.InvokeTeleportCasted();

        return true;
    }

    private void HandleTeleport()
    {
        if (playerController != null && !playerController.CanAct()) return;
        if (playerMovementController != null && !playerMovementController.IsMovementEnabled) return;
        if (playerAbilitySystem != null && playerAbilitySystem.IsBusy()) return;

        Vector2 castDirection;
        if (playerMovementController != null && playerMovementController.MoveInput.sqrMagnitude > 0.01f)
        {
            castDirection = playerMovementController.MoveInput.normalized;
        }
        else
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(input != null ? (Vector3)input.MouseScreenPosition : Input.mousePosition);
            mousePos.z = 0f;
            castDirection = (mousePos - transform.position).normalized;
        }

        TryUseTeleport(castDirection);
    }
}
