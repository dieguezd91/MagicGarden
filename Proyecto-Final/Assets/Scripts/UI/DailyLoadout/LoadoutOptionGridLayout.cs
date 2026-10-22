using UnityEngine;
using UnityEngine.UI;

namespace MagicGarden.LoadoutUI
{
    // Fits authored card dimensions inside the configured columns. Does not create or own option data.
    [ExecuteAlways, RequireComponent(typeof(GridLayoutGroup))]
    public sealed class LoadoutOptionGridLayout : MonoBehaviour
    {
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private RectTransform optionTemplate;
        [SerializeField] private ScrollRect scroll;
        private void OnEnable() { Resize(); }
        private void OnRectTransformDimensionsChange() { Resize(); }
        private void LateUpdate() { Resize(); }
        public void RefreshLayout() { Resize(); }
        private void Resize()
        {
            if (grid == null) return;
            float width = ((RectTransform)transform).rect.width - grid.padding.horizontal;
            int columns = Mathf.Max(1, grid.constraintCount);
            float availableWidth = Mathf.Max(1, (width - grid.spacing.x * (columns - 1)) / columns);
            var preferred = optionTemplate != null ? optionTemplate.sizeDelta : new Vector2(150, 150);
            float cellWidth = Mathf.Min(availableWidth, Mathf.Max(1, preferred.x));
            var size = new Vector2(cellWidth, cellWidth * Mathf.Max(1, preferred.y) / Mathf.Max(1, preferred.x));
            if (grid.cellSize != size) grid.cellSize = size;
            if (scroll == null || scroll.viewport == null) return;
            int visible = 0;
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeSelf) continue;
                var ignored = child.GetComponent<LayoutElement>();
                if (ignored == null || !ignored.ignoreLayout) visible++;
            }
            int rows = Mathf.CeilToInt(visible / (float)columns);
            float height = grid.padding.vertical + rows * size.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
            bool overflow = height > scroll.viewport.rect.height + .5f;
            scroll.horizontal = false; scroll.vertical = overflow;
            if (!overflow)
            {
                scroll.StopMovement();
                var content = (RectTransform)transform;
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0);
            }
            if (scroll.verticalScrollbar != null && scroll.verticalScrollbar.gameObject.activeSelf != overflow)
                scroll.verticalScrollbar.gameObject.SetActive(overflow);
        }
    }
}
