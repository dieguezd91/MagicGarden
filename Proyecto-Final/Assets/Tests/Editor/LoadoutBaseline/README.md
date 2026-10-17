# Stage 1 baseline characterization

These tests exercise the original public inventory/progression APIs. No production
inventory, gameplay, input, HUD, scene or prefab behavior is changed.

## Coverage

`InventoryBaselineTests` covers:

- Seed selection, repeated selection events and invalid selection.
- Consumption, final-seed removal, restocking and inventory notifications.
- Storage-reference swaps without selection transfer; rejected swaps.
- Existing-stock grants, full storage, retry after freeing storage, quantity additions.
- Spell selection, cycling, skipping locked entries and wraparound.
- Spell unlock selection/inventory event order.
- Inventory-owned cooldowns, selection independence, explicit lifecycle callback
  invocation and existing re-unlock behavior. Real frame advancement is covered by
  `Assets/Tests/PlayMode/LoadoutBaseline/SpellCooldownPlayModeTests.cs`.
- Spell reset preserving definitions/selection while clearing locks and cooldowns.
- Cumulative XP, level events, pending choices and reset event order.
- Unlock rebuild versus incremental level-up notifications and progression reset.
- Repeated day notifications, repeated night counters, weekly rollover and day reset.

Reflection follows the project's existing Assembly-CSharp testing convention.
Private access is limited to fixture setup and lifecycle dispatch. Assertions use
public methods, properties, events and returned public slot data. Fixtures restore
pre-existing singleton references and destroy only their own inactive objects.
The lifecycle case pre-matches the world phase to avoid triggering transitions in
an open user scene. It does not test world animation or the broad scene reset.

## Execution record

Baseline: `Develop-Testing` / `2a2d2ffc45e97d55e8b75db7db4d6d65e0427378`.

- Unity 2022.3.62f3 EditMode batch run attempted with
  `-runTests -testPlatform EditMode -testFilter InventoryBaselineTests`.
- Exit code **21**: another Unity instance already has this project open.
- **No Unity test executed. No passing baseline tests are claimed.**
- Log: `Logs/LoadoutStage1-baseline.log` (local, ignored).
- Source-only compilation results are recorded in the Stage 1 task log/final report;
  compilation is not Unity execution.

When the project is available, use **Window > General > Test Runner > EditMode**
and run `InventoryBaselineTests`. Run the existing `MagicGarden.Tests` assembly
as well. Do not close the user's editor or create a second project instance to
work around the lock without approval.

## Stage 2 fixture correction

The former `CooldownsBelongToOriginalSlotAndUpdateThroughUnityMessageDispatch`
failed because Unity does not deliver SendMessage to inactive objects. It is now
`CooldownsBelongToOriginalSlotAndNotifyThroughLifecycleUpdate`, invoking the
existing `Update` callback using the same fixture lifecycle helper as `Awake`.
Assertions retain the exact delta/clamp, original-slot notification, selection and
inventory-event checks; no production cooldown behavior was changed. EditMode
`Time.deltaTime` may be zero, so this test does not claim elapsed-frame coverage.
The separate PlayMode tests require actual frame decrease and expiry notifications.
Stage 2 execution status is recorded in `odd/tasks/daily-loadout-v2-stage-2.md`.

## Not covered by this fixture

Actual planting/casting animation, Fire's delayed callback, tutorial/UI interaction,
upgrade-modified cooldown scaling, automatic deferred delivery with UpgradeRuntime,
and scene-wide reset require later Unity integration verification. Existing upgrade
tests are preserved; they have not been executed as part of this blocked batch run.
