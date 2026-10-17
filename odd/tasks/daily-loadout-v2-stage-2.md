# Daily Loadout V2 — Stage 2
Objective: repair baseline fixture; identity resolution, deterministic bootstrap and daily/run lifecycle only. Branch: feature/daily-loadout-v2; base: 2a2d2ffc45e97d55e8b75db7db4d6d65e0427378.
Delivery: local uncommitted changes; no push. Tests: Unity 2022.3.62f3 EditMode/PlayMode where available, source compilation and pure-model fallback clearly separated.

## Specs
S1. Preflight: "Read all applicable repository instructions." "Verify the current branch and working-tree changes." "Inspect the complete Stage 1 implementation and tests." "Review the approved architecture and LD-01 through LD-06." "Preserve all user changes." "Do not reuse the previous `loadout` branch."
S2. Fixture: "Preserve `SpellInventory` production behavior." "Do not weaken assertions merely to obtain a passing test." "Use a deterministic, supported test mechanism." "If frame-dependent cooldown behavior requires PlayMode, separate it into an appropriate PlayMode test." "Preserve coverage for original-slot cooldown ownership and update notifications." "Execute the corrected test and record its actual result."
S3. Resolution: "Use the existing `SeedsEnum` identity." "Resolve against current `SeedInventory` entries." "Handle empty inventory slots, depleted stock, swapped storage positions, and replenishment." "Never make equipment identity depend on inventory indices." "Do not mutate seed quantities or storage." "Use existing `SpellDataSO` references as the equipment identity." "Validate mappings based on the current data configuration." "Do not copy or own cooldown data." "The resolver must not select items automatically during ordinary queries."
S4. States: "Content is unlocked." "Content is equipped." "Content currently has a usable inventory entry." "An unlocked and equipped plant may legitimately have zero seeds."
S5. Bootstrap: "On a new run, equip up to three initially unlocked plants and three initially unlocked spells." "Allow empty positions if fewer items are available." "Use deterministic progression/catalog ordering, not unordered HashSet iteration." "Do not consume the daily reconfiguration allowance." "Do not grant seeds, spells, or unlocks." "Do not overwrite inventory data." "Loadout initialization must not occur with incomplete unlock state." "New content unlocked later must not unexpectedly replace existing equipment."
S6. Lifecycle: "Only one manual confirmation per genuine in-game day." "Previous committed equipment persists across days." "A new day restores the configuration allowance." "Repeated notifications must not restore the allowance." "A weekly day-number rollover must not corrupt confirmation tracking." "A new run must reset all Loadout session state and automatically initialize starting equipment again." "A configuration rejection must not consume the daily allowance." "Preserve existing `OnNewDay` and `OnPhaseChanged` event semantics."
S7. Approved rules: "Manual configuration and confirmation are available during the day only." "Unconfirmed draft changes are discarded when night starts. Committed equipment remains active." "Seed depletion does not remove equipment assignments." "Empty slots are valid; duplicate assignments are invalid."
S8. Assembly: "Keep the pure equipment model independent of Unity gameplay classes." "Do not place code that requires `Assembly-CSharp` types inside an assembly that cannot legally reference them." "Do not move existing production files between assemblies."
S9. Verification: "Execute the existing tests as well." "Use actual Unity Test Runner results where possible. Report skipped or unavailable tests explicitly." "If the failure cannot be resolved safely, document the blocker and continue only with isolated Stage 2 development. Do not report the full stage as verified."
S10. Restrictions: "No second selection authority." "No gameplay restrictions yet." "No new singleton." "No inventory replacement." "No cooldown duplication." "No unrelated refactoring." "No commits or pushes." "Do not begin gameplay admission or HUD integration without approval."

## Tasks
T1 | S1-S2,S9-S10 | inline | blocked | commit: forbidden | Fixture correction and two PlayMode tests written; waiting for Unity editor availability for actual reproduction/corrected execution. No pass claimed.
T2 | S3-S8,S10 | inline | blocked | commit: forbidden | Resolver/bootstrap/day session/owner readiness implemented; all current runtime and test sources compile. Waiting for actual Unity integration execution; no runtime pass claimed.
T3 | S5-S7,S10 | inline | blocked | commit: forbidden | End-of-ResetGame completion call added after all legacy operations; waiting for actual Unity reset/lifecycle test execution.
T4 | S1-S10 | inline + independent verifier (shared progression/reset integration) | blocked | commit: forbidden | Source compilation/audits complete; independent verdict partial, no severe defect. Waiting for actual Unity import/Test Runner results; independent optional execution unavailable under scratch-location constraints. No full-stage/native approval.

## Log
L1 — User request (verbatim):
# Daily Loadout V2 — Stage 2: Equipment Resolution and Daily Lifecycle

## Context

Stage 1 introduced an isolated equipment-state model and baseline characterization tests on `feature/daily-loadout-v2`.

One baseline test remains failing:

`InventoryBaselineTests.CooldownsBelongToOriginalSlotAndUpdateThroughUnityMessageDispatch`

Unity reports `SendMessage Update has no receiver`, and the cooldown remains at 3600 instead of decreasing.

This failure must be investigated without modifying production cooldown behavior to satisfy the test.

## Objective

Integrate the existing equipment-state model with content progression, inventory resolution, and the day/night lifecycle.

Preserve the original architecture and gameplay behavior.

**Do not integrate the Loadout with gameplay execution or UI in this stage.**

## Preflight

1. Read all applicable repository instructions.
2. Verify the current branch and working-tree changes.
3. Inspect the complete Stage 1 implementation and tests.
4. Review the approved architecture and LD-01 through LD-06.
5. Preserve all user changes.
6. Do not reuse the previous `loadout` branch.

The existing Stage 1 model must be reviewed before introducing adapters or lifecycle components.

## Task A — Resolve the baseline test failure

Inspect the failing test and its fixture.

Determine why `SendMessage("Update")` does not dispatch as expected in Unity EditMode.

Requirements:

- Preserve `SpellInventory` production behavior.
- Do not weaken assertions merely to obtain a passing test.
- Use a deterministic, supported test mechanism.
- If frame-dependent cooldown behavior requires PlayMode, separate it into an appropriate PlayMode test.
- Preserve coverage for original-slot cooldown ownership and update notifications.
- Execute the corrected test and record its actual result.

If the failure cannot be resolved safely, document the blocker and continue only with isolated Stage 2 development. Do not report the full stage as verified.

## Task B — Equipment identity and inventory resolution

Implement the smallest necessary adapter to resolve equipped identities against the existing inventories.

Plants:

- Use the existing `SeedsEnum` identity.
- Resolve against current `SeedInventory` entries.
- Handle empty inventory slots, depleted stock, swapped storage positions, and replenishment.
- Never make equipment identity depend on inventory indices.
- Do not mutate seed quantities or storage.

Spells:

- Use existing `SpellDataSO` references as the equipment identity.
- Resolve to the corresponding existing `SpellInventory` entry.
- Validate mappings based on the current data configuration.
- Do not introduce a new persistent identity system without demonstrated necessity.
- Do not copy or own cooldown data.

Resolution must distinguish:

1. Content is unlocked.
2. Content is equipped.
3. Content currently has a usable inventory entry.

An unlocked and equipped plant may legitimately have zero seeds.

The resolver must not select items automatically during ordinary queries.

## Task C — Deterministic initial equipment

Implement LD-01:

- On a new run, equip up to three initially unlocked plants and three initially unlocked spells.
- Allow empty positions if fewer items are available.
- Use deterministic progression/catalog ordering, not unordered HashSet iteration.
- Do not consume the daily reconfiguration allowance.
- Do not grant seeds, spells, or unlocks.
- Do not overwrite inventory data.

Inspect the initialization sequence of `PlayerContentUnlockSystem`.

Account for lifecycle ordering: Loadout initialization must not occur with incomplete unlock state.

New content unlocked later must not unexpectedly replace existing equipment.

## Task D — Day/night lifecycle integration

Implement the approved rules:

**LD-02:** Manual configuration and confirmation are available during the day only.

**LD-03:** Unconfirmed draft changes are discarded when night starts. Committed equipment remains active.

**LD-04:** Seed depletion does not remove equipment assignments.

**LD-05:** Empty slots are valid; duplicate assignments are invalid.

Daily behavior:

- Only one manual confirmation per genuine in-game day.
- Previous committed equipment persists across days.
- A new day restores the configuration allowance.
- Repeated notifications must not restore the allowance.
- A weekly day-number rollover must not corrupt confirmation tracking.
- A new run must reset all Loadout session state and automatically initialize starting equipment again.
- A configuration rejection must not consume the daily allowance.

Important existing contracts:

- `DayCycleController.StartDay()` can be called repeatedly.
- `OnNewDay` is not necessarily a unique-day notification.
- The displayed day number may repeat.
- `TotalNights` and `ResetDayCount()` have different reset semantics.
- `GameResetController` currently manages a specific reset sequence.

Do not identify a new day using the displayed day number alone.

Prefer adapting to existing lifecycle signals. If the existing contracts are insufficient, propose and implement only the smallest necessary extension, with dedicated tests.

Preserve existing `OnNewDay` and `OnPhaseChanged` event semantics.

Pay particular attention to reset ordering and progression rebuilds.

## Task E — Assembly boundaries

The Stage 1 model uses a dedicated assembly definition.

Before adding adapters:

- Inspect current assembly dependencies.
- Keep the pure equipment model independent of Unity gameplay classes.
- Do not place code that requires `Assembly-CSharp` types inside an assembly that cannot legally reference them.
- Prefer small adapters at the existing integration boundary.
- Do not move existing production files between assemblies.

## Task F — Tests

Add focused tests covering:

- Identity resolution independent of inventory position.
- Seed depletion and subsequent replenishment.
- Unlocked but not stocked plants.
- Spell resolution and cooldown ownership.
- Deterministic initial equipment.
- Initial equipment not consuming confirmation.
- One manual confirmation per day.
- Repeated day notifications.
- Day-to-night draft discard.
- Night-to-day allowance restoration.
- Weekly rollover.
- Full run reset and initialization ordering.
- Invalid confirmation leaving committed state unchanged.

Execute the existing tests as well.

Use actual Unity Test Runner results where possible. Report skipped or unavailable tests explicitly.

## Non-negotiable restrictions

Do not modify:

- `SeedInventory` selection or mutation contracts.
- `SpellInventory` selection or cooldown contracts.
- Planting execution.
- Spellcasting execution.
- Player abilities.
- Input bindings.
- HUD controllers.
- Tooltips and tutorials.
- Scenes or prefabs.

Exceptions require explicit user approval.

In particular:

- No second selection authority.
- No gameplay restrictions yet.
- No new singleton.
- No inventory replacement.
- No cooldown duplication.
- No unrelated refactoring.
- No commits or pushes.

The Stage 2 adapter may expose methods needed by later stages, but must not change gameplay behavior until Stage 3.

## Completion criteria

Stage 2 is complete when:

1. The failing baseline test has been resolved or explicitly documented as a verification blocker.
2. Equipment resolves correctly against the existing inventories.
3. Initial equipment is deterministic.
4. Daily confirmation and draft lifecycle follow the approved rules.
5. Reset behavior is verified.
6. All existing inventory contracts remain unchanged.
7. Tests pass, with actual execution evidence.
8. No gameplay or UI integration has occurred.
9. The diff contains only approved Stage 2 changes.

## Final report

Provide:

- Files added and modified.
- Exact integration boundaries.
- Confirmation of preserved inventory contracts.
- Baseline test results.
- New Stage 2 test results.
- Unity compilation and Test Runner status.
- Lifecycle edge cases verified.
- Remaining risks.
- Proposed Stage 3 integration plan.

Stop after Stage 2.

**Do not begin gameplay admission or HUD integration without approval.**

L2 — Preflight: branch feature/daily-loadout-v2, HEAD 2a2d2ffc, no tracked edits, 20 pre-existing untracked Stage 1 files. No local AGENTS.md found. All pre-existing files plus two target production files backed up/hash-recorded under ignored Proyecto-Final/Temp/DailyLoadoutStage2/preflight. Full Stage 1 review delegated read-only, pure model spot-checked inline; no old loadout source inspected. Unity remains open; no current in-project result XML found for the reported failure. Failure cannot currently be re-executed in the locked project; source and Unity 2022.3 SendMessage documentation identify unsupported inactive-object dispatch. Do not claim actual reproduction or test pass without execution.
L3 — Boundary decision: new ordinary session/resolver in Assembly-CSharp (outside Scripts/Loadout asmdef), owned by PlayerContentUnlockSystem. Explicit unlock-ready flag guards initial bootstrap; ordered progression lists filtered through unlock membership. OnNewDay plus TotalNights deduplicates days; Night phase discards. One end-of-ResetGame call resets/rebootstraps session after existing progression/mana work. No changes to OnNewDay/OnPhaseChanged semantics, legacy counters, inventory methods, gameplay/UI/assets, or pure model. Shared initialization/reset risks require one independent verifier. Roughly 900–1200 authored lines forecast including tests/docs; no delivery/PR/commit requested.

L4 — Implementation: corrected EditMode callback dispatch (same lifecycle helper as Awake) with existing assertions retained; 2 new real-frame PlayMode tests use captured 0.02-second delta and restore time/static state. Added 13 public-API integration tests, resolver, session and owner readiness/composition. Session initialization/day handling grouped with resolver because bootstrap readiness is a shared dependency; reset task remains only the completion hook. Full-run call added strictly after existing ResetGame mana/cleanup operations. Pure model source unchanged. No actual Unity RED or corrected-test execution obtained; reported original failure cannot be independently rerun while editor owns project.

L5 — Compilation/execution: first source-only compile wrapper stopped on Python cp1252 console output after pure-model compile, not a production compile failure; corrected using PYTHONIOENCODING=utf-8. Final compile exits 0 for seven assemblies: pure model (1 source), Assembly-CSharp (224 sources), baseline tests, model tests, Stage 2 integration tests, PlayMode tests, existing MagicGarden.Tests (9 sources). Compiler uses generated Unity references/defines and only ignored output paths, not Unity import; existing-source warnings and response-file noconfig warning remain. Last compile includes fixed captured-delta tests. Existing pure model suite executed on bundled Mono: 19 pass/0 fail, exit 0, empty stderr. Unity full EditMode attempt exited 21 before any tests: project open in another instance. No Unity retry/termination or PlayMode attempt against the same unavailable project. New 13 integration tests, corrected baseline, 2 PlayMode tests and existing Unity regression suite NOT reported passing.

L6 — Native assess unassessable due undeclared untracked scope, changedPaths 0/candidate null; independentVerifier true. No low-risk/native approval inferred. Parent preflight-hash audit passes: 17 of 22 pre-existing files byte-identical, exactly 5 approved changes (owner/reset, baseline test/README, pure-model README); 19 Stage 2 new files in approved surfaces including 11 metadata. No inventory/gameplay/input/UI/scene/prefab changes. One independent verifier requested against full Stage 2 specifications; actual Unity execution remains external blocker.

L7 — Independent verdict partial: reviewed every S1-S10, current runtime/tests/metadata, complete original request and preflight backups. Confirmed 22 backup hashes, 17 unchanged originals/5 approved changes, 19 additions/11 metadata, 14 additive tracked production lines across two files, correct assembly boundaries, source compiler coverage of all 224 gameplay files and test sources. No reproduced severe defect; no correction batch requested. Its optional recompile/probes were NOT executed because verifier scratch rules require system-temp while this session permits only project-local output; no outside writes or retry. Proposed extra probes not added; existing requested coverage remains as authored. Runtime ordering, reset, resolver, corrected baseline and PlayMode results remain unverified. Four unchanged Assets/Scripts/Editor sources are outside the seven-assembly source check. One inspection-script SyntaxError in verifier was corrected; neither inspection command was a behavior test.

L8 — Stop: all source tasks are implemented but held blocked for actual Unity verification; no task is marked functionally complete. Only existing model tests have an executed 19/19 bundled-Mono pass. Needs human decision: run the corrected baseline, model/integration/EditMode regression and PlayMode suites in the open editor (or make the project available for a later authorized run). Do not rerun unavailable verifiers or start gameplay admission/HUD work. No commits, staging, pushes, scene/prefab changes or old-loadout reuse.
