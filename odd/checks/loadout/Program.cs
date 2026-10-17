using MagicGarden;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}
bool Spell(string id) => id.StartsWith("spell:");
bool Plant(string id) => id.StartsWith("plant:");
var state = new DailyLoadoutState();
Check(state.BeginDay(0), "Initial day opens");
Check(state.TryEdit(true, 0, "spell:a"), "First draft edit");
Check(state.TryEdit(true, 0, "spell:b"), "Repeated edit");
Check(!state.TryEdit(true, 1, "spell:b"), "Duplicate rejected");
Check(!state.TryEdit(true, 3, "spell:c"), "Fourth slot rejected");
Check(!state.TryEdit(false, -1, "plant:a"), "Negative slot rejected");
Check(state.TryEdit(false, 2, "locked"), "Raw draft can be validated atomically");
Check(!state.TryConfirm(Spell, Plant), "Locked plant rejects confirmation");
Check(!state.IsConfirmed && state.GetEquipped(true, 0) == null, "Failure leaves all state unchanged");
Check(state.TryEdit(false, 2, "plant:a"), "Correct invalid draft");
int events = 0;
state.Confirmed += () =>
{
    events++;
    Check(state.IsConfirmed && state.GetEquipped(false, 2) == "plant:a", "All slots locked before notification");
    Check(!state.TryConfirm(Spell, Plant), "Reentrant confirmation rejected");
    Check(!state.TryEdit(true, 0, "spell:c"), "Reentrant edit rejected");
};
Check(state.TryConfirm(Spell, Plant), "Valid confirmation");
Check(events == 1, "Exactly one commit event");
Check(state.GetEquipped(true, 0) == "spell:b" && state.GetEquipped(false, 0) == null, "Exact slots and empties retained");
Check(!state.TryEdit(true, 0, null) && !state.TryConfirm(Spell, Plant), "Locked reopening cannot edit or confirm");
Check(!state.BeginDay(0) && !state.BeginDay(-1) && state.IsConfirmed, "Duplicate/stale day cannot reopen");
Check(state.BeginDay(1) && !state.IsConfirmed, "New day unlocks");
Check(state.GetDraft(true, 0) == "spell:b", "New draft starts from previous equipment");
Check(state.TryEdit(true, 0, null), "New day can clear equipment");
state.ResetRun();
Check(!state.IsConfirmed && state.GetEquipped(true, 0) == null && state.GetDraft(false, 2) == null, "New run clears all state");
Check(state.BeginDay(0), "New run can start at first day");
var full = new DailyLoadoutState(); full.BeginDay(0);
for (int i = 0; i < 3; i++)
{
    Check(full.TryEdit(true, i, "spell:" + i), "Spell capacity");
    Check(full.TryEdit(false, i, "plant:" + i), "Plant capacity");
}
Check(full.TryConfirm(Spell, Plant), "All six slots commit");
Check(!full.TryEdit(false, 3, "plant:4"), "Plant capacity never exceeds three");
var empty = new DailyLoadoutState(); empty.BeginDay(0);
Check(empty.TryConfirm(Spell, Plant), "Entirely empty configuration allowed");
Check(!empty.TryConfirm(Spell, Plant), "Empty configuration still consumes daily confirmation");
Console.WriteLine($"PASS: {assertions} daily Loadout assertions");
