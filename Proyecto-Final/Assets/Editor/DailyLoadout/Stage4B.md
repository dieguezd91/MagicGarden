# Connect and verify the Daily Loadout panel

Stage 4B supplies a focused `DailyLoadoutPresenter`, deterministic asset catalog projection, explicit Editor-authored option capacity and isolated real-session tests. The existing session owns all assignments, duplicate validation, discard, atomic confirmation and daily allowance. No inventory, bootstrap, admission, restoration or HUD behavior is replaced.

## Native binding (only when the prefab is not yet bound)

The current local panel already contains the presenter and six serialized option references. Preserve that user-authored state; diagnosing the reported test failures does not require rebinding or regenerating it.

1. Allow Unity **2022.3.62f3** to refresh and compile; resolve compilation errors first.
2. Run **Tools → Magic Garden → Daily Loadout → Bind functional UI (preserve visuals)** once. This loads the existing panel and adds only a presenter and six inactive nested `LoadoutOption` instances under the existing Content transform. It leaves slot instances, layout, art, fonts and persistent Button callbacks untouched. Repeated binding does not add duplicate presenters/options. The obsolete Stage 4A generator has been removed; never regenerate the existing assets.
3. Review the resulting panel prefab diff before saving unrelated work. Only additive component/pool references are expected.
4. Run the tests below and the existing regression suites. Native binding and Unity test execution have **not** been performed by this coding session; source/structural checks are not a substitute.

`DailyLoadoutPanelView.OptionViews` is the explicit serialized pool. Six is the current maximum number of distinct assets in either category of the production progression catalog. Both categories reuse it, never simultaneously. If the catalog grows, author additional nested options in Prefab Mode and extend this array (or deliberately raise the authoring capacity). Runtime overflow fails closed with a visible capacity message; it does not silently truncate or create GameObjects.

## Isolated test fixtures

The temporary manual harness and its creation menu have been removed. Automated
EditMode/PlayMode fixtures still compose isolated sessions and use the retained
`DailyLoadoutUIIntegrationAuthoring.Configure` helper on unsaved test instances.
For actual world/HUD acceptance, use the Game.unity checklist in [Stage4D.md](Stage4D.md).

## Runtime composition

Use an authored panel instance and the existing `UIFlowController`. `presenter.Open()` resolves the active player's existing `PlayerContentUnlockSystem.Loadout`, its experience component, current flow and modal owner, or receive those via:

```csharp
presenter.Initialize(existingSession, unlocks, experience, gameFlow, uiFlow);
presenter.Open(); // false if session/day/modal/references are unavailable
```

Initialize does not construct a session. Opening requires a ready daytime session and exclusive `UIModal.DailyLoadout` ownership. After confirmation, reopening displays committed slots read-only. Slot/option intents call only the session's draft setters; Confirm calls only `TryConfirm`. Closing, disabling, destroying, losing modal ownership or leaving daytime discards through `DiscardDraft` and removes the presenter's own listeners. Night closes synchronously through the existing phase event. A while-open refresh also observes upgrade overlays, run reset and disposed/unavailable sessions, for which no session UI event exists. No second equipment snapshot/counter is maintained.

The actual opening interaction in Game and HUD adaptation are deliberately **not wired** in Stage 4B.

## Automated coverage and execution

Run Test Runner from an empty/Menu test context, not over a live gameplay world. Tests create isolated fixtures using the real session and public Button callbacks; reflection supplies serialized fixtures and explicit EditMode lifecycle callbacks across the Assembly-CSharp boundary. Ordinary runtime MonoBehaviours are not expected to receive engine lifecycle dispatch in EditMode. The corresponding PlayMode tests use actual `SetActive(false)`/`Destroy` dispatch, without manually invoking the presenter callbacks. Tests perform native option authoring on **unsaved test instances**, not the production prefab.

Layout tests activate and measure every authored option-pool entry before adding enough visible previews for scrolling overflow. Dormant pool transforms are not active grid participants and must not be measured as if they had been laid out. The tests retain horizontal containment, masking, bottom-row reachability and six-slot bounds checks at all four resolutions.

| Suite | Coverage |
|---|---|
| `DailyLoadoutPresenterTests` (EditMode) | Ordered unlocked catalog, duplicate asset identities, icons/names, no UI grants/selection changes; identity assignment, duplicate rejection/no swaps, empty/Clear, separation, Cancel, six-slot confirmation, eligibility failure, daily lock, new day, modal conflict, subscription cleanup, full reset, disposed/missing session and pool overflow |
| `DailyLoadoutPresenterPlayModeTests` | Actual frame lifecycle: night-close/discard, public buttons/confirmation/new day, no runtime hierarchy creation, upgrade overlay without phase event, disable/destroy cleanup, reset/disposal polling |
| `DailyLoadoutFunctionalPrefabTests` | Native additive authoring is idempotent and preserves existing transforms/art/reference identities; production prefab presenter/pool capacity and reference integrity |
| `DailyLoadoutPointerPersistenceTests` | Slot/option hover and press survive unchanged presenter availability refresh; actual read-only transitions still reset pointer state |
| Existing `DailyLoadoutUITests` | Stage 4A reference/art/state/grid and resolution checks |
| Existing Loadout / Equipment / LoadoutBaseline suites | State/session, stock/admission, seed selection/drag, cooldown and gameplay regressions |

After the Editor is deliberately closed by its owner, batch commands can run separately (do not launch another Editor on an open project):

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
$project = Join-Path $PWD 'Proyecto-Final'
$out = Join-Path $project ('Logs\Stage4B-' + (Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Path $out | Out-Null
& $unity -batchmode -projectPath $project -executeMethod DailyLoadoutUIIntegrationAuthoring.BindPrefab -quit -logFile "$out\binding.log"
& $unity -batchmode -projectPath $project -runTests -testPlatform EditMode -testResults "$out\editmode.xml" -logFile "$out\editmode.log"
& $unity -batchmode -projectPath $project -runTests -testPlatform PlayMode -testResults "$out\playmode.xml" -logFile "$out\playmode.log"
```

Inspect XML and logs, including skipped tests, rather than inferring success from process launch. Static Roslyn compilation against the project's existing Unity references passed for the runtime, Editor tooling, support fixture and both test assemblies. Independent source review verified all three existing prefabs are byte-identical to `c35d0984` and excluded systems are untouched. These checks do not prove native import or runtime behavior.

No agent-executed Unity test results are claimed here: the active Editor has no connected CLI Pipeline instance. The owner has supplied a cleanup failure and three layout failures; the test fixtures have been corrected in source, but their Unity reruns remain unexecuted by the agent. Current local prefab references show native binding is present; do not repeat binding merely to address these failures. A passing full Unity suite, natural startup/discovery and manual Game checks are still unverified. Stage 4B is **not complete** until the applicable automated Unity tests pass.
