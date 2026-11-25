using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneTransitionController : MonoBehaviour
{
    public static SceneTransitionController Instance { get; private set; }

    [Header("Transition Type")]
    [SerializeField] private TransitionType transitionType = TransitionType.Animated;

    [Header("Fade Configuration")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration;
    [SerializeField] private Color fadeColor;

    [Header("Animated Transition Configuration")]
    [SerializeField] private Image animatedTransitionImage;
    [SerializeField] private Animator transitionAnimator;
    [SerializeField] private string fadeOutAnimationName = "FadeOut";
    [SerializeField] private string fadeInAnimationName = "FadeIn";
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private bool waitForAnimationEnd = true;
    [Header("Loading Settings")]
    [SerializeField] private bool showLoadingScreen = false;
    [SerializeField] private float minimumLoadTime;

    [Header("Transition Sound")]
    // Own source instead of SoundManager: this persists across scenes, including the menu (no SoundManager there).
    [SerializeField] private AudioClip fadeOutSound;
    [SerializeField] private AudioClip fadeInSound;
    [SerializeField] private AudioMixerGroup transitionMixerGroup;
    [SerializeField, Range(0f, 1f)] private float transitionVolume = 0.6f;
    private AudioSource transitionSource;

    [Header("Music")]
    [Tooltip("Seconds before the screen is fully covered that the next scene's music starts, overlapping the old music's fade-out tail so there is no silent gap.")]
    [SerializeField, Min(0f)] private float musicOverlapSeconds = 0.3f;

    [Header("Quit")]
    [Tooltip("Seconds to fade to black while the leaves cover the screen before the game closes.")]
    [SerializeField] private float quitFadeDuration = 1.2f;

    [Header("Startup")]
    [Tooltip("Seconds the leaves stay closed after launch before revealing. Launch frames hitch, so revealing at once is never seen.")]
    [SerializeField] private float startupRevealDelay = 0.6f;

    private bool isTransitioning = false;
    private EventSystem blockedEventSystem;

    /// <summary>True while a transition covers the screen: UI and gameplay input must be ignored (the mouse still moves).</summary>
    public static bool InputBlocked => Instance != null && Instance.isTransitioning;
    private Canvas transitionCanvas;
    private Coroutine currentTransitionCoroutine;

    private void Awake()
    {
        transitionCanvas = GetComponentInParent<Canvas>();

        if (Instance == null)
        {
            Instance = this;

            if (transitionCanvas != null)
            {
                DontDestroyOnLoad(transitionCanvas.gameObject);
            }
            else
            {
                DontDestroyOnLoad(gameObject);
            }

            Initialize();
        }
        else
        {
            if (transitionCanvas != null)
            {
                Destroy(transitionCanvas.gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Catches the new scene's EventSystem before its first Update.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SyncInputBlock();

    private void LateUpdate() => SyncInputBlock();

    // Disabling the EventSystem stops clicks, hover, keyboard and gamepad navigation at once.
    private void SyncInputBlock()
    {
        if (isTransitioning)
        {
            // EventSystem.current is null once disabled, so a non-null one is a freshly loaded scene's.
            EventSystem active = EventSystem.current;
            if (active != null)
            {
                active.enabled = false;
                blockedEventSystem = active;
            }
        }
        else if (blockedEventSystem != null)
        {
            blockedEventSystem.enabled = true;
            blockedEventSystem = null;
        }
    }

    private void Initialize()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (transitionCanvas != null)
        {
            transitionCanvas.sortingOrder = 9999;
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            if (transitionCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                transitionCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        if (transitionType == TransitionType.Simple && fadeImage != null)
        {
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
            fadeImage.raycastTarget = false;
            fadeImage.gameObject.SetActive(true);

            if (animatedTransitionImage != null)
                animatedTransitionImage.gameObject.SetActive(false);
        }
        else if (transitionType == TransitionType.Animated && animatedTransitionImage != null)
        {
            animatedTransitionImage.color = Color.white;
            animatedTransitionImage.raycastTarget = false;
            animatedTransitionImage.gameObject.SetActive(false);

            if (fadeImage != null)
                fadeImage.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (Instance == this)
        {
            // Game launch: start fully covered by leaves, then reveal.
            if (transitionType == TransitionType.Animated && transitionAnimator != null && animatedTransitionImage != null)
            {
                animatedTransitionImage.gameObject.SetActive(true);
                transitionAnimator.Play(fadeOutAnimationName, 0, 1f);
                transitionAnimator.Update(0f); // apply the covered pose before the first frame renders
            }
            isTransitioning = true; // no input (or second transition) until the reveal ends
            StartCoroutine(StartupReveal());
        }
    }

    private IEnumerator StartupReveal()
    {
        // Skip the loading-hitch frames, then hold so the closed leaves are actually seen.
        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(startupRevealDelay);
        yield return StartCoroutine(FadeIn());
        isTransitioning = false;
    }

    #region Public API

    public void LoadScene(string sceneName, Action onSceneLoaded = null)
    {
        if (!isTransitioning)
        {
            StartCoroutine(TransitionToScene(sceneName, onSceneLoaded));
        }
    }

    public void LoadScene(int sceneIndex, Action onSceneLoaded = null)
    {
        if (!isTransitioning)
        {
            StartCoroutine(TransitionToScene(sceneIndex, onSceneLoaded));
        }
    }

    /// <summary>Cover with leaves while fading to black, then call <paramref name="quit"/>.</summary>
    public void QuitWithTransition(Action quit)
    {
        if (isTransitioning) return;
        if (currentTransitionCoroutine != null) StopCoroutine(currentTransitionCoroutine);
        currentTransitionCoroutine = StartCoroutine(QuitRoutine(quit));
    }

    private IEnumerator QuitRoutine(Action quit)
    {
        isTransitioning = true;

        // Black overlay above the leaves; also blocks clicks while quitting.
        var blackGO = new GameObject("QuitFade", typeof(RectTransform), typeof(Image));
        blackGO.transform.SetParent(transitionCanvas != null ? transitionCanvas.transform : transform, false);
        blackGO.transform.SetAsLastSibling();
        var rect = (RectTransform)blackGO.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var black = blackGO.GetComponent<Image>();
        black.color = new Color(0f, 0f, 0f, 0f);
        black.raycastTarget = true;

        Coroutine leaves = StartCoroutine(FadeOut());

        // Unscaled: quitting from the pause menu runs at timeScale 0.
        float startVolume = AudioListener.volume;
        for (float t = 0f; t < quitFadeDuration; t += Time.unscaledDeltaTime)
        {
            float k = t / quitFadeDuration;
            black.color = new Color(0f, 0f, 0f, k);
            AudioListener.volume = startVolume * (1f - k);
            yield return null;
        }
        black.color = Color.black;
        AudioListener.volume = 0f;
        yield return leaves;

        quit?.Invoke();
#if UNITY_EDITOR
        // Editor only stops Play mode; don't leave the next session muted.
        AudioListener.volume = startVolume;
#endif
    }

    public void FadeOutOnly(Action onComplete = null)
    {
        if (currentTransitionCoroutine != null)
        {
            StopCoroutine(currentTransitionCoroutine);
        }
        currentTransitionCoroutine = StartCoroutine(FadeOutCoroutine(onComplete));
    }

    public void FadeInOnly(Action onComplete = null)
    {
        if (currentTransitionCoroutine != null)
        {
            StopCoroutine(currentTransitionCoroutine);
        }
        currentTransitionCoroutine = StartCoroutine(FadeInCoroutine(onComplete));
    }

    #endregion

    #region Scene Transition Logic

    // The old music fades out while the screen is being covered. The next scene's music starts
    // musicOverlapSeconds before the cover completes, so the two only overlap briefly and there is no silent gap.
    private void FadeOutMusicWhileCovering(string nextSceneName)
    {
        if (MusicManager.Instance == null) return;
        float cover = CoverSeconds();
        MusicManager.Instance.FadeOutForSceneChange(cover);
        StartCoroutine(StartNextSceneMusic(nextSceneName, Mathf.Max(0f, cover - musicOverlapSeconds)));
    }

    private IEnumerator StartNextSceneMusic(string sceneName, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (MusicManager.Instance != null) MusicManager.Instance.StartSceneMusic(sceneName);
    }

    private static string SceneNameForIndex(int index)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(index);
        return string.IsNullOrEmpty(path) ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(path);
    }

    private float CoverSeconds()
    {
        if (transitionType == TransitionType.Simple) return fadeDuration;
        if (waitForAnimationEnd && transitionAnimator != null && transitionAnimator.runtimeAnimatorController != null)
        {
            float longest = 0f;
            foreach (var clip in transitionAnimator.runtimeAnimatorController.animationClips)
                if (clip != null) longest = Mathf.Max(longest, clip.length);
            if (longest > 0f) return longest;
        }
        return animationDuration;
    }

    private IEnumerator TransitionToScene(string sceneName, Action onSceneLoaded = null)
    {
        isTransitioning = true;
        FadeOutMusicWhileCovering(sceneName);
        float startTime = Time.realtimeSinceStartup;

        yield return StartCoroutine(FadeOut());

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        float elapsedTime = Time.realtimeSinceStartup - startTime;
        if (elapsedTime < minimumLoadTime)
        {
            yield return new WaitForSecondsRealtime(minimumLoadTime - elapsedTime);
        }

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        onSceneLoaded?.Invoke();

        yield return new WaitForSecondsRealtime(0.1f);

        yield return StartCoroutine(FadeIn());

        isTransitioning = false;
    }

    private IEnumerator TransitionToScene(int sceneIndex, Action onSceneLoaded = null)
    {
        isTransitioning = true;
        FadeOutMusicWhileCovering(SceneNameForIndex(sceneIndex));
        float startTime = Time.realtimeSinceStartup;

        yield return StartCoroutine(FadeOut());

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        float elapsedTime = Time.realtimeSinceStartup - startTime;
        if (elapsedTime < minimumLoadTime)
        {
            yield return new WaitForSecondsRealtime(minimumLoadTime - elapsedTime);
        }

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        onSceneLoaded?.Invoke();

        yield return new WaitForSecondsRealtime(0.1f);

        yield return StartCoroutine(FadeIn());

        isTransitioning = false;
    }

    #endregion

    #region Fade Effects

    private void PlayTransitionSound(AudioClip clip)
    {
        if (clip == null) return;
        if (transitionSource == null)
        {
            transitionSource = gameObject.AddComponent<AudioSource>();
            transitionSource.playOnAwake = false;
            transitionSource.ignoreListenerPause = true; // transitions can run while paused (timeScale 0)
            transitionSource.outputAudioMixerGroup = transitionMixerGroup;
        }
        transitionSource.PlayOneShot(clip, transitionVolume);
    }

    private IEnumerator FadeOut()
    {
        PlayTransitionSound(fadeOutSound);
        if (transitionType == TransitionType.Simple)
        {
            yield return StartCoroutine(FadeOutSimple());
        }
        else
        {
            yield return StartCoroutine(FadeOutAnimated());
        }
    }

    private IEnumerator FadeIn()
    {
        PlayTransitionSound(fadeInSound);
        if (transitionType == TransitionType.Simple)
        {
            yield return StartCoroutine(FadeInSimple());
        }
        else
        {
            yield return StartCoroutine(FadeInAnimated());
        }
    }

    private IEnumerator FadeOutSimple()
    {
        if (fadeImage == null) yield break;

        float elapsedTime = 0f;
        Color startColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        Color endColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / fadeDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            fadeImage.color = Color.Lerp(startColor, endColor, smoothT);
            yield return null;
        }

        fadeImage.color = endColor;
    }

    private IEnumerator FadeInSimple()
    {
        if (fadeImage == null) yield break;

        float elapsedTime = 0f;
        Color startColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
        Color endColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / fadeDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            fadeImage.color = Color.Lerp(startColor, endColor, smoothT);
            yield return null;
        }

        fadeImage.color = endColor;
    }

    private IEnumerator FadeOutAnimated()
    {
        if (transitionAnimator == null || animatedTransitionImage == null)
        {
            yield break;
        }

        animatedTransitionImage.gameObject.SetActive(true);

        Canvas.ForceUpdateCanvases();

        transitionAnimator.Play(fadeOutAnimationName, 0, 0f);

        if (waitForAnimationEnd)
        {
            yield return new WaitForEndOfFrame();

            AnimatorStateInfo stateInfo = transitionAnimator.GetCurrentAnimatorStateInfo(0);
            float animLength = stateInfo.length;

            if (animLength <= 0f)
            {
                animLength = animationDuration;
            }

            yield return new WaitForSecondsRealtime(animLength);
        }
        else
        {
            yield return new WaitForSecondsRealtime(animationDuration);
        }
    }

    private IEnumerator FadeInAnimated()
    {
        if (transitionAnimator == null || animatedTransitionImage == null)
        {
            yield break;
        }

        // Reveal = the cover animation scrubbed backwards. Playing the reversed (speed -1) state via
        // Animator.Play snaps to the end instead of animating, so drive the time ourselves.
        animatedTransitionImage.gameObject.SetActive(true);
        transitionAnimator.Play(fadeOutAnimationName, 0, 1f);
        transitionAnimator.Update(0f);
        float animLength = transitionAnimator.GetCurrentAnimatorStateInfo(0).length;
        if (!waitForAnimationEnd || animLength <= 0f)
            animLength = animationDuration;

        float previousSpeed = transitionAnimator.speed;
        transitionAnimator.speed = 0f; // we set the time each frame
        for (float t = 0f; t < animLength; t += Time.unscaledDeltaTime)
        {
            transitionAnimator.Play(fadeOutAnimationName, 0, 1f - t / animLength);
            transitionAnimator.Update(0f);
            yield return null;
        }
        transitionAnimator.Play(fadeOutAnimationName, 0, 0f);
        transitionAnimator.Update(0f);
        transitionAnimator.speed = previousSpeed;

        animatedTransitionImage.gameObject.SetActive(false);
    }

    private IEnumerator FadeOutCoroutine(Action onComplete = null)
    {
        yield return StartCoroutine(FadeOut());
        onComplete?.Invoke();
        currentTransitionCoroutine = null;
    }

    private IEnumerator FadeInCoroutine(Action onComplete = null)
    {
        yield return StartCoroutine(FadeIn());
        onComplete?.Invoke();
        currentTransitionCoroutine = null;
    }

    #endregion

    public bool IsTransitioning => isTransitioning;
}

public enum TransitionType
{
    Simple,
    Animated
}