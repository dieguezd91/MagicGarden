using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicGarden;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static UpgradeUIArtTests;

// Editor test assembly deliberately enters real Play Mode. Reflection preserves the Assembly-CSharp boundary.
public class UpgradeUIArtPlayModeChecks
{
    private static readonly string Output = UpgradeUICompactLayoutTests.Output;
    [UnityTearDown] public IEnumerator LeavePlayModeAfterFailure()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
    private static Component Find(string type) => (Component)UnityEngine.Object.FindObjectOfType(TypeOf(type));
    private static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).SetValue(target, value);
    private static void Listen(object target, string name, Delegate callback) => target.GetType().GetEvent(name).AddEventHandler(target, callback);
    private static Component Panel(string name, Transform parent)
    {
        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab"), parent, false);
        return go.GetComponent(TypeOf(name + "View"));
    }
    private static IList Data(string name, int count)
    {
        var type = TypeOf(name); var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));
        for (int i = 0; i < count; i++)
        {
            var data = Activator.CreateInstance(type);
            foreach (var field in type.GetFields())
            {
                if (field.FieldType == typeof(string)) field.SetValue(data, field.Name + " " + i);
                if (field.FieldType == typeof(Sprite)) field.SetValue(data, Sprite("CraftButton"));
                if (field.FieldType == typeof(Color)) field.SetValue(data, new Color(.72f, .5f, 1));
            }
            list.Add(data);
        }
        return list;
    }
    private static void Pointer(Component button, string handler)
    {
        var eventType = TypeOf("UnityEngine.EventSystems.EventSystem", "UnityEngine.UI");
        var current = eventType.GetProperty("current").GetValue(null);
        var data = Activator.CreateInstance(TypeOf("UnityEngine.EventSystems.PointerEventData", "UnityEngine.UI"), current);
        var execute = TypeOf("UnityEngine.EventSystems.ExecuteEvents", "UnityEngine.UI");
        var callback = execute.GetProperty(handler + "Handler").GetValue(null);
        var contract = TypeOf("UnityEngine.EventSystems.I" + char.ToUpperInvariant(handler[0]) + handler.Substring(1) + "Handler", "UnityEngine.UI");
        execute.GetMethods().Single(m => m.Name == "Execute" && m.IsGenericMethod).MakeGenericMethod(contract)
            .Invoke(null, new[] { (object)button.gameObject, data, callback });
    }
    private static void Click(Component button)
    {
        Pointer(button, "pointerEnter"); Pointer(button, "pointerDown"); Pointer(button, "pointerUp"); Pointer(button, "pointerClick");
    }
    internal static IEnumerator ButtonStates(Component button)
    {
        var eventType = TypeOf("UnityEngine.EventSystems.EventSystem", "UnityEngine.UI");
        var events = eventType.GetProperty("current").GetValue(null);
        eventType.GetMethod("SetSelectedGameObject", new[] { typeof(GameObject) }).Invoke(events, new object[] { null });
        Pointer(button, "pointerExit");
        string[] states = { "Normal", "Highlighted", "Pressed", "Selected", "Disabled" };
        foreach (string state in states)
        {
            if (state == "Highlighted") Pointer(button, "pointerEnter");
            if (state == "Pressed") Pointer(button, "pointerDown");
            if (state == "Selected") { Pointer(button, "pointerUp"); Call(button, "Select"); }
            if (state == "Disabled") Set(button, "interactable", false);
            yield return new WaitForSecondsRealtime(.15f);
            var current = button.GetType().GetProperty("currentSelectionState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button);
            Assert.That(current.ToString(), Is.EqualTo(state));
            var expected = (Color)Property(Property(button, "colors"), char.ToLowerInvariant(state[0]) + state.Substring(1) + "Color");
            var renderer = (CanvasRenderer)Property(Property(button, "targetGraphic"), "canvasRenderer");
            var actual = renderer.GetColor();
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.01)); Assert.That(actual.g, Is.EqualTo(expected.g).Within(.01));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.01)); Assert.That(actual.a, Is.EqualTo(expected.a).Within(.01));
        }
        Set(button, "interactable", true); Pointer(button, "pointerExit");
        Debug.Log("UI_ART_STATES: Normal/Highlighted/Pressed/Selected/Disabled transition states and rendered graphic tints verified.");
    }
    private static Rect Bounds(RectTransform r)
    {
        var corners = new Vector3[4]; r.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners.Min(p => p.x), corners.Min(p => p.y), corners.Max(p => p.x), corners.Max(p => p.y));
    }
    private static void TextFit(GameObject root, string evidence)
    {
        var lines = new List<string>();
        foreach (var label in Components(root, "TMPro.TextMeshProUGUI"))
        {
            if (!label.gameObject.activeInHierarchy) continue;
            Call(label, "ForceMeshUpdate", true, true);
            bool overflowing = (bool)Property(label, "isTextOverflowing");
            var r = (RectTransform)label.transform;
            lines.Add(label.name + " | " + Property(label, "text") + " | rect=" + r.rect.size + " | font=" + Property(label, "fontSize") + " | overflow=" + overflowing);
            Assert.That(overflowing, Is.False, label.name + ": " + Property(label, "text"));
            Assert.That((float)Property(label, "fontSize"), Is.GreaterThanOrEqualTo(24));
            var margin = (Vector4)Property(label, "margin");
            Assert.That(margin.x, Is.GreaterThanOrEqualTo(4)); Assert.That(margin.z, Is.GreaterThanOrEqualTo(4));
            Assert.That(margin.y, Is.GreaterThanOrEqualTo(2)); Assert.That(margin.w, Is.GreaterThanOrEqualTo(2));
            if (!string.IsNullOrWhiteSpace((string)Property(label, "text")))
            {
                var ink = (UnityEngine.Bounds)Property(label, "textBounds");
                Assert.That(ink.min.x, Is.GreaterThanOrEqualTo(r.rect.xMin - 1), label.name + " ink left");
                Assert.That(ink.max.x, Is.LessThanOrEqualTo(r.rect.xMax + 1), label.name + " ink right");
                Assert.That(ink.min.y, Is.GreaterThanOrEqualTo(r.rect.yMin - 1), label.name + " ink bottom");
                Assert.That(ink.max.y, Is.LessThanOrEqualTo(r.rect.yMax + 1), label.name + " ink top");
                // Measure visible glyph ink against the actual sliced border, not merely the outer frame rect.
                for (var parent = label.transform.parent; parent != null; parent = parent.parent)
                {
                    var image = parent.GetComponent(TypeOf("UnityEngine.UI.Image", "UnityEngine.UI"));
                    if (image == null || Property(image, "sprite") != (object)Sprite("TutorialBox")) continue;
                    var frame = (RectTransform)parent;
                    var sprite = (Sprite)Property(image, "sprite");
                    var canvas = parent.GetComponentInParent<Canvas>();
                    Vector4 border = sprite.border * canvas.referencePixelsPerUnit / sprite.pixelsPerUnit / (float)Property(image, "pixelsPerUnitMultiplier");
                    var a = frame.InverseTransformPoint(label.transform.TransformPoint(ink.min));
                    var b = frame.InverseTransformPoint(label.transform.TransformPoint(ink.max));
                    Assert.That(a.x, Is.GreaterThanOrEqualTo(frame.rect.xMin + border.x + 2 - .1f), label.name + " sliced ink left");
                    Assert.That(a.y, Is.GreaterThanOrEqualTo(frame.rect.yMin + border.y + 2 - .1f), label.name + " sliced ink bottom");
                    Assert.That(b.x, Is.LessThanOrEqualTo(frame.rect.xMax - border.z - 2 + .1f), label.name + " sliced ink right");
                    Assert.That(b.y, Is.LessThanOrEqualTo(frame.rect.yMax - border.w - 2 + .1f), label.name + " sliced ink top");
                    break;
                }
            }
        }
        File.WriteAllLines(Output + evidence + "-text-fit.txt", lines);
        foreach (var card in root.GetComponentsInChildren(TypeOf("LevelUpCardView"), false))
        {
            var icon = (Component)Field(card, "icon");
            var target = (Component)Field(card, "targetLabel");
            Assert.That(Bounds((RectTransform)icon.transform).Overlaps(Bounds((RectTransform)target.transform)), Is.False);
            var button = Field(card, "selectButton"); var graphic = (Component)Property(button, "targetGraphic");
            Assert.That(Bounds((RectTransform)graphic.transform).Overlaps(Bounds((RectTransform)((Component)Field(card, "previewLabel")).transform)), Is.False);
        }
        foreach (var row in root.GetComponentsInChildren(TypeOf("SpecialUpgradeItemView"), false))
        {
            var graphic = (Component)Property(Field(row, "purchaseButton"), "targetGraphic");
            foreach (string field in new[] { "costsLabel", "prerequisitesLabel" })
                Assert.That(Bounds((RectTransform)graphic.transform).Overlaps(Bounds((RectTransform)((Component)Field(row, field)).transform)), Is.False, field);
        }
    }
    // world=true renders the Game camera under the canvas, i.e. what the player sees (the blur snapshot included); otherwise UI only on a flat background.
    internal static IEnumerator Capture(Canvas canvas, GameObject panel, string name, int width, int height, bool measured = true, bool world = false)
    {
        Assert.That(SystemInfo.graphicsDeviceType.ToString(), Is.Not.EqualTo("Null"), "Native graphics device required");
        var go = new GameObject("UI capture camera"); var camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .18f, .14f);
        camera.orthographic = true; camera.orthographicSize = 540; camera.cullingMask = 1 << 5; camera.transform.position = new Vector3(0, 0, -10);
        if (world && Camera.main != null)
        {
            var main = Camera.main; camera.CopyFrom(main); camera.cullingMask = main.cullingMask | (1 << 5); camera.transform.position = main.transform.position;
        }
        var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt;
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        int oldLayer = canvas.sortingLayerID, oldOrder = canvas.sortingOrder;
        float oldScale = canvas.scaleFactor, oldDistance = canvas.planeDistance;
        var scaler = canvas.GetComponent(TypeOf("UnityEngine.UI.CanvasScaler", "UnityEngine.UI")) as Behaviour;
        bool scalerEnabled = scaler != null && scaler.enabled;
        var previous = RenderTexture.active;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            if (world) { canvas.sortingLayerID = SortingLayer.layers[SortingLayer.layers.Length - 1].id; canvas.sortingOrder = short.MaxValue; } // draw over every world sprite, like Overlay does
            // CanvasScaler uses actual Screen dimensions; disable it ONLY for an exact reference-resolution capture.
            if (scaler != null)
            {
                Assert.That(Property(scaler, "referenceResolution"), Is.EqualTo(new Vector2(1920, 1080)));
                Assert.That(Property(scaler, "matchWidthOrHeight"), Is.EqualTo(.5f));
                scaler.enabled = false;
            }
            Directory.CreateDirectory(Output);
            canvas.scaleFactor = Mathf.Sqrt(width / 1920f * height / 1080f); // existing Canvas matchWidthOrHeight=.5
            Canvas.ForceUpdateCanvases();
            yield return null; // Let the graphics-enabled player loop build and render Canvas meshes into the camera target.
            yield return null;
            string evidence = name + "-" + width + "x" + height;
            if (measured) { UpgradeUICompactLayoutTests.MeasuredCapture(canvas, panel, evidence, width, height); TextFit(panel, evidence); }
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32();
                File.WriteAllBytes(Output + evidence + ".png", texture.EncodeToPNG());
                Assert.That(pixels.Select(p => p.r + ":" + p.g + ":" + p.b).Distinct().Take(32).Count(), Is.GreaterThan(16), "Capture must contain real rendered UI");
                Debug.Log("UI_ART_CAPTURE " + evidence + " graphics=" + SystemInfo.graphicsDeviceType);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        finally
        {
            if (scaler != null) scaler.enabled = scalerEnabled;
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.sortingLayerID = oldLayer; canvas.sortingOrder = oldOrder;
            canvas.scaleFactor = oldScale; canvas.planeDistance = oldDistance; RenderTexture.active = previous;
            camera.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
        }
    }
    // The reward is shown alone: drawn after the level-up panel, over the level-up session's single backdrop, with the panel hidden.
    private static void AssertRewardHierarchy(Component presenter, Component reward, Component level)
    {
        Assert.That(reward.transform.GetSiblingIndex(), Is.GreaterThan(level.transform.GetSiblingIndex()));
        var backdrop = SessionBackdrop(presenter);
        Assert.That(backdrop.transform.GetSiblingIndex(), Is.LessThan(level.transform.GetSiblingIndex()), "the blur is behind both modals");
        Assert.That(backdrop.gameObject.activeInHierarchy, Is.True);
        Assert.That(backdrop.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
        Assert.That(level.gameObject.activeSelf, Is.False, "LevelUpPanel stays hidden while the reward is visible");
    }
    private static Component SessionBackdrop(Component presenter) => (Component)Field(presenter, "sessionBackdrop");
    private static float BackdropAlpha(Component backdrop) => backdrop.GetComponent<CanvasGroup>().alpha;
    // The same backdrop instance (same blur snapshot, not re-captured) is shown, active and fully opaque: no hide/show blink in between.
    private static void AssertBackdropSteady(Component backdrop, object snapshot, string when)
    {
        Assert.That((bool)Property(backdrop, "IsShown"), Is.True, when + ": still shown");
        Assert.That(backdrop.gameObject.activeSelf, Is.True, when + ": still active");
        Assert.That(Property(backdrop, "HasBlur"), Is.True, when + ": blur kept");
        Assert.That(Field(backdrop, "blurTexture"), Is.SameAs(snapshot), when + ": no new snapshot");
        Assert.That(BackdropAlpha(backdrop), Is.GreaterThanOrEqualTo(.99f), when + ": no fade dip");
    }
    private static IEnumerator WaitBackdropOpaque(Component backdrop, float seconds = 2f)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end && BackdropAlpha(backdrop) < .999f) yield return null;
        Assert.That(BackdropAlpha(backdrop), Is.EqualTo(1f).Within(.001f));
    }
    private sealed class Game
    {
        public Component flow, runtime, presenter, experience, unlocks, indicator, reward, level;
        public object modal;
        public Canvas canvas;
    }
    // The scene load itself must be yielded by the test method (nested enumerators can only yield null in Edit Mode tests).
    private static Game SetupGame()
    {
        var g = new Game();
        var tutorial = Find("TutorialManager"); if (tutorial != null) Call(tutorial, "SkipTutorial"); // Test fixture bypass only.
        g.flow = Find("GameFlowController"); Call(g.flow, "SetPhase", Enum.Parse(TypeOf("GamePhase"), "Day"));
        g.modal = Property(Find("UIManager"), "Flow");
        var currentModal = Property(g.modal, "CurrentModal"); if (currentModal.ToString() != "None") Call(g.modal, "Close", currentModal);
        g.runtime = Find("UpgradeRuntime"); g.presenter = Find("UpgradePanel"); g.experience = Find("PlayerExperienceSystem");
        g.unlocks = Find("PlayerContentUnlockSystem"); g.indicator = Find("LevelUpIndicator");
        Assert.That(g.runtime, Is.Not.Null); Assert.That(((Behaviour)g.runtime).enabled, Is.True); Assert.That(Property(g.presenter, "IsReady"), Is.True);
        Assert.That(g.indicator, Is.Not.Null, "Player prefab carries the LevelUpIndicator");
        g.level = (Component)Field(g.presenter, "levelUpView"); g.reward = (Component)Field(g.presenter, "rewardView");
        g.canvas = g.presenter.GetComponent<Canvas>();
        Call(g.experience, "ResetProgression"); Time.timeScale = 1;
        return g;
    }
    // No lambdas: a closure over test locals is created before EnterPlayMode and is gone after the domain reload.
    private static IEnumerator WaitForChoosing(Component flow, float seconds, string what)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!(bool)Property(flow, "IsChoosingUpgrade") && Time.realtimeSinceStartup < end) { SkipTutorialIfActive(); yield return null; }
        Assert.That(Property(flow, "IsChoosingUpgrade"), Is.True, what + " | " + FlowState(flow));
    }
    // Test fixture bypass only: the tutorial can (re)start after the scene fade and would gate the upgrade flow.
    private static void SkipTutorialIfActive()
    {
        var tutorial = Find("TutorialManager");
        if (tutorial != null && (bool)Call(tutorial, "IsTutorialActive")) Call(tutorial, "SkipTutorial");
    }
    private static string FlowState(Component flow)
    {
        var experience = Find("PlayerExperienceSystem"); var ui = Find("UIManager"); var modal = Property(ui, "Flow");
        var tutorial = Find("TutorialManager"); var life = experience.GetComponent(TypeOf("LifeController"));
        return "timeScale=" + Time.timeScale + " worldPhase=" + Property(flow, "WorldPhase") + " modal=" + Property(modal, "CurrentModal") +
            " hasOpenModal=" + Property(modal, "HasOpenModal") + " pending=" + Property(experience, "PendingChoices") +
            " alive=" + Call(life, "IsAlive") + " tutorialActive=" + (tutorial != null ? Call(tutorial, "IsTutorialActive") : "n/a") +
            " paused=" + Property(Find("PauseController"), "IsPaused");
    }
    private static bool Choosing(Game g) => (bool)Property(g.flow, "IsChoosingUpgrade");
    // The level-up modals tween in/out on unscaled time; selection is blocked until they settle, so flows wait for it.
    private static IEnumerator WaitIdle(Component reward, Component level, float seconds = 5f)
    {
        float end = Time.realtimeSinceStartup + seconds;
        yield return null;
        while (Time.realtimeSinceStartup < end && ((bool)Property(reward, "IsAnimating") || (bool)Property(level, "IsAnimating"))) yield return null;
        Assert.That(Property(reward, "IsAnimating"), Is.False, "reward tween settled"); Assert.That(Property(level, "IsAnimating"), Is.False, "level-up tween settled");
    }
    private static IEnumerator WaitIdle(Game g, float seconds = 5f) => WaitIdle(g.reward, g.level, seconds);
    private static bool RewardHasContent(Game g, int level)
    {
        var reward = Call(g.unlocks, "GetLevelReward", level);
        return reward != null && (bool)Property(reward, "HasContent");
    }
    private static void CaptureWorld(Camera camera, string name, int width, int height)
    {
        Directory.CreateDirectory(Output);
        var rt = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + name + "-" + width + "x" + height + ".png", texture.EncodeToPNG());
            Debug.Log("UI_ART_CAPTURE " + name + "-" + width + "x" + height + " world");
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = previous; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
    private static void CaptureCloseUp(Camera main, Transform player, Transform icon, string name, int width, int height)
    {
        var go = new GameObject("Indicator close-up camera"); var close = go.AddComponent<Camera>();
        try
        {
            close.CopyFrom(main); close.orthographicSize = 3.4f;
            close.transform.position = new Vector3(player.position.x, (player.position.y + icon.position.y) / 2f, main.transform.position.z); // frames player and icon
            CaptureWorld(close, name, width, height);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    [UnityTest] public IEnumerator LevelUpIndicatorShowsAbovePlayerAndHoldsTheModalUntilItFinishes()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        Call(g.experience, "AddExperience", 100);
        Assert.That(Property(g.indicator, "IsPlaying"), Is.True, "plays the same frame the level is gained");
        // Sample deterministically while the ring is active; the old .75s shots are after it has faded.
        Call(g.indicator, "Tick", .18f);
        var ring = (SpriteRenderer)Field(g.indicator, "ring");
        Assert.That(ring, Is.Not.Null); Assert.That(ring.enabled, Is.True);
        CaptureCloseUp(Camera.main, g.experience.transform, ring.transform, "runtime-levelup-ring-peak", 1280, 720);
        Call(g.indicator, "Play");
        float start = Time.realtimeSinceStartup;
        yield return new WaitForSecondsRealtime(.75f);
        Assert.That(Property(g.indicator, "IsPlaying"), Is.True);
        Assert.That(Choosing(g), Is.False); Assert.That(Property(g.modal, "CurrentModal").ToString(), Is.EqualTo("None"));
        var icon = (SpriteRenderer)Field(g.indicator, "icon"); var body = ((Component)g.experience).GetComponent<SpriteRenderer>();
        Assert.That(icon.enabled, Is.True); Assert.That(icon.sprite.name, Is.EqualTo("LevelUpIcon"));
        Assert.That(icon.bounds.center.y, Is.GreaterThan(body.bounds.max.y), "icon is above the player");
        Assert.That(icon.sortingOrder, Is.GreaterThan(body.sortingOrder));
        Assert.That(Time.timeScale, Is.EqualTo(1f), "gameplay is not frozen during the feedback");
        var main = Camera.main; Assert.That(main, Is.Not.Null);
        CaptureWorld(main, "runtime-levelup-indicator-scene", 1920, 1080);
        CaptureCloseUp(main, body.transform, icon.transform, "runtime-levelup-indicator-closeup", 1280, 720);
        float end = Time.realtimeSinceStartup + 4f;
        while ((bool)Property(g.indicator, "IsPlaying") && Time.realtimeSinceStartup < end)
        {
            Assert.That(Choosing(g), Is.False, "modal must not open while the indicator plays");
            Assert.That(Property(g.modal, "CurrentModal").ToString(), Is.Not.EqualTo("LevelUp"));
            SkipTutorialIfActive();
            yield return null;
        }
        Assert.That(Property(g.indicator, "IsPlaying"), Is.False);
        float played = Time.realtimeSinceStartup - start;
        Assert.That(played, Is.InRange(1.0f, 2.5f));
        yield return WaitForChoosing(g.flow, 3f, "modal opens once the indicator finished");
        Assert.That(Property(g.modal, "CurrentModal").ToString(), Is.EqualTo("LevelUp"));
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator ExperienceBarEndsOnTheExactValueAfterAGainAndOnTheNewLevelRangeAfterSeveralLevels()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        var bar = Find("ExperienceUIController"); Assert.That(bar, Is.Not.Null, "the Game scene has the XP bar");
        var slider = (Component)Field(bar, "xpSlider"); var levelText = (Component)Field(bar, "levelText"); var xpText = (Component)Field(bar, "xpText");
        var scale = slider.transform.localScale;
        yield return null;
        Call(g.experience, "AddExperience", 10);
        yield return new WaitForSecondsRealtime(.04f);
        Assert.That((float)Property(slider, "value"), Is.LessThan(10f), "fill is animated, not snapped");
        yield return Capture(g.canvas, slider.gameObject, "runtime-xp-mid-fill", 1920, 1080, false, true);
        yield return WaitBarValue(g, slider, 2f);
        Assert.That(Property(slider, "value"), Is.EqualTo(10f));
        Assert.That(Property(slider, "maxValue"), Is.EqualTo((float)(int)Property(g.experience, "ExperienceRequired")));
        Call(g.experience, "AddExperience", 400);
        int level = (int)Property(g.experience, "CurrentLevel");
        Assert.That(level, Is.GreaterThanOrEqualTo(3), "several levels in one gain");
        Assert.That(Property(levelText, "text"), Is.EqualTo("LV. " + level), "texts update immediately");
        Assert.That(Property(xpText, "text"), Is.EqualTo((int)Property(g.experience, "CurrentExperience") + " / " + (int)Property(g.experience, "ExperienceRequired") + " XP"));
        yield return WaitBarValue(g, slider, 4f);
        int required = (int)Property(g.experience, "ExperienceRequired");
        int floor = (int)Call(Property(g.experience, "ProgressionData"), "GetLevelThreshold", level);
        Assert.That(Property(slider, "minValue"), Is.EqualTo((float)floor));
        Assert.That(Property(slider, "maxValue"), Is.EqualTo((float)(required > floor ? required : floor + 1)));
        Assert.That(Property(slider, "value"), Is.EqualTo((float)(int)Property(g.experience, "CurrentExperience")));
        Assert.That(slider.transform.localScale.x, Is.EqualTo(scale.x).Within(1e-3f), "the punch leaves no scale drift");
        yield return Capture(g.canvas, slider.gameObject, "runtime-xp-settled", 1920, 1080, false, true);
        yield return new ExitPlayMode();
    }
    private static IEnumerator WaitBarValue(Game g, Component slider, float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        float expected = (int)Property(g.experience, "CurrentExperience");
        // An approximate intermediate value is not completion: the controller writes the exact target on completion.
        while (Time.realtimeSinceStartup < end && (float)Property(slider, "value") != expected) yield return null;
        Assert.That((float)Property(slider, "value"), Is.EqualTo(expected), "XP animation must reach its exact target before the deadline");
    }
    [UnityTest] public IEnumerator ManaPickupVisibleInkMatchesMaterialIconHeight()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        var labels = ShowPickupGeometrySamples(g);
        yield return new WaitForSecondsRealtime(.65f);
        var mana = PickupVisibleInk(labels[0], out var manaCap, out var manaPlus);
        var crystal = PickupVisibleInk(labels[1], out var crystalCap, out var crystalPlus);
        CaptureCloseUp(Camera.main, g.experience.transform, labels[0].transform.parent, "runtime-pickup-icon-size", 1280, 720);
        Assert.That(mana.size.y / crystal.size.y, Is.EqualTo(1f).Within(.1f), "compare visible artwork, not transparent sprite quads");
        Assert.That(mana.center.y, Is.EqualTo(manaCap.center.y).Within(manaCap.size.y * .25f), "larger mana stays vertically centered on the text");
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator PickupIconTextGapsAreCompactWithoutOverlapping()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        var labels = ShowPickupGeometrySamples(g);
        yield return new WaitForSecondsRealtime(.65f);
        foreach (var label in labels)
        {
            var ink = PickupVisibleInk(label, out var cap, out var plus);
            float gap = (plus.min.x - ink.max.x) / cap.size.y;
            Debug.Log("PICKUP_VISIBLE_GAP text=" + Property(label, "text") + " capFraction=" + gap);
            Assert.That(gap, Is.InRange(0f, .3f), "icon-to-text gap must be compact and not overlap");
        }
        yield return new ExitPlayMode();
    }
    private static Component[] ShowPickupGeometrySamples(Game g)
    {
        var feedback = g.experience.GetComponentInChildren(TypeOf("FloatingTextController"), true);
        string manaPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("ManaItem t:Prefab").Single());
        var mana = AssetDatabase.LoadAssetAtPath<GameObject>(manaPath).GetComponentInChildren<SpriteRenderer>(true).sprite;
        var crystal = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy Materials/Spectral Crystal.prefab").GetComponentInChildren<SpriteRenderer>(true).sprite;
        Call(feedback, "ShowPickup", "Mana", 10, mana);
        Call(feedback, "ShowPickup", "Spectral Crystal", 3, crystal);
        var panel = (Transform)Field(feedback, "pickupPanel");
        var labels = Components(panel.gameObject, "TMPro.TextMeshProUGUI");
        return new[] { labels.Single(l => ((string)Property(l, "text")).EndsWith("Mana")), labels.Single(l => ((string)Property(l, "text")).EndsWith("Spectral Crystal")) };
    }
    // Pixel alpha establishes visible artwork bounds; TMP quads include the transparent padding that caused this regression.
    private static Bounds PickupVisibleInk(Component label, out Bounds cap, out Bounds plus)
    {
        Call(label, "ForceMeshUpdate", true, true);
        var info = Property(label, "textInfo");
        var chars = ((Array)Field(info, "characterInfo")).Cast<object>().Take((int)Field(info, "characterCount")).ToArray();
        var sprite = chars.Single(c => Field(c, "elementType").ToString() == "Sprite" && (bool)Field(c, "isVisible"));
        var glyph = Property(Field(sprite, "textElement"), "glyph");
        var rect = Property(glyph, "glyphRect");
        int x = (int)Property(rect, "x"), y = (int)Property(rect, "y");
        int w = (int)Property(rect, "width"), h = (int)Property(rect, "height");
        var texture = ((Sprite)Field(glyph, "sprite")).texture;
        var previous = RenderTexture.active;
        var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        int minX = w, minY = h, maxX = -1, maxY = -1;
        try
        {
            Graphics.Blit(texture, target); RenderTexture.active = target;
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); readable.Apply(false, false);
            var pixels = readable.GetPixels32();
            for (int row = 0; row < h; row++) for (int col = 0; col < w; col++)
            {
                if (pixels[(y + row) * texture.width + x + col].a <= 16) continue;
                minX = Mathf.Min(minX, col); minY = Mathf.Min(minY, row);
                maxX = Mathf.Max(maxX, col); maxY = Mathf.Max(maxY, row);
            }
        }
        finally
        {
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(readable);
        }
        Assert.That(maxX >= minX && maxY >= minY, Is.True, "glyph must contain actual visible pixels");
        Bounds CharacterBounds(object character)
        {
            var result = new Bounds(); result.SetMinMax((Vector3)Field(character, "bottomLeft"), (Vector3)Field(character, "topRight")); return result;
        }
        cap = CharacterBounds(chars.First(c => Field(c, "elementType").ToString() == "Character" && (bool)Field(c, "isVisible") && char.IsUpper((char)Field(c, "character"))));
        plus = CharacterBounds(chars.First(c => (char)Field(c, "character") == '+' && (bool)Field(c, "isVisible")));
        var quad = CharacterBounds(sprite);
        var ink = new Bounds();
        ink.SetMinMax(quad.min + Vector3.Scale(quad.size, new Vector3((float)minX / w, (float)minY / h, 0)),
            quad.min + Vector3.Scale(quad.size, new Vector3((float)(maxX + 1) / w, (float)(maxY + 1) / h, 0)));
        Debug.Log("PICKUP_VISIBLE_INK text=" + Property(label, "text") + " pixels=" + (maxX - minX + 1) + "x" + (maxY - minY + 1) + " height=" + ink.size.y + " cap=" + cap.size.y);
        return ink;
    }
    [UnityTest] public IEnumerator SimultaneousResourcePickupsStackAsEntriesWhoseTextStaysOnOneLine()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        var feedback = g.experience.GetComponentInChildren(TypeOf("FloatingTextController"), true);
        string manaPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("ManaItem t:Prefab").Single());
        var mana = AssetDatabase.LoadAssetAtPath<GameObject>(manaPath).GetComponentInChildren<SpriteRenderer>(true).sprite;
        var crystal = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy Materials/Spectral Crystal.prefab").GetComponentInChildren<SpriteRenderer>(true).sprite;
        Call(feedback, "ShowPickup", "Mana", 10, mana);
        Call(feedback, "ShowPickup", "Spectral Crystal", 3, crystal);
        var sourceAsset = Field(feedback, "itemSpriteAsset");
        foreach (var resource in new[] { "WindwalkerEssence", "VoltaicCore", "EternalEmber" })
        {
            int index = (int)Call(sourceAsset, "GetSpriteIndexFromName", resource);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            var character = ((IList)Property(sourceAsset, "spriteCharacterTable"))[index];
            var icon = (Sprite)Field(Property(character, "glyph"), "sprite");
            Call(feedback, "ShowPickup", resource, 2, icon);
        }
        yield return new WaitForSecondsRealtime(.65f);
        var panel = (Transform)Field(feedback, "pickupPanel");
        var labels = Components(panel.gameObject, "TMPro.TextMeshProUGUI");
        Assert.That(labels.Length, Is.EqualTo(5));
        var manaLabel = labels.Single(l => ((string)Property(l, "text")).EndsWith("+10 Mana"));
        var crystalLabel = labels.Single(l => ((string)Property(l, "text")).EndsWith("+3 Spectral Crystal"));
        var centers = new List<Vector3>(); float capHeight = 0f;
        foreach (var label in labels)
        {
            Call(label, "ForceMeshUpdate", true, true);
            var info = Property(label, "textInfo");
            Assert.That(Field(info, "lineCount"), Is.EqualTo(1), "icon, amount and name read on one line: " + Property(label, "text"));
            var chars = ((Array)Field(info, "characterInfo")).Cast<object>().Take((int)Field(info, "characterCount")).Where(c => (bool)Field(c, "isVisible")).ToArray();
            Assert.That(chars.Select(c => (int)Field(c, "lineNumber")).Distinct().Count(), Is.EqualTo(1), "no visible character drops to another line");
            var letter = chars.First(c => Field(c, "elementType").ToString() == "Character" && char.IsUpper((char)Field(c, "character")));
            var min = (Vector3)Field(letter, "bottomLeft"); var max = (Vector3)Field(letter, "topRight");
            float letterCenter = ((min + max) * .5f).y, letterHeight = max.y - min.y;
            foreach (var c in chars)
            {
                float center = (((Vector3)Field(c, "bottomLeft")).y + ((Vector3)Field(c, "topRight")).y) * .5f;
                Assert.That(center, Is.EqualTo(letterCenter).Within(letterHeight * .6f), "every glyph and icon of an entry sits on the same line");
            }
            centers.Add(label.transform.TransformPoint((min + max) * .5f));
            capHeight = Mathf.Max(capHeight, label.transform.TransformVector(max - min).y);
        }
        CaptureWorld(Camera.main, "runtime-pickups-stacked-single-line-entries", 1280, 720);
        var rows = centers.Select(c => c.y).OrderBy(y => y).ToArray();
        for (int i = 1; i < rows.Length; i++)
            Assert.That(rows[i] - rows[i - 1], Is.GreaterThan(capHeight), "different resources stack one above the other without overlapping");
        // Batchmode's Game view aspect is arbitrary; judge visibility at the same 16:9 framing as the capture.
        Camera.main.aspect = 16f / 9f;
        for (int i = 0; i < labels.Length; i++)
        {
            var bounds = (Bounds)Property(labels[i], "textBounds");
            var min = labels[i].transform.TransformPoint(bounds.min); var max = labels[i].transform.TransformPoint(bounds.max);
            Assert.That(Camera.main.WorldToViewportPoint(min).x, Is.GreaterThanOrEqualTo(0f), "the complete row stays visible");
            Assert.That(Camera.main.WorldToViewportPoint(max).x, Is.LessThanOrEqualTo(1f), "the complete row stays visible");
        }
        Camera.main.ResetAspect();
        Call(feedback, "ShowPickup", "Mana", 5, mana);
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(Components(panel.gameObject, "TMPro.TextMeshProUGUI").Length, Is.EqualTo(5), "repeated resources reuse their inline entry");
        Assert.That(Property(manaLabel, "text"), Does.EndWith("+15 Mana"));
        Assert.That(Property(crystalLabel, "text"), Does.EndWith("+3 Spectral Crystal"));
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator ManaPickupRendersItsSpriteCountsRepeatedGainsAndSettlesWithoutScaleDrift()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        var feedback = g.experience.GetComponentInChildren(TypeOf("FloatingTextController"), true);
        Assert.That(feedback, Is.Not.Null);
        string manaPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("ManaItem t:Prefab").Single());
        var mana = AssetDatabase.LoadAssetAtPath<GameObject>(manaPath).GetComponentInChildren<SpriteRenderer>(true).sprite;
        Call(feedback, "ShowPickup", "Mana", 1, mana);
        yield return new WaitForSecondsRealtime(.4f);
        var panel = (Transform)Field(feedback, "pickupPanel");
        var label = Components(panel.gameObject, "TMPro.TextMeshProUGUI").Single();
        Call(label, "ForceMeshUpdate", true, true);
        var textInfo = Property(label, "textInfo");
        var characters = ((Array)Field(textInfo, "characterInfo")).Cast<object>().Take((int)Field(textInfo, "characterCount")).ToArray();
        Debug.Log("MANA_RENDER_PROBE sprite=" + mana.name + " resolved=" + Call(feedback, "ResolveSpriteName", mana.name) +
            " text=" + Property(label, "text") + " active=" + label.gameObject.activeInHierarchy +
            " enabled=" + ((Behaviour)label).enabled + " canvas=" + label.GetComponentInParent<Canvas>());
        foreach (var character in characters)
            Debug.Log("MANA_CHARACTER type=" + Field(character, "elementType") + " visible=" + Field(character, "isVisible") +
                " spriteIndex=" + Field(character, "spriteIndex") + " asset=" + Field(character, "spriteAsset") +
                " min=" + Field(character, "bottomLeft") + " max=" + Field(character, "topRight"));
        // TMP 3.0.7 can overwrite spriteCount after parsing; require actual visible sprite geometry instead.
        var sprites = characters.Where(c => Field(c, "elementType").ToString() == "Sprite" && (bool)Field(c, "isVisible")).ToArray();
        Assert.That(sprites.Length, Is.EqualTo(1), "the mana icon must generate visible sprite geometry");
        var spriteInfo = sprites.Single();
        Assert.That((int)Field(spriteInfo, "spriteIndex"), Is.GreaterThanOrEqualTo(0));
        var renderedGlyph = Property(Field(spriteInfo, "textElement"), "glyph");
        Assert.That((Sprite)Field(renderedGlyph, "sprite") == mana, Is.True, "the pickup copy retains the actual mana artwork");
        var sharedAsset = Field(feedback, "itemSpriteAsset");
        int sharedIndex = (int)Call(sharedAsset, "GetSpriteIndexFromName", mana.name);
        var sharedCharacter = ((IList)Property(sharedAsset, "spriteCharacterTable"))[sharedIndex];
        var sharedRect = Property(Property(sharedCharacter, "glyph"), "glyphRect");
        Assert.That(Property(sharedRect, "width"), Is.EqualTo(16), "the shared shop/HUD glyph must not be cropped");
        Assert.That(Property(sharedRect, "height"), Is.EqualTo(16));
        var scale = label.transform.localScale;
        CaptureCloseUp(Camera.main, g.experience.transform, label.transform, "runtime-mana-pickup", 1280, 720);
        var letterInfo = characters.First(c => (char)Field(c, "character") == 'M');
        var spriteMin = (Vector3)Field(spriteInfo, "bottomLeft"); var spriteMax = (Vector3)Field(spriteInfo, "topRight");
        var letterMin = (Vector3)Field(letterInfo, "bottomLeft"); var letterMax = (Vector3)Field(letterInfo, "topRight");
        float iconCenter = (spriteMin.y + spriteMax.y) * .5f;
        float textCenter = (letterMin.y + letterMax.y) * .5f;
        float capHeight = letterMax.y - letterMin.y;
        Assert.That(spriteMax.x, Is.GreaterThan(spriteMin.x), "sprite has rendered width");
        Assert.That(spriteMax.y, Is.GreaterThan(spriteMin.y), "sprite has rendered height");
        Assert.That(spriteMin.x, Is.GreaterThanOrEqualTo((float)Field(spriteInfo, "origin") - .001f), "mana icon must not draw left of its inline origin");
        Debug.Log("MANA_ALIGNMENT iconCenter=" + iconCenter + " textCenter=" + textCenter + " capHeight=" + capHeight);
        Assert.That(iconCenter, Is.EqualTo(textCenter).Within(capHeight * .25f), "mana icon must be vertically aligned with the text, not centered on its baseline");
        Call(feedback, "ShowPickup", "Mana", 9, mana);
        yield return new WaitForSecondsRealtime(.08f);
        CaptureCloseUp(Camera.main, g.experience.transform, label.transform, "runtime-mana-counting", 1280, 720);
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That((string)Property(label, "text"), Does.EndWith("+10 Mana"));
        Assert.That(label.transform.localScale, Is.EqualTo(scale), "repeat restores the exact resting scale");
        CaptureCloseUp(Camera.main, g.experience.transform, label.transform, "runtime-mana-settled", 1280, 720);
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator RewardModalShowsAloneThenUpgradeCardsAfterContinue()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        Assert.That(RewardHasContent(g, 2), Is.False, "not recorded before the level is gained");
        Call(g.experience, "AddExperience", 100);
        Assert.That(RewardHasContent(g, 2), Is.True);
        yield return WaitForChoosing(g.flow, 5f, "upgrade flow starts");
        int pending = (int)Property(g.experience, "PendingChoices");
        Assert.That(Property(g.reward, "IsVisible"), Is.True);
        Assert.That(g.level.gameObject.activeSelf, Is.False, "level-up panel hidden while the reward is visible");
        AssertRewardHierarchy(g.presenter, g.reward, g.level);
        var backdrop = SessionBackdrop(g.presenter);
        Assert.That(Property(g.reward, "IsAnimating"), Is.True, "the reward card pops in");
        yield return new WaitForSecondsRealtime(.04f);
        Assert.That(BackdropAlpha(backdrop), Is.InRange(.01f, .9f), "the blur fades in instead of appearing at once");
        yield return Capture(g.canvas, g.reward.gameObject, "runtime-reward-mid-open", 1920, 1080, false, true);
        yield return WaitIdle(g);
        var levelTwo = Call(g.unlocks, "GetLevelReward", 2);
        Assert.That((string)Property(Field(g.reward, "plantLimitLabel"), "text"), Is.EqualTo("Plant limit: +1 (" + Property(levelTwo, "PlantLimit") + ")"), "the plant limit ends at its final value once the rows finished");
        yield return WaitBackdropOpaque(backdrop);
        var snapshot = Field(backdrop, "blurTexture"); Assert.That(snapshot, Is.Not.Null);
        AssertBlurred(backdrop, "reward alone");
        yield return Capture(g.canvas, g.reward.gameObject, "runtime-reward-open", 1920, 1080, false, true);
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) }) yield return Capture(g.canvas, g.reward.gameObject, "runtime-reward-alone", size.x, size.y, false);
        // Selection is impossible while the reward is visible, both through the presenter and the runtime.
        g.presenter.GetType().GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(g.presenter, new object[] { 0 });
        Call(g.runtime, "Choose", 0); yield return null;
        Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(pending));
        Assert.That(Property(g.reward, "IsVisible"), Is.True);
        Click((Component)Field(g.reward, "continueButton")); yield return null;
        float clicked = Time.realtimeSinceStartup;
        Assert.That(Property(g.reward, "IsVisible"), Is.True, "still fading out");
        Assert.That(g.level.gameObject.activeSelf, Is.False, "the upgrade cards wait for the reward to be gone");
        Call(g.runtime, "Choose", 0); Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(pending), "blocked while the reward tween runs");
        // Reward -> upgrades: the single backdrop never hides, blinks or re-snapshots; captures mid-transition.
        bool closingShot = false, openingShot = false; float limit = clicked + 3f;
        while (Time.realtimeSinceStartup < limit && !(g.level.gameObject.activeSelf && !(bool)Property(g.level, "IsAnimating") && !(bool)Property(g.reward, "IsVisible")))
        {
            AssertBackdropSteady(backdrop, snapshot, "across Continue");
            if (!closingShot && Time.realtimeSinceStartup - clicked > .1f && (bool)Property(g.reward, "IsVisible"))
            { closingShot = true; yield return Capture(g.canvas, g.reward.gameObject, "runtime-reward-to-upgrades-reward-closing", 1920, 1080, false, true); }
            else if (!openingShot && g.level.gameObject.activeSelf && (bool)Property(g.level, "IsAnimating"))
            { openingShot = true; yield return Capture(g.canvas, g.level.gameObject, "runtime-reward-to-upgrades-panel-opening", 1920, 1080, false, true); }
            yield return null;
        }
        yield return WaitIdle(g);
        Assert.That(Property(g.reward, "IsVisible"), Is.False);
        Assert.That(g.level.gameObject.activeSelf, Is.True, "upgrade panel appears after Continue");
        Assert.That(Choosing(g), Is.True);
        AssertBackdropSteady(backdrop, snapshot, "upgrades after Continue");
        AssertBlurred(backdrop, "upgrades after Continue");
        Assert.That(backdrop.transform.GetSiblingIndex(), Is.LessThan(g.level.transform.GetSiblingIndex()), "the blur is behind the upgrade modal");
        yield return Capture(g.canvas, g.level.gameObject, "runtime-upgrades-after-continue-world", 1920, 1080, false, true);
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) }) yield return Capture(g.canvas, g.level.gameObject, "runtime-upgrades-after-reward", size.x, size.y);
        var cards = (Array)Field(g.level, "cards");
        Click((Component)Field(cards.GetValue(0), "selectButton")); yield return null;
        Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(pending - 1));
        Assert.That(Choosing(g), Is.False); Assert.That(Time.timeScale, Is.EqualTo(1f));
        // The session ends with the choice: only now does the backdrop fade out (softly, then it is released).
        Assert.That((bool)Property(backdrop, "IsShown"), Is.False);
        yield return new WaitForSecondsRealtime(.1f);
        Assert.That(backdrop.gameObject.activeSelf, Is.True, "still fading out");
        Assert.That(BackdropAlpha(backdrop), Is.InRange(.01f, .99f), "the blur fades out instead of vanishing");
        yield return WaitBackdrop(backdrop, false);
        Assert.That(Property(backdrop, "HasBlur"), Is.False);
        yield return new ExitPlayMode();
    }
    // Night gold feedback: each gold line starts at "+0", counts to its final text on unscaled time, and an invalid
    // designer format warns once per line (not on every counter step) and falls back to the default text.
    [UnityTest] public IEnumerator ReenabledExperiencePresenterKeepsNightXpBehindCollectedFeedback()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        var bar = Find("ExperienceUIController"); var feedback = Find("FloatingTextController");
        var slider = (Component)Field(bar, "xpSlider");
        var levelText = Field(bar, "levelText"); var xpText = Field(bar, "xpText");
        Call(g.experience, "AddExperience", 99);
        yield return WaitBarValue(g, slider, 2f);
        var oldLevel = Property(levelText, "text"); var oldXp = Property(xpText, "text"); var oldValue = Property(slider, "value");
        Call(Find("DayCycleController"), "StartNight");
        Call(Find("LevelManager"), "ForceEndNight");
        int earnedXp = (int)Property(g.experience, "CurrentExperience");
        int pending = (int)Property(g.experience, "PendingChoices");
        Assert.That(Property(feedback, "IsShowingGoldFeedback"), Is.True);
        Assert.That(earnedXp, Is.GreaterThan(99));
        ((Behaviour)bar).enabled = false;
        ((Behaviour)bar).enabled = true;
        Assert.That(Property(levelText, "text"), Is.EqualTo(oldLevel), "re-enable must not reveal night levels during collected feedback");
        Assert.That(Property(xpText, "text"), Is.EqualTo(oldXp)); Assert.That(Property(slider, "value"), Is.EqualTo(oldValue));
        bar.gameObject.SetActive(false); bar.gameObject.SetActive(true);
        Assert.That(Property(levelText, "text"), Is.EqualTo(oldLevel), "HUD re-enable keeps the same barrier");
        Assert.That(Property(xpText, "text"), Is.EqualTo(oldXp)); Assert.That(Property(slider, "value"), Is.EqualTo(oldValue));
        Assert.That(Property(bar, "IsPresentationBusy"), Is.True, "the current earned state remains queued, not lost");
        Assert.That(Property(g.experience, "CurrentExperience"), Is.EqualTo(earnedXp));
        Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(pending));
        // Reset must discard the deferred gain, even while the collected feedback is still playing.
        Call(g.experience, "ResetProgression");
        Assert.That(Property(bar, "IsPresentationBusy"), Is.False);
        Assert.That(Property(g.indicator, "HasPendingPresentation"), Is.False);
        Assert.That(Property(levelText, "text"), Is.EqualTo("LV. 1"));
        Assert.That(Property(slider, "value"), Is.EqualTo(0f));
        Call(g.experience, "AddExperience", 100);
        Assert.That(Property(feedback, "IsShowingGoldFeedback"), Is.True);
        Assert.That(Property(bar, "IsPresentationBusy"), Is.True);
        Assert.That(Property(levelText, "text"), Is.EqualTo("LV. 1"));
        float deadline = Time.realtimeSinceStartup + 12f;
        while ((bool)Property(feedback, "IsShowingGoldFeedback") && Time.realtimeSinceStartup < deadline)
        {
            SkipTutorialIfActive();
            Assert.That(Property(levelText, "text"), Is.EqualTo("LV. 1"));
            Assert.That(Property(slider, "value"), Is.EqualTo(0f));
            Assert.That(Choosing(g), Is.False);
            yield return null;
        }
        Assert.That(Property(feedback, "IsShowingGoldFeedback"), Is.False);
        yield return WaitForChoosing(g.flow, 5f, "fresh XP animates and opens its choice after gold; the reset gain never replays");
        Assert.That(Property(levelText, "text"), Is.EqualTo("LV. " + Property(g.experience, "CurrentLevel")));
        Assert.That(Property(slider, "value"), Is.EqualTo((float)(int)Property(g.experience, "CurrentExperience")));
        Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(1));
        Call(g.experience, "ResetProgression");
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator NightFeedbackThenExperienceThenAllChoicesFinishBeforeSleepFadeWakeAndDoorOpen()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        var day = Find("DayCycleController"); var manager = Find("LevelManager");
        var transition = Find("NightToDayTransitionController"); var feedback = Find("FloatingTextController");
        var bar = Find("ExperienceUIController"); var inventory = Find("InventoryManager"); var rewards = Find("RewardsSystem");
        Assert.That(transition, Is.Not.Null); Assert.That(feedback, Is.Not.Null); Assert.That(bar, Is.Not.Null);
        var black = (Component)Field(transition, "blackScreen"); var slider = Field(bar, "xpSlider");
        var goldPanel = (Transform)Field(feedback, "goldRewardPanel");
        var xpLevelText = Field(bar, "levelText");
        var animator = (Animator)Field(transition, "playerAnimator");
        var door = Find("HouseDoor"); Assert.That(door, Is.Not.Null);
        var doorAnimator = (Component)Field(door, "doorLockAnimator"); Assert.That(doorAnimator, Is.Not.Null);
        var original = (UnityEngine.Object)Property(g.experience, "ProgressionData");
        var progression = UnityEngine.Object.Instantiate(original);
        SetField(progression, "experiencePerNight", 450); SetField(g.experience, "progressionData", progression);
        // Keep a visible fade for the overlap regression, while avoiding unrelated camera/travel waits.
        SetField(transition, "freeMovementDuration", .05f); SetField(transition, "cameraFocusTimeout", 0f);
        SetField(transition, "beforeFadeDelay", .05f); SetField(transition, "fadeInDuration", 2f);
        SetField(transition, "blackScreenDuration", .1f); SetField(transition, "fadeOutDuration", .1f);
        try
        {
            Call(g.experience, "AddExperience", 99);
            yield return WaitBarValue(g, (Component)slider, 2f);
            Call(day, "StartNight");
            int goldBefore = (int)Call(inventory, "GetGold");
            int expectedGold = (int)Field(rewards, "rewardNoPlantsDestroyed") + (int)Field(rewards, "rewardHomeUntouched") + (int)Field(rewards, "rewardPlayerUnharmed");
            Call(manager, "ForceEndNight"); // actual public completion path: spawner -> LevelManager -> grants/transition
            Assert.That((int)Call(inventory, "GetGold"), Is.EqualTo(goldBefore + expectedGold), "gold is granted synchronously, not deferred for animation");
            Assert.That(Property(g.experience, "CurrentExperience"), Is.EqualTo(549), "night XP is granted synchronously");
            int pending = (int)Property(g.experience, "PendingChoices");
            Assert.That(pending, Is.GreaterThanOrEqualTo(3), "one night can grant several levels");
            Assert.That(goldPanel.childCount, Is.GreaterThan(0));
            Assert.That(Property(transition, "IsTransitioning"), Is.True);
            CaptureCloseUp(Camera.main, g.experience.transform, goldPanel, "runtime-night-feedback-before-transition", 1280, 720);
            yield return new WaitForSecondsRealtime(.25f);
            CaptureCloseUp(Camera.main, g.experience.transform, goldPanel, "runtime-night-collected-feedback", 1280, 720);
            float deadline = Time.realtimeSinceStartup + 12f;
            while (goldPanel.childCount > 0 && goldPanel.GetComponent<CanvasGroup>().alpha > .001f && Time.realtimeSinceStartup < deadline)
            {
                SkipTutorialIfActive();
                Assert.That(Property(g.indicator, "IsPlaying"), Is.False, "collected feedback must finish before LvUp starts");
                Assert.That(Property(xpLevelText, "text"), Is.EqualTo("LV. 1"), "XP presentation, not the earned XP data, waits for collected feedback");
                Assert.That(Property(slider, "value"), Is.EqualTo(99f));
                Assert.That(Choosing(g), Is.False);
                Assert.That(animator.GetBool("isSleeping"), Is.False);
                Assert.That(black.gameObject.activeSelf, Is.False);
                yield return null;
            }
            Assert.That(goldPanel.childCount == 0 || goldPanel.GetComponent<CanvasGroup>().alpha <= .001f, Is.True, "collected feedback must finish before its deadline");
            deadline = Time.realtimeSinceStartup + 2f;
            while (!(bool)Property(g.indicator, "IsPlaying") && Time.realtimeSinceStartup < deadline) { SkipTutorialIfActive(); yield return null; }
            Assert.That(Property(g.indicator, "IsPlaying"), Is.True, "LvUp starts after collected feedback");
            Assert.That(Property(xpLevelText, "text"), Is.EqualTo("LV. " + Property(g.experience, "CurrentLevel")));
            CaptureCloseUp(Camera.main, g.experience.transform, g.indicator.transform, "runtime-night-experience-after-collected", 1280, 720);
            deadline = Time.realtimeSinceStartup + 5f;
            while (((bool)Property(g.indicator, "IsPlaying") || (float)Property(slider, "value") != (float)(int)Property(g.experience, "CurrentExperience")) && Time.realtimeSinceStartup < deadline)
            {
                SkipTutorialIfActive();
                Assert.That(Choosing(g), Is.False, "choices wait for XP and LvUp");
                Assert.That(animator.GetBool("isSleeping"), Is.False);
                Assert.That(black.gameObject.activeSelf, Is.False);
                yield return null;
            }
            yield return WaitForChoosing(g.flow, 3f, "choices follow XP, before the night animation");
            var backdrop = SessionBackdrop(g.presenter);
            for (int step = 0; step < pending; step++)
            {
                yield return WaitIdle(g);
                Assert.That(Property(g.flow, "WorldPhase").ToString(), Is.EqualTo("Night"));
                Assert.That(animator.GetBool("isSleeping"), Is.False, "no collapse before all choices are completed");
                Assert.That(black.gameObject.activeSelf, Is.False);
                Assert.That(Property(g.reward, "IsVisible"), Is.True);
                if (step == 0) yield return Capture(g.canvas, g.reward.gameObject, "runtime-night-selectable-reward-before-collapse", 1280, 720, false, true);
                int beforeChoice = (int)Property(g.experience, "PendingChoices");
                Click((Component)Field(g.reward, "continueButton"));
                yield return WaitIdle(g);
                Assert.That(animator.GetBool("isSleeping"), Is.False);
                var cards = (Array)Field(g.level, "cards");
                Click((Component)Field(cards.GetValue(0), "selectButton"));
                yield return null;
                Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(beforeChoice - 1), "exactly one pending level is consumed per selection");
            }
            Assert.That(Choosing(g), Is.False);
            yield return WaitBackdrop(backdrop, false);
            bool sawSleep = false, sawFade = false, sawDay = false, capturedWake = false;
            deadline = Time.realtimeSinceStartup + 12f;
            while ((bool)Property(transition, "IsTransitioning") && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(0));
                Assert.That(Choosing(g), Is.False);
                sawSleep |= animator.GetBool("isSleeping");
                sawFade |= black.gameObject.activeInHierarchy && ((Color)Property(black, "color")).a > .001f;
                bool isDay = Property(g.flow, "WorldPhase").ToString() == "Day";
                sawDay |= isDay;
                if (isDay)
                {
                    Assert.That(animator.GetBool("isSleeping"), Is.False, "day triggers waking");
                    Assert.That(Call(doorAnimator, "IsShowingClosedState"), Is.True, "door opening waits until waking is visible and complete");
                    if (!black.gameObject.activeSelf && !capturedWake)
                    {
                        capturedWake = true;
                        CaptureCloseUp(Camera.main, g.experience.transform, door.transform, "runtime-night-day-wake-before-door", 1280, 720);
                    }
                }
                yield return null;
            }
            Assert.That(Property(transition, "IsTransitioning"), Is.False, "night animation must finish before its deadline");
            Assert.That(sawSleep && sawFade && sawDay, Is.True, "collapse, fade and day still run after the choices");
            Assert.That(capturedWake, Is.True);
            Assert.That(Property(g.flow, "WorldPhase").ToString(), Is.EqualTo("Day"));
            Assert.That(black.gameObject.activeSelf, Is.False);
            var doorSprites = (IList)Field(doorAnimator, "sprites");
            var doorRenderer = doorAnimator.GetComponent<SpriteRenderer>();
            deadline = Time.realtimeSinceStartup + 4f;
            while (doorRenderer.sprite != (Sprite)doorSprites[0] && Time.realtimeSinceStartup < deadline) yield return null;
            // An open lock may be an empty sprite; use Unity's object equality, not CLR reference identity.
            Assert.That(doorRenderer.sprite == (Sprite)doorSprites[0], Is.True, "door opening finishes after the day wake");
            Assert.That((Sprite)doorSprites[doorSprites.Count - 1] != null, Is.True, "the closed lock must have actual visible art");
            CaptureCloseUp(Camera.main, g.experience.transform, door.transform, "runtime-night-day-door-open", 1280, 720);
            Assert.That((int)Call(inventory, "GetGold"), Is.EqualTo(goldBefore + expectedGold), "presentation never grants gold twice");
        }
        finally
        {
            SetField(g.experience, "progressionData", original);
            UnityEngine.Object.Destroy(progression);
            Time.timeScale = 1f;
        }
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator NightGoldLinesCountFromZeroToTheirFinalTextAndWarnOncePerLineOnAnInvalidFormat()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var floating = Find("FloatingTextController");
        Assert.That(floating, Is.Not.Null, "the Game scene has a FloatingTextController");
        var tmpType = TypeOf("TMPro.TextMeshProUGUI", "Unity.TextMeshPro");
        var rewardType = floating.GetType().GetNestedType("GoldReward");
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rewardType));
        foreach (var (message, gold) in new[] { ("Defenses Intact", 30), ("Home Protected", 50) })
        {
            object reward = Activator.CreateInstance(rewardType);
            rewardType.GetField("message").SetValue(reward, message);
            rewardType.GetField("goldAmount").SetValue(reward, gold);
            rewardType.GetField("textColor").SetValue(reward, Color.white);
            list.Add(reward);
        }
        SetField(floating, "goldEntryFormat", "{0} {3}");
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Invalid text format"));
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Invalid text format"));
        float savedScale = Time.timeScale; Time.timeScale = 0f; // a level-up can pause the game meanwhile
        try
        {
            Call(floating, "ShowGoldFeedbackSequence", list, 80);
            var entries = (IList)Field(floating, "goldFeedbackEntries");
            Assert.That(entries.Count, Is.EqualTo(2));
            string Text(int index) => (string)Property(((GameObject)entries[index]).GetComponent(tmpType), "text");
            Assert.That(Text(1), Does.EndWith("+0 gold"), "the counter starts at zero");
            float end = Time.realtimeSinceStartup + 2.5f; while (Time.realtimeSinceStartup < end) yield return null;
            Assert.That(Text(0), Is.EqualTo("<sprite name=\"GoldIcon\"> Defenses Intact\n+30 gold"));
            Assert.That(Text(1), Is.EqualTo("<sprite name=\"GoldIcon\"> Home Protected\n+50 gold"));
            for (int i = 0; i < 2; i++)
                Assert.That(Vector3.Distance(((GameObject)entries[i]).transform.localScale, Vector3.one), Is.LessThan(1e-3f), "line " + i + " ends at its layout scale");
        }
        finally { Time.timeScale = savedScale; }
        yield return new ExitPlayMode();
    }
    // Every level-up raises the plant limit, so every level shows its reward once before its upgrades.
    [UnityTest] public IEnumerator EveryLevelShowsItsRewardOnceBeforeItsUpgrades()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        Call(g.experience, "AddExperience", 750);
        int current = (int)Property(g.experience, "CurrentLevel");
        Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(current - 1));
        var rewardLevels = new List<int>(); var plainLevels = new List<int>();
        yield return WaitForChoosing(g.flow, 5f, "upgrade flow starts");
        var sessionBackdrop = SessionBackdrop(g.presenter); var snapshot = Field(sessionBackdrop, "blurTexture"); Assert.That(snapshot, Is.Not.Null);
        for (int step = 0; step < current - 1; step++)
        {
            yield return WaitIdle(g);
            yield return WaitBackdropOpaque(sessionBackdrop);
            AssertBackdropSteady(sessionBackdrop, snapshot, "pending level " + step);
            int pending = (int)Property(g.experience, "PendingChoices");
            int level = current - pending + 1;
            Assert.That(RewardHasContent(g, level), Is.True, "every level-up raises the plant limit: level " + level);
            if (RewardHasContent(g, level))
            {
                rewardLevels.Add(level);
                Assert.That(Property(g.reward, "IsVisible"), Is.True, "reward for level " + level);
                Assert.That(g.level.gameObject.activeSelf, Is.False);
                Assert.That((string)Property(Field(g.reward, "headingLabel"), "text"), Does.Contain(level.ToString()));
                Assert.That(((GameObject)Field(g.reward, "plantLimitSlot")).activeSelf, Is.True, "plant limit row for level " + level);
                Assert.That((string)Property(Field(g.reward, "plantLimitLabel"), "text"), Does.StartWith("Plant limit: +1"));
                Click((Component)Field(g.reward, "continueButton"));
                yield return WaitIdle(g);
                AssertBackdropSteady(sessionBackdrop, snapshot, "after the reward of level " + level);
                Assert.That(Property(g.reward, "IsVisible"), Is.False);
                yield return null; Assert.That(Property(g.reward, "IsVisible"), Is.False, "a level's reward is shown once");
            }
            else
            {
                plainLevels.Add(level);
                Assert.That(Property(g.reward, "IsVisible"), Is.False, "no reward for level " + level);
            }
            Assert.That(g.level.gameObject.activeSelf, Is.True, "upgrade choices for level " + level);
            var cards = (Array)Field(g.level, "cards");
            Click((Component)Field(cards.GetValue(0), "selectButton")); yield return null;
            Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(pending - 1));
        }
        Assert.That(plainLevels, Is.Empty); Assert.That(rewardLevels.Count, Is.EqualTo(current - 1));
        Assert.That(rewardLevels.Distinct().Count(), Is.EqualTo(rewardLevels.Count));
        Assert.That(Choosing(g), Is.False);
        yield return WaitBackdrop(sessionBackdrop, false);
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator InterruptedChoiceShowsItsRewardAgainAfterProgressionReset()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        Call(g.experience, "AddExperience", 100);
        yield return WaitForChoosing(g.flow, 5f, "upgrade flow starts");
        Assert.That(Property(g.reward, "IsVisible"), Is.True);
        Call(g.experience, "ResetProgression");
        yield return WaitIdle(g);
        Assert.That(Choosing(g), Is.False); Assert.That(Property(g.reward, "IsVisible"), Is.False);
        Assert.That(g.level.gameObject.activeSelf, Is.False);
        Time.timeScale = 1; Call(g.experience, "AddExperience", 100);
        yield return WaitForChoosing(g.flow, 5f, "upgrade flow restarts");
        Assert.That(Property(g.reward, "IsVisible"), Is.True, "level 2 reward shows again after the reset");
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator IsolatedPrefabPointerSmokeAndRenderedLayouts()
    {
        yield return new EnterPlayMode();
        yield return IsolatedSmoke();
        yield return new ExitPlayMode();
    }
    private static IEnumerator WaitOpen(Component view, float seconds = 3f)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end && (bool)Property(view, "IsAnimating")) yield return null;
        Assert.That(Property(view, "IsAnimating"), Is.False);
    }
    private static IEnumerator IsolatedSmoke()
    {
        var root = new GameObject("Isolated upgrade UI smoke", typeof(RectTransform), typeof(Canvas)); root.layer = 5;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        root.AddComponent(TypeOf("UnityEngine.UI.GraphicRaycaster", "UnityEngine.UI"));
        var events = new GameObject("Smoke EventSystem"); events.AddComponent(TypeOf("UnityEngine.EventSystems.EventSystem", "UnityEngine.UI"));
        var level = Panel("LevelUpPanel", root.transform); var shop = Panel("SpecialUpgradePanel", root.transform);
        int selected = -99, selections = 0, purchases = 0, closed = 0;
        Listen(level, "Selected", (Action<int>)(i => { selected = i; selections++; }));
        Listen(shop, "Purchased", (Action<int>)(i => purchases++)); Listen(shop, "Closed", (Action)(() => closed++));
        var choices = Data("LevelUpCardData", 3);
        string[] targets = { "Player", "Fireball", "Moonflower (seed)" };
        for (int i = 0; i < 3; i++)
        {
            SetField(choices[i], "Target", targets[i]); SetField(choices[i], "Name", "Blessed " + targets[i]);
            SetField(choices[i], "Stat", "AttackSpeed"); SetField(choices[i], "Bonus", "+12.5% base");
            SetField(choices[i], "Rarity", new[] { "COMMON", "RARE", "LEGENDARY" }[i]); SetField(choices[i], "Preview", "1.25 → 1.406 attacks/s");
        }
        Call(level, "Show", choices, 3); Call(level, "Show", choices, 3); yield return null;
        yield return WaitOpen(level);
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) }) yield return Capture(canvas, level.gameObject, "isolated-levelup", size.x, size.y);
        var cards = (Array)Field(level, "cards");
        yield return ButtonStates((Component)Field(cards.GetValue(2), "selectButton"));
        Click((Component)Field(cards.GetValue(1), "selectButton"));
        Assert.That(selections, Is.EqualTo(1)); Assert.That(selected, Is.EqualTo(1));
        Call(level, "Show", Data("LevelUpCardData", 0), 1); yield return null;
        yield return Capture(canvas, level.gameObject, "isolated-empty", 1920, 1080);
        Click((Component)Field(level, "continueButton")); Assert.That(selected, Is.EqualTo(-1)); Assert.That(selections, Is.EqualTo(2)); Call(level, "HideImmediate");
        var items = Data("SpecialUpgradeItemData", 2);
        for (int i = 0; i < 2; i++)
        {
            SetField(items[i], "Target", "Fireball"); SetField(items[i], "Name", "Piercing embers"); SetField(items[i], "Description", "Projectiles pierce one additional enemy.");
            SetField(items[i], "Costs", "Wood 120/20  Crystal 35/10  Stone 60/15"); SetField(items[i], "Stacks", i + "/3"); SetField(items[i], "Prerequisites", "Requires: burning_embers");
            SetField(items[i], "CanPurchase", i == 0);
        }
        Call(shop, "Show", items); Call(shop, "Show", items); yield return null;
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) }) yield return Capture(canvas, shop.gameObject, "isolated-shop", size.x, size.y);
        var rows = (IList)Field(shop, "rows"); Click((Component)Field(rows[1], "purchaseButton")); Assert.That(purchases, Is.Zero);
        Click((Component)Field(rows[0], "purchaseButton")); Assert.That(purchases, Is.EqualTo(1));
        Click((Component)Field(shop, "closeButton")); Assert.That(closed, Is.EqualTo(1));
        Call(shop, "Hide"); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(events); yield return null;
    }
    private static object RewardData(int level, string plantName)
    {
        var reward = Activator.CreateInstance(TypeOf("LevelUnlockReward"), level);
        var plant = ScriptableObject.CreateInstance(TypeOf("PlantDataSO")); plant.GetType().GetField("plantName").SetValue(plant, plantName);
        reward.GetType().GetMethod("AddPlant", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(reward, new object[] { plant });
        return reward;
    }
    // Opening and closing run on unscaled time (timeScale is 0 during a level-up), end exactly at alpha 1 / scale 1 (open) or inactive (closed),
    // and keep the modal non-interactive while they run.
    [UnityTest] public IEnumerator LevelUpModalsTweenInAndOutOnUnscaledTimeAndBlockInputWhileTheyRun()
    {
        yield return new EnterPlayMode();
        yield return IsolatedTweens();
        yield return new ExitPlayMode();
    }
    private static IEnumerator IsolatedTweens()
    {
        var root = new GameObject("Isolated tween smoke", typeof(RectTransform), typeof(Canvas)); root.layer = 5;
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.AddComponent(TypeOf("UnityEngine.UI.GraphicRaycaster", "UnityEngine.UI"));
        var events = new GameObject("Tween EventSystem"); events.AddComponent(TypeOf("UnityEngine.EventSystems.EventSystem", "UnityEngine.UI"));
        float oldScale = Time.timeScale; Time.timeScale = 0;
        try
        {
            // ---- reward card ----
            var reward = Panel("LevelUpReward", root.transform); Call(reward, "HideImmediate");
            int continued = 0; Listen(reward, "Continued", (Action)(() => continued++));
            var card = (RectTransform)Field(reward, "content"); var group = (CanvasGroup)Field(reward, "group");
            Assert.That(card, Is.Not.Null); Assert.That(group, Is.Not.Null);
            Assert.That(Call(reward, "Show", RewardData(3, "Sunflower")), Is.True);
            Assert.That(Property(reward, "IsAnimating"), Is.True);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(group.alpha, Is.InRange(.05f, .95f), "fading in on unscaled time");
            // OutBack overshoots 1 by at most ~1.6% of the 0.85 -> 1 span, and an editor frame hitch can land the sample there.
            Assert.That(card.localScale.x, Is.InRange(.8f, 1.02f), "scaling up from the smaller size");
            Assert.That(group.interactable, Is.False);
            Click((Component)Field(reward, "continueButton")); Assert.That(continued, Is.Zero, "Continue is blocked while the card pops in");
            yield return WaitOpen(reward);
            Assert.That(group.alpha, Is.EqualTo(1f)); Assert.That(card.localScale, Is.EqualTo(Vector3.one));
            Assert.That(group.interactable, Is.True);
            Assert.That(Property(reward, "IsVisible"), Is.True);
            Click((Component)Field(reward, "continueButton")); Click((Component)Field(reward, "continueButton"));
            Assert.That(Property(reward, "IsAnimating"), Is.True); Assert.That(Property(reward, "IsVisible"), Is.True, "still visible (and blocking selection) while it closes");
            Assert.That(continued, Is.Zero, "Continued is raised once the card is gone");
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(group.alpha, Is.LessThan(1f)); Assert.That(Mathf.Abs(card.localScale.x - 1f), Is.GreaterThan(.001f), "InBack: anticipates, then shrinks");
            yield return WaitOpen(reward);
            Assert.That(Property(reward, "IsVisible"), Is.False); Assert.That(reward.gameObject.activeSelf, Is.False);
            Assert.That(continued, Is.EqualTo(1));
            Call(reward, "Hide"); // inactive: immediate, safe
            Assert.That(Property(reward, "IsAnimating"), Is.False); Assert.That(Property(reward, "IsVisible"), Is.False);

            // ---- level-up panel ----
            var level = Panel("LevelUpPanel", root.transform); Call(level, "HideImmediate");
            int selections = 0, hidden = 0;
            Listen(level, "Selected", (Action<int>)(i => selections++)); Listen(level, "Hidden", (Action)(() => hidden++));
            var window = (RectTransform)Field(level, "content"); var levelGroup = (CanvasGroup)Field(level, "group");
            Assert.That(window, Is.Not.Null); Assert.That(levelGroup, Is.Not.Null);
            var cards = (Array)Field(level, "cards");
            Assert.That(Call(level, "Show", Data("LevelUpCardData", 3), 3), Is.True);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(levelGroup.alpha, Is.InRange(.05f, .95f)); Assert.That(window.localScale.x, Is.InRange(.8f, 1.02f));
            Click((Component)Field(cards.GetValue(0), "selectButton")); Assert.That(selections, Is.Zero, "cards cannot be picked while the panel opens");
            yield return WaitOpen(level);
            Assert.That(levelGroup.alpha, Is.EqualTo(1f)); Assert.That(window.localScale, Is.EqualTo(Vector3.one));
            Click((Component)Field(cards.GetValue(1), "selectButton")); Assert.That(selections, Is.EqualTo(1));
            Call(level, "Hide");
            Assert.That(Property(level, "IsAnimating"), Is.True); Assert.That(Property(level, "IsVisible"), Is.True);
            Click((Component)Field(cards.GetValue(2), "selectButton")); Assert.That(selections, Is.EqualTo(1), "cards cannot be picked while the panel closes");
            yield return WaitOpen(level);
            Assert.That(Property(level, "IsVisible"), Is.False); Assert.That(level.gameObject.activeSelf, Is.False);
            Assert.That(hidden, Is.Zero, "closing through Hide never reports an external disable");
        }
        finally { Time.timeScale = oldScale; UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(events); }
        yield return null;
    }
    // The rows enter one by one after the card pops in, the plant limit counts from the previous limit to the new one, and Continue stays
    // blocked (IsAnimating) until all of it finished. Unscaled time: timeScale is 0 like during a level-up.
    [UnityTest] public IEnumerator RewardRowsEnterOneByOneAndThePlantLimitCountsUntilContinueUnlocks()
    {
        yield return new EnterPlayMode();
        var root = new GameObject("Isolated reward rows", typeof(RectTransform), typeof(Canvas)); root.layer = 5;
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.AddComponent(TypeOf("UnityEngine.UI.GraphicRaycaster", "UnityEngine.UI"));
        var events = new GameObject("Rows EventSystem"); events.AddComponent(TypeOf("UnityEngine.EventSystems.EventSystem", "UnityEngine.UI"));
        float oldScale = Time.timeScale; Time.timeScale = 0;
        try
        {
            var reward = Panel("LevelUpReward", root.transform); Call(reward, "HideImmediate");
            int continued = 0; Listen(reward, "Continued", (Action)(() => continued++));
            var data = RewardData(3, "Sunflower");
            data.GetType().GetMethod("AddPlantLimit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(data, new object[] { 1, 6 });
            Assert.That(Call(reward, "Show", data), Is.True);
            var seed = (GameObject)Field(reward, "seedSlot"); var limit = (GameObject)Field(reward, "plantLimitSlot"); var label = Field(reward, "plantLimitLabel");
            var seedIcon = ((Component)Field(reward, "seedIcon")).transform; var labelTransform = ((Component)label).transform;
            Assert.That(Property(reward, "IsAnimating"), Is.True);
            Assert.That(seed.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f), "rows wait for the card pop");
            Assert.That(limit.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f));
            Assert.That((string)Property(label, "text"), Is.EqualTo("Plant limit: +1 (5)"), "starts from the previous limit");
            Click((Component)Field(reward, "continueButton")); Assert.That(continued, Is.Zero);
            // Rows now finish individually and include reading gaps plus a final hold (about 3.1s for all three).
            float end = Time.realtimeSinceStartup + 5f, seedStart = -1f, limitStart = -1f, seedSettled = -1f; bool continuedWhileAnimating = false, sawPreviousLimit = false;
            while (Time.realtimeSinceStartup < end && (bool)Property(reward, "IsAnimating"))
            {
                float now = Time.realtimeSinceStartup;
                if (seedStart < 0 && seed.GetComponent<CanvasGroup>().alpha > .02f) seedStart = now;
                if (seedSettled < 0 && seed.GetComponent<CanvasGroup>().alpha >= .999f && Vector3.Distance(seedIcon.localScale, Vector3.one) < .001f) seedSettled = now;
                if (limitStart < 0 && limit.GetComponent<CanvasGroup>().alpha > .02f) limitStart = now;
                sawPreviousLimit |= (string)Property(label, "text") == "Plant limit: +1 (5)";
                Click((Component)Field(reward, "continueButton")); continuedWhileAnimating |= continued > 0;
                yield return null;
            }
            Assert.That(Property(reward, "IsAnimating"), Is.False, "rows settled");
            Assert.That(continuedWhileAnimating, Is.False, "Continue is blocked until the rows finish");
            Assert.That(seedStart, Is.GreaterThan(0f)); Assert.That(limitStart, Is.GreaterThan(0f));
            Assert.That(seedStart, Is.LessThanOrEqualTo(limitStart), "seed enters before the plant limit");
            Assert.That(seedSettled, Is.GreaterThan(0f));
            Assert.That(limitStart - seedSettled, Is.GreaterThanOrEqualTo((float)Field(reward, "rowReadingGap") - .1f), "there is a visible reading gap after the first row settles");
            Assert.That(sawPreviousLimit, Is.True);
            Assert.That((string)Property(label, "text"), Is.EqualTo("Plant limit: +1 (6)"));
            foreach (var row in new[] { seed, limit }) { Assert.That(row.transform.localScale, Is.EqualTo(Vector3.one)); Assert.That(row.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f)); }
            Assert.That(seedIcon.localScale, Is.EqualTo(Vector3.one)); Assert.That(labelTransform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Property(reward, "IsVisible"), Is.True);
            Click((Component)Field(reward, "continueButton"));
            Assert.That(Property(reward, "IsAnimating"), Is.True, "closing");
            yield return WaitOpen(reward);
            Assert.That(continued, Is.EqualTo(1));
        }
        finally { Time.timeScale = oldScale; UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(events); }
        yield return null;
        yield return new ExitPlayMode();
    }
    private static Texture2D RenderTo(Camera camera, int width, int height)
    {
        var rt = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            return texture;
        }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = previous; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    }
    private static float Luma(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;
    // Mean luminance of the icon's own pixels (frame with the icon minus frame without it) and the mean contrast against what was behind them.
    private static void MeasureIcon(Camera main, Transform player, SpriteRenderer icon, string name, out float luminance, out float contrast, out int pixels)
    {
        var go = new GameObject("Indicator measure camera"); var close = go.AddComponent<Camera>();
        try
        {
            close.CopyFrom(main); close.orthographicSize = 3.4f;
            close.transform.position = new Vector3(player.position.x, (player.position.y + icon.transform.position.y) / 2f, main.transform.position.z);
            const int w = 1280, h = 720;
            var with = RenderTo(close, w, h);
            icon.enabled = false;
            var without = RenderTo(close, w, h);
            icon.enabled = true;
            Directory.CreateDirectory(Output);
            File.WriteAllBytes(Output + name + "-" + w + "x" + h + ".png", with.EncodeToPNG());
            var a = with.GetPixels(); var b = without.GetPixels();
            float lum = 0, diff = 0; pixels = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) < .05f) continue;
                pixels++; lum += Luma(a[i]); diff += Mathf.Abs(Luma(a[i]) - Luma(b[i]));
            }
            luminance = pixels > 0 ? lum / pixels : 0; contrast = pixels > 0 ? diff / pixels : 0;
            UnityEngine.Object.DestroyImmediate(with); UnityEngine.Object.DestroyImmediate(without);
            Debug.Log("UI_ART_INDICATOR " + name + " pixels=" + pixels + " luminance=" + luminance + " contrast=" + contrast);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    // The global Light2D dims lit sprites at night (0.3 intensity, blue); the highlighted world sprites are unlit, so the indicator must keep its brightness.
    [UnityTest] public IEnumerator IndicatorKeepsItsBrightnessAndContrastFromDayToNight()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        var lights = Find("DayNightLightController"); Assert.That(lights, Is.Not.Null);
        var update = lights.GetType().GetMethod("UpdateLightBasedOnGameState", BindingFlags.Instance | BindingFlags.NonPublic);
        var icon = (SpriteRenderer)Field(g.indicator, "icon"); var main = Camera.main;
        var luminance = new float[2]; var contrast = new float[2]; var phases = new[] { "Day", "Night" };
        for (int i = 0; i < 2; i++)
        {
            update.Invoke(lights, new object[] { Enum.Parse(TypeOf("GamePhase"), phases[i]), false });
            yield return null;
            Call(g.indicator, "Play"); Call(g.indicator, "Tick", .75f); // in the hold phase: fully opaque
            MeasureIcon(main, ((Component)g.experience).transform, icon, "runtime-levelup-indicator-" + phases[i].ToLowerInvariant(), out luminance[i], out contrast[i], out int pixels);
            Assert.That(pixels, Is.GreaterThan(100), phases[i] + ": the icon is in frame");
            Call(g.indicator, "Stop");
        }
        Assert.That(luminance[1], Is.GreaterThanOrEqualTo(luminance[0] * .8f), "night does not dim the icon");
        Assert.That(contrast[0], Is.GreaterThanOrEqualTo(.12f), "readable against the day world");
        Assert.That(contrast[1], Is.GreaterThanOrEqualTo(.12f), "readable against the night world");
        yield return new ExitPlayMode();
    }
    private static Component Backdrop(Component owner, string field) => (Component)Field(owner, field);
    private static IEnumerator WaitBackdrop(Component backdrop, bool shown, float seconds = 2f)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end && ((bool)Property(backdrop, "IsShown") != shown || (!shown && backdrop.gameObject.activeSelf))) yield return null;
        Assert.That(Property(backdrop, "IsShown"), Is.EqualTo(shown)); Assert.That(backdrop.gameObject.activeSelf, Is.EqualTo(shown));
    }
    // The blur is a real snapshot of the world, not the tint: it exists while the backdrop shows and is released when it hides.
    private static void AssertBlurred(Component backdrop, string what)
    {
        Assert.That(Property(backdrop, "HasBlur"), Is.True, what + ": blur captured");
        var raw = (Behaviour)Field(backdrop, "blurImage"); Assert.That(raw.enabled, Is.True);
        var texture = (RenderTexture)Field(backdrop, "blurTexture"); Assert.That(texture, Is.Not.Null);
        Assert.That(texture.width, Is.LessThan(Screen.width), "downsampled");
        var previous = RenderTexture.active; var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = texture; copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply();
            var pixels = copy.GetPixels(); float mean = 0; float maxStep = 0;
            for (int i = 0; i < pixels.Length; i++) mean += Luma(pixels[i]);
            for (int y = 0; y < copy.height; y++)
                for (int x = 1; x < copy.width; x++) maxStep = Mathf.Max(maxStep, Mathf.Abs(Luma(pixels[y * copy.width + x]) - Luma(pixels[y * copy.width + x - 1])));
            mean /= pixels.Length;
            Debug.Log("UI_ART_BLUR " + what + " size=" + texture.width + "x" + texture.height + " meanLuma=" + mean + " maxHorizontalStep=" + maxStep);
            Assert.That(mean, Is.GreaterThan(.02f), what + ": the snapshot contains the world, not black");
            Assert.That(maxStep, Is.LessThan(.12f), what + ": no hard edges survive the blur");
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(copy); }
    }
    [UnityTest] public IEnumerator BackdropShowsABlurredSnapshotOfTheWorldForTheInventoryAndTheReward()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        var ui = Find("UIManager"); var inventory = Property(ui, "Inventory"); var controller = Find("GameStateUIController");
        var inventoryBackdrop = Backdrop(controller, "inventoryBackdrop");
        Call(inventory, "ToggleInventory");
        yield return WaitBackdrop(inventoryBackdrop, true);
        yield return new WaitForSecondsRealtime(1.5f);
        AssertBlurred(inventoryBackdrop, "inventory");
        yield return Capture(controller.GetComponentInParent<Canvas>().rootCanvas, inventoryBackdrop.gameObject, "runtime-inventory-blur", 1920, 1080, false, true);
        // The book animation must finish before the inventory accepts a close request.
        for (int attempt = 0; attempt < 8 && (bool)Property(inventoryBackdrop, "IsShown"); attempt++)
        {
            Call(inventory, "CloseInventory");
            yield return new WaitForSecondsRealtime(.6f);
        }
        yield return WaitBackdrop(inventoryBackdrop, false);
        Assert.That(Property(inventoryBackdrop, "HasBlur"), Is.False, "RenderTexture released on hide");
        Assert.That(((Behaviour)Field(inventoryBackdrop, "blurImage")).enabled, Is.False);
        Call(g.experience, "AddExperience", 100);
        yield return WaitForChoosing(g.flow, 5f, "upgrade flow starts");
        var sessionBackdrop = SessionBackdrop(g.presenter);
        Assert.That(Property(g.reward, "IsVisible"), Is.True);
        yield return WaitIdle(g);
        yield return WaitBackdropOpaque(sessionBackdrop);
        AssertBlurred(sessionBackdrop, "reward");
        var snapshot = Field(sessionBackdrop, "blurTexture");
        Click((Component)Field(g.reward, "continueButton"));
        yield return WaitIdle(g);
        AssertBlurred(sessionBackdrop, "upgrades");
        AssertBackdropSteady(sessionBackdrop, snapshot, "upgrades share the reward's blur");
        yield return Capture(g.canvas, g.level.gameObject, "runtime-upgrades-over-blur", 1920, 1080, false, true);
        Click((Component)Field(((Array)Field(g.level, "cards")).GetValue(0), "selectButton"));
        yield return WaitBackdrop(sessionBackdrop, false);
        Assert.That(Property(sessionBackdrop, "HasBlur"), Is.False);
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator MissingIndicatorNeverBlocksTheUpgradeFlow()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        SetField(g.runtime, "indicator", null);
        float start = Time.realtimeSinceStartup;
        Call(g.experience, "AddExperience", 100);
        yield return WaitForChoosing(g.flow, .9f, "flow opens without waiting for an indicator");
        Assert.That(Property(g.indicator, "IsPlaying"), Is.True, "the (ignored) indicator is still animating");
        Assert.That(Time.realtimeSinceStartup - start, Is.LessThan(.9f));
        Assert.That(Property(g.reward, "IsVisible"), Is.True);
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator GameSceneRuntimeChoiceShopPurchaseAndModalRelease()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var tutorial = Find("TutorialManager"); if (tutorial != null) Call(tutorial, "SkipTutorial"); // Test fixture bypass only.
        var flow = Find("GameFlowController"); Call(flow, "SetPhase", Enum.Parse(TypeOf("GamePhase"), "Day"));
        var ui = Find("UIManager"); var modal = Property(ui, "Flow");
        var currentModal = Property(modal, "CurrentModal"); if (currentModal.ToString() != "None") Call(modal, "Close", currentModal);
        var runtime = Find("UpgradeRuntime"); var presenter = Find("UpgradePanel"); var experience = Find("PlayerExperienceSystem");
        Assert.That(runtime, Is.Not.Null); Assert.That(((Behaviour)runtime).enabled, Is.True); Assert.That(Property(presenter, "IsReady"), Is.True);
        Call(experience, "ResetProgression"); Time.timeScale = 1; Call(experience, "AddExperience", 150);
        yield return WaitForChoosing(flow, 5f, "level-up indicator finishes, then the flow opens");
        Assert.That(Property(flow, "IsChoosingUpgrade"), Is.True); Assert.That(Time.timeScale, Is.Zero);
        Assert.That(Property(modal, "CurrentModal").ToString(), Is.EqualTo("LevelUp"));
        var suspended = ((IEnumerable)Field(runtime, "suspendedInteractions")).Cast<Behaviour>().ToArray();
        Assert.That(suspended.Length, Is.GreaterThan(0));
        foreach (var consumer in suspended) Assert.That(consumer.enabled, Is.False);
        var input = Find("InputReader"); Assert.That(Property(input, "MoveInput"), Is.EqualTo(Vector2.zero));
        Assert.That(Call(modal, "Open", Enum.Parse(TypeOf("UIModal"), "Inventory")), Is.False);
        var level = (Component)Field(presenter, "levelUpView"); var canvas = presenter.GetComponent<Canvas>();
        var cards = (Array)Field(level, "cards"); int pending = (int)Property(experience, "PendingChoices");
        // Level 2 unlocked content: the reward modal is shown alone first; the upgrade panel only after Continue.
        var reward = (Component)Field(presenter, "rewardView");
        Assert.That(Property(reward, "IsVisible"), Is.True);
        AssertRewardHierarchy(presenter, reward, level);
        yield return WaitIdle(reward, level);
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) }) yield return Capture(canvas, reward.gameObject, "runtime-reward-alone", size.x, size.y, false);
        Call(runtime, "Choose", 0); yield return null;
        Assert.That(Property(experience, "PendingChoices"), Is.EqualTo(pending), "choices are blocked while the reward is visible");
        Click((Component)Field(reward, "continueButton"));
        yield return WaitIdle(reward, level);
        Assert.That(Property(reward, "IsVisible"), Is.False);
        Assert.That(level.gameObject.activeSelf, Is.True);
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) }) yield return Capture(canvas, level.gameObject, "runtime-levelup", size.x, size.y);
        Click((Component)Field(cards.GetValue(0), "selectButton")); yield return null;
        Assert.That(Property(experience, "PendingChoices"), Is.EqualTo(pending - 1));
        Assert.That(Property(flow, "IsChoosingUpgrade"), Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
        foreach (var consumer in suspended) Assert.That(consumer.enabled, Is.True);
        Assert.That(Property(modal, "CurrentModal").ToString(), Is.EqualTo("None"));
        Call(runtime, "ToggleShop"); yield return null;
        Assert.That(Property(presenter, "IsShopOpen"), Is.True); Assert.That(Property(modal, "CurrentModal").ToString(), Is.EqualTo("Crafting"));
        var shop = (Component)Field(presenter, "shopView"); var rows = (IList)Field(shop, "rows"); Assert.That(rows.Count, Is.GreaterThan(0));
        var definitions = (IList)Field(presenter, "shopDefinitions"); var targetIds = (IList)Field(presenter, "shopTargets");
        var definition = (SpecialDefinition)definitions[0]; string target = (string)targetIds[0];
        var state = (RunState)Property(runtime, "State"); int stacks = state.SpecialStacks(definition.id, target);
        var inventory = Find("InventoryManager");
        // Ensure the fixture proves disabled pointer clicks before granting exactly the production costs.
        var costs = CostPlanner.Aggregate(definition.costs, out var aggregated) ? aggregated : throw new InvalidOperationException("Invalid costs");
        foreach (var cost in costs) Drain(inventory, cost.Key);
        Call(presenter, "ShowShop"); yield return null;
        Click((Component)Field(rows[0], "purchaseButton")); Assert.That(state.SpecialStacks(definition.id, target), Is.EqualTo(stacks));
        foreach (var cost in costs) Grant(inventory, cost.Key, cost.Value);
        Call(presenter, "ShowShop"); yield return null;
        Assert.That(Property(Field(rows[0], "purchaseButton"), "interactable"), Is.True);
        Assert.That(((string)Property(Field(rows[0], "costsLabel"), "text")).Trim(), Does.StartWith("<sprite name=\"GoldIcon\"> 200/200 + <sprite name=\""), "Cauldron upgrades cost gold (shown first, with the gold icon) + materials with their icons");
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) }) yield return Capture(canvas, shop.gameObject, "runtime-shop", size.x, size.y);
        Click((Component)Field(rows[0], "purchaseButton")); yield return null;
        Assert.That(state.SpecialStacks(definition.id, target), Is.EqualTo(stacks + 1));
        foreach (var cost in costs) Assert.That(Amount(inventory, cost.Key), Is.EqualTo(0));
        Click((Component)Field(shop, "closeButton")); yield return null;
        Assert.That(Property(presenter, "IsShopOpen"), Is.False); Assert.That(shop.gameObject.activeSelf, Is.False);
        Assert.That(Property(modal, "CurrentModal").ToString(), Is.EqualTo("None")); Assert.That(Time.timeScale, Is.EqualTo(1));
        Debug.Log("UI_ART_RUNTIME: Game scene pointer selection, disabled purchase, resource spending, stack increment, close, timeScale/input/modal assertions passed; tutorial skipped only in fixture.");
        yield return new ExitPlayMode();
    }
}
