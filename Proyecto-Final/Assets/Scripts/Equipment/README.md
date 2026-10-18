# Daily Loadout — equipment, admission and contextual selection

Equipment holds unlocked identities, not inventory locations or selected-item state.
Stage 3 adds read-only gameplay admission and contextual input while the original
inventories and controllers retain storage, resources, effects and events.

## Ownership

| Boundary | Responsibility |
|---|---|
| `DailyLoadoutState<SeedsEnum, SpellDataSO>` | Pure committed/draft assignments, one confirmation per genuine day |
| `DailyLoadoutSession` | Existing unlock owner's run/day coordination; owns resolver and admission policy |
| `EquipmentInventoryResolver` | Live identity-to-inventory lookup, without selection or mutation |
| `EquipmentAdmissionPolicy` | Ready session, unlock, committed equipment, usable entry and category phase |
| `EquipmentGameplaySelection` | Stateless translation to original inventory `SelectSlot` calls |
| Original inventories | Selected index, stock, storage swaps, cooldown ownership and notifications |
| Original player/world systems | Readiness, mana, range, tiles, animations, planting/casting, upgrades and tutorial events |

The pure model remains in `MagicGarden.Loadout`. Unity adapters live in
Assembly-CSharp; no existing production source moved assemblies. No new singleton,
scene wiring, prefab changes, HUD layout or Loadout menu is introduced.

## Identity resolution and eligibility

`ResolvePlant(SeedsEnum)` / `ResolveSpell(SpellDataSO)` separately report unlock,
equipment and usable-entry state, plus the original slot and its **current** index.
Never cache these indices across storage changes. Plants require positive stock
and a prefab. Depletion/swapping cannot erase committed assignments; replenishment
is visible without an equipment edit.

Spells use the original SO reference and its existing configured SlotIndex. Resolver
validation retains type, prefab, name, icon, base mana/cooldown and upgrade-key
matching. Conflicting unlocked SO mappings fail closed. The result's slot is the
original cooldown owner, not a copy.

`session.Admission` returns `EquipmentAdmissionResult` and a live resolution:
`Allowed`, `SessionUnavailable`, `WrongPhase`, `EmptyPosition`, `Locked`,
`Unequipped`, or `Unavailable`. Plants require current Day; spells require current
Night. A disposed/uninitialized session or incomplete unlock rebuild fails closed.
Admission does not select, spend, start cooldowns, instantiate or emit inventory
notifications. Spell eligibility is **not casting readiness**: an equipped spell
on cooldown or without enough mana remains selectable, as before; the original
casting controller enforces readiness.

## Daily configuration

The existing unlock owner creates its session before initial rebuild and disposes
it on destruction. Bootstrap waits for ready progression/unlocks, traverses levels
and serialized catalog order, and equips up to three distinct identities per
category without granting stock or consuming confirmation. Later unlocks never
replace equipment automatically.

Draft and confirmation require ready state and Day. Confirmation revalidates all
six assignments before committing atomically. Success locks further draft edits;
rejection leaves committed state and allowance unchanged. Discard cannot unlock a
confirmed day. Night discards drafts; the next genuine day restores editing with
committed equipment intact. `OnNewDay` uses `TotalNights`, not the repeating display
day, so repeated notifications cannot renew the allowance. The existing full-run
reset completion resets this session and bootstraps starting equipment again.

## One numeric input route — LD-06 / LD-07

```
InputReader.ReadSlotKeys (top row or keypad)
  -> OnSlotSelected (zero-based)
  -> PlayerAbilitySystem.HandleEquipmentSlotInput
  -> Day: SeedInventory.SelectSlot(input index), bounded by PlantSlotsCount
  -> Night: EquipmentGameplaySelection.TrySelectPosition
  -> original inventory SelectSlot -> original selection events
```

- Day: key N selects seed storage slot N, regardless of equipment order, unlocks,
  stock or whether that slot is empty. The inventory's actual capacity bounds it.
  Selection does not grant planting permission; execution still checks admission.
- Night: keys 1–3 select equipped spell positions 1–3; empty/unavailable equipment
  and positions outside 0–2 do nothing. This spell path is unchanged.
- Player `CanAct`, pause, upgrade blocking and active digging/harvesting still
  guard both paths. Equipment assignments remain unchanged by storage selection.
- Both HUD controllers no longer poll numeric keys. `InputReader` publishes the
  same zero-based event once per frame, for top-row/keypad keys1–9. The ninth key
  restores the former seed-HUD range; invalid storage indices are ignored.
- `EquipmentGameplaySelection` still offers explicit equipment-position queries;
  its plant-position helper tests do not define daytime keyboard semantics.
- LD-07 removes seed double-press keyboard swapping without a replacement mode.
  Mouse selection and existing drag/drop still use original inventory APIs.
  Selecting unequipped content through those APIs does not grant execution rights.
- Day Q/E retains ability cycling. Night Q/E uses equipped order, wraps in the
  existing direction, skips empty/invalid entries and calls `SpellInventory.SelectSlot`.
  General `SpellInventory.CycleSpell` is unchanged.

## Execution guards and resource behavior

### Planting

`PlayerAbilitySystem.HandlePlanting` rejects equipment/phase before starting an
action or paying mana. It retains the original range, plant-limit and mana checks.
The action captures its selected slot object, seed identity, prefab and actual
payment. After the existing 40% animation wait, it revalidates live equipment,
phase, stock and selected-slot identity **before** `TilePlantingSystem.TryPlant`.
Storage/selection changes cannot make it plant the captured prefab while charging
another seed.

Mana is still paid before animation, preserving normal successful timing and
unrelated ability-cancellation/invalid-soil costs. Only delayed admission rejection
restores that action's actual payment using `ManaSystem.SetMana`; using `AddMana`
would incorrectly apply lunar gain bonuses. Clamping/notifications remain owned
by ManaSystem. There is no reservation subsystem. Concurrent regeneration or a
changed mana cap can clamp restoration; this needs real runtime validation.

Accepted world commits still use the original TryPlant, seed consumption, sound,
day-action, particles and tutorial paths. Previously planted entities are never
queried by admission. Repository caller search found one TryPlant caller, the
player coroutine; the world API remains unchanged rather than inventing identity
or resource rules for potential non-player callers.

### Casting

`PlayerSpellController.CanCastSpell` checks live session admission for the selected
inventory index before the attack animation and from `ShootFromHand`.
`CastSpell` also checks readiness/admission at its execution boundary. Wrong-phase,
unequipped, invalid, cooldown and insufficient-mana rejection precede spending,
instantiation, cooldown and successful-cast tutorial events.

Accepted casting retains the existing parameters, upgrade descriptor, effects,
movement penalty, sounds and cooldown code. FireSpell still receives the **original
inventory index** and starts that entry's cooldown when its stream ends, even if
selection/equipment changes. No spell subclass or teleport production code changed.
The independent teleport ability remains outside Loadout admission.

## Verification and remaining proof

Run from an **empty scene** in Unity 2022.3.62f3 Test Runner (fixtures must not
mutate a loaded user gameplay world):

1. **EditMode — Run All**, including `DailyLoadoutStateTests`,
   `DailyLoadoutIntegrationTests`, `InventoryBaselineTests` and existing upgrade suites.
2. **PlayMode — Run All**, including `SpellCooldownPlayModeTests` and
   `GameplayAdmissionPlayModeTests`.
3. In a gameplay scene, check Day top-row/keypad1–9 against storage bounds,
   Night1–3 against spell equipment (higher keys rejected),
   day/night Q/E, modal/pause/player blocking, mouse selection and real drag/drop,
   hand-animation events, planting interruption and tutorial advancement.

Stage 3 and its regression corrections add eleven EditMode cases and twenty-three
PlayMode cases and revise the confirmation-lock case. The original EditMode drag
coverage invokes swap completion only. The six new `SeedDragLifecyclePlayModeTests`
use a real Canvas, GraphicRaycaster and EventSystem through public SeedSlot drag
callbacks; they check first/repeated occupied and empty swaps, rejected/cancelled
drags, end-without-begin, disable cleanup and current icon/count state. These tests
have not yet executed in Unity and do not claim physical mouse verification. PlayMode
uses real frames/coroutines and production event subscribers but explicitly
publishes InputReader events; it is not OS-key or animation-clip wiring proof.

Observed here: all seven source assemblies compile, and the 19 pure-model NUnit
methods pass under Unity's bundled Mono (not Unity Test Runner). Independent
verification found an ordinary seed-selection cancellation refund regression;
the correction separates equipment eligibility from delayed selected-slot binding,
and its targeted source recheck is resolved. Three new runtime probes cover
selection-cancellation costs, windup depletion and exact single-payment restoration.
The independent scratch compilation timed out copying the open project's files,
then failed on missing package DLLs; it did not establish a source compile failure.
Native risk assessment was unassessable because intended-untracked scope was not
declared; no native review or authority acknowledgement occurred. Unity Editor was
already active, so no batch execution was attempted. **New Unity suites and actual
runtime input/animation/drag verification have not run. Stage 3 is not verified
complete.** The user's 101/101 EditMode and 2/2 PlayMode results cover the published
Stage 1/2 baseline, not this candidate. See `odd/tasks/daily-loadout-v2-stage-3.md`.

## Stage 3 regression corrections (runtime verification pending)

The second initial entry in `Assets/Prefabs/UI/SeedInventory.prefab` now uses
identity 7, matching its Thorned Tendrils data GUID
`f63882e6bad5d994d989aa1cbe877347`, instead of identity 1. The other initial
identities remain 14 and 3; all three quantities remain 3. Game overrides capacity
to seven but inherits these contents; no Game/Menu content override was edited.
No runtime synchronization or unlock-grant suppression was added. A serialized
asset check failed before the correction and passed afterward; this is structural
evidence, not a Unity test result.

The previous mismatch let the ordinary one-seed unlock delivery create a real
identity-7 entry sharing the second entry's icon. SwapSlots itself still only
exchanges entry references and emits its original notification. Whether delivery
was deferred to the first drag in the reported session remains unobserved.

Drag cleanup now clears source state on success, rejection, cancellation and
HUD disable. Rendering is refreshed from current inventory after cleanup; empty
slots clear stored sprites/text rather than exposing stale contents. There are
no inventory, planting/casting, progression or numeric-routing changes in this
correction.

The latest user correction supersedes the earlier Day equipment-position rule:
seed keys1/2/3 must select storage indices0/1/2, not the equipment-resolved
indices2/1/none. This remains true after swaps and with3 or9 storage slots.
The original mismatch/grant correction above is retained. Three new PlayMode
cases exercise capacities3/9, post-swap/disposed-Loadout selection and rejected
planting from unequipped/empty numeric selections; the mixed Day/Night numeric
test now expects direct Day storage selection while preserving its Night checks.
These tests publish input events, not physical keypresses.
In the active Editor, record physical keys1–9, selected before/after, original
selection event and HUD highlight, then separately check planting admission.
Run EditMode and PlayMode Run All from an empty scene and repeat the affected
manual checklist while retaining the seven previously validated checks.
No new Unity results are claimed here. Recovery specification:
`odd/tasks/daily-loadout-v2-stage-3-regressions.md`.

## Proposed Stage 4 (not implemented)

After Stage 3's real Unity results pass, separately approve an equipment HUD/menu
that displays three identities per category with live availability and current
selection derived from the inventories. Draft editing should use session APIs,
show the consumed daily allowance, and confirm atomically. Decide click semantics,
empty/depleted visuals and storage-versus-equipment presentation before changing
layouts, scenes or prefabs. Keep storage drag/drop separate and never move resource
or cooldown ownership into that UI.
