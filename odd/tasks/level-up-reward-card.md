# Level-up reward card
Status: in progress on branch feature/level-up-reward-card.
Risk: item 3 (removes GameStateUIController.blurVolume serialized field and DOF inventory blur, consumed by Canvas Game UI.prefab and Game UI.prefab).

## Specs
S1. "tengo que agregar un indicador de que ganó cuando subió de nivel (como una tarjeta de mejora pero para decirle al player que semilla y hechizo ganó. Está aparece por encima de las tarjetas de mejoras que se ven con Blur detrás)"
S2. Closing: "Botón 'Continuar'" — the reward card stays until the player confirms; only then the backdrop is removed and upgrade cards can be chosen.
S3. Multi-level: "Una por nivel" — before each pending choice, the reward of that level.
S4. Level with no new seed or spell: "A" — do not show the card; go straight to upgrades as today.
S5. Backdrop: "B pero unifiquemos ese blur y saquemos el otro" — a semi-transparent dark overlay (no real blur), shared by the reward card and the inventory; the DOF Volume blur in GameStateUIController is removed.

## Tasks
- T1 [done] S1,S3,S4 — Record newly unlocked plants/spells per level in PlayerContentUnlockSystem (cleared on progression reset); UpgradeRuntime resolves level CurrentLevel-PendingChoices+1 for each shown choice. Route: inline, test-first. Commit: -
- T2 [done] S1,S2,S5 — UIBackdrop component (dark overlay fade) + LevelUpRewardView (heading, seed icon/name, spell icon/name, Continue); UpgradePanel shows it over the cards and blocks card selection until Continue. Route: inline, test-first. Commit: -
- T3 [pending] S5 — Replace DOF blur in GameStateUIController with the shared UIBackdrop for inventory; remove blurVolume field and Volume usage. Route: inline. Commit: -
- T4 [pending] S1,S5 — Author prefabs (LevelUpReward prefab nested in Canvas Game UI after LevelUpPanel; inventory backdrop) via editor authoring menu run in Unity; run EditMode tests. Route: inline (Unity batchmode). Commit: -

## Log
L1. "en una branch nueva, tengo que agregar un indicador de que ganó cuando subió de nivel (como una tarjeta de mejora pero para decirle al player que semilla y hechizo ganó. Está aparece por encima de las tarjetas de mejoras que se ven con Blur detrás), siempre siguiendo ODD"
L2. Answers: close "Botón 'Continuar'"; multi-level "Una por nivel"; then "1 A, 2 B pero unifiquemos ese blur y saquemos el otro."
L3. Evidence: unlock events fire synchronously inside AddExperience; panel is polled later by UpgradeRuntime.Update (UpgradeRuntime.cs:82-117). PendingChoices is a bare int. Existing DOF blur is camera post-process; Game UI canvas is Screen Space Overlay. LevelUpPanel tests require all serialized refs wired and fixed geometry, so the reward view is a separate prefab.
L4. T1 RED: GetLevelReward missing (InventoryBaselineTests.cs:415). GREEN: 11/12 InventoryBaselineTests; the failing RepeatedDayNotifications...CounterSemantics also fails on base with changes stashed (pre-existing, unrelated). Batchmode rewrote ProjectSettings.asset (editor version noise); reverted, never committed. Level mapping in UpgradeRuntime moved to T2.
L5. T2 RED: 2 failures (LevelUpReward prefab missing). Authored via UpgradeUIPrefabAuthoring.GenerateReward (batchmode). GREEN: UpgradeUIIntegrationTests 15/15. rewardView optional on UpgradePanel (warning if unwired); level = CurrentLevel-PendingChoices+1, shown once per level.
