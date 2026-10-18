using UnityEngine;
using UnityEngine.UI;

namespace MagicGarden.LoadoutUI
{
    // Keeps the three-column option grid inside its viewport at different aspect ratios.
    [ExecuteAlways, RequireComponent(typeof(GridLayoutGroup))]
    public sealed class LoadoutOptionGridLayout : MonoBehaviour
    {
        [SerializeField] private GridLayoutGroup grid;
        private void OnEnable() { Resize(); }
        private void OnRectTransformDimensionsChange() { Resize(); }
        private void Resize()
        {
            if (grid == null) return;
            float width = ((RectTransform)transform).rect.width - grid.padding.horizontal;
            int columns = Mathf.Max(1, grid.constraintCount);
            float cellWidth = Mathf.Max(1, (width - grid.spacing.x * (columns - 1)) / columns);
            var size = new Vector2(cellWidth, 120);
            if (grid.cellSize != size) grid.cellSize = size;
        }
    }
}
