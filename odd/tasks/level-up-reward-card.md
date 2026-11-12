# Level-up reward card
Status: in progress on branch feature/level-up-reward-card.
Risk: item 3 (removes GameStateUIController.blurVolume serialized field and DOF inventory blur, consumed by Canvas Game UI.prefab and Game UI.prefab).

## Specs
S1. "tengo que agregar un indicador de que ganó cuando subió de nivel (como una tarjeta de mejora pero para decirle al player que semilla y hechizo ganó. Está aparece por encima de las tarjetas de mejoras que se ven con Blur detrás)"
S2. Closing: "Botón 'Continuar'" — the reward card stays until the player confirms; only then the backdrop is removed and upgrade cards can be chosen.
S3. Multi-level: "Una por nivel" — before each pending choice, the reward of that level.
S4. Level with no new seed or spell: "A" — do not show the card; go straight to upgrades as today.
S5. Backdrop: "B pero unifiquemos ese blur y saquemos el otro" — a semi-transparent dark overlay (no real blur), shared by the reward card and the inventory; the DOF Volume blur in GameStateUIController is removed.
S6. User report: "el blur entre ambos modales no se ve, se ven superpuestas, no hay una jerarquia de lo que se muestra, agrega los assets de la ui que correspondan"

## Tasks
- T1 [done] S1,S3,S4 — Record newly unlocked plants/spells per level in PlayerContentUnlockSystem (cleared on progression reset); UpgradeRuntime resolves level CurrentLevel-PendingChoices+1 for each shown choice. Route: inline, test-first. Commit: -
- T2 [done] S1,S2,S5 — UIBackdrop component (dark overlay fade) + LevelUpRewardView (heading, seed icon/name, spell icon/name, Continue); UpgradePanel shows it over the cards and blocks card selection until Continue. Route: inline, test-first. Commit: -
- T3 [done] S5 — Replace DOF blur in GameStateUIController with the shared UIBackdrop for inventory; remove blurVolume field and Volume usage. Route: inline. Commit: -
- T4 [done] S1,S5 — Author prefabs (LevelUpReward prefab nested in Canvas Game UI after LevelUpPanel; inventory backdrop) via editor authoring menu run in Unity; run EditMode tests. Route: inline (Unity batchmode). Commit: -

- T5 [done] S1,S5,S6 — Make the reward modal visually above the level-up panel: visible backdrop dimming the upgrade cards, distinct elevated reward card, and the same imported UI art/font/9-slice assets the LevelUpPanel/LevelUpCard prefabs use; prove with runtime captures. Route: worker. Commit: -

## Log
L1. "en una branch nueva, tengo que agregar un indicador de que ganó cuando subió de nivel (como una tarjeta de mejora pero para decirle al player que semilla y hechizo ganó. Está aparece por encima de las tarjetas de mejoras que se ven con Blur detrás), siempre siguiendo ODD"
L2. Answers: close "Botón 'Continuar'"; multi-level "Una por nivel"; then "1 A, 2 B pero unifiquemos ese blur y saquemos el otro."
L3. Evidence: unlock events fire synchronously inside AddExperience; panel is polled later by UpgradeRuntime.Update (UpgradeRuntime.cs:82-117). PendingChoices is a bare int. Existing DOF blur is camera post-process; Game UI canvas is Screen Space Overlay. LevelUpPanel tests require all serialized refs wired and fixed geometry, so the reward view is a separate prefab.
L4. T1 RED: GetLevelReward missing (InventoryBaselineTests.cs:415). GREEN: 11/12 InventoryBaselineTests; the failing RepeatedDayNotifications...CounterSemantics also fails on base with changes stashed (pre-existing, unrelated). Batchmode rewrote ProjectSettings.asset (editor version noise); reverted, never committed. Level mapping in UpgradeRuntime moved to T2.
L5. T2 RED: 2 failures (LevelUpReward prefab missing). Authored via UpgradeUIPrefabAuthoring.GenerateReward (batchmode). GREEN: UpgradeUIIntegrationTests 15/15. rewardView optional on UpgradePanel (warning if unwired); level = CurrentLevel-PendingChoices+1, shown once per level.
L6. T3 RED: InventoryUses... failed (backdrop null). ReplaceInventoryBlur authored both Canvas Game UI and Game UI (BackgroundBlur Volume object removed). GameSceneRuntimeChoice test updated: reward card blocks card clicks until Continue. GREEN: UpgradeUIArtPlayModeChecks+UpgradeUIIntegrationTests 19/19.
L7. Full EditMode: base d9b6ffba 13 failures (DailyLoadout x10, UpgradeUIArtTests art identity, InventoryBaseline day cycle, TutorialLoadoutFlow); branch has the same 13 plus none new after the test update. PlayMode suites not run; reward card not checked visually in-game.
L8. Independent verify (gentle-ai-verify): S1-S5 met, no blockers. Fixed advisory A2 (reward not re-shown after interrupted choice): ReleaseChoice resets lastRewardLevel. Upgrade tests 27/27. Advisories kept: inventory backdrop now blocks clicks behind it; Game UI.prefab reserialization dropped stale fields (unused by scenes).
L9. User report verbatim in S6 (in-game check): backdrop invisible, modals overlap, no hierarchy; asked to add the corresponding UI assets. Likely cause: generated reward prefab uses the dark .96 panel color and LiberationSans with no sprites, matching the dark LevelUpPanel so the 0.6 black backdrop is imperceptible.
L10. T5 worker mwdswiyf-4-3rvx partial: blocked, Unity Editor PID 38336 has this project open (batchmode exit 21). Only edit: UpgradeUIArtPlayModeChecks.cs reward capture + AssertRewardHierarchy (not run). Static finding: LevelUpPanel has black .65 dimmer + light-mauve TutorialBox window; reward card is plain dark, LiberationSans, no TutorialBox/CraftButton/m5x7. Resume: baseline capture, RED asserts (font m5x7, TutorialBox frame, CraftButton, backdrop alpha>.6), authoring, after captures, 4 suites.
L11. T5 worker mwdt2c8t-5-7yam: root cause confirmed by runtime capture (opaque-dark frameless card, level-up text bleeding through, 0.6 backdrop over dimmed window). Applied existing TutorialBox frame (+shadow, 720x640 centered), TutorialBox slots, CraftButton Continue, m5x7 font; UIBackdrop.DimAlpha=0.8 for reward and inventory backdrops via UpgradeUIPrefabAuthoring.ApplyRewardArt. RED 8 new UpgradeUIArtTests; GREEN UpgradeUIArtTests 12/13 (pre-existing art identity failure), PlayModeChecks 2/2, UIIntegration 17/17, UpgradeIntegration 8/8. Captures: Library/MagicGardenUIArtValidation/reward-card/{before,after}. Inventory backdrop not captured in-game. Parent reverted batchmode dirt and removed Library/RewardCardValidation.
