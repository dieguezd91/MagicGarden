# Connect the Sacred Tree to Daily Loadout

Stage 4C routes the **existing** `RestorationAltar` and F trigger to the Stage 4B presenter through `UIManager.DailyLoadout`. The presenter retains session discovery, editing/confirmation authority and exclusive `UIModal.DailyLoadout` ownership. No HUD, storage, admission, bootstrap, balance or restoration business rules are changed.

## Apply the native wiring

1. Let Unity **2022.3.62f3** import/compile the scripts. Save/review any relevant unsaved work and leave Prefab Mode. Do not regenerate Stage 4A or rebind the already-authored Stage 4B panel.
2. Run **Tools → Magic Garden → Daily Loadout → Wire Sacred Tree (Stage 4C)**. This explicit, idempotent command:
   - Adds one **nested instance** of the existing `DailyLoadoutPanel.prefab` under `Canvas Game UI.prefab`, initially inactive; binds `UIManager.dailyLoadout`, presenter view and the shared serialized UI flow. It refuses duplicate/unrecognized presenters rather than deleting user work.
   - Changes only the existing tree tutorial proximity route in `Restoration Altar.prefab`. Collider, F key, range, prompt, audio, restoration component and options stay intact.
   - Migrates the **existing** two tutorial step assets and hint title using native asset APIs. Names, GUIDs, step IDs, ordering, counts and gating remain stable.
   - Validates the saved prefab references and the actual `Game` scene. If Game is not already loaded, it opens it additively, closes only that temporary scene and restores the active scene. It does not save or replace your open scene or hand-edit YAML.
3. Review the generated asset diff. Run **Validate Sacred Tree (Stage 4C)** independently afterward. Rerunning Wire does not add another panel, presenter or handler.

No menu runs automatically on import. Custom tutorial copy/objectives, dirty target assets, a dirty Game scene or an open prefab stage stop authoring for review. If validation reports a scene override or a pre-existing missing component, resolve that specific issue in Unity; do not regenerate/delete unrelated content. Saving multiple assets is not transactional; if Unity reports a save failure, review the partial diff before rerunning.

**Current authoring status:** this coding session has not run the menu. Scene/prefab/tutorial asset wiring remains pending native Editor execution. Existing user changes to ProjectSettings and the TMP fallback asset are unrelated and must remain untouched.

## Lifecycle and tutorial

| Concern | Contract |
|---|---|
| Admission | Exact Day phase, positive time scale, no upgrade block or other modal; presenter independently checks ready run session and view. A confirmed day is not rejected. |
| Initialization | The inactive panel does not need the player's session in Awake. First F before player `Start` fails cleanly; subsequent F resolves the now-ready current owner. No extra session or singleton is created. |
| Completion | Optional `IInteractionLifecycle` subscribes only the initiating trigger to its altar. Altar subscribes only while its presenter is open. Legacy crafting/restoration notifications cannot release a Loadout interaction. |
| Cancel / Escape | Presenter discards draft, releases its own modal, deactivates, then emits `Closed` once. Escape uses `InputConsumptionManager` before closing, irrespective of Update order with Pause. |
| Confirm | Existing Stage 4B behavior: commit once, stay open read-only. Cancel or Escape still closes; reopening remains read-only that day. |
| Night / interruption | Existing phase/modal/disposed-session checks close the panel. Trigger/altar/UI owner disable or destruction closes its owned panel and cleans up subscriptions. Leaving range suppresses the prompt; later closure cannot show an out-of-range prompt. |
| Full reset | Gameplay reset explicitly closes `UIManager`'s panel before existing reset operations. Standalone session-reset refresh behavior from Stage 4B is unchanged. |
| Tutorial | Append-only `OpenDailyLoadout=28`, `DailyLoadoutProximity=29`; proximity route appends `DailyLoadoutProximity=4`. Reach the Sacred Tree, then successfully open its panel. The opening objective completes on **successful open**, without requiring confirmation or consuming the daily allowance. Failed opens do not advance it. No restoration tutorial/UI events are emitted. |

**Narrative/design note:** healing is no longer taught at this tree. Restoration systems, costs, health rules, animations, UI assets and old objective/event values remain available for a future restoration entry point. The migrated open objective deliberately teaches access, not mandatory daily confirmation; no unrelated tutorial step is redesigned.

## Automated verification

Run from an empty/Menu test context, not a live Game world. Apply native wiring **before** installed-reference validation tests. Tests must not mutate production prefabs to make themselves pass.

- **EditMode:** `DailyLoadoutWorldInteractionTests`, `DailyLoadoutWorldAuthoringTests`, all Stage 4B `DailyLoadout*` tests, and Loadout / Equipment / LoadoutBaseline regression assemblies.
- **PlayMode:** `DailyLoadoutWorldPlayModeTests`, `DailyLoadoutPresenterPlayModeTests`, existing loadout admission/cooldown/drag lifecycle suites.
- The world tests call the same public `InteractionTrigger.TryInteract` entry used by F; trigger overlap is simulated, not native keyboard injection. PlayMode covers real owner Awake/OnEnable/Start, no session injection, an early rejection and retry, real panel editing/confirmation, read-only reopening, Escape, night, reset, disable and destruction.
- `ProductionTutorialUiDefersNextStepUntilLoadoutClosesAndResumesWithoutLegacyEvents` uses the real TutorialUI/manager scheduling and Update fallback: successful opening completes the objective, the following step waits for the open modal, and Cancel permits it to display and complete without restoration notifications or private input-buffer shortcuts.
- Authoring tests use **unsaved prefab contents** to assert idempotence and preservation. `InstalledGameAndTutorialReferencesAreValid` intentionally fails until the menu has been run.
- Source/static compilation is not a Unity import, Test Runner result, visual approval, keyboard test or proof of full Game scene startup. Run the checklist below separately.

The Editor is already open and CLI `Unity status --json` found no Pipeline endpoint. No packages were installed and no competing batch Editor was launched. Native tests/manual checks are unexecuted by this coding session.

## Gameplay checklist — owner execution required

- [ ] Open Game and start a normal run; reach the existing Sacred Tree during Day. Existing F prompt/range/art/collider are unchanged.
- [ ] Press F once: exactly one real Daily Loadout opens; no restoration panel. Available catalog and current assignments match the active player. Repeated F cannot duplicate it.
- [ ] Edit both categories; Cancel discards. Close restores prompt/input. Reopen, confirm once, close and reopen read-only; allowance does not reset.
- [ ] Escape closes exactly once without opening Pause in the same frame. Normal movement/action input resumes afterward.
- [ ] Leave range with the panel open, then close; no stale prompt. Reenter and reopen normally.
- [ ] Inventory/crafting/upgrade/pause/ritual/GameOver prevent access or overlap. Night closes the open panel and discards unconfirmed edits.
- [ ] Reset run while open, disable/destroy the initiating tree or UI owner, and unload/reload Game: no stale modal ownership or callbacks.
- [ ] In a fresh tutorial, reach the tree then open Loadout; both migrated objectives advance without healing/confirmation or false restoration events. Confirmed-day inspection can also complete the open objective.
- [ ] Run the EditMode/PlayMode suites, including Stage 4B regressions; inspect XML/failures/skips. Review native asset references and unchanged HUD/restoration assets.

Do not mark Stage 4C complete until native asset authoring, automated suites and the manual gameplay flow pass. Stage 4D, commits and push remain out of scope.
