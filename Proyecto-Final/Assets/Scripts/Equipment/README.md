# Daily Loadout — equipment, admission and contextual selection

Equipment holds unlocked identities, not inventory locations or selected-item state.
Stage 4D uses the existing Seed/Spell HUD bars as committed-equipment hotbars.
It supersedes Stage 3's daytime raw-storage numeric selection and HUD storage drag.

## Ownership

| Boundary | Responsibility |
|---|---|
| `DailyLoadoutState<SeedsEnum, SpellDataSO>` | Six committed/draft positions, empty initialization, one atomic confirmation per genuine day |
| `DailyLoadoutSession` | Existing unlock owner's run/day coordination, resolver, admission and `CommittedChanged` notification |
| `EquipmentInventoryResolver` | Live identity-to-inventory lookup; no resource mutation |
| `EquipmentAdmissionPolicy` | Ready session, unlock, committed equipment, usable entry and category phase |
| `EquipmentGameplaySelection` | Stateless equipment-position translation to original inventory selection |
| Original inventories | Selected storage index, stock, storage swaps and cooldown ownership |
| Existing HUD controllers | Exactly three committed positions each; live counts, cooldown and resolved highlight |
| Original player/world systems | Readiness, mana, range, tiles, animation, planting/casting, upgrades and tutorial events |

The pure model remains in `MagicGarden.Loadout`; Unity adapters remain in
Assembly-CSharp. No singleton, equipment inventory copy or alternate session exists.

## Initialization and daily configuration

Once progression/unlocks are ready, `TryInitialize()` initializes six **empty**
positions. Initially unlocked items stay in the catalog and inventory delivery
remains unchanged. Unlocking never equips anything. Complete run reset initializes
empty again; a consecutive genuine day preserves committed identities.

Draft and confirmation require ready state and Day. Confirmation validates all
six assignments atomically, notifies committed observers synchronously on success,
and locks both draft setters for that day. Rejection changes neither committed
state nor allowance. Explicit empty assignments and partially filled configurations
are valid. Draft edits/discard do not emit `CommittedChanged` or affect the HUD.
Night discards drafts; the next genuine day restores editing. Day tokens use
`TotalNights`, not the repeating displayed day number. Repeated day notifications
cannot renew the allowance. Session disposal releases observers.

## Identity, resources and eligibility

Resolve identities again after storage changes; do not cache inventory indices.
Plants require positive stock and a prefab for gameplay admission. Depletion clears
an inventory entry but not its committed assignment: the HUD retains catalog identity/
icon with quantity zero. Replenishment uses the existing inventory and needs no
Loadout edit. Equipment editing does not grant, consume, move or replace resources.

Spells use their original ScriptableObject and configured inventory SlotIndex.
Resolution retains type/prefab/name/icon/mana/cooldown/upgrade-key validation and
rejects conflicting unlocked mappings. The original inventory entry owns cooldown;
the HUD reads its current effective cooldown ratio, not a copied timer.

`session.Admission` returns `Allowed`, `SessionUnavailable`, `WrongPhase`,
`EmptyPosition`, `Locked`, `Unequipped` or `Unavailable`. Plants require Day; spells
require Night. A disposed/uninitialized session or incomplete unlock rebuild fails
closed. Admission does not select, spend, start cooldowns or emit success events.
An equipped spell on cooldown or without mana stays selectable; the original
casting controller still enforces readiness.

## One input route for numbers and clicks

```text
InputReader.OnSlotSelected (plants 1–3 / spells 4–6) / existing HUD slot click
  -> PlayerAbilitySystem.TrySelectEquipmentPosition
  -> EquipmentGameplaySelection.TrySelectPosition
  -> resolve committed position 0..2 to current inventory entry
  -> original inventory SelectSlot and selection events
```

- Day keys 1–3 select equipped plants; Night keys 4–6 select equipped spells.
- Keys from the inactive category are ignored. HUD clicks keep their category-local positions.
- Higher numeric positions are rejected regardless of storage capacity.
- An empty position is ignored: it cannot select itself or clear the current selection,
  and emits no selection event. Unavailable assigned equipment still clears that
  category's raw selection to `-1`. Invalid positions and an
  unavailable session fail without selecting storage content.
- HUD clicks use the same route and player/pause/upgrade/action gates as keys.
- Day Q/E retains ability cycling. Night Q/E wraps committed spell order and skips
  empty/invalid assignments. General inventory storage APIs remain separate.
- Equipment HUD dragging is disabled: it cannot reorder storage or equipment.
  Standalone SeedSlot storage presentation/callbacks and `SeedInventory.SwapSlots`
  remain available; they do not grant equipment admission.
- Only the seed HUD is visible in Day; only the spell HUD is visible in Night.
  The outgoing bar hides immediately and stops receiving input; only the incoming
  bar fades in, so the two never overlap. Other phases hide both bars.
  No runtime slot/hierarchy generation or numeric-key polling was added.

## Execution and payment boundaries

Planting still checks live admission before action/payment and after its existing
40% animation wait. It captures slot identity, prefab and actual payment. A storage/
selection swap cannot plant the captured prefab while charging a different seed.
Mana is paid before animation, retaining ordinary cancellation/invalid-soil costs.
Only delayed admission rejection restores the action's payment via `SetMana` rather
than lunar-bonus `AddMana`. Existing mana clamping/notifications remain authoritative.
Accepted planting retains world, stock, sound, action, particle and tutorial paths.

Casting rechecks admission at readiness and execution, before payment/effects/
cooldown/success events. Existing upgrades, movement penalty, sounds, spells and
teleport behavior remain unchanged. FireSpell receives its original inventory index
and starts that entry's cooldown when its stream ends despite later selection or
equipment changes.

## Verification and native installation

See `Assets/Editor/DailyLoadout/Stage4D.md` for the explicit Unity-native authoring
menus, regression suites and Game.unity checklist. Run native suites from an empty
scene so fixtures do not mutate an open gameplay world.

Current evidence: ten affected runtime/Editor/test assemblies compiled, exit 0
(with compiler warnings). All 19 updated pure-model NUnit methods passed under
Unity's bundled Mono, exit 0 and empty stderr. This is **not Unity Test Runner**.
Updated admission/drag baseline suites and new committed-HUD tests are compiled,
not executed. No connected Unity Editor endpoint is available, and the menus and
manual flow have not run. Stage 4D runtime acceptance therefore remains pending.

Historical asset correction retained: SeedInventory prefab's second initial entry
uses identity 7 for Thorned Tendrils; initial quantities and ordinary unlock grants
are not changed by Stage 4D. Existing author visuals/GUIDs are preserved; no scene
or prefab YAML was edited for this stage.
