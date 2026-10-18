# Daily Loadout V2 — Stage 4A
Independent presentation UI on `feature/daily-loadout-v2`. No commits or push authorized.
Unity is open without an available Editor bridge; explicit native authoring and visual checks require the Editor.

## Specs
S1. "Create an independent Daily Loadout UI prefab. Do not overwrite or destroy the existing restoration panel." "Use uGUI and TextMeshPro, consistent with the existing project."
S2. "Display six equipment positions:" "Plant 1" "Plant 2" "Plant 3" "Spell 1" "Spell 2" "Spell 3". "Empty" "Assigned" "Selected" "Highlighted" "Invalid drop target" "Disabled/read-only". "Organize them as two clearly distinguishable categories within the existing stone-table visual style."
S3. "Below the main table, reuse the smaller stone-table/button assets to create selectable equipment options." "Plant icon and name" "Spell icon and name" "Locked/unlocked state" "Selected/assigned state" "Empty content" "Hover and pressed states". "Provide a layout that can accommodate additional unlocked content without redesigning the panel."
S4. "Confirm" "Cancel/Close" "Clear equipment position". "Provide room for a message indicating whether the daily configuration allowance is available or has already been consumed."
S5. "Avoid hardcoded asset paths or runtime GameObject.Find calls in the new UI." "Keep visual components separate from gameplay data and equipment rules." "Use serialized references for required UI elements." "Do not introduce new singletons." "Do not implement a parallel inventory." "Do not modify DailyLoadoutSession or DailyLoadoutState." "Do not modify RestorationAltar, HouseRestorationSystem, tutorial flows, numeric controls, or gameplay admission." "Do not integrate the new prefab into the Game scene yet." "Do not change the existing HUD prefabs in this phase."
S6. "Inspect prefab hierarchies and serialized references before changing assets." "Prefer Unity-native prefab creation/editing through the available Editor tooling. If direct Unity Editor access is unavailable, create a reproducible Editor setup workflow rather than making risky manual YAML changes." "Preserve GUIDs and existing prefab references."
S7. "The new UI may expose presentation methods, but it must not mutate equipment or simulate successful confirmation."
S8. "Verify all new prefab references." "Check layout at the project's supported UI resolutions." "Verify pixel-art sprite rendering." "Verify that six main positions are visible." "Verify options can be displayed without clipping." "Verify that hover, selected, disabled and empty visual states are distinguishable." "Confirm the original restoration panel remains unchanged." "Run relevant existing tests." "If the UI cannot be inspected in an active Unity Editor, explicitly report that visual verification is pending."
S9. "Do not start gameplay integration or Stage 4B." "Do not commit or push changes."

## Tasks
T1 | S1-S9 | inline: record scope and available workflow | complete | commit: prohibited
T2 | S1-S7,S9 | inline: presentation components and explicit native authoring menu (single coupled unit) | blocked: source written, completion awaits Unity compilation/execution | commit: prohibited
T3 | S8-S9 | inline static checks and Editor verification handoff | complete: evidence below | commit: prohibited
T4 | S1-S4,S8 | Editor owner: generate prefabs, execute Unity tests and inspect visuals | blocked: open Unity has no callable control bridge | commit: prohibited

## Log
L1. Original request (verbatim):

> # Daily Loadout V2 — Stage 4A: UI Construction
>
> ## Context
>
> Repository: Proyecto-Final
> Branch: `feature/daily-loadout-v2`
>
> Stages 1–3 are implemented and their EditMode and PlayMode tests pass.
>
> The reference document is `Magic Garden Desing Doc`, page 2, LOADOUT section.
>
> We are now implementing the visual interface for Daily Loadout.
>
> ## Goal
>
> Build the Daily Loadout configuration UI using the existing restoration panel's stone-table assets and visual style.
>
> This is the UI construction phase. Gameplay integration and interactable replacement will be handled separately.
>
> ## Required layout
>
> ### Main stone table
>
> Display six equipment positions:
>
> - Plant 1
> - Plant 2
> - Plant 3
> - Spell 1
> - Spell 2
> - Spell 3
>
> Each position must support visual states for:
>
> - Empty
> - Assigned
> - Selected
> - Highlighted
> - Invalid drop target
> - Disabled/read-only
>
> Organize them as two clearly distinguishable categories within the existing stone-table visual style.
>
> ### Available options
>
> Below the main table, reuse the smaller stone-table/button assets to create selectable equipment options.
>
> Support visual presentation of:
>
> - Plant icon and name
> - Spell icon and name
> - Locked/unlocked state
> - Selected/assigned state
> - Empty content
> - Hover and pressed states
>
> Provide a layout that can accommodate additional unlocked content without redesigning the panel.
>
> ### Actions
>
> Include visual controls for:
>
> - Confirm
> - Cancel/Close
> - Clear equipment position
>
> Provide room for a message indicating whether the daily configuration allowance is available or has already been consumed.
>
> ## Existing assets
>
> Inspect and reuse suitable sprites, materials, buttons, layouts, and UI components from:
>
> - `Assets/Prefabs/UI/Canvas/Game/Panel Restoration UI.prefab`
> - `Assets/Prefabs/UI/Restore/`
> - `Assets/Prefabs/UI/Canvas/Game/HUD/HUD Seed Slots.prefab`
> - `Assets/Prefabs/UI/Canvas/Game/HUD/HUD Spell Slots.prefab`
>
> Preserve the game's existing pixel-art visual language.
>
> Do not create replacement artwork when an existing asset is suitable.
>
> ## Implementation constraints
>
> 1. Create an independent Daily Loadout UI prefab. Do not overwrite or destroy the existing restoration panel.
> 2. Reuse visual assets, not restoration-specific business logic.
> 3. Use uGUI and TextMeshPro, consistent with the existing project.
> 4. Avoid hardcoded asset paths or runtime GameObject.Find calls in the new UI.
> 5. Keep visual components separate from gameplay data and equipment rules.
> 6. Use serialized references for required UI elements.
> 7. Do not introduce new singletons.
> 8. Do not implement a parallel inventory.
> 9. Do not modify DailyLoadoutSession or DailyLoadoutState.
> 10. Do not modify RestorationAltar, HouseRestorationSystem, tutorial flows, numeric controls, or gameplay admission.
> 11. Do not integrate the new prefab into the Game scene yet.
> 12. Do not change the existing HUD prefabs in this phase.
>
> ## Unity asset editing
>
> Inspect prefab hierarchies and serialized references before changing assets.
>
> Prefer Unity-native prefab creation/editing through the available Editor tooling. If direct Unity Editor access is unavailable, create a reproducible Editor setup workflow rather than making risky manual YAML changes.
>
> Preserve GUIDs and existing prefab references.
>
> ## Expected deliverables
>
> - Daily Loadout panel prefab
> - Reusable equipment slot view
> - Reusable available-option view
> - Visual states for the required interactions
> - Serialized references ready for a future presenter/controller
> - Any supporting free Unity UI assets strictly required for layout
>
> The new UI may expose presentation methods, but it must not mutate equipment or simulate successful confirmation.
>
> ## Verification
>
> 1. Verify all new prefab references.
> 2. Check layout at the project's supported UI resolutions.
> 3. Verify pixel-art sprite rendering.
> 4. Verify that six main positions are visible.
> 5. Verify options can be displayed without clipping.
> 6. Verify that hover, selected, disabled and empty visual states are distinguishable.
> 7. Confirm the original restoration panel remains unchanged.
> 8. Run relevant existing tests.
>
> If the UI cannot be inspected in an active Unity Editor, explicitly report that visual verification is pending.
>
> ## Completion report
>
> Provide:
>
> - Files created and modified
> - Prefab hierarchy
> - Reused assets
> - Available visual states
> - Verification performed
> - Remaining integration tasks
>
> Do not start gameplay integration or Stage 4B.
>
> Do not commit or push changes. el documento referenciado es el que esta en el siguiente link: https://docs.google.com/document/d/1OaIL_7JYT0UqP1Z4kNHJIEHvbk4QIuR72I0pzdsmcFI/edit
L2. Read-only exploration found restoration sprites in UISprites, m5x7 TMP font, 1920x1080 Canvas reference and existing one-shot authoring pattern. Existing restoration button variants carry business callbacks; reuse only their art.
L3. Unity 2022.3.62f3 is running on this project. No callable Editor bridge is available. Use explicit Tools menu, not import-time scene/prefab mutation. Native generation, Unity tests and visual verification remain pending until Editor execution. Source generation is not a delivered prefab.

L4. Source: five presentation/layout components, explicit authoring generator, README, Editor test assembly and tests. No pre-existing tracked project file was edited. Tests were written before native execution but no RED/GREEN was observed: the Unity runner is unavailable while the project is open without a control bridge.
L5. Parent static probes passed: 20 reference-field assignments resolve, asmdef JSON parses, runtime sources contain no forbidden find/asset-loading/domain APIs. `git diff --check` exit 0. `git diff --exit-code HEAD -- Proyecto-Final/Assets Proyecto-Final/Packages Proyecto-Final/ProjectSettings` exit 0 proves existing tracked assets/gameplay remain unchanged; untracked source was read separately.
L6. Native assess returned unassessable because intended untracked scope was undeclared. No native review started or approval claimed. Independent verifier mvd3r6b5-2-6t0e found no severe source defects; S5/S7/S9 met at source level, S6 met for fallback authoring workflow, S1-S4/S8 pending native execution. Its full-project scratch-copy attempt timed out on live Unity Temp locks; only targeted static probes succeeded. No Unity tests were run. Its overflow-test advisory prompted an added final-row scrolling assertion; this assertion is not executed.
L7. Needs your decision: execute Tools > Magic Garden > Daily Loadout > Create UI prefabs (once) in the current Editor, then run DailyLoadoutUITests, existing EditMode/Loadout PlayMode tests and the README visual checklist. Prefabs and native metadata are not generated yet; Stage 4A is partial, not complete. No commits/push. Risk item 6: rendered UI/pixel clarity require visual checks not established by static tests.
