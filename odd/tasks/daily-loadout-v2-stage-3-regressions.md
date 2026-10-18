# Daily Loadout V2 — Stage 3 Regression Fixes
Scope: authorized seed identity/drag corrections and restored seed key N→storage slot N. Latest user clarification supersedes the former daytime equipment-position rule; Night spells remain unchanged.
Branch: feature/daily-loadout-v2. User authorized the Stage 3 delivery commit; no push. Unity Editor is active; no competing batch instance. Runtime verification gaps below are not resolved by committing.

## Specs
S1. "Before editing:" "Verify the referenced asset GUID." "Inspect actual scene instances and prefab overrides." "Check whether other initialized entries contain mismatched identities." "Fix the incorrect serialized identity without changing seed quantities, slot order, prefab references, or progression rules." "Do not introduce automatic runtime synchronization unless another demonstrated requirement makes it necessary." "Do not suppress legitimate seed-unlock grants."
S2. "Add regression coverage ensuring that matching existing seeds do not receive an additional unlock entry." "Initial inventory identity consistency." "Preservation of original inventory quantities and events."
S3. "Successful swaps clear drag-source visual state." "Failed and cancelled drags restore correct visuals." "End-drag without a successful begin-drag is harmless." "Slot icons and count labels reflect current inventory data." "No stale sprite is displayed in an empty slot." "Inventory storage swaps preserve identities and quantities." "Equipment assignments remain unchanged after storage swaps." "Preserve mouse-based inventory selection, `SeedInventory.SwapSlots()`, and existing inventory notifications." "Do not redesign the HUD."
S4. Latest user correction supersedes the earlier Day equipment-position mapping: "y pero antes funcionaba bien, el 1 tiene que seleccionar el slot 1 y asi sucesivamente, el loadout no tiene que modificar ese comportamiento" "sea 9 o sean 3 esa regla se tiene que respetar". Preserve the working Night spell path, execution admission and single input handler. "Record selected inventory index before and after each keypress." "Verify that `onSlotSelected` is raised and the HUD receives the expected index."
S5. Day selection follows the latest S4 storage rule, not the earlier equipment-position expectation. "Selection after inventory swapping." "Successful, failed, and cancelled drag operations." "Drag-source cleanup and icon/count restoration." "Run Unity EditMode and PlayMode tests." "Report actual execution results, not compilation alone." "Perform manual gameplay verification for the two failed checklist items."
S6. "Preserve the seven previously validated gameplay behaviors." "Do not change Loadout gameplay rules." "Do not alter inventory ownership or selection contracts." "Do not modify unrelated scenes or prefabs." "Do not change crafting, cooldowns, spells, tutorials, or progression rules." "No commits or pushes." "Stop after these corrections. Do not begin Stage 4."
S7. "Exact root cause of each regression." "Files modified." "Any scene or prefab overrides found." "Before/after inventory identities and quantities." "Numeric-selection runtime trace." "Automated test results." "Remaining manual verification."

## Tasks
T1 | S1-S2,S5-S6 | inline (one-line asset fix with focused tests; no parallel writer benefit) | blocked (active Editor integration tests) | commit: forbidden | Tests written first; only prefab identity1→7 changed. Serialized invariant observed RED exit1 then GREEN exit0; quantities/order/references unchanged. Unity tests unexecuted.
T2 | S3,S5-S6 | inline (coupled slot/controller lifecycle and regression fixture) | blocked (active Editor drag tests/manual checks) | commit: forbidden | Six PlayMode regressions written before production fix; slot/controller cleanup and current-data rendering implemented. Source compilation7/7 passed; no Unity runtime result.
T3 | S4-S7 | inline + independent verifier for shared input | blocked (active Editor runtime tests/manual keys) | commit: forbidden | Latest Day storage-selection rule implemented at existing subscriber;1–9 polling restored. Three PlayMode regressions plus revised mixed test compile; source7/7, scoped whitespace0 and exact six-file scope audit pass. Independent source verdict favorable/partial; runtime proof pending.

## Log
L1 — Original request (verbatim):
# Daily Loadout V2 — Stage 3 Regression Fixes

## Context

Branch: `feature/daily-loadout-v2`

Two regressions remain from Stage 3:

1. During the day, keys 1–3 do not correctly change equipped plant selection. Selection appears stuck on one plant.
2. The first seed drag & drop causes an additional Thorned Tendrils entry to appear with quantity 1.

Seven other gameplay checks passed manually. Preserve them.

## Decision 1 — Correct inconsistent serialized data

Confirmed repository mismatch:

- `Assets/Prefabs/UI/SeedInventory.prefab`: second entry has `seedType: 1`.
- Its referenced Thorned Tendrils asset has `seedType: 7`.
- Initial progression unlocks identity 7.
- The configured unlock grant is one seed.

**Authorized exception:** Modify `SeedInventory.prefab` to restore identity consistency.

Before editing:
- Verify the referenced asset GUID.
- Inspect actual scene instances and prefab overrides.
- Check whether other initialized entries contain mismatched identities.

Fix the incorrect serialized identity without changing seed quantities, slot order, prefab references, or progression rules.

Do not introduce automatic runtime synchronization unless another demonstrated requirement makes it necessary.

Do not suppress legitimate seed-unlock grants.

Add regression coverage ensuring that matching existing seeds do not receive an additional unlock entry.

## Decision 2 — Repair drag lifecycle defects

Authorized changes are limited to fixing verified drag lifecycle problems.

Ensure:

- Successful swaps clear drag-source visual state.
- Failed and cancelled drags restore correct visuals.
- End-drag without a successful begin-drag is harmless.
- Slot icons and count labels reflect current inventory data.
- No stale sprite is displayed in an empty slot.
- Inventory storage swaps preserve identities and quantities.
- Equipment assignments remain unchanged after storage swaps.

Preserve mouse-based inventory selection, `SeedInventory.SwapSlots()`, and existing inventory notifications.

Do not redesign the HUD.

## Decision 3 — Diagnose remaining numeric selection

Do not rewrite the contextual input routing based only on the reported symptom.

After correcting the serialized identity:

1. Reproduce keys 1, 2, and 3 in PlayMode.
2. Record committed equipment identities.
3. Record equipment admission results.
4. Record resolved inventory indices.
5. Record selected inventory index before and after each keypress.
6. Verify that `onSlotSelected` is raised and the HUD receives the expected index.

Expected behavior:
- Key 1 selects Dark Root (identity 3).
- Key 2 selects Thorned Tendrils (identity 7).
- Key 3 does nothing because its equipment position is empty.

Equipment positions are not storage positions.

If numeric selection still fails after correcting the asset, identify the exact failed boundary before changing production code.

## Verification

Add or update regression tests for:

- Initial inventory identity consistency.
- No duplicate-looking unlock grant when existing stock is valid.
- Correct selection of equipped identities in different inventory positions.
- Selection after inventory swapping.
- Successful, failed, and cancelled drag operations.
- Drag-source cleanup and icon/count restoration.
- Preservation of original inventory quantities and events.

Run Unity EditMode and PlayMode tests.

Report actual execution results, not compilation alone.

Perform manual gameplay verification for the two failed checklist items.

## Constraints

- Preserve the seven previously validated gameplay behaviors.
- Do not change Loadout gameplay rules.
- Do not alter inventory ownership or selection contracts.
- Do not modify unrelated scenes or prefabs.
- Do not change crafting, cooldowns, spells, tutorials, or progression rules.
- No commits or pushes.

## Completion

Report:
- Exact root cause of each regression.
- Files modified.
- Any scene or prefab overrides found.
- Before/after inventory identities and quantities.
- Numeric-selection runtime trace.
- Automated test results.
- Remaining manual verification.

Stop after these corrections. Do not begin Stage 4.
L2 — Pre-edit audit: Thorned data GUID f63882e6bad5d994d989aa1cbe877347 verified; prefab populated tuples (identity,count,dataIdentity): (14,3,14),(1,3,7),(3,3,3). Other seven entries empty. Game instance 4545205442570902120 inherits contents and overrides capacity to7; no individual content/selection overrides. Menu has no direct prefab/script instance. Audit limited to named prefab and Game/Menu direct instances, not every project scene. Existing unrelated TMP fallback and ProjectSettings edits preserved. Unity process12136 opens this project; no active-Editor automation tool available. No observed Unity RED/GREEN possible here; tests will be written before source correction, compilation is structural only.
L3 — Tests first: appended three Editor regressions for canonical serialized identities, existing-stock grant idempotence/legitimate missing grant, actual catalog selection before/after storage swap. Added six PlayMode regressions using public SeedSlot callbacks with real Canvas, GraphicRaycaster, EventSystem and count labels: first/repeated occupied swaps, empty swaps and stale clearing, failed swap, outside/same-slot cancel, end-without-begin/rejected empty begin, HUD disable. These are unexecuted; they do not prove physical input.
L4 — Observed serialized invariant RED before fix: exit1, stdout tuples (14,3,14),(1,3,7),(3,3,3), stderr FAIL slot1 identity1 != data7. Corrected only second prefab seedType1→7. GREEN exit0: tuples (14,3,14),(7,3,7),(3,3,3), zero mismatches; exact diff assertion proved all other prefab content unchanged. No runtime synchronization or grant suppression.
L5 — Drag fixes: SeedSlot clears empty sprite/text, derives display from its current entry and hides icon/count while dragging. Controller guards unmatched begin/end, finally clears source and refreshes current inventory on success/failure/cancel; disable/destroy also cleans up. No inventory, input, gameplay, scene or unrelated prefab changes. Source and tests compile: `PYTHONIOENCODING=utf-8 python Proyecto-Final/Temp/DailyLoadoutStage2/compile.py` exit0 all7 assemblies, runtime226 sources, PlayMode3 sources. Logs under existing ignored Temp/DailyLoadoutStage2/regression-fixes.*.log. Whole-worktree diff whitespace reports existing TMP fallback whitespace (not changed); scoped C# and README check exit0, CRLF warnings only.
L6 — Native ASSESS attempted once: unassessable due undeclared untracked files; no START/authority/approval/ack. Separate verifier mvd0akye-7-wkuz read full spec/log and correction slice, corroborated asset/scene/grant/render/cleanup contracts and found no severe source defect. Verdict partial: S1 structurally met; S2-S3,S6 unverified; S4-S5,S7 incomplete due runtime requirements. Its scoped whitespace command reported no diagnostics but numeric exit not exposed; parent spotcheck explicitly observed exit0. Optional advisory probes (OnDrag hover, overlap/foreign end, live depletion/destroy and throwing subscribers) were not added: no source blocker or new requirements. No copies, Unity launches, compilers or tests run by verifier.
L7 — Needs your decision: active Editor EditMode Run All + PlayMode Run All and both failed manual checklist items remain unexecuted. Physical keys1/2/3 trace is absent; expected equipment[3,7,empty] → storage[2,1,none] is not an observation. Capture admission/resolved index/selected before-after/original event/HUD highlight, and inventory entries before/after first drag. Seven user-validated behaviors were not enumerated or rerun. Numeric selection cause beyond the confirmed identity mismatch and the exact first-drag delivery timing are not runtime-proven. At that point all three tasks remained blocked on this proof. No commits/pushes or Stage4.
L8 — User correction (verbatim, in order):
"la seleccion con 1,2,3 para hechizos funciona perfecto, para semillas no, es tan simple como replicar la logica no?"
"en el caso del 1 se selecciona el slot 3, el 2 selecciona el 2, y el 3 no hace nada."
"y pero antes funcionaba bien, el 1 tiene que seleccionar el slot 1 y asi sucesivamente, el loadout no tiene que modificar ese comportamiento"
"sea 9 o sean 3 esa regla se tiene que respetar"
This latest requirement supersedes historical Day equipment-position expectations in L1/L7, without changing Night spells or planting admission. T3 alone reopened. Implement direct Day inventory selection at the existing gameplay input subscriber; equipment queries/helper contracts and Night paths remain intact. Restore numeric1–9 polling (old seed HUD handled those keys; InputReader currently stops at8). No new binding, duplicate subscriber, runtime synchronization, HUD redesign, scenes/prefabs or commits. Unity execution remains unavailable in the active Editor; write tests first but do not claim local Unity RED/GREEN.
L9 — Latest correction evidence: three PlayMode tests written before changing production (capacities3/9 and bounds/events, same storage position after swaps/disposed session, numeric empty/unequipped selection cannot plant). Mixed Day/Night test changed only Day selection/event expectations; Night spell indices/event counts retained. PlayerAbilitySystem Day branch directly selects the inventory index; InputReader loops over1–9 in the same single dispatch. All existing entry blockers, first-event Planting activation, original inventory events, equipment/helper public contracts and execution admission retained. No stock, resource, cooldown, drag, prefab/scene or progression code changed.
L10 — `PYTHONIOENCODING=utf-8 python Proyecto-Final/Temp/DailyLoadoutStage2/compile.py`: all7 assemblies exit0, runtime226 sources/PlayMode3 sources; logs seed-storage-selection.*.log in existing ignored Temp. Hash audit from before this task found exactly six expected changed files (two production input files, PlayMode tests, README, two ODD docs); scoped whitespace exit0. Native ASSESS once unassessable because intended-untracked scope undeclared; no native authority. Independent verifier mvd2a2pr-a-qzkr found no concrete defect, confirmed Day mapping independent of Loadout and Night/execution contracts preserved; partial due missing runtime proof. Optional probe not added, no extra sweep.
L11 — Needs your decision: run active Editor GameplayAdmissionPlayModeTests/full PlayMode and relevant EditMode regression suites, then Day physical top-row/keypad1–9 with actual capacity, same-index selection after drag, empty/unequipped selection without planting, and unchanged Night1–3/QE/blockers. Expected fresh Game selection1/2/3→storage slots1/2/3 (not previous3/2/no-op). No new Unity pass/fail/skip counts or manual trace claimed; at that point no commits/pushes/Stage4.
L12 — Delivery authorization (verbatim): "perfecto hace el commit". Supersedes the earlier no-commit constraint for this Stage 3 delivery only; no push or Stage4 authorized. Include Stage 3 code, tests, seed prefab correction and technical/task documentation; exclude pre-existing TMP fallback and ProjectSettings edits. Delivery commit identity: the Git revision containing this L12 entry, subject `feat(loadout): integrate equipment admission and preserve seed selection`. Keep runtime tasks blocked until real results; user approval to commit is not test evidence. Final full commit hash is recorded in session memory/output rather than attempting a self-referential hash in this file.
