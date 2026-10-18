# Daily Loadout V2 — Stage 4D
Objective: empty new-run equipment, immediate editor catalog, committed-equipment HUD and controls. Branch: feature/daily-loadout-v2.
Delivery: local feature commit authorized; no push. Runner: Unity 2022.3.62f3; native Editor CLI endpoint unavailable.

## Specs
S1. "All three plant positions must be empty." "All three spell positions must be empty." "Initially unlocked plants and spells must remain available in the catalog." "Unlocking content must never automatically equip it." "Do not assign default items during bootstrap." "Do not grant or remove inventory resources when editing equipment." "Update the state/session initialization contract and affected tests coherently." "One confirmation per day." "Atomic confirmation." "Explicit empty positions." "Unique identities within each category." "Discarding unconfirmed changes." "Previously committed equipment across consecutive days." "Empty initial equipment again after a complete Run reset."
S2. "On opening the panel, automatically select Plant 1 as the editing target." "Immediately display all unlocked plant options." "Selecting a spell position switches the catalog to spells." "Selecting a plant position switches back to plants." "Make the active category and target position obvious." "Always show unlocked items, including currently assigned ones." "Mark options already assigned to another position." "Prevent duplicate assignment without silently moving equipment." "Do not display \"No available options\" merely because everything is already assigned." "Keep the selected position highlighted." "Clear affects only the selected position." "Selecting one item must never populate another position." "Preserve the existing approved stone-table assets and panel layout." "elimina el feedback de highligthed en los textos de las opciones, solo tiene que mostrarse si una opcion esta disponible o esta seleccionada"
S3. "Reuse the existing Seed Slots and Spell Slots HUD assets." "Show exactly three equipment positions per category." "Initial display: six empty positions." "Display assignments from the committed Loadout only." "Draft editing must not change the HUD." "Refresh immediately after successful confirmation." "Empty positions remain visible." "Unequipped unlocked content must not appear." "Do not replace underlying inventory data with equipment arrays." "Resolve equipment identities against actual inventory entries." "Preserve real seed quantities and spell cooldowns." "A plant with zero stock must retain its assigned identity and display quantity zero." "el inventario de semillas solo debe verse durante el dia, durante la noche se tiene que ver el inventario de hechizos, no deben superponerse uno con otro." "Preserve the game's existing pixel-art design, layout and relevant UI animations." "Do not create a new HUD from scratch."
S4. "Day: number keys 1–3 select equipped plant positions." "Night: number keys 1–3 select equipped spell positions." "Empty equipment positions must never activate unrelated inventory entries." "Clicking a hotbar slot must follow the same equipment mapping." "Preserve appropriate Q/E category cycling." "Keep gameplay admission as the final authorization boundary." "Do not bypass existing seed stock, cooldown or mana validation." "Remove the obsolete assumption that daytime hotbar positions correspond to raw storage slots."
S5. "Unlocked content catalog" "Committed and draft Loadout" "Underlying SeedInventory / SpellInventory" "HUD presentation" "Gameplay selection and admission" "Avoid duplicate sources of truth, new singletons and LINQ in runtime update paths." "Do not modify restoration business logic or unrelated game mechanics." "Use Unity-native editing for prefab/scene changes. Preserve existing prefab GUIDs and visual adjustments."
S6. "Add and update tests covering:" "Empty Loadout at initial Run start." "Initial catalog still contains unlocked content." "One-item Loadout with remaining positions empty." "Confirming a partially filled Loadout." "No automatic equipment grants on unlock." "Confirmed-only HUD updates." "Cancel leaves the HUD unchanged." "Correct day/night equipment display." "Empty slot selection." "Input and clicked slot mapping." "Correct seed count and cooldown presentation." "New-day persistence." "New-Run reset." "Existing Stage 4B/4C integration regression tests."
S7. "Perform a manual test in Game.unity:" "1. Start a new Run." "2. Confirm the equipment HUD is empty." "3. Open the tree's Loadout editor." "4. Verify the two initial plant options are immediately visible." "5. Equip only one plant and one spell." "6. Confirm." "7. Verify that only those two elements appear in their respective HUD positions." "8. Test their use during the correct phases." "9. Verify the remaining slots stay empty." "Report:" "Files changed." "Initial equipment behavior." "HUD and input changes." "Unity tests executed and results." "Manual checks completed." "Any remaining limitations." "Do not commit or push automatically."

S8. "perfecto hace el commit" "elimina las herramientas que ya no se vayan a usar"

## Tasks
- T1 [S1,S5-S6] blocked - empty state/session initialization and committed notification implemented; ten-assembly compile and 19/19 exact pure-model Mono tests passed. Await owner-run native session/reset integration tests; no Editor endpoint. No commit authorized.
- T2 [S2,S6] blocked - Plant1/full unlocked catalog/target UX and Stage4B/C fixtures adapted and compiled. Await owner-run Unity UI/lifecycle regressions. No commit authorized.
- T3 [S3-S6] blocked - committed three-position HUD, numeric/click mapping, resource resolution, native authoring menu and focused/adapted baseline test sources implemented and compiled. Bounded worker route for HUD/input and test adaptation. Await owner-run native Wire/Validate menus, Unity suites and manual inputs. No commit authorized.
- T4 [S5-S7] done - ten-assembly compile exit0 (warnings); pure-model 19 passed/0 failed exit0 empty stderr (not Unity Test Runner). Independent source verification found no confirmed severe Stage4D-caused defect; 94 visual/settings hashes unchanged; scoped source/docs whitespace passed. Whole-tree whitespace exit2 on unchanged baseline TMP/Canvas asset whitespace. Current docs/native checklist and limitations recorded. Native assessment unavailable (untracked declaration), no review authority; native/manual acceptance remains blocked under T1-T3. No commit authorized.

- T5 [S8] in_progress - remove unused one-off prefab generator and isolated manual harness/menu; preserve test authoring helpers and pending validators; compile and commit authorized Loadout 4C/4D scope, excluding ProjectSettings/TMP and unrelated changes. Route: inline.

## Log
L1 — Original request (verbatim):

# Daily Loadout V2 — Stage 4D: Empty Initial Loadout and Equipped HUD

## Context

Project: Magic Garden
Unity: 2022.3.62f3
Branch: feature/daily-loadout-v2

Stages 1–4C are implemented locally. The Stage 4C tests passed.

Inspect the current local working tree before making changes. Do not require a push.

The user has approved a change to the initial Loadout behavior and clarified that the existing HUD bars must display equipped content rather than the complete underlying inventories.

## 1. Empty initial Loadout

At the beginning of a new Run:

- All three plant positions must be empty.
- All three spell positions must be empty.
- Initially unlocked plants and spells must remain available in the catalog.
- Unlocking content must never automatically equip it.
- Do not assign default items during bootstrap.
- Do not grant or remove inventory resources when editing equipment.

Update the state/session initialization contract and affected tests coherently.

Preserve:
- One confirmation per day.
- Atomic confirmation.
- Explicit empty positions.
- Unique identities within each category.
- Discarding unconfirmed changes.
- Previously committed equipment across consecutive days.
- Empty initial equipment again after a complete Run reset.

## 2. Improve the Loadout editor UX

The current UI shows no available options until a position is selected.

Change this behavior:

- On opening the panel, automatically select Plant 1 as the editing target.
- Immediately display all unlocked plant options.
- Selecting a spell position switches the catalog to spells.
- Selecting a plant position switches back to plants.
- Make the active category and target position obvious.
- Always show unlocked items, including currently assigned ones.
- Mark options already assigned to another position.
- Prevent duplicate assignment without silently moving equipment.
- Do not display "No available options" merely because everything is already assigned.
- Keep the selected position highlighted.
- Clear affects only the selected position.
- Selecting one item must never populate another position.

Preserve the existing approved stone-table assets and panel layout.

## 3. Adapt existing HUD bars

Reuse the existing Seed Slots and Spell Slots HUD assets.

These bars are now equipment hotbars, not complete inventory displays.

Requirements:

- Show exactly three equipment positions per category.
- Initial display: six empty positions.
- Display assignments from the committed Loadout only.
- Draft editing must not change the HUD.
- Refresh immediately after successful confirmation.
- Empty positions remain visible.
- Unequipped unlocked content must not appear.
- Do not replace underlying inventory data with equipment arrays.
- Resolve equipment identities against actual inventory entries.
- Preserve real seed quantities and spell cooldowns.
- A plant with zero stock must retain its assigned identity and display quantity zero.
- Show both equipment categories, with the inactive category visually dimmed and non-interactable.
- Preserve the game's existing pixel-art design, layout and relevant UI animations.

Do not create a new HUD from scratch.

## 4. Equipment selection and controls

Ensure visual selection and gameplay selection remain consistent.

- Day: number keys 1–3 select equipped plant positions.
- Night: number keys 1–3 select equipped spell positions.
- Empty equipment positions must never activate unrelated inventory entries.
- Clicking a hotbar slot must follow the same equipment mapping.
- Preserve appropriate Q/E category cycling.
- Keep gameplay admission as the final authorization boundary.
- Do not bypass existing seed stock, cooldown or mana validation.

Remove the obsolete assumption that daytime hotbar positions correspond to raw storage slots.

## 5. Architecture constraints

Maintain separation between:

- Unlocked content catalog
- Committed and draft Loadout
- Underlying SeedInventory / SpellInventory
- HUD presentation
- Gameplay selection and admission

Avoid duplicate sources of truth, new singletons and LINQ in runtime update paths.

Do not modify restoration business logic or unrelated game mechanics.

Use Unity-native editing for prefab/scene changes. Preserve existing prefab GUIDs and visual adjustments.

## 6. Verification

Add and update tests covering:

- Empty Loadout at initial Run start.
- Initial catalog still contains unlocked content.
- One-item Loadout with remaining positions empty.
- Confirming a partially filled Loadout.
- No automatic equipment grants on unlock.
- Confirmed-only HUD updates.
- Cancel leaves the HUD unchanged.
- Correct day/night equipment display.
- Empty slot selection.
- Input and clicked slot mapping.
- Correct seed count and cooldown presentation.
- New-day persistence.
- New-Run reset.
- Existing Stage 4B/4C integration regression tests.

Perform a manual test in Game.unity:

1. Start a new Run.
2. Confirm the equipment HUD is empty.
3. Open the tree's Loadout editor.
4. Verify the two initial plant options are immediately visible.
5. Equip only one plant and one spell.
6. Confirm.
7. Verify that only those two elements appear in their respective HUD positions.
8. Test their use during the correct phases.
9. Verify the remaining slots stay empty.

## Completion

Report:
- Files changed.
- Initial equipment behavior.
- HUD and input changes.
- Unity tests executed and results.
- Manual checks completed.
- Any remaining limitations.

Do not commit or push automatically.

L2 — Baseline: HEAD375ecf9d plus local Stage4C implementation/native wiring, user LoadoutOption/LoadoutSlot visual edits and ProjectSettings/TMP dirt. User reports Stage4C tests passed. Binary diff backup and current UI prefab/settings hashes stored in ignored Temp/Stage4D-check. No prior changes reverted. Unity status exit6/no endpoints; do not install plugins/restart or launch competing Editor. Test-first exception: test sources first where feasible, no native RED/GREEN claim; static compilation is not Unity execution.
L3 — Explorer mapped bootstrap auto-equipment, missing committed notifications, raw Day numeric/seed HUD/click/tooltip/drag assumptions, ten authored HUD slots/category. Existing state-free selection helper and admission remain boundary; zero-stock HUD uses committed identity/catalog metadata and actual inventory quantity. Plan optional ClearSelection APIs for empty equipment positions without changing valid raw storage SelectSlot behavior; no selected-equipment snapshot. Native authoring retains first three nested slots and deactivates extras, preserving GUIDs/art/layout.

L4 - Implementation/check evidence: state initializes six empty positions with parameterless TryInitialize; session emits CommittedChanged only for committed changes. Presenter defaults to Plant1 and retains target with full unlocked category. Existing bars project committed positions and live resources; shared numeric/click mapping replaces daytime raw storage positions. Bounded worker adapted both superseded numeric/HUD-drag baseline suites without removing action/payment assertions. Native HUD authoring menu added but not executed. Updated Loadout/Equipment docs and Stage4D native checklist. Static compilation of MagicGarden.Loadout, Assembly-CSharp, Assembly-CSharp-Editor, UI test support/EditMode/PlayMode, Loadout.Tests, Equipment.Tests, LoadoutBaseline.Tests and Loadout.PlayMode.Tests exited0. Exact 19 updated pure-model NUnit methods passed under bundled Mono (not Unity Test Runner), exit0, empty stderr. Logs: Proyecto-Final/Temp/Stage4D-check/. No native behavioral RED/GREEN claimed. Existing prefab/settings baseline preserved so far; final hashes/checks pending.

L5 - Independent verifier mvdc5nyy-6-fuyh read full spec and baseline patch: no confirmed severe Stage4D-caused defect. S1-S4 structurally implemented but native lifecycle/UI/input acceptance unverified; S5 source/scope met; S6 tests compiled but native execution pending; S7 manual steps unperformed. Verifier flagged an advisory, baseline-present private Awake concern between SpellSlotUI and ImprovedUIButton Button.onClick bridge. Custom OnClick tests do not establish native Button or physical click behavior. No suggested additional probes applied and no advisory-driven source expansion. Owner must check actual assigned/empty spell Button dispatch, repeated native Wire saves/Validate and Game flow.

L6 - Final parent preservation/whitespace checks: whole-tree git diff --check exit2 solely reported trailing whitespace in pre-existing TMP fallback and Canvas asset changes. Assets intentionally untouched. Scoped scripts/tests/Editor docs/task whitespace check exit0. All 94 baseline visual/settings hashes match. No scene/prefab YAML edit, native menu execution, native Unity test run, manual gameplay, commit or push performed. Source/runtime acceptance remains partial. Needs your decision: owner must execute native menus, Unity suites and documented Game.unity flow (or provide an authorized connected Editor endpoint) before T1-T3/Stage4D can complete.

L7 — User correction (verbatim): "el inventario de semillas solo debe verse durante el dia, durante la noche se tiene que ver el inventario de hechizos, no deben superponerse uno con otro." S3 visibility supersedes simultaneous dimmed bars; Day seeds only, Night spells only, outgoing bar hidden immediately to avoid cross-fade overlap. T3 visibility correction in progress; other acceptance blockers unchanged.

L8 — Exclusive phase visibility correction implemented inline: both existing controllers cancel outgoing fades and set inactive alpha0 immediately with raycasts/interactions disabled; incoming-only fades preserve animations without overlap. Updated EditMode Day/Night/Day assertions and added PlayMode interrupted-transition coverage; updated Equipment README/manual checklist. Ten-assembly compile-visibility.log exit0 with warnings; scoped whitespace check passed. No native RED/GREEN, Unity test execution or visual gameplay proof; runner remains unavailable. No inventory, equipment selection, prefab/scene or art edits; no commit/push. T3 returns to blocked pending owner-run native acceptance.

L9 — User correction (verbatim): "elimina el feedback de highligthed en los textos de las opciones, solo tiene que mostrarse si una opcion esta disponible o esta seleccionada" Option-only labels reduced to Available/Selected; assigned identities are Selected in every target, including the currently edited position. Hover/focus sprites, input guards, duplicate rejection/error tint and equipment-position detailed text remain unchanged. Tests updated first; native runner unavailable, no behavioral RED/GREEN claimed. Compilation in progress.

L10 — Option-status correction: ten-assembly compile-option-text.log exit0 with warnings; scoped whitespace check passed. Exact Available/Selected label assertions cover assignment at current/other targets and pointer refresh; existing slot detailed-state and visual/error/input assertions preserved. Native tests/visual checks not executed; T2 remains blocked on owner-run Unity acceptance. No asset edits, commit or push. Memory correction67 supplements the original feature mirror62; S2 current source document is authoritative.

L11 — User requests (verbatim): "perfecto hace el commit"; selected commit scope "Todo el trabajo de Loadout 4C/4D (recomendado)" with code/tests/docs/related assets, excluding ProjectSettings/TMP/unrelated changes; then "elimina las herramientas que ya no se vayan a usar". Readback: initial prefab generator only supplies an otherwise replaceable path constant; manual harness only referenced by its Editor creation menu, with no serialized GUID references. Remove those obsolete tools, retain test-dependent Configure/authoring helpers and pending native wiring/validators. Cleanup and local commit authorized; no push.

L12 — Retired the unused one-off prefab generator and isolated manual harness/menu with their metadata; preserved test-dependent Configure/BindPrefab and World/Hud wiring/validation tools. Removed generator-path coupling and stale test diagnostic instructions. Ten-assembly compile-tool-cleanup.log exit0 (native tests still unavailable). Related Loadout close-button sprite-border author edits belong to the selected feature scope; ProjectSettings/TMP excluded. Preparing authorized local feature commit; no push.
