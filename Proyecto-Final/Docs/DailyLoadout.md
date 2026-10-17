# Daily Loadout

Open **Loadout [O]** from the persistent six-slot HUD. Choose a draft slot, click an unlocked item, then **Confirm for today**. Confirmation applies all six slots together and locks configuration until the next game day. Close the menu to resume gameplay selection.

## Controls and rules

| Action | Control / behavior |
|---|---|
| Open or close configuration | `O` or the HUD Loadout button |
| Close configuration | `Escape` or Close; unconfirmed draft edits are retained |
| Equip an item in the draft | Click a draft slot, then an unlocked item below |
| Leave a slot empty | Select the draft slot and click Clear selected slot |
| Confirm | Confirm for today; at most three distinct spells and three distinct plants, including empty slots |
| Active spell | HUD slots `1`–`3` (numpad also works), or existing Q/E spell cycling at night |
| Active seed | HUD slots `4`–`6` (numpad also works) during the day |
| Switch active equipment | Does not edit or unlock the confirmed configuration |

Hover a usable HUD slot for its existing spell or seed tooltip. Seed tooltips retain the existing planting-ability requirement and tutorial completion event; click a seed slot to activate planting, then hover it for details.

Unavailable categories and exhausted seed slots stay visible but dark and unselectable. Spell cooldowns remain on their inventory entries; the HUD displays remaining cooldown and mana cost. Plant slots display seed counts, including zero. Depletion never removes a plant from the confirmed Loadout. Acquiring more seeds for that identity makes it usable again during the day.

Each new day starts with yesterday's configuration copied into an editable draft. Confirm it again (or change it first) before using equipment. Repeated day notifications and weekly display-day resets do not reopen a confirmation. A new run clears both draft and confirmed equipment before inventories and unlocks reset.

The menu uses `UIFlowController` modal arbitration and does not own time scale. Other gameplay input is blocked while it is open. The movement teleport ability, digging, harvesting, removing, and already planted plants are not Loadout entries; autonomous planted-plant growth and nighttime attacks remain unchanged.

## Integration

- `DailyLoadoutState` is the engine-independent equipment/confirmation model in `MagicGarden.Core`.
- `DailyLoadoutController` is a component on the existing `LevelManager` prefab, not a new singleton. It resolves inventory/unlock/player references once in `Awake`; day, flow and pause references are serialized.
- Stable spell catalog IDs and plant seed-type IDs are stored, never mutable seed storage positions. Inventories resolve active equipment to their own entries when accessed.
- `DayCycleController.GameDaySerial` identifies genuine new days separately from the displayed weekly day. `RunReset` clears equipment before reset callbacks rebuild inventory/unlocks.
- Public spell selection/cast predicates, animation-driven casting, and `TilePlantingSystem.TryPlant` all validate equipment and phase. Planting validates again after its animation delay; seed and mana costs are committed by the guarded planting entry point, not charged before a cancellable animation.
- `Assets/Prefabs/UI/DailyLoadout.prefab` contains the serialized six-slot HUD, six-slot draft, lock state, confirmation/clear/close controls and scrollable unlocked-item catalog. `Assets/Scenes/Game.unity` instantiates it. Legacy seed/spell HUD prefabs are inactive and `UIManager` no longer drives them when the unified presenter exists.

## Checks

From the repository root:

```sh
dotnet run --project odd/checks/loadout/Loadout.Checks.csproj
python odd/checks/compile_loadout.py
python odd/checks/check_loadout_assets.py
```

The .NET check executes the actual pure equipment model without Unity. The compiler check requires this checkout's generated Unity `.csproj` files and cached dependency assemblies; it compiles core, gameplay and Editor test sources against those references without importing assets or running Unity. Structural asset checks do not prove Editor deserialization or visual correctness.

In Unity Test Runner, run `DailyLoadoutTests` and `DailyLoadoutIntegrationTests`, followed by the existing EditMode suite. Test fixtures must run in the Test Runner's isolated test environment, not during an active gameplay session. Play Mode checks still required: menu layout at supported resolutions, input/modal interaction, day/night transitions, casting/cooldown effects, delayed planting cancellation, seed restocking, new-run reset, and existing planted-plant nighttime combat.

The project currently records Unity **2022.3.62f3**. A batch run cannot open the project while another Unity instance owns it. No successful .NET/compiler/structural result should be described as Unity Editor runtime validation.
