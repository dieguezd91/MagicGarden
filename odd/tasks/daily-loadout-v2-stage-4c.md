# Daily Loadout V2 — Stage 4C
Objective: connect the existing Sacred Tree to the session-backed panel. Branch: feature/daily-loadout-v2.
Delivery: local changes only, no commit/push, no Stage 4D. Runner: Unity 2022.3.62f3 EditMode/PlayMode; active Editor has no CLI endpoint.

## Specs
S1. "Use the current local working tree as the source of truth, including the latest Stage 4B commit. Do not require a push." "Inspect current local implementations of:" "RestorationAltar" "InteractionTrigger" "DailyLoadoutPresenter" "DailyLoadoutPanelView" "UIFlowController" "UIEvents" "UIManager" "RestorationAltarUIManager" "HouseRestorationSystem" "TutorialManager" "TutorialEvents" "TutorialStep" "TutorialProximityTrigger" "Also inspect the current `Game` scene, the tree/altar prefab, and `Canvas Game UI.prefab`." "Reuse those contracts rather than creating parallel controllers."
S2. "Preserve the existing physical interactable, collider, interaction range, prompt and input key F." "When the player interacts during the Day:" "Open the actual Stage 4B Daily Loadout panel." "Resolve the active player's current loadout session." "Display current assignments and available content." "Permit editing only through existing session rules." "Allow read-only inspection after the daily confirmation has been consumed." "Do not reject interaction merely because the player has already confirmed that day." "The tree must not open the restoration panel." "During Night, GameOver, ritual, pause or another blocking state, reject opening through the appropriate existing contracts." "Do not create another interactable or new global singleton."
S3. "Use the existing `DailyLoadoutPanel.prefab`." "Integrate a persistent prefab instance under the appropriate gameplay UI Canvas." "Correct serialized references." "Initially closed." "Exactly one functional panel instance." "No runtime creation of panel hierarchy or equipment slots." "No duplicated presenters or event handlers." "Preserve the approved Stage 4A visual changes." "Preserve the Stage 4B controller and presentation architecture." "Use Unity-native prefab/scene editing." "If direct Editor control is unavailable, supply an idempotent Editor menu command that performs the required wiring and validates references." "Do not hand-edit scene or prefab YAML when Unity-native authoring is available." "Never overwrite unrelated user changes."
S4. "Use the existing `UIModal.DailyLoadout` ownership introduced during Stage 4B." "F opens the panel once." "Clicking Cancel/Close dismisses it." "Escape dismisses it and consumes Escape appropriately." "Confirming does not leave the interaction in a blocked state." "Leaving interaction range does not create stale prompts or references." "Closing restores normal gameplay input." "Night transition safely closes the panel." "Game reset, disable and destruction clean up modal ownership and subscriptions." "Other game modals cannot overlap with Daily Loadout." "Provide a dedicated, correctly scoped Daily Loadout completion notification or equivalent direct callback." "The interaction must return to its idle state after the panel closes." "Do not fire restoration close events to simulate Daily Loadout completion." "Avoid changing behavior of unrelated interactables."
S5. "Keep these systems functional and their original business rules unchanged:" "HouseRestorationSystem" "House health restoration" "Restoration costs and inventory resource consumption" "Restoration daily limit" "Healing animations and visual assets" "The existing Restoration UI may remain disconnected from this tree for now." "Do not delete restoration components, data or prefabs merely because their previous entry point is replaced." "Avoid unnecessary renaming of serialized MonoBehaviour classes, since that could break existing prefab references."
S6. "The existing tutorial has restoration-specific objectives:" "12-FindRestorationAltar" "13-OpenRestoration" "Inspect all associated tutorial subscriptions and serialized objectives." "Implement the smallest coherent adaptation so the player can:" "1. Reach the existing Sacred Tree." "2. Open the Daily Loadout UI." "3. Complete the relevant tutorial objective through the correct Loadout interaction." "Do not emit `TutorialEvents.InvokeRestorationOpened/Closed()` when opening or closing the Loadout." "If new tutorial events or objective types are required, append compatible enum values without renumbering existing serialized values." "Keep tutorial text consistent with Loadout configuration rather than claiming the player healed the tree." "Do not redesign unrelated tutorial steps or the restoration mechanic." "Document any remaining narrative/design decision separately."
S7. "Do not modify:" "SeedInventory or SpellInventory storage behavior." "DailyLoadoutState or DailyLoadoutSession contracts unless a genuine integration blocker is proven." "EquipmentAdmissionPolicy." "Planting or spell casting rules." "HUD Seed Slots or HUD Spell Slots." "Numeric key mapping." "Existing equipment bootstrap/balance decisions." "Wave, crafting, combat or day/night systems unrelated to UI access." "No new packages or paid plugins."
S8. "Add focused automated tests for:" "Valid daytime interaction opens Daily Loadout." "Attempted nighttime interaction is rejected." "Read-only reopening after confirmation." "Double interaction does not open duplicate panels." "Other modal open prevents interaction." "Cancel/Close releases interaction and modal state." "Escape closes exactly once." "Night transition closes correctly." "Interaction prompt restores correctly." "Tutorial can progress through the new interaction." "No false restoration tutorial events." "Existing restoration logic remains unchanged." "Scene and prefab references are valid." "Session discovery works under real scene initialization order." "Run relevant EditMode and PlayMode suites, including Stage 4B regressions." "Manually verify the complete F-interaction flow in the gameplay scene." "Do not claim native runtime verification if tests could not run."
S9. "Stage 4C is complete when:" "The tree opens the functional Daily Loadout panel." "The panel shows real unlocked equipment." "Draft editing and daily confirmation work through the actual interactable." "Closing reliably restores interaction and input." "The old restoration panel no longer opens from this tree." "Restoration business logic is preserved." "The tutorial cannot become blocked at the replaced interaction." "No gameplay HUD changes are introduced." "Unity tests and the gameplay interaction checklist pass." "Report files modified, prefab/scene wiring, tutorial adjustments, tests executed, failures and remaining risks." "Do not start Stage 4D." "Do not commit or push."

## Tasks
- T1 [S1-S5,S7-S8] blocked — inline runtime and interaction/regression test source written; static runtime assembly compiled. Waits for owner-run Unity suites and gameplay flow to prove lifecycle. No native RED/GREEN. Commit prohibited by S9.
- T2 [S3,S5-S8] blocked — inline native authoring/menu, compatible tutorial routing, asset preservation tests, natural-Start PlayMode tests and Stage4C.md written; static Editor/support/test assemblies compiled. Waits for owner execution of Wire Sacred Tree and installed scene/tutorial validation. Actual scene/prefab/tutorial assets unchanged. Commit prohibited by S9.
- T3 [S8-S9] done — static five-assembly final compilation and strengthened legacy-test assembly compile passed; scoped whitespace/preservation checks and independent source verifier completed. Native suites/manual proof remain explicitly blocked in T1/T2; no Stage4C completion claim. Commit prohibited by S9.

## Log
L1 — Original request (verbatim):

# Daily Loadout V2 — Stage 4C: World Interactable Integration

## Context

Project: Magic Garden
Unity: 2022.3.62f3
Branch: `feature/daily-loadout-v2`

Stages 1–4B have been implemented and committed locally.

Stage 4A provides the approved UI prefabs. Stage 4B provides the functional session-backed presenter.

Use the current local working tree as the source of truth, including the latest Stage 4B commit. Do not require a push.

## Goal

Connect the existing Restoration Altar / Sacred Tree interactable to the Daily Loadout panel.

The existing tree must open the Daily Loadout UI instead of the house restoration UI.

Retain the original restoration logic and assets for potential future use.

Do not modify the gameplay HUD. That belongs to Stage 4D.

## 1. Inspect before modifying

Inspect current local implementations of:

- `RestorationAltar`
- `InteractionTrigger`
- `DailyLoadoutPresenter`
- `DailyLoadoutPanelView`
- `UIFlowController`
- `UIEvents`
- `UIManager`
- `RestorationAltarUIManager`
- `HouseRestorationSystem`
- `TutorialManager`
- `TutorialEvents`
- `TutorialStep`
- `TutorialProximityTrigger`

Also inspect the current `Game` scene, the tree/altar prefab, and `Canvas Game UI.prefab`.

Identify exactly how Stage 4B handles opening, closing, modal ownership, session discovery and night transitions.

Reuse those contracts rather than creating parallel controllers.

## 2. Tree interaction

Preserve the existing physical interactable, collider, interaction range, prompt and input key F.

When the player interacts during the Day:

- Open the actual Stage 4B Daily Loadout panel.
- Resolve the active player's current loadout session.
- Display current assignments and available content.
- Permit editing only through existing session rules.
- Allow read-only inspection after the daily confirmation has been consumed.

Do not reject interaction merely because the player has already confirmed that day.

The tree must not open the restoration panel.

During Night, GameOver, ritual, pause or another blocking state, reject opening through the appropriate existing contracts.

Do not create another interactable or new global singleton.

## 3. UI integration

Use the existing `DailyLoadoutPanel.prefab`.

Integrate a persistent prefab instance under the appropriate gameplay UI Canvas.

Requirements:

- Correct serialized references.
- Initially closed.
- Exactly one functional panel instance.
- No runtime creation of panel hierarchy or equipment slots.
- No duplicated presenters or event handlers.
- Preserve the approved Stage 4A visual changes.
- Preserve the Stage 4B controller and presentation architecture.

Use Unity-native prefab/scene editing.

If direct Editor control is unavailable, supply an idempotent Editor menu command that performs the required wiring and validates references.

Do not hand-edit scene or prefab YAML when Unity-native authoring is available.

Never overwrite unrelated user changes.

## 4. Modal and interaction lifecycle

Use the existing `UIModal.DailyLoadout` ownership introduced during Stage 4B.

Verify these cases:

- F opens the panel once.
- Clicking Cancel/Close dismisses it.
- Escape dismisses it and consumes Escape appropriately.
- Confirming does not leave the interaction in a blocked state.
- Leaving interaction range does not create stale prompts or references.
- Closing restores normal gameplay input.
- Night transition safely closes the panel.
- Game reset, disable and destruction clean up modal ownership and subscriptions.
- Other game modals cannot overlap with Daily Loadout.

Important: `InteractionTrigger` currently listens to restoration/crafting closure notifications.

Provide a dedicated, correctly scoped Daily Loadout completion notification or equivalent direct callback.

The interaction must return to its idle state after the panel closes.

Do not fire restoration close events to simulate Daily Loadout completion.

Avoid changing behavior of unrelated interactables.

## 5. Preserve restoration

Keep these systems functional and their original business rules unchanged:

- `HouseRestorationSystem`
- House health restoration
- Restoration costs and inventory resource consumption
- Restoration daily limit
- Healing animations and visual assets

The existing Restoration UI may remain disconnected from this tree for now.

Do not delete restoration components, data or prefabs merely because their previous entry point is replaced.

Avoid unnecessary renaming of serialized MonoBehaviour classes, since that could break existing prefab references.

## 6. Tutorial migration

The existing tutorial has restoration-specific objectives:

- `12-FindRestorationAltar`
- `13-OpenRestoration`

The second depends on restoration completion events. Replacing the tree's UI without addressing this would potentially block tutorial progression.

Inspect all associated tutorial subscriptions and serialized objectives.

Implement the smallest coherent adaptation so the player can:

1. Reach the existing Sacred Tree.
2. Open the Daily Loadout UI.
3. Complete the relevant tutorial objective through the correct Loadout interaction.

Do not emit `TutorialEvents.InvokeRestorationOpened/Closed()` when opening or closing the Loadout.

If new tutorial events or objective types are required, append compatible enum values without renumbering existing serialized values.

Keep tutorial text consistent with Loadout configuration rather than claiming the player healed the tree.

Do not redesign unrelated tutorial steps or the restoration mechanic.

Document any remaining narrative/design decision separately.

## 7. Scope restrictions

Do not modify:

- SeedInventory or SpellInventory storage behavior.
- DailyLoadoutState or DailyLoadoutSession contracts unless a genuine integration blocker is proven.
- EquipmentAdmissionPolicy.
- Planting or spell casting rules.
- HUD Seed Slots or HUD Spell Slots.
- Numeric key mapping.
- Existing equipment bootstrap/balance decisions.
- Wave, crafting, combat or day/night systems unrelated to UI access.

No new packages or paid plugins.

## 8. Verification

Add focused automated tests for:

- Valid daytime interaction opens Daily Loadout.
- Attempted nighttime interaction is rejected.
- Read-only reopening after confirmation.
- Double interaction does not open duplicate panels.
- Other modal open prevents interaction.
- Cancel/Close releases interaction and modal state.
- Escape closes exactly once.
- Night transition closes correctly.
- Interaction prompt restores correctly.
- Tutorial can progress through the new interaction.
- No false restoration tutorial events.
- Existing restoration logic remains unchanged.
- Scene and prefab references are valid.
- Session discovery works under real scene initialization order.

Run relevant EditMode and PlayMode suites, including Stage 4B regressions.

Manually verify the complete F-interaction flow in the gameplay scene.

Do not claim native runtime verification if tests could not run.

## Definition of Done

Stage 4C is complete when:

- The tree opens the functional Daily Loadout panel.
- The panel shows real unlocked equipment.
- Draft editing and daily confirmation work through the actual interactable.
- Closing reliably restores interaction and input.
- The old restoration panel no longer opens from this tree.
- Restoration business logic is preserved.
- The tutorial cannot become blocked at the replaced interaction.
- No gameplay HUD changes are introduced.
- Unity tests and the gameplay interaction checklist pass.

## Delivery

Report files modified, prefab/scene wiring, tutorial adjustments, tests executed, failures and remaining risks.

Do not start Stage 4D.

Do not commit or push.

L4 — Source progress only: tree now calls UIManager's serialized Stage4B presenter; optional IInteractionLifecycle provides initiator-only completion without restoration notifications. Presenter exposes one-shot Closed and consumed Escape dismissal. UIManager/reset close its assigned panel without changing standalone session reset behavior. Dedicated tutorial open/proximity events and append-only enum values added. Unity-native menu source configures nested panel and migrates existing tutorial assets without YAML changes. Tests cover interactions, authoring preservation and natural owner Start discovery. These sources have NOT yet been compiled/executed. Existing assets and user dirty files remain unchanged.
L6 — Checks: first static compile exited1 in Editor assembly: Unity2022 lacks EditorSceneManager.OpenPreviewScene. Corrected to additive OpenScene, close only when opened here, restore active scene. Corrected five-assembly compile exited0 (Temp/Stage4C-check/compile-corrected.log); this is not native Unity execution. Scope preservation probe/diff --check exited0: original Game/tree/Canvas/panel and tutorial assets unchanged, restoration/health/UI/Equipment/Loadout/HUD sources untouched. Native assess returned unassessable because untracked declarations are required; no reviewDue/start route was returned, no lineage created; independent verification required. New tests total 20 cases across three classes, not yet counted/executed natively.
L5 — User instruction (verbatim): "The todo plan has not been reviewed during several tool-use turns. Check whether it still matches the work performed and update tasks where needed. Do not mark work done without verification." Reconciled task file/projection; no implementation task is done.
L2 — Baseline: 375ecf9d on feature/daily-loadout-v2. Existing dirty ProjectSettings/ProjectSettings.asset and TMP LiberationSans SDF fallback asset are unrelated and must remain unchanged. Read-only exploration mapped all requested components/assets; presenter owns modal/session resolution, session starts in player owner Start. Existing reset behavior refreshes open Stage4B panel; Stage4C composition must dismiss its interaction on reset without breaking standalone presenter reset regression.
L3 — Unity status --json exited 6, no Pipeline endpoints. Live Editor PID12136 owns Menu scene. No competing batch Editor, restart, plugin install, YAML edits, native RED/GREEN or manual runtime claim. Use idempotent Editor menu fallback authorized by S3. Source tests first, static compilation where possible; native execution pending. Route inline because units depend on runtime integration, background subagents off; explorer supplied mapping. Feature is large because native authoring and owner-controlled runtime proof are separate resumable deliverables.

L7 — Independent verifier mvd95sbs-2-t5mo: no severe concrete change-caused source defect. S1/S7 met structurally; S2/S4/S5 source-covered but runtime proof absent; S3/S6 fallback authored but native installation/migration pending; S8/S9 not complete. Baseline probes verified append-only enums, balanced subscriptions, seven unchanged assets, original Canvas/tree scene links and excluded-source preservation. Natural owner Start test is genuine within fixture-composed dependencies, not full Game startup evidence.
L8 — Added real TutorialUI/manager scheduled next-step deferral/resume PlayMode regression after open-objective sequencing concern; it uses actual altar open, waits with modal open, Cancel, production Update fallback and next Wait completion, without private buffer calls. Strengthened legacy crafting regression to use actual TryInteract, prompt hiding, duplicate rejection, closure and range exit instead of seeding isInteracting. Final five-assembly static compile exited0 (compile-final.log); final focused test-assembly compilation exited0 (compile-legacy-check.log). Twenty new cases remain natively unexecuted. Parent scoped diff/new-file whitespace checks exited0. No prefab/scene/tutorial asset authoring was executed, no native tests/manual keyboard verification were executed, no commit or push.
L9 — Needs your decision: owner must run Wire Sacred Tree (Stage 4C), Validate Sacred Tree, applicable EditMode/PlayMode suites including Stage4B and the Stage4C.md gameplay checklist. Current Canvas has no installed dailyLoadout reference until that menu runs, so the tree currently fails closed rather than opening an unbound panel. Native review assessment was unavailable (untracked declaration required), no review lineage or approval exists. Risk: item3 shared InteractionTrigger/tutorial compatibility; native regression proof pending. Full-memory mirror synchronization follows this final local record.
