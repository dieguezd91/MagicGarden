# Daily Loadout V2 — Stage 4B
Objective: connect the existing panel to the run-owned session without changing Stage 4A visuals. Branch: `feature/daily-loadout-v2`.
Delivery: user subsequently authorized a local commit in L16; no push or next stage. Runner: Unity 2022.3.62f3 EditMode/PlayMode; live Editor currently owns the project.

## Specs
S1. "Inspect the current working tree, including the user's latest prefab adjustments." "Inspect prefab references before modifying any UI assets." "Use the current local working tree as the source for uncommitted changes. Preserve unrelated work."
S2. "Display unlocked plants and spells." "Preserve deterministic catalog order." "Use `PlantDataSO` and `SpellDataSO` for icons and names." "Preserve stable equipment identities." "Never represent equipment assignments using inventory indices." "Do not grant seeds or spells when populating the UI." "Never enumerate unordered unlock collections to establish display order."
S3. "The main table contains three plant positions and three spell positions." "Click an equipment position to select it." "Display the unlocked options belonging to that category." "Click an option to assign it to the selected draft position." "Use Clear to empty the selected position."
S4. "Use `DailyLoadoutSession.TrySetDraftPlant/Spell`." "Empty positions are valid." "Reject duplicate identities within the same category." "Never modify committed equipment while editing." "Do not implement implicit assignment swaps." "Rejected operations must preserve the existing draft and display an appropriate state." "Use the existing presentation views and button references."
S5. "Use `DailyLoadoutSession.TryConfirm()` as the sole authority." "Commit all six draft positions atomically." "Display the confirmed configuration." "Disable further editing for that day." "Show the daily confirmation allowance as consumed." "Keep the current draft." "Do not report success." "Do not consume the daily allowance." "Closing or cancelling without confirmation must discard the draft through the existing session API." "Do not add a second confirmation counter or separate equipment state."
S6. "Editing is allowed only when `session.CanConfigure` is true." "After daily confirmation, the panel may display committed equipment in read-only mode." "If the night begins while the panel is open, safely close it." "Preserve the existing session behavior for unconfirmed drafts." "A new genuine day restores the ability to edit without requiring reconfirmation of the previous configuration."
S7. "Implement a focused presenter/controller." "Resolve or receive the existing session." "Bind and unbind UI callbacks safely." "Refresh slot content and available options." "Handle selected category and position." "Handle Confirm, Clear and Cancel." "Release UI state when disabled or destroyed." "Do not create a new singleton." "Use the existing UI modal ownership mechanism where appropriate. Prevent modal conflicts and duplicate subscriptions."
S8. "Do not build the panel, slots or layout hierarchy dynamically during gameplay. Reuse serialized prefab instances; if more option views are necessary, configure them through Editor tooling and keep the catalog capacity explicit." "Do not redesign or regenerate the Stage 4A prefabs."
S9. "Do not modify:" "RestorationAltar" "HouseRestorationSystem" "Restoration tutorial events" "Game scene interactable wiring" "Existing gameplay HUD" "Day/night numeric input routing" "Planting or casting admission" "SeedInventory or SpellInventory storage logic" "Equipment progression and initial bootstrap rules" "The actual altar interaction and HUD adaptation belong to the next integration stage."
S10. "Create focused EditMode and PlayMode tests covering:" "Correct catalog population and ordering." "Assignment by identity." "Duplicate rejection." "Empty positions and Clear." "Draft/committed separation." "Cancel/discard." "Atomic confirmation." "Confirmation failure." "Daily confirmation lock." "Night transition while the panel is open." "New-day reconfiguration." "Safe event subscription and cleanup." "Run reset and unavailable session behavior." "Existing prefab reference integrity."
S11. "Provide an isolated PlayMode test harness that can open and operate the panel without changing the Game scene or RestorationAltar." "Run all relevant tests when Unity execution is available." "If the Editor cannot be controlled, report exactly which tests remain unexecuted rather than claiming success."
S12. "Stage 4B is complete when:" "The real unlocked catalog is displayed." "The six draft positions can be edited." "Clear and Cancel work." "Confirmation commits correctly." "Read-only and daily allowance states work." "No restoration or gameplay systems regress." "Automated tests pass in Unity." "Report modified files, tests, limitations and remaining integration work." "Do not commit or push." "Do not start the next stage."

## Tasks
- T1 [S1–S11] blocked — runtime, harness, docs and 25 focused test cases written; current local prefab now contains the bound presenter and six option references. Waits for passing owner-controlled Unity EditMode/PlayMode tests; four failures reported in L11. Prior static compilation passed; no agent-observed Unity RED/GREEN. Commit prohibited by S12.
- T2 [S9–S12] done — available verification only: independent source/scope review and limited pointer correction recheck, five-assembly static compilation, metadata/asmdef/whitespace checks, 3/3 baseline-identical prefabs. Runtime proof remains blocked under T1. No commits.
- T3 [S11–S12] done — recorded prior source/check outcomes and native blockers; superseded binding status reconciled in L12. Stage 4B remains incomplete under T1; no commits/push or next-stage changes.
- T4 [S7–S8,S10–S11] blocked — inline source correction complete in three test files; five-assembly static compile and scoped structural checks passed. Awaits owner-run Unity reruns of cleanup EditMode, all four layout resolutions and actual PlayMode disable/destroy test. Current prefab/ProjectSettings bytes preserved; no native reproduction or GREEN claimed. No commits.

## Log
L1 — Original request (verbatim):
# Daily Loadout V2 — Stage 4B: Functional UI Integration

## Context

Repository: Proyecto-Final
Working branch: `feature/daily-loadout-v2`

Stage 4A is committed and its generated prefabs have been visually reviewed and adjusted by the user.

The reference design is `Magic Garden Desing Doc`, page 2, LOADOUT section.

## Goal

Connect the existing Daily Loadout panel to the real equipment session.

The player must be able to inspect unlocked plants/spells, edit six draft assignments, clear slots, discard changes, and confirm the Loadout once per day.

Preserve the Stage 4A visual design and prefab hierarchy.

## Before implementation

Inspect the current working tree, including the user's latest prefab adjustments.

Read:

- `DailyLoadoutPanelView`
- `LoadoutSlotView`
- `LoadoutOptionView`
- `DailyLoadoutSession`
- `DailyLoadoutState`
- `PlayerContentUnlockSystem`
- `PlayerExperienceSystem`
- `UIFlowController`

Inspect prefab references before modifying any UI assets.

Use the current local working tree as the source for uncommitted changes. Preserve unrelated work.

## Functional requirements

### 1. Available equipment

Populate the UI from the existing progression catalog and unlock state.

- Display unlocked plants and spells.
- Preserve deterministic catalog order.
- Use `PlantDataSO` and `SpellDataSO` for icons and names.
- Preserve stable equipment identities.
- Never represent equipment assignments using inventory indices.
- Do not grant seeds or spells when populating the UI.
- Never enumerate unordered unlock collections to establish display order.

### 2. Editing

The main table contains three plant positions and three spell positions.

Interaction:

1. Click an equipment position to select it.
2. Display the unlocked options belonging to that category.
3. Click an option to assign it to the selected draft position.
4. Use Clear to empty the selected position.

Rules:

- Use `DailyLoadoutSession.TrySetDraftPlant/Spell`.
- Empty positions are valid.
- Reject duplicate identities within the same category.
- Never modify committed equipment while editing.
- Do not implement implicit assignment swaps.
- Rejected operations must preserve the existing draft and display an appropriate state.

Use the existing presentation views and button references.

### 3. Confirmation

Use `DailyLoadoutSession.TryConfirm()` as the sole authority.

On success:
- Commit all six draft positions atomically.
- Display the confirmed configuration.
- Disable further editing for that day.
- Show the daily confirmation allowance as consumed.

On failure:
- Keep the current draft.
- Do not report success.
- Do not consume the daily allowance.

Closing or cancelling without confirmation must discard the draft through the existing session API.

Do not add a second confirmation counter or separate equipment state.

### 4. Day/night behavior

- Editing is allowed only when `session.CanConfigure` is true.
- After daily confirmation, the panel may display committed equipment in read-only mode.
- If the night begins while the panel is open, safely close it.
- Preserve the existing session behavior for unconfirmed drafts.
- A new genuine day restores the ability to edit without requiring reconfirmation of the previous configuration.

### 5. UI lifecycle

Implement a focused presenter/controller.

Responsibilities:

- Resolve or receive the existing session.
- Bind and unbind UI callbacks safely.
- Refresh slot content and available options.
- Handle selected category and position.
- Handle Confirm, Clear and Cancel.
- Release UI state when disabled or destroyed.

Do not create a new singleton.

Use the existing UI modal ownership mechanism where appropriate. Prevent modal conflicts and duplicate subscriptions.

Do not build the panel, slots or layout hierarchy dynamically during gameplay. Reuse serialized prefab instances; if more option views are necessary, configure them through Editor tooling and keep the catalog capacity explicit.

### 6. Scope exclusions

Do not modify:

- RestorationAltar
- HouseRestorationSystem
- Restoration tutorial events
- Game scene interactable wiring
- Existing gameplay HUD
- Day/night numeric input routing
- Planting or casting admission
- SeedInventory or SpellInventory storage logic
- Equipment progression and initial bootstrap rules

The actual altar interaction and HUD adaptation belong to the next integration stage.

Do not redesign or regenerate the Stage 4A prefabs.

### 7. Verification

Create focused EditMode and PlayMode tests covering:

- Correct catalog population and ordering.
- Assignment by identity.
- Duplicate rejection.
- Empty positions and Clear.
- Draft/committed separation.
- Cancel/discard.
- Atomic confirmation.
- Confirmation failure.
- Daily confirmation lock.
- Night transition while the panel is open.
- New-day reconfiguration.
- Safe event subscription and cleanup.
- Run reset and unavailable session behavior.
- Existing prefab reference integrity.

Provide an isolated PlayMode test harness that can open and operate the panel without changing the Game scene or RestorationAltar.

Run all relevant tests when Unity execution is available.

If the Editor cannot be controlled, report exactly which tests remain unexecuted rather than claiming success.

## Completion criteria

Stage 4B is complete when:

- The real unlocked catalog is displayed.
- The six draft positions can be edited.
- Clear and Cancel work.
- Confirmation commits correctly.
- Read-only and daily allowance states work.
- No restoration or gameplay systems regress.
- Automated tests pass in Unity.

Report modified files, tests, limitations and remaining integration work.

Do not commit or push.
Do not start the next stage.

L2 — Exploration: clean working tree on requested branch; c35d0984 baseline includes customized prefabs. Session and state rules will remain unchanged. Inline route: single tightly coupled runtime integration, no independent writer units. Independent verifier required for modal/lifecycle regression risk. Reference document not retrievable; existing user-reviewed prefab is the visual authority. Forecast approximately 1000–1400 authored lines including isolated fixtures; no PR/delivery requested.
L3 — Environment: Unity 2022.3.62f3 Editor PID 12136 already owns this project (Menu scene). Do not terminate it, change its scene, or launch a competing Editor. Test-first exception: write focused tests but do not claim observed RED/GREEN without Unity execution.
L4 — Unity CLI capability check: `Unity status` returned no connected editors. `Unity recompile --project-path C:/Users/diegu/OneDrive/Documentos/GitHub/Proyecto-Final/Proyecto-Final --timeout 60` exited 7: `No Pipeline instance found for project`. No automatic retry, bridge installation or Editor restart. Native prefab binding and Unity tests remain unexecuted. Additive authoring and an isolated unsaved harness are provided as explicit menu commands, never import-time mutations. Tasks consolidated to keep tightly coupled implementation and its tests together; all remain unverified.
L5 — Engram initially stored locator/status only; full-document mirror is refreshed at handoff. Local feature document remains authoritative.
L6 — All runtime, Editor, fixture and new EditMode/PlayMode test assemblies compiled successfully with direct Roslyn against the existing Unity project references (exit 0). This is static compilation, not Unity import or test execution. Generated response files/DLLs/logs stay in ignored `Proyecto-Final/Temp/Stage4B-check/`. Final compile follows small source/test refinements.
L7 — Native assessment returned `unassessable`: intended-untracked declaration required. Inspect found no existing authority and requested selection. No START or review transaction created; implementation remains partial awaiting native binding/test proof. Candidate treated as high for independent verification. All source changes remain uncommitted per explicit instruction.
L8 — Independent verifier inspected the full specification, source, fixtures and compile logs. Confirmed 3/3 prefabs byte-identical to c35d0984, excluded surfaces untouched and no additional severe source issue. Native binding/test execution and natural startup/discovery proof remain missing. Source-chain finding: per-frame unchanged availability reset hover/press. Corrected availability setters to reset pointer only on actual transitions; added two focused pointer cases. Limited recheck confirmed the source interaction corrected, without claiming native execution.
L9 — Final correction static compile exited 0 for runtime, Editor, support, EditMode and PlayMode assemblies. Structural command exited 0: metadata present for all new assets, UI test asmdefs parse, prefab bytes match baseline, excluded surfaces unchanged, git diff --check passed (CRLF conversion warnings only). Twenty-five new test cases remain UNEXECUTED: DailyLoadoutPresenterTests (16), DailyLoadoutFunctionalPrefabTests (2), DailyLoadoutPointerPersistenceTests (2), DailyLoadoutPresenterPlayModeTests (5). Existing DailyLoadoutUITests and Loadout/Equipment/LoadoutBaseline EditMode/PlayMode regressions also UNEXECUTED.
L10 — Owner action: allow Editor compilation; run Tools/Magic Garden/Daily Loadout/Bind functional UI (preserve visuals), review the additive prefab diff, use Open isolated functional harness and run Unity test suites. ProductionPrefabHasBoundPresenterAndExplicitCatalogCapacity is expected to fail against the currently unbound production panel. No source startup-order proof or visual validation claimed. Risk: item 3 (shared UI availability setters/modal consumers); native regression coverage pending. Stage 4B is not complete. Actual altar interaction, Game wiring and HUD adaptation remain excluded.

L11 — User-reported Unity failures (verbatim):
ReopenIsIdempotentAndDisableUnbindsOwnedCallbacksOnly (0,034s)
---
Expected: True
  But was:  False
---
at DailyLoadoutPresenterTests.ReopenIsIdempotentAndDisableUnbindsOwnedCallbacksOnly () [0x0009c] in C:\Users\diegu\OneDrive\Documentos\GitHub\Proyecto-Final\Proyecto-Final\Assets\Tests\Editor\DailyLoadoutUI\DailyLoadoutPresenterTests.cs:181

LayoutFitsScaledCanvasAndAdditionalOptionsScroll(1920,1080) (0,107s)
---
Expected: greater than or equal to -727.528076f
  But was:  -916.528076f
---
at DailyLoadoutUITests.LayoutFitsScaledCanvasAndAdditionalOptionsScroll (System.Int32 width, System.Int32 height) [0x001d7] in C:\Users\diegu\OneDrive\Documentos\GitHub\Proyecto-Final\Proyecto-Final\Assets\Tests\Editor\DailyLoadoutUI\DailyLoadoutUITests.cs:172

LayoutFitsScaledCanvasAndAdditionalOptionsScroll(1280,720) (0,047s)
---
Expected: greater than or equal to -727.528076f
  But was:  -916.528076f
---
at DailyLoadoutUITests.LayoutFitsScaledCanvasAndAdditionalOptionsScroll (System.Int32 width, System.Int32 height) [0x001d7] in C:\Users\diegu\OneDrive\Documentos\GitHub\Proyecto-Final\Proyecto-Final\Assets\Tests\Editor\DailyLoadoutUI\DailyLoadoutUITests.cs:172

LayoutFitsScaledCanvasAndAdditionalOptionsScroll(2560,1440) (0,044s)
---
Expected: greater than or equal to -727.528076f
  But was:  -916.528076f
---
at DailyLoadoutUITests.LayoutFitsScaledCanvasAndAdditionalOptionsScroll (System.Int32 width, System.Int32 height) [0x001d7] in C:\Users\diegu\OneDrive\Documentos\GitHub\Proyecto-Final\Proyecto-Final\Assets\Tests\Editor\DailyLoadoutUI\DailyLoadoutUITests.cs:172

L12 — Resume reconciliation: current local panel now contains DailyLoadoutPresenter and six serialized inactive OptionViews. User also changed ProjectSettings; both are preserved. Unity status returned no endpoints (exit 6); no native reproduction or rerun possible through available channel. No claim that failures already work. Read-only explorer mapped ordinary-MonoBehaviour EditMode disable dispatch and inactive pooled transforms measured as grid participants. Source hypotheses, not executed reproductions. Correct fixtures only: explicit OnDisable dispatch in EditMode, actual disable/cleanup coverage in PlayMode, activate every authored pool option and overflow preview before layout assertions. No production/visual changes planned. Local pre-correction snapshots in ignored Temp/Stage4B-check/reported-failures/. Task mirror refreshed after correction evidence.
L13 — Correction source: EditMode cleanup now explicitly invokes OnDisable after SetActive(false), matching the manually initialized fixture; actual engine dispatch remains covered without manual callbacks in the strengthened PlayMode test. Both exercise three reopen/disable cycles, edited draft discard, unchanged committed plant, modal release, preserved external Clear callback and consumed-allowance nonmutation. PlayMode also checks destruction discards a fresh edit. Layout activates and measures every serialized pooled option, adds previews until at least24 unique visible items, and retains original horizontal/vertical bounds, mask, overflow and slot checks for all four resolution cases. No runtime source, prefab or ProjectSettings changes made.
L14 — Checks: direct Roslyn compilation of five assemblies passed (exit0), evidence Temp/Stage4B-check/reported-failures/compile.log. Full-tree git diff --check failed (exit2) on trailing whitespace in the user-bound prefab's serialized empty fields; deliberately not cleaned. Correction-path whitespace check passed (exit0). Structural probe passed (exit0): current panel SHA256 510531e992b6d619fbcedcaf9fb1284c276230b7d1dc54b58c9e312e9cd396f2 and ProjectSettings SHA256 67f72149364bd3930b01fec4a01e1ec6d5015f45f2d3884a0b420a88480547d8 match pre-correction snapshots; all test declarations/cases retained; bounds/overflow assertions preserved; PlayMode still uses actual disable/destroy dispatch. These are source/compile checks, NOT native reproductions or passing Unity tests.
L15 — Needs owner-controlled Unity rerun: ReopenIsIdempotentAndDisableUnbindsOwnedCallbacksOnly; LayoutFitsScaledCanvasAndAdditionalOptionsScroll at 1920x1080,1280x720,2560x1440,1024x768; DisableAndDestroyReleaseSubscriptionsWithoutTouchingAnotherModal; then applicable full UI/session/regression suites. All reruns remain unexecuted by agent. Current local binding is present; do not rebind/regenerate to fix these tests. Stage4B remains incomplete, no commits/push. Risk for T4: none (only tests/docs changed; no production helper or contract changed in this correction).
L16 — User authorization (verbatim): "perfecto hace el commit". Authorizes committing the current Stage4B implementation, owner-bound panel, tests and docs on feature/daily-loadout-v2. Exclude unrelated ProjectSettings.asset and TextMesh Pro fallback-font changes; preserve both in the working tree. No push authorized. Passing Unity reruns have not been supplied, so verification blockers remain open; commit does not certify Stage4B completion.
