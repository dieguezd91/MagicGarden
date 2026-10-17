# Magic Garden Daily Loadout
Objective: Daily equipment authority, gameplay gates, unified HUD and configuration UI. Branch: Develop-Testing. No commits or publishing (explicit user constraint).
Delivery: local uncommitted implementation; no PR requested. Runner: Unity 2022.3.62f3 EditMode (installed version; project records this version).

## Specs
S1. "Create a dedicated controller as the single authority for equipped items." "Keep inventory ownership separate from equipped-item selection." "Support up to 3 items per category, allowing empty slots." "Validate unlocked items, duplicates, capacity and daily confirmation." "Use stable item identities rather than mutable inventory indices." "Commit configuration atomically."
S2. "One complete Loadout configuration can be confirmed per in-game day." "Players may freely edit their configuration before confirmation. Once confirmed, changes are locked until the next day." "Reset daily availability only when a genuinely new game day starts." "Reset Loadout state appropriately on a new run." "Switching active equipped items does not count as modifying the Loadout."
S3. "Integrate equipment validation with existing spellcasting and planting flows." "Prevent bypassing restrictions through hotkeys, UI, or direct gameplay calls." "Preserve existing mana, cooldown, inventory, seed consumption and ability behavior." "Ensure exhausted seed quantities do not silently change the equipped configuration." "Equipped plants/seeds are usable during the day; equipped spells are usable at night." "Planted plants must continue functioning at night; only planting is restricted."
S4. "Create a Loadout configuration menu using existing UI conventions." "Display available items and six equipment slots." "Support editing before confirmation and show the daily lock state afterward." "Replace separate gameplay hotbars with one persistent 6-slot HUD." "Update slot availability according to day/night." "Preserve cooldown and seed count indicators." "Avoid competing input handlers." "Update necessary prefab/scene references, not just scripts."
S5. "Maximum 3 spells + 3 plants." "Exactly one confirmed configuration per day." "Multiple edits before confirmation." "No changes after confirmation, including reopening the menu." "Reconfiguration available on the next day." "Correct behavior across day/night and other GameStates." "Disabled items cannot be selected or used." "Unselected inventory items remain unavailable in gameplay." "Empty slots, exhausted seeds and newly unlocked items behave correctly." "Existing gameplay, UI and reset behavior remains functional."
S6. "Follow existing architecture and conventions." "Prefer modular, event-driven design." "Avoid unnecessary singletons, duplicated state and runtime LINQ." "No paid dependencies." "Do not implement XP, level-up rewards, or unrelated GDD features." "Keep changes scoped; preserve unrelated work." "Do not commit automatically." "Never claim compilation or runtime validation unless actually executed."

## Tasks
T1 | S1,S2,S3,S6 | inline (coupled authority and gameplay contracts) | done | Pure-model RED exit 1 (missing source), GREEN exit 0 (33 assertions); C# compiler check passed core/gameplay/test sources against cached Unity references. Unity execution remains tracked in T3. No commit authorized.
T2 | S4,S5,S6 | inline (UI consumes T1 contracts; sequential, background writers disabled) | done | Serialized HUD/menu and scene wiring implemented. Reopened for verifier blocker B1 (lost tutorial hover), observed regression RED exit 1, restored existing seed/spell tooltip forwarding and pointer-event tests; correction checks passed: 2820 structural assertions, C# cached-reference compilation and model33. Unity visual/runtime checks remain T3. No commit authorized.
T3 | S1-S6 | independent verifier after own checks (shared gameplay contracts: risk item 3) | blocked | Feasible checks passed and targeted independent recheck cleared B1. Awaiting user-run Unity Test Runner/Play Mode verification or a closed Editor for authorized batch validation; the existing Editor owns the project (exit21). Native assessment unassessable (undeclared untracked scope), no native review/lineage created. No commit authorized.

## Log
L1. Original request (verbatim):
## Task: Daily Loadout System

Implement a daily Loadout system in Magic Garden.

### Context
- Unity 2022.3.16f1, C#.
- The player can equip up to **3 spells and 3 plants**.
- One complete Loadout configuration can be confirmed per in-game day.
- Players may freely edit their configuration before confirmation. Once confirmed, changes are locked until the next day.
- Only unlocked items can be equipped.
- Equipped plants/seeds are usable during the day; equipped spells are usable at night.
- All 6 slots remain visible in a unified HUD. Unavailable slots are darkened and cannot be selected.
- Planted plants must continue functioning at night; only planting is restricted.
- Switching active equipped items does not count as modifying the Loadout.

### Phase 1 — Inspect
Before modifying anything:

1. Read repository instructions (`AGENTS.md` and applicable nested instructions).
2. Inspect `LevelManager`, `SpellInventory`, `SeedInventory`, `PlayerController`, `PlayerAbilitySystem`, existing UI controllers, relevant prefabs and scenes.
3. Identify existing selection, unlocking, input, day/night, and reset contracts.
4. Check for uncommitted changes and preserve them.

Reuse existing systems wherever possible.

### Phase 2 — Implement

**Loadout**
- Create a dedicated controller as the single authority for equipped items.
- Keep inventory ownership separate from equipped-item selection.
- Support up to 3 items per category, allowing empty slots.
- Validate unlocked items, duplicates, capacity and daily confirmation.
- Use stable item identities rather than mutable inventory indices.
- Commit configuration atomically.
- Reset daily availability only when a genuinely new game day starts.
- Reset Loadout state appropriately on a new run.

**Gameplay**
- Integrate equipment validation with existing spellcasting and planting flows.
- Prevent bypassing restrictions through hotkeys, UI, or direct gameplay calls.
- Preserve existing mana, cooldown, inventory, seed consumption and ability behavior.
- Ensure exhausted seed quantities do not silently change the equipped configuration.

**UI**
- Create a Loadout configuration menu using existing UI conventions.
- Display available items and six equipment slots.
- Support editing before confirmation and show the daily lock state afterward.
- Replace separate gameplay hotbars with one persistent 6-slot HUD.
- Update slot availability according to day/night.
- Preserve cooldown and seed count indicators.
- Avoid competing input handlers.
- Update necessary prefab/scene references, not just scripts.

### Phase 3 — Verify
Validate:

- Maximum 3 spells + 3 plants.
- Exactly one confirmed configuration per day.
- Multiple edits before confirmation.
- No changes after confirmation, including reopening the menu.
- Reconfiguration available on the next day.
- Correct behavior across day/night and other GameStates.
- Disabled items cannot be selected or used.
- Unselected inventory items remain unavailable in gameplay.
- Empty slots, exhausted seeds and newly unlocked items behave correctly.
- Existing gameplay, UI and reset behavior remains functional.

### Constraints
- Follow existing architecture and conventions.
- Prefer modular, event-driven design.
- Avoid unnecessary singletons, duplicated state and runtime LINQ.
- No paid dependencies.
- Do not implement XP, level-up rewards, or unrelated GDD features.
- Keep changes scoped; preserve unrelated work.
- Do not commit automatically.

### Completion
Finish only when implementation, prefab/scene integration and feasible verification are complete.

Report:
1. Files changed.
2. Architectural decisions.
3. Verification performed and results.
4. Remaining issues or unverified Unity Editor behavior.

Never claim compilation or runtime validation unless actually executed.

L2. Initial inspection: clean working tree, no repository/nested AGENTS.md. Read-only explorer mapped contracts. Day numbers wrap weekly; seed slots are mutable storage; independent public casting and planting paths need gates. Standalone teleport movement ability remains outside inventory equipment. Installed/project Unity is 2022.3.62f3, not requested-context 2022.3.16f1. Expected review workload exceeds 400 lines; no commits/PR/delivery requested, keep coherent local work units.
L3. T1/T2 implementation: pure DailyLoadoutState plus run-owned controller; stable identities resolve to inventory entries on access. GameDaySerial/RunReset distinguish genuine day transitions and resets. Inventory selection/casting and public planting now gate equipment and phase; delayed planting revalidates day/identity and costs occur at commit. Autonomous plants/movement teleport unchanged. Unified serialized prefab is instantiated in Game.unity; prior gameplay HUD prefabs disabled; InputReader owns keys and UIFlow owns Loadout modal. Full mirror synchronized after final evidence reconciliation.
L4. Checks: Unity RED batch attempt exited 21 before compilation because this project is already open; not retried and no Editor process stopped. .NET RED exited 1 for absent model, GREEN exited 0 with 33 assertions. Cached-reference C# compiler check exited 0 for core, gameplay and Editor-test assemblies (warnings, not an Editor build). Structural prefab checks passed 2816 assertions. Initial git diff --check reported two new YAML blank-field trailing spaces; corrected at source and in generated manager block. PyYAML unavailable; no dependency installed. Unity tests added but execution and Play Mode/visual validation remain unavailable. Native review mode reads on; assessment returned unassessable because untracked scope was not explicitly declared; no lineage or review created.
L5. Independent verifier read all S1-S6, observed model33/structural2816/compiler/diff checks pass, plus 96 isolated dependency-stub assertions. It reproduced B1: retiring SeedSlot lost hover-to-tooltip required by tutorial objective23. T2 reopened; parent reproduced structural RED exit1 before implementing pointer enter/exit on LoadoutSlotView and forwarding from DailyLoadoutUI to existing seed/spell tooltip APIs, resolving seed identity at hover time. Added pointer-event NUnit regression and docs. Correction GREEN: structural2820, cached-reference C# compiler, model33 and git diff --check all exit0. Targeted independent recheck cleared B1: source route reaches existing seed tutorial emitter, resolves swapped seed index at hover, and preserves nighttime spell tooltip/cleanup. No Unity runtime proof claimed.
L6. Final feasible verification: parent structural spot check2820 and git diff --check exit0; independent verifier observed cached-reference compile exit0, same structural2820 and targeted hover-route GREEN. T1/T2 implemented; T3 blocked on Editor validation. The project is already open in Unity, so initial batch exit21 cannot prove import, NUnit execution, animation/event delivery, visible layout or nighttime plant behavior. No process stopped, no retry, no commits, no review lineage. User decision needed for Editor-side tests or closing the project before a later batch run.
