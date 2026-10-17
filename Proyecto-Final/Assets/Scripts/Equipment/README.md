# Daily Loadout — Stage 2 integration boundary

This layer coordinates equipment configuration only. It does not select inventory
items or restrict existing planting/casting/input/UI. No scene/prefab changes or
new singleton are required.

## Ownership and assemblies

- `DailyLoadoutState<SeedsEnum, SpellDataSO>` remains engine-independent in
  `MagicGarden.Loadout`; its source is unchanged from Stage 1.
- `DailyLoadoutSession` and `EquipmentInventoryResolver` live here in
  **Assembly-CSharp**, outside the pure model's asmdef directory.
- `PlayerContentUnlockSystem.Start` creates its ordinary `Loadout` session before
  the existing initial rebuild. `OnDestroy` disposes the session subscriptions.
- `HasInitializedUnlockState` is false while rebuilding and when progression is
  missing. It becomes true only after the existing rebuild and seed-delivery work,
  before the existing `OnUnlockStateRebuilt` notification. Existing notification
  counts/order and unlock authority are preserved.

## Resolution, not selection

`ResolvePlant(SeedsEnum)` and `ResolveSpell(SpellDataSO)` report separately:

| Property | Meaning |
|---|---|
| `IsUnlocked` | Existing unlock authority recognizes this identity |
| `IsEquipped` | Identity is in committed equipment |
| `HasUsableInventoryEntry` | A current, valid inventory entry can be resolved |
| `Slot` | Original inventory-owned slot object, or null |
| `InventoryIndex` | Ephemeral resolved location, or -1; never an equipment identity |

A plant can be unlocked and equipped with no stocked entry. Each query scans the
current seed slots by `SeedsEnum`, ignores empty/depleted/missing-prefab entries,
and returns the first matching stocked candidate. This is a lookup, not a new
selection/consumption policy. Depletion and swaps cannot erase assignments;
subsequent replenishment is visible without an equipment edit.

Spell identity is the original SO reference. Its configured SlotIndex locates the
candidate, but is never stored as the assignment identity. Resolution validates
unlocked slot status, type, prefab, name, icon, base mana/cooldown and upgrade key
against the current SO. Conflicting unlocked SO mappings to the same index fail
closed. No persistent identity scheme is introduced.

For spells, a usable entry means **valid storage/configuration**, not permission
to cast now. Cooldown, mana, phase and other gameplay checks still belong to the
existing controllers. `Slot` is the live original cooldown owner; no cooldown
value or timer is copied into equipment. Results must not be cached across
inventory/configuration changes. Ordinary queries emit no inventory events and
never call selection, grant, consumption or unlock methods.

## Bootstrap and lifecycle

1. Wait for completed unlock readiness. Missing progression is not treated as an
   empty ready catalog; a genuinely empty configured catalog may bootstrap empty.
2. Traverse progression by ascending level up to the player's current level, then
   serialized list order within each level. Filter through existing membership.
   The model chooses the first three distinct identities per category.
3. Bootstrap once per run, regardless of stock, without spending confirmation or
   invoking any unlock/grant/rebuild operation. Later unlocks/rebuilds do not
   replace committed equipment.
4. Observe `OnNewDay` using **TotalNights**, never displayed day, as the run-local
   token. Repeated notifications preserve drafts and the used allowance. Initial
   bootstrap reconciles an already-established Day if Start ordering missed its
   first notification.
5. Observe Night phase to discard drafts and close configuration. Day phase alone
   does not grant a new allowance: GameOverController can set Day before reset.
6. Draft/confirm APIs require current Day, initialized state and ready unlock data.
   They validate unlock membership without requiring stock. Confirmation rechecks
   all draft identities; rejection leaves committed equipment/allowance untouched.
   Empty plant (`SeedsEnum.None`) and spell (`null`) requests clear positions;
   duplicate assignments remain rejected by the pure model.
7. At the end of the existing `GameResetController.ResetGame` sequence, reset the
   existing owner's session and bootstrap from the now-rebuilt starting unlocks.
   This explicit run boundary allows token reuse even though TotalNights is not
   reset by ResetDayCount. A progression rebuild by itself is not a run reset.

## Tests and execution limits

- EditMode: `DailyLoadoutIntegrationTests` (13 tests), original baseline fixtures,
  and the 19 pure-state tests. Integration fixtures use inactive objects, restore
  singleton references and dispose subscriptions. They skip rather than animate
  an existing world; the broad reset case also requires no pre-existing enemies,
  spells or mana object.
- PlayMode: `SpellCooldownPlayModeTests` (2 tests) uses actual active inventory
  Awake/Update frames to verify decrease, original-slot notifications and expiry.
- The original inactive SendMessage fixture is corrected using explicit callback
  dispatch; its delta/notification assertions remain intact.
- Actual execution/compilation evidence is recorded in
  `odd/tasks/daily-loadout-v2-stage-2.md`; test presence or source compilation is
  not a Unity runtime pass.

## Explicit non-goals

No gameplay admission, number-key/Q/E routing, second selection authority, HUD,
tooltip/tutorial changes, inventory replacement, planted-entity changes, save
migration or asset wiring. Those require a separately approved Stage 3.
