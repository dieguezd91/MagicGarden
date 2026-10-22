namespace MagicGarden.LoadoutUI
{
    public sealed class LoadoutOptionView : LoadoutEquipmentView
    {
        [UnityEngine.SerializeField] private LoadoutOptionDragSource dragSource;
        public void BindDrag(DailyLoadoutPresenter presenter, int index)
        {
            if (dragSource != null) dragSource.Bind(presenter, index);
        }
        protected override bool AllowEmptySelection => false;
        public void SetLocked(bool value) { SetLockedState(value); }
    }
}
