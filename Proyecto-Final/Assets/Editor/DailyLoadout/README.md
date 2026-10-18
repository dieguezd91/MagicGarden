# Author and inspect the Daily Loadout UI

Stage 4A supplies presentation components and a native, explicit prefab generator. **The prefabs do not exist until you run the menu below.** This workflow never modifies the Game scene, restoration panel, HUD, equipment state or admission rules.

## Generate once

1. Let Unity 2022.3.62f3 refresh and compile the new scripts. Resolve any Console compilation errors before proceeding.
2. Select **Tools → Magic Garden → Daily Loadout → Create UI prefabs (once)**.
3. Confirm these persistent assets, their `.meta` files and all serialized references exist:
   - `Assets/Prefabs/UI/DailyLoadout/LoadoutSlot.prefab`
   - `Assets/Prefabs/UI/DailyLoadout/LoadoutOption.prefab`
   - `Assets/Prefabs/UI/DailyLoadout/DailyLoadoutPanel.prefab`
4. Open the panel in Prefab Mode. There must be three Plant positions and three Spell positions, with separate category headings.

The generator resolves existing art by GUID/local sprite ID only in Editor code. Runtime views use serialized references exclusively. A second invocation refuses to overwrite existing prefabs; customize generated assets in Prefab Mode. Existing importer settings and assets are never changed. Unity generates metadata natively during refresh/save; retain those `.meta` files with the assets. If generation fails, only this invocation's new prefabs are rolled back.

## Generated hierarchy

```text
DailyLoadoutPanel [Image, DailyLoadoutPanelView]
└── Stone window [Image: existing restoration window]
    ├── Title
    ├── Close [Button: existing restoration exit sprites]
    ├── Daily allowance [TMP]
    ├── Main stone table [Image]
    │   ├── Plants category / Spells category [TMP]
    │   ├── Plant 1 / Plant 2 / Plant 3 [nested LoadoutSlot]
    │   └── Spell 1 / Spell 2 / Spell 3 [nested LoadoutSlot]
    ├── Available heading [TMP]
    ├── Options scroll [ScrollRect: vertical, clamped]
    │   └── Viewport [Image, RectMask2D]
    │       ├── Content [GridLayoutGroup, ContentSizeFitter, LoadoutOptionGridLayout]
    │       └── Empty options [TMP]
    ├── Clear position [Button, TMP]
    └── Confirm [Button, TMP]

LoadoutSlot / LoadoutOption [Image, Button, view]
├── Icon frame [existing inventory slot sprite]
│   └── Icon [Image, preserve aspect; hidden when absent]
├── Equipment name [TMP]
├── Visual state [TMP]
└── Position [TMP; slots only]
```

Options are **not** populated from gameplay. The empty panel is intentional. Content is a responsive three-column grid with vertical overflow handled by the scroll viewport, not additional rows extending outside the panel. The option prefab is a serialized asset reference ready for a future presenter. No preview items are saved into the production panel.

## Presentation surface

| View | Supplied state or reference |
|---|---|
| Slot and option | `SetContent(Sprite, string)`, `SetAssigned`, `SetSelected`, `SetHighlighted`, `SetInvalidDropTarget`, `SetReadOnly`, `SelectButton` |
| Slot | `SetPositionLabel` |
| Option | `SetLocked`; empty or locked options cannot be selected |
| Panel | `PlantSlots`, `SpellSlots`, `OptionPrefab`, `OptionContent`, `OptionsScroll`, `ConfirmButton`, `CancelButton`, `ClearButton` |
| Panel | `SetAllowanceMessage`, `SetOptionsEmpty`, `SetActionAvailability` |

Content and assignment are separate presentation inputs: assigning a sprite/name does not equip it. Selection uses a blue tint and text; invalid targets use a red tint and text; disabled/read-only/locked items are dimmed and labeled; hover and press use the existing alternative stone sprites. Empty slots remain selectable; empty options do not. Pointer and keyboard focus supply transient highlighting without changing assignment. Long names/state labels use TMP autosizing and ellipsis instead of spilling onto adjacent controls.

Confirm and Clear start disabled. Close has no callback. No button mutates data, consumes the daily allowance, closes the panel, or reports successful confirmation. A future presenter must bind intents, supply allowance/read-only state, populate options, and explicitly enable actions. There are no drag/drop handlers or equipment rules in these views.

## Reused assets

All sprites come from existing `UISprites.png` (GUID `6b876b38e4597704b84b5e76e54d27a1`). The builder validates Point filtering without altering import settings.

| Use | Existing local sprite IDs |
|---|---|
| Window/table | `1488800299` |
| Small stone normal / hover / pressed | `924556296` / `-236189874` / `-1569556968` |
| Exit normal / hover / pressed | `774320644` / `726165551` / `-1595310454` |
| Icon frame, also used by existing seed/spell slots | `-1001860351` |
| TMP font | existing `m5x7`, GUID `52be7e01f8fbce54186eac8da2da70cc` |

No new art, materials, fonts, packages, singleton, inventory or restoration scripts are required.

## Verify in Unity

Run **Window → General → Test Runner → EditMode → DailyLoadoutUITests** after generation. Missing prefabs deliberately fail rather than skip. Tests cover serialized references, six positions, callback isolation, state transitions, original art identity and grid overflow geometry.

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
- [ ] Exercise empty, assigned, selected, highlighted, invalid, read-only, locked/unlocked, hover, pressed and keyboard-focus states. Check combinations and readable status text.
- [ ] Confirm/clear enablement is externally supplied; clicking controls alone causes no gameplay changes or false confirmation.
- [ ] Allowance text accommodates both available and consumed messages.
- [ ] Verify the restoration panel, Restore folder, HUD prefabs and Game scene have no diff.

**Current verification status:** No Unity compilation, prefab generation, EditMode/PlayMode execution or visual inspection has been performed by the coding session. An active Editor without a callable control bridge prevents those checks here. Static checks alone do not establish completion of Stage 4A.

## Next step

Generate and inspect the three assets, run the tests, and retain the resulting prefabs/metadata. Stage 4B presenter/gameplay wiring, interactable replacement, scene integration, equipment admission, confirmation handling and daily allowance consumption remain out of scope.
