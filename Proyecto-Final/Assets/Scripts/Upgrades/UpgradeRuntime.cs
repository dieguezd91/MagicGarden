using System;
using System.Collections.Generic;
using MagicGarden;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-300)]
public sealed class UpgradeRuntime : MonoBehaviour
{
    public static UpgradeRuntime Current => GameFlowController.Instance != null
        ? GameFlowController.Instance.GetComponent<UpgradeRuntime>() : null;
    public static bool GameplayBlocked => GameFlowController.Instance != null && GameFlowController.Instance.IsChoosingUpgrade;
    public RunState State { get; } = new RunState();
    public Balance Balance { get; private set; }
    private readonly List<Target> targets = new List<Target>();
    private readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, ScalingProfile> profiles = new Dictionary<string, ScalingProfile>();
    private readonly System.Random random = new System.Random();
    private PlayerExperienceSystem experience;
    private int lastRewardLevel;
    private LifeController playerLife;
    private PlayerMovementController movement;
    private PauseController pause;
    private InventoryManager inventory;
    private UpgradePanel panel;
    private List<Offer> offers;
    private float savedScale;
    public float OwnedTimeScale => savedScale;
    private float baseHealth;
    private bool choosing, buying;
    private readonly List<Behaviour> suspendedInteractions = new List<Behaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Also covers Enter Play Mode with scene reload disabled.
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isLoaded) OnSceneLoaded(active, LoadSceneMode.Single);
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Game") return;
        foreach (var root in scene.GetRootGameObjects())
        {
            var flow = root.GetComponentInChildren<GameFlowController>(true);
            if (flow != null && flow.GetComponent<UpgradeRuntime>() == null)
            {
                flow.gameObject.AddComponent<UpgradeRuntime>();
                return;
            }
        }
    }
    private void Start()
    {
        experience = FindObjectOfType<PlayerExperienceSystem>();
        if (experience == null) { Debug.LogError("Magic Garden requires the scene's PlayerExperienceSystem."); enabled = false; return; }
        playerLife = experience.GetComponent<LifeController>();
        movement = experience.GetComponent<PlayerMovementController>();
        pause = FindObjectOfType<PauseController>();
        baseHealth = playerLife != null ? playerLife.maxHealth : 0;
        var json = Resources.Load<TextAsset>("MagicGardenBalance");
        if (json == null) { Debug.LogError("Missing MagicGardenBalance.json"); enabled = false; return; }
        Balance = JsonUtility.FromJson<Balance>(json.text);
        if (Balance.profiles != null)
            foreach (var profile in Balance.profiles)
                if (profile != null && !string.IsNullOrEmpty(profile.capability))
                    profiles[string.IsNullOrEmpty(profile.targetId) ? profile.capability : profile.targetId] = profile;
        panel = FindObjectOfType<UpgradePanel>();
        if (panel == null || !panel.Initialize(this))
        {
            Debug.LogError("Magic Garden requires a wired UpgradePanel presenter in the existing Canvas Game UI prefab. No runtime UI fallback is created.");
            enabled = false;
            return;
        }
        inventory = InventoryManager.Instance;
        if (inventory != null) inventory.onMaterialChanged += MaterialsChanged;
        UIEvents.OnCraftingUIToggleRequested += ToggleShop;
        experience.OnProgressionReset += ResetRun;
        GameFlowController.Instance.OnPhaseChanged += PhaseChanged;
    }
    private void Update()
    {
        if (choosing) { Time.timeScale = 0; return; }
        if (experience == null || experience.PendingChoices == 0 || panel == null || !panel.IsReady) return;
        var flow = UIManager.Instance?.Flow;
        var phase = GameFlowController.Instance.WorldPhase;
        if (flow == null || flow.HasOpenModal || (pause != null && pause.IsPaused) || Time.timeScale <= 0 ||
            (phase != GamePhase.Day && phase != GamePhase.Night) || (playerLife != null && !playerLife.IsAlive())) return;
        var tutorial = TutorialManager.Instance;
        var tutorialUI = FindObjectOfType<TutorialUI>();
        if (tutorial != null && tutorial.IsTutorialActive() &&
            (tutorial.IsPlayerGated() || !tutorial.CanAcceptPlayerInput() ||
             tutorial.GetCurrentObjectiveType() == TutorialObjectiveType.Wait || (tutorialUI != null && tutorialUI.IsTyping))) return;
        if (!flow.Open(UIModal.LevelUp)) return;
        savedScale = Time.timeScale;
        choosing = true;
        GameFlowController.Instance.IsChoosingUpgrade = true;
        Time.timeScale = 0;
        // These legacy raw-input consumers do not subscribe to InputReader.
        SuspendRawInput<InteractionTrigger>();
        SuspendRawInput<BossNightManager>();
        SuspendRawInput<LunarCycleManager>();
        var body = experience.GetComponent<Rigidbody2D>();
        if (body != null) body.velocity = Vector2.zero;
        ShowNextChoice();
    }
    private void SuspendRawInput<T>() where T : Behaviour
    {
        foreach (var consumer in FindObjectsOfType<T>())
            if (consumer.enabled) { suspendedInteractions.Add(consumer); consumer.enabled = false; }
    }
    // Pending choices are consumed oldest first, so the shown choice belongs to the earliest unconsumed level.
    public static int LevelForPendingChoice(int currentLevel, int pendingChoices) => currentLevel - pendingChoices + 1;
    private void ShowNextChoice()
    {
        RefreshTargets();
        offers = OfferSelector.Select(State, targets, Balance, random);
        int level = LevelForPendingChoice(experience.CurrentLevel, experience.PendingChoices);
        LevelUnlockReward reward = null;
        if (level != lastRewardLevel)
        {
            lastRewardLevel = level;
            var unlocks = experience.GetComponent<PlayerContentUnlockSystem>();
            reward = unlocks != null ? unlocks.GetLevelReward(level) : null;
        }
        panel.ShowChoices(offers, experience.PendingChoices, reward);
    }
    public void Choose(int index)
    {
        if (!choosing || experience.PendingChoices <= 0) return;
        if (offers.Count == 0)
        {
            experience.ConsumeChoice(); // Explicit Continue for exhausted pools, never silently soft-lock.
        }
        else
        {
            if (index < 0 || index >= offers.Count) return;
            var offer = offers[index];
            RefreshTargets();
            var owned = FindTarget(offer.Target.Id);
            if (owned == null || !State.CanApply(owned, offer.Rule)) { ShowNextChoice(); return; }
            if (!State.Apply(offer)) return;
            experience.ConsumeChoice();
            RefreshPlayerHealth();
        }
        if (experience.PendingChoices > 0) ShowNextChoice();
        else ReleaseChoice();
    }
    private void ReleaseChoice()
    {
        if (!choosing) return;
        choosing = false;
        lastRewardLevel = 0; // An interrupted choice shows its reward again when it reopens.

        if (GameFlowController.Instance != null) GameFlowController.Instance.IsChoosingUpgrade = false;
        UIManager.Instance?.Flow?.Close(UIModal.LevelUp);
        foreach (var interaction in suspendedInteractions) if (interaction != null) interaction.enabled = true;
        suspendedInteractions.Clear();
        panel?.Hide();
        if (pause == null || !pause.IsPaused) Time.timeScale = savedScale;
    }
    private void PhaseChanged(GamePhase phase)
    {
        // Day/night transitions keep the choice owner, not a stale restored phase.
        if (phase == GamePhase.GameOver || phase == GamePhase.MainMenu) ReleaseChoice();
        if (panel != null && panel.IsShopOpen && phase != GamePhase.Day) panel.CloseShop();
    }
    private void OnDisable() { ReleaseChoice(); panel?.CloseShop(); }
    private void OnDestroy()
    {
        UIEvents.OnCraftingUIToggleRequested -= ToggleShop;
        if (inventory != null) inventory.onMaterialChanged -= MaterialsChanged;
        if (experience != null) experience.OnProgressionReset -= ResetRun;
        if (GameFlowController.Instance != null) GameFlowController.Instance.OnPhaseChanged -= PhaseChanged;
        panel?.Unbind(this); // The authored Canvas/presenter belong to the scene, not this runtime.
    }
    private void ResetRun()
    {
        lastRewardLevel = 0;
        ReleaseChoice();
        State.Clear();
        RefreshPlayerHealth();
        panel?.CloseShop();
    }
    private void RefreshPlayerHealth()
    {
        if (playerLife == null || baseHealth <= 0) return;
        float previous = playerLife.maxHealth;
        playerLife.maxHealth = Value("player", Stat.MaxHealth, baseHealth, "Player");
        playerLife.currentHealth = Mathf.Clamp(playerLife.currentHealth + playerLife.maxHealth - previous, 0, playerLife.maxHealth);
        playerLife.onHealthChanged?.Invoke(playerLife.currentHealth, playerLife.maxHealth);
    }
    private void AddTarget(Target target, Sprite icon)
    {
        if (target == null || string.IsNullOrEmpty(target.Id) || FindTarget(target.Id) != null) return;
        target.Profile = Profile(target.Id, target.Capability);
        if (target.Bases.TryGetValue(Stat.Area, out var area))
            target.Bases[Stat.Area] = area * (1 + State.Effect(target.Id, EffectKind.AreaMultiplier));
        targets.Add(target);
        icons[target.Id] = icon;
    }
    public void RefreshTargets()
    {
        targets.Clear(); icons.Clear();
        var player = new Target { Id = "player", Name = "Player", Capability = "Player" };
        if (baseHealth > 0) player.Bases[Stat.MaxHealth] = baseHealth;
        if (movement != null) player.Bases[Stat.MoveSpeed] = movement.BaseMoveSpeed;
        var playerIcon = experience != null ? experience.GetComponent<SpriteRenderer>()?.sprite : null;
        AddTarget(player, playerIcon);
        var teleport = experience != null ? experience.GetComponent<PlayerTeleportController>() : null;
        if (teleport != null) AddTarget(teleport.DescribeUpgradeTarget(), playerIcon);
        var inventory = SpellInventory.Instance;
        if (inventory != null)
            for (int i = 0; i < inventory.spellSlots.Length; i++)
            {
                var slot = inventory.GetSpellSlot(i);
                if (slot == null || !slot.isUnlocked || slot.spellPrefab == null) continue;
                var spell = slot.spellPrefab.GetComponent<Spell>();
                if (spell == null) continue;
                var target = spell.DescribeUpgradeTarget(slot.UpgradeId(i), slot.spellName);
                target.Bases[Stat.AttackSpeed] = 1 / Mathf.Max(0.001f, slot.cooldown);
                AddTarget(target, slot.spellIcon);
            }
        // Existing planted representatives take precedence over seeds for accurate maturity previews.
        foreach (var plant in FindObjectsOfType<Plant>())
        {
            var life = plant.GetComponent<LifeController>();
            if (plant.plantData != null && (life == null || life.IsAlive()))
                AddTarget(plant.DescribeUpgradeTarget(), plant.plantData.plantIcon);
        }
        var seeds = SeedInventory.Instance;
        if (seeds != null)
            for (int i = 0; i < seeds.PlantSlotsCount; i++)
            {
                var slot = seeds.GetPlantSlot(i);
                if (slot == null || slot.seedCount <= 0 || slot.plantPrefab == null) continue;
                var plant = slot.plantPrefab.GetComponent<Plant>();
                if (plant == null || slot.data == null) continue;
                var target = plant.DescribeUpgradeTarget();
                target.Id = slot.data.UpgradeLineId;
                target.Name = slot.plantName + " (seed)";
                AddTarget(target, slot.plantIcon);
            }
    }
    public Target FindTarget(string id)
    {
        foreach (var target in targets) if (target.Id == id) return target;
        return null;
    }
    public Sprite Icon(string id) => icons.TryGetValue(id, out var icon) ? icon : null;
    public IList<Target> Targets => targets;
    private ScalingProfile Profile(string id, string capability)
    {
        if (!string.IsNullOrEmpty(id) && profiles.TryGetValue(id, out var specific)) return specific;
        return capability != null && profiles.TryGetValue(capability, out var general) ? general : null;
    }
    public static float Value(string id, Stat stat, float basis, string capability)
    {
        var current = Current;
        if (current == null || string.IsNullOrEmpty(id)) return basis;
        var profile = current.Profile(id, capability);
        return current.State.Value(id, stat, basis, profile != null ? profile.Ratio(stat) : 0);
    }
    public static float Cooldown(string id, float basis, string capability)
    {
        var current = Current;
        if (current == null || string.IsNullOrEmpty(id)) return Mathf.Max(0.001f, basis);
        var profile = current.Profile(id, capability);
        return current.State.Cooldown(id, basis, profile != null ? profile.Ratio(Stat.AttackSpeed) : 0);
    }
    public static float EffectValue(string id, EffectKind kind) => Current != null ? Current.State.Effect(id, kind) : 0;
    private void MaterialsChanged(MaterialType type, int amount) => panel?.RequestShopRefresh();
    public void ToggleShop()
    {
        if (!isActiveAndEnabled || panel == null || !panel.IsReady || choosing) return;
        if (panel.IsShopOpen) panel.CloseShop();
        else if (GameFlowController.Instance.CurrentPhase == GamePhase.Day &&
                 UIManager.Instance?.Flow != null && UIManager.Instance.Flow.Open(UIModal.Crafting))
        {
            RefreshTargets();
            panel.ShowShop();
            TutorialEvents.InvokeCraftingOpened();
        }
    }
    public void CloseShop() => panel?.CloseShop();
    public void PanelDisabled() => ReleaseChoice();
    public bool CanPurchase(SpecialDefinition definition, Target target)
    {
        return !buying && !choosing && target != null && FindTarget(target.Id) != null &&
            State.CanBuy(definition, target) && InventoryManager.Instance != null &&
            InventoryManager.Instance.HasCosts(definition.costs);
    }
    public bool Purchase(SpecialDefinition definition, string targetId)
    {
        if (!isActiveAndEnabled || buying || choosing || panel == null || !panel.IsShopOpen || GameFlowController.Instance.CurrentPhase != GamePhase.Day) return false;
        RefreshTargets();
        var target = FindTarget(targetId);
        if (!CanPurchase(definition, target)) return false;
        buying = true;
        try
        {
            // Commit upgrade before resource notifications; transaction rejects reentrant spending.
            return InventoryManager.Instance.TrySpendCosts(definition.costs, () => State.Buy(definition, target));
        }
        finally { buying = false; }
    }
}
