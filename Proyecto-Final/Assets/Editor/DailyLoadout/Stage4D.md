# Stage 4D — empty equipment and committed HUD

Status: source implementation and static/model checks only. Native HUD authoring,
Unity suites and the manual Game flow have **not** run. Do not treat this as runtime
acceptance. No automatic commit/push, package installation or Editor restart.

## Install through Unity, not YAML

1. Open this project in Unity 2022.3.62f3 and let scripts compile.
2. Review/save relevant Canvas/Game edits and exit Play Mode and Prefab Mode.
3. Run **Tools → Magic Garden → Daily Loadout → Wire Equipment HUD (Stage 4D)**.
4. Run **Tools → Magic Garden → Daily Loadout → Validate Equipment HUD (Stage 4D)**.
5. Review the generated diff in `Assets/Prefabs/UI/Canvas/Canvas Game UI.prefab`.

The idempotent tool retains the first three existing nested seed/spell slots,
deactivates extras and trims controller references. It creates/replaces no slot,
art, transform or inventory resources. It refuses invalid/missing references,
dirty targets, Play Mode and Prefab Mode. It validates the Game scene's inherited
HUD too. No scene or prefab assets were directly edited for Stage 4D in this session.
Runtime controllers also bound presentation to the first three existing references;
this does not replace persistent authoring/validation or prove the saved Canvas.

## Automated native checks

From an empty scene, run EditMode and PlayMode suites, including:

- `DailyLoadoutStateTests` and `DailyLoadoutIntegrationTests`.
- `DailyLoadoutPresenterTests` and `DailyLoadoutPresenterPlayModeTests`.
- `DailyLoadoutEquippedHudTests` and `DailyLoadoutEquippedHudPlayModeTests`.
- Stage 4B/4C functional-prefab, world interaction, authoring and tutorial regressions.
- `GameplayAdmissionPlayModeTests`, `SeedDragLifecyclePlayModeTests`,
  `InventoryBaselineTests`, `SpellCooldownPlayModeTests` and existing upgrade suites.

Admission fixtures explicitly commit equipment; new-run fixtures do not. Updated
baseline numeric tests reject the old storage-index behavior. HUD pointer tests
assert no storage drag; standalone slot/storage API regressions remain separate.
Input event publication/pointer callbacks are not physical keyboard/mouse proof.
Source review also noted a pre-existing lifecycle concern: SpellSlotUI declares a
private Awake while ImprovedUIButton's private Awake installs the Unity Button
click bridge. Existing HUD tests invoke the custom OnClick downstream of that
bridge. Native acceptance must check actual Unity Button dispatch for assigned and
empty spell positions; no click-bridge runtime pass or confirmed new defect is claimed.

## Manual Game.unity acceptance

1. Start a new Run: the Day seed bar shows three empty positions and the spell bar
   is hidden. At Night only the spell bar is visible. The outgoing bar disappears
   immediately even during a fade: the bars must never overlap. Hidden bars cannot
   receive clicks. Resources/catalog unlocks remain available.
2. Open the Sacred Tree Loadout: Plant 1 is selected and the initial two unlocked
   plant options are visible immediately. Option status text is only `Available`
   or `Selected` (assigned in any draft position, including the current target).
   Hover/focus changes the visual sprite, never appends `Highlighted` or other
   debug state text. Equipment-position labels retain their detailed states.
   Preserve the approved art/layout.
3. Equip only one plant and one spell. Switch category/target; confirm selected
   highlight persists. All unlocked options remain visible and duplicates cannot
   silently move another assignment. Clear changes only its target.
4. Before confirming, verify the HUD is unchanged. Cancel/reopen and verify discard.
5. Reapply one plant/one spell and confirm: only those committed positions populate
   immediately; the remaining positions stay visibly empty. The daily allowance is
   consumed once, and confirmed reopening is read-only.
6. In Day use keys 1–3 and clicks for plants; in Night use them for spells. Empty
   positions cannot use a previous/unrelated item. Higher keys cannot select raw
   storage. Check Day Q/E abilities, Night Q/E equipment cycling and blocking modals.
7. Plant/cast through actual gameplay: verify real quantities, mana, stock admission
   and cooldown. At zero plant stock retain assigned identity/icon with count zero;
   replenish and verify usability without re-equipping. No draft affects resources.
8. Advance day: committed equipment persists. Unlock more content: catalog grows,
   equipment does not. Complete run reset: all six positions become empty again.
9. Repeat the Stage 4C F/Escape/range/reset/tutorial lifecycle and restoration checks.

## Evidence collected here

- Static C# compilation: ten affected assemblies, exit 0; warnings remain.
- Pure-model NUnit methods under bundled Mono: 19 passed, 0 failed, exit 0,
  empty stderr. Not Unity Test Runner; no UI/physics/player lifecycle proof.
- Logs and reused compiler/model harness: ignored `Temp/Stage4D-check/`.
- Native suites/manual flow/menu execution: pending; CLI reports no connected
  Editor endpoint. Existing open Editor was not restarted or competed with.
- Independent source verification: no confirmed severe Stage4D-caused defect;
  native acceptance remains unmet. No additional regression probes were applied.
- Preservation: 94 visual/settings hashes unchanged. Source/documentation whitespace
  check passed. Whole-tree whitespace check exited 2 on existing Canvas/TMP asset
  whitespace; those user-owned assets were left unchanged.
- Native review assessment: unavailable because intended-untracked scope requires
  declaration; no review lineage or approval is claimed.
