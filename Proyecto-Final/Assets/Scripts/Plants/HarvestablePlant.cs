
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// World object the player harvests with the Harvest tool (by day only).
/// Each harvest spends 1 day action and spawns 1 random pickup from the data's loot table.
/// Has a limited number of uses that refill when a new day starts.
/// </summary>
public class HarvestablePlant : MonoBehaviour
{
    [Header("DATA")]
    [Tooltip("Defines uses, range, loot, particles and sounds.")]
    [InlineEditor]
    [SerializeField] private HarvestableResourceSO data;

    [Header("REFERENCES")]
    [Tooltip("Trigger circle = harvest range.")]
    [SerializeField] private CircleCollider2D rangeTrigger;

    [Tooltip("Clickable area together with the sprite.")]
    [SerializeField] private Collider2D clickCollider;

    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Where loot and particles spawn. Defaults to this object.")]
    [SerializeField] private Transform effectsAnchor;

    [Header("VISUAL STATES")]
    [Tooltip("Sprite displayed normally.")]
    [SerializeField] private Sprite normalSprite;

    [Tooltip("Sprite displayed when the player can harvest this object.")]
    [SerializeField] private Sprite interactableSprite;

    [Tooltip("Sprite displayed when the object has no resources remaining.")]
    [SerializeField] private Sprite depletedSprite;

    [Header("EVENTS (optional hooks for extra feedback)")]
    public UnityEvent onHarvestStarted;
    public UnityEvent onHarvested;
    public UnityEvent onDepleted;
    public UnityEvent onRefreshed;

    private static readonly List<HarvestablePlant> activePlants =
        new List<HarvestablePlant>();

    private readonly HashSet<Collider2D> playerCollidersInRange =
        new HashSet<Collider2D>();

    private PlayerAbilitySystem playerAbilitySystem;
    private GameObject harvestingParticlesInstance;

    private int usesRemaining;
    private bool isBeingHarvested;
    private bool isHovered;

    public HarvestableResourceSO Data => data;
    public int UsesRemaining => usesRemaining;
    public int MaxUses => data != null ? data.MaxUsesPerDay : 0;
    public float InteractionRange => data != null ? data.InteractionRange : 0f;

    private Vector3 EffectsPosition =>
        effectsAnchor != null ? effectsAnchor.position : transform.position;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (data == null)
            Debug.LogWarning(
                $"HarvestablePlant '{name}' has no HarvestableResourceSO assigned.",
                this
            );

        ApplyRange();

        usesRemaining = MaxUses;

        UpdateVisualState();
    }

    private void OnEnable()
    {
        if (!activePlants.Contains(this))
            activePlants.Add(this);
    }

    private void OnDisable()
    {
        activePlants.Remove(this);
        playerCollidersInRange.Clear();

        isHovered = false;
        UpdateVisualState();
    }

    private void Start()
    {
        DayCycleController.Instance?.OnNewDay.AddListener(HandleNewDay);
    }

    private void OnDestroy()
    {
        if (DayCycleController.Instance != null)
            DayCycleController.Instance.OnNewDay.RemoveListener(HandleNewDay);

        DestroyHarvestingParticles();
    }

    public bool IsReadyToHarvest()
    {
        return data != null && usesRemaining > 0;
    }

    public bool IsBeingHarvested()
    {
        return isBeingHarvested;
    }

    public float GetHarvestDuration()
    {
        return data != null ? data.HarvestDuration : 0f;
    }

    /// <summary>Remaining uses as 0..1, used by PlantGrowthUI.</summary>
    public float GetTotalProgress()
    {
        return MaxUses > 0 ? (float)usesRemaining / MaxUses : 0f;
    }

    /// <summary>
    /// True while the player's body collider is inside the range trigger.
    /// </summary>
    public bool IsPlayerInRange
    {
        get
        {
            playerCollidersInRange.RemoveWhere(
                c => c == null || !c.isActiveAndEnabled
            );

            return playerCollidersInRange.Count > 0;
        }
    }

    /// <summary>
    /// True if the point is over the sprite or click collider.
    /// The range circle does not count.
    /// </summary>
    public bool ContainsClickPoint(Vector2 worldPoint)
    {
        if (clickCollider != null && clickCollider.OverlapPoint(worldPoint))
            return true;

        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return false;

        Bounds bounds = spriteRenderer.bounds;

        return worldPoint.x >= bounds.min.x &&
               worldPoint.x <= bounds.max.x &&
               worldPoint.y >= bounds.min.y &&
               worldPoint.y <= bounds.max.y;
    }

    /// <summary>The active harvestable under the point, or null.</summary>
    public static HarvestablePlant FindAtPoint(Vector2 worldPoint)
    {
        foreach (var plant in activePlants)
        {
            if (plant != null && plant.ContainsClickPoint(worldPoint))
                return plant;
        }

        return null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayerBody(other))
            playerCollidersInRange.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        playerCollidersInRange.Remove(other);
    }

    private static bool IsPlayerBody(Collider2D other)
    {
        if (other.isTrigger)
            return false;

        return other.CompareTag("Player") ||
               (other.attachedRigidbody != null &&
                other.attachedRigidbody.CompareTag("Player"));
    }

    private void ApplyRange()
    {
        if (rangeTrigger == null)
            return;

        rangeTrigger.isTrigger = true;

        if (data != null)
            rangeTrigger.radius = data.InteractionRange;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyRange();
    }
#endif

    /// <summary>Called by PlayerAbilitySystem when the player starts channeling.</summary>
    public void StartHarvest()
    {
        if (!IsReadyToHarvest() || isBeingHarvested)
            return;

        isBeingHarvested = true;

        UpdateVisualState();

        DestroyHarvestingParticles();

        if (data.HarvestingParticles != null)
        {
            harvestingParticlesInstance = Instantiate(
                data.HarvestingParticles,
                EffectsPosition,
                Quaternion.identity,
                transform
            );
        }

        PlaySound(data.HarvestingSound, "Harvest");

        onHarvestStarted?.Invoke();
    }

    public void CancelHarvest()
    {
        if (!isBeingHarvested)
            return;

        isBeingHarvested = false;

        DestroyHarvestingParticles();

        UpdateVisualState();
    }

    /// <summary>
    /// Spends one use and spawns one pickup.
    /// Returns false if nothing was harvested.
    /// </summary>
    public bool CompleteHarvest()
    {
        if (!isBeingHarvested)
            return false;

        isBeingHarvested = false;

        DestroyHarvestingParticles();

        usesRemaining = Mathf.Max(0, usesRemaining - 1);

        SpawnLoot();
        SpawnParticles(data.HarvestedParticles);
        PlaySound(data.HarvestedSound);

        onHarvested?.Invoke();

        bool depleted = usesRemaining <= 0;

        if (depleted)
        {
            SpawnParticles(data.DepletedParticles);
            PlaySound(data.DepletedSound);
            onDepleted?.Invoke();
        }

        UpdateVisualState();

        return true;
    }

    /// <summary>Refills all uses when a new day starts.</summary>
    [ContextMenu("Refresh Uses")]
    public void Refresh()
    {
        if (data == null)
            return;

        bool wasFull = usesRemaining >= MaxUses;

        CancelHarvest();

        usesRemaining = MaxUses;

        UpdateVisualState();

        if (wasFull)
            return;

        SpawnParticles(data.RegrowParticles);
        PlaySound(data.RegrowSound);

        onRefreshed?.Invoke();
    }

    private void HandleNewDay(int day)
    {
        Refresh();
    }

    private void UpdateVisualState()
    {
        if (spriteRenderer == null)
            return;

        Sprite targetSprite;

        if (!IsReadyToHarvest())
        {
            // No resources remaining.
            targetSprite = depletedSprite != null
                ? depletedSprite
                : normalSprite;
        }
        else if (isHovered && !isBeingHarvested)
        {
            // The player can interact with this resource.
            targetSprite = interactableSprite != null
                ? interactableSprite
                : normalSprite;
        }
        else
        {
            // Default appearance.
            targetSprite = normalSprite;
        }

        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;
    }

    private void SpawnLoot()
    {
        GameObject prefab = data.RollLoot();

        if (prefab == null)
        {
            Debug.LogWarning(
                $"'{data.name}' has an empty loot table.",
                data
            );

            return;
        }

        GameObject pickup = Instantiate(
            prefab,
            EffectsPosition,
            Quaternion.identity
        );

        // Same pop-out behavior as enemy drops.
        if (pickup.TryGetComponent(out Rigidbody2D rb))
        {
            rb.AddForce(
                Random.insideUnitCircle.normalized * data.LootScatterForce,
                ForceMode2D.Impulse
            );
        }
    }

    private void SpawnParticles(GameObject prefab)
    {
        if (prefab == null)
            return;

        GameObject instance = Instantiate(
            prefab,
            EffectsPosition,
            Quaternion.identity
        );

        Destroy(instance, data.ParticleLifetime);
    }

    private void DestroyHarvestingParticles()
    {
        if (harvestingParticlesInstance != null)
            Destroy(harvestingParticlesInstance);

        harvestingParticlesInstance = null;
    }

    private void PlaySound(
        SoundClipData sound,
        string fallbackSoundName = null
    )
    {
        if (SoundManager.Instance == null)
            return;

        if (sound != null && sound.clips != null && sound.clips.Length > 0)
        {
            SoundManager.Instance.PlayClip(
                sound,
                SoundSourceType.Localized,
                transform
            );
        }
        else if (!string.IsNullOrEmpty(fallbackSoundName))
        {
            SoundManager.Instance.Play(fallbackSoundName);
        }
    }

    // Hover feedback only applies with the Harvest tool, during the day,
    // while the player is in range and the resource is available.
    private void Update()
    {
        if (isBeingHarvested || data == null)
            return;

        if (playerAbilitySystem == null)
            playerAbilitySystem = FindAnyObjectByType<PlayerAbilitySystem>();

        Camera cam = Camera.main;

        bool hovered =
            cam != null &&
            playerAbilitySystem != null &&
            playerAbilitySystem.CurrentAbility == PlayerAbility.Harvesting &&
            GameFlowController.Instance != null &&
            GameFlowController.Instance.CurrentPhase == GamePhase.Day &&
            IsReadyToHarvest() &&
            IsPlayerInRange &&
            ContainsClickPoint(cam.ScreenToWorldPoint(Input.mousePosition));

        if (hovered == isHovered)
            return;

        isHovered = hovered;

        UpdateVisualState();
    }
}
