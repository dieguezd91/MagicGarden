namespace MagicGarden.LoadoutUI
{
    public sealed class LoadoutOptionView : LoadoutEquipmentView
    {
        protected override bool AllowEmptySelection => false;
        public void SetLocked(bool value) { SetLockedState(value); }
    }
}
