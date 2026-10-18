namespace MagicGarden.LoadoutUI
{
    public sealed class LoadoutOptionView : LoadoutEquipmentView
    {
        protected override bool AllowEmptySelection => false;
        protected override bool AvailabilityTextOnly => true;
        public void SetLocked(bool value) { SetLockedState(value); }
    }
}
