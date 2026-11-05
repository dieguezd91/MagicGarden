# Author a loadout-first tutorial flow

The installed `SummerlimbsTutorial` sequence enables both flags below and teaches manual daily confirmation before planting and combat. Other sequences remain opt-in: default-false flags retain legacy empty-confirmation and numeric ritual-gate behavior. Edit the sequence list, not numeric order gates, to redesign lessons.

## Installed phase map

| List positions / asset | Effective completion | Teaching purpose |
|---|---|---|
| 1-2 · `Awakening` / `ForestMission` | Continue each | Newly awakened ENT, forest mentor, unknown infernal motive |
| 3 · `Movement` | Move ×1 | Registered WASDKeys + ArrowKeys icons |
| 4 · `EnterHouse` | EnterHouse ×1, ungated | Real doorway entry |
| 5 · `PrepareDailyLoadout` | Continue, gated | Categories, tutorial-only minimum one each, immutable daily Confirm; before opening |
| 6 · `OpenDailyLoadout` | OpenDailyLoadout ×1, ungated | F at station; choose, Confirm, close; no grants or stock spending |
| 7 · `ConfirmDailyLoadout` | ConfirmDailyLoadout ×1, ungated | Real confirmed usable session; reopen if closed without confirming |
| 8 · `ExitHouse` | ExitHouse ×1, ungated | Equipped daily lock, real exit |
| 9 · `Dig` | Dig ×1 | Shovel, left-click, Q/E/wheel |
| 10 · `Plant` | Generic Plant ×1 | Any filled equipped 1-3 slot with stock; no forced identity/type |
| 11 · `PrepareForNight` | Continue, gated | Preparation and concise battle controls; no teleport objective |
| 12 · `NightDefense` | All: UseRitualAltar ×1 + CastSpell ×1 + SurviveNight ×1 | F ritual, any equipped 4-6 spell, survive to dawn |
| 13 · `ResourcesAndProgression` | Continue, gated | Verified progression and optional resource pointer; no harvest requirement |
| 14 · `TutorialComplete` | Continue, gated | Summerlimbs continues day preparation/night defense |

Active asset filenames and Unity names describe their lessons without numeric order prefixes. GUIDs, internal legacy step IDs and legacy numeric orders remain stable; IDs are technical compatibility keys, not lesson descriptions. The selection explanation has its own `PrepareDailyLoadout` asset. The unused `12-FindRestorationAltar` keeps its proximity objective and ungated metadata so existing Stage 4C validation and legacy references remain valid. The manager prefab already references this sequence; its legacy list and UI references remain untouched. Existing unused assets are not deleted. `allowNightTransition` is false (serialized default) before position 12 and true on positions 12-14. Later gated dialogue still blocks actions until continued; permission does not bypass input/phase checks.

Crafting is outside the current gameplay/tutorial scope. Unused crafting and fifth-night-warning assets remain legacy references, not installed lessons. The retained boss configuration uses a seven-night interval; do not restore an unconditional fifth-night warning from its old filename.

## Quick path

1. Assign a `TutorialSequence` to the existing `TutorialManager`. Its `steps` list controls playback order, independently of `stepOrder`.
2. Enable `requireUsableLoadout` on that sequence. Teach entering the house and opening the daily loadout, then use `ConfirmDailyLoadout` before teaching planting or attacking.
3. Ask for at least one plant and one spell, in any positions. The other four positions may remain empty. The player must press Confirm; opening or closing the panel never confirms or equips anything.
4. Enable `useExplicitNightPermission`. Leave `allowNightTransition` disabled on preparation steps; enable it only on steps where the ritual should be available.
5. Use an explicit `ExitHouse` step when the player should leave the house. Do not gate that step with `isGatedStep`: the existing house door deliberately blocks exit during gated steps.

## Fields and objectives

| Authoring surface | Default | Effect |
|---|---|---|
| `TutorialSequence.requireUsableLoadout` | false | While this tutorial is active, authoritative daily confirmation rejects drafts without a plant and a spell. Skipping/completing/disabling the tutorial removes this extra restriction. |
| `TutorialSequence.useExplicitNightPermission` | false | Replaces the altar's numeric tutorial threshold with the current step's permission. Does not bypass daytime, input, or ritual-in-progress checks. |
| `TutorialStep.allowNightTransition` | false | Permits starting the night ritual at this step only when explicit permission is enabled. Set it on every step where the ritual should remain available; it is not a permanent unlock. |
| `ConfirmDailyLoadout` | enum 30 | Completes from the real run session's `HasConfirmedToday` and at least one committed plant and spell. Initialization, draft edits, panel open/close, rejected confirmation, and globally legal empty confirmation do not complete it. |
| `ExitHouse` | enum 31 | Completes from actual exterior world state or the existing house-exit callback. Entering the house or casting a spell does not stand in for exit. |

Earlier objective numbers remain unchanged. `GetCurrentObjectiveType`, `GetCurrentStepOrder`, and `IsPlayerGated` retain their public meanings for existing consumers such as upgrade and tooltip systems.

## Confirmation feedback and daily allowance

A missing category displays this exact feedback:

> Select at least one plant and one spell before confirming your tutorial loadout.

Rejection occurs before the existing atomic confirmation transaction. It must leave draft and committed assignments, inventory stock, materials, gold, the day token, and the daily allowance unchanged. The player can correct the draft and retry without reopening the panel.

Successful confirmation keeps the existing once-per-day lock. Closing/reopening the panel, restarting the tutorial, or sending another same-day notification does not reopen editing. Only the next actual day restores the allowance. This feature does not grant seeds, auto-equip, auto-confirm, or change the pure global loadout domain rules.

Outside an active opted-in tutorial, existing manual empty confirmation remains legal. Do not use the loadout's `CommittedChanged` notification as confirmation evidence: that notification also fires at initialization and full-run reset.

## Conditions, waits, and transition timing

Opening or reopening the loadout immediately hides the current tutorial, including an unfinished hide animation, and pauses it until the panel closes. This also applies to registered inventory, crafting, restoration and level-up modals; legacy inventory/pause notifications remain supported. Closing one panel cannot force the tutorial above a replacement modal. Explicit and delayed redisplay respect the same rule, without requiring a LevelManager to exist.

Resuming re-presents the same step without resetting its objective counts or configured wait progress. The station's opening observation is retained even though modal ownership is acquired first. Confirmation and exit reconcile actual state after closing/display delays. Teach category selection and the Confirm lock before OpenDailyLoadout; never put required Continue instructions behind the panel. This presentation coordination does not change the once-per-day loadout lock or panel sorting.

The installed ritual lesson activates All of ritual use, cast and survival together before interaction. RitualAltar defers the next lesson until ritual completion; keeping battle conditions on the same active step avoids losing NightStarted, early casts or enemy events between lessons. Do not split these into mandatory StartNight/DefeatEnemy/SurviveNight event-only steps without checking their activation timing. No wait duration stands in for survival.

Both new objectives can be used as normal objectives or as configured All/Any conditions. Use count one: state reconciliation contributes one observation per step, not one observation per frame. Configured Continue and Duration waits still apply after conditions are satisfied.

The manager reconciles a successful confirmation that happened immediately before the confirmation step appeared. It also checks existing world state when an exit happened during a deferred display transition. Pauses and typing delay consumption; completion, skip, disable, and restart clear step-local observations. Restarting against genuinely still-confirmed equipment can satisfy the state objective again; resetting the session clears that actual state and does not replay an old notification.

For legacy sequences, the old EnterHouse-to-CastSpell house-exit deferral remains. Sequences with explicit night permission or an explicit ExitHouse objective/condition use their authored flow instead. Avoid combining the legacy implicit deferral with an additional manual deferral for the same exit.

`RitualAltar.CanInteract` calls `TutorialManager.CanStartTutorialNight(legacyMinimumStepOrder)`. With explicit permission disabled, the altar's existing serialized numeric threshold remains authoritative. Outside the tutorial, this query imposes no tutorial restriction.

## Verify revised content

- [ ] One plant and one spell confirm successfully without filling all six slots.
- [ ] Empty, plant-only, and spell-only rejection preserves state and allowance and shows the exact feedback above.
- [ ] Post-confirm editing and reconfirmation remain rejected until the next actual day.
- [ ] Confirmation before step entry, pause/resume, skip/reset/restart, and deferred house exit do not deadlock or replay stale progress.
- [ ] Close the loadout without confirming, then reopen it: the current dialogue hides immediately and returns with its progress retained after closing. A replacement modal, legacy close notification or forced redisplay must not uncover it prematurely.
- [ ] Night permission follows metadata even when `stepOrder` is deliberately out of order; legacy sequences retain their threshold.
- [ ] After changing the sequence, run narrative, loadout, dialogue layout, timing and relevant integration checks in Unity. Record automated results separately from manual gameplay acceptance.
- [ ] Run `TutorialScenePrerequisiteTests`: it inspects Game outside Play Mode (opening additively only when needed), derives choices from level-one progression and reads resolved seed slots/starter spell indices without forced Awake/Start, saves or gameplay. It closes only a scene it opened and restores the previous active/loaded scene context. It checks serialized prerequisites only, not fresh-Game casting or runtime readiness.

## Input icon availability

Active lessons use the installed `WASDKeys`, `ArrowKeys`, `QKey`, `EKey`, `FKey`, `SpaceKey`, `TABKey`, `IKey`, `ScrollMouse` and `MouseClick` sprites for physical controls. There are no registered dedicated key icons for **1, 2, 3, 4, 5 or 6** in the tutorial TMP registry. The `1-3` / `4-6` ranges remain readable text as a reported missing-icon exception; do not fabricate art or substitute an unlabeled EmptyKey. Confirm and Continue are UI button labels, not keyboard bindings.
