using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MagicGarden.LoadoutUI
{
    // Lays the catalog out as one horizontal strip. When the cards do not fit, the strip scrolls sideways
    // and < > arrows appear on the sides that still have cards out of view. Does not create or own option data.
    [ExecuteAlways, RequireComponent(typeof(GridLayoutGroup))]
    public sealed class LoadoutOptionGridLayout : MonoBehaviour
    {
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private RectTransform optionTemplate;
        [SerializeField] private ScrollRect scroll;
        [Header("Arrows (built at runtime when left empty)")]
        [SerializeField] private Button previousArrow;
        [SerializeField] private Button nextArrow;
        [SerializeField] private float arrowWidth = 40f;
        [SerializeField] private float arrowGap = 8f;
        [SerializeField] private float arrowScrollSeconds = 0.25f;

        private bool overflow;
        private bool wasOverflow;
        private float scrollTarget = -1f;
        private float scrollVelocity;

        public bool Overflowing => overflow;

        private void OnEnable() { wasOverflow = false; scrollTarget = -1f; Resize(); }
        private void OnRectTransformDimensionsChange() { Resize(); }
        private void LateUpdate() { Resize(); AnimateArrowScroll(); UpdateArrows(); }
        public void RefreshLayout() { Resize(); }

        public void ResetScroll()
        {
            scrollTarget = -1f;
            if (scroll == null) return;
            scroll.StopMovement();
            if (overflow) scroll.horizontalNormalizedPosition = 0f;
        }

        private void Resize()
        {
            if (grid == null) return;
            var content = (RectTransform)transform;
            float height = scroll != null && scroll.viewport != null ? scroll.viewport.rect.height : content.rect.height;
            height -= grid.padding.vertical;
            var preferred = optionTemplate != null ? optionTemplate.sizeDelta : new Vector2(150, 150);
            float cellHeight = Mathf.Max(1, preferred.y);
            if (height > 1) cellHeight = Mathf.Min(cellHeight, height);
            var size = new Vector2(cellHeight * Mathf.Max(1, preferred.x) / Mathf.Max(1, preferred.y), cellHeight);
            if (grid.cellSize != size) grid.cellSize = size;
            if (scroll == null || scroll.viewport == null) return;

            int visible = 0;
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeSelf) continue;
                var ignored = child.GetComponent<LayoutElement>();
                if (ignored == null || !ignored.ignoreLayout) visible++;
            }
            float width = grid.padding.horizontal + visible * size.x + Mathf.Max(0, visible - 1) * grid.spacing.x;
            float available = ((RectTransform)scroll.transform).rect.width;
            overflow = width > available + .5f;

            scroll.vertical = false; scroll.horizontal = overflow;
            scroll.scrollSensitivity = -Mathf.Abs(scroll.scrollSensitivity); // wheel down moves to the right
            if (scroll.verticalScrollbar != null && scroll.verticalScrollbar.gameObject.activeSelf)
                scroll.verticalScrollbar.gameObject.SetActive(false);

            float inset = overflow && Application.isPlaying ? arrowWidth + arrowGap : 0f;
            var viewport = scroll.viewport;
            if (!Mathf.Approximately(viewport.offsetMin.x, inset) || !Mathf.Approximately(viewport.offsetMax.x, -inset))
            {
                viewport.offsetMin = new Vector2(inset, viewport.offsetMin.y);
                viewport.offsetMax = new Vector2(-inset, viewport.offsetMax.y);
            }

            if (!overflow)
            {
                scroll.StopMovement();
                scrollTarget = -1f;
                float centered = Mathf.Max(0, (viewport.rect.width - width) * .5f);
                if (!Mathf.Approximately(content.anchoredPosition.x, centered) || !Mathf.Approximately(content.anchoredPosition.y, 0))
                    content.anchoredPosition = new Vector2(centered, 0);
            }
            else if (!wasOverflow)
            {
                scroll.StopMovement();
                content.anchoredPosition = new Vector2(0, 0);
            }
            wasOverflow = overflow;
        }

        private void AnimateArrowScroll()
        {
            if (scrollTarget < 0f || scroll == null || !overflow) return;
            float current = scroll.horizontalNormalizedPosition;
            float next = Mathf.SmoothDamp(current, scrollTarget, ref scrollVelocity, arrowScrollSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(next - scrollTarget) < .001f) { next = scrollTarget; scrollTarget = -1f; }
            scroll.horizontalNormalizedPosition = next;
        }

        private void Step(int direction)
        {
            if (scroll == null || !overflow || grid == null) return;
            var content = (RectTransform)transform;
            float hidden = content.rect.width - scroll.viewport.rect.width;
            if (hidden <= 0) return;
            float card = grid.cellSize.x + grid.spacing.x;
            int perPage = Mathf.Max(1, Mathf.FloorToInt((scroll.viewport.rect.width + grid.spacing.x) / card));
            float from = scrollTarget >= 0f ? scrollTarget : scroll.horizontalNormalizedPosition;
            scroll.StopMovement();
            scrollVelocity = 0f;
            scrollTarget = Mathf.Clamp01(from + direction * perPage * card / hidden);
            SoundManager.Instance?.PlayOneShot("ButtonClick", ui: true);
        }

        private void UpdateArrows()
        {
            if (!Application.isPlaying || scroll == null) return;
            EnsureArrows();
            float position = scrollTarget >= 0f ? scrollTarget : scroll.horizontalNormalizedPosition;
            SetVisible(previousArrow, overflow && position > .005f);
            SetVisible(nextArrow, overflow && position < .995f);
        }

        private static void SetVisible(Button arrow, bool visible)
        {
            if (arrow != null && arrow.gameObject.activeSelf != visible) arrow.gameObject.SetActive(visible);
        }

        private void EnsureArrows()
        {
            if (previousArrow == null) previousArrow = BuildArrow("Previous options", "<", 0f);
            if (nextArrow == null) nextArrow = BuildArrow("Next options", ">", 1f);
        }

        private Button BuildArrow(string name, string glyph, float side)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(scroll.transform, false);
            rect.anchorMin = new Vector2(side, 0f); rect.anchorMax = new Vector2(side, 1f);
            rect.pivot = new Vector2(side, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(arrowWidth, -Mathf.Max(0, scroll.viewport.rect.height - grid.cellSize.y));

            var image = go.GetComponent<Image>();
            var styleSource = GetComponentInParent<DailyLoadoutPanelView>()?.PlantTabButton;
            if (styleSource != null && styleSource.image != null && styleSource.image.sprite != null)
            {
                image.sprite = styleSource.image.sprite; image.type = styleSource.image.type;
                image.color = Color.white;
            }
            else image.color = new Color(0f, 0f, 0f, .55f);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<TextMeshProUGUI>();
            var fontSource = styleSource != null ? styleSource.GetComponentInChildren<TMP_Text>(true) : null;
            if (fontSource != null) { text.font = fontSource.font; text.color = fontSource.color; }
            text.text = glyph; text.fontSize = 32; text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;

            var button = go.GetComponent<Button>();
            int direction = side < .5f ? -1 : 1;
            button.onClick.AddListener(() => Step(direction));
            go.SetActive(false);
            return button;
        }
    }
}
