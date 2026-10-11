
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System;

public class DayNightSpriteSwapper : MonoBehaviour
{
    [Serializable]
    public class VisualSettings
    {
        public Sprite sprite;
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localScale = Vector3.one;

        [Header("Solo para UI")]
        public Vector2 uiSizeDelta = new Vector2(100, 100);
    }

    [Header("CONFIGURACIÓN")]
    [SerializeField] private bool useCustomTransform = true;
    [Space(10)]
    [SerializeField] private VisualSettings daySettings;
    [SerializeField] private VisualSettings nightSettings;

    [Header("REFERENCES")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Image uiImage;

    private RectTransform rectTransform;

    private LevelManager levelManager;
    private GameFlowController gameFlowController;

    private GamePhase lastPhase = GamePhase.None;

    private Coroutine transitionCoroutine;
    private bool isInitialized = false;
    private bool isUI = false;

    private void Start()
    {
        if (uiImage == null)
            uiImage = GetComponent<Image>();

        if (uiImage != null)
        {
            isUI = true;
            rectTransform = GetComponent<RectTransform>();

            if (daySettings.uiSizeDelta == Vector2.zero)
                daySettings.uiSizeDelta = rectTransform.sizeDelta;

            if (nightSettings.uiSizeDelta == Vector2.zero)
                nightSettings.uiSizeDelta = rectTransform.sizeDelta;
        }
        else
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
            {
                enabled = false;
                return;
            }
        }

        StartCoroutine(InitializeDelayed());
    }

    private IEnumerator InitializeDelayed()
    {
        yield return null;

        levelManager = LevelManager.Instance;

        if (levelManager == null)
        {
            enabled = false;
            yield break;
        }

        gameFlowController = GameFlowController.Instance;

        if (gameFlowController == null)
        {
            enabled = false;
            yield break;
        }

        gameFlowController.OnPhaseChanged += HandlePhaseChanged;

        GamePhase currentPhase = gameFlowController.CurrentPhase;

        if (IsDayOrNight(currentPhase))
        {
            lastPhase = currentPhase;
            SetVisuals(currentPhase);
        }

        isInitialized = true;
    }

    private void HandlePhaseChanged(GamePhase newPhase)
    {
        if (!isInitialized)
            return;

        if (!IsDayOrNight(newPhase))
            return;

        lastPhase = newPhase;
        SetVisuals(newPhase);
    }

    private void OnEnable()
    {
        if (!isInitialized || gameFlowController == null)
            return;

        GamePhase currentPhase = gameFlowController.CurrentPhase;

        if (IsDayOrNight(currentPhase))
        {
            lastPhase = currentPhase;
            SetVisuals(currentPhase);
        }
        else if (IsDayOrNight(lastPhase))
        {
            SetVisuals(lastPhase);
        }
    }

    private bool IsDayOrNight(GamePhase phase)
    {
        return phase == GamePhase.Day ||
               phase == GamePhase.Night;
    }

    private void SetVisuals(GamePhase phase)
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
            ResetColorAlpha();
        }

        VisualSettings targetSettings =
            phase == GamePhase.Night ? nightSettings : daySettings;

        if (targetSettings.sprite == null)
            return;

        if (isUI && uiImage != null)
        {
            uiImage.sprite = targetSettings.sprite;
        }
        else if (!isUI && spriteRenderer != null)
        {
            spriteRenderer.sprite = targetSettings.sprite;
        }

        if (useCustomTransform)
        {
            if (isUI && rectTransform != null)
            {
                rectTransform.anchoredPosition = new Vector2(
                    targetSettings.localPosition.x,
                    targetSettings.localPosition.y
                );

                rectTransform.sizeDelta = targetSettings.uiSizeDelta;
            }
            else
            {
                transform.localPosition = targetSettings.localPosition;
            }

            transform.localScale = targetSettings.localScale;
        }
    }

    private void ResetColorAlpha()
    {
        if (isUI && uiImage != null)
        {
            Color c = uiImage.color;
            uiImage.color = new Color(c.r, c.g, c.b, 1f);
        }
        else if (!isUI && spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            spriteRenderer.color = new Color(c.r, c.g, c.b, 1f);
        }
    }

    public void ForceUpdate()
    {
        if (gameFlowController == null)
            gameFlowController = GameFlowController.Instance;

        if (gameFlowController == null)
            return;

        GamePhase currentPhase = gameFlowController.CurrentPhase;

        if (IsDayOrNight(currentPhase))
            lastPhase = currentPhase;

        if (IsDayOrNight(lastPhase))
            SetVisuals(lastPhase);
    }

    private void OnDestroy()
    {
        if (gameFlowController != null)
        {
            gameFlowController.OnPhaseChanged -= HandlePhaseChanged;
        }

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
    }
}
