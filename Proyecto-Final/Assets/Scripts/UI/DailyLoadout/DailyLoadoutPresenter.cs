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
        private readonly DailyLoadoutCatalog catalog = new DailyLoadoutCatalog();
        private readonly List<KeyValuePair<Button, UnityAction>> bindings = new List<KeyValuePair<Button, UnityAction>>();
        private DailyLoadoutSession suppliedSession, activeSession;
        private bool plantCategory = true, ownsModal, lastConfirmed;
        private int selectedPosition = -1;
        private string feedback;
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
            lastConfirmed = session.HasConfirmedToday;
            selectedPosition = 0; plantCategory = true; feedback = null;
            Bind();
            flow.OnPhaseChanged += HandlePhaseChanged;
            modals.ModalChanged += HandleModalChanged;
            gameObject.SetActive(true);
            Refresh();
            return true;
        }

        private bool HasValidView()
        {
            if (view == null || view.PlantSlots == null || view.SpellSlots == null ||
                view.PlantSlots.Count != 3 || view.SpellSlots.Count != 3 || view.OptionCapacity == 0 ||
                view.ConfirmButton == null || view.CancelButton == null || view.ClearButton == null) return false;
            var buttons = new HashSet<Button> { view.ConfirmButton, view.CancelButton, view.ClearButton };
            foreach (var slot in view.PlantSlots)
                if (slot == null || slot.SelectButton == null || !buttons.Add(slot.SelectButton)) return false;
            foreach (var slot in view.SpellSlots)
                if (slot == null || slot.SelectButton == null || !buttons.Add(slot.SelectButton)) return false;
            foreach (var option in view.OptionViews)
                if (option == null || option.SelectButton == null || !buttons.Add(option.SelectButton)) return false;
            return true;
        }

        private void Listen(Button button, UnityAction callback)
        {
            button.onClick.AddListener(callback);
            bindings.Add(new KeyValuePair<Button, UnityAction>(button, callback));
        }

        private void Bind()
        {
            for (int i = 0; i < 3; i++)
            {
                int position = i;
                Listen(view.PlantSlots[i].SelectButton, () => SelectPosition(true, position));
                Listen(view.SpellSlots[i].SelectButton, () => SelectPosition(false, position));
            }
            for (int i = 0; i < view.OptionCapacity; i++)
            {
                int option = i;
                Listen(view.OptionViews[i].SelectButton, () => AssignOption(option));
            }
            Listen(view.ConfirmButton, Confirm);
            Listen(view.ClearButton, Clear);
            Listen(view.CancelButton, Close);
        }

        // Session has no UI change event. Poll only while open, including upgrade overlays,
        // disposal and run reset; never introduce a second equipment snapshot or counter.
        private void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape) && TryCloseFromEscape()) return;
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
            if (activeSession == null || !activeSession.IsReady || unlocks == null || experience == null ||
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
            bool capacityOk = catalog.Plants.Count <= view.OptionCapacity && catalog.Spells.Count <= view.OptionCapacity;
            bool editable = activeSession.CanConfigure && capacityOk;
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
            int count = selectedPosition < 0 ? 0 : plantCategory ? catalog.Plants.Count : catalog.Spells.Count;
            for (int i = 0; i < view.OptionCapacity; i++)
            {
                var option = view.OptionViews[i];
                bool visible = capacityOk && i < count;
                option.gameObject.SetActive(visible);
                if (!visible) continue;
                var plant = plantCategory ? catalog.Plants[i] : null;
                var spell = plantCategory ? null : catalog.Spells[i];
                option.SetContent(plantCategory ? plant.plantIcon : spell.SpellIcon,
                    plantCategory ? plant.plantName : spell.SpellName);
                bool assigned = false;
                for (int slot = 0; slot < 3; slot++)
                    assigned |= (plantCategory
                        ? activeSession.GetDraftPlant(slot).HasValue && activeSession.GetDraftPlant(slot).Identity == plant.seedType
                        : activeSession.GetDraftSpell(slot).HasValue && activeSession.GetDraftSpell(slot).Identity == spell);
                option.SetAssigned(assigned); option.SetSelected(false); option.SetReadOnly(!editable);
                option.SetInvalidDropTarget(false); option.SetLocked(false);
            }
            view.SetOptionsEmpty(count == 0 || !capacityOk);
            view.SetActionAvailability(editable, editable && selectedPosition >= 0);
            string allowance = activeSession.HasConfirmedToday
                ? "Daily configuration allowance: consumed — read-only."
                : activeSession.CanConfigure ? "Daily configuration allowance: available."
                : "Daily configuration allowance: unavailable — read-only.";
            StatusMessage = !capacityOk ? "Loadout option capacity exceeded. Configure the prefab option pool in the Editor."
                : (plantCategory ? "Plant " : "Spell ") + (selectedPosition + 1) + " selected. " +
                    allowance + (string.IsNullOrEmpty(feedback) ? "" : " " + feedback);
            view.SetAllowanceMessage(StatusMessage);
        }

        public void SelectPosition(bool plants, int position)
        {
            if (!IsOpen || position < 0 || position >= 3) return;
            plantCategory = plants; selectedPosition = position; feedback = null;
            if (view.OptionsScroll != null) view.OptionsScroll.verticalNormalizedPosition = 1;
            Refresh();
        }

        private bool CanEdit()
        {
            if (!IsOpen) return false;
            Refresh();
            return IsOpen && activeSession.CanConfigure && catalog.Plants.Count <= view.OptionCapacity &&
                catalog.Spells.Count <= view.OptionCapacity;
        }

        public void AssignOption(int optionIndex)
        {
            if (!CanEdit() || selectedPosition < 0) return;
            int count = plantCategory ? catalog.Plants.Count : catalog.Spells.Count;
            if (optionIndex < 0 || optionIndex >= count) return;
            bool accepted = plantCategory
                ? activeSession.TrySetDraftPlant(selectedPosition, catalog.Plants[optionIndex].seedType)
                : activeSession.TrySetDraftSpell(selectedPosition, catalog.Spells[optionIndex]);
            feedback = accepted ? "Draft updated. Confirm to commit."
                : "Assignment rejected: duplicate or unavailable equipment. Draft unchanged.";
            Refresh();
            if (!accepted && IsOpen)
            {
                view.OptionViews[optionIndex].SetInvalidDropTarget(true);
            }
        }

        public void Clear()
        {
            if (!CanEdit() || selectedPosition < 0) return;
            bool accepted = plantCategory ? activeSession.TrySetDraftPlant(selectedPosition, SeedsEnum.None)
                : activeSession.TrySetDraftSpell(selectedPosition, null);
            feedback = accepted ? "Position cleared in draft." : "Clear rejected. Draft unchanged.";
            Refresh();
        }

        public void Confirm()
        {
            if (!IsOpen) return;
            // Do not clear/rebuild the draft before attempting the authoritative transaction.
            bool confirmed = activeSession.TryConfirm();
            feedback = confirmed ? "Loadout confirmed." : "Confirmation rejected. Draft and daily allowance unchanged.";
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
            foreach (var option in view.OptionViews) if (option != null) option.gameObject.SetActive(false);
        }

        public void Close()
        {
            bool wasOpen = IsOpen;
            if (IsOpen)
            {
                IsOpen = false;
                activeSession?.DiscardDraft();
                if (flow != null) flow.OnPhaseChanged -= HandlePhaseChanged;
                if (modals != null) modals.ModalChanged -= HandleModalChanged;
            }
            foreach (var pair in bindings) if (pair.Key != null) pair.Key.onClick.RemoveListener(pair.Value);
            bindings.Clear();
            if (ownsModal && modals != null) modals.Close(UIModal.DailyLoadout);
            ownsModal = false; activeSession = null; selectedPosition = -1; feedback = null;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
            if (view != null)
            {
                view.SetActionAvailability(false, false);
                foreach (var option in view.OptionViews) if (option != null) option.gameObject.SetActive(false);
                foreach (var slot in view.PlantSlots) if (slot != null) { slot.SetSelected(false); slot.SetReadOnly(true); }
                foreach (var slot in view.SpellSlots) if (slot != null) { slot.SetSelected(false); slot.SetReadOnly(true); }
            }
            if (gameObject.activeSelf) gameObject.SetActive(false);
            // Notify after release/deactivation, exactly once despite OnDisable reentrancy.
            if (wasOpen) Closed?.Invoke();
        }

        private void OnDisable() { Close(); }
        private void OnDestroy() { Close(); }
    }
}
