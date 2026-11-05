# Author a linear tutorial

Use existing Tutorial Step assets and the existing tutorial panel. No scene references belong in these assets. Existing assets keep legacy completion until **Use Configured Completion** is enabled. The shipped story is installed through `Assets/Scriptables/Tutorial/SummerlimbsTutorial.asset`, assigned on `Assets/Prefabs/Managers/TutorialManager.prefab`. It reuses a focused subset of existing steps in a redesigned loadout-first order. Active filenames and Unity names describe their lessons without stale numeric order prefixes. Internal legacy IDs remain stable compatibility keys; use the sequence list and effective objective/conditions for playback. See [the installed flow map](TutorialFlowAuthoring.md#installed-phase-map).

## Quick path

1. In the Project window choose **Create > Tutorial > Step**. Set **Step ID**, **Step Order**, **Instruction Text** (dialogue), optional **Step Image**, **Objective Type**, **Required Count**, and **Is Gated Step**.
2. Choose **Create > Tutorial > Sequence**. Add Step assets to **Steps** in playback order. Reorder by dragging list entries. Select each asset to edit its dialogue. The sequence inspector reports missing entries and conflicting identifiers/orders.
3. Assign this asset to **Sequence** on the existing scene's **TutorialManager**. Leave **Sequence** empty to retain the original **Tutorial Steps** list sorted by **Step Order**. Do not replace the manager or panel prefab.
4. Play through every step, including pause, inventory, Continue and skip. Inspector diagnostics do not replace a gameplay test.

## Dialogue fit and input icons

- Edit **Instruction Text** and optional **Step Image** on the step. The existing panel measures preferred text height after establishing the real text width and image state, including rich-text and sprite tags. It grows without reducing the installed font size or replacing the background/buttons.
- **Maximum Viewport Height** on TutorialUI defaults to 0.75 of its parent canvas height. The panel stays inside that viewport; its existing narrow nested width is widened when necessary for readability. The bottom button row is reserved independently of whether Continue is currently visible.
- Unusually long dialogue uses a masked vertical viewport with full-height content. Use the mouse wheel over the text or drag the text area to read it; fast-forward reveals characters and returns to the beginning rather than jumping past unread content. Scrolling does not confirm or advance the step. Continue, Enter, cooldown, objective gating, pause and skip retain their existing roles.
- No manual ScrollRect wiring is needed on the installed prefab: TutorialUI upgrades its existing text viewport once at runtime, reusing the serialized text and viewport references. Existing scroll indicator hooks remain supported without requiring a scrollbar.
- Write speakers as `<b>FOREST:</b>` or `<b>SUMMERLIMBS:</b>`. Rich-text markup is measured as formatting, not printed as literal characters.
- Use `<sprite name="ArrowKeys">` for arrow movement and `<sprite name="WASDKeys">` for WASD. `QKey`, `EKey`, `FKey`, `SpaceKey`, `TABKey`, `IKey`, `ScrollMouse` and `MouseClick` are also registered in the same installed UISprites TMP asset. Use these sprites instead of spelling their bindings in active lesson text. Dedicated numeric key icons 1-6 are not registered; the remaining `1-3` / `4-6` text is a reported exception, not a completed icon conversion. ArrowKeys reuses the existing PNG slice; it is not new art.
- Preview both 1920×1080 and 1024×768, with and without a step image. Check the final line by scrolling all the way down, and verify Continue remains separate from the text. Automated layout tests can save render evidence under ignored `Library/TutorialRevisionEvidences`; screenshots are not source assets.

## Configured completion

Enable **Use Configured Completion** on a Step to opt into these fields:

| Field | Effect |
|---|---|
| Conditions | Existing event objectives and Required Count per row; counts start fresh for each step. |
| Condition Combination | All requires every row; Any requires one row. Empty conditions are immediately satisfied. |
| Wait Mode: None | Complete after conditions and panel typing finish. |
| Wait Mode: Continue | After conditions, require the existing Continue button/Enter. Set Objective Type to Wait and provide visible text. |
| Wait Mode: Duration | After conditions and typing, wait Wait Duration seconds of scaled gameplay time. Menu pause/inventory suspends this timer. |
| Hint | Optional Contextual Hint asset, created via Create > Tutorial > Contextual Hint. Title and description append below dialogue in the same panel. No new hint popup. |
| Enter Actions | Explicit presentation actions: Hide Panel or Show Panel. Last action wins; an empty list shows the panel normally. Do not end the list with Hide Panel when using Continue. A hidden Ritual Altar panel does not wait for typing. |

Example: All of Dig ×2 and Harvest ×1, followed by Duration = 2 seconds, advances only after both counts, dialogue typing and that wait. Any of Plant Production ×2 or Plant Defensive ×1 accepts either route to the same next step; it is not branching.

## Compatibility and limitations

- **Objective Type**, **Step Order** and **Is Gated Step** still drive existing external gameplay queries and gating. Conditions do not replace those query values. Preserve installed step orders when other gameplay depends on them. Move retains its existing movement-unblock exception; Ritual Altar retains its typing gate.
- A sequence uses exact list order, but Step Order remains the externally reported number. Do not renumber existing steps casually.
- Plant is a generic alias for production/defensive planting. One planting updates each applicable condition row once, never counts twice in the same row. Duplicate rows are parallel counters, not sequential objectives.
- Events received while a step settles can be buffered for that step. They never carry to the next step. Configured completion ignores events while menu-paused. Skip, disable and restart discard timers and pending subscriptions.
- Open Crafting and Open Restoration intentionally retain their historical **close-event** semantics, including configured conditions. Use Open Daily Loadout for the loadout-open event.
- **First Plant Ready** observes actual availability from `PlantManager.GetHarvestablePlants()`: at least one resource reports ready and is not already being harvested. It is not an elapsed growth timer. The current step reconciles resources already available at entry and resources becoming available later, including after resume. Only steps using this legacy objective or configured condition query availability. Use **Required Count = 1**: this is one latched state observation per step, not a frame counter or a count of distinct plants. Other counts are unsupported and diagnosed in the inspector. Configured All/Any and Continue/Duration gates still apply; skip, disable, restart and step changes clear the local observation. The public readiness event API keeps its separate one-shot semantics.
- None, Explore House, Plant Hybrid, Welcome, Fifth Night Warning and Wait are not gameplay event conditions. Use an empty condition list plus Continue or Duration for dialogue-only steps.
- No branches, arbitrary methods, UnityEvents, scene-object references, resource grants, teleports, enemy spawning or generic scripting actions are supported. Gameplay state still belongs to existing managers/subscribers. Enter House → Cast Spell keeps its legacy house-exit deferral only outside explicit house flows; the installed sequence uses real Exit House.
- Wait Duration is used only by opt-in Duration mode. Legacy Wait still requires Continue; its existing Wait Duration field never auto-advances it.

## Installed Summerlimbs story

The forest mentors Summerlimbs, a newly awakened ENT created to help defend it. Lore and movement lead into house selection and daily confirmation, then one generic dig/plant practice and ritual/night defense. The forest does not know why the infernal beings attack the ancestral tree. Keep that uncertainty when editing dialogue; do not add an explanation of the passage or unrelated lore.

- Edit **Instruction Text** on the referenced Step assets, not a copied list of dialogue. Existing English copy and input sprite tags remain the convention.
- Preserve asset GUIDs/fileIDs and existing manager/UI references. Reorder the sequence and edit objectives, counts, gating and copy to fit the lesson; there is no fixed 29-step requirement. The prefab's legacy list remains as a fallback, but reused steps now carry redesigned semantics. Scene-specific panel overrides are unchanged.
- Both sequence flags are enabled: tutorial-only plant+spell confirmation and explicit night permission. Selection/irreversibility is explained before opening the panel. Open, actual Confirm and ungated Exit are distinct objectives.
- Only one generic Dig, Plant and equipped Cast Spell are mandatory. No identity, second plant type, harvest/readiness, crafting, upgrade purchase or six-slot fill is required. Unused legacy assets, including `21-WaitForHarvest`, remain available but are not installed lessons.
- The ritual/battle lesson uses All of actual Use Ritual Altar, Cast Spell and Survive Night. It is already active before the ritual begins, so no separate Start Night/Defeat Enemy step can miss an early event. It has no achievement timer or synthetic completion.
- Content and scene-prerequisite functional GREEN remain pending parent-owned Unity execution; earlier runtime evidence is not a redesigned-flow playthrough.

## Before installing narrative changes

- [ ] Keep saved asset GUIDs and existing panel references.
- [ ] Resolve inspector errors; use finite nonnegative waits and counts of at least one.
- [ ] Test with the real scene emitters, pause/resume, skip, restart and disabled manager.
- [ ] Verify external step-order/objective queries still unlock the intended gameplay.
