# Daily Loadout V2 — Stage 1
Objective: baseline characterization and isolated equipment state only. Branch: feature/daily-loadout-v2; base: 2a2d2ffc45e97d55e8b75db7db4d6d65e0427378.
Delivery: uncommitted local changes only (user prohibition). Runner: Unity 2022.3.62f3 EditMode; standalone model checks where available; no gameplay/UI integration.

## Specs
S1. Branch and preservation: "Verify that `Develop-Testing` is up to date." "Inspect the working tree before creating the branch." "Preserve all uncommitted user changes." "Create the new branch directly from `Develop-Testing`." "Never merge, cherry-pick, or copy implementation files from `loadout`." "Read root and applicable nested repository instructions before editing."
S2. Baseline tests: "Inventory selection and associated events." "Seed consumption, depletion, restocking, and storage swaps." "Spell selection and cycling." "Cooldown ownership and update behavior." "Unlock and progression integration." "Important existing lifecycle behavior." "Do not change production gameplay code to make baseline tests pass." "Record which tests actually run successfully and which cannot be executed."
S3. Model: "Three plant assignments and three spell assignments." "Stable item identities." "Committed state and editable draft state." "Atomic confirmation." "Duplicate prevention." "Support for empty positions." "Daily confirmation tracking." "Initial-equipment bootstrap operation that does not consume confirmation." "Draft discard." "Run reset."
S4. LD-01: "On a new run, automatically equip up to three initially unlocked plants and three initially unlocked spells." "Initialization does not consume the daily reconfiguration allowance." "Use a deterministic content order." "Do not alter inventory quantities or cooldowns."
S5. LD-02: "Editing and confirming equipment is permitted only during the day." "Only one manual confirmation is permitted per in-game day." "Existing committed equipment remains usable if the player does not reconfigure it."
S6. LD-03: "Draft edits do not affect committed equipment." "Discard unconfirmed changes when night begins." "Initialize the next day's draft from the currently committed equipment."
S7. LD-04: "Equipped plants remain assigned even when their seed quantity reaches zero." "Depleted plants are temporarily unusable." "Restocking restores usability automatically." "Equipment assignments must survive inventory slot clearing and rearrangement."
S8. LD-05: "Up to three plants and three spells." "Empty equipment positions are allowed." "Duplicate identities within a category are forbidden."
S9. LD-06 (future integration, not Stage 1): "Preserve the existing contextual behavior of number keys." "During the day, keys 1–3 select equipped plants." "During the night, keys 1–3 select equipped spells." "Q/E retains daytime ability cycling and nighttime spell cycling, restricted to equipped spells where applicable." "Do not introduce separate numeric ranges for plants and spells." "Inventory selection state remains inventory-owned."
S10. Isolation: "Keep this model independent of inventory mutation, gameplay execution, and UI." "Add EditMode tests covering its invariants and approved rules." "The model may expose explicit operations for lifecycle coordination, but do not connect it to the actual day/night controller yet."
S11. Restrictions: "Do not modify `SeedInventory` or `SpellInventory` production behavior." "Do not modify planting or casting implementations." "Do not modify HUD controllers or input handling." "Do not modify Unity scenes or prefabs." "Do not wire gameplay execution to the new equipment model." "Do not migrate code from the previous implementation." "Do not commit or push changes."
S12. Completion/report: "Its tests pass in Unity's Test Runner, or any inability to execute them is reported explicitly." "The final diff contains no unrelated modifications." "Stop after Stage 1. Do not proceed to gameplay integration or UI without explicit approval."

## Tasks
T1 | S1-S2,S11-S12 | inline (background writers unavailable; preparation map delegated read-only) | done | commit: forbidden | 11 baseline tests added; source compilation PASS (0 warnings/errors); Unity execution unavailable, exit 21 (project already open).
T2 | S3-S8,S10-S12 | inline | done | commit: forbidden | 19 exact model tests passed on bundled Mono; valid RED 3 passed/16 failed, GREEN 19 passed/0 failed; model remains unwired.
T3 | S1-S12 | inline checks + delegated independent verifier (native assessment unassessable) | blocked | commit: forbidden | Source/ancestry/scope/metadata reviewed; model independently passes. Waiting for user-owned Unity editor availability or Test Runner execution to supply missing baseline/import/regression proof. No runtime pass or native approval claimed.

## Log
L1 — User request (verbatim):
# Daily Loadout V2 — Implementation, Stage 1

## Objective

Begin a clean implementation of Daily Loadout V2, following the approved architecture review and gameplay contracts.

The previous implementation caused extensive regressions by replacing existing inventory and gameplay responsibilities. This must not happen again.

## Repository and branch

Repository: `dieguezd91/Proyecto-Final`

Base branch: `Develop-Testing`

Create a new branch: `feature/daily-loadout-v2`

Requirements:

1. Verify that `Develop-Testing` is up to date.
2. Inspect the working tree before creating the branch.
3. Preserve all uncommitted user changes.
4. Create the new branch directly from `Develop-Testing`.
5. Never merge, cherry-pick, or copy implementation files from `loadout`.
6. Read root and applicable nested repository instructions before editing.

## Approved architecture

Daily Loadout is an additive equipment configuration layer.

- `PlayerContentUnlockSystem` owns content unlocking.
- `SeedInventory` owns seed storage, quantities, selection, and inventory events.
- `SpellInventory` owns spell data, selection, and cooldowns.
- Existing gameplay controllers own planting, casting, and animations.
- Loadout owns equipment assignments, draft configuration, confirmation, and daily reconfiguration restrictions.
- Equipment slots must reference item identity rather than mutable inventory positions.
- Loadout must not become a second inventory or a second gameplay selection authority.

## Approved gameplay rules

**LD-01 — Automatic initial equipment**
- On a new run, automatically equip up to three initially unlocked plants and three initially unlocked spells.
- Initialization does not consume the daily reconfiguration allowance.
- Use a deterministic content order.
- Do not alter inventory quantities or cooldowns.

**LD-02 — Day-only configuration**
- Editing and confirming equipment is permitted only during the day.
- Only one manual confirmation is permitted per in-game day.
- Existing committed equipment remains usable if the player does not reconfigure it.

**LD-03 — Unconfirmed changes**
- Draft edits do not affect committed equipment.
- Discard unconfirmed changes when night begins.
- Initialize the next day's draft from the currently committed equipment.

**LD-04 — Seed depletion**
- Equipped plants remain assigned even when their seed quantity reaches zero.
- Depleted plants are temporarily unusable.
- Restocking restores usability automatically.
- Equipment assignments must survive inventory slot clearing and rearrangement.

**LD-05 — Empty slots and duplicates**
- Up to three plants and three spells.
- Empty equipment positions are allowed.
- Duplicate identities within a category are forbidden.

**LD-06 — Contextual controls**
- Preserve the existing contextual behavior of number keys.
- During the day, keys 1–3 select equipped plants.
- During the night, keys 1–3 select equipped spells.
- Q/E retains daytime ability cycling and nighttime spell cycling, restricted to equipped spells where applicable.
- Do not introduce separate numeric ranges for plants and spells.
- Inventory selection state remains inventory-owned.

## Additional integration requirements

- The unified HUD will eventually display all six equipment positions, with the inactive category visually disabled.
- Preserve existing tutorial events, tooltips, input behavior, sounds, crafting, upgrades, mana, cooldowns, and inventory interactions.
- Previously planted entities must retain their original nighttime behavior.
- Do not create new gameplay rules to resolve ambiguity.
- Preserve existing external method signatures and event semantics.
- Avoid new singletons, duplicated state, broad refactors, and unrelated changes.

## Authorized work — Stage 1 only

### Milestone A: Baseline regression tests

Review the current architecture and add focused characterization tests for existing behaviors, including:

- Inventory selection and associated events.
- Seed consumption, depletion, restocking, and storage swaps.
- Spell selection and cycling.
- Cooldown ownership and update behavior.
- Unlock and progression integration.
- Important existing lifecycle behavior.

Do not change production gameplay code to make baseline tests pass.

Record which tests actually run successfully and which cannot be executed.

### Milestone B: Isolated equipment model

Implement the minimal Loadout equipment-state model:

- Three plant assignments and three spell assignments.
- Stable item identities.
- Committed state and editable draft state.
- Atomic confirmation.
- Duplicate prevention.
- Support for empty positions.
- Daily confirmation tracking.
- Initial-equipment bootstrap operation that does not consume confirmation.
- Draft discard.
- Run reset.

Keep this model independent of inventory mutation, gameplay execution, and UI.

Add EditMode tests covering its invariants and approved rules.

The model may expose explicit operations for lifecycle coordination, but do not connect it to the actual day/night controller yet.

## Forbidden in this stage

- Do not modify `SeedInventory` or `SpellInventory` production behavior.
- Do not modify planting or casting implementations.
- Do not modify HUD controllers or input handling.
- Do not modify Unity scenes or prefabs.
- Do not wire gameplay execution to the new equipment model.
- Do not migrate code from the previous implementation.
- Do not commit or push changes.

## Completion criteria

Stage 1 is complete only when:

1. The new branch is confirmed to originate from `Develop-Testing`.
2. Existing inventory and gameplay behavior remains unchanged.
3. Baseline tests are documented with real execution results.
4. The isolated equipment model implements the approved state rules.
5. Its tests pass in Unity's Test Runner, or any inability to execute them is reported explicitly.
6. The final diff contains no unrelated modifications.

## Final report

Provide:

- Branch name and base SHA.
- Files added or modified.
- Baseline tests and results.
- Equipment-model tests and results.
- Architectural decisions and assumptions.
- Actual Unity verification performed.
- Remaining risks and blockers.
- Proposed scope for the next implementation stage.

**Stop after Stage 1. Do not proceed to gameplay integration or UI without explicit approval.**

L2 — Preparation: fetched origin Develop-Testing successfully; local, remote and FETCH_HEAD equal base SHA; ahead/behind 0/0; clean tree before git switch -c feature/daily-loadout-v2 Develop-Testing. No local AGENTS.md found by repository-scoped scan; injected global instructions apply. Previous loadout source not accessed. Existing Unity editor has this project open (PID 12136); no process will be stopped. No commits authorized. Baseline and model are independent deliverables; background writer policy off means inline work. Forecast roughly 700–1000 authored lines across characterization, model, tests and explanatory docs; no delivery/PR requested, no commit boundaries created.

L3 — T1: added 11 baseline characterization tests and isolated Editor assembly; source-only dotnet build passed with 0 warnings/errors. Unity EditMode attempt exited 21 before tests: another editor owns the project. No retry, process termination, or alternate Unity project. Baseline tests are NOT reported passing. Test record: Assets/Tests/Editor/LoadoutBaseline/README.md. T2 begins with test-first pure-model checks; Unity runtime coverage remains explicitly unavailable.

L4 — Tooling incident separated from behavior: cached NUnit net35 cannot run on .NET 10 (19 CallContext TypeLoadExceptions). First Mono compile incorrectly included Unity-only baseline references and failed for missing netstandard facade; baseline was already compiled separately. Corrected pure-model harness uses bundled Mono with mscorlib/System/System.Core and cached NUnit. Actual RED: exit 1, 3 pass/16 assertion failures against scaffold. Actual GREEN: exit 0, 19 pass/0 fail, empty stderr. Logs retained under ignored Temp/DailyLoadoutStage1. No failed infrastructure attempt is called RED. No Unity runner retry or process termination.

L5 — T2: added pure generic model and separate asmdefs/tests, model README, and new asset metadata only. Stable identities support existing enum/SO types without dependency on Assembly-CSharp. Ordered bootstrap input and monotonic per-run day token are explicit caller contracts; future adapters own actual unlock validation, day mapping and stock usability. Model does not own gameplay selection. No existing tracked production files changed. T3 begins; native assess returned unassessable because new untracked files require explicit declaration, changedPaths 0, candidate null, independentVerifier true. This is NOT a low-risk assessment or approved native review; follow the high-risk verification fallback without inventing review authority.

L6 — Independent verifier: current-source model compile exit 0; exact 19 model tests 19 pass/0 fail, exit 0, empty stderr; three independent probes also pass. No severe reproduced blocker. Reviewed every S1-S12 and all 20 new files; direct branch creation confirmed by reflog, HEAD/base/merge-base identical, existing tracked diff empty, 11 unique valid metadata GUIDs with no tracked collision. Verdict partial solely for absent Unity execution. Advisory (not reproduced): inactive-fixture SendMessage delivery in cooldown test must be validated in Unity. No source correction was made for an unproven advisory. Additional probe code was not added to the repository; no commits.

L7 — Parent closing audit: existing tracked diff against base exits 0; branch and scoped untracked-file assertions pass. Optional new-file whitespace-audit wrapper did not complete: it first incorrectly rejected git --no-index's normal exit 1, then treated configured LF-to-CRLF warnings as failures (also with core.autocrlf=false). These are failed audit-wrapper attempts, not passing whitespace checks or model failures; no more retries. Only the first new folder metadata had whitespace normalized during these attempts. Independent scope/metadata findings remain as reported; all gameplay/source files unchanged since GREEN. Missing Unity Test Runner/import/baseline/existing-suite proof remains explicitly unavailable. T3 blocked on editor availability/user-run tests; Stage 1 implementation delivered with verification limitation, not an unconditional completion claim. Stop before gameplay/UI integration as instructed.
