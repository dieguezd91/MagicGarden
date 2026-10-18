# Daily Loadout — equipment-state model

`DailyLoadoutState<TPlant, TSpell>` owns equipment configuration only. Stage 2
instantiates it through an Assembly-CSharp session adapter; Stage 3 locks draft
editing after confirmation. The model has no Unity dependency, singleton, inventory reference,
selected-slot state, quantities, cooldowns, input or UI.

## Identity and bootstrap

A later coordinator can instantiate `DailyLoadoutState<SeedsEnum, SpellDataSO>`
from Assembly-CSharp without moving or modifying those existing types.

- `EquipmentAssignment<T>.For(identity)` represents an occupied position.
- `default(EquipmentAssignment<T>)` represents an empty position.
- Null is not a valid occupied identity. Domain sentinels such as `SeedsEnum.None`
  must be excluded by the caller; the generic model does not invent domain rules.
- Equality uses `EqualityComparer<T>.Default`: enum value identity for plants and
  the existing ScriptableObject identity for spells. Do not pass mutable storage
  indices, display names or upgrade-target keys as item identities.

`TryBootstrap(orderedPlants, orderedSpells)` chooses the first three distinct
identities per category in the supplied order. It accepts fewer than three,
including none, and is permitted only once per run. Null lists/identities reject
atomically. It copies assignments; it does not retain the lists.

The caller must provide **already unlocked** content in a deterministic catalog
order (for example, serialized progression-list order), not HashSet enumeration.
The model does not query or duplicate PlayerContentUnlockSystem. Bootstrap does
not enter a day and does not consume a manual confirmation.

## Explicit lifecycle, not live integration

1. `TryBootstrap(...)` initializes the run's committed equipment and draft.
2. `TryBeginDay(runDayToken)` opens editing and copies committed equipment to draft.
3. `TrySetDraftPlant` / `TrySetDraftSpell` edit one of three positions. Duplicate
   identities within the same category and invalid positions are rejected without
   mutation. Use an empty assignment to clear a position.
4. `TryConfirm()` commits **both** categories and spends the day's confirmation.
   Rejection changes neither category nor the allowance. An unchanged manual
   confirmation still counts as the day's one confirmation.
5. `DiscardDraft()` restores the current committed configuration without spending
   the allowance. `BeginNight()` also discards, and closes editing/confirmation.
6. The next new day begins with the existing committed configuration.
7. `ResetRun()` clears committed/draft state, initialization, phase, token and lock.

Day tokens are caller-supplied, monotonically increasing within a run. They are
**not** the game's repeating displayed day number. Repeating the active day token
is idempotent and does not erase edits or renew confirmation. Older tokens, and
reopening a night-closed day with the same token, are rejected. Only a run reset
permits token reuse. There is deliberately no DayCycleController subscription.

Draft edits never affect committed state. Successful confirmation locks both draft
setters for the rest of that genuine day. Discarding drafts, repeated day notifications
and a night transition cannot reopen editing; a genuinely new day or run reset can.

## Stage boundaries

Stage 2 provides LD-01 bootstrap and explicit daily/run coordination through
`Assets/Scripts/Equipment/DailyLoadoutSession.cs`, owned by the existing unlock
system. Its resolver reads current stock/configuration without selecting or
mutating inventory. See `Assets/Scripts/Equipment/README.md` for those boundaries.

Stage 3 adds LD-06 contextual controls and gameplay admission outside this pure
model. LD-07 removes keyboard double-press swapping, retaining mouse/drag storage
interaction. The six-position HUD and Loadout menu remain out of scope. The pure
model does not validate domain unlock membership; the session does. No save format
or cross-session persistence is introduced.

## Verification

EditMode tests: `Assets/Tests/Editor/Loadout/DailyLoadoutStateTests.cs` in
`MagicGarden.Loadout.Tests`. Use Unity's EditMode Test Runner when available.

Actual local results:

- Pure model: **19 passed, 0 failed** under Unity 2022.3.62f3's bundled Mono runtime,
  invoking the exact NUnit `[Test]` methods. This is not Unity Test Runner execution.
- Valid test-first RED: **3 passed, 16 failed**, exit 1, against the initial scaffold.
- GREEN: **19 passed, 0 failed**, exit 0, empty stderr.
- A .NET 10 attempt could not run Unity's net35 NUnit (`CallContext` TypeLoadException).
  An initial Mono compile also mixed Unity baseline references without their
  netstandard facade. Neither infrastructure failure is behavioral RED evidence.
- Baseline Unity run: blocked before tests, exit 21, because this project was open
  in another editor. No Unity test pass or complete regression-suite pass is claimed.

Local ignored verification harness/logs: `Temp/DailyLoadoutStage1/`.
Baseline coverage/limitations: `Assets/Tests/Editor/LoadoutBaseline/README.md`.
