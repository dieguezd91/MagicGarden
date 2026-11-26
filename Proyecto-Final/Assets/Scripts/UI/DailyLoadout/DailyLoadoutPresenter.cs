using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MagicGarden.LoadoutUI
{
    /// <summary>UI intent only: the run-owned session remains the sole equipment authority.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DailyLoadoutPanelView))]
    public sealed class DailyLoadoutPresenter : MonoBehaviour
    {
        [SerializeField] private DailyLoadoutPanelView view;
        [SerializeField] private PlayerContentUnlockSystem unlocks;
        [SerializeField] private PlayerExperienceSystem experience;
        [SerializeField] private GameFlowController flow;
        [SerializeField] private UIFlowController modals;
        [Tooltip("Shared blur behind modal UI (the same one the inventory uses). Optional.")]
        [SerializeField] private UIBackdrop backdrop;
        private readonly DailyLoadoutCatalog catalog = new DailyLoadoutCatalog();
        private readonly List<KeyValuePair<Button, UnityAction>> bindings = new List<KeyValuePair<Button, UnityAction>>();
        private DailyLoadoutSession suppliedSession, activeSession;
        private bool plantCategory = true, ownsModal, lastConfirmed;
        private int selectedPosition = -1;
        private string feedback;
        // Transient pointer intent only. The session remains the only assignment state.
        private LoadoutOptionDragSource dragSource;
        private DailyLoadoutSession dragSession;
        private LoadoutSlotView dragHover;
        private bool dragPlants;
        private SeedsEnum dragSeed;
        private SpellDataSO dragSpell;
        private int dragPointer;
        private LoadoutEquipmentView hoverView;
        public event System.Action Closed;
        public bool IsOpen { get; private set; }
        public int SelectedPosition => selectedPosition;
        public bool IsPlantCategory => plantCategory;
        public string StatusMessage { get; private set; }

        /// <summary>Optional composition for isolated harnesses or future scene wiring; never creates a session.</summary>
        public void Initialize(DailyLoadoutSession session, PlayerContentUnlockSystem owner,
            PlayerExperienceSystem progression, GameFlowController gameFlow, UIFlowController uiFlow)
        {
            Close();
            suppliedSession = session; unlocks = owner; experience = progression; flow = gameFlow; modals = uiFlow;
            if (view == null) view = GetComponent<DailyLoadoutPanelView>();
        }

        private void Resolve()
        {
            if (view == null) view = GetComponent<DailyLoadoutPanelView>();
            if (unlocks == null) unlocks = FindObjectOfType<PlayerContentUnlockSystem>();
            if (experience == null && unlocks != null) experience = unlocks.GetComponent<PlayerExperienceSystem>();
            if (flow == null) flow = GameFlowController.Instance;
            if (modals == null) modals = FindObjectOfType<UIFlowController>();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && !IsOpen && !Open()) gameObject.SetActive(false);
        }

        public bool Open()
        {
            if (IsOpen) return true;
            Resolve();
            var session = suppliedSession ?? (unlocks != null ? unlocks.Loadout : null);
            if (!HasValidView() || session == null || !session.IsReady || unlocks == null ||
                experience == null || experience.ProgressionData == null || flow == null ||
                flow.CurrentPhase != GamePhase.Day || !session.IsDay || modals == null)
            {
                SetUnavailable("Loadout unavailable. Wait for the equipment session and a daytime UI owner.");
                return false;
            }
            if (!modals.Open(UIModal.DailyLoadout))
            {
                SetUnavailable("Loadout unavailable while another modal is open.");
                return false;
            }
            activeSession = session; ownsModal = true; IsOpen = true;
            if (backdrop != null) backdrop.Show();
            lastConfirmed = session.HasConfirmedToday;
            selectedPosition = 0; plantCategory = true; feedback = null;
            Bind();
            flow.OnPhaseChanged += HandlePhaseChanged;
            modals.ModalChanged += HandleModalChanged;
            gameObject.SetActive(true);
            Refresh();
            PlaySfx("UI_Loadout_Open");
            return true;
        }

        private bool HasValidView()
        {
            if (view == null || view.PlantSlots == null || view.SpellSlots == null ||
                view.PlantSlots.Count != 3 || view.SpellSlots.Count != 3 || view.OptionCapacity == 0 ||
                view.ConfirmButton == null || view.CancelButton == null || view.ClearButton == null ||
                view.PlantTabButton == null || view.SpellTabButton == null) return false;
            var buttons = new HashSet<Button> { view.ConfirmButton, view.CancelButton, view.ClearButton };
            foreach (var slot in view.PlantSlots)
                if (slot == null || slot.SelectButton == null || !buttons.Add(slot.SelectButton)) return false;
            foreach (var slot in view.SpellSlots)
                if (slot == null || slot.SelectButton == null || !buttons.Add(slot.SelectButton)) return false;
            foreach (var option in AllOptions())
                if (option == null || option.SelectButton == null || !buttons.Add(option.SelectButton)) return false;
            return true;
        }

        private static void PlayUi(InterfaceSoundType type) => UIManager.Instance?.InterfaceSounds?.PlaySound(type);
        private static void PlaySfx(string name) => SoundManager.Instance?.Play(name);

        private void Listen(Button button, UnityAction callback)
        {
            if (button.GetComponent<ImprovedUIButton>() == null && button.GetComponent<LoadoutButtonHoverSound>() == null)
                button.gameObject.AddComponent<LoadoutButtonHoverSound>();
            button.onClick.AddListener(callback);
            bindings.Add(new KeyValuePair<Button, UnityAction>(button, callback));
        }

        private void Bind()
        {
            for (int i = 0; i < 3; i++)
            {
                int position = i;
                Listen(view.PlantSlots[i].SelectButton, () => { PlaySfx("UI_Loadout_Slot_Select"); SelectPosition(true, position); });
                Listen(view.SpellSlots[i].SelectButton, () => { PlaySfx("UI_Loadout_Slot_Select"); SelectPosition(false, position); });
                view.PlantSlots[i].BindDrop(this, true, i); view.SpellSlots[i].BindDrop(this, false, i);
            }
            BindOptions(view.PlantOptionViews, true);
            BindOptions(view.SpellOptionViews, false);
            // The tab tap only plays when the category actually changes.
            Listen(view.PlantTabButton, () => { if (!plantCategory) PlaySfx("UI_Loadout_Tab"); SelectPosition(true, Mathf.Max(0, selectedPosition)); });
            Listen(view.SpellTabButton, () => { if (plantCategory) PlaySfx("UI_Loadout_Tab"); SelectPosition(false, Mathf.Max(0, selectedPosition)); });
            Listen(view.ConfirmButton, Confirm);
            Listen(view.ClearButton, Clear);
            Listen(view.CancelButton, Close);
            if (view.CloseButton != null) Listen(view.CloseButton, Close);
            foreach (var target in EquipmentViews()) if (target != null) target.HoverChanged += HandleHover;
        }

        private IEnumerable<LoadoutOptionView> AllOptions()
        {
            foreach (var option in view.PlantOptionViews) yield return option;
            foreach (var option in view.SpellOptionViews) yield return option;
        }

        private void BindOptions(IReadOnlyList<LoadoutOptionView> options, bool plants)
        {
            for (int i = 0; i < options.Count; i++)
            {
                int option = i;
                Listen(options[i].SelectButton, () => AssignOption(plants, option));
                options[i].BindDrag(this, i);
            }
        }

        // Session has no UI change event. Poll only while open, including upgrade overlays,
        // disposal and run reset; never introduce a second equipment snapshot or counter.
        private void Update()
        {
            if (!IsOpen) return;
            if (!SceneTransitionController.InputBlocked && Input.GetKeyDown(KeyCode.Escape) && TryCloseFromEscape()) return;
            Refresh();
        }

        public bool TryCloseFromEscape()
        {
            if (!IsOpen || InputConsumptionManager.IsEscapeConsumed) return false;
            InputConsumptionManager.ConsumeEscape();
            Close();
            return true;
        }
        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Day) Close();
            else Refresh();
        }
        private void HandleModalChanged(UIModal modal) { if (modal != UIModal.DailyLoadout) Close(); }

        public void Refresh()
        {
            if (!IsOpen) return;
            if (activeSession == null || !activeSession.IsReady || unlocks == null ||
                !ReferenceEquals(activeSession, suppliedSession ?? unlocks.Loadout) || experience == null ||
                experience.ProgressionData == null || flow == null || flow.WorldPhase != GamePhase.Day ||
                modals == null || !modals.IsOpen(UIModal.DailyLoadout))
            {
                Close(); return;
            }
            if (lastConfirmed != activeSession.HasConfirmedToday)
            {
                feedback = null;
                lastConfirmed = activeSession.HasConfirmedToday;
            }
            catalog.Refresh(experience.ProgressionData, unlocks);
            view.ShowCategory(plantCategory);
            bool capacityOk = catalog.Plants.Count <= view.OptionCapacity && catalog.Spells.Count <= view.OptionCapacity;
            bool editable = activeSession.CanConfigure && capacityOk;
            if (dragSession != null && (!editable || !ReferenceEquals(dragSession, activeSession) || !DragIdentityAvailable())) CancelDrag();
            for (int i = 0; i < 3; i++)
            {
                var plant = activeSession.CanConfigure ? activeSession.GetDraftPlant(i) : activeSession.GetCommittedPlant(i);
                var spell = activeSession.CanConfigure ? activeSession.GetDraftSpell(i) : activeSession.GetCommittedSpell(i);
                var plantData = plant.HasValue ? catalog.FindPlant(plant.Identity) : null;
                var spellData = spell.HasValue ? spell.Identity : null;
                var plantView = view.PlantSlots[i]; var spellView = view.SpellSlots[i];
                plantView.SetContent(plantData != null ? plantData.plantIcon : null,
                    plant.HasValue ? (plantData != null ? plantData.plantName : plant.Identity.ToString()) : null);
                spellView.SetContent(spellData != null ? spellData.SpellIcon : null,
                    spell.HasValue ? (spellData != null ? spellData.SpellName : "Unavailable spell") : null);
                plantView.SetAssigned(plant.HasValue); spellView.SetAssigned(spell.HasValue);
                plantView.SetSelected(plantCategory && selectedPosition == i);
                spellView.SetSelected(!plantCategory && selectedPosition == i);
                plantView.SetReadOnly(!editable); spellView.SetReadOnly(!editable);
                // Category/position browsing remains available after daily confirmation.
                plantView.SelectButton.interactable = true; spellView.SelectButton.interactable = true;
            }
            RefreshOptions(true, editable, capacityOk);
            RefreshOptions(false, editable, capacityOk);
            RefreshDetail();
            view.SetActionAvailability(editable, editable && selectedPosition >= 0);
            string allowance = activeSession.HasConfirmedToday
                ? "Daily configuration allowance: consumed — read-only."
                : activeSession.CanConfigure ? "Daily configuration allowance: available."
                : "Daily configuration allowance: unavailable — read-only.";
            StatusMessage = !capacityOk ? "Loadout option capacity exceeded. Configure the prefab option pool in the Editor."
                : (plantCategory ? "Plant " : "Spell ") + (selectedPosition + 1) + " selected. " +
                    allowance + (string.IsNullOrEmpty(feedback) ? "" : " " + feedback);
            view.SetAllowanceMessage("<sprite name=\"SunIcon\"> 1 change per day " + (activeSession.HasConfirmedToday
                ? "<color=#E8A35A>USED</color>" : editable ? "<color=#8BE28B>AVAILABLE</color>" : "<color=#E8A35A>LOCKED</color>"));
            view.SetFeedbackMessage(!capacityOk ? StatusMessage : feedback);
            ApplyDragFeedback(); // Polling must not erase the pointer's transient target state.
        }

        // Both catalogs are visible at once, so each one is refreshed every time.
        private void RefreshOptions(bool plants, bool editable, bool capacityOk)
        {
            var options = view.Options(plants);
            int count = selectedPosition < 0 ? 0 : plants ? catalog.Plants.Count : catalog.Spells.Count;
            for (int i = 0; i < view.OptionCapacity; i++)
            {
                var option = options[i];
                bool visible = capacityOk && i < count;
                option.gameObject.SetActive(visible);
                if (!visible) continue;
                var plant = plants ? catalog.Plants[i] : null;
                var spell = plants ? null : catalog.Spells[i];
                option.SetContent(plants ? plant.plantIcon : spell.SpellIcon, plants ? plant.plantName : spell.SpellName);
                int held = -1;
                for (int slot = 0; slot < 3 && held < 0; slot++)
                    if (plants
                        ? activeSession.GetDraftPlant(slot).HasValue && activeSession.GetDraftPlant(slot).Identity == plant.seedType
                        : activeSession.GetDraftSpell(slot).HasValue && activeSession.GetDraftSpell(slot).Identity == spell) held = slot;
                option.SetAssigned(held >= 0); option.SetEquippedSlot(held);
                option.SetSelected(false); option.SetReadOnly(!editable);
                option.SetInvalidDropTarget(false); option.SetLocked(false);
            }
            view.SetOptionsEmpty(plants, count == 0 || !capacityOk);
        }


        // Description strip: whatever the pointer is over, otherwise the selected position.
        private void RefreshDetail()
        {
            PlantDataSO plant = null; SpellDataSO spell = null;
            if (hoverView != null && hoverView.isActiveAndEnabled) Describe(hoverView, out plant, out spell);
            if (plant == null && spell == null && selectedPosition >= 0)
                Describe(plantCategory ? view.PlantSlots[selectedPosition] : view.SpellSlots[selectedPosition], out plant, out spell);
            if (plant != null) view.SetSelection(true, selectedPosition, plant.plantIcon, plant.plantName, plant.description);
            else if (spell != null) view.SetSelection(false, selectedPosition, spell.SpellIcon, spell.SpellName,
                "Mana cost " + spell.ManaCost + ". Cooldown " + spell.Cooldown.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s.");
            else view.SetSelection(plantCategory, selectedPosition, null, string.Empty, "Hover a plant or spell to see what it does.");
        }

        private void Describe(LoadoutEquipmentView target, out PlantDataSO plant, out SpellDataSO spell)
        {
            plant = null; spell = null;
            for (int i = 0; i < 3; i++)
            {
                if (target == view.PlantSlots[i])
                {
                    var held = activeSession.CanConfigure ? activeSession.GetDraftPlant(i) : activeSession.GetCommittedPlant(i);
                    if (held.HasValue) plant = catalog.FindPlant(held.Identity);
                    return;
                }
                if (target == view.SpellSlots[i])
                {
                    var held = activeSession.CanConfigure ? activeSession.GetDraftSpell(i) : activeSession.GetCommittedSpell(i);
                    if (held.HasValue) spell = held.Identity;
                    return;
                }
            }
            for (int i = 0; i < catalog.Plants.Count && i < view.PlantOptionViews.Count; i++)
                if (target == view.PlantOptionViews[i]) { plant = catalog.Plants[i]; return; }
            for (int i = 0; i < catalog.Spells.Count && i < view.SpellOptionViews.Count; i++)
                if (target == view.SpellOptionViews[i]) { spell = catalog.Spells[i]; return; }
        }

        private void HandleHover(LoadoutEquipmentView target, bool entered)
        {
            if (entered) hoverView = target;
            else if (hoverView == target) hoverView = null;
        }

        private IEnumerable<LoadoutEquipmentView> EquipmentViews()
        {
            foreach (var slot in view.PlantSlots) yield return slot;
            foreach (var slot in view.SpellSlots) yield return slot;
            foreach (var option in AllOptions()) yield return option;
        }

        private int FirstEmptyPosition(bool plants)
        {
            for (int slot = 0; slot < 3; slot++)
                if (!(plants ? activeSession.GetDraftPlant(slot).HasValue : activeSession.GetDraftSpell(slot).HasValue)) return slot;
            return -1;
        }

        public void SelectPosition(bool plants, int position)
        {
            if (!IsOpen || position < 0 || position >= 3) return;
            if (plantCategory != plants) CancelDrag();
            plantCategory = plants; selectedPosition = position; feedback = null;
            view.ShowCategory(plants);
            Refresh();
        }

        private bool CanEdit()
        {
            if (!IsOpen) return false;
            Refresh();
            return IsOpen && activeSession.CanConfigure && catalog.Plants.Count <= view.OptionCapacity &&
                catalog.Spells.Count <= view.OptionCapacity;
        }

        public void AssignOption(int optionIndex) { AssignOption(plantCategory, optionIndex); }

        // Click path. Same category as the selection: that position. Other category: its first empty position.
        public void AssignOption(bool plants, int optionIndex)
        {
            if (!CanEdit() || selectedPosition < 0) return;
            int count = plants ? catalog.Plants.Count : catalog.Spells.Count;
            if (optionIndex < 0 || optionIndex >= count) return;
            int target = plants == plantCategory ? selectedPosition : FirstEmptyPosition(plants);
            bool accepted = target >= 0 && AssignIdentity(plants, plants ? catalog.Plants[optionIndex].seedType : SeedsEnum.None,
                plants ? null : catalog.Spells[optionIndex], plants, target);
            if (!accepted && IsOpen) view.Options(plants)[optionIndex].SetInvalidDropTarget(true);
        }

        private bool AssignIdentity(bool sourcePlants, SeedsEnum seed, SpellDataSO spell, bool targetPlants, int position)
        {
            bool accepted = CanEdit() && sourcePlants == targetPlants && position >= 0 && position < 3 &&
                (sourcePlants ? seed != SeedsEnum.None && activeSession.TrySetDraftPlant(position, seed)
                    : spell != null && activeSession.TrySetDraftSpell(position, spell));
            if (accepted) { plantCategory = targetPlants; selectedPosition = position; }
            PlaySfx(accepted ? "UI_Loadout_Assign" : "UI_Loadout_Reject");
            feedback = accepted ? "Draft updated. Confirm to commit."
                : "Assignment rejected: duplicate or unavailable equipment. Draft unchanged.";
            Refresh();
            return accepted;
        }

        public bool BeginOptionDrag(LoadoutOptionDragSource source, int optionIndex, PointerEventData data)
        {
            if (dragSession != null || source == null || !CanEdit() || !view.HasDragPreview) return false;
            // The source only knows its pool index; the pool that contains it decides the category.
            bool sourcePlants = optionIndex >= 0 && optionIndex < view.PlantOptionViews.Count &&
                source.transform.IsChildOf(view.PlantOptionViews[optionIndex].transform);
            int count = sourcePlants ? catalog.Plants.Count : catalog.Spells.Count;
            if (optionIndex < 0 || optionIndex >= count || optionIndex >= view.Options(sourcePlants).Count ||
                !source.transform.IsChildOf(view.Options(sourcePlants)[optionIndex].transform)) return false;
            dragPlants = sourcePlants;
            dragSeed = dragPlants ? catalog.Plants[optionIndex].seedType : SeedsEnum.None;
            dragSpell = dragPlants ? null : catalog.Spells[optionIndex];
            if (!DragIdentityAvailable()) { dragSeed = SeedsEnum.None; dragSpell = null; return false; }
            dragSource = source; dragSession = activeSession; dragPointer = data.pointerId;
            dragSession.CommittedChanged += CancelDrag; // Reset/confirmation invalidates the interaction, never the model.
            view.ShowDragPreview(view.Options(dragPlants)[optionIndex].IconSprite, data);
            feedback = "Drop onto a matching position.";
            Refresh();
            return true;
        }

        private bool DragIdentityAvailable()
        {
            if (unlocks == null) return false;
            if (dragPlants) return dragSeed != SeedsEnum.None && catalog.FindPlant(dragSeed) != null && unlocks.IsPlantUnlocked(dragSeed);
            if (dragSpell == null || !unlocks.IsSpellUnlocked(dragSpell)) return false;
            foreach (var spell in catalog.Spells) if (spell == dragSpell) return true;
            return false;
        }

        private bool MatchesDrag(LoadoutOptionDragSource source, PointerEventData data)
            => dragSession != null && dragSource == source && data.pointerId == dragPointer && data.pointerDrag == source.gameObject;

        public void MoveOptionDrag(LoadoutOptionDragSource source, PointerEventData data)
        {
            if (!MatchesDrag(source, data)) return;
            Refresh();
            if (MatchesDrag(source, data)) view.MoveDragPreview(data);
        }

        public void EndOptionDrag(LoadoutOptionDragSource source, PointerEventData data)
        {
            if (!MatchesDrag(source, data)) return;
            CancelDrag(); feedback = "Drop cancelled. Draft unchanged."; Refresh();
        }

        public void CancelOptionDrag(LoadoutOptionDragSource source) { if (dragSource == source) CancelDrag(); }

        public void HoverDropTarget(LoadoutSlotView slot, PointerEventData data, bool entered)
        {
            if (dragSource == null || !MatchesDrag(dragSource, data)) return;
            if (entered) dragHover = slot;
            else if (dragHover == slot) dragHover = null;
            ApplyDragFeedback();
        }

        public void DropOption(LoadoutSlotView slot, bool plants, int position, PointerEventData data)
        {
            if (dragSource == null || !MatchesDrag(dragSource, data) || !CanEdit() || dragSession == null) return;
            if (position < 0 || position >= 3 || slot != (plants ? view.PlantSlots[position] : view.SpellSlots[position])) return;
            // Capture before cleanup/refresh can recycle the original option view.
            bool sourcePlants = dragPlants; var seed = dragSeed; var spell = dragSpell;
            CancelDrag();
            AssignIdentity(sourcePlants, seed, spell, plants, position);
        }

        private bool CanDrop(bool plants, int position)
        {
            if (dragSession == null || !dragSession.CanConfigure || plants != dragPlants || !DragIdentityAvailable()) return false;
            // Read-only prediction for feedback. Only the authoritative setter below can accept a drop.
            for (int i = 0; i < 3; i++)
            {
                if (i == position) continue;
                if (plants ? dragSession.GetDraftPlant(i).HasValue && dragSession.GetDraftPlant(i).Identity == dragSeed
                    : dragSession.GetDraftSpell(i).HasValue && dragSession.GetDraftSpell(i).Identity == dragSpell) return false;
            }
            return true;
        }

        private void ApplyDragFeedback()
        {
            if (view == null) return;
            for (int i = 0; i < view.PlantSlots.Count; i++)
            {
                var slot = view.PlantSlots[i]; bool allowed = CanDrop(true, i);
                if (slot != null) slot.SetDropFeedback(allowed, dragSession != null && dragHover == slot, allowed);
            }
            for (int i = 0; i < view.SpellSlots.Count; i++)
            {
                var slot = view.SpellSlots[i]; bool allowed = CanDrop(false, i);
                if (slot != null) slot.SetDropFeedback(allowed, dragSession != null && dragHover == slot, allowed);
            }
        }

        private void CancelDrag()
        {
            if (dragSession != null) dragSession.CommittedChanged -= CancelDrag;
            dragSession = null; dragSource = null; dragHover = null; dragSeed = SeedsEnum.None; dragSpell = null; dragPointer = 0;
            if (view != null) { view.HideDragPreview(); ApplyDragFeedback(); }
        }

        public void Clear() { ClearPosition(plantCategory, selectedPosition); }

        // Empties one draft position and selects it. Right-click on a slot lands here.
        public void ClearPosition(bool plants, int position)
        {
            CancelDrag();
            if (!CanEdit() || position < 0 || position >= 3) return;
            bool accepted = plants ? activeSession.TrySetDraftPlant(position, SeedsEnum.None)
                : activeSession.TrySetDraftSpell(position, null);
            if (accepted) { plantCategory = plants; selectedPosition = position; }
            PlaySfx(accepted ? "UI_Loadout_Clear" : "UI_Loadout_Reject");
            feedback = accepted ? "Position cleared in draft." : "Clear rejected. Draft unchanged.";
            Refresh();
        }

        public void Confirm()
        {
            if (!IsOpen) return;
            CancelDrag();
            // Do not clear/rebuild the draft before attempting the authoritative transaction.
            bool confirmed = activeSession.TryConfirm();
            PlaySfx(confirmed ? "UI_Loadout_Confirm" : "UI_Loadout_Reject");
            feedback = confirmed ? "Loadout confirmed."
                : activeSession.CanConfigure && activeSession.IsTutorialLoadoutRequired && !activeSession.HasUsableDraft
                    ? DailyLoadoutSession.TutorialLoadoutRequiredMessage
                    : "Confirmation rejected. Draft and daily allowance unchanged.";
            lastConfirmed = activeSession.HasConfirmedToday;
            Refresh();
        }

        private void SetUnavailable(string message)
        {
            StatusMessage = message;
            if (view == null) return;
            view.SetActionAvailability(false, false); view.SetAllowanceMessage(message); view.SetOptionsEmpty(true);
            foreach (var slot in view.PlantSlots) if (slot != null) slot.SetReadOnly(true);
            foreach (var slot in view.SpellSlots) if (slot != null) slot.SetReadOnly(true);
            foreach (var option in AllOptions()) if (option != null) option.gameObject.SetActive(false);
        }

        public void Close()
        {
            CancelDrag();
            bool wasOpen = IsOpen;
            if (IsOpen)
            {
                PlaySfx("UI_Loadout_Close");
                if (backdrop != null) backdrop.Hide();
                IsOpen = false;
                activeSession?.DiscardDraft();
                if (flow != null) flow.OnPhaseChanged -= HandlePhaseChanged;
                if (modals != null) modals.ModalChanged -= HandleModalChanged;
            }
            foreach (var pair in bindings) if (pair.Key != null) pair.Key.onClick.RemoveListener(pair.Value);
            bindings.Clear();
            if (view != null) foreach (var target in EquipmentViews()) if (target != null) target.HoverChanged -= HandleHover;
            hoverView = null;
            if (ownsModal && modals != null) modals.Close(UIModal.DailyLoadout);
            ownsModal = false; activeSession = null; selectedPosition = -1; feedback = null;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
            if (view != null)
            {
                view.SetActionAvailability(false, false);
                foreach (var option in AllOptions()) if (option != null) { option.BindDrag(null, -1); option.gameObject.SetActive(false); }
                foreach (var slot in view.PlantSlots) if (slot != null) { slot.BindDrop(null, true, -1); slot.SetSelected(false); slot.SetReadOnly(true); }
                foreach (var slot in view.SpellSlots) if (slot != null) { slot.BindDrop(null, false, -1); slot.SetSelected(false); slot.SetReadOnly(true); }
            }
            if (gameObject.activeSelf) gameObject.SetActive(false);
            // Notify after release/deactivation, exactly once despite OnDisable reentrancy.
            if (wasOpen) Closed?.Invoke();
        }

        private void OnDisable() { Close(); }
        private void OnDestroy() { Close(); }
    }
}
