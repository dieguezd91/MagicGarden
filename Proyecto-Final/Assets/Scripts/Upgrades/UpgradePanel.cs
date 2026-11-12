using System.Collections.Generic;
using MagicGarden;
using UnityEngine;

// Scene-owned presenter. Views own authored visuals and UI events; runtime owns rules and flow.
public sealed class UpgradePanel : MonoBehaviour
{
    [SerializeField] private LevelUpPanelView levelUpView;
    [SerializeField] private SpecialUpgradePanelView shopView;
    [Tooltip("Optional: shows the seeds/spells unlocked by the level, alone, before its upgrade choice.")]
    [SerializeField] private LevelUpRewardView rewardView;
    [Tooltip("One blur backdrop for the whole level-up session: it fades in with the first modal (reward or upgrades), stays while they replace each other and fades out when the session ends. Drawn behind both modals.")]
    [SerializeField] private UIBackdrop sessionBackdrop;
    private UpgradeRuntime runtime;
    private bool shopRefresh, hiding;
    private readonly List<SpecialDefinition> shopDefinitions = new List<SpecialDefinition>();
    private readonly List<string> shopTargets = new List<string>();
    public bool IsShopOpen { get; private set; }
    public bool IsReady => isActiveAndEnabled && levelUpView != null && levelUpView.IsConfigured && shopView != null && shopView.IsConfigured;

    private void Awake() => HideImmediate();
    public bool Initialize(UpgradeRuntime owner)
    {
        if (owner == null || !IsReady || (runtime != null && runtime != owner))
        {
            Debug.LogError("Magic Garden upgrade UI is missing or has invalid serialized prefab references on Canvas Game UI.", this);
            return false;
        }
        if (runtime == owner) return true;
        if (sessionBackdrop == null) Debug.LogWarning("Level-up session backdrop is not wired on Canvas Game UI; the level-up modals show without blur.", this);
        runtime = owner;
        levelUpView.Selected += Select;
        levelUpView.Hidden += ViewHidden;
        shopView.Purchased += Purchase;
        shopView.Closed += CloseShop;
        shopView.Hidden += ViewHidden;
        if (rewardView != null) rewardView.Continued += RewardContinued;
        HideImmediate();
        return true;
    }
    public void Unbind(UpgradeRuntime owner)
    {
        if (runtime != owner) return;
        HideImmediate();
        if (levelUpView != null) { levelUpView.Selected -= Select; levelUpView.Hidden -= ViewHidden; }
        if (shopView != null) { shopView.Purchased -= Purchase; shopView.Closed -= CloseShop; shopView.Hidden -= ViewHidden; }
        if (rewardView != null) rewardView.Continued -= RewardContinued;
        runtime = null;
    }
    // The reward is shown alone; selection is only possible once Continue brought the choices up.
    // Also blocked while the choices are still popping in or closing.
    private void Select(int index)
    {
        if (rewardView != null && (rewardView.IsVisible || rewardView.IsAnimating)) return;
        if (levelUpView != null && levelUpView.IsAnimating) return;
        runtime?.Choose(index);
    }
    private void RewardContinued() => runtime?.RewardContinued();
    public void RequestShopRefresh() { if (IsShopOpen) shopRefresh = true; }
    private static Color ColorFor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Legendary: return new Color(1, 0.76f, 0.25f);
            case Rarity.Rare: return new Color(0.72f, 0.5f, 1);
            case Rarity.Common: return new Color(0.4f, 0.8f, 1);
            default: return new Color(0.65f, 0.85f, 0.65f);
        }
    }
    // Shows the level's unlocks over their backdrop with the upgrade panel hidden; false when there is nothing to show.
    public bool ShowReward(LevelUnlockReward reward)
    {
        if (!IsReady || runtime == null || reward == null || !reward.HasContent) return false;
        if (rewardView == null || !rewardView.IsConfigured)
        {
            Debug.LogWarning("Level-up reward view is not wired on Canvas Game UI; reward card skipped.", this);
            return false;
        }
        IsShopOpen = false;
        HideViews();
        if (!rewardView.Show(reward)) return false;
        sessionBackdrop?.Show();
        return true;
    }
    public void ShowChoices(IList<Offer> offers, int pending)
    {
        if (!IsReady || runtime == null) { WiringFailure(); return; }
        IsShopOpen = false;
        HideViews(); // the session backdrop stays: this modal replaces the reward (or the previous level's choices) behind the same blur
        var data = new List<LevelUpCardData>();
        foreach (var offer in offers)
            data.Add(new LevelUpCardData
            {
                Icon = runtime.Icon(offer.Target.Id), Target = offer.Target.Name, Name = offer.Rule.displayName,
                Stat = offer.Rule.stat.ToString(), Rarity = offer.Rarity.rarity.ToString().ToUpperInvariant(), Tint = ColorFor(offer.Rarity.rarity),
                Bonus = offer.Rule.stat == Stat.Quantity ? "+1" : $"+{offer.Rarity.bonus * offer.Target.Ratio(offer.Rule.stat) * 100:0.##}% base",
                Preview = $"{offer.Before:0.###} → {offer.After:0.###}" + (offer.Rule.stat == Stat.AttackSpeed ? " attacks/s" : "")
            });
        sessionBackdrop?.Show();
        if (!levelUpView.Show(data, pending)) WiringFailure();
    }
    public void ShowShop() => DisplayShop(true);
    private void RefreshShop() => DisplayShop(false);
    private void DisplayShop(bool newOpen)
    {
        if (!IsReady || runtime == null) { WiringFailure(); return; }
        shopRefresh = false;
        newOpen |= !shopView.gameObject.activeInHierarchy;
        if (newOpen) HideImmediate();
        IsShopOpen = true;
        shopDefinitions.Clear(); shopTargets.Clear();
        var items = new List<SpecialUpgradeItemData>();
        foreach (var target in runtime.Targets)
            foreach (var definition in runtime.Balance.specials)
            {
                if (definition.capability != target.Capability) continue;
                string costs = "";
                foreach (var cost in definition.costs)
                {
                    var inventory = InventoryManager.Instance;
                    string name = inventory != null ? inventory.GetMaterialName((MaterialType)cost.material) : ((MaterialType)cost.material).ToString();
                    int owned = inventory != null ? inventory.GetMaterialAmount((MaterialType)cost.material) : 0;
                    costs += $"{name} {owned}/{cost.amount}  ";
                }
                items.Add(new SpecialUpgradeItemData
                {
                    Icon = runtime.Icon(target.Id), Target = target.Name, Name = definition.displayName, Description = definition.description,
                    Costs = costs, Stacks = $"{runtime.State.SpecialStacks(definition.id, target.Id)}/{definition.maxStacks}",
                    Prerequisites = definition.prerequisites != null && definition.prerequisites.Length > 0
                        ? "Requires: " + string.Join(", ", definition.prerequisites) : "",
                    CanPurchase = runtime.CanPurchase(definition, target)
                });
                shopDefinitions.Add(definition); shopTargets.Add(target.Id);
            }
        if (!(newOpen ? shopView.Show(items) : shopView.Refresh(items))) WiringFailure();
    }
    private void Purchase(int index)
    {
        if (runtime == null || !IsShopOpen || index < 0 || index >= shopDefinitions.Count) return;
        runtime.Purchase(shopDefinitions[index], shopTargets[index]);
        runtime.RefreshTargets(); RefreshShop();
    }
    public void CloseShop()
    {
        if (!IsShopOpen) return;
        IsShopOpen = false; shopRefresh = false;
        UIManager.Instance?.Flow?.Close(UIModal.Crafting);
        Hide();
        UIEvents.TriggerCraftingUIClosed();
        TutorialEvents.InvokeCraftingClosed();
    }
    // Closes whatever is open and ends the level-up session; the modals fade/scale out and the backdrop fades out in Play Mode.
    public void Hide()
    {
        hiding = true;
        try { rewardView?.Hide(); levelUpView?.Hide(); shopView?.Hide(); sessionBackdrop?.Hide(); }
        finally { hiding = false; }
    }
    // Tears everything down without animation, backdrop included.
    public void HideImmediate()
    {
        HideViews();
        sessionBackdrop?.HideImmediate();
    }
    // Replaces one modal by another: no close animation, the next Show animates in. The session backdrop is left alone.
    private void HideViews()
    {
        hiding = true;
        try { rewardView?.HideImmediate(); levelUpView?.HideImmediate(); shopView?.Hide(); }
        finally { hiding = false; }
    }
    private void WiringFailure()
    {
        Debug.LogError("Magic Garden upgrade UI cannot display: check the serialized Views, three cards and shop item prefab on Canvas Game UI.", this);
        runtime?.PanelDisabled();
        // A missing shop view must not leave the modal/input owner open either.
        if (IsShopOpen) CloseShop();
        else UIManager.Instance?.Flow?.Close(UIModal.Crafting);
        HideImmediate();
    }
    private void ViewHidden()
    {
        if (hiding) return;
        runtime?.PanelDisabled();
        if (IsShopOpen) CloseShop();
    }
    private void OnDisable() => ViewHidden();
    private void OnDestroy() { if (runtime != null) Unbind(runtime); }
    private void Update()
    {
        if (IsShopOpen && shopRefresh) { runtime.RefreshTargets(); RefreshShop(); }
        if (IsShopOpen && Input.GetKeyDown(KeyCode.Escape) && !InputConsumptionManager.IsEscapeConsumed)
        {
            InputConsumptionManager.ConsumeEscape(); CloseShop();
        }
    }
}
