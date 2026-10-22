# Author and inspect the Daily Loadout UI

Stage 4A prefabs already exist and contain user-reviewed visual adjustments. **Do not regenerate them.** Stage 4B adds a session-backed presenter and additive option-pool authoring; see [functional integration](Stage4B.md). Neither workflow modifies the Game scene, restoration panel, HUD or admission rules.

## Maintain the existing assets

The one-off Stage 4A generator and manual isolated-harness menu have been retired.
Edit the existing prefabs in Unity Prefab Mode; do not regenerate or replace their
GUIDs or approved art. The reference layout revision below is explicitly authorized;
do not use older generators to overwrite it. The additive Stage 4B binding helper remains because
isolated tests use its Configure method. Stage 4C/4D wiring and validation tools
remain while native installation/regression checks are pending.

Existing assets:
- `Assets/Prefabs/UI/DailyLoadout/LoadoutSlot.prefab`
- `Assets/Prefabs/UI/DailyLoadout/LoadoutOption.prefab`
- `Assets/Prefabs/UI/DailyLoadout/DailyLoadoutPanel.prefab`

See [Stage4C.md](Stage4C.md) for Sacred Tree wiring and
[Stage4D.md](Stage4D.md) for committed HUD installation and current acceptance.

## Generated hierarchy

```text
DailyLoadoutPanel [Image, DailyLoadoutPanelView]
└── Stone window [Image: existing restoration window]
    ├── Title
    ├── Close [Button: existing restoration exit sprites]
    ├── Daily allowance badge / Daily allowance [TMP]
    ├── Main stone table [Image]
    │   ├── Plants category / Spells category [TMP]
    │   ├── Plant 1 / Plant 2 / Plant 3 [nested LoadoutSlot]
    │   └── Spell 1 / Spell 2 / Spell 3 [nested LoadoutSlot]
    ├── Available panel [existing stone sprite]
    │   ├── Available heading / Catalog instruction [TMP]
    │   ├── Plants tab / Spells tab [Button]
    │   ├── Plant options panel [active by default]
    │   └── Spell options panel [inactive by default]
    │       └── Options scroll / Viewport / Content [six authored options per category]
    ├── Selected position panel [position, equipment icon/name, state, draft feedback]
    ├── Clear position [Button, TMP]
    ├── Cancel draft [Button, TMP]
    └── Confirm [Button, TMP]

LoadoutSlot / LoadoutOption [Image, Button, view]
├── Icon frame [existing inventory slot sprite]
│   └── Icon [Image, preserve aspect; hidden when absent]
├── Equipment name [TMP]
├── Visual state [TMP]
└── Position [TMP; slots only]
```

Stage 4B's **Bind functional UI (preserve visuals)** command retains its additive integration role. The reference revision uses **Apply reference category layout** (`DailyLoadoutLayoutAuthoring.BindPrefab`) to reorganize the existing native prefab without replacing assets or the six equipment references. It preserves the original six plant option instances and authors a separate six-instance spell pool. Each category has an independent ScrollRect panel with its authored column configuration. Tabs and equipment-position clicks select the matching panel; hidden panels cannot intercept pointer input. Changing category cancels an unfinished drag from the hidden panel. Runtime never instantiates UI; closing still discards the draft and Confirm remains the only commit operation.

## Presentation surface

### Catalog cards and conditional scrolling

Catalog cards display **only their name and icon**. The existing serialized state-label object is kept inactive so refresh, hover, confirmation and read-only transitions never add an extra caption. Equipment-position labels are unchanged.

`LoadoutOptionGridLayout` takes the preferred dimensions/aspect from the authored option prefab (currently square), shrinking only when necessary to fit the category's configured columns. It no longer forces every card to 120px high or stretches a small square card across an entire column. Each category retains its manually authored column count, padding, spacing and alignment.

Vertical scrolling is derived from visible grid participants, row count, card height, padding and spacing relative to the viewport. When everything fits, wheel/body scrolling is disabled, inertia stops and content returns to the top. When rows overflow, scrolling is enabled again. An assigned vertical scrollbar follows the same rule; none is added automatically. No UI components or parallel visibility state are created at runtime.

Use **Refine catalog cards (preserve manual layout)** for this narrow native migration. It edits only card internals and layout bindings, without invoking the reference-layout migration or repopulating preview data. Do not rerun the broader reference-layout command over subsequent manual edits unless you explicitly intend to reset that layout.

### Prefab preview without Play Mode

All option objects, TMP labels, images, buttons and both catalog panels are serialized in the prefab. Runtime **does not build** these components; it updates their existing content, state and visibility from the real session.

The saved panel now contains visible catalog examples with real existing plant/spell names and icons, populated equipment examples and selected-position detail. These are **Editor preview data only**, not starting equipment, unlocks or committed assignments. Opening the real session replaces them, including hiding unused catalog capacity and restoring genuinely empty positions.

Open `DailyLoadoutPanel.prefab` in Prefab Mode to edit its authored components directly. Plants are visible by default; to inspect spells, disable `Plant options panel` and enable `Spell options panel` in the hierarchy. Both panels already contain all six authored options, including their text and image components. The intentional ScrollRect mask still applies; inspect or frame individual rows in the hierarchy when reviewing content outside the viewport.

To explicitly refresh examples from existing catalog assets, outside Play Mode and after saving/closing Prefab Mode use **Tools → Magic Garden → Daily Loadout → Populate authored prefab preview**. This only fills existing components and changes no geometry. It never runs on import, `OnValidate`, `Awake` or `OnEnable`, and never writes gameplay state. Applying the reference layout also finishes with this populated preview instead of hiding/blanking the authored options. Ordinary prefab edits are not automatically regenerated.

| View | Supplied state or reference |
|---|---|
| Slot and option | `SetContent(Sprite, string)`, `SetAssigned`, `SetSelected`, `SetHighlighted`, `SetInvalidDropTarget`, `SetReadOnly`, `SelectButton` |
| Slot | `SetPositionLabel` |
| Option | `SetLocked`; empty or locked options cannot be selected |
| Panel | `PlantSlots`, `SpellSlots`, `OptionPrefab`, `OptionContent`, `OptionsScroll`, `ConfirmButton`, `CancelButton`, `ClearButton` |
| Panel | `SetAllowanceMessage`, `SetOptionsEmpty`, `SetActionAvailability` |

Content and assignment are separate presentation inputs: assigning a sprite/name does not equip it. Equipment-position state labels contain exactly one of **Selected**, **Available**, or **Empty**: assigned/selected takes precedence, otherwise populated is Available and unpopulated is Empty. A selected empty target therefore reads Selected. Catalog cards do not display a state label at all. Hover, focus, valid/invalid drops and locked/read-only availability retain their visual and input behavior but never add written state suffixes. Long names use wrapping/ellipsis within the authored bounds. The selected-position detail and daily allowance badge keep transaction messages separate from equipment state.

Confirm and Clear start disabled and persistent Button callbacks remain empty. The presenter binds runtime listeners, enables actions from the session, and unbinds only its own listeners on close/disable/destroy. Views forward click/drop intent and render feedback; equipment rules remain in the existing session/state.

## Icon drag and drop

Drag an available option's **icon frame** onto a compatible table position, without selecting the destination first. Clicking an option still assigns it to the selected position. Dragging the card body scrolls the catalog; wheel scrolling remains available over both areas. The production ScrollRect currently has no vertical Scrollbar reference, and this change does not add one.

Click and drop share identity-based assignment through the existing session setters. A drag captures identity/category/session, never a reusable catalog index. Cross-category, duplicate, unavailable and read-only drops cannot modify assignments. Occupied targets may be replaced; Clear Position remains the removal action. Equipped positions are not drag sources.

`DailyLoadoutDragAuthoring.BindPrefabs` adds the source to the existing option icon frame and one initially inactive `Drag Preview` to the existing panel. It preserves artwork/layout and is idempotent; never regenerate the approved panel to add dragging. The preview is reused, non-raycastable and outside the options mask. Runtime creates or destroys no preview hierarchy.

Presenter refresh reapplies transient compatible/hovered/invalid feedback. Cancel, Escape, disable/destroy, modal/phase changes, Run reset, session replacement/disposal, unlock loss and consumed allowance release the drag and its subscription. Closing retains the existing draft-discard behavior.

Run `DailyLoadoutDragTests` (EditMode) and `DailyLoadoutDragPlayModeTests` (PlayMode), then existing Daily Loadout/UI/equipment suites. Tests dispatch uGUI events through authored source/target components and verify native raycast/ScrollRect routing. The explicit `DailyLoadoutDragRenderTests` requires native graphics and writes both category panels and normal/valid/invalid synthetic states at two resolutions to `Results/LayoutRenders`; it is not manual acceptance. `DailyLoadoutLayoutTests` covers exclusive state text, independent panels, tabs, draft isolation, detail projection, subscriptions and native authoring idempotence.

Run the explicit render fixture in its own Editor process, separately from EditMode assertions: a mixed native run reported destroyed TMP material references after the render fixture returned from Play Mode. Fresh-process suites provide the functional proof; this runner interaction is not a claimed gameplay defect or a suppressed expected log.

Manual Game acceptance remains: Sacred Tree via F, both category tabs, Plant3/Spell2 drops without preselection, replacement, incompatible/duplicate/outside drops, scrolling, Cancel/reopen, Confirm/HUD projection and read-only reopening. Check pointer feedback at1920x1080 and1280x720 with physical input.

## Reused assets

All sprites come from existing `UISprites.png` (GUID `6b876b38e4597704b84b5e76e54d27a1`). Keep Point filtering and the existing sprite identities when editing assets.

| Use | Existing local sprite IDs |
|---|---|
| Window/table | `1488800299` |
| Small stone normal / hover / pressed | `924556296` / `-236189874` / `-1569556968` |
| Exit normal / hover / pressed | `774320644` / `726165551` / `-1595310454` |
| Icon frame, also used by existing seed/spell slots | `-1001860351` |
| TMP font | existing `m5x7`, GUID `52be7e01f8fbce54186eac8da2da70cc` |

No new art, materials, fonts, packages, singleton, inventory or restoration scripts are required.

## Verify in Unity

Run **Window → General → Test Runner → EditMode → DailyLoadoutUITests** against the existing prefabs. Missing prefabs deliberately fail rather than skip. Tests cover serialized references, six positions, callback isolation, state transitions, original art identity and grid overflow geometry.

Then run the existing EditMode suite and the existing Daily Loadout PlayMode tests. Do not treat earlier Stage 1–3 results as verification of these changes. Batch-mode testing requires closing the project in the interactive Editor first; do not launch another Editor on the same open project.

For batch execution after closing the Editor (PowerShell from the repository root):

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
$project = Join-Path $PWD 'Proyecto-Final'
# Pick a fresh output directory for each run; do not overwrite previous evidence.
$out = Join-Path $project ('Logs\DailyLoadoutUI-' + (Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Path $out | Out-Null
& $unity -batchmode -projectPath $project -runTests -testPlatform EditMode `
  -testResults "$out\editmode.xml" -logFile "$out\editmode.log"
# Separate invocation/output directory for PlayMode; inspect XML failures and logs.
```

### Visual checklist — still required

Use an **isolated, unsaved test scene**, not the Game scene. Add a Screen Space Overlay Canvas with CanvasScaler (Scale With Screen Size, reference 1920×1080, match .5), GraphicRaycaster and EventSystem. Add the panel beneath it. Do not save this setup into any existing scene/prefab.

- [ ] Inspect at 1920×1080 (existing reference), 1280×720 and 2560×1440; also probe 1024×768 for narrower aspect ratio. These are proposed checks, not a claim that all are certified project-supported resolutions.
- [ ] Six positions and category headings are visible; icon, position, name and status do not overlap.
- [ ] Existing stone art, font and Point-filtered sprites look correct at actual Game-view scale; check border stretching and pixel clarity manually.
- [ ] Populate 24 preview options with plant/spell icons and long names; all columns stay within the viewport and scrolling reaches the final row. Intended vertical masking must not hide any row permanently.
- [ ] Exercise assignment, selection, hover/focus, invalid drops, read-only and locked/unlocked input states. Written equipment status must remain exactly Selected, Available or Empty, never a combination.
- [ ] Confirm/clear enablement is externally supplied; clicking controls alone causes no gameplay changes or false confirmation.
- [ ] Allowance text accommodates both available and consumed messages.
- [ ] Verify the restoration panel, Restore folder, HUD prefabs and Game scene have no diff.

**Verification:** Stage 4A assets are present and were visually reviewed by the user. Stage 4B verification is recorded separately in [Stage4B.md](Stage4B.md); do not treat historical results as proof of the new integration.

## Next step

Use the retained [Stage 4B binding/test support](Stage4B.md) when needed. Current altar/HUD wiring, tests and manual acceptance are documented in [Stage4C.md](Stage4C.md) and [Stage4D.md](Stage4D.md).
